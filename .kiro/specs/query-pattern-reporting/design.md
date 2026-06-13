# Query Pattern Reporting Design

## Overview

Python now emits SQL-shape `QueryPatternDetected` evidence with:

- `sqlSourceKind`
- `operationName`
- `tableName` / `tableNames`
- `columnNames` / `fieldNames`
- `queryShapeHash`

The current Python report does not list query-pattern evidence, so the most valuable first slice is to make Python `report.md` show this evidence clearly and safely.

This spec intentionally scopes .NET, TypeScript, and JVM report changes out of the first PR. Their current scan reports only receive facts produced by their own adapters, and those adapters do not emit SQL-shape `QueryPatternDetected` facts today.

## Fact Flavors

TraceMap has two practical flavors of `QueryPatternDetected` evidence:

| Flavor | Discriminator | Typical fields | Meaning |
| --- | --- | --- | --- |
| SQL-shape | non-empty `sqlSourceKind` | `operationName`, `tableName`, `tableNames`, `columnNames`, `fieldNames`, `queryShapeHash` | Lightweight static SQL shape evidence |
| Query-builder | no `sqlSourceKind` | `filterFields`, `sortFields`, `selectFields`, `includeFields`, `mutationFields`, `patternHash` | Framework/query-builder call shape evidence |

Python currently emits the SQL-shape flavor. C# and TypeScript currently emit the query-builder flavor. JVM query-pattern reporting is not part of this slice.

## Current Behavior

Python scan reports currently summarize:

- fact counts
- evidence tiers
- rule counts
- known gaps
- Python MVP limitations

They do not list query-pattern evidence, even though `facts.ndjson` and `index.sqlite` contain useful SQL-shape metadata for public samples such as `samples/python-fastapi-sample`.

## Proposed Python Output

Add a `## Query Patterns` section to `src/python/tracemap_py/report.py`.

For SQL-shape facts, render compact lines like:

```text
- `SELECT` on `orders` columns `id;status;total` source `orm-text` shape `9858bc44eae2ff12104e17967718e5dc` (Tier2Structural) at `app/repository.py:8`
```

The `orm-text` example is Tier2 in `samples/python-fastapi-sample` because SQLAlchemy import evidence is visible. Other literal SQL shapes may render as Tier3 when only syntax evidence is available.

When optional fields are missing, keep the row honest:

```text
- `unknown` on `unknown` columns `none` source `sql-file` shape `n/a` (Tier2Structural) at `schema.sql:1`
```

Use:

- operation: `operationName` or `unknown`
- table: `tableName`, else `tableNames`, else `unknown`
- columns: `columnNames`, else `fieldNames`, else `none`; these properties are semicolon-delimited strings
- source: `sqlSourceKind` or `unknown`
- shape: `queryShapeHash` or `n/a`
- location: `evidence.file_path:evidence.start_line`

Do not display raw SQL text, source snippets, or literal values.

The Python emitter currently sets `fieldNames` to the same value as `columnNames` for SQL-shape facts, so the fallback is mainly future-proofing. Render only one column list.

## Limitations in Report

Place `## Query Patterns` after the `## Rules` section and before `## Known Gaps`. Put the SQL-shape limitation directly under the query-pattern section so it is visible where the evidence appears.

Required limitation meaning:

```text
SQL query-pattern rows are static shape evidence only; they do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
```

Exact wording can vary, but the report must include the anchor phrases `static shape evidence` and `runtime execution` so tests can assert the evidence boundary.

## Ordering and Row Count

Preserve existing Python report behavior by iterating over facts in emission order. The Python scanner already discovers inventory deterministically, and this avoids introducing a new sort contract.

Do not add a row cap or truncation behavior in this slice. Existing report sections do not implement truncation notices, and adding that behavior would broaden the PR.

Do not deduplicate query-pattern rows. If the same table appears in ORM text, a `.sql` file, and a migration, the report should show each evidence source separately.

## Documentation

Update:

- `docs/VALIDATION.md` with a Markdown report inspection command for the public Python sample.
- `rules/rule-catalog.yml` under `python.integration.sql.v1` with a report-display limitation note that derived table/column/source-kind metadata may be displayed but raw SQL text and literal values are not.

Do not loosen `docs/LANGUAGE_ADAPTER_CONTRACT.md`. Clarifications are allowed only if they preserve the existing evidence boundary.

## Future .NET and TypeScript Work

.NET and TypeScript report renderers may later add SQL-shape formatting as forward-compatibility scaffolding. That follow-up should state clearly that:

- C# and TypeScript adapters do not emit `sqlSourceKind` query-pattern facts today.
- SQL-shape renderer tests are synthetic unless a real adapter producer exists.
- Query-builder rendering must continue using `filterFields`, `sortFields`, `selectFields`, `includeFields`, `mutationFields`, and `patternHash`.
- SQL-shape rendering should use `queryShapeHash`, not `patternHash`.

Keep this future work separate from the Python report PR unless a new producer makes it user-visible.

## Validation

Run:

```bash
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

`npm run check --prefix src/typescript` is not required for the first implementation PR unless TypeScript files are changed.
