# SQL Schema Change Impact Requirements

## Introduction

TraceMap emits cross-language SQL dependency surfaces from static SQL text, query-shape evidence, mapping-only persistence evidence, combined surfaces, paths, reverse queries, diffs, and general contract-delta impact analysis.

SQL Schema Change Impact is a SQL-specific delta adapter for the existing contract-delta impact engine. It accepts structured database-facing change input and projects those changes onto existing TraceMap evidence. It answers review questions like:

- Which endpoints, services, methods, repositories, and dependency paths have static evidence tied to a changed schema, table, column, query shape, SQL resource, or persistence mapping?
- Which evidence is SQL query text/shape evidence versus ORM or declarative schema mapping evidence?
- Where does TraceMap stop because evidence is hash-only, unlinked, reduced coverage, identity-conflicted, or too ambiguous?

This is still static analysis. SQL/schema impact means evidence-backed review context. It does not prove runtime SQL execution, database schema existence, migration correctness, query-plan behavior, data contents, permissions, transaction behavior, tenant isolation, branch feasibility, production traffic, or actual customer impact.

## Scope

In scope:

- Define a deterministic SQL/schema delta input model for `schema`, `table`, `column`, `query-shape`, `sql-file`, `mapping`, and `persistence-surface` changes.
- Normalize SQL/schema deltas into safe selectors that reuse the contract-delta impact engine and its reporting boundaries.
- Match deltas against SQL-shape `QueryPatternDetected`, `SqlTextUsed`, `SqlFileDeclared`, `DatabaseColumnMapping`, ORM mapping facts, combined `sql-query` and `sql-persistence` surfaces, reachable path evidence, reverse evidence, and coverage gaps.
- Support single-index and combined-index operation.
- Distinguish query-shape evidence, SQL text/hash evidence, SQL resource evidence, mapping evidence, static reachability evidence, and runtime execution claims.
- Emit Markdown and JSON impact reports with evidence rows, rule IDs, evidence tiers, file spans, source labels, commit SHAs, extractor versions, supporting IDs, caveats, and limitations.
- Reuse existing combined report, path, reverse, diff, impact, and contract-delta semantics where possible.
- Preserve redaction and deterministic output guarantees.

Out of scope:

- No database connection.
- No migration execution.
- No dialect-specific semantic validation.
- No query-plan inference or schema introspection.
- No SQL parser dependency in v1.
- No generated SQL equivalence claims for ORMs.
- No data-value, permission, tenant, row-level security, transaction, or branch feasibility inference.
- No runtime traffic or production usage claims.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No raw SQL text, source snippets, literal values, connection strings, raw URLs, or local absolute paths in reports.

## Requirements

### Requirement 1: SQL Schema Delta Input

**User Story:** As a release reviewer, I want a structured SQL/schema delta file so TraceMap can match database-facing changes without parsing free-form migration text.

#### Acceptance Criteria

1. WHEN a SQL/schema delta file is provided THEN it SHALL include `version`, `source`, `changes`, and optional `metadata`.
2. WHEN `version` is provided THEN v1 SHALL be `sql-schema-delta.v1`.
3. WHEN a change is provided THEN it SHALL include `id`, `kind`, `changeType`, `reference`, and optional `old`, `new`, `severity`, `sourceHint`, and `metadata`.
4. WHEN a change omits `id` THEN v1 SHALL fail validation or emit a documented input gap and SHALL NOT derive an implicit ID.
5. WHEN duplicate change IDs are provided THEN v1 SHALL fail validation or emit a duplicate-ID input gap.
6. WHEN `kind` is provided THEN allowed values SHALL include `schema`, `table`, `column`, `query-shape`, `sql-file`, `mapping`, and `persistence-surface`.
7. WHEN `changeType` is provided THEN allowed values SHALL include `added`, `removed`, `renamed`, `type_changed`, `nullable_changed`, `constraint_changed`, `index_changed`, `behavior_changed`, `shape_changed`, and `unknown_changed`.
8. WHEN an unknown `version`, `kind`, or `changeType` is provided THEN v1 SHALL fail closed or emit an input gap; it SHALL NOT silently continue with a stronger classification.
9. WHEN `reference` is provided for a schema change THEN it SHALL support safe fields such as `schemaName`, `databaseNameHash`, `sourceKind`, and `surfaceKind`.
10. WHEN `reference` is provided for a table change THEN it SHALL support safe fields such as `schemaName`, `tableName`, `tableNames`, `sourceKind`, and `surfaceKind`.
11. WHEN `reference` is provided for a column change THEN it SHALL support safe fields such as `schemaName`, `tableName`, `columnName`, `columnNames`, `mappedName`, `containingType`, and `propertyName`.
12. WHEN `reference` is provided for a query-shape change THEN it SHALL support `queryShapeHash`, `textHash`, `operationName`, `tableName`, `tableNames`, `columnNames`, `sqlSourceKind`, and `sourceSymbol` where available.
13. WHEN `reference` is provided for a sql-file change THEN it SHALL support safe fields such as `sqlResourceName`, `sqlSourceKind`, `textHash`, and `queryShapeHash`; absolute file paths in input MAY be used for matching but SHALL NOT be echoed in output.
14. WHEN `reference` is provided for a mapping or persistence-surface change THEN it SHALL support `surfaceKind=sql-persistence`, `tableName`, `columnName`, `mappedName`, `containingType`, and supporting symbol fields.
15. WHEN a reference only contains unsafe free text or raw SQL THEN TraceMap SHALL emit an input gap, skip the unsafe selector, and SHALL NOT echo unsafe values in output.
16. WHEN input order changes but content is identical THEN parsed changes, gaps, findings, and reports SHALL remain deterministic.
17. WHEN `version` is `sql-schema-delta.v1` THEN the file SHALL be treated as a SQL-only input format and SHALL NOT be parsed as a valid `contract-delta-v2` file.
18. WHEN `changes` is empty THEN TraceMap SHALL emit a valid empty report rather than an error.

### Requirement 2: Single-Index Matching

**User Story:** As a repository maintainer, I want SQL/schema impact to work against one scan index when I do not have a combined index.

#### Acceptance Criteria

1. WHEN a single-language index is provided THEN TraceMap SHALL match SQL/schema changes against `facts`, `scan_manifest`, and any available symbol/relationship tables in that index.
2. WHEN matching schema changes THEN TraceMap SHALL consider safe schema metadata in SQL-shape facts, SQL resource facts, mapping facts, and adapter-specific persistence metadata where available.
3. WHEN matching table changes THEN TraceMap SHALL consider `QueryPatternDetected` table fields, `SqlTextUsed` with linked shape evidence, `SqlFileDeclared`, `DatabaseColumnMapping`, ORM mapping facts, and SQL command framework facts where available.
4. WHEN matching column changes THEN TraceMap SHALL consider `columnName`, `columnNames`, `fieldNames`, `mappedName`, `propertyName`, `attributeName`, DTO/member facts, and `DatabaseColumnMapping` where available.
5. WHEN matching query-shape changes THEN TraceMap SHALL prefer `queryShapeHash` over table-only metadata and SHALL treat `textHash` as hash-only review-tier evidence unless shape metadata also matches.
6. WHEN a match is schema-only, syntax-only, name-only, table-only, column-only, mappedName-only, hash-only, or ambiguous THEN classification SHALL be no stronger than review-tier.
7. WHEN full semantic coverage is credible and no evidence matches THEN TraceMap MAY emit `NoEvidenceFullCoverage`.
8. WHEN coverage is reduced, build failed, semantic analysis is unavailable, commit SHA is missing, or relevant analysis gaps exist THEN no-match results SHALL be `NoEvidenceReducedCoverage` or `UnknownAnalysisGap`, not `NoEvidenceFullCoverage`.
9. WHEN the same fact matches by multiple reference fields THEN TraceMap SHALL deduplicate by stable fact ID and preserve all matched fields in evidence metadata.
10. WHEN the language adapter cannot provide Tier1 SQL mapping evidence THEN direct SQL/schema findings SHALL NOT be classified as `DefiniteImpact`.

### Requirement 3: Combined-Index Matching

**User Story:** As a platform reviewer, I want SQL/schema impact across combined indexes so I can see cross-repo and cross-language static evidence.

#### Acceptance Criteria

1. WHEN a combined index is provided THEN TraceMap SHALL preserve source label, source index ID, scan ID, repo identity hash, commit SHA, language, analysis level, build status, and coverage warnings for each finding.
2. WHEN matching SQL query evidence in a combined index THEN TraceMap SHALL reuse the combined `sql-query` surface projection rather than re-projecting facts independently.
3. WHEN matching mapping-only persistence evidence in a combined index THEN TraceMap SHALL use `sql-persistence` surfaces and SHALL NOT treat those rows as query execution evidence.
4. WHEN matching table, column, or mapping deltas THEN `sql-persistence` surfaces SHALL be considered by default and reported separately from `sql-query` evidence.
5. WHEN a table/column delta matches both `sql-query` and `sql-persistence` surfaces THEN the report SHALL show both evidence categories separately.
6. WHEN source identity is missing, unverified, duplicated, or conflicting THEN findings for that source SHALL be downgraded or gapped according to existing combined diff/impact identity rules.
7. WHEN duplicate SQL surface identities exist THEN TraceMap SHALL emit a duplicate/volatile identity caveat and SHALL NOT pick an arbitrary winner for strong classification.
8. WHEN optional combined precision tables are missing THEN TraceMap SHALL emit schema/coverage gaps and SHALL NOT claim complete cross-source reachability.
9. WHEN combined and single-index output differ because combined path/reverse context exists only in combined mode THEN the report SHALL state which evidence layer was available.

### Requirement 4: Evidence Categories

**User Story:** As a reviewer, I want SQL impact evidence separated by meaning so I do not confuse query text with schema mapping or runtime execution.

#### Acceptance Criteria

1. WHEN evidence comes from SQL-shape `QueryPatternDetected` THEN TraceMap SHALL label it as static query-shape evidence.
2. WHEN evidence comes from `SqlTextUsed` without shape metadata THEN TraceMap SHALL label it as hash-only SQL text evidence and include `HashOnlyEvidence`.
3. WHEN evidence comes from safe schema/table/column metadata without query-shape or mapping evidence THEN TraceMap SHALL label it as static schema-metadata evidence and SHALL NOT imply the database schema exists at runtime.
4. WHEN evidence comes from `DatabaseColumnMapping`, `mappedName`, or ORM declarative mapping THEN TraceMap SHALL label it as mapping/persistence evidence and SHALL NOT imply a query executes.
5. WHEN evidence comes from `mappedName` without a table and column anchor THEN TraceMap SHALL keep the finding review-tier and include a mapped-name-only caveat.
6. WHEN evidence comes from `.sql` file declaration or SQL resource inventory THEN TraceMap SHALL label it as resource evidence and SHALL NOT imply the file is executed.
7. WHEN evidence comes from path/reverse traversal THEN TraceMap SHALL label it as static reachability evidence and include path/reverse classifications and caveats.
8. WHEN evidence comes from syntax/textual fallback THEN TraceMap SHALL label it as `Tier3SyntaxOrTextual` and keep the finding review-tier.
9. WHEN evidence exists only as an unlinked surface THEN TraceMap SHALL include an `UnlinkedSurface` or equivalent caveat and SHALL NOT report a successful endpoint-to-SQL path.
10. WHEN unsafe fact properties contain raw SQL, snippets, connection strings, URLs, or local absolute paths THEN output SHALL omit or hash them.
11. WHEN supporting IDs are emitted THEN they SHALL be deterministic for identical inputs and options even if the report includes a volatile identity caveat.

### Requirement 5: Path And Reverse Context

**User Story:** As an investigator, I want optional reachability context so I can see which endpoints or symbols statically connect to a changed SQL surface.

#### Acceptance Criteria

1. WHEN `--include-paths` is omitted THEN TraceMap SHALL NOT run forward path traversal and SHALL state that path context was not requested.
2. WHEN `--include-reverse` is omitted THEN TraceMap SHALL NOT run reverse traversal and SHALL state that reverse context was not requested.
3. WHEN `--include-paths` is provided with a combined index THEN TraceMap SHALL gather bounded path context for matched `sql-query` and `sql-persistence` surfaces where stable selectors can be derived.
4. WHEN `--include-reverse` is provided with a combined index THEN TraceMap SHALL gather bounded reverse context from matched SQL surfaces to endpoint, symbol, or source roots where supported.
5. WHEN `--include-paths` or `--include-reverse` is provided with a single-language index THEN TraceMap SHALL reject the option with a clear message.
6. WHEN path/reverse selectors cannot be derived from stable `queryShapeHash`, `textHash`, table/column metadata, or surface identity THEN TraceMap SHALL emit `PathContextUnavailable` or `ReverseContextUnavailable`.
7. WHEN a match is table-only, column-only, name-only, syntax-only, high fan-out, or ambiguous THEN TraceMap SHALL NOT seed traversal unless a stable surface identity is also present.
8. WHEN no path/reverse evidence is found and contributing sources have reduced coverage THEN TraceMap SHALL emit `UnknownAnalysisGap` rather than proving absence of callers.
9. WHEN traversal caps are hit THEN TraceMap SHALL emit `TruncatedByLimit` and mark report coverage partial.
10. WHEN path/reverse context is emitted THEN supporting path IDs, edge IDs, fact IDs, rule IDs, evidence tiers, source labels, and coverage caveats SHALL be sorted deterministically.
11. WHEN a `sql-persistence` surface is used for traversal THEN output SHALL keep it distinct from `sql-query` and SHALL state that mapping reachability is not query execution.
12. WHEN path/reverse context is available for only some findings in a batch THEN context gaps SHALL be emitted per finding and the summary SHALL mark context coverage as partial.

### Requirement 6: Classifications

**User Story:** As a reviewer, I want SQL/schema impact classifications that distinguish strong static evidence from ambiguous or unknown evidence.

#### Acceptance Criteria

1. WHEN a single-index finding has direct Tier1 semantic member/type/method evidence tied to a changed SQL mapping or query boundary THEN it MAY be `DefiniteImpact` unless coverage gaps require downgrade.
2. WHEN the adapter language cannot currently provide Tier1 SQL mapping or query-boundary evidence THEN it SHALL NOT emit `DefiniteImpact` for that finding.
3. WHEN a single-index finding has strong Tier2 structural SQL-shape, ORM mapping, endpoint, or framework integration evidence THEN it SHALL be `ProbableImpact` unless stronger semantic evidence exists.
4. WHEN a single-index finding is schema-only, syntax-only, name-only, table-only, column-only, mappedName-only, hash-only, or ambiguous THEN it SHALL be `NeedsReview`.
5. WHEN a combined-index finding has credible full-metadata SQL surface identity plus supporting static path/reverse evidence under credible coverage THEN it MAY be `StaticImpactEvidence`.
6. WHEN a combined-index finding has strong SQL-shape or mapping evidence but lacks requested path/reverse context THEN it SHALL be `ProbableStaticImpact`.
7. WHEN a combined-index finding depends on Tier3, hash-only, table-only, schema-only, duplicate identity, volatile identity, or ambiguous matching THEN it SHALL be `NeedsReviewImpact`.
8. WHEN analysis gaps prevent a credible conclusion THEN TraceMap SHALL classify it as `UnknownAnalysisGap`.
9. WHEN no evidence matches under credible full coverage in single-index mode THEN TraceMap MAY emit `NoEvidenceFullCoverage`.
10. WHEN no evidence matches under reduced coverage in single-index mode THEN TraceMap SHALL emit `NoEvidenceReducedCoverage` or `UnknownAnalysisGap` if relevant gaps exist.
11. WHEN no comparable evidence is found in combined mode under credible coverage THEN TraceMap SHALL emit report-level `NoImpactEvidence`, not a strong item classification.
12. WHEN source identity, hash-only evidence, unlinked surfaces, or schema gaps reduce confidence THEN TraceMap SHALL downgrade and include caveats.
13. WHEN confidence is emitted THEN it SHALL be derived from classification using a documented fixed mapping.
14. WHEN the input index is combined THEN TraceMap SHALL NOT emit single-index classifications such as `DefiniteImpact`; combined mode SHALL use the combined classification vocabulary.

### Requirement 7: CLI And Output

**User Story:** As a user, I want SQL/schema impact output that follows existing TraceMap conventions.

#### Acceptance Criteria

1. WHEN the command is implemented THEN v1 SHALL use `tracemap reduce --index <index.sqlite> --sql-schema-delta <delta.json> --out <path>`, where `--index` MAY point to either a single-language index or a combined index.
2. WHEN the command is implemented THEN it SHALL reuse the contract-delta impact engine internally rather than creating a competing SQL impact engine.
3. WHEN both `--contract-delta` and `--sql-schema-delta` are provided THEN TraceMap SHALL reject the command with a clear mutual-exclusion error.
4. WHEN neither `--contract-delta` nor `--sql-schema-delta` is provided to `reduce` THEN TraceMap SHALL reject the command with a clear missing-input error.
5. WHEN `--sql-schema-delta` is active and output path is a file with no format provided THEN TraceMap SHALL write Markdown to that file.
6. WHEN `--sql-schema-delta` is active, output path is a directory or has no extension, and `--format` is omitted or `--format markdown` is provided THEN TraceMap SHALL write `sql-impact-report.md` and `sql-impact-report.json`.
7. WHEN `--contract-delta` is active THEN existing contract-delta output file names SHALL remain unchanged.
8. WHEN `--format json` is provided with file output THEN TraceMap SHALL write deterministic JSON to that file.
9. WHEN the command completes THEN the CLI SHALL print output path, change count, finding count, gap count, source count, and report coverage.
10. WHEN `--exit-code` is provided THEN TraceMap SHALL return `1` only for `DefiniteImpact`, `ProbableImpact`, `StaticImpactEvidence`, or `ProbableStaticImpact`; gap-only, review-tier, and no-evidence results SHALL return `0`.
11. WHEN selectors such as `--source`, `--change-id`, `--kind`, `--table`, `--column`, `--query-shape`, `--surface`, or `--endpoint` are provided THEN filtering SHALL be deterministic and selector no-match SHALL be explicit.
12. WHEN caps such as `--max-findings`, `--max-evidence-rows`, `--max-paths-per-change`, `--max-roots`, and `--max-gaps` are hit THEN TraceMap SHALL emit `TruncatedByLimit`.
13. WHEN the index is opened THEN it SHALL be read-only and SHALL NOT be mutated.
14. WHEN schema-only high fan-out matches exceed caps THEN TraceMap SHALL emit `TruncatedByLimit` rather than silently dropping evidence.
15. WHEN `--format json` is provided with directory output THEN TraceMap SHALL write `sql-impact-report.json` and omit Markdown unless an explicit multi-format option is added later.

### Requirement 8: JSON Report Contract

**User Story:** As an automation author, I want deterministic JSON so CI and release tooling can compare SQL impact output.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `mode`, `input`, `query`, `index`, `summary`, `findings`, `gaps`, `coverageWarnings`, and `limitations`.
2. WHEN a finding is emitted THEN it SHALL include stable finding ID, change ID, change kind, change type, classification, confidence, source label if available, rule ID, evidence tier, evidence category, file span, commit SHA, extractor version, supporting fact IDs, supporting edge IDs, supporting path IDs, and safe display metadata.
3. WHEN a stable finding ID is generated THEN it SHALL be derived from the change ID, source label or single-index source identity, evidence category, classification, and stable evidence key.
4. WHEN `reportType` is `SqlSchemaChangeImpactSingleV1` or `SqlSchemaChangeImpactCombinedV1` THEN the report SHALL use the new SQL impact JSON model and SHALL NOT retrofit the legacy Markdown-only impact record.
5. WHEN SQL metadata is emitted THEN it SHALL use safe fields such as `operationName`, `schemaName`, `databaseNameHash`, `tableName`, `tableNames`, `columnName`, `columnNames`, `mappedName`, `queryShapeHash`, `textHash`, `sqlSourceKind`, and `surfaceKind`.
6. WHEN arrays are emitted THEN they SHALL be sorted deterministically.
7. WHEN metadata objects are emitted THEN keys SHALL be sorted deterministically.
8. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.
9. WHEN the same inputs and options are run twice THEN Markdown and JSON SHALL be byte-stable.
10. WHEN unsafe values appear in fact property bags THEN JSON SHALL omit or hash them.

### Requirement 9: Rules And Limitations

**User Story:** As a maintainer, I want every SQL/schema impact conclusion tied to documented rules and limitations.

#### Acceptance Criteria

1. WHEN a SQL/schema finding is emitted THEN v1 SHALL use existing contract-delta impact rule IDs unless implementation adds and documents SQL-specific rules.
2. WHEN input validation gaps are emitted THEN they SHALL cite `contract.delta.input.v2` once that rule exists in `rules/rule-catalog.yml`, unless a documented SQL-specific input rule exists.
3. WHEN impact findings are emitted THEN they SHALL cite `contract.delta.impact.v2` once that rule exists in `rules/rule-catalog.yml`, unless a documented SQL-specific impact rule exists.
4. WHEN path/reverse context is emitted THEN it SHALL cite existing `combined.paths.*`, `combined.reverse.*`, or `contract.delta.context.v2` once that rule exists in `rules/rule-catalog.yml`.
5. WHEN supporting evidence is emitted THEN each evidence row SHALL include the source fact rule ID and evidence tier.
6. WHEN hash-only caveats are emitted THEN the report SHALL cite the reused surface/impact rule that documents the caveat.
7. WHEN unlinked-surface, selector, truncation, identity, or schema gaps are emitted THEN every gap SHALL include rule ID, evidence tier, and limitation.
8. WHEN implementation introduces new SQL/schema impact rules THEN `rules/rule-catalog.yml` SHALL document them before code merges.
9. WHEN reused contract-delta v2 rules do not yet exist in `rules/rule-catalog.yml` THEN the first implementation PR SHALL add them with limitations before emitting findings or gaps that cite them.
10. WHEN the reused contract-delta v2 rules are absent from `rules/rule-catalog.yml` THEN no finding or gap citing those rule IDs is valid until the catalog entries land.
11. WHEN limitations are rendered THEN they SHALL state SQL/schema impact is static evidence and not runtime execution, schema existence, migration correctness, or dialect validation proof.

### Requirement 10: Tests And Validation

**User Story:** As a maintainer, I want tests that prove SQL/schema impact stays deterministic, safe, and honest as SQL evidence evolves.

#### Acceptance Criteria

1. Tests SHALL cover schema, table, column, query-shape, sql-file, mapping, and persistence-surface delta inputs.
2. Tests SHALL cover single-index and combined-index operation.
3. Tests SHALL prove `queryShapeHash` matches are stronger than table-only or text-hash-only matches.
4. Tests SHALL prove `textHash`-only matches emit `HashOnlyEvidence` and review-tier classification.
5. Tests SHALL prove `DatabaseColumnMapping` matches are `sql-persistence` evidence and do not claim query execution.
6. Tests SHALL prove mappedName-only matches stay review-tier.
7. Tests SHALL prove unlinked SQL surfaces do not count as successful paths.
8. Tests SHALL prove reduced coverage prevents full no-evidence conclusions.
9. Tests SHALL prove source identity conflicts, volatile identities, and duplicate surface identities downgrade or gap findings.
10. Tests SHALL prove path/reverse context is optional, bounded, and gap-labeled when unavailable.
11. Tests SHALL prove raw SQL, snippets, literal values, connection strings, raw URLs, and local absolute paths do not render.
12. Tests SHALL prove Markdown and JSON are byte-stable for identical inputs, including volatile identity caveats.
13. Tests SHALL prove input indexes are opened read-only and not mutated.
14. Tests SHALL prove selector no-match, per-selector no-match details, `--contract-delta`/`--sql-schema-delta` mutual exclusion, exit-code behavior, and cap truncation behavior.
15. Tests SHALL prove fixed confidence mapping for every emitted classification.
16. Tests SHALL prove sql-file reference matching and persistence-surface delta matching.
17. Tests SHALL prove schema-only high fan-out matches remain review-tier.
18. Tests SHALL cover `added` with no evidence, `removed` with multiple evidence rows, unknown `version`, duplicate change IDs, empty changes, combined-index `--include-paths` partial context, and directory `--format json`.
19. Tests SHALL prove report output includes rule IDs, evidence tiers, spans, commit SHAs, extractor versions, and limitations.
20. Implementation validation SHALL include `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check`.
