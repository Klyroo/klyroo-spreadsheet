using Klyroo.Spreadsheet;

var schema = new SpreadsheetSchema
{
    Columns = [new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] }]
};
var importer = new SpreadsheetImporter(schema);
importer.LoadText("Email\nvalid@klyroo.dev\nbad-email", "users.csv");
Console.WriteLine($"Importable: {importer.ImportableRows.Count}, Invalid: {importer.InvalidRows.Count}");
