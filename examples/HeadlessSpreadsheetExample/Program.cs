using Klyroo.Spreadsheet;

var schema = SpreadsheetSchema.For<UserRow>(s =>
    s.Column(r => r.Email, c => c.Required().Rule(SpreadsheetRule.Email())));
var importer = new SpreadsheetImporter<UserRow>(schema);
importer.LoadText("Email\nvalid@klyroo.dev\nbad-email", "users.csv");
Console.WriteLine($"Importable: {importer.ImportableRows.Count}, Invalid: {importer.InvalidRows.Count}");

public sealed class UserRow
{
    public string? Email { get; set; }
}
