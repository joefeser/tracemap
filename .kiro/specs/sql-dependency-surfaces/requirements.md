# SQL Dependency Surfaces Requirements

## Introduction

TraceMap already treats SQL as a cross-language dependency surface in combined reports, paths, reverse queries, diffs, and impact. The current evidence is useful but uneven:

- Python emits direct SQL text and SQL-shape `QueryPatternDetected` facts.
- .NET emits SQL text/config/EF/Dapper evidence and some query-builder facts, but SQL-shape metadata is not consistently derived from direct SQL text.
- TypeScript emits Prisma/Base44 query-builder facts, but direct SQL client literals are not yet a shared SQL-shape surface.
- JVM emits SQL resources/JDBC/JPA SQL text evidence, but query-pattern shape metadata may be absent or inconsistent.

This spec defines a deterministic, report-safe SQL dependency-surface slice. It does not add a full SQL parser. It makes simple static SQL shapes more comparable across adapters and makes combined SQL surfaces easier to query, diff, reverse, and inspect.

## Scope

In scope:

- Define a shared SQL surface contract for `SqlTextUsed`, `SqlFileDeclared`, `QueryPatternDetected`, and `DatabaseColumnMapping`.
- Add or align lightweight SQL-shape extraction for simple static SQL text across .NET, TypeScript, JVM, and Python where current adapter support is missing.
- Normalize safe derived SQL surface metadata for combined reports, paths, reverse queries, diffs, and impact.
- Add tests and validation fixtures that prove simple SQL surfaces can be discovered and linked without raw SQL output.
- Update `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, `docs/ACCEPTANCE.md`, and `rules/rule-catalog.yml`.

Out of scope:

- No full SQL parser dependency.
- No dialect-specific validation.
- No schema introspection or database connectivity.
- No migration execution.
- No runtime query execution claims.
- No generated SQL equivalence claims for ORMs.
- No branch feasibility, transaction, permission, tenant, or row-level security inference.
- No raw SQL text, source snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Requirements

### Requirement 1: Shared SQL surface fact contract

**User Story:** As a cross-language TraceMap user, I want SQL evidence to use the same safe metadata keys across adapters so combined analysis can compare surfaces without language-specific special cases.

#### Acceptance Criteria

1. WHEN an adapter emits `SqlTextUsed` for statically visible SQL text THEN it SHALL include `textHash` and `textLength`.
2. WHEN an adapter can determine a safe SQL source kind THEN it SHALL include `sqlSourceKind` using the shared values documented in `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
3. WHEN an adapter can determine a safe leading operation THEN it SHALL include `operationName` using uppercase operation names.
4. `SqlTextUsed` SHALL NOT include table or column guesses unless a separate `QueryPatternDetected` fact is also emitted.
5. WHEN a simple static SQL shape is visible THEN the adapter SHALL emit `QueryPatternDetected` with `operationName`, `sqlSourceKind`, `queryShapeHash`, and the safest available table/column metadata.
6. `QueryPatternDetected` SHALL use `tableName` for one primary safe table, `tableNames` for multiple safe tables, and `columnNames` or `fieldNames` for visible safe columns or projected fields.
7. WHEN table or column identifiers are unsafe or ambiguous THEN the adapter SHALL omit those properties rather than emit raw SQL fragments.
8. All SQL facts SHALL include rule ID, evidence tier, file span, commit SHA, and extractor version through the existing fact model.
9. WHEN `QueryPatternDetected` is emitted for SQL-shape evidence THEN it SHALL NOT include raw SQL text, predicate literal values, source snippets, or generated SQL text.
10. WHEN an adapter emits new SQL-shape `QueryPatternDetected` behavior THEN the emitting rule ID SHALL be documented in `rules/rule-catalog.yml` with limitations before the implementation PR merges.
11. WHEN an adapter detects dynamic SQL construction but cannot see a complete static SQL text THEN it SHALL emit an `AnalysisGap` or adapter-equivalent reduced-coverage evidence instead of pretending complete `SqlTextUsed` evidence exists.

### Requirement 2: Lightweight SQL-shape extraction

**User Story:** As a maintainer, I want simple SQL text to become deterministic table/column dependency evidence without pretending TraceMap understands every SQL dialect.

#### Acceptance Criteria

1. The shared SQL-shape extractor SHALL recognize simple static `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `TRUNCATE`, `MERGE`, `CALL`, `EXEC`, and `EXECUTE` starts when the operation is visible after trimming whitespace.
2. The extractor SHALL derive `queryShapeHash` from the Python v1 reference normalization: strip SQL comments, mask string literal contents, collapse whitespace, trim trailing semicolons, and hash the normalized masked SQL text with SHA-256 truncated to 32 lowercase hex characters.
3. The extractor SHALL derive table and column metadata only for simple patterns documented in the design.
4. The extractor SHALL ignore or omit quoted identifiers, complex expressions, subqueries, CTEs, dynamic concatenation, interpolated values, and dialect-specific constructs unless a deterministic rule explicitly supports them.
5. The extractor SHALL preserve `SqlTextUsed` even when no `QueryPatternDetected` shape can be derived.
6. The extractor SHALL not store raw SQL, string literal values, predicate values, source snippets, or generated SQL.
7. The extractor SHALL emit an `AnalysisGap` or skip shape emission for dynamic SQL construction according to each adapter's existing reduced-coverage behavior.

### Requirement 3: .NET adapter SQL surfaces

**User Story:** As a .NET user, I want Dapper, ADO.NET, EF raw SQL, and `.sql` resources to produce shared SQL surface evidence when the SQL text is statically visible.

#### Acceptance Criteria

1. .NET SHALL preserve existing `SqlTextUsed`, `SqlCommandDetected`, `DapperCallDetected`, `DbChangeSaved`, and `DatabaseColumnMapping` behavior.
2. .NET SHALL add SQL-shape `QueryPatternDetected` for simple static SQL literals in supported Dapper/ADO.NET/EF raw-SQL call shapes when the text is visible.
3. .NET SHALL add SQL-shape `QueryPatternDetected` for `.sql` files when the file text contains a supported simple static shape.
4. .NET SHALL set `sqlSourceKind` to `literal-string`, `orm-text`, `sql-file`, or `dynamic-boundary` as appropriate; production migration-file classification is deferred unless already supported by existing scanner behavior.
5. .NET SHALL not emit raw SQL text in facts, reports, logs, or generated markdown/json.
6. .NET tests SHALL cover at least one Dapper or ADO.NET literal, one EF raw SQL literal, one `.sql` file shape, and one dynamic boundary that does not emit complete SQL text evidence.
7. .NET SQL text facts attached to code SHALL use a containing method/class/file target symbol when available so path and reverse analysis can link SQL surfaces to reachable code.

### Requirement 4: TypeScript adapter SQL surfaces

**User Story:** As a TypeScript user, I want direct SQL client literals and existing query-builder evidence to participate in SQL surface analysis without claiming generated SQL for ORMs.

#### Acceptance Criteria

1. TypeScript SHALL preserve existing Prisma/Base44 query-builder `QueryPatternDetected` facts.
2. TypeScript SHALL not relabel Prisma/Base44 query-builder facts as SQL-shape evidence unless a producer has direct SQL text evidence.
3. TypeScript SHALL detect simple static SQL text literals for common direct SQL APIs such as `query(...)`, `execute(...)`, `sql.query(...)`, tagged SQL literals, or adapter-supported client methods when deterministic.
4. TypeScript direct SQL evidence SHALL emit `SqlTextUsed` and, when shape is visible, SQL-shape `QueryPatternDetected`.
5. TypeScript SHALL classify template literals with non-static expressions as dynamic boundaries unless the static SQL shape is still safe to derive without literal values.
6. TypeScript tests SHALL cover one direct SQL literal with `sqlSourceKind`, one dynamic boundary, and one existing Prisma/Base44 query-builder regression without `sqlSourceKind`.
7. TypeScript direct SQL literal behavior SHALL use a documented rule ID such as `typescript.integration.sql.v1`; query-builder behavior SHALL keep its existing rule ID unless evidence behavior changes.

### Requirement 5: JVM adapter SQL surfaces

**User Story:** As a JVM user, I want JDBC, JPA query literals, and SQL resources to produce comparable SQL surfaces.

#### Acceptance Criteria

1. JVM SHALL preserve existing `SqlFileDeclared`, `SqlTextUsed`, and JPA/JDBC evidence.
2. JVM SHALL emit SQL-shape `QueryPatternDetected` for simple static SQL resources and JDBC/JPA literals when the shape is visible.
3. JVM SHALL not require semantic classpath success for syntax-visible SQL literals.
4. JVM SHALL set `sqlSourceKind` to `literal-string`, `sql-file`, `orm-text`, or `dynamic-boundary` as appropriate; production migration-file classification is deferred unless already supported by existing scanner behavior.
5. JVM tests SHALL cover one SQL resource shape, one JDBC literal shape, one JPA literal shape if the current extractor supports JPA literal evidence, and one unsupported/dynamic shape that does not overclaim.

### Requirement 6: Python adapter SQL surfaces

**User Story:** As a Python user, I want the existing SQL-shape behavior to remain the reference path while aligning with any shared contract refinements.

#### Acceptance Criteria

1. Python SHALL preserve existing SQLAlchemy, DB-API, `.sql` file, and query-pattern behavior.
2. Python SHALL align property names, source-kind values, and limitations with the shared SQL surface contract.
3. Python tests SHALL continue covering direct SQL text, `.sql` files, SQLAlchemy `text(...)`, and safe query-pattern reporting.
4. Python SHALL not regress raw SQL suppression.

### Requirement 7: Combined SQL surface normalization

**User Story:** As a reviewer, I want combined reports and path queries to show SQL surfaces consistently regardless of source language.

#### Acceptance Criteria

1. `tracemap report` over a combined index SHALL project SQL surfaces from `QueryPatternDetected`, `SqlTextUsed`, `SqlFileDeclared`, `DatabaseColumnMapping`, `DapperCallDetected`, and `SqlCommandDetected` using one shared surface model.
2. SQL surfaces SHALL have deterministic stable keys independent of combined row order and local paths.
3. SQL surface stable keys SHALL prefer `queryShapeHash` before table-only metadata to avoid collapsing distinct queries against the same table.
4. `tracemap paths --to-surface sql-query` SHALL continue to find terminal SQL surfaces and SHALL label unlinked SQL evidence as gaps rather than successful paths.
5. `tracemap reverse --surface sql-query` SHALL work for SQL-shape and hash-only surfaces.
6. `tracemap diff` and `tracemap impact` SHALL compare SQL surfaces without treating reduced coverage or hash-only evidence as full semantic certainty.
7. Combined Markdown/JSON SHALL not display raw SQL, literal values, snippets, connection strings, raw URLs, or local absolute paths.
8. WHEN combined reports render table or column identifiers THEN they SHALL validate identifiers against the safe identifier policy and hash or omit unsafe values even if scanner facts stored them.
9. WHEN a SQL surface has only `textHash` and no shape metadata THEN diff and impact SHALL include a `HashOnlyEvidence` caveat and classify changes no stronger than review-tier evidence unless both sides have credible shape metadata.
10. WHEN a SQL surface stable key depends on fact ID hash fallback THEN combined analysis SHALL emit a `VolatileIdentity` or equivalent gap and classify the surface no stronger than review-tier evidence.

### Requirement 8: Evidence tiers and limitations

**User Story:** As a user reading SQL results, I want the report to explain how strong each SQL finding is and where it can be wrong.

#### Acceptance Criteria

1. SQL-shape facts from `.sql` files and recognized framework direct-SQL APIs MAY be `Tier2Structural` when file/framework structure is credible, but limitations SHALL state that Tier2 SQL evidence does not prove runtime execution or schema validity.
2. Syntax-only literal detections SHALL be `Tier3SyntaxOrTextual` unless the adapter has stronger structural context.
3. Dynamic boundaries and unsupported SQL construction SHALL emit `Tier4Unknown` gaps where appropriate.
4. Every new or updated SQL rule SHALL document limitations in `rules/rule-catalog.yml`.
5. Reports SHALL state that SQL surfaces are static shape/hash evidence and do not prove runtime execution, schema existence, dialect validity, generated SQL equivalence, permissions, transactions, or branch feasibility.

### Requirement 9: Safety and determinism

**User Story:** As an open-source maintainer, I want SQL reports to be safe to publish and byte-stable across runs.

#### Acceptance Criteria

1. No implementation SHALL store raw SQL text by default.
2. No implementation SHALL render raw SQL text, predicate literal values, source snippets, connection strings, raw URLs, or local absolute paths.
3. Hashes SHALL be deterministic lowercase SHA-256 hex truncations using documented lengths.
4. Identifier display SHALL follow the safe identifier policy from query-pattern reporting v2.
5. Output ordering SHALL be deterministic.
6. No scanner or reporter SHALL add timestamps to deterministic report payloads.
7. Validation SHALL include `./scripts/check-private-paths.sh` and `git diff --check`.

### Requirement 10: Validation and samples

**User Story:** As a contributor, I want repeatable sample checks that prove SQL surfaces are useful across languages.

#### Acceptance Criteria

1. Each affected adapter SHALL have a local fixture or unit test that emits `SqlTextUsed` and SQL-shape `QueryPatternDetected`.
2. At least one combined sample smoke SHALL prove an endpoint-to-`sql-query` path from application code to a terminal SQL surface.
3. Validation docs SHALL include SQLite inspection queries for SQL facts and combined SQL surfaces.
4. Public OSS smoke expectations SHALL remain honest about reduced coverage and shall not require every public repo to emit SQL shapes.
5. All language adapter tests affected by this slice SHALL pass before PR merge.
6. Shared golden fixtures SHALL prove `queryShapeHash` consistency across Python, .NET, TypeScript, and JVM implementations for the same simple SQL inputs.
7. Combined tests SHALL prove two SQL surfaces against the same table but different shape hashes do not collapse to one stable key.
8. Path tests SHALL prove SQL facts without call/symbol path evidence emit `UnlinkedSurface` or equivalent gaps and do not produce successful endpoint-to-SQL paths.
9. Combined report tests SHALL prove deterministic byte-stable Markdown/JSON output for SQL surfaces.
