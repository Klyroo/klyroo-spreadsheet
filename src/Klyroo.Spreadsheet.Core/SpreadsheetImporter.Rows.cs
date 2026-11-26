using System.Globalization;

namespace Klyroo.Spreadsheet;

// The row pipeline: matching file columns to the schema, parsing every row once, and then keeping the rows up to date one change at a time.
// A full pass (Recompute) runs when the file, the layout or the column mapping changes. Editing a cell, answering a value, leaving rows out
// or applying a server result touches only the rows concerned: the duplicate-key index tells which other rows a key change affects.
public abstract partial class SpreadsheetImporter
{
    // A row as parsed from the file text and the user's edits, before the checks that look at other rows (duplicate keys, existing records,
    // limits, manual discards) and before server and async results are added. Immutable once built: an edit replaces it.
    private sealed class ParsedRow
    {
        public required int Index { get; init; }
        public required int RowNumber { get; init; }
        public required object?[] Values { get; init; }
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
            MarkAsyncDirty();                 // rows the new limit takes in were never checked
            RefinalizeAll();
            Changed?.Invoke();
        }
    }

    /// <summary>The row limit in force: <see cref="SpreadsheetSchema.MaxRows"/> when set, otherwise <see cref="DefaultMaxRows"/>. <see langword="null"/> when there is no limit.</summary>
    public int? EffectiveMaxRows => Schema.MaxRows ?? _defaultMaxRows;

    /// <summary>The number of rows left out because they are past <see cref="EffectiveMaxRows"/>.</summary>
    public int LimitedRowCount => EffectiveMaxRows is { } max ? Math.Max(0, _rowArr.Length - Math.Max(0, max)) : 0;

    // Parsing

    // Match timeouts counted per rule (column key and position in Rules) during the current read, and the rules stopped after too many.
    private readonly Dictionary<(string Column, int Rule), int> _ruleTimeouts = [];
    private readonly List<SpreadsheetDisabledRule> _disabledRules = [];

    /// <summary>The rules the importer stopped evaluating in the current read because their matches kept timing out (see <see cref="SpreadsheetSchema.RuleTimeoutLimit"/>). Values after the rule was stopped were not checked by it. Empty in the normal case.</summary>
    public IReadOnlyList<SpreadsheetDisabledRule> DisabledRules => _disabledRules;

    // What the host's callbacks threw during the current pass (a read or an edit), by source, so the log gets one entry per rule with a count.
    private sealed class HostFailure(string source, Exception first)
    {
        public string Source { get; } = source;
        public Exception First { get; } = first;
        public int Count { get; set; }
        public bool Stopped { get; set; }
    }

    private readonly Dictionary<string, HostFailure> _passFailures = [];
    private readonly HashSet<(string Column, int Rule)> _failedRules = [];

    // The generic text the uploader sees when a callback of the host threw. The component shows the text of the user's locale for the code.
    internal static SpreadsheetRowIssue RowFailedIssue(string? field) =>
        new(field, "row.failed", SpreadsheetIssueLevel.Error, "The row could not be checked", []);

    private void NoteHostFailure(string source, Exception exception, bool stopped = false)
    {
        if (!_passFailures.TryGetValue(source, out var failure))
        {
            failure = new HostFailure(source, exception);
            _passFailures[source] = failure;
        }
        failure.Count++;
        failure.Stopped |= stopped;
    }

    private void FlushHostFailures()
    {
        if (_passFailures.Count == 0) return;
        var logger = Logger;
        foreach (var failure in _passFailures.Values)
        {
            if (logger is not null) SpreadsheetLog.HostCallbackFailed(logger, failure.Source, failure.Count, failure.Stopped, failure.First);
        }
        _passFailures.Clear();
    }

    private void Recompute()
    {
        try
        {
            RecomputeCore();
        }
        finally
        {
            FlushHostFailures();
        }
    }

    private void RecomputeCore()
    {
        _ruleTimeouts.Clear();
        _failedRules.Clear();
        _disabledRules.Clear();
        _asyncChecked.Clear();
        _asyncIssues.Clear();
        MarkAsyncDirty();

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
        var rowValues = new object?[_fields.Count];
        var cellViews = new Dictionary<string, SpreadsheetCellView>(_fields.Count, StringComparer.Ordinal);
        var rowIssues = new List<SpreadsheetRowIssue>();
        List<UnknownEntry>? unknown = null;
        var skippedByValue = false;
        var rowEdited = false;

        for (var fieldIndex = 0; fieldIndex < _fields.Count; fieldIndex++)
        {
            var fState = _fields[fieldIndex];
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
                    parsedVal = col.DefaultBoxed;
                    displayText = SpreadsheetValueConverter.DisplayText(col, col.DefaultBoxed);
                    defaulted = true;
                }
                else if (col.Required)
                {
                    fieldIssue = new SpreadsheetRowIssue(col.Key, "required", SpreadsheetIssueLevel.Error, "Required value is missing", []);
                    rowIssues.Add(fieldIssue);
                }
            }
            else if (col.Kind == SpreadsheetColumnKind.Select)
            {
                var matchedOpt = MatchSelectOption(col, cellText);
                if (matchedOpt is not null)
                {
                    parsedVal = col.ConvertLoose(matchedOpt.Value);
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
                                parsedVal = col.ConvertLoose(effectiveAns.MappedValue);
                                displayText = effectiveAns.MappedLabel ?? effectiveAns.MappedValue ?? cellText;
                                break;
                            case SpreadsheetValueAnswerAction.Create:
                                parsedVal = col.ConvertLoose(cellText);
                                displayText = cellText;
                                created = true;
                                break;
                            case SpreadsheetValueAnswerAction.LeaveEmpty:
                                parsedVal = null;
                                displayText = "";
                                break;
                            case SpreadsheetValueAnswerAction.SkipRows:
                                skippedByValue = true;
                                fieldIssue = new SpreadsheetRowIssue(col.Key, "value.skipped", SpreadsheetIssueLevel.Info, $"Left out: \"{cellText}\" is set to skip its rows", [cellText]);
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
                        fieldIssue = new SpreadsheetRowIssue(col.Key, code, SpreadsheetIssueLevel.Error, msg, [cellText]);
                        rowIssues.Add(fieldIssue);
                    }
                }
            }
            else
            {
                if (!TryParseCell(col, cellText, InvariantNumbers, out parsedVal, out var parsedText, out var parseErr, out var localizable, out var parseFailure))
                {
                    if (parseFailure is not null)
                    {
                        // The column's own parser threw: a failure of the host, so the uploader gets the generic issue and the log the exception.
                        NoteHostFailure($"the parser of the column '{col.Key}'", parseFailure);
                        fieldIssue = RowFailedIssue(col.Key);
                        rowIssues.Add(fieldIssue);
                    }
                    else
                    {
                        // Only a failure of the built-in text reading carries the cell text as an argument, so the component can show it in the
                        // user's locale. A refusal by the column's own parser, or by the property type (1.5 in a whole number column), is shown as written.
                        fieldIssue = new SpreadsheetRowIssue(col.Key, col.Kind.ToString().ToLowerInvariant(), SpreadsheetIssueLevel.Error, parseErr!, localizable ? [cellText] : null);
                        rowIssues.Add(fieldIssue);
                    }
                }
                else
                {
                    displayText = parsedText ?? cellText;
                }
            }

            // Run field rules on non-empty parsed values
            if (parsedVal is not null && col.Rules is { Count: > 0 })
            {
                for (var ruleIndex = 0; ruleIndex < col.Rules.Count; ruleIndex++)
                {
                    var rule = col.Rules[ruleIndex];
                    if (_ruleTimeouts.TryGetValue((col.Key, ruleIndex), out var seen) && Schema.RuleTimeoutLimit > 0 && seen >= Schema.RuleTimeoutLimit)
                    {
                        // A stopped rule no longer runs its regex, but its values are not waved through: they get the same issue a timeout gets,
                        // so a hostile file cannot make the values after the limit importable.
                        var skipped = _failedRules.Contains((col.Key, ruleIndex))
                            ? RowFailedIssue(col.Key)
                            : new SpreadsheetRowIssue(col.Key, "pattern", rule.Level, "The format could not be checked in time", ["timeout"]);
                        rowIssues.Add(skipped);
                        fieldIssue ??= skipped;
                        continue;
                    }
                    var passed = RunRule(rule, parsedVal, out var timedOut, out var ruleFailure);
                    if (ruleFailure is not null)
                    {
                        // The rule could not decide, so the row cannot be trusted whatever the level of the rule: it is blocked like a row the host's setter refuses.
                        // The uploader gets the generic issue, the log gets the exception, and the circuit breaker of the timeouts stops a rule that keeps throwing.
                        var failedCount = _ruleTimeouts.GetValueOrDefault((col.Key, ruleIndex)) + 1;
                        _ruleTimeouts[(col.Key, ruleIndex)] = failedCount;
                        _failedRules.Add((col.Key, ruleIndex));
                        var stopsNow = Schema.RuleTimeoutLimit > 0 && failedCount == Schema.RuleTimeoutLimit;
                        if (stopsNow)
                        {
                            _disabledRules.Add(new SpreadsheetDisabledRule(col.Key, rule.Code, failedCount, SpreadsheetDisabledReason.Failures));
                        }
                        NoteHostFailure($"the rule '{rule.Code}' of the column '{col.Key}'", ruleFailure, stopsNow);
                        var failed = RowFailedIssue(col.Key);
                        rowIssues.Add(failed);
                        fieldIssue ??= failed;
                        continue;
                    }
                    if (timedOut)
                    {
                        var count = _ruleTimeouts.GetValueOrDefault((col.Key, ruleIndex)) + 1;
                        _ruleTimeouts[(col.Key, ruleIndex)] = count;
                        if (Schema.RuleTimeoutLimit > 0 && count == Schema.RuleTimeoutLimit)
                        {
                            _disabledRules.Add(new SpreadsheetDisabledRule(col.Key, rule.Code, count));
                        }
                    }
                    if (!passed)
                    {
                        var ruleIssue = timedOut
                            ? new SpreadsheetRowIssue(col.Key, "pattern", rule.Level, "The format could not be checked in time", ["timeout"])
                            : new SpreadsheetRowIssue(col.Key, rule.Code, rule.Level, rule.Message, rule.MessageArgs);
                        rowIssues.Add(ruleIssue);
                        fieldIssue ??= ruleIssue;
                    }
                }
            }

            rowValues[fieldIndex] = parsedVal;
            cellViews[col.Key] = new SpreadsheetCellView(cellText, originalText, displayText, hasEdit, defaulted, created, fieldIssue);
        }

        var extraIssues = Schema.CheckRow(rowValues, out var rowFailure);
        if (rowFailure is not null) NoteHostFailure("the row factory, a property setter or ValidateRow", rowFailure);
        if (extraIssues is { Count: > 0 })
        {
            rowIssues.AddRange(extraIssues);
        }

        string? duplicateKey = null;
        if (Schema.RowKeyIndex >= 0)
        {
            var key = KeyText(rowValues[Schema.RowKeyIndex]).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key)) duplicateKey = key;
        }

        return new ParsedRow
        {
            Index = rIdx,
            RowNumber = _dataRowNumbers[rIdx],
            Values = rowValues,
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
        if (_asyncIssues.TryGetValue(pr.Index, out var asyncIssues))
        {
            (extra ??= []).AddRange(asyncIssues);
        }

        var duplicateDiscard = false;
        if (pr.DuplicateKey is { } dupKey && _keyGroups.TryGetValue(dupKey, out var group) && group.Count > 1)
        {
            switch (Schema.Duplicates)
            {
                case SpreadsheetDuplicatesPolicy.Error:
                    var other = _parsed[group[0] != pr.Index ? group[0] : group[1]];
                    (extra ??= []).Add(new SpreadsheetRowIssue(Schema.RowKeyField, "row.duplicate", SpreadsheetIssueLevel.Error, $"Same key as row {other.RowNumber}", [other.RowNumber.ToString(CultureInfo.InvariantCulture)]));
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
        if (Schema.RowKeyIndex >= 0
            && KeyText(pr.Values[Schema.RowKeyIndex]) is { Length: > 0 } keyStr
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
                (extra ??= []).Add(new SpreadsheetRowIssue(Schema.RowKeyField, "row.exists", SpreadsheetIssueLevel.Error, "Already stored", []));
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
            pr.Cells,
            issues,
            status,
            discard,
            mode,
            pr.Edited,
            discard is null && !hasError)
        {
            Values = pr.Values,
        };
    }

    // The key of a row as text, the same for the duplicate check, the existing-record check and the lookup: invariant, so a number or a date key does not depend on the culture.
    private static string KeyText(object? value) => SpreadsheetValueConverter.KeyText(value);

    private bool IsExistingKey(string key)
    {
        if (Schema.IsStoredKey(key)) return true;
        foreach (var found in _lookedUpRecords)
        {
            if (found.Contains(key)) return true;
        }
        return false;
    }

    // Incremental updates

    /// <summary>Re-parses the given rows and updates everything that depends on them: the duplicate-key index (and the other rows of the keys concerned), the rows and, when asked or when their unknown values changed, the value questions.</summary>
    private void ReparseRows(IEnumerable<int> rowIndexes, bool alwaysRebuildQuestions)
    {
        try
        {
            ReparseRowsCore(rowIndexes, alwaysRebuildQuestions);
        }
        finally
        {
            FlushHostFailures();
        }
    }

    private void ReparseRowsCore(IEnumerable<int> rowIndexes, bool alwaysRebuildQuestions)
    {
        var touched = new HashSet<int>();
        var unknownChanged = false;
        foreach (var rIdx in rowIndexes)
        {
            if (rIdx < 0 || rIdx >= _parsed.Count) continue;
            var old = _parsed[rIdx];
            var fresh = ParseRow(rIdx);
            _parsed[rIdx] = fresh;
            _asyncChecked.Remove(rIdx);
            _asyncIssues.Remove(rIdx);
            MarkAsyncDirty();
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
                col.CanCreateOption));
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
