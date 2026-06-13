# Query Pattern Reporting Design

## Overview

TraceMap now has two practical flavors of `QueryPatternDetected` evidence:

| Flavor | Identifier | Typical fields | Meaning |
| --- | --- | --- | --- |
| SQL-shape | `sqlSourceKind` is present | `operationName`, `tableName`, `tableNames`, `columnNames`, `fieldNames`, `queryShapeHash` | Lightweight static SQL shape evidence |
| Query-builder | `sqlSourceKind` absent | `filterFields`, `sortFields`, `selectFields`, `includeFields`, `mutationFields`, `patternHash` | Framework/query-builder call shape evidence |

Reports should display these as different evidence shapes while keeping the underlying fact schema unchanged.

## Current Behavior

.NET and TypeScript reports currently format all query patterns with query-builder fields. SQL-shape facts can therefore render as low-signal lines such as `fields none`, even though the SQLite/NDJSON fact contains useful table and column metadata.

Python scan reports currently summarize fact counts, tiers, rules, and gaps, but do not list query-pattern evidence at all.

## Proposed Output

For SQL-shape facts, render a compact line like:

```text
- `SELECT` on `orders` columns `id;status;total` source `orm-text` shape `9858bc44eae2ff12104e17967718e5dc` (Tier2Structural) at `app/repository.py:8`
```

When optional fields are missing, keep the row honest:

```text
- `unknown` on `unknown` columns `none` source `sql-file` shape `abc123...` (Tier2Structural) at `schema.sql:1`
```

For query-builder facts, preserve the existing shape:

```text
- `Where` fields `Status` (Tier3SyntaxOrTextual) at `src/Repo.cs:12`
```

The exact Markdown wording can vary slightly by runtime, but the field vocabulary and evidence boundaries should be consistent.

## Implementation Plan

### .NET

Update `src/dotnet/TraceMap.Reporting/MarkdownReportWriter.cs`.

- Add a helper that detects SQL-shape facts by `properties["sqlSourceKind"]`.
- Render SQL-shape query facts with operation, table, columns, source kind, shape hash, tier, and span.
- Keep existing `DisplayFields` behavior for query-builder facts.
- Add tests in `src/dotnet/tests/TraceMap.Tests` that construct representative `CodeFact` values and assert report text.

### TypeScript

Update `src/typescript/src/reporting/MarkdownReportWriter.ts`.

- Add a `formatQueryPattern` helper parallel to .NET.
- Branch on `fact.properties.sqlSourceKind`.
- Preserve existing `displayFields` for query-builder facts.
- Add or extend TypeScript report tests to cover both SQL-shape and query-builder rows.

### Python

Update `src/python/tracemap_py/report.py`.

- Add a `## Query Patterns` section.
- Render SQL-shape facts using the same field vocabulary.
- If Python later emits query-builder facts, fall back to the query-builder field display rather than printing `none` for all cases.
- Add Python tests that scan `samples/python-fastapi-sample` and assert report output contains the `orders` SQL-shape evidence without raw SQL text.

## Shared Formatting Rules

- Sort rows deterministically by file path, line, fact ID or stable display name where available.
- Limit displayed rows if the report has an existing section limit; if truncation exists, say how many rows were omitted.
- Escape or wrap display values in Markdown backticks.
- Use semicolon-separated field lists as already stored in fact properties.
- Do not display raw snippets, raw SQL text, or literal values.

## Limitations

Reports are evidence presentation only. A readable SQL-shape row does not prove:

- the query executes at runtime
- the database table exists
- the SQL dialect accepts the query
- a branch is feasible
- generated ORM SQL is equivalent
- values inside the query are known

These limitations should be visible either in the report limitations section or traceable through `docs/LANGUAGE_ADAPTER_CONTRACT.md` and `rules/rule-catalog.yml`.

## Validation

Run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
./scripts/check-private-paths.sh
git diff --check
```

If TypeScript dependencies are not installed, install them according to the existing TypeScript validation path before running `npm run check`.
