using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// A saved mapping: headers to columns and answers to unknown values, stored as JSON and applied to the next file with the same headers.
[PinCulture]
public sealed class MappingProfileTests : BunitContext
{
    public MappingProfileTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetSchema<ImportRow> Schema(string key = "people") => SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = key;
            var colName = sb.Column(r => r.Name, col => col.Key("name").Label("Name").Required());
            sb.Column(r => r.Email, col => col.Key("email").Label("Email").Required().Rule(SpreadsheetRule.Email()));
            sb.Column(r => r.Level, col => col.Key("level")
                .Label("Level")
                .Options([new SpreadsheetOption<string?>("pro", "Pro"), new SpreadsheetOption<string?>("free", "Free")]));
            sb.Column(r => r.Note, col => col.Key("note").Label("Note"));
            sb.Column(r => r.Initial, col => col.Key("initial").From(colName));
        });

    // The file uses its own names for the columns.
    private const string BankFile = "Full name;E-mail address;Plan;Comment\nAda;ada@example.com;Gold;vip\nGrace;grace@example.com;Gold;\nLin;lin@example.com;Pro;";

    private static SpreadsheetImporter<ImportRow> MappedImporter()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText(BankFile, "bank.csv");
        importer.AssignColumn("name", 0);
        importer.AssignColumn("email", 1);
        importer.AssignColumn("level", 2);
        importer.IgnoreColumn("note");
        importer.AnswerValue("level", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Pro"));
        return importer;
    }

    [Fact]
    public void GetMappingProfile_captures_the_headers_the_ignored_columns_and_the_answers()
    {
        var profile = MappedImporter().GetMappingProfile();

        Assert.Equal(SpreadsheetMappingProfile.CurrentVersion, profile.Version);
        Assert.Equal("people", profile.SchemaKey);
        Assert.Equal(
            new Dictionary<string, string> { ["full name"] = "name", ["e-mail address"] = "email", ["plan"] = "level" },
            profile.Columns);
        Assert.Equal(["note"], profile.IgnoredColumns);
        var answer = Assert.Single(profile.ValueAnswers);
        Assert.Equal("level::gold", answer.Key);
        Assert.Equal(new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Pro"), answer.Value);
    }

    [Fact]
    public void Headers_matched_by_name_are_in_the_profile_and_computed_columns_are_not()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Name *;EMAIL\nAda;ada@example.com", "people.csv");

        var profile = importer.GetMappingProfile();

        Assert.Equal(new Dictionary<string, string> { ["name"] = "name", ["email"] = "email" }, profile.Columns);
        Assert.DoesNotContain("initial", profile.Columns.Values);
    }

    [Fact]
    public void A_profile_makes_a_round_trip_through_JSON()
    {
        var profile = MappedImporter().GetMappingProfile();

        var json = profile.ToJson();
        var back = SpreadsheetMappingProfile.FromJson(json)!;

        Assert.Equal(profile.Version, back.Version);
        Assert.Equal(profile.SchemaKey, back.SchemaKey);
        Assert.Equal(profile.Columns, back.Columns);
        Assert.Equal(profile.IgnoredColumns, back.IgnoredColumns);
        Assert.Equal(profile.ValueAnswers, back.ValueAnswers);
        Assert.Contains("\"schemaKey\": \"people\"", json, StringComparison.Ordinal);
        Assert.Contains("\"action\": \"Map\"", json, StringComparison.Ordinal);       // enums are written by name
        Assert.Contains("\"level::gold\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_JSON_uses_the_source_generated_context_and_tolerates_unknown_properties()
    {
        Assert.NotNull(SpreadsheetMappingProfileJsonContext.Default.SpreadsheetMappingProfile);
        var json = JsonSerializer.Serialize(MappedImporter().GetMappingProfile(), SpreadsheetMappingProfileJsonContext.Default.SpreadsheetMappingProfile);

        var back = SpreadsheetMappingProfile.FromJson(json.Replace("\"version\": 1", "\"version\": 1, \"future\": {\"x\": 1}", StringComparison.Ordinal));

        Assert.Equal("people", back?.SchemaKey);
        Assert.Throws<JsonException>(() => SpreadsheetMappingProfile.FromJson("{ not json"));
        Assert.Null(SpreadsheetMappingProfile.FromJson("null"));
        Assert.Throws<ArgumentNullException>(() => SpreadsheetMappingProfile.FromJson(null!));
    }

    [Fact]
    public void A_profile_without_optional_parts_reads_back_with_empty_collections()
    {
        var back = SpreadsheetMappingProfile.FromJson("""{ "columns": { "full name": "name" } }""")!;

        Assert.Null(back.SchemaKey);
        Assert.Equal("name", back.Columns["full name"]);
        Assert.Empty(back.IgnoredColumns);
        Assert.Empty(back.ValueAnswers);
    }

    [Fact]
    public void ApplyMappingProfile_maps_the_headers_of_a_new_file_and_resolves_its_values()
    {
        var profile = MappedImporter().GetMappingProfile();
        var next = new SpreadsheetImporter<ImportRow>(Schema());
        next.LoadText("Comment;Plan;E-mail address;Full name\nvip;Gold;ada@example.com;Ada", "next.csv");
        Assert.Contains(next.MissingFields, f => f.Column.Key == "name");

        var result = next.ApplyMappingProfile(profile);

        Assert.Equal(new SpreadsheetMappingApplyResult(3, 1, 1), result);
        Assert.True(result.Applied);
        Assert.Empty(next.MissingFields);
        Assert.Empty(next.OpenQuestions);
        Assert.Equal("Ada", next.Value(0).Name);
        Assert.Equal("pro", next.Value(0).Level);
        Assert.Equal(["Comment"], next.UnusedHeaders);                 // "note" was declared absent, so its header is not used
        Assert.Equal(SpreadsheetColumnStatus.Unmatched, next.Fields.Single(f => f.Column.Key == "note").Status);
    }

    [Fact]
    public void ApplyMappingProfile_skips_what_does_not_exist_and_a_profile_of_another_schema_or_version()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("A;B\n1;2", "ab.csv");

        var unknown = new SpreadsheetMappingProfile
        {
            Columns = new Dictionary<string, string> { ["a"] = "nothing", ["zzz"] = "name" },
            IgnoredColumns = ["nothing"],
            ValueAnswers = new Dictionary<string, SpreadsheetValueAnswer> { ["nothing::x"] = new(SpreadsheetValueAnswerAction.LeaveEmpty), ["broken"] = new(SpreadsheetValueAnswerAction.LeaveEmpty) },
        };
        Assert.False(importer.ApplyMappingProfile(unknown).Applied);

        var changed = 0;
        importer.Changed += () => changed++;
        Assert.False(importer.ApplyMappingProfile(new SpreadsheetMappingProfile { SchemaKey = "other", Columns = new Dictionary<string, string> { ["a"] = "name" } }).Applied);
        Assert.False(importer.ApplyMappingProfile(new SpreadsheetMappingProfile { Version = 99, Columns = new Dictionary<string, string> { ["a"] = "name" } }).Applied);
        Assert.Equal(0, changed);
        Assert.Throws<ArgumentNullException>(() => importer.ApplyMappingProfile(null!));

        var empty = new SpreadsheetImporter<ImportRow>(Schema());
        Assert.False(empty.ApplyMappingProfile(MappedImporter().GetMappingProfile()).Applied);     // nothing loaded
    }

    [Fact]
    public void MatchesMappingProfile_needs_every_mapped_header_and_allows_extra_ones_in_any_order()
    {
        var profile = MappedImporter().GetMappingProfile();
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        Assert.False(importer.MatchesMappingProfile(profile));      // no file yet

        importer.LoadText("Extra;Plan;Full name;E-mail address\nx;Gold;Ada;ada@example.com", "a.csv");
        Assert.True(importer.MatchesMappingProfile(profile));

        importer.LoadText("Full name;E-mail address\nAda;ada@example.com", "b.csv");
        Assert.False(importer.MatchesMappingProfile(profile));      // "Plan" is missing

        Assert.False(importer.MatchesMappingProfile(new SpreadsheetMappingProfile()));   // maps nothing: matches nothing
    }

    [Fact]
    public void SavedMapping_is_applied_when_a_file_with_matching_headers_is_loaded()
    {
        var profile = MappedImporter().GetMappingProfile();
        var next = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = profile };
        Assert.False(next.SavedMappingApplied);

        next.LoadText("Full name;E-mail address;Plan\nAda;ada@example.com;Gold", "next.csv");

        Assert.True(next.SavedMappingApplied);
        Assert.Empty(next.MissingFields);
        Assert.Empty(next.OpenQuestions);
        Assert.Single(next.ImportableRows);
        Assert.Equal("user", next.Fields.Single(f => f.Column.Key == "name").AssignedBy);
    }

    [Fact]
    public void SavedMapping_is_not_applied_when_the_headers_do_not_match()
    {
        var next = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = MappedImporter().GetMappingProfile() };

        next.LoadText("Name;Email\nAda;ada@example.com", "plain.csv");

        Assert.False(next.SavedMappingApplied);
        Assert.Equal("file", next.Fields.Single(f => f.Column.Key == "name").AssignedBy);   // matched by name as usual
    }

    [Fact]
    public void Setting_SavedMapping_on_a_loaded_file_applies_it_at_once_and_clearing_it_applies_nothing()
    {
        var next = new SpreadsheetImporter<ImportRow>(Schema());
        next.LoadText("Full name;E-mail address;Plan\nAda;ada@example.com;Gold", "next.csv");
        Assert.NotEmpty(next.MissingFields);

        next.SavedMapping = MappedImporter().GetMappingProfile();

        Assert.True(next.SavedMappingApplied);
        Assert.Empty(next.MissingFields);

        next.SavedMapping = null;
        Assert.False(next.SavedMappingApplied);
    }

    [Fact]
    public void SavedMapping_follows_the_header_row_and_the_sheet_the_user_picks_and_ClearFile()
    {
        var next = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = MappedImporter().GetMappingProfile() };
        next.LoadText("Title row;;\nFull name;E-mail address;Plan\nAda;ada@example.com;Gold", "banner.csv");
        Assert.True(next.SavedMappingApplied);

        next.SetHeaderRow(0);                                    // the user picks the wrong row: nothing matches
        Assert.False(next.SavedMappingApplied);
        next.SetHeaderRow(1);
        Assert.True(next.SavedMappingApplied);

        next.ClearFile();
        Assert.False(next.SavedMappingApplied);
    }

    [Fact]
    public void A_profile_for_another_schema_key_is_not_applied_automatically()
    {
        var next = new SpreadsheetImporter<ImportRow>(Schema("orders")) { SavedMapping = MappedImporter().GetMappingProfile() };

        next.LoadText(BankFile, "bank.csv");

        Assert.False(next.SavedMappingApplied);
    }

    [Fact]
    public void A_choice_made_after_the_automatic_mapping_overrides_it()
    {
        var next = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = MappedImporter().GetMappingProfile() };
        next.LoadText(BankFile, "bank.csv");

        next.AssignColumn("name", 3);                            // the user prefers the comment column for the name

        Assert.Equal("Comment", next.Fields.Single(f => f.Column.Key == "name").HeaderText);
        next.ResetColumn("name");
        Assert.Equal("file", next.Fields.Single(f => f.Column.Key == "name").AssignedBy ?? "file");   // matched by header again, or not at all
        Assert.NotEqual("Comment", next.Fields.Single(f => f.Column.Key == "name").HeaderText);
    }

    [Fact]
    public void A_header_is_used_for_one_column_only()
    {
        var profile = new SpreadsheetMappingProfile
        {
            Columns = new Dictionary<string, string> { ["full name"] = "name", ["name"] = "note" },
        };
        var importer = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = profile };

        importer.LoadText("Name;Full name\nAda;Ada Lovelace", "dups.csv");

        Assert.Equal("Full name", importer.Fields.Single(f => f.Column.Key == "name").HeaderText);
        Assert.Equal("Name", importer.Fields.Single(f => f.Column.Key == "note").HeaderText);
    }

    [Fact]
    public void A_stored_profile_can_be_read_back_and_used_for_the_next_import()
    {
        var stored = MappedImporter().GetMappingProfile().ToJson();      // what you keep in a database or local storage

        var next = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = SpreadsheetMappingProfile.FromJson(stored) };
        next.LoadText("Plan;Full name;E-mail address\nGold;Grace;grace@example.com", "next.csv");

        Assert.Equal("pro", next.Value(0).Level);
        Assert.Equal(SpreadsheetColumnStatus.Matched, next.Fields.Single(f => f.Column.Key == "email").Status);
    }

    [Fact]
    public void The_component_applies_a_saved_mapping_and_says_so()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema()) { SavedMapping = MappedImporter().GetMappingProfile() };
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("Full name;E-mail address;Plan\nAda;ada@example.com;Gold", "bank.csv"));

        cut.WaitForAssertion(() => Assert.True(importer.FileLoaded));
        cut.WaitForAssertion(() => Assert.Contains("saved column mapping", cut.Find("[data-slot=spreadsheet-saved-mapping]").TextContent, StringComparison.Ordinal));
        Assert.Equal("status", cut.Find("[data-slot=spreadsheet-saved-mapping]").GetAttribute("role"));
        Assert.Empty(importer.MissingFields);
    }
}
