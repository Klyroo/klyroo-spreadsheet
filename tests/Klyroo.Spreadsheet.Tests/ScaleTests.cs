using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// Large files: the importer touches only the rows a change concerns, caches the lists it builds, and pages the review.
public sealed class ScaleTests : BunitContext
{
    private const int Rows = 50_000;

    public ScaleTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetSchema BulkSchema(int? maxRows = null) => new()
    {
        Key = "bulk",
        MaxRows = maxRows,
        RowKeyField = "code",
        Columns =
        [
            new SpreadsheetColumnSchema { Key = "code", Label = "Code", Required = true },
            new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] },
            new SpreadsheetColumnSchema { Key = "amount", Label = "Amount", Kind = SpreadsheetColumnKind.Number, Rules = [SpreadsheetRule.Min(0)] },
            new SpreadsheetColumnSchema { Key = "level", Label = "Level", Kind = SpreadsheetColumnKind.Select, Options = [new SpreadsheetOption("a", "A"), new SpreadsheetOption("b", "B")] },
        ],
    };

    private static string BulkCsv(int rows)
    {
        var sb = new StringBuilder(rows * 48);
        sb.AppendLine("Code;Email;Amount;Level");
        for (var i = 1; i <= rows; i++)
        {
            sb.Append("SKU-").Append(i.ToString("D6", CultureInfo.InvariantCulture)).Append(";user").Append(i).Append("@klyroo.dev;")
              .Append((i * 1.5).ToString("F1", CultureInfo.InvariantCulture)).Append(';').Append(i % 2 == 0 ? 'A' : 'B').Append('\n');
        }
        return sb.ToString();
    }

    [Fact]
    public void Fifty_thousand_rows_load_and_take_edits_within_a_generous_bound()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        var watch = Stopwatch.StartNew();
        importer.LoadText(BulkCsv(Rows), "bulk.csv");
        var load = watch.Elapsed;

        Assert.Equal(Rows, importer.AllRows.Count);
        Assert.Equal(Rows, importer.ImportableRows.Count);
        Assert.True(load < TimeSpan.FromSeconds(30), $"Loading {Rows} rows took {load}");

        // 200 edits that break and fix rows, read the lists the way a render does after each of them.
        watch.Restart();
        for (var i = 0; i < 100; i++)
        {
            importer.EditCell(i, "email", "not-an-email");
            Assert.Equal(Rows - 1, importer.ImportableRows.Count);
            Assert.Single(importer.InvalidRows);
            _ = importer.ReviewPageRows;
            importer.EditCell(i, "email", $"fixed{i}@klyroo.dev");
            Assert.Equal(Rows, importer.ImportableRows.Count);
        }
        var edits = watch.Elapsed;
        Assert.True(edits < TimeSpan.FromSeconds(30), $"200 edits of a {Rows}-row file took {edits}");

        // An edit that makes two keys equal flags both rows, and fixing it clears both, without reading the file again.
        importer.EditCell(10, "code", importer.AllRows[11].Data["code"]!.ToString()!);
        Assert.Equal(2, importer.InvalidRows.Count);
        Assert.All(importer.InvalidRows, r => Assert.Contains(r.Issues, i => i.Code == "row.duplicate"));
        importer.RevertCell(10, "code");
        Assert.Empty(importer.InvalidRows);
    }

    [Fact]
    public void Editing_a_cell_parses_only_the_edited_row()
    {
        var parsed = 0;
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns = [new SpreadsheetColumnSchema { Key = "name", CustomParse = s => { parsed++; return s.Trim(); } }],
        });
        importer.LoadText("Name\n" + string.Join('\n', Enumerable.Range(0, 1_000).Select(i => $"row{i}")), "names.csv");
        Assert.Equal(1_000, parsed);

        importer.EditCell(500, "name", "edited");
        Assert.Equal(1_001, parsed);
        importer.RevertCell(500, "name");
        Assert.Equal(1_002, parsed);
        importer.DiscardRows([1, 2, 3]);
        importer.RestoreRows([2]);
        Assert.Equal(1_002, parsed);
        Assert.Equal(2, importer.DiscardedRows.Count);
    }

    [Fact]
    public void The_lists_are_cached_until_something_changes()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(20), "bulk.csv");

        Assert.Same(importer.VisibleReviewRows, importer.VisibleReviewRows);
        Assert.Same(importer.ImportableRows, importer.ImportableRows);
        Assert.Same(importer.InvalidRows, importer.InvalidRows);
        Assert.Same(importer.DiscardedRows, importer.DiscardedRows);
        Assert.Same(importer.AllRows, importer.AllRows);

        var before = importer.AllRows;
        var visibleBefore = importer.VisibleReviewRows;
        importer.EditCell(0, "email", "bad");

        Assert.NotSame(visibleBefore, importer.VisibleReviewRows);
        Assert.Equal(20, before.Count);
        Assert.Empty(before[0].Issues);                     // a list taken earlier is a snapshot, not a view
        Assert.Single(importer.AllRows[0].Issues);

        var cachedVisible = importer.VisibleReviewRows;
        importer.SetReviewSearch("SKU-000003");
        Assert.NotSame(cachedVisible, importer.VisibleReviewRows);
        Assert.Single(importer.VisibleReviewRows);
    }

    [Fact]
    public void Changing_a_key_updates_the_rows_that_share_the_old_and_the_new_key()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            RowKeyField = "code",
            Duplicates = SpreadsheetDuplicatesPolicy.Error,
            Columns = [new SpreadsheetColumnSchema { Key = "code" }],
        });
        importer.LoadText("Code\nA\nB\na\nC", "keys.csv");
        Assert.Equal([0, 2], importer.InvalidRows.Select(r => r.Index));
        Assert.Contains("row 4", importer.AllRows[0].Issues[0].Message, StringComparison.Ordinal);

        importer.EditCell(2, "code", "B");                  // leaves the group of "a", joins the group of "b"
        Assert.Equal([1, 2], importer.InvalidRows.Select(r => r.Index));
        Assert.Empty(importer.AllRows[0].Issues);

        importer.EditCell(0, "code", "C");                  // a third key shared by rows 0 and 3
        Assert.Equal([0, 1, 2, 3], importer.InvalidRows.Select(r => r.Index));

        importer.EditCell(1, "code", "");                   // an empty key is no key
        Assert.Equal([0, 3], importer.InvalidRows.Select(r => r.Index));
        Assert.Contains("row 5", importer.AllRows[0].Issues[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SpreadsheetDuplicatesPolicy.KeepFirst, new[] { 2, 3 })]
    [InlineData(SpreadsheetDuplicatesPolicy.KeepLast, new[] { 0, 1 })]
    public void Keep_first_and_keep_last_follow_key_edits(SpreadsheetDuplicatesPolicy policy, int[] discardedAfterEdit)
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            RowKeyField = "code",
            Duplicates = policy,
            Columns = [new SpreadsheetColumnSchema { Key = "code" }],
        });
        importer.LoadText("Code\nA\nA\nB\nB", "keys.csv");
        Assert.Equal(2, importer.DiscardedRows.Count);

        importer.EditCell(2, "code", "A");                  // A is now held by rows 0, 1, 2 and B by row 3
        var expected = policy == SpreadsheetDuplicatesPolicy.KeepFirst ? new[] { 1, 2 } : new[] { 0, 1 };
        Assert.Equal(expected, importer.DiscardedRows.Select(r => r.Index));
        Assert.All(importer.DiscardedRows, r => Assert.Equal(SpreadsheetDiscardReason.Duplicate, r.DiscardReason));

        importer.EditCell(1, "code", "B");
        Assert.Equal(discardedAfterEdit, importer.DiscardedRows.Select(r => r.Index));
    }

    // The incremental update must give the rows and questions a full pass over the same state gives.
    [Theory]
    [InlineData(SpreadsheetDuplicatesPolicy.Error, SpreadsheetExistingAction.Update, 1)]
    [InlineData(SpreadsheetDuplicatesPolicy.KeepFirst, SpreadsheetExistingAction.Skip, 2)]
    [InlineData(SpreadsheetDuplicatesPolicy.KeepLast, SpreadsheetExistingAction.Error, 3)]
    public void Incremental_updates_match_a_full_pass_over_the_same_state(SpreadsheetDuplicatesPolicy duplicates, SpreadsheetExistingAction existing, int seed)
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            RowKeyField = "code",
            Duplicates = duplicates,
            ExistingAction = existing,
            ExistingRecords = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                ["B"] = new Dictionary<string, object?>(),
                ["d"] = new Dictionary<string, object?>(),
            },
            MaxRows = 35,
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "code", Required = true },
                new SpreadsheetColumnSchema { Key = "amount", Kind = SpreadsheetColumnKind.Number, Rules = [SpreadsheetRule.Max(100, level: SpreadsheetIssueLevel.Warning)] },
                new SpreadsheetColumnSchema { Key = "level", Kind = SpreadsheetColumnKind.Select, Options = [new SpreadsheetOption("g", "Gold"), new SpreadsheetOption("s", "Silver")] },
            ],
        });
        var random = new Random(seed);
        string[] codes = ["A", "B", "C", "D", "a", "b", "d", ""];
        string[] levels = ["Gold", "gold", "Silver", "Bronze", "zzz", "Platinum", ""];
        importer.LoadText("Code;Amount;Level\n" + string.Join('\n', Enumerable.Range(0, 40).Select(i =>
            $"{codes[random.Next(codes.Length)]};{random.Next(0, 150)};{levels[random.Next(levels.Length)]}")), "random.csv");

        var recompute = typeof(SpreadsheetImporter).GetMethod("Recompute", BindingFlags.NonPublic | BindingFlags.Instance)!;
        for (var step = 0; step < 300; step++)
        {
            var row = random.Next(40);
            switch (random.Next(8))
            {
                case 0: importer.EditCell(row, "code", codes[random.Next(codes.Length)]); break;
                case 1: importer.EditCell(row, "amount", random.Next(3) == 0 ? "x" : random.Next(0, 150).ToString(CultureInfo.InvariantCulture)); break;
                case 2: importer.EditCell(row, "level", levels[random.Next(levels.Length)]); break;
                case 3: importer.RevertCell(row, random.Next(3) switch { 0 => "code", 1 => "amount", _ => "level" }); break;
                case 4: importer.DiscardRows([row, random.Next(40)]); break;
                case 5: importer.RestoreRows([row]); break;
                case 6:
                    var open = importer.Questions.FirstOrDefault();
                    if (open is not null)
                    {
                        importer.AnswerValue(open.FieldKey, open.RawValue, random.Next(3) switch
                        {
                            0 => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "g", "Gold"),
                            1 => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows),
                            _ => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty),
                        });
                    }
                    break;
                default:
                    var answered = importer.Questions.FirstOrDefault(q => q.AnsweredBy == "user");
                    if (answered is not null) importer.ClearAnswer(answered.FieldKey, answered.RawValue);
                    break;
            }

            var incremental = Snapshot(importer);
            recompute.Invoke(importer, null);
            Assert.Equal(Snapshot(importer), incremental);
        }
    }

    private static string Snapshot(SpreadsheetImporter importer)
    {
        var sb = new StringBuilder();
        foreach (var row in importer.AllRows)
        {
            sb.Append(row.Index).Append('|').Append(row.RowNumber).Append('|').Append(row.Status).Append('|').Append(row.DiscardReason).Append('|').Append(row.Mode).Append('|').Append(row.Importable).Append('|').Append(row.Edited).Append('|');
            sb.AppendJoin(',', row.Data.Select(kv => $"{kv.Key}={kv.Value}")).Append('|');
            sb.AppendJoin(',', row.Cells.Select(kv => $"{kv.Key}:{kv.Value.Text}/{kv.Value.DisplayText}/{kv.Value.Edited}/{kv.Value.Issue?.Code}")).Append('|');
            sb.AppendJoin(';', row.Issues.Select(i => $"{i.Field}/{i.Code}/{i.Level}/{i.Message}")).AppendLine();
        }
        foreach (var q in importer.Questions)
        {
            sb.Append(q.Id).Append('|').Append(q.RawValue).Append('|').Append(string.Join(',', q.RowIndexes)).Append('|').Append(q.Answer?.Action).Append('|').Append(q.AnsweredBy).AppendLine();
        }
        sb.Append(importer.OpenQuestions.Count).Append('|').Append(importer.MissingFields.Count);
        return sb.ToString();
    }

    [Fact]
    public void Answering_a_value_reads_only_the_rows_that_hold_it()
    {
        var parsed = 0;
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "name", CustomParse = s => { parsed++; return s; } },
                new SpreadsheetColumnSchema { Key = "level", Kind = SpreadsheetColumnKind.Select, Options = [new SpreadsheetOption("a", "A")] },
            ],
        });
        importer.LoadText("Name;Level\n" + string.Join('\n', Enumerable.Range(0, 500).Select(i => $"n{i};{(i % 100 == 0 ? "weird" : "A")}")), "levels.csv");
        var loaded = parsed;

        importer.AnswerValue("level", "weird", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "a", "A"));

        Assert.Equal(loaded + 5, parsed);                    // the five rows that hold "weird"
        Assert.Empty(importer.OpenQuestions);
        Assert.Equal(500, importer.ImportableRows.Count);
    }

    [Fact]
    public void An_answer_for_a_value_without_a_question_still_applies()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns = [new SpreadsheetColumnSchema { Key = "level", Kind = SpreadsheetColumnKind.Select, UnknownPolicy = SpreadsheetUnknownValuePolicy.Error, Options = [new SpreadsheetOption("a", "A")] }],
        });
        importer.LoadText("Level\nweird\nA", "levels.csv");
        Assert.False(importer.AllRows[0].Importable);

        importer.AnswerValue("level", "weird", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "a", "A"));

        Assert.True(importer.AllRows[0].Importable);
        Assert.Equal("a", importer.AllRows[0].Data["level"]);
    }

    [Fact]
    public void The_review_is_paged_and_the_page_follows_the_filters()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(120), "bulk.csv");

        Assert.Equal(SpreadsheetImporter.DefaultReviewPageSize, importer.ReviewPageSize);
        Assert.Equal(3, importer.ReviewPageCount);
        Assert.Equal(50, importer.ReviewPageRows.Count);
        Assert.Equal(0, importer.ReviewPageRows[0].Index);

        importer.SetReviewPage(2);
        Assert.Equal(20, importer.ReviewPageRows.Count);
        Assert.Equal(100, importer.ReviewPageRows[0].Index);

        importer.SetReviewPage(99);                          // clamped to the last page
        Assert.Equal(2, importer.ReviewPage);
        importer.SetReviewPage(-5);
        Assert.Equal(0, importer.ReviewPage);

        importer.SetReviewPage(2);
        importer.SetReviewSearch("SKU-0000");                // a filter goes back to the first page (99 rows match)
        Assert.Equal(0, importer.ReviewPage);
        Assert.Equal(2, importer.ReviewPageCount);

        importer.SetReviewPageSize(25);
        Assert.Equal(25, importer.ReviewPageRows.Count);
        importer.SetReviewPage(3);
        importer.SetReviewPageSize(100);
        Assert.Equal(0, importer.ReviewPage);
        Assert.Equal(1, importer.ReviewPageCount);
        importer.SetReviewPageSize(0);                       // at least one row per page
        Assert.Equal(1, importer.ReviewPageSize);
    }

    [Fact]
    public void The_page_stays_inside_the_rows_when_they_shrink_and_an_empty_review_has_one_page()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(60), "bulk.csv");
        importer.SetReviewPage(1);
        importer.DiscardRows(Enumerable.Range(10, 50));
        importer.SetReviewTab(SpreadsheetReviewTab.Importable);
        Assert.Equal(1, importer.ReviewPageCount);
        Assert.Equal(0, importer.ReviewPage);

        importer.SetReviewSearch("no such text");
        Assert.Empty(importer.ReviewPageRows);
        Assert.Equal(1, importer.ReviewPageCount);
    }

    [Fact]
    public void A_default_row_limit_leaves_out_the_rows_past_it_and_the_schema_limit_wins()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(30), "bulk.csv");
        Assert.Null(importer.EffectiveMaxRows);
        Assert.Equal(0, importer.LimitedRowCount);

        importer.DefaultMaxRows = 10;
        Assert.Equal(10, importer.EffectiveMaxRows);
        Assert.Equal(20, importer.LimitedRowCount);
        Assert.Equal(10, importer.ImportableRows.Count);
        Assert.All(importer.DiscardedRows, r => Assert.Equal(SpreadsheetDiscardReason.Limit, r.DiscardReason));

        importer.DefaultMaxRows = null;
        Assert.Equal(30, importer.ImportableRows.Count);

        var schemaLimited = new SpreadsheetImporter(BulkSchema(maxRows: 5)) { DefaultMaxRows = 25 };
        schemaLimited.LoadText(BulkCsv(30), "bulk.csv");
        Assert.Equal(5, schemaLimited.EffectiveMaxRows);
        Assert.Equal(25, schemaLimited.LimitedRowCount);
    }

    // The component

    private IRenderedComponent<KlySpreadsheetImport> RenderReview(SpreadsheetImporter importer, Action<ComponentParameterCollectionBuilder<KlySpreadsheetImport>>? parameters = null)
    {
        importer.NextStep();
        return Render<KlySpreadsheetImport>(p =>
        {
            p.Add(x => x.Importer, importer);
            parameters?.Invoke(p);
        });
    }

    [Fact]
    public void The_review_table_renders_one_page_and_a_keyboard_pager_moves_between_pages()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(120), "bulk.csv");
        var cut = RenderReview(importer);

        Assert.Equal(50, cut.FindAll("[data-slot=spreadsheet-row]").Count);
        Assert.Equal("Page 1 of 3", cut.Find("[data-slot=spreadsheet-pager-status]").TextContent.Trim());
        Assert.Equal("Rows 1 to 50 of 120", cut.Find("[data-slot=spreadsheet-pager-range]").TextContent.Trim());
        Assert.True(cut.Find("[data-slot=spreadsheet-pager-prev]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-slot=spreadsheet-pager-first]").HasAttribute("disabled"));
        Assert.Equal("BUTTON", cut.Find("[data-slot=spreadsheet-pager-next]").TagName);   // native buttons: Tab and Enter work

        cut.Find("[data-slot=spreadsheet-pager-next]").Click();
        Assert.Equal("Page 2 of 3", cut.Find("[data-slot=spreadsheet-pager-status]").TextContent.Trim());
        Assert.Equal("50", cut.FindAll("[data-slot=spreadsheet-row]")[0].GetAttribute("data-index"));

        cut.Find("[data-slot=spreadsheet-pager-last]").Click();
        Assert.Equal(20, cut.FindAll("[data-slot=spreadsheet-row]").Count);
        Assert.True(cut.Find("[data-slot=spreadsheet-pager-next]").HasAttribute("disabled"));

        cut.Find("[data-slot=spreadsheet-pager-size]").Change("100");
        Assert.Equal(2, importer.ReviewPageCount);
        Assert.Equal(100, cut.FindAll("[data-slot=spreadsheet-row]").Count);
        cut.Find("[data-slot=spreadsheet-pager-first]");
    }

    [Fact]
    public void The_page_size_parameter_sets_the_rows_per_page_and_a_users_choice_survives_a_rerender()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(60), "bulk.csv");
        var cut = RenderReview(importer, p => p.Add(x => x.PageSize, 20));
        Assert.Equal(20, cut.FindAll("[data-slot=spreadsheet-row]").Count);

        cut.Find("[data-slot=spreadsheet-pager-size]").Change("25");
        cut.Render();
        Assert.Equal(25, cut.FindAll("[data-slot=spreadsheet-row]").Count);
    }

    [Fact]
    public void Select_all_selects_the_rows_of_the_page()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(120), "bulk.csv");
        var cut = RenderReview(importer);

        cut.Find("[data-slot=spreadsheet-select-all]").Change(true);

        Assert.Equal(50, importer.SelectedRowIndexes.Count);
    }

    [Fact]
    public void The_component_applies_a_default_row_limit_and_says_how_many_rows_it_left_out()
    {
        var importer = new SpreadsheetImporter(BulkSchema());
        importer.LoadText(BulkCsv(30), "bulk.csv");
        var cut = RenderReview(importer, p => p.Add(x => x.MaxRows, 12));

        Assert.Equal(12, importer.ImportableRows.Count);
        var notice = cut.Find("[data-slot=spreadsheet-row-limit]");
        Assert.Equal("status", notice.GetAttribute("role"));
        Assert.Contains("12", notice.TextContent, StringComparison.Ordinal);
        Assert.Contains("18", notice.TextContent, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.MaxRows, (int?)null));
        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-row-limit]"));
        Assert.Equal(30, importer.ImportableRows.Count);
    }

    [Fact]
    public void The_default_row_limit_of_the_component_is_ten_thousand_and_the_schema_limit_wins()
    {
        Assert.Equal(10_000, KlySpreadsheetImport.DefaultMaxRows);
        var importer = new SpreadsheetImporter(BulkSchema(maxRows: 7));
        importer.LoadText(BulkCsv(30), "bulk.csv");
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.Equal(7, importer.EffectiveMaxRows);
        Assert.Equal(7, importer.ImportableRows.Count);
        Assert.NotNull(cut.Find("[data-slot=spreadsheet-row-limit]"));
    }
}
