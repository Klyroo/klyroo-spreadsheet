namespace Klyroo.Spreadsheet;

// Submitting: batches go to the host one after the other, and what the host accepted is recorded batch by batch, so a failure
// or a cancellation in the middle never makes a retry send the accepted rows again.
public sealed partial class SpreadsheetImporter
{
    private readonly HashSet<int> _accepted = [];
    private CancellationTokenSource? _submitCts;

    /// <summary>The <see cref="SpreadsheetRow.Index"/> of every row a batch sent by <see cref="SubmitAsync"/> or <see cref="RetryRejectedAsync"/> got accepted. It survives a failed or cancelled submit and is what a retry leaves out. A new file, sheet or header row, and <see cref="ResetSubmit"/>, empty it.</summary>
    public IReadOnlySet<int> AcceptedRowIndexes => _accepted;

    /// <summary>The rows whose index is in <see cref="AcceptedRowIndexes"/>, in row order, as the importer currently sees them.</summary>
    public IReadOnlyList<SpreadsheetRow> AcceptedRows => _accepted.Where(i => i >= 0 && i < _rowArr.Length).Order().Select(i => _rowArr[i]).ToList();

    /// <summary>The importable rows that no accepted batch has covered: what the next <see cref="SubmitAsync"/> sends.</summary>
    public IReadOnlyList<SpreadsheetRow> PendingRows => ImportableRows.Where(r => !_accepted.Contains(r.Index)).ToList();

    /// <summary>
    /// Sends every importable row that was not accepted yet to <see cref="SpreadsheetSchema.OnSubmitAsync"/> (or <see cref="SpreadsheetSchema.OnSubmit"/>) in batches of
    /// <see cref="SpreadsheetSchema.BatchSize"/>. It first calls <see cref="SpreadsheetColumnSchema.OnCreateOption"/> once for each value
    /// answered with Create. Rejected rows are kept as errors on their row. Calling it again after a failure or a cancellation resumes: the rows already accepted are not sent twice.
    /// An exception from a callback is caught and reported in <see cref="SubmitError"/>; a cancellation ends in <see cref="SpreadsheetSubmitStatus.Cancelled"/>.
    /// Does nothing while a submit is running or when <see cref="CanSubmit"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the submit. The batch in flight is cancelled through the token given to <see cref="SpreadsheetSchema.OnSubmitAsync"/> and counts as not accepted. <see cref="CancelSubmit"/> does the same.</param>
    /// <returns>A task that completes when the submit has finished, failed or been cancelled.</returns>
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (SubmitStatus == SpreadsheetSubmitStatus.Running || !CanSubmit) return;
        await SendRowsAsync(() => PendingRows, cancellationToken);
    }

    /// <summary>Sends again the rows the server rejected, that are still importable. The rejection issues are cleared before the send. Does nothing while a submit is running or when nothing was rejected.</summary>
    /// <param name="cancellationToken">Cancels the retry, like for <see cref="SubmitAsync"/>.</param>
    /// <returns>A task that completes when the retry has finished, failed or been cancelled.</returns>
    public async Task RetryRejectedAsync(CancellationToken cancellationToken = default)
    {
        if (SubmitStatus == SpreadsheetSubmitStatus.Running || _serverRejections.Count == 0) return;
        var rejectedIndexes = _serverRejections.Keys.ToHashSet();
        _serverRejections.Clear();
        RefinalizeRows(rejectedIndexes);
        await SendRowsAsync(() => PendingRows.Where(r => rejectedIndexes.Contains(r.Index)).ToList(), cancellationToken);
    }

    /// <summary>Asks the running submit to stop before its next batch and cancels the batch in flight. The rows accepted so far stay accepted, and <see cref="SubmitAsync"/> resumes with the rest. Does nothing when no submit is running.</summary>
    public void CancelSubmit() => _submitCts?.Cancel();

    /// <summary>Forgets what was accepted and rejected, and returns <see cref="SubmitStatus"/> to <see cref="SpreadsheetSubmitStatus.Idle"/>, so the next <see cref="SubmitAsync"/> sends every importable row again. Does nothing while a submit is running.</summary>
    public void ResetSubmit()
    {
        if (SubmitStatus == SpreadsheetSubmitStatus.Running) return;
        var rejected = _serverRejections.Keys.ToList();
        ResetSubmitState();
        RefinalizeRows(rejected);
        Changed?.Invoke();
    }

    // Empties the submit bookkeeping. Used when the rows themselves change meaning (a new file, sheet or header row).
    private void ResetSubmitState()
    {
        if (SubmitStatus != SpreadsheetSubmitStatus.Running) SubmitStatus = SpreadsheetSubmitStatus.Idle;
        _accepted.Clear();
        _serverRejections.Clear();
        SubmitDone = 0;
        SubmitTotal = 0;
        SubmitError = null;
    }

    private async Task SendRowsAsync(Func<IReadOnlyList<SpreadsheetRow>> selectRows, CancellationToken external)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        _submitCts = cts;
        var token = cts.Token;
        SubmitStatus = SpreadsheetSubmitStatus.Running;
        SubmitDone = 0;
        SubmitTotal = 0;
        SubmitError = null;
        Changed?.Invoke();

        var touched = new HashSet<int>();
        try
        {
            // Step 1: create the new options once each. The returned option becomes a Map answer, so the rows carry
            // its value and a later send does not create it again.
            await CreateAnsweredOptionsAsync(token);

            var rowsToSend = selectRows();
            SubmitTotal = rowsToSend.Count;
            Changed?.Invoke();

            // Step 2: send in batches, recording each batch as soon as the host has answered.
            var batchSize = Schema.BatchSize > 0 ? Schema.BatchSize : Math.Max(1, rowsToSend.Count);
            for (var offset = 0; offset < rowsToSend.Count; offset += batchSize)
            {
                token.ThrowIfCancellationRequested();
                var batch = rowsToSend.Skip(offset).Take(batchSize).ToList();
                IReadOnlyList<SpreadsheetRejectedRow>? rejected = null;
                if (Schema.OnSubmitAsync is { } submitAsync)
                {
                    rejected = await submitAsync(batch, token);
                }
                else if (Schema.OnSubmit is { } submit)
                {
                    rejected = await submit(batch);
                }

                var rejectedIndexes = rejected is { Count: > 0 } ? rejected.Select(r => r.RowIndex).ToHashSet() : [];
                foreach (var row in batch)
                {
                    if (!rejectedIndexes.Contains(row.Index)) _accepted.Add(row.Index);
                }
                foreach (var rej in rejected ?? [])
                {
                    if (!_serverRejections.TryGetValue(rej.RowIndex, out var list))
                    {
                        list = [];
                        _serverRejections[rej.RowIndex] = list;
                    }
                    list.Add(new SpreadsheetRowIssue(rej.Field, "server", SpreadsheetIssueLevel.Error, rej.Message));
                    touched.Add(rej.RowIndex);
                }

                SubmitDone = Math.Min(rowsToSend.Count, offset + batch.Count);
                Changed?.Invoke();
            }

            SubmitStatus = SpreadsheetSubmitStatus.Done;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            SubmitStatus = SpreadsheetSubmitStatus.Cancelled;
        }
        catch (Exception ex)
        {
            SubmitError = ex.Message;
            SubmitStatus = SpreadsheetSubmitStatus.Error;
        }
        finally
        {
            _submitCts = null;
            RefinalizeRows(touched);
            Changed?.Invoke();
        }
    }

    // Runs the create-option callback for each value answered with Create, and records the returned option as a Map answer.
    private async Task CreateAnsweredOptionsAsync(CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            foreach (var q in _questions.Where(q => q.Answer?.Action == SpreadsheetValueAnswerAction.Create).ToList())
            {
                var col = Schema.Columns.FirstOrDefault(c => c.Key == q.FieldKey);
                SpreadsheetOption? option;
                if (col?.OnCreateOption is { } create)
                {
                    option = create(q.RawValue);
                }
                else
                {
                    continue;
                }
                var value = string.IsNullOrEmpty(option?.Value) ? q.RawValue : option.Value;
                _valueAnswers[q.Id] = new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, value, option?.Label ?? value);
                created = true;
            }
        }
        finally
        {
            if (created) Recompute();
        }
    }
}
