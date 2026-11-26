using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// Profiles saved before columns were typed hold the lowercase keys of the old schema; the keys are now the property names.
public sealed class ProfileKeyCaseTests
{
    private const string Bank = "Full name;E-mail address;Plan;Comment\nAda;ada@example.com;Gold;vip\nLin;lin@example.com;Pro;";

    private static SpreadsheetSchema<ImportRow> Schema() => SpreadsheetSchema.For<ImportRow>(sb =>
    {
        sb.Key = "people";
        sb.Column(r => r.Name, c => c.Required());
        sb.Column(r => r.Email, c => c.Required());
        sb.Column(r => r.Level, c => c.Options([new SpreadsheetOption<string?>("pro", "Pro"), new SpreadsheetOption<string?>("free", "Free")]));
        sb.Column(r => r.Note);
    });

    private static SpreadsheetMappingProfile OldProfile() => new()
    {
        SchemaKey = "people",
        Columns = new Dictionary<string, string> { ["full name"] = "name", ["e-mail address"] = "email", ["plan"] = "level" },
        IgnoredColumns = ["note"],
        ValueAnswers = new Dictionary<string, SpreadsheetValueAnswer> { ["level::gold"] = new(SpreadsheetValueAnswerAction.Map, "pro", "Pro") },
    };

    [Fact]
    public void A_profile_with_lowercase_keys_still_applies_to_the_property_name_keys()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText(Bank, "bank.csv");

        var result = importer.ApplyMappingProfile(OldProfile());

        Assert.Equal(new SpreadsheetMappingApplyResult(3, 1, 1), result);
        Assert.Equal(["Name", "Email", "Level"], importer.Fields.Where(f => f.HeaderIndex is not null).Select(f => f.Column.Key));
        Assert.Equal(SpreadsheetColumnStatus.Unmatched, importer.Fields.Single(f => f.Column.Key == "Note").Status);
        Assert.Empty(importer.OpenQuestions);
        Assert.Equal("pro", importer.Value(0).Level);
        Assert.All(importer.AllRows, r => Assert.True(r.Importable));
    }

    [Fact]
    public void A_saved_mapping_with_lowercase_keys_is_applied_when_the_file_loads()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = OldProfile() };

        importer.LoadText(Bank, "bank.csv");

        Assert.True(importer.SavedMappingApplied);
        Assert.Equal("Full name", importer.Fields.Single(f => f.Column.Key == "Name").HeaderText);
        Assert.All(importer.AllRows, r => Assert.True(r.Importable));
    }

    [Fact]
    public void An_exact_key_wins_and_an_ambiguous_case_insensitive_one_is_skipped()
    {
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "people";
            sb.Column(r => r.Name, c => c.Key("name"));
            sb.Column(r => r.Email, c => c.Key("Name"));
            sb.Column(r => r.Note, c => c.Key("Note"));
        });
        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("A;B;C\n1;2;3", "x.csv");

        var result = importer.ApplyMappingProfile(new SpreadsheetMappingProfile
        {
            SchemaKey = "people",
            Columns = new Dictionary<string, string> { ["a"] = "name", ["b"] = "NAME", ["c"] = "note" },
        });

        Assert.Equal(2, result.ColumnsApplied);
        Assert.Equal(0, importer.Fields.Single(f => f.Column.Key == "name").HeaderIndex);
        Assert.Null(importer.Fields.Single(f => f.Column.Key == "Name").HeaderIndex);
        Assert.Equal(2, importer.Fields.Single(f => f.Column.Key == "Note").HeaderIndex);
    }
}
