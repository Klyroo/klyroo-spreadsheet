using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klyroo.Spreadsheet;

/// <summary>
/// A column mapping and the answers to unknown values, in a form you can store and apply to the next file with the same headers:
/// <see cref="SpreadsheetImporter.GetMappingProfile"/> builds it from the current state, <see cref="SpreadsheetImporter.ApplyMappingProfile"/> applies it, and
/// <see cref="SpreadsheetImporter.SavedMapping"/> applies it automatically when a file's headers match. It holds no cell data, only header texts, column keys and answers.
/// </summary>
/// <remarks>
/// Serialize it with <see cref="ToJson"/> and read it back with <see cref="FromJson"/>. They use <see cref="SpreadsheetMappingProfileJsonContext"/>, a source-generated
/// context, so storing a profile works with NativeAOT and trimming. To store it inside your own JSON, add <c>[JsonSerializable(typeof(SpreadsheetMappingProfile))]</c> to your context.
/// </remarks>
public sealed record SpreadsheetMappingProfile
{
    // The JSON deserializer leaves a property it did not read as null, so the getters turn that into an empty collection.
    private static readonly IReadOnlyDictionary<string, string> NoColumns = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, SpreadsheetValueAnswer> NoAnswers = new Dictionary<string, SpreadsheetValueAnswer>();
    private readonly IReadOnlyDictionary<string, string>? _columns;
    private readonly IReadOnlyDictionary<string, string>? _repeatedColumns;
    private readonly IReadOnlyList<string>? _ignoredColumns;
    private readonly IReadOnlyDictionary<string, SpreadsheetValueAnswer>? _valueAnswers;

    /// <summary>The format version of <see cref="Version"/> this library writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The format version. A profile of an unknown version is not applied.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>The <see cref="SpreadsheetSchema.Key"/> the profile was made for. A profile with a key is applied only to a schema with the same key. <see langword="null"/> applies to any schema.</summary>
    public string? SchemaKey { get; init; }

    /// <summary>The file headers and the column they were mapped to: the header after <see cref="SpreadsheetImporter.NormalizeText"/> (lower case, no accents, no trailing <c>*</c>) mapped to the <see cref="SpreadsheetColumnSchema.Key"/>. When a header appears several times in the file, this holds its first occurrence and <see cref="RepeatedColumns"/> the others.</summary>
    public IReadOnlyDictionary<string, string> Columns
    {
        get => _columns ?? NoColumns;
        init => _columns = value;
    }

    /// <summary>The columns mapped to the second, third and later occurrence of a header that repeats in the file (two <c>Date</c> columns, for example): <c>"2:date"</c> is the second column headed <c>date</c>, mapped to a column key. It keeps a re-applied profile from sending both occurrences to the same column.</summary>
    public IReadOnlyDictionary<string, string> RepeatedColumns
    {
        get => _repeatedColumns ?? NoColumns;
        init => _repeatedColumns = value;
    }

    /// <summary>The keys of the columns the user declared absent from the file with <see cref="SpreadsheetImporter.IgnoreColumn"/>.</summary>
    public IReadOnlyList<string> IgnoredColumns
    {
        get => _ignoredColumns ?? [];
        init => _ignoredColumns = value;
    }

    /// <summary>The answers to unknown select values, by question id (<c>columnKey::normalizedValue</c>, see <see cref="SpreadsheetValueQuestion.Id"/>).</summary>
    public IReadOnlyDictionary<string, SpreadsheetValueAnswer> ValueAnswers
    {
        get => _valueAnswers ?? NoAnswers;
        init => _valueAnswers = value;
    }

    /// <summary>Writes the profile as indented JSON (camelCase names, enums as strings).</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, SpreadsheetMappingProfileJsonContext.Default.SpreadsheetMappingProfile);

    /// <summary>Reads a profile written by <see cref="ToJson"/>.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The profile, or <see langword="null"/> when the text is the JSON value <c>null</c>.</returns>
    /// <exception cref="JsonException">The text is not valid JSON for a profile.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static SpreadsheetMappingProfile? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, SpreadsheetMappingProfileJsonContext.Default.SpreadsheetMappingProfile);
    }
}

/// <summary>What <see cref="SpreadsheetImporter.ApplyMappingProfile"/> changed.</summary>
/// <param name="ColumnsApplied">The columns mapped to a file header of the profile.</param>
/// <param name="ColumnsIgnored">The columns declared absent from the file.</param>
/// <param name="ValueAnswersApplied">The answers recorded.</param>
public sealed record SpreadsheetMappingApplyResult(int ColumnsApplied, int ColumnsIgnored, int ValueAnswersApplied)
{
    /// <summary><see langword="true"/> when anything was applied.</summary>
    public bool Applied => ColumnsApplied + ColumnsIgnored + ValueAnswersApplied > 0;
}

/// <summary>The source-generated JSON context of <see cref="SpreadsheetMappingProfile"/>, safe for NativeAOT and trimming.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SpreadsheetMappingProfile))]
public sealed partial class SpreadsheetMappingProfileJsonContext : JsonSerializerContext
{
}
