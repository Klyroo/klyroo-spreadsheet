// SpreadsheetTexts.Current is process-wide: a test that translates the controls must not run beside the others, so every test shares one collection.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
