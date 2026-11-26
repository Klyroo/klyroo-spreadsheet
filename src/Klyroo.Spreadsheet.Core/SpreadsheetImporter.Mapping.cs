namespace Klyroo.Spreadsheet;

// Saved mappings: the mapping of file headers to schema columns and the answers to unknown values, exported and applied again.
public sealed partial class SpreadsheetImporter
{
    private SpreadsheetMappingProfile? _savedMapping;

    /// <summary>
    /// A stored mapping to apply automatically. Whenever a file is loaded, or another sheet or header row is chosen, and the headers match the profile
    /// (<see cref="MatchesMappingProfile"/>), its column mapping and answers are applied before the rows are read, so the user does not map the same
    /// export twice. Setting it while a file is loaded applies it at once when it matches. <see langword="null"/> (the default) applies nothing.
    /// </summary>
    public SpreadsheetMappingProfile? SavedMapping
    {
        get => _savedMapping;
        set
        {
            _savedMapping = value;
            SavedMappingApplied = false;
            if (value is null || !FileLoaded || MappingLocked) return;
            if (ApplyProfile(value, Headers))
            {
                SavedMappingApplied = true;
                Recompute();
            }
            Changed?.Invoke();
        }
    }

    /// <summary><see langword="true"/> when <see cref="SavedMapping"/> matched the headers of the loaded file and was applied.</summary>
    public bool SavedMappingApplied { get; private set; }

    /// <summary>Captures the current mapping: each file header that feeds a column (whether matched by name, by suggestion or by the user), the columns declared absent, and the answers the user gave to unknown values. Store it with <see cref="SpreadsheetMappingProfile.ToJson"/>.</summary>
    /// <returns>The profile, with <see cref="SpreadsheetMappingProfile.SchemaKey"/> set to <see cref="SpreadsheetSchema.Key"/>.</returns>
    public SpreadsheetMappingProfile GetMappingProfile()
    {
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        var repeated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in _fields)
        {
            if (!string.IsNullOrEmpty(field.Column.From) || field.HeaderIndex is not { } index || field.HeaderText is null) continue;
            var header = NormalizeText(field.HeaderText);
            if (header.Length == 0) continue;
            var occurrence = OccurrenceOf(Headers, index);
            if (occurrence == 1) columns.TryAdd(header, field.Column.Key);
            else repeated.TryAdd($"{occurrence}:{header}", field.Column.Key);
        }
        return new SpreadsheetMappingProfile
        {
            SchemaKey = Schema.Key,
            Columns = columns,
            RepeatedColumns = repeated,
            IgnoredColumns = _manualColumns.Where(kv => kv.Value is null).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList(),
            ValueAnswers = new Dictionary<string, SpreadsheetValueAnswer>(_valueAnswers, StringComparer.Ordinal),
        };
    }

    /// <summary>Whether a profile fits the loaded file: it has the version and schema key of this importer, and every header it maps is a header of the file (compared after <see cref="NormalizeText"/>, in any order, extra file headers allowed).</summary>
    /// <param name="profile">The profile to test.</param>
    /// <returns><see langword="true"/> when <see cref="ApplyMappingProfile"/> would map at least the profile's columns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    public bool MatchesMappingProfile(SpreadsheetMappingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return FileLoaded && ProfileMatches(profile, Headers);
    }

    /// <summary>Applies a profile to the loaded file: maps the file headers it knows to their columns, declares its ignored columns absent, and records its value answers. It overrides earlier manual choices for the columns and answers it names and keeps the others. It does not check <see cref="MatchesMappingProfile"/>: headers the file lacks are skipped.</summary>
    /// <param name="profile">The profile to apply.</param>
    /// <returns>What was applied. Nothing changes, and <see cref="Changed"/> is not raised, for a profile of another schema or version, or when no file is loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    public SpreadsheetMappingApplyResult ApplyMappingProfile(SpreadsheetMappingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!FileLoaded || MappingLocked || !ProfileUsable(profile)) return new SpreadsheetMappingApplyResult(0, 0, 0);
        var result = ApplyProfileCore(profile, Headers);
        if (result.Applied)
        {
            Recompute();
            Changed?.Invoke();
            MappingChanged?.Invoke();
        }
        return result;
    }

    private bool ProfileUsable(SpreadsheetMappingProfile profile) =>
        profile.Version == SpreadsheetMappingProfile.CurrentVersion
        && (profile.SchemaKey is null || profile.SchemaKey == Schema.Key);

    private bool ProfileMatches(SpreadsheetMappingProfile profile, IReadOnlyList<string> headers)
    {
        if (!ProfileUsable(profile) || profile.Columns.Count == 0) return false;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var header in headers.Select(NormalizeText).Where(h => h.Length > 0))
        {
            counts[header] = counts.GetValueOrDefault(header) + 1;
        }
        if (!profile.Columns.Keys.All(counts.ContainsKey)) return false;
        foreach (var key in profile.RepeatedColumns.Keys)
        {
            if (!TrySplitRepeated(key, out var occurrence, out var header) || counts.GetValueOrDefault(header) < occurrence) return false;
        }
        return true;
    }

    // The 1-based position of a header among the headers with the same normalized text.
    private static int OccurrenceOf(IReadOnlyList<string> headers, int index)
    {
        var text = NormalizeText(headers[index]);
        var occurrence = 1;
        for (var i = 0; i < index; i++)
        {
            if (NormalizeText(headers[i]) == text) occurrence++;
        }
        return occurrence;
    }

    // "2:date" is the second column headed "date".
    private static bool TrySplitRepeated(string key, out int occurrence, out string header)
    {
        occurrence = 0;
        header = "";
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || !int.TryParse(key.AsSpan(0, colon), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out occurrence) || occurrence < 2) return false;
        header = key[(colon + 1)..];
        return header.Length > 0;
    }

    // Before the first read of a freshly chosen layout: map by the saved profile when its headers match.
    private void ApplySavedMappingOnLoad()
    {
        SavedMappingApplied = false;
        if (_savedMapping is null) return;
        var sheet = CurrentSheet;
        if (sheet is null || _headerRowIndex < 0 || _headerRowIndex >= sheet.Rows.Count) return;
        SavedMappingApplied = ApplyProfile(_savedMapping, sheet.Rows[_headerRowIndex]);
    }

    private bool ApplyProfile(SpreadsheetMappingProfile profile, IReadOnlyList<string> headers)
    {
        if (!ProfileMatches(profile, headers)) return false;
        ApplyProfileCore(profile, headers);
        return true;
    }

    // A profile comes from storage, so it is untrusted: null keys and values, unknown columns, answers that do not fit the column and undefined enum values are skipped.
    private SpreadsheetMappingApplyResult ApplyProfileCore(SpreadsheetMappingProfile profile, IReadOnlyList<string> headers)
    {
        var columnsByKey = Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)).ToDictionary(c => c.Key, StringComparer.Ordinal);
        var usedHeaders = new HashSet<int>();
        var mapped = 0;

        // Index of the nth header with a normalized text.
        int? Find(string header, int occurrence)
        {
            var seen = 0;
            for (var h = 0; h < headers.Count; h++)
            {
                if (NormalizeText(headers[h]) != header) continue;
                if (++seen == occurrence) return h;
            }
            return null;
        }

        void Map(string header, int occurrence, string? key)
        {
            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(key) || !columnsByKey.ContainsKey(key)) return;
            if (Find(header, occurrence) is not { } h || usedHeaders.Contains(h)) return;
            usedHeaders.Add(h);
            foreach (var other in _manualColumns.Where(kv => kv.Value == h && kv.Key != key).Select(kv => kv.Key).ToList())
            {
                _manualColumns.Remove(other);
            }
            _manualColumns[key] = h;
            mapped++;
        }

        foreach (var (header, key) in profile.Columns) Map(header, 1, key);
        foreach (var (repeatedKey, key) in profile.RepeatedColumns)
        {
            if (repeatedKey is not null && TrySplitRepeated(repeatedKey, out var occurrence, out var header)) Map(header, occurrence, key);
        }

        var ignored = 0;
        foreach (var key in profile.IgnoredColumns)
        {
            if (string.IsNullOrEmpty(key) || !columnsByKey.ContainsKey(key) || profile.Columns.Values.Contains(key) || profile.RepeatedColumns.Values.Contains(key)) continue;
            _manualColumns[key] = null;
            ignored++;
        }

        var answers = 0;
        foreach (var (id, answer) in profile.ValueAnswers)
        {
            if (string.IsNullOrEmpty(id) || answer is null || !Enum.IsDefined(answer.Action)) continue;
            var separator = id.IndexOf("::", StringComparison.Ordinal);
            if (separator <= 0 || !columnsByKey.TryGetValue(id[..separator], out var column)) continue;
            if (!AnswerFits(column, answer)) continue;
            _valueAnswers[id] = answer;
            answers++;
        }
        return new SpreadsheetMappingApplyResult(mapped, ignored, answers);
    }

    // A stored answer must make sense for the column: a Map answer names one of its options, and only a column that can create options accepts Create.
    private static bool AnswerFits(SpreadsheetColumnSchema column, SpreadsheetValueAnswer answer) => answer.Action switch
    {
        SpreadsheetValueAnswerAction.Map => column.Kind == SpreadsheetColumnKind.Select
            && answer.MappedValue is { } value && column.Options?.Any(o => o.Value == value) == true,
        SpreadsheetValueAnswerAction.Create => column.OnCreateOption is not null || column.OnCreateOptionAsync is not null,
        _ => true,
    };
}
