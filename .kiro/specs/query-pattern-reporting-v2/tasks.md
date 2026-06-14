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
  - [ ] Preserve query-builder field rendering for `filterFields`, `sortFields`, `selectFields`, `includeFields`, and `mutationFields`.
  - [ ] Preserve deterministic report ordering.
  - [ ] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [ ] 3. Add TypeScript flavor-aware query-pattern formatting. Requirements: 1, 2, 3, 5, 8.
  - [ ] Add SQL-shape discriminator based on non-empty `sqlSourceKind`.
  - [ ] Add SQL-shape row formatting for operation, table, columns, source kind, hash, tier, and span.
  - [ ] Preserve Prisma/Base44 query-builder field rendering.
  - [ ] Preserve deterministic report ordering.
  - [ ] Avoid raw SQL, snippets, literal values, URLs, connection strings, and local absolute paths.

- [ ] 4. Add JVM query-pattern report section. Requirements: 1, 2, 3, 6, 8.
  - [ ] Add deterministic `## Query Patterns` section when query-pattern facts exist.
  - [ ] Add SQL-shape row formatting.
  - [ ] Add query-builder fallback formatting.
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
  - [ ] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Test SQL-shape facts do not render as low-signal `fields none` rows.

- [ ] 8. Add TypeScript tests. Requirements: 2, 3, 5, 8.
  - [ ] Test existing query-builder facts still render extracted fields.
  - [ ] Test synthetic SQL-shape facts render operation, table, columns, source kind, and 32-character lowercase shape hash.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Test SQL-shape facts do not render as low-signal `fields none` rows.

- [ ] 9. Add JVM tests. Requirements: 2, 3, 6, 8.
  - [ ] Test `## Query Patterns` appears when query-pattern facts exist.
  - [ ] Test SQL-shape facts render derived metadata.
  - [ ] Test query-builder fallback facts render fields.
  - [ ] Test raw SQL text is not rendered.
  - [ ] Prefer real sample output where stable; otherwise label synthetic tests clearly.

- [ ] 10. Validate. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] TypeScript adapter tests using the documented command.
  - [ ] JVM adapter tests using the documented command.
  - [ ] Python adapter tests if Python report code changes.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- Add SQL-shape producers for TypeScript if a later extraction spec approves it.
- Expand JVM SQL/JPA extraction if a later extraction spec approves it.
- Add combined-report query-pattern display changes if combined reports need their own flavor-aware view.
- Add HTML/UI rendering after the Markdown behavior is stable.
