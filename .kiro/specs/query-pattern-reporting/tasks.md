# Query Pattern Reporting Tasks

- [ ] 1. Audit current report renderers
  - [ ] 1.1 Confirm .NET query-pattern output currently uses query-builder field display only.
  - [ ] 1.2 Confirm TypeScript query-pattern output currently uses query-builder field display only.
  - [ ] 1.3 Confirm Python report lacks a query-pattern evidence section.

- [ ] 2. Implement .NET SQL-shape query rendering
  - [ ] 2.1 Add SQL-shape detection for `QueryPatternDetected` facts with `sqlSourceKind`.
  - [ ] 2.2 Render operation, table(s), columns, source kind, query shape hash, evidence tier, and file/line.
  - [ ] 2.3 Preserve existing query-builder `DisplayFields` behavior.
  - [ ] 2.4 Add tests for SQL-shape and query-builder facts.

- [ ] 3. Implement TypeScript SQL-shape query rendering
  - [ ] 3.1 Add `formatQueryPattern` or equivalent helper.
  - [ ] 3.2 Branch on `sqlSourceKind`.
  - [ ] 3.3 Preserve existing query-builder rendering.
  - [ ] 3.4 Add tests for both rendering modes.

- [ ] 4. Implement Python query-pattern report section
  - [ ] 4.1 Add deterministic query-pattern section to `src/python/tracemap_py/report.py`.
  - [ ] 4.2 Render Python SQL-shape facts with shared vocabulary.
  - [ ] 4.3 Assert sample report includes `orders`, columns, source kind, and no raw SQL.

- [ ] 5. Documentation and validation
  - [ ] 5.1 Update validation docs with a public sample report inspection command.
  - [ ] 5.2 Keep SQL evidence limitations aligned with `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
  - [ ] 5.3 Run .NET, TypeScript, Python, endpoint smoke, private-path guard, and diff checks.
