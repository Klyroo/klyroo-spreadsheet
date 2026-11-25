# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Primary**: .NET 10 / Blazor and `.NET MAUI Blazor Hybrid` developers building self-service CSV/TSV bulk import workflows for SaaS onboarding, backoffice data migration, and catalog ingestion.
- **Secondary**: Backend .NET engineers running headless CSV parsing, header normalization, and validation in Minimal APIs, background workers, or CLI tools.

## Product Purpose

`klyroo-spreadsheet` provides a 100% NativeAOT spreadsheet import engine (`Klyroo.Spreadsheet.Core`) and a 5-step guided Blazor import wizard (`Klyroo.Spreadsheet`, `<KlySpreadsheetImport>`) that turns messy real-world CSV/TSV uploads and clipboard pastes into validated, reconciled domain batches.

Success means non-technical operators can drop a European semicolon CSV with banner rows, accented headers, decimal commas, unknown enum values, or partial server rejections, fix issues inline in the browser, and retry only rejected rows without re-uploading.

## Positioning

Whereas most Blazor ecosystems ship no structured bulk importer (forcing teams to stitch together CSV parsers, manual column mappers, and ad-hoc error tables), `Klyroo.Spreadsheet` delivers 20-row heuristic header detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown-value resolution questions, intra-file/existing-record upsert reconciliation, inline cell editing, and batched rejected-row retry in a headless NativeAOT core under MIT.

## Operating Context

- **Packages**: `Klyroo.Spreadsheet.Core` (`SpreadsheetSchema`, `SpreadsheetColumnSchema`, `SpreadsheetImporter`, NFD/heuristic mapping, validation rules, batch retry) and `Klyroo.Spreadsheet` (`KlySpreadsheetImport`, `klyroo-spreadsheet.css`).
- **Integration**: Built on `Klyroo.UI` primitives and natively registrable in `Klyroo.Agent` (`AgentBridgeService.RegisterSpreadsheet`).
- **Showcase**: Powers the `Bulk Import` tab in `Klyroo.Stack.Site` `/showcase` preloaded with a European semicolon CSV (`Nom;Région;Offre;Revenu`) that appends validated accounts into the live backoffice.

## Capabilities and Constraints

- `Klyroo.Spreadsheet.Core` is 100% headless with `<IsAotCompatible>true</IsAotCompatible>` and `<IsTrimmable>true</IsTrimmable>`.
- Handles RFC 4180 CSV/TSV, European semicolons and decimal commas, 20-row header layout heuristics, NFD accent folding, ≥80% pass-rate suggestions, unknown option reconciliation, duplicate/upsert policies, and batched submit with partial retry.
- Ships precompiled zero-friction CSS (`klyroo-spreadsheet.css`) alongside native Tailwind CSS v4 support.
- 100% MIT open-source license; strict `Klyroo.Spreadsheet*` / `KlySpreadsheet*` / `--klyroo-*` naming.

## Brand Commitments

- **Name**: `Klyroo.Spreadsheet` (`klyroo-spreadsheet`), part of the **Klyroo** ecosystem.
- **Voice**: Direct, forgiving on dirty user inputs, and explicit about row/cell errors.

## Evidence on Hand

- Passing unit and bUnit test suite (`tests/Klyroo.Spreadsheet.Tests`) plus NativeAOT publish verification in CI.
- Measured `.NET 10.0` Release benchmark (in `klyroo-stack/docs/benchmarks.md`): 10k CSV rows (NFD normalization + deduplication) completes in `23.0 ms` median.
- Live 5-step wizard demonstration in `Klyroo.Stack.Site` `/showcase` (`Bulk Import` tab) and product docs at `/spreadsheet`.
- Verified precompiled standalone CSS (`klyroo-spreadsheet.css`).
- **Explicit absences (do not fabricate)**: No NuGet download stats, customer logos, or third-party testimonials exist yet.

## Product Principles

1. **Expect messy real-world files**: Export banners, accented headers, regional delimiters, and unexpected option values are normal inputs, not fatal exceptions.
2. **Never silently guess wrong**: Auto-suggest mappings only above an explicit confidence threshold (≥80% sample pass rate) and surface unknown values as explicit operator questions.
3. **Fix inline, retry only failures**: Partial batch rejections preserve operator progress and isolate only the rows that need correction.
4. **Headless engine parity**: Every parsing, mapping, and validation rule runs identically in `Klyroo.Spreadsheet.Core` without a browser.

## Accessibility & Inclusion

- Durable standard: **WCAG 2.1 AA** across the 5-step import wizard: keyboard-accessible file upload/paste, mapping selects, inline cell editing, step status announcements, clear cell-level error messages, and international locale/delimiter support (Unicode NFD, comma/dot decimals).
