# Query Pattern Reporting Tasks

- [ ] 1. Read current report behavior for context
  - [ ] 1.1 Read `src/python/tracemap_py/report.py` and confirm the implementation target.
  - [ ] 1.2 Read existing .NET/TypeScript report writers only to avoid accidentally changing out-of-scope renderers.
  - [ ] 1.3 Do not create an audit artifact for this step.

- [ ] 2. Implement Python SQL-shape query rendering
  - [ ] 2.1 Add a deterministic `## Query Patterns` section to `src/python/tracemap_py/report.py`.
  - [ ] 2.2 Treat a `QueryPatternDetected` fact as SQL-shape only when `sqlSourceKind` is non-empty.
  - [ ] 2.3 Render `operationName`, `tableName`/`tableNames`, `columnNames` preferred over `fieldNames`, `sqlSourceKind`, `queryShapeHash`, evidence tier, and file/line.
  - [ ] 2.4 Preserve fact emission order; do not add row limits or truncation behavior.
  - [ ] 2.5 Do not display raw SQL text, source snippets, or literal values.

- [ ] 3. Surface limitations in the Python report
  - [ ] 3.1 Add SQL-shape limitation text to the query-pattern section or Python limitations section.
  - [ ] 3.2 Ensure the limitation says query-pattern rows do not prove runtime execution, schema existence, dialect validity, generated SQL equivalence, or branch feasibility.

- [ ] 4. Add tests
  - [ ] 4.1 Add or extend Python tests against `samples/python-fastapi-sample`.
  - [ ] 4.2 Assert `report.md` includes `orders`, `id;status;total`, a visible SQL source kind such as `orm-text` or `sql-file`, and `queryShapeHash` or a concrete shape hash label.
  - [ ] 4.3 Assert `report.md` does not include raw SQL text such as `SELECT id, status, total FROM orders`.
  - [ ] 4.4 Assert the SQL-shape limitation text appears in `report.md`.
  - [ ] 4.5 Run the new focused test first, then run the full Python test suite.

- [ ] 5. Documentation and rule catalog
  - [ ] 5.1 Update `docs/VALIDATION.md` with one or two `grep` commands that inspect query-pattern output from the public Python sample `report.md`; do not add a new script.
  - [ ] 5.2 Update `rules/rule-catalog.yml` for `python.integration.sql.v1` with a report-display limitation note.
  - [ ] 5.3 Keep `docs/LANGUAGE_ADAPTER_CONTRACT.md` semantics intact; only clarify if needed.

- [ ] 6. Validation
  - [ ] 6.1 Run `/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests`.
  - [ ] 6.2 Run `PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh`.
  - [ ] 6.3 Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] 6.4 Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] 6.5 Run `./scripts/check-private-paths.sh`.
  - [ ] 6.6 Run `git diff --check`.

## Deferred Follow-up

- [ ] Future .NET SQL-shape rendering, synthetic-only unless a producer exists.
- [ ] Future TypeScript SQL-shape rendering, synthetic-only unless a producer exists.
- [ ] Future JVM query-pattern report rendering, separately scoped.
