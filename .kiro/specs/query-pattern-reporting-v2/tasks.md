# Query Pattern Reporting V2 Tasks

## Implementation Tasks

- [x] 1. Confirm current report behavior. Requirements: 1, 3, 4, 5, 6.
  - [x] Inspect current .NET query-pattern report output.
  - [x] Inspect current TypeScript query-pattern report output.
  - [x] Inspect current JVM report writer behavior for `QueryPatternDetected`.
  - [x] Confirm Python already renders SQL-shape query-pattern facts.
  - [x] Identify the smallest report-writer tests for each affected adapter.

- [x] 2. Add .NET flavor-aware query-pattern formatting. Requirements: 1, 2, 3, 4, 8.
  - [x] Add SQL-shape discriminator based on non-empty `sqlSourceKind`.
  - [x] Add SQL-shape row formatting for operation, table, columns, source kind, hash, tier, and span.
  - [x] Apply safe identifier rendering for table, column, and field values.
  - [x] Preserve query-builder field rendering for `filterFields`, `sortFields`, `selectFields`, `includeFields`, and `mutationFields`.
  - [x] Preserve deterministic report ordering.
  - [x] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [x] 3. Add TypeScript flavor-aware query-pattern formatting. Requirements: 1, 2, 3, 5, 8.
  - [x] Add SQL-shape discriminator based on non-empty `sqlSourceKind`.
  - [x] Add SQL-shape row formatting for operation, table, columns, source kind, hash, tier, and span.
  - [x] Apply safe identifier rendering for table, column, and field values.
  - [x] Preserve Prisma/Base44 query-builder field rendering.
  - [x] Preserve deterministic report ordering.
  - [x] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [x] 4. Add JVM query-pattern report section. Requirements: 1, 2, 3, 6, 8.
  - [x] Add deterministic `## Query Patterns` section when query-pattern facts exist.
  - [x] Place the section after existing evidence summary/sample sections and before limitations.
  - [x] Sort rows by safe file path, start line, and stable fact identifier when available.
  - [x] Add SQL-shape row formatting.
  - [x] Add query-builder fallback formatting.
  - [x] Apply safe identifier rendering for table, column, and field values.
  - [x] Preserve existing JVM report sections and ordering.
  - [x] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [x] 5. Add shared limitations wording. Requirements: 2, 7, 8.
  - [x] Add or align limitation text containing `static shape evidence` and `runtime execution`.
  - [x] State that query-pattern rows do not prove database schema existence.
  - [x] State that query-pattern rows do not prove dialect validity.
  - [x] State that query-pattern rows do not prove generated SQL equivalence.
  - [x] State that query-pattern rows do not prove branch feasibility.

- [x] 6. Update rule catalog and docs. Requirements: 7.
  - [x] Update `rules/rule-catalog.yml` for `csharp.syntax.querypattern.v1`.
  - [x] Update `rules/rule-catalog.yml` for `typescript.integration.querypattern.v1`.
  - [x] Update `rules/rule-catalog.yml` for `jvm.integration.sql.v1`.
  - [x] Update `rules/rule-catalog.yml` for `python.integration.sql.v1`.
  - [x] Update `docs/VALIDATION.md` with query-pattern report inspection examples.
  - [x] Update adapter README files only if existing docs would otherwise mislead users.

- [x] 7. Add .NET tests. Requirements: 2, 3, 4, 8.
  - [x] Test existing query-builder facts still render extracted fields.
  - [x] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [x] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [x] Label synthetic SQL-shape tests as report-rendering-only.
  - [x] Test raw SQL text is not rendered.
  - [x] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [x] Test SQL-shape facts do not render as low-signal `fields none` rows.
  - [x] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.

- [x] 8. Add TypeScript tests. Requirements: 2, 3, 5, 8.
  - [x] Test existing query-builder facts still render extracted fields.
  - [x] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [x] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [x] Label synthetic SQL-shape tests as report-rendering-only.
  - [x] Test raw SQL text is not rendered.
  - [x] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [x] Test SQL-shape facts do not render as low-signal `fields none` rows.
  - [x] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.

- [x] 9. Add JVM tests. Requirements: 2, 3, 6, 8.
  - [x] Test `## Query Patterns` appears when query-pattern facts exist.
  - [x] Test `## Query Patterns` is omitted or absent when no query-pattern facts exist, matching the final implementation decision.
  - [x] Test SQL-shape facts render derived metadata.
  - [x] Test SQL-shape facts render a 32-character lowercase `queryShapeHash`.
  - [x] Test query-builder fallback facts render fields.
  - [x] Test a fact without `sqlSourceKind` renders as query-builder evidence, not SQL-shape evidence.
  - [x] Test raw SQL text is not rendered.
  - [x] Test unsafe table/column identifiers are replaced with deterministic identifier hashes.
  - [x] Test query-pattern limitation text contains `static shape evidence` and `runtime execution`.
  - [x] Test existing JVM sections remain ordered around the new query-pattern section.
  - [x] Prefer real sample output where stable; otherwise label synthetic tests clearly.

- [x] 10. Validate. Requirements: 8.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `npm run check --prefix src/typescript`
  - [x] `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test`
  - [x] Python adapter tests if Python report code changes.
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Deferred Follow-Ups

- Add SQL-shape producers for TypeScript if a later extraction spec approves it.
- Expand JVM SQL/JPA extraction if a later extraction spec approves it.
- Add combined-report query-pattern display changes if combined reports need their own flavor-aware view.
- Add HTML/UI rendering after the Markdown behavior is stable.
