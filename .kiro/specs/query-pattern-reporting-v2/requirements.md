# Query Pattern Reporting V2 Requirements

## Introduction

TraceMap now has two practical `QueryPatternDetected` evidence flavors:

- SQL-shape facts with `sqlSourceKind`, `operationName`, table/column metadata, and `queryShapeHash`.
- Query-builder facts with `filterFields`, `sortFields`, `selectFields`, `includeFields`, `mutationFields`, and `patternHash`.

The Python report already renders SQL-shape facts. The remaining gap is broader report consistency: .NET and TypeScript reports currently render query-builder facts as `fields ...`, but SQL-shape facts can degrade to `fields none`; JVM reports do not yet render query-pattern rows at all. This spec makes query-pattern reports readable across language adapters without changing extractors, fact schemas, reducers, combined analysis, or runtime claims.

## Scope

In scope:

- Improve single-language scan `report.md` rendering for `.NET`, TypeScript, and JVM.
- Preserve existing Python SQL-shape report behavior unless a shared wording clarification is required.
- Render SQL-shape facts safely when a report writer sees `sqlSourceKind`.
- Render query-builder facts with existing field semantics when `sqlSourceKind` is absent.
- Add tests that use real adapter output where producers exist and synthetic facts only for forward-compatible SQL-shape rendering in adapters that do not yet emit SQL-shape facts.
- Update validation docs and rule catalog limitations.

Out of scope:

- No SQL parser dependency.
- No new extractor behavior.
- No schema changes.
- No reducer changes.
- No combined report/path/diff/impact/reverse behavior changes.
- No raw SQL, literal values, source snippets, connection strings, or local absolute paths in reports.
- No runtime SQL execution, schema validity, dialect validity, generated SQL equivalence, or business impact claims.

## Requirements

### Requirement 1: Query-pattern flavor detection

**User Story:** As a report reader, I want TraceMap to distinguish SQL-shape evidence from query-builder evidence so that query reports do not show misleading `fields none` rows.

#### Acceptance Criteria

1. WHEN a `QueryPatternDetected` fact has a non-empty `sqlSourceKind` property THEN report writers SHALL render it as SQL-shape evidence.
2. WHEN a `QueryPatternDetected` fact does not have a non-empty `sqlSourceKind` property THEN report writers SHALL render it as query-builder evidence.
3. WHEN optional SQL-shape properties are missing THEN report writers SHALL use deterministic placeholders such as `unknown`, `none`, or `n/a`.
4. WHEN optional query-builder properties are missing THEN report writers SHALL preserve existing `fields none` behavior for query-builder facts.
5. WHEN a fact has both flavor families THEN `sqlSourceKind` SHALL be the discriminator and SQL-shape rendering SHALL win.

### Requirement 2: SQL-shape rendering

**User Story:** As a maintainer, I want SQL-shape report rows to show useful derived metadata without exposing raw SQL.

#### Acceptance Criteria

1. SQL-shape rows SHALL display `operationName` when present, else `unknown`.
2. SQL-shape rows SHALL display `tableName` when present, else `tableNames`, else `unknown`.
3. SQL-shape rows SHALL display `columnNames` when present, else `fieldNames`, else `none`.
4. SQL-shape rows SHALL display `sqlSourceKind` when present, else `unknown`.
5. SQL-shape rows SHALL display `queryShapeHash` when present, else `n/a`.
6. SQL-shape rows SHALL include evidence tier and file/line span.
7. SQL-shape rows SHALL NOT display raw SQL text, source snippets, literal values, connection strings, raw URLs, or local absolute paths.
8. SQL-shape rows SHALL include wording that makes the row static shape evidence, not runtime execution proof.

### Requirement 3: Query-builder rendering is preserved

**User Story:** As an existing TraceMap user, I want C# and TypeScript query-builder reports to keep showing filter/sort/select/include/mutation fields.

#### Acceptance Criteria

1. Query-builder rows SHALL display `operationName` or the existing fallback display name.
2. Query-builder rows SHALL display a semicolon-delimited field list derived from `filterFields`, `sortFields`, `selectFields`, `includeFields`, and `mutationFields`.
3. Query-builder rows SHALL preserve `fields none` when none of those field properties are present.
4. Query-builder rows SHOULD display `patternHash` when present if doing so does not destabilize existing report readability.
5. Query-builder rows SHALL include evidence tier and file/line span.
6. Existing C# and TypeScript query-builder fixture expectations SHALL continue to pass.

### Requirement 4: .NET report writer

**User Story:** As a .NET user, I want `tracemap scan` reports to render SQL-shape facts correctly when they appear and preserve current LINQ query-pattern rendering.

#### Acceptance Criteria

1. `src/dotnet/TraceMap.Reporting/MarkdownReportWriter.cs` SHALL route `QueryPatternDetected` facts through a flavor-aware formatter.
2. C# syntax query-pattern facts emitted by `csharp.syntax.querypattern.v1` SHALL continue to show operation and extracted fields.
3. A synthetic .NET report test SHALL cover a SQL-shape `QueryPatternDetected` fact with `sqlSourceKind`, `operationName`, `tableName`, `columnNames`, and `queryShapeHash`.
4. The synthetic SQL-shape test SHALL assert the report shows derived SQL-shape metadata and does not show raw SQL text.
5. The .NET implementation SHALL NOT add new extraction rules or change facts emitted by `CSharpSyntaxExtractor`.

### Requirement 5: TypeScript report writer

**User Story:** As a TypeScript user, I want TypeScript reports to preserve Prisma/Base44 query-builder rendering and handle SQL-shape facts safely if a future extractor emits them.

#### Acceptance Criteria

1. `src/typescript/src/reporting/MarkdownReportWriter.ts` SHALL route `QueryPatternDetected` facts through a flavor-aware formatter.
2. Existing Prisma/Base44 query-pattern facts SHALL continue to show operation and extracted fields.
3. A synthetic TypeScript report test SHALL cover a SQL-shape `QueryPatternDetected` fact.
4. The synthetic SQL-shape test SHALL assert derived SQL-shape metadata appears and raw SQL text does not appear.
5. The TypeScript implementation SHALL NOT add SQL extraction or change current query-pattern fact emission.

### Requirement 6: JVM report writer

**User Story:** As a JVM user, I want JVM scan reports to show query-pattern evidence when JVM SQL/JPA extractors emit `QueryPatternDetected`.

#### Acceptance Criteria

1. `src/jvm/src/main/java/com/tracemap/jvm/reporting/MarkdownReportWriter.java` SHALL add a deterministic `## Query Patterns` section.
2. JVM SQL-shape facts SHALL render with the same SQL-shape fields and limitations as other languages.
3. Query-builder-style JVM facts, if any future producer emits them, SHALL render with the shared query-builder field list behavior.
4. JVM report tests SHALL use real JVM sample output if `samples/jvm-*` currently emits `QueryPatternDetected`; otherwise they SHALL use synthetic report-writer facts and clearly label the test synthetic.
5. The JVM implementation SHALL NOT add extractor behavior in this slice.

### Requirement 7: Shared limitations and docs

**User Story:** As a reviewer, I want report wording and rule docs to prevent overclaiming.

#### Acceptance Criteria

1. Reports that render query-pattern rows SHALL include a stable limitation phrase containing `static shape evidence` and `runtime execution`.
2. The limitation SHALL state that query-pattern evidence does not prove database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
3. `docs/VALIDATION.md` SHALL include scan-report inspection examples for query-pattern sections across the affected adapters.
4. `rules/rule-catalog.yml` SHALL document that C#, TypeScript, JVM, and Python query-pattern reports may display derived metadata but must not display raw SQL text or literal values.
5. `docs/LANGUAGE_ADAPTER_CONTRACT.md` MAY clarify report-display behavior, but SHALL NOT loosen SQL evidence semantics.

### Requirement 8: Determinism and safety

**User Story:** As a maintainer, I want query-pattern reporting to stay deterministic and safe to share.

#### Acceptance Criteria

1. Report row ordering SHALL preserve each report writer's existing deterministic ordering contract.
2. New tests SHALL assert no raw SQL text is displayed for SQL-shape rows.
3. New tests SHALL assert at least one real or synthetic 32-character lowercase hash is displayed for SQL-shape rows.
4. New tests SHALL assert query-builder rows do not regress to missing fields.
5. Implementations SHALL not add timestamps, random IDs, or nondeterministic ordering.
6. Implementations SHALL not store source snippets or raw SQL by default.
