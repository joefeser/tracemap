# Query Pattern Reporting V2 Tasks

## Implementation Tasks

- [ ] 1. Confirm current report behavior. Requirements: 1, 3, 4, 5, 6.
  - [ ] Inspect current .NET query-pattern report output.
  - [ ] Inspect current TypeScript query-pattern report output.
  - [ ] Inspect current JVM report writer behavior for `QueryPatternDetected`.
  - [ ] Confirm Python already renders SQL-shape query-pattern facts.
  - [ ] Identify the smallest report-writer tests for each affected adapter.

- [ ] 2. Add .NET flavor-aware query-pattern formatting. Requirements: 1, 2, 3, 4, 8.
  - [ ] Add SQL-shape discriminator based on non-empty `sqlSourceKind`.
  - [ ] Add SQL-shape row formatting for operation, table, columns, source kind, hash, tier, and span.
  - [ ] Apply safe identifier rendering for table, column, and field values.
  - [ ] Preserve query-builder field rendering for `filterFields`, `sortFields`, `selectFields`, `includeFields`, and `mutationFields`.
  - [ ] Preserve deterministic report ordering.
  - [ ] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [ ] 3. Add TypeScript flavor-aware query-pattern formatting. Requirements: 1, 2, 3, 5, 8.
  - [ ] Add SQL-shape discriminator based on non-empty `sqlSourceKind`.
  - [ ] Add SQL-shape row formatting for operation, table, columns, source kind, hash, tier, and span.
  - [ ] Apply safe identifier rendering for table, column, and field values.
  - [ ] Preserve Prisma/Base44 query-builder field rendering.
  - [ ] Preserve deterministic report ordering.
  - [ ] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [ ] 4. Add JVM query-pattern report section. Requirements: 1, 2, 3, 6, 8.
  - [ ] Add deterministic `## Query Patterns` section when query-pattern facts exist.
  - [ ] Place the section after existing evidence summary/sample sections and before limitations.
  - [ ] Sort rows by safe file path, start line, and stable fact identifier when available.
  - [ ] Add SQL-shape row formatting.
  - [ ] Add query-builder fallback formatting.
  - [ ] Apply safe identifier rendering for table, column, and field values.
  - [ ] Preserve existing JVM report sections and ordering.
  - [ ] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [ ] 5. Add shared limitations wording. Requirements: 2, 7, 8.
  - [ ] Add or align limitation text containing `static shape evidence` and `runtime execution`.
  - [ ] State that query-pattern rows do not prove database schema existence.
  - [ ] State that query-pattern rows do not prove dialect validity.
  - [ ] State that query-pattern rows do not prove generated SQL equivalence.
  - [ ] State that query-pattern rows do not prove branch feasibility.

- [ ] 6. Update rule catalog and docs. Requirements: 7.
  - [ ] Update `rules/rule-catalog.yml` for `csharp.syntax.querypattern.v1`.
  - [ ] Update `rules/rule-catalog.yml` for `typescript.integration.querypattern.v1`.
  - [ ] Update `rules/rule-catalog.yml` for `jvm.integration.sql.v1`.
  - [ ] Update `rules/rule-catalog.yml` for `python.integration.sql.v1`.
  - [ ] Update `docs/VALIDATION.md` with query-pattern report inspection examples.
  - [ ] Update adapter README files only if existing docs would otherwise mislead users.

- [ ] 7. Add .NET tests. Requirements: 2, 3, 4, 8.
  - [ ] Test existing query-builder facts still render extracted fields.
  - [ ] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [ ] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [ ] Label synthetic SQL-shape tests as report-rendering-only.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [ ] Test SQL-shape facts do not render as low-signal `fields none` rows.
  - [ ] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.

- [ ] 8. Add TypeScript tests. Requirements: 2, 3, 5, 8.
  - [ ] Test existing query-builder facts still render extracted fields.
  - [ ] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [ ] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [ ] Label synthetic SQL-shape tests as report-rendering-only.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [ ] Test SQL-shape facts do not render as low-signal `fields none` rows.
  - [ ] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.

- [ ] 9. Add JVM tests. Requirements: 2, 3, 6, 8.
  - [ ] Test `## Query Patterns` appears when query-pattern facts exist.
  - [ ] Test `## Query Patterns` is omitted or absent when no query-pattern facts exist, matching the final implementation decision.
  - [ ] Test SQL-shape facts render derived metadata.
  - [ ] Test SQL-shape facts render a 32-character lowercase `queryShapeHash`.
  - [ ] Test query-builder fallback facts render fields.
  - [ ] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [ ] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.
  - [ ] Test existing JVM sections remain ordered around the new query-pattern section.
  - [ ] Prefer real sample output where stable; otherwise label synthetic tests clearly.

- [ ] 10. Validate. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `npm run check --prefix src/typescript`
  - [ ] `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test`
  - [ ] Python adapter tests if Python report code changes.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- Add SQL-shape producers for TypeScript if a later extraction spec approves it.
- Expand JVM SQL/JPA extraction if a later extraction spec approves it.
- Add combined-report query-pattern display changes if combined reports need their own flavor-aware view.
- Add HTML/UI rendering after the Markdown behavior is stable.
