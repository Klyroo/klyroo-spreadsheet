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
            if (value is null || !FileLoaded) return;
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
        foreach (var field in _fields)
        {
            if (!string.IsNullOrEmpty(field.Column.From) || field.HeaderIndex is null || field.HeaderText is null) continue;
            var header = NormalizeText(field.HeaderText);
            if (header.Length > 0) columns.TryAdd(header, field.Column.Key);
        }
        return new SpreadsheetMappingProfile
        {
            SchemaKey = Schema.Key,
            Columns = columns,
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
        if (!FileLoaded || !ProfileUsable(profile)) return new SpreadsheetMappingApplyResult(0, 0, 0);
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
        var present = headers.Select(NormalizeText).Where(h => h.Length > 0).ToHashSet(StringComparer.Ordinal);
        return profile.Columns.Keys.All(present.Contains);
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

    private SpreadsheetMappingApplyResult ApplyProfileCore(SpreadsheetMappingProfile profile, IReadOnlyList<string> headers)
    {
        var columnsByKey = Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)).ToDictionary(c => c.Key, StringComparer.Ordinal);
        var usedHeaders = new HashSet<int>();
        var mapped = 0;
        foreach (var (normalizedHeader, key) in profile.Columns)
        {
            if (!columnsByKey.ContainsKey(key)) continue;
            for (var h = 0; h < headers.Count; h++)
            {
                if (usedHeaders.Contains(h) || NormalizeText(headers[h]) != normalizedHeader) continue;
                usedHeaders.Add(h);
                foreach (var other in _manualColumns.Where(kv => kv.Value == h && kv.Key != key).Select(kv => kv.Key).ToList())
                {
                    _manualColumns.Remove(other);
                }
                _manualColumns[key] = h;
                mapped++;
                break;
            }
        }

        var ignored = 0;
        foreach (var key in profile.IgnoredColumns)
        {
            if (!columnsByKey.ContainsKey(key) || profile.Columns.Values.Contains(key)) continue;
            _manualColumns[key] = null;
            ignored++;
        }

        var answers = 0;
        foreach (var (id, answer) in profile.ValueAnswers)
        {
            var separator = id.IndexOf("::", StringComparison.Ordinal);
            if (separator <= 0 || !columnsByKey.ContainsKey(id[..separator]) || answer is null) continue;
            _valueAnswers[id] = answer;
            answers++;
        }
        return new SpreadsheetMappingApplyResult(mapped, ignored, answers);
    }
}
