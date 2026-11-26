namespace Klyroo.Spreadsheet;

public sealed partial class SpreadsheetImporter
{
    /// <summary>
    /// Sends every importable row to <see cref="SpreadsheetSchema.OnSubmit"/> in batches of <see cref="SpreadsheetSchema.BatchSize"/>.
    /// It first calls <see cref="SpreadsheetColumnSchema.OnCreateOption"/> once for each value answered with Create. Rejected rows are kept
    /// as errors on their row. An exception from a callback is caught and reported in <see cref="SubmitError"/>. Does nothing while a submit is running
    /// or when <see cref="CanSubmit"/> is <see langword="false"/>. <see cref="ImportedCount"/> starts again from zero.
    /// </summary>
    /// <returns>A task that completes when the submit has finished or failed.</returns>
    public async Task SubmitAsync()
    {
        if (SubmitStatus == SpreadsheetSubmitStatus.Running || !CanSubmit) return;
        ImportedCount = 0;
        await SendRowsAsync(createOptions: true, () => ImportableRows);
    }

    /// <summary>Sends again the rows the server rejected, that are still importable. The rejection issues are cleared before the send. Does nothing while a submit is running or when nothing was rejected.</summary>
    /// <returns>A task that completes when the retry has finished or failed.</returns>
    public async Task RetryRejectedAsync()
    {
        if (SubmitStatus == SpreadsheetSubmitStatus.Running || _serverRejections.Count == 0) return;
        var rejectedIndexes = _serverRejections.Keys.ToHashSet();
        _serverRejections.Clear();
        Recompute();
        await SendRowsAsync(createOptions: true, () => _rowArr.Where(r => rejectedIndexes.Contains(r.Index) && r.Importable).ToList());
    }

    private async Task SendRowsAsync(bool createOptions, Func<IReadOnlyList<SpreadsheetRow>> selectRows)
    {
        SubmitStatus = SpreadsheetSubmitStatus.Running;
        SubmitDone = 0;
        SubmitError = null;
        Changed?.Invoke();

        try
        {
            // Step 1: create the new options once each. The returned option becomes a Map answer, so the rows carry
            // its value and a later send does not create it again.
            if (createOptions && CreateAnsweredOptions())
            {
                Recompute();
            }
            var rowsToSend = selectRows();
            SubmitTotal = rowsToSend.Count;

            // Step 2: Send in batches
            var batchSize = Schema.BatchSize > 0 ? Schema.BatchSize : Math.Max(1, rowsToSend.Count);
            var rejectedAll = new List<SpreadsheetRejectedRow>();

            for (var offset = 0; offset < rowsToSend.Count; offset += batchSize)
            {
                var batch = rowsToSend.Skip(offset).Take(batchSize).ToList();
                if (Schema.OnSubmit is not null)
                {
                    var rejected = await Schema.OnSubmit(batch);
                    if (rejected is { Count: > 0 })
                    {
                        rejectedAll.AddRange(rejected);
                    }
                }
                SubmitDone = Math.Min(rowsToSend.Count, offset + batch.Count);
                Changed?.Invoke();
            }

            _serverRejections.Clear();
            foreach (var rej in rejectedAll)
            {
                if (!_serverRejections.TryGetValue(rej.RowIndex, out var list))
                {
                    list = [];
                    _serverRejections[rej.RowIndex] = list;
                }
                list.Add(new SpreadsheetRowIssue(rej.Field, "server", SpreadsheetIssueLevel.Error, rej.Message));
            }

            ImportedCount += rowsToSend.Count - _serverRejections.Count;
            SubmitStatus = SpreadsheetSubmitStatus.Done;
            Recompute();
        }
        catch (Exception ex)
        {
            SubmitError = ex.Message;
            SubmitStatus = SpreadsheetSubmitStatus.Error;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    // Runs OnCreateOption for each value answered with Create, and records the returned option as a Map answer.
    private bool CreateAnsweredOptions()
    {
        var created = false;
        foreach (var q in _questions.Where(q => q.Answer?.Action == SpreadsheetValueAnswerAction.Create).ToList())
        {
            var col = Schema.Columns.FirstOrDefault(c => c.Key == q.FieldKey);
            if (col?.OnCreateOption is null) continue;
            var option = col.OnCreateOption(q.RawValue);
            var value = string.IsNullOrEmpty(option?.Value) ? q.RawValue : option.Value;
            _valueAnswers[q.Id] = new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, value, option?.Label ?? value);
            created = true;
        }
        return created;
    }
}
