using System.Globalization;

namespace Klyroo.Spreadsheet;

// The row pipeline: matching file columns to the schema, parsing every row once, and then keeping the rows up to date one change at a time.
// A full pass (Recompute) runs when the file, the layout or the column mapping changes. Editing a cell, answering a value, leaving rows out
// or applying a server result touches only the rows concerned: the duplicate-key index tells which other rows a key change affects.
public sealed partial class SpreadsheetImporter
{
    // A row as parsed from the file text and the user's edits, before the checks that look at other rows (duplicate keys, existing records,
    // limits, manual discards) and before server and async results are added. Immutable once built: an edit replaces it.
    private sealed class ParsedRow
    {
        public required int Index { get; init; }
        public required int RowNumber { get; init; }
        public required Dictionary<string, object?> Data { get; init; }
        public required Dictionary<string, SpreadsheetCellView> Cells { get; init; }
        public required List<SpreadsheetRowIssue> Issues { get; init; }
        public required bool SkippedByValue { get; init; }
        public required bool Edited { get; init; }
        public required UnknownEntry[] Unknown { get; init; }
        public required string? DuplicateKey { get; init; }
    }

    private readonly record struct UnknownEntry(string Id, SpreadsheetColumnSchema Column, string Raw, string Normalized);

    private static readonly UnknownEntry[] NoUnknown = [];

    private IReadOnlyList<IReadOnlyList<string>> _dataRows = [];
    private IReadOnlyList<int> _dataRowNumbers = [];
    private List<ParsedRow> _parsed = [];
    private SpreadsheetRow[] _rowArr = [];
    private IReadOnlyList<SpreadsheetFieldState> _missingFields = [];
    private IReadOnlyList<SpreadsheetValueQuestion> _openQuestions = [];
    private readonly Dictionary<string, List<int>> _keyGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<SpreadsheetColumnSchema, Dictionary<string, SpreadsheetOption>> _optionIndexes = new(ReferenceEqualityComparer.Instance);
    private int? _defaultMaxRows;

    // Bumped whenever a row changes (rows version) or a review filter changes (view version): the lists built from the rows are cached against them.
    private int _rowsVersion = 1;
    private int _viewVersion = 1;
    private (int Version, IReadOnlyList<SpreadsheetRow> Rows) _allCache;
    private (int Version, IReadOnlyList<SpreadsheetRow> Rows) _importableCache;
    private (int Version, IReadOnlyList<SpreadsheetRow> Rows) _invalidCache;
    private (int Version, IReadOnlyList<SpreadsheetRow> Rows) _discardedCache;
    private (int Rows, int View, IReadOnlyList<SpreadsheetRow> List) _visibleCache;

    private static IReadOnlyList<SpreadsheetRow> Cached(ref (int Version, IReadOnlyList<SpreadsheetRow> Rows) cache, int version, Func<IReadOnlyList<SpreadsheetRow>> build)
    {
        if (cache.Version != version || cache.Rows is null)
        {
            cache = (version, build());
        }
        return cache.Rows;
    }

    private void BumpRows() => _rowsVersion++;

    private void BumpView() => _viewVersion++;

    /// <summary>Every data row after the header, discarded ones included. The list is a snapshot: it does not change when you edit afterwards, and it is rebuilt on the next access after a change.</summary>
    public IReadOnlyList<SpreadsheetRow> AllRows => Cached(ref _allCache, _rowsVersion, () => _rowArr.AsSpan().ToArray());

    /// <summary>The rows that would be sent on submit. The list is cached until a row changes.</summary>
    public IReadOnlyList<SpreadsheetRow> ImportableRows => Cached(ref _importableCache, _rowsVersion, () => _rowArr.Where(r => r.Importable).ToArray());

    /// <summary>The rows with at least one error that are not discarded. The list is cached until a row changes.</summary>
    public IReadOnlyList<SpreadsheetRow> InvalidRows => Cached(ref _invalidCache, _rowsVersion, () => _rowArr.Where(r => r.DiscardReason is null && r.Status == SpreadsheetRowStatus.Blocking).ToArray());

    /// <summary>The rows left out of the import for any reason. The list is cached until a row changes.</summary>
    public IReadOnlyList<SpreadsheetRow> DiscardedRows => Cached(ref _discardedCache, _rowsVersion, () => _rowArr.Where(r => r.DiscardReason is not null).ToArray());

    /// <summary>The fields with status <see cref="SpreadsheetColumnStatus.Missing"/>. Submission is blocked while this is not empty.</summary>
    public IReadOnlyList<SpreadsheetFieldState> MissingFields => _missingFields;

    /// <summary>The questions with no answer yet. Submission is blocked while this is not empty.</summary>
    public IReadOnlyList<SpreadsheetValueQuestion> OpenQuestions => _openQuestions;

    /// <summary>
    /// A row limit that applies when <see cref="SpreadsheetSchema.MaxRows"/> is <see langword="null"/>. <see langword="null"/> (the default) means no limit.
    /// <c>KlySpreadsheetImport</c> sets it from its <c>MaxRows</c> parameter. Rows past the limit are left out with <see cref="SpreadsheetDiscardReason.Limit"/>.
    /// </summary>
    public int? DefaultMaxRows
    {
        get => _defaultMaxRows;
        set
        {
            if (_defaultMaxRows == value) return;
            _defaultMaxRows = value;
            if (!FileLoaded) return;
            RefinalizeAll();
            Changed?.Invoke();
        }
    }

    /// <summary>The row limit in force: <see cref="SpreadsheetSchema.MaxRows"/> when set, otherwise <see cref="DefaultMaxRows"/>. <see langword="null"/> when there is no limit.</summary>
    public int? EffectiveMaxRows => Schema.MaxRows ?? _defaultMaxRows;

    /// <summary>The number of rows left out because they are past <see cref="EffectiveMaxRows"/>.</summary>
    public int LimitedRowCount => EffectiveMaxRows is { } max ? Math.Max(0, _rowArr.Length - Math.Max(0, max)) : 0;

    // Parsing

    private void Recompute()
    {
        var sheet = CurrentSheet;
        if (sheet is null || sheet.Rows.Count == 0 || _headerRowIndex >= sheet.Rows.Count)
        {
            Headers = [];
            UnusedHeaders = [];
            _fields = Schema.Columns.Select(c => new SpreadsheetFieldState(
                c,
                c.HasDefault ? SpreadsheetColumnStatus.Default : c.Required ? SpreadsheetColumnStatus.Missing : SpreadsheetColumnStatus.Unmatched,
                null, null, null, [], null)).ToList();
            _missingFields = _fields.Where(f => f.Status == SpreadsheetColumnStatus.Missing).ToList();
            _questions = [];
            _openQuestions = [];
            _dataRows = [];
            _dataRowNumbers = [];
            _parsed = [];
            _rowArr = [];
            _keyGroups.Clear();
            BumpRows();
            return;
        }

        Headers = sheet.Rows[_headerRowIndex];
        var dataRowSlice = sheet.Rows.Skip(_headerRowIndex + 1).ToList();
        _dataRows = dataRowSlice;
        _dataRowNumbers = sheet.RowNumbers.Skip(_headerRowIndex + 1).ToList();

        // 1. Match columns
        var usedHeaders = new HashSet<int>();
        var matchedIndexByField = new Dictionary<string, (int? Index, string? AssignedBy)>(StringComparer.Ordinal);

        foreach (var col in Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)))
        {
            if (_manualColumns.TryGetValue(col.Key, out var manualIdx))
            {
                if (manualIdx is { } idx && idx >= 0 && idx < Headers.Count)
                {
                    usedHeaders.Add(idx);
                    matchedIndexByField[col.Key] = (idx, "user");
                }
                else
                {
                    matchedIndexByField[col.Key] = (null, null);
                }
            }
        }

        foreach (var col in Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)))
        {
            if (matchedIndexByField.ContainsKey(col.Key)) continue;
            int? found = null;
            for (var h = 0; h < Headers.Count; h++)
            {
                if (usedHeaders.Contains(h) || string.IsNullOrWhiteSpace(Headers[h])) continue;
                if (IsHeaderMatch(col, Headers[h]))
                {
                    found = h;
                    usedHeaders.Add(h);
                    break;
                }
            }
            matchedIndexByField[col.Key] = (found, found.HasValue ? "file" : null);
        }

        var unusedHeaderIndices = Enumerable.Range(0, Headers.Count)
            .Where(i => !usedHeaders.Contains(i) && !string.IsNullOrWhiteSpace(Headers[i]))
            .ToList();
        UnusedHeaders = unusedHeaderIndices.Select(i => Headers[i]).ToList();

        // 2. Compute column states + 80% pass rate suggestions for missing columns
        var suggestedHeadersTaken = new HashSet<int>();
        var fieldStates = new List<SpreadsheetFieldState>(Schema.Columns.Count);
        foreach (var col in Schema.Columns)
        {
            int? hIdx = null;
            string? assignedBy = null;
            if (string.IsNullOrEmpty(col.From) && matchedIndexByField.TryGetValue(col.Key, out var m))
            {
                hIdx = m.Index;
                assignedBy = m.AssignedBy;
            }

            var status = hIdx.HasValue ? SpreadsheetColumnStatus.Matched
                : !string.IsNullOrEmpty(col.From) ? SpreadsheetColumnStatus.Matched
                : col.HasDefault ? SpreadsheetColumnStatus.Default
                : col.Required ? SpreadsheetColumnStatus.Missing
                : SpreadsheetColumnStatus.Unmatched;

            var samples = hIdx.HasValue
                ? CollectDistinctSamples(dataRowSlice, hIdx.Value, 3)
                : [];

            SpreadsheetColumnSuggestion? suggestion = null;
            if (status == SpreadsheetColumnStatus.Missing && CanSuggestColumn(col))
            {
                foreach (var candidateIdx in unusedHeaderIndices)
                {
                    if (suggestedHeadersTaken.Contains(candidateIdx)) continue;
                    var sampleCells = CollectNonEmptyCells(dataRowSlice, candidateIdx, SuggestionSampleSize);
                    if (sampleCells.Count < 2) continue;
                    var passCount = sampleCells.Count(cell => PassesColumnCheck(col, cell, InvariantNumbers));
                    if ((double)passCount / sampleCells.Count >= SuggestionPassRate)
                    {
                        suggestedHeadersTaken.Add(candidateIdx);
                        suggestion = new SpreadsheetColumnSuggestion(
                            candidateIdx,
                            Headers[candidateIdx],
                            sampleCells.Distinct(StringComparer.Ordinal).Take(3).ToList());
                        break;
                    }
                }
            }

            fieldStates.Add(new SpreadsheetFieldState(
                col,
                status,
                hIdx,
                hIdx.HasValue ? Headers[hIdx.Value] : null,
                assignedBy,
                samples,
                suggestion));
        }
        _fields = fieldStates;
        _missingFields = fieldStates.Where(f => f.Status == SpreadsheetColumnStatus.Missing).ToList();

        // 3. Parse every row, index the duplicate keys, then finalize the rows
        var parsed = new List<ParsedRow>(dataRowSlice.Count);
        for (var rIdx = 0; rIdx < dataRowSlice.Count; rIdx++)
        {
            parsed.Add(ParseRow(rIdx));
        }
        _parsed = parsed;

        _keyGroups.Clear();
        for (var i = 0; i < parsed.Count; i++)
        {
            if (parsed[i].DuplicateKey is { } key) IndexAdd(key, i);
        }

        var rows = new SpreadsheetRow[parsed.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = BuildRow(parsed[i]);
        }
        _rowArr = rows;
        BumpRows();

        // 4. Build value questions
        RebuildQuestions();
    }

    private ParsedRow ParseRow(int rIdx)
    {
        var rawCells = _dataRows[rIdx];
        var rowData = new Dictionary<string, object?>(StringComparer.Ordinal);
        var cellViews = new Dictionary<string, SpreadsheetCellView>(StringComparer.Ordinal);
        var rowIssues = new List<SpreadsheetRowIssue>();
        List<UnknownEntry>? unknown = null;
        var skippedByValue = false;
        var rowEdited = false;

        foreach (var fState in _fields)
        {
            var col = fState.Column;
            string originalText;
            if (!string.IsNullOrEmpty(col.From) && cellViews.TryGetValue(col.From, out var fromCell))
            {
                originalText = fromCell.Text;
            }
            else if (fState.HeaderIndex is { } hi && hi < rawCells.Count)
            {
                originalText = rawCells[hi];
            }
            else
            {
                originalText = "";
            }

            var hasEdit = _edits.TryGetValue((rIdx, col.Key), out var editedText);
            var cellText = hasEdit ? editedText! : originalText;
            if (hasEdit) rowEdited = true;

            object? parsedVal = null;
            var displayText = cellText;
            var defaulted = false;
            var created = false;
            SpreadsheetRowIssue? fieldIssue = null;

            if (string.IsNullOrWhiteSpace(cellText))
            {
                if (col.HasDefault)
                {
                    parsedVal = col.DefaultValue;
                    displayText = col.DefaultValue?.ToString() ?? "";
                    defaulted = true;
                }
                else if (col.Required)
                {
                    fieldIssue = new SpreadsheetRowIssue(col.Key, "required", SpreadsheetIssueLevel.Error, "Required value is missing");
                    rowIssues.Add(fieldIssue);
                }
            }
            else if (col.Kind == SpreadsheetColumnKind.Select)
            {
                var matchedOpt = MatchSelectOption(col, cellText);
                if (matchedOpt is not null)
                {
                    parsedVal = matchedOpt.Value;
                    displayText = matchedOpt.Label;
                }
                else
                {
                    var normKey = NormalizeText(cellText);
                    var qId = $"{col.Key}::{normKey}";
                    _valueAnswers.TryGetValue(qId, out var userAns);
                    var effectiveAns = userAns ?? PolicyToAnswer(col.UnknownPolicy);

                    if (col.UnknownPolicy != SpreadsheetUnknownValuePolicy.Error)
                    {
                        (unknown ??= []).Add(new UnknownEntry(qId, col, cellText, normKey));
                    }

                    if (effectiveAns is not null)
                    {
                        switch (effectiveAns.Action)
                        {
                            case SpreadsheetValueAnswerAction.Map:
                                parsedVal = effectiveAns.MappedValue;
                                displayText = effectiveAns.MappedLabel ?? effectiveAns.MappedValue ?? cellText;
                                break;
                            case SpreadsheetValueAnswerAction.Create:
                                parsedVal = cellText;
                                displayText = cellText;
                                created = true;
                                break;
                            case SpreadsheetValueAnswerAction.LeaveEmpty:
                                parsedVal = null;
                                displayText = "";
                                break;
                            case SpreadsheetValueAnswerAction.SkipRows:
                                skippedByValue = true;
                                fieldIssue = new SpreadsheetRowIssue(col.Key, "value.skipped", SpreadsheetIssueLevel.Info, $"Left out: \"{cellText}\" is set to skip its rows");
                                rowIssues.Add(fieldIssue);
                                break;
                        }
                    }
                    else
                    {
                        var code = col.UnknownPolicy == SpreadsheetUnknownValuePolicy.Error ? "value.not-found" : "value.unknown";
                        var msg = col.UnknownPolicy == SpreadsheetUnknownValuePolicy.Error
                            ? $"\"{cellText}\" is not a known value"
                            : $"\"{cellText}\" is not a known value · choose it in Values";
                        fieldIssue = new SpreadsheetRowIssue(col.Key, code, SpreadsheetIssueLevel.Error, msg);
                        rowIssues.Add(fieldIssue);
                    }
                }
            }
            else
            {
                if (!TryParseScalar(col, cellText, InvariantNumbers, out parsedVal, out var parseErr))
                {
                    fieldIssue = new SpreadsheetRowIssue(col.Key, col.Kind.ToString().ToLowerInvariant(), SpreadsheetIssueLevel.Error, parseErr!);
                    rowIssues.Add(fieldIssue);
                }
                else
                {
                    displayText = parsedVal?.ToString() ?? cellText;
                }
            }

            // Run field rules on non-empty parsed values
            if (parsedVal is not null && col.Rules is { Count: > 0 })
            {
                foreach (var rule in col.Rules)
                {
                    var passed = RunRule(rule, parsedVal, out var timedOut);
                    if (!passed)
                    {
                        var ruleIssue = timedOut
                            ? new SpreadsheetRowIssue(col.Key, "pattern", rule.Level, "The format could not be checked in time")
                            : new SpreadsheetRowIssue(col.Key, rule.Code, rule.Level, rule.Message);
                        rowIssues.Add(ruleIssue);
                        fieldIssue ??= ruleIssue;
                    }
                }
            }

            rowData[col.Key] = parsedVal;
            cellViews[col.Key] = new SpreadsheetCellView(cellText, originalText, displayText, hasEdit, defaulted, created, fieldIssue);
        }

        if (Schema.ValidateRow is not null)
        {
            var extraIssues = Schema.ValidateRow(rowData);
            if (extraIssues is { Count: > 0 })
            {
                rowIssues.AddRange(extraIssues);
            }
        }

        string? duplicateKey = null;
        if (!string.IsNullOrEmpty(Schema.RowKeyField) && rowData.TryGetValue(Schema.RowKeyField, out var keyValue))
        {
            var key = keyValue?.ToString()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key)) duplicateKey = key;
        }

        return new ParsedRow
        {
            Index = rIdx,
            RowNumber = _dataRowNumbers[rIdx],
            Data = rowData,
            Cells = cellViews,
            Issues = rowIssues,
            SkippedByValue = skippedByValue,
            Edited = rowEdited,
            Unknown = unknown is null ? NoUnknown : unknown.ToArray(),
            DuplicateKey = duplicateKey,
        };
    }

    // Applies the checks that look beyond the row's own cells and builds the public row.
    private SpreadsheetRow BuildRow(ParsedRow pr)
    {
        List<SpreadsheetRowIssue>? extra = null;
        if (_serverRejections.TryGetValue(pr.Index, out var serverErrors))
        {
            (extra ??= []).AddRange(serverErrors);
        }

        var duplicateDiscard = false;
        if (pr.DuplicateKey is { } dupKey && _keyGroups.TryGetValue(dupKey, out var group) && group.Count > 1)
        {
            switch (Schema.Duplicates)
            {
                case SpreadsheetDuplicatesPolicy.Error:
                    var other = _parsed[group[0] != pr.Index ? group[0] : group[1]];
                    (extra ??= []).Add(new SpreadsheetRowIssue(Schema.RowKeyField, "row.duplicate", SpreadsheetIssueLevel.Error, $"Same key as row {other.RowNumber}"));
                    break;
                case SpreadsheetDuplicatesPolicy.KeepFirst:
                    duplicateDiscard = group[0] != pr.Index;
                    break;
                case SpreadsheetDuplicatesPolicy.KeepLast:
                    duplicateDiscard = group[^1] != pr.Index;
                    break;
            }
        }

        var mode = SpreadsheetRowMode.Create;
        var existingDiscard = false;
        if (!string.IsNullOrEmpty(Schema.RowKeyField)
            && pr.Data.TryGetValue(Schema.RowKeyField, out var keyObj)
            && keyObj?.ToString() is { Length: > 0 } keyStr
            && IsExistingKey(keyStr))
        {
            if (Schema.ExistingAction == SpreadsheetExistingAction.Update)
            {
                mode = SpreadsheetRowMode.Update;
            }
            else if (Schema.ExistingAction == SpreadsheetExistingAction.Skip)
            {
                mode = SpreadsheetRowMode.Skip;
                existingDiscard = true;
            }
            else if (Schema.ExistingAction == SpreadsheetExistingAction.Error)
            {
                mode = SpreadsheetRowMode.Update;
                (extra ??= []).Add(new SpreadsheetRowIssue(Schema.RowKeyField, "row.exists", SpreadsheetIssueLevel.Error, "Already stored"));
            }
        }

        IReadOnlyList<SpreadsheetRowIssue> issues = extra is null ? pr.Issues : [.. pr.Issues, .. extra];

        // Precedence: limit > value > duplicate > existing > manual
        SpreadsheetDiscardReason? discard =
            (EffectiveMaxRows is { } maxR && pr.Index >= maxR) ? SpreadsheetDiscardReason.Limit
            : pr.SkippedByValue ? SpreadsheetDiscardReason.Value
            : duplicateDiscard ? SpreadsheetDiscardReason.Duplicate
            : existingDiscard ? SpreadsheetDiscardReason.Existing
            : _manualDiscards.Contains(pr.Index) ? SpreadsheetDiscardReason.Manual
            : null;

        var hasError = false;
        var hasWarning = false;
        foreach (var issue in issues)
        {
            if (issue.Level == SpreadsheetIssueLevel.Error) hasError = true;
            else if (issue.Level == SpreadsheetIssueLevel.Warning) hasWarning = true;
        }

        var status = discard is not null ? SpreadsheetRowStatus.Discarded
            : hasError ? SpreadsheetRowStatus.Blocking
            : hasWarning ? SpreadsheetRowStatus.Warning
            : SpreadsheetRowStatus.Valid;

        return new SpreadsheetRow(
            pr.Index,
            pr.RowNumber,
            pr.Data,
            pr.Cells,
            issues,
            status,
            discard,
            mode,
            pr.Edited,
            discard is null && !hasError);
    }

    private bool IsExistingKey(string key) => Schema.ExistingRecords?.ContainsKey(key) == true;

    // Incremental updates

    /// <summary>Re-parses the given rows and updates everything that depends on them: the duplicate-key index (and the other rows of the keys concerned), the rows and, when asked or when their unknown values changed, the value questions.</summary>
    private void ReparseRows(IEnumerable<int> rowIndexes, bool alwaysRebuildQuestions)
    {
        var touched = new HashSet<int>();
        var unknownChanged = false;
        foreach (var rIdx in rowIndexes)
        {
            if (rIdx < 0 || rIdx >= _parsed.Count) continue;
            var old = _parsed[rIdx];
            var fresh = ParseRow(rIdx);
            _parsed[rIdx] = fresh;
            touched.Add(rIdx);
            unknownChanged |= !SameUnknown(old.Unknown, fresh.Unknown);

            if (old.DuplicateKey != fresh.DuplicateKey)
            {
                if (old.DuplicateKey is { } oldKey && IndexRemove(oldKey, rIdx) is { } oldGroup)
                {
                    touched.UnionWith(oldGroup);
                }
                if (fresh.DuplicateKey is { } newKey)
                {
                    touched.UnionWith(IndexAdd(newKey, rIdx));
                }
            }
        }

        foreach (var t in touched)
        {
            _rowArr[t] = BuildRow(_parsed[t]);
        }
        if (touched.Count > 0) BumpRows();
        if (alwaysRebuildQuestions || unknownChanged) RebuildQuestions();
    }

    private void RefinalizeRows(IEnumerable<int> rowIndexes)
    {
        var any = false;
        foreach (var i in rowIndexes)
        {
            if (i < 0 || i >= _parsed.Count) continue;
            _rowArr[i] = BuildRow(_parsed[i]);
            any = true;
        }
        if (any) BumpRows();
    }

    private void RefinalizeAll()
    {
        for (var i = 0; i < _parsed.Count; i++)
        {
            _rowArr[i] = BuildRow(_parsed[i]);
        }
        BumpRows();
    }

    private static bool SameUnknown(UnknownEntry[] a, UnknownEntry[] b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].Id != b[i].Id || a[i].Raw != b[i].Raw) return false;
        }
        return true;
    }

    // The duplicate-key index: for each normalized key, the ascending indexes of the rows that hold it.
    private List<int> IndexAdd(string key, int row)
    {
        if (!_keyGroups.TryGetValue(key, out var group))
        {
            _keyGroups[key] = group = [];
        }
        var at = group.BinarySearch(row);
        if (at < 0) group.Insert(~at, row);
        return group;
    }

    private List<int>? IndexRemove(string key, int row)
    {
        if (!_keyGroups.TryGetValue(key, out var group)) return null;
        var at = group.BinarySearch(row);
        if (at >= 0) group.RemoveAt(at);
        if (group.Count == 0)
        {
            _keyGroups.Remove(key);
            return null;
        }
        return group;
    }

    // Builds the questions from the unknown values of the parsed rows, in the order they first appear.
    private void RebuildQuestions()
    {
        var tokens = new Dictionary<string, (SpreadsheetColumnSchema Col, string Raw, string Norm, List<int> Rows)>(StringComparer.Ordinal);
        foreach (var pr in _parsed)
        {
            foreach (var u in pr.Unknown)
            {
                if (!tokens.TryGetValue(u.Id, out var entry))
                {
                    entry = (u.Column, u.Raw, u.Normalized, []);
                    tokens[u.Id] = entry;
                }
                entry.Rows.Add(pr.Index);
            }
        }

        var questionList = new List<SpreadsheetValueQuestion>(tokens.Count);
        foreach (var (qId, (col, rawVal, normKey, rowIndices)) in tokens)
        {
            var hasUser = _valueAnswers.TryGetValue(qId, out var uAns);
            var pAns = PolicyToAnswer(col.UnknownPolicy);
            var effective = hasUser ? uAns : pAns;
            var answeredBy = hasUser ? "user" : pAns is not null ? "policy" : null;
            questionList.Add(new SpreadsheetValueQuestion(
                qId,
                col.Key,
                col.EffectiveLabel,
                rawVal,
                normKey,
                rowIndices,
                col.Options ?? [],
                effective,
                answeredBy,
                col.OnCreateOption is not null));
        }
        _questions = questionList;
        _openQuestions = questionList.Where(q => q.IsOpen).ToList();
    }

    // The options of a select column by normalized label, value and alias. When two options claim a text, the first option wins.
    private Dictionary<string, SpreadsheetOption> OptionIndex(SpreadsheetColumnSchema col)
    {
        if (_optionIndexes.TryGetValue(col, out var index)) return index;
        index = new Dictionary<string, SpreadsheetOption>(StringComparer.Ordinal);
        foreach (var opt in col.Options ?? [])
        {
            index.TryAdd(NormalizeText(opt.Label), opt);
            index.TryAdd(NormalizeText(opt.Value), opt);
            if (opt.Aliases is null) continue;
            foreach (var alias in opt.Aliases)
            {
                index.TryAdd(NormalizeText(alias), opt);
            }
        }
        _optionIndexes[col] = index;
        return index;
    }

    private SpreadsheetOption? MatchSelectOption(SpreadsheetColumnSchema col, string text) =>
        col.Options is { Count: > 0 } && OptionIndex(col).TryGetValue(NormalizeText(text), out var option) ? option : null;
}
