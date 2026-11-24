# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Primary**: .NET 10 / Blazor and `.NET MAUI Blazor Hybrid` developers building self-service CSV/TSV bulk import workflows for SaaS onboarding, backoffice data migration, and catalog ingestion.
- **Secondary**: Backend .NET engineers running headless CSV parsing, header normalization, and validation in Minimal APIs, background workers, or CLI tools.

## Product Purpose

`lux-spreadsheet` provides a 100% NativeAOT spreadsheet import engine (`Lux.Spreadsheet.Core`) and a 5-step guided Blazor import wizard (`Lux.Spreadsheet`, `<LxSpreadsheetImport>`) that turns messy real-world CSV/TSV uploads and clipboard pastes into validated, reconciled domain batches.

Success means non-technical operators can drop a European semicolon CSV with banner rows, accented headers, decimal commas, unknown enum values, or partial server rejections, fix issues inline in the browser, and retry only rejected rows without re-uploading.

## Positioning

Whereas most Blazor ecosystems ship no structured bulk importer (forcing teams to stitch together CSV parsers, manual column mappers, and ad-hoc error tables), `Lux.Spreadsheet` delivers 20-row heuristic header detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown-value resolution questions, intra-file/existing-record upsert reconciliation, inline cell editing, and batched rejected-row retry in a headless NativeAOT core under MIT.

## Operating Context

- **Packages**: `Lux.Spreadsheet.Core` (`SpreadsheetSchema`, `SpreadsheetColumnSchema`, `SpreadsheetImporter`, NFD/heuristic mapping, validation rules, batch retry) and `Lux.Spreadsheet` (`LxSpreadsheetImport`, `lux-spreadsheet.css`).
- **Integration**: Built on `Lux.UI` primitives and natively registrable in `Lux.Agent` (`AgentBridgeService.RegisterSpreadsheet`).
- **Showcase**: Powers the `Bulk Import` tab in `Lux.Stack.Site` `/showcase` preloaded with a European semicolon CSV (`Nom;Région;Offre;Revenu`) that appends validated accounts into the live backoffice.

## Capabilities and Constraints

- `Lux.Spreadsheet.Core` is 100% headless with `<IsAotCompatible>true</IsAotCompatible>` and `<IsTrimmable>true</IsTrimmable>`.
- Handles RFC 4180 CSV/TSV, European semicolons and decimal commas, 20-row header layout heuristics, NFD accent folding, ≥80% pass-rate suggestions, unknown option reconciliation, duplicate/upsert policies, and batched submit with partial retry.
- Ships precompiled zero-friction CSS (`lux-spreadsheet.css`) alongside native Tailwind CSS v4 support.
- 100% MIT open-source license; strict `Lux.Spreadsheet*` / `LxSpreadsheet*` / `--lux-*` naming.

## Brand Commitments

- **Name**: `Lux.Spreadsheet` (`lux-spreadsheet`), part of the **LuxStack** ecosystem.
- **Voice**: Direct, forgiving on dirty user inputs, and explicit about row/cell errors.

## Evidence on Hand

- Passing unit and bUnit test suite (`tests/Lux.Spreadsheet.Tests`) plus NativeAOT publish verification in CI.
- Measured `.NET 10.0` Release benchmark (in `lux.stack/docs/benchmarks.md`): 10k CSV rows (NFD normalization + deduplication) completes in `23.0 ms` median.
- Live 5-step wizard demonstration in `Lux.Stack.Site` `/showcase` (`Bulk Import` tab) and product docs at `/spreadsheet`.
- Verified precompiled standalone CSS (`lux-spreadsheet.css`).
- **Explicit absences (do not fabricate)**: No NuGet download stats, customer logos, or third-party testimonials exist yet.

## Product Principles

1. **Expect messy real-world files**: Export banners, accented headers, regional delimiters, and unexpected option values are normal inputs, not fatal exceptions.
2. **Never silently guess wrong**: Auto-suggest mappings only above an explicit confidence threshold (≥80% sample pass rate) and surface unknown values as explicit operator questions.
3. **Fix inline, retry only failures**: Partial batch rejections preserve operator progress and isolate only the rows that need correction.
4. **Headless engine parity**: Every parsing, mapping, and validation rule runs identically in `Lux.Spreadsheet.Core` without a browser.

## Accessibility & Inclusion

- Durable standard: **WCAG 2.1 AA** across the 5-step import wizard: keyboard-accessible file upload/paste, mapping selects, inline cell editing, step status announcements, clear cell-level error messages, and international locale/delimiter support (Unicode NFD, comma/dot decimals).
