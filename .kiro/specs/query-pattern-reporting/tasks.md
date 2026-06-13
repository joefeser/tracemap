# Query Pattern Reporting Tasks

- [x] 1. Read current report behavior for context
  - [x] 1.1 Read `src/python/tracemap_py/report.py` and confirm the implementation target.
  - [x] 1.2 Read existing .NET/TypeScript report writers only to avoid accidentally changing out-of-scope renderers.
  - [x] 1.3 Do not create an audit artifact for this step.

- [x] 2. Implement Python SQL-shape query rendering
  - [x] 2.1 Add a deterministic `## Query Patterns` section to `src/python/tracemap_py/report.py` after `## Rules` and before `## Known Gaps`.
  - [x] 2.2 Treat a `QueryPatternDetected` fact as SQL-shape only when `sqlSourceKind` is non-empty.
  - [x] 2.3 Render `operationName`, `tableName`/`tableNames`, `columnNames` preferred over duplicate `fieldNames`, `sqlSourceKind`, `queryShapeHash`, evidence tier, and file/line.
  - [x] 2.4 Preserve fact emission order; do not add row limits, truncation behavior, or deduplication across sources.
  - [x] 2.5 Do not display raw SQL text, source snippets, or literal values.

- [x] 3. Surface limitations in the Python report
  - [x] 3.1 Add SQL-shape limitation text directly under the query-pattern section.
  - [x] 3.2 Ensure the limitation says query-pattern rows do not prove runtime execution, schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
  - [x] 3.3 Include stable test anchors `static shape evidence` and `runtime execution`.

- [x] 4. Add tests
  - [x] 4.1 Add or extend Python tests against `samples/python-fastapi-sample`.
  - [x] 4.2 Assert `report.md` includes `orders`, `id;status;total`, a visible SQL source kind such as `orm-text` or `sql-file`, and an actual 32-character lowercase hex shape hash value.
  - [x] 4.3 Assert `report.md` does not include raw SQL text such as `SELECT id, status, total FROM orders`.
  - [x] 4.4 Assert the SQL-shape limitation text appears in `report.md`.
  - [x] 4.5 Run the new focused test first, then run the full Python test suite.

- [x] 5. Documentation and rule catalog
  - [x] 5.1 Update `docs/VALIDATION.md` with `grep "orm-text" <out>/report.md` and `grep "orders" <out>/report.md` examples that inspect query-pattern output from the public Python sample; do not add a new script.
  - [x] 5.2 Update the existing `python.integration.sql.v1` entry in `rules/rule-catalog.yml` with a report-display limitation note.
  - [x] 5.3 Keep `docs/LANGUAGE_ADAPTER_CONTRACT.md` semantics intact; only clarify if needed.

- [x] 6. Validation
  - [x] 6.1 Run `/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests`.
  - [x] 6.2 Run `PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh`.
  - [x] 6.3 Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] 6.4 Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] 6.5 Run `./scripts/check-private-paths.sh`.
  - [x] 6.6 Run `git diff --check`.

## Deferred Follow-up

- Future .NET SQL-shape rendering, synthetic-only unless a producer exists.
- Future TypeScript SQL-shape rendering, synthetic-only unless a producer exists.
- Future JVM query-pattern report rendering, separately scoped.
