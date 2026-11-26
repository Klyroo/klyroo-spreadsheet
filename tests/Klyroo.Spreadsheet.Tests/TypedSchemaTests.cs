using System.Reflection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public enum Plan
{
    Free,
    ProMonthly,
    Enterprise,
}

public sealed class Order
{
    public string? Reference { get; set; }
    public int Quantity { get; set; }
    public int? Seats { get; set; }
    public decimal Total { get; set; }
    public bool Paid { get; set; }
    public bool? Gift { get; set; }
    public DateOnly Day { get; set; }
    public DateTime? Shipped { get; set; }
    public DateTimeOffset? Delivered { get; set; }
    public Plan Plan { get; set; }
    public Plan? Upgrade { get; set; }
    public string? Country { get; set; }
    public Guid? Token { get; set; }
}

public sealed class PositionalOrder(string reference)
{
    public string Reference { get; } = reference;
    public int Quantity { get; set; }
}

public sealed class TypedSchemaTests
{
    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    private static SpreadsheetColumnSchema Column<TRow>(SpreadsheetSchema<TRow> schema, string key) where TRow : class =>
        schema.Columns.Single(c => c.Key == key);

    [Fact]
    public void The_kind_of_a_column_follows_the_type_of_the_property()
    {
        var schema = SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Reference);
            sb.Column(r => r.Quantity);
            sb.Column(r => r.Seats);
            sb.Column(r => r.Total);
            sb.Column(r => r.Paid);
            sb.Column(r => r.Gift);
            sb.Column(r => r.Day);
            sb.Column(r => r.Shipped);
            sb.Column(r => r.Delivered);
            sb.Column(r => r.Plan);
            sb.Column(r => r.Upgrade);
        });

        Assert.Equal(SpreadsheetColumnKind.Text, Column(schema, "Reference").Kind);
        Assert.Equal(SpreadsheetColumnKind.Number, Column(schema, "Quantity").Kind);
        Assert.Equal(SpreadsheetColumnKind.Number, Column(schema, "Seats").Kind);
        Assert.Equal(SpreadsheetColumnKind.Number, Column(schema, "Total").Kind);
        Assert.Equal(SpreadsheetColumnKind.Boolean, Column(schema, "Paid").Kind);
        Assert.Equal(SpreadsheetColumnKind.Boolean, Column(schema, "Gift").Kind);
        Assert.Equal(SpreadsheetColumnKind.Date, Column(schema, "Day").Kind);
        Assert.Equal(SpreadsheetColumnKind.Date, Column(schema, "Shipped").Kind);
        Assert.Equal(SpreadsheetColumnKind.Date, Column(schema, "Delivered").Kind);
        Assert.Equal(SpreadsheetColumnKind.Select, Column(schema, "Plan").Kind);
        Assert.Equal(SpreadsheetColumnKind.Select, Column(schema, "Upgrade").Kind);
        Assert.Equal(typeof(int?), Column(schema, "Seats").ValueType);
    }

    [Fact]
    public void A_property_that_cannot_hold_an_empty_cell_is_required_and_a_nullable_one_is_not()
    {
        var schema = SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Reference);
            sb.Column(r => r.Quantity);
            sb.Column(r => r.Seats);
            sb.Column(r => r.Paid);
            sb.Column(r => r.Total, c => c.Default(0m));
        });

        Assert.False(Column(schema, "Reference").Required);
        Assert.True(Column(schema, "Quantity").Required);
        Assert.False(Column(schema, "Seats").Required);
        Assert.True(Column(schema, "Paid").Required);
        Assert.False(Column(schema, "Total").Required);

        var importer = new SpreadsheetImporter<Order>(schema);
        importer.LoadText("Reference,Quantity\nA,\nB,3", "o.csv");
        Assert.Equal("required", Assert.Single(importer.AllRows[0].Issues, i => i.Field == "Quantity").Code);
        Assert.Equal(["Paid"], importer.MissingFields.Select(f => f.Column.Key));
    }

    [Fact]
    public void Values_are_read_into_the_property_types_of_the_row()
    {
        var schema = SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Reference);
            sb.Column(r => r.Quantity);
            sb.Column(r => r.Seats);
            sb.Column(r => r.Total);
            sb.Column(r => r.Paid);
            sb.Column(r => r.Gift);
            sb.Column(r => r.Day);
            sb.Column(r => r.Shipped);
            sb.Column(r => r.Delivered);
            sb.Column(r => r.Plan);
            sb.Column(r => r.Upgrade);
        });
        var importer = new SpreadsheetImporter<Order>(schema);
        importer.LoadText(
            "Reference;Quantity;Seats;Total;Paid;Gift;Day;Shipped;Delivered;Plan;Upgrade\n" +
            "R1;3;;12.50;yes;;2026-03-04;2026-03-04 10:30:15;2026-03-05;Pro monthly;enterprise", "o.csv");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));

        Assert.Empty(row.Issues);
        Assert.Equal("R1", row.Value.Reference);
        Assert.Equal(3, row.Value.Quantity);
        Assert.Null(row.Value.Seats);
        Assert.Equal(12.5m, row.Value.Total);
        Assert.True(row.Value.Paid);
        Assert.Null(row.Value.Gift);
        Assert.Equal(new DateOnly(2026, 3, 4), row.Value.Day);
        Assert.Equal(new DateTime(2026, 3, 4, 10, 30, 15), row.Value.Shipped);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), row.Value.Delivered);
        Assert.Equal(Plan.ProMonthly, row.Value.Plan);
        Assert.Equal(Plan.Enterprise, row.Value.Upgrade);
        Assert.Same(row.Cells, importer.AllRows[0].Cells);
    }

    [Theory]
    [InlineData("1.5", "is not a whole number")]
    [InlineData("3000000000", "is out of range")]
    public void A_whole_number_property_refuses_what_it_cannot_hold(string text, string reason)
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Quantity)));
        importer.LoadText($"Quantity\n{text}", "o.csv");

        var issue = Assert.Single(importer.AllRows[0].Issues);

        Assert.Equal("number", issue.Code);
        Assert.Contains(reason, issue.Message, StringComparison.Ordinal);
        Assert.Empty(importer.ImportableRows);
    }

    [Fact]
    public void An_enum_column_offers_its_members_and_asks_about_the_other_values()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Plan)));
        importer.LoadText("Plan\nfree\nPro monthly\nPlatinum", "o.csv");

        var column = Assert.Single(importer.Schema.Columns);
        Assert.Equal(["Free", "ProMonthly", "Enterprise"], column.Options!.Select(o => o.Value));
        Assert.Equal(["Free", "Pro monthly", "Enterprise"], column.Options!.Select(o => o.Label));
        var question = Assert.Single(importer.OpenQuestions);
        Assert.Equal("Platinum", question.RawValue);

        importer.AnswerValue("Plan", "Platinum", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "Enterprise", "Enterprise"));

        Assert.Equal([Plan.Free, Plan.ProMonthly, Plan.Enterprise], importer.AllRows.Select(r => importer.ToTyped(r).Value.Plan));
    }

    [Fact]
    public void Options_replace_the_members_of_an_enum_and_give_a_string_column_its_choices()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Plan, c => c.Options([new SpreadsheetOption<Plan>(Plan.Free, "Gratuit", ["0"]), new SpreadsheetOption<Plan>(Plan.Enterprise, "Entreprise")]));
            sb.Column(r => r.Country, c => c.Options([new SpreadsheetOption<string?>("FR", "France", ["République française"])]));
        }));
        importer.LoadText("Plan;Country\ngratuit;france\n0;FR\nEntreprise;République française", "o.csv");

        Assert.Empty(importer.OpenQuestions);
        Assert.Equal([Plan.Free, Plan.Free, Plan.Enterprise], importer.AllRows.Select(r => importer.ToTyped(r).Value.Plan));
        Assert.All(importer.AllRows, r => Assert.Equal("FR", importer.ToTyped(r).Value.Country));
        Assert.Equal(SpreadsheetColumnKind.Select, Column(importer.Schema, "Country").Kind);
    }

    [Fact]
    public void A_typed_default_fills_empty_cells()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Quantity, c => c.Default(1));
            sb.Column(r => r.Plan, c => c.Default(Plan.Free));
            sb.Column(r => r.Seats, c => c.Default(null));
            sb.Column(r => r.Reference);
        }));
        importer.LoadText("Quantity;Plan;Seats;Reference\n;;;r1\n5;Enterprise;2;r2", "o.csv");

        Assert.Empty(importer.MissingFields);
        var first = importer.ToTyped(importer.AllRows[0]);
        Assert.Equal(1, first.Value.Quantity);
        Assert.Equal(Plan.Free, first.Value.Plan);
        Assert.Null(first.Value.Seats);
        Assert.True(first.Cells["Quantity"].Defaulted);
        Assert.Equal(2, importer.ToTyped(importer.AllRows[1]).Value.Seats);
    }

    [Fact]
    public void A_parser_returns_a_value_or_a_message_and_an_exception_is_a_message_too()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Token, c => c.Parser(text => Guid.TryParse(text, out var g) ? SpreadsheetParseResult<Guid?>.Ok(g) : SpreadsheetParseResult<Guid?>.Fail("Not a token")));
            sb.Column(r => r.Reference, c => c.Parser(text => text == "boom" ? throw new FormatException("Exploded") : SpreadsheetParseResult<string?>.Ok(text.ToUpperInvariant())));
        }));
        var token = Guid.NewGuid();
        importer.LoadText($"Token;Reference\n{token};abc\nnope;boom", "o.csv");

        Assert.Equal(token, importer.ToTyped(importer.AllRows[0]).Value.Token);
        Assert.Equal("ABC", importer.ToTyped(importer.AllRows[0]).Value.Reference);
        Assert.Equal(["Not a token", "Exploded"], importer.AllRows[1].Issues.Select(i => i.Message));
        Assert.All(importer.AllRows[1].Issues, i => Assert.Null(i.Args));
    }

    [Fact]
    public void Rules_are_typed_like_the_column_and_a_nullable_column_takes_the_rule_of_its_underlying_type()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Quantity, c => c.Rule(SpreadsheetRule.Between(1, 10)));
            sb.Column(r => r.Seats, c => c.Rule(SpreadsheetRule.Min(2)));
            sb.Column(r => r.Total, c => c.Rule(SpreadsheetRule.Max(100m)).Rule("even", "Must be whole", v => v == Math.Floor(v)));
        }));
        importer.LoadText("Quantity;Seats;Total\n5;;50\n11;1;100.5", "o.csv");

        Assert.Empty(importer.AllRows[0].Issues);
        Assert.Equal(["Quantity:between", "Seats:min", "Total:max", "Total:even"], importer.AllRows[1].Issues.Select(i => $"{i.Field}:{i.Code}"));
        Assert.Equal(["1", "10"], importer.AllRows[1].Issues[0].Args);
    }

    [Fact]
    public void ValidateRow_receives_the_row_with_its_values()
    {
        var seen = new List<(int Quantity, Plan Plan)>();
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Quantity);
            sb.Column(r => r.Plan);
            sb.ValidateRow = row =>
            {
                seen.Add((row.Quantity, row.Plan));
                return row.Plan == Plan.Free && row.Quantity > 1
                    ? [new SpreadsheetRowIssue("Quantity", "free.limit", SpreadsheetIssueLevel.Error, "Free allows one")]
                    : [];
            };
        }));
        importer.LoadText("Quantity;Plan\n1;Free\n4;Free\n4;Enterprise", "o.csv");

        Assert.Equal([(1, Plan.Free), (4, Plan.Free), (4, Plan.Enterprise)], seen);
        Assert.Equal([true, false, true], importer.AllRows.Select(r => r.Importable));
    }

    [Fact]
    public async Task OnSubmit_receives_typed_rows_and_keeps_their_position_in_the_file()
    {
        var received = new List<(int Index, int RowNumber, string? Reference, int Quantity)>();
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.Column(r => r.Reference, c => c.Required());
            sb.Column(r => r.Quantity);
            sb.OnSubmit = rows =>
            {
                received.AddRange(rows.Select(r => (r.Index, r.RowNumber, r.Value.Reference, r.Value.Quantity)));
                return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
            };
        }));
        importer.LoadText("Reference;Quantity\nA;1\n;2\nC;3", "o.csv");

        await importer.SubmitAsync(Token);

        Assert.Equal([(0, 2, "A", 1), (2, 4, "C", 3)], received);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal(
            ["A", "C"],
            importer.ImportableTypedRows.Select(r => r.Value.Reference));
    }

    [Fact]
    public async Task Existing_records_and_lookups_are_keyed_by_the_text_of_the_row_key()
    {
        var importer = new SpreadsheetImporter<Order>(SpreadsheetSchema.For<Order>(sb =>
        {
            sb.RowKey(sb.Column(r => r.Quantity));
            sb.ExistingRecords = new Dictionary<string, Order> { ["7"] = new Order { Quantity = 7 } };
            sb.LookupExistingRecordsAsync = (keys, ct) => Task.FromResult<IReadOnlyDictionary<string, Order>>(
                keys.Where(k => k == "9").ToDictionary(k => k, _ => new Order { Quantity = 9 }));
        }));
        importer.LoadText("Quantity\n7\n8\n9", "o.csv");

        Assert.Equal([SpreadsheetRowMode.Update, SpreadsheetRowMode.Create, SpreadsheetRowMode.Create], importer.AllRows.Select(r => r.Mode));

        await importer.ValidateAsync(Token);

        Assert.Equal([SpreadsheetRowMode.Update, SpreadsheetRowMode.Create, SpreadsheetRowMode.Update], importer.AllRows.Select(r => r.Mode));
    }

    [Fact]
    public void A_schema_for_a_type_without_a_parameterless_constructor_takes_a_factory()
    {
        var schema = SpreadsheetSchema.For(() => new PositionalOrder("fixed"), sb => sb.Column(r => r.Quantity));
        var importer = new SpreadsheetImporter<PositionalOrder>(schema);
        importer.LoadText("Quantity\n4", "o.csv");

        var row = importer.ToTyped(importer.AllRows[0]);

        Assert.Equal("fixed", row.Value.Reference);
        Assert.Equal(4, row.Value.Quantity);
    }

    [Fact]
    public void The_key_of_a_column_is_the_property_name_unless_it_is_overridden_and_a_computed_column_reads_an_earlier_one()
    {
        var schema = SpreadsheetSchema.For<Order>(sb =>
        {
            var reference = sb.Column(r => r.Reference, c => c.Key("ref").Label("Reference"));
            sb.Column(r => r.Country, c => c.From(reference));
        });

        Assert.Equal(["ref", "Country"], schema.Columns.Select(c => c.Key));
        Assert.Equal("ref", schema.Columns[1].From);
        var importer = new SpreadsheetImporter<Order>(schema);
        importer.LoadText("Reference\nX1", "o.csv");
        Assert.Equal("X1", importer.ToTyped(importer.AllRows[0]).Value.Country);
    }

    [Fact]
    public void The_schema_refuses_what_cannot_be_read_back_into_the_row()
    {
        SpreadsheetColumnSchema foreign = null!;
        SpreadsheetSchema.For<Order>(sb => foreign = sb.Column(r => r.Total));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Reference + "x")));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Reference!.Length)));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For(() => new PositionalOrder("x"), sb => sb.Column(r => r.Reference)));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => { sb.Column(r => r.Reference); sb.Column(r => r.Reference); }));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb =>
        {
            var later = sb.Column(r => r.Country);
            sb.Column(r => r.Reference, c => c.From(later));
            sb.Column(r => r.Quantity, c => c.From(foreign));
        }));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Token)));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => sb.Column(r => r.Day, c => c.Options([new SpreadsheetOption<DateOnly>(default, "None")]))));
        Assert.Throws<ArgumentException>(() => SpreadsheetSchema.For<Order>(sb => sb.RowKey(foreign)));
    }

    [Fact]
    public void The_public_row_carries_no_untyped_values()
    {
        Assert.Null(typeof(SpreadsheetRow).GetProperty("Data", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(SpreadsheetSchema).GetProperty("ExistingRecords", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(SpreadsheetRow<Order>).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance));
    }
}
