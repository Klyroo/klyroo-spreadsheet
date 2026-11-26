namespace Klyroo.Spreadsheet;

// The checks that need the host's data: the lookup of existing records by key and the batched row validation. They run in batches
// with a cancellation token, and what they learn is kept per row, so editing a row asks again for that row only.
public sealed partial class SpreadsheetImporter
{
    private readonly Dictionary<int, List<SpreadsheetRowIssue>> _asyncIssues = [];
    private readonly HashSet<int> _asyncChecked = [];
    private readonly HashSet<string> _lookedUpKeys = new(StringComparer.Ordinal);
    private readonly List<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> _lookedUpRecords = [];
    private bool _asyncDirty = true;
    private int _asyncStamp;

    // Anything that makes a row unchecked calls this. A run clears the dirty flag only when the stamp did not move while it ran.
    private void MarkAsyncDirty()
    {
        _asyncDirty = true;
        _asyncStamp++;
    }

    // Whether a send must run the checks first: unlike NeedsAsyncValidation, a failed or cancelled run does not hide it, so a retry checks again.
    private bool AsyncChecksPending => HasAsyncChecks && FileLoaded && _asyncDirty;
    private CancellationTokenSource? _validateCts;

    /// <summary>The state of the last <see cref="ValidateAsync"/> run (or of the check <see cref="SubmitAsync"/> ran before sending).</summary>
    public SpreadsheetValidationStatus ValidationStatus { get; private set; } = SpreadsheetValidationStatus.Idle;

    /// <summary>The message of the exception thrown by an async check callback, or <see langword="null"/>.</summary>
    public string? ValidationError { get; private set; }

    /// <summary><see langword="true"/> when the schema defines <see cref="SpreadsheetSchema.LookupExistingRecordsAsync"/> or <see cref="SpreadsheetSchema.ValidateRowsAsync"/>.</summary>
    public bool HasAsyncChecks => Schema.ValidateRowsAsync is not null || Schema.LookupExistingRecordsAsync is not null;

    /// <summary><see langword="true"/> when async checks are defined, a file is loaded, rows changed since they last completed, and no check is running or has failed (a failed run is repeated by <see cref="ValidateAsync"/> or by <see cref="SubmitAsync"/>). <c>KlySpreadsheetImport</c> runs the checks itself when this is <see langword="true"/> on the review step.</summary>
    public bool NeedsAsyncValidation =>
        HasAsyncChecks && FileLoaded && _asyncDirty
        && ValidationStatus is not (SpreadsheetValidationStatus.Running or SpreadsheetValidationStatus.Error or SpreadsheetValidationStatus.Cancelled);

    /// <summary>
    /// Runs the async checks the schema defines, on the rows that were not checked yet: first the existing-records lookup for the row keys that were not looked up
    /// (<see cref="SpreadsheetSchema.LookupExistingRecordsAsync"/>, which sets each row's <see cref="SpreadsheetRow.Mode"/> and applies <see cref="SpreadsheetSchema.ExistingAction"/>),
    /// then the batched validation of the rows that are not discarded and have no error (<see cref="SpreadsheetSchema.ValidateRowsAsync"/>).
    /// Editing a row, or changing the column mapping or an answer, makes it unchecked again. Rows already checked are not sent twice, so calling it repeatedly is cheap.
    /// An exception is caught and reported in <see cref="ValidationError"/>; the results of the batches that finished are kept.
    /// </summary>
    /// <param name="cancellationToken">Cancels the checks. <see cref="CancelValidation"/> does the same.</param>
    /// <returns>A task that completes when the checks have finished, failed or been cancelled. Does nothing without async checks, without a file, while a check or a submit is running.</returns>
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (!HasAsyncChecks || !FileLoaded || ValidationStatus == SpreadsheetValidationStatus.Running || SubmitStatus == SpreadsheetSubmitStatus.Running) return;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await ValidateCoreAsync(cts.Token, rethrow: false, cts);
    }

    /// <summary>Stops the running <see cref="ValidateAsync"/> before its next batch and cancels the callback in flight. Does nothing when no check is running.</summary>
    public void CancelValidation() => _validateCts?.Cancel();

    private async Task ValidateCoreAsync(CancellationToken token, bool rethrow, CancellationTokenSource? owned = null)
    {
        _validateCts = owned;
        var stamp = _asyncStamp;
        ValidationStatus = SpreadsheetValidationStatus.Running;
        ValidationError = null;
        Changed?.Invoke();
        try
        {
            await LookupExistingAsync(token);
            await RunRowValidationAsync(token);
            // Rows changed while the run was in flight were not marked checked, so they stay for the next run.
            if (stamp == _asyncStamp) _asyncDirty = false;
            ValidationStatus = SpreadsheetValidationStatus.Done;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            ValidationStatus = SpreadsheetValidationStatus.Cancelled;
            if (rethrow) throw;
        }
        catch (Exception ex)
        {
            ValidationError = ex.Message;
            ValidationStatus = SpreadsheetValidationStatus.Error;
            if (rethrow) throw;
        }
        finally
        {
            _validateCts = null;
            Changed?.Invoke();
        }
    }

    private async Task LookupExistingAsync(CancellationToken token)
    {
        if (Schema.LookupExistingRecordsAsync is not { } lookup || string.IsNullOrEmpty(Schema.RowKeyField)) return;

        var keys = new List<string>();
        var seen = new HashSet<string>(_lookedUpKeys, StringComparer.Ordinal);
        foreach (var row in _rowArr)
        {
            if (row.DiscardReason == SpreadsheetDiscardReason.Limit) continue;
            if (row.Data.TryGetValue(Schema.RowKeyField, out var value) && value?.ToString() is { Length: > 0 } key && seen.Add(key))
            {
                keys.Add(key);
            }
        }

        var batchSize = Schema.LookupBatchSize > 0 ? Schema.LookupBatchSize : Math.Max(1, keys.Count);
        for (var offset = 0; offset < keys.Count; offset += batchSize)
        {
            token.ThrowIfCancellationRequested();
            var batch = keys.Skip(offset).Take(batchSize).ToList();
            var found = await lookup(batch, token);
            if (found is { Count: > 0 }) _lookedUpRecords.Add(found);
            foreach (var key in batch) _lookedUpKeys.Add(key);
            if (found is { Count: > 0 })
            {
                RefinalizeAll();
                Changed?.Invoke();
            }
        }
    }

    private async Task RunRowValidationAsync(CancellationToken token)
    {
        if (Schema.ValidateRowsAsync is not { } validate) return;

        var rows = _rowArr
            .Where(r => r.DiscardReason is null && r.Status != SpreadsheetRowStatus.Blocking && !_asyncChecked.Contains(r.Index))
            .ToList();
        var batchSize = Schema.ValidateBatchSize > 0 ? Schema.ValidateBatchSize : Math.Max(1, rows.Count);
        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            token.ThrowIfCancellationRequested();
            var batch = rows.Skip(offset).Take(batchSize).ToList();
            var sent = batch.Where(r => r.Index < _parsed.Count).ToDictionary(r => r.Index, r => _parsed[r.Index]);
            var issues = await validate(batch, token);

            // Apply the answers only to rows that are the same parsed row as when the batch left: an edit (or a new read of the file)
            // made the answer stale, and that row stays unchecked.
            var batchIndexes = batch
                .Where(r => r.Index < _parsed.Count && ReferenceEquals(_parsed[r.Index], sent[r.Index]))
                .Select(r => r.Index).ToHashSet();
            foreach (var index in batchIndexes)
            {
                _asyncChecked.Add(index);
                _asyncIssues.Remove(index);
            }
            foreach (var found in issues ?? [])
            {
                if (!batchIndexes.Contains(found.RowIndex)) continue;
                if (!_asyncIssues.TryGetValue(found.RowIndex, out var list))
                {
                    list = [];
                    _asyncIssues[found.RowIndex] = list;
                }
                list.Add(found.Issue);
            }
            RefinalizeRows(batchIndexes);
            Changed?.Invoke();
        }
    }
}
