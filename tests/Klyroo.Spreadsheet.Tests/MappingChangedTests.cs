using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The app is told when the user's mapping changes, so it can persist the profile.
public sealed class MappingChangedTests : BunitContext
{
    public MappingChangedTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetImporter<ImportRow> NewImporter() => new(SpreadsheetSchema.For<ImportRow>(sb =>
    {
        sb.Key = "people";
        sb.Column(r => r.Name, col => col.Key("name").Label("Name").Required());
        sb.Column(r => r.Level, col => col.Key("level").Label("Level").Options([new SpreadsheetOption<string?>("pro", "Pro")]));
    }));

    private const string File = "Full name;Plan\nAda;Gold";

    [Fact]
    public void The_importer_raises_MappingChanged_for_the_users_choices_only()
    {
        var importer = NewImporter();
        var raised = 0;
        importer.MappingChanged += () => raised++;

        importer.LoadText(File, "people.csv");                    // loading and automatic matching are not the user's choice
        Assert.Equal(0, raised);

        importer.AssignColumn("name", 0);
        importer.IgnoreColumn("level");
        importer.ResetColumn("level");
        importer.AnswerValue("level", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty));
        importer.ClearAnswer("level", "Gold");
        Assert.Equal(5, raised);

        importer.EditCell(0, "name", "Grace");                    // a cell edit is not a mapping change
        importer.SetReviewSearch("x");
        Assert.Equal(5, raised);

        importer.ApplyMappingProfile(new SpreadsheetMappingProfile { Columns = new Dictionary<string, string> { ["plan"] = "level" } });
        Assert.Equal(6, raised);
        importer.ApplyMappingProfile(new SpreadsheetMappingProfile { Columns = new Dictionary<string, string> { ["nothing"] = "level" } });
        Assert.Equal(6, raised);                                  // nothing applied, nothing raised
    }

    [Fact]
    public void The_component_calls_OnMappingProfileChanged_with_the_current_profile()
    {
        var importer = NewImporter();
        importer.LoadText(File, "people.csv");
        var profiles = new List<SpreadsheetMappingProfile>();
        var cut = Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, importer)
            .Add(x => x.OnMappingProfileChanged, profile => profiles.Add(profile)));
        Assert.Empty(profiles);                                   // rendering and the automatic match raise nothing

        importer.NextStep();                                      // a required column is missing: the columns step
        cut.Find("[data-field=name] [data-slot=spreadsheet-column-select]").Change("0");

        var first = Assert.Single(profiles);
        Assert.Equal("people", first.SchemaKey);
        Assert.Equal("name", first.Columns["full name"]);

        importer.AnswerValue("level", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Pro"));
        Assert.Equal(2, profiles.Count);
        Assert.Equal("pro", profiles[1].ValueAnswers["level::gold"].MappedValue);
    }

    [Fact]
    public void A_profile_the_callback_received_can_be_given_back_as_SavedMapping()
    {
        const string levelFile = "Full name;Level\nAda;Gold";
        var importer = NewImporter();
        importer.LoadText(levelFile, "people.csv");
        SpreadsheetMappingProfile? stored = null;
        Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, importer)
            .Add(x => x.OnMappingProfileChanged, profile => stored = profile));
        importer.AssignColumn("name", 0);
        importer.AnswerValue("level", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Pro"));

        var next = NewImporter();
        next.SavedMapping = SpreadsheetMappingProfile.FromJson(stored!.ToJson());
        next.LoadText(levelFile, "next.csv");

        Assert.True(next.SavedMappingApplied);
        Assert.Equal("pro", next.Value(0).Level);
        Assert.Empty(next.MissingFields);
    }

    [Fact]
    public async Task The_component_stops_listening_when_disposed_or_when_the_importer_changes()
    {
        var first = NewImporter();
        first.LoadText(File, "people.csv");
        var received = 0;
        var cut = Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, first)
            .Add(x => x.OnMappingProfileChanged, _ => received++));

        var second = NewImporter();
        second.LoadText(File, "people.csv");
        cut.Render(p => p.Add(x => x.Importer, second));
        first.AssignColumn("name", 0);
        Assert.Equal(0, received);
        second.AssignColumn("name", 0);
        Assert.Equal(1, received);

        await DisposeComponentsAsync();
        second.AssignColumn("name", 0);
        Assert.Equal(1, received);
    }

    [Fact]
    public void Without_a_callback_nothing_is_built()
    {
        var importer = NewImporter();
        importer.LoadText(File, "people.csv");
        Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        importer.AssignColumn("name", 0);                         // no handler: must not throw

        Assert.True(importer.FileLoaded);
    }
}
