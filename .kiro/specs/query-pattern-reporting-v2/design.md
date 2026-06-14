# Query Pattern Reporting V2 Design

## Overview

Improve single-language scan report rendering for `QueryPatternDetected` facts across .NET, TypeScript, and JVM.

TraceMap already emits two useful query-pattern shapes:

- SQL-shape evidence: static metadata derived from SQL-like text or query APIs, identified by `sqlSourceKind`.
- Query-builder evidence: field-oriented query operations such as filters, sorts, selects, includes, and mutations.

The current report writers are uneven. Python already renders SQL-shape facts safely. .NET and TypeScript render query-builder fields, which is good for LINQ/Prisma/Base44-style facts, but SQL-shape facts can collapse to low-signal rows such as `fields none`. JVM can emit SQL-related facts but does not yet render a query-pattern section in scan reports.

This slice is report-only. It does not change extraction, fact schemas, reducers, combined indexes, or runtime interpretation.

## Goals

- Render SQL-shape `QueryPatternDetected` facts with useful derived metadata.
- Preserve existing .NET and TypeScript query-builder report semantics.
- Add JVM query-pattern report output.
- Keep reports deterministic and safe for public sharing.
- Document that query-pattern rows are static shape evidence, not runtime SQL execution proof.

## Non-Goals

- No SQL parser dependency.
- No new SQL or query-builder extraction.
- No fact schema migration.
- No reducer, combined report, path, diff, impact, or reverse-query changes.
- No source snippets or raw SQL text by default.
- No runtime database, ORM, schema, dialect, generated-SQL, or branch-feasibility claims.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Current State

| Adapter | Current behavior | Desired behavior |
| --- | --- | --- |
| .NET | Query-builder facts render as operation plus `fields ...`; SQL-shape facts can show `fields none`. | Flavor-aware query-pattern rows. |
| TypeScript | Query-builder facts render as operation plus `fields ...`; SQL-shape facts would show low-signal field output. | Flavor-aware query-pattern rows. |
| JVM | No dedicated scan report query-pattern section. | Add deterministic `## Query Patterns` section. |
| Python | SQL-shape facts already render useful metadata. | Preserve behavior unless shared wording needs a small clarification. |

## Flavor Detection

The discriminator is intentionally simple:

```text
sqlSourceKind is present and non-empty => SQL-shape
otherwise => query-builder
```

If a future fact contains both SQL-shape and query-builder properties, `sqlSourceKind` wins. This avoids rendering SQL-shape evidence through query-builder field logic and prevents misleading `fields none` rows.

Missing optional properties are rendered with deterministic placeholders:

| Property family | Missing display |
| --- | --- |
| Operation | `unknown` |
| Table or tables | `unknown` |
| Columns or fields | `none` |
| Source kind | `unknown` |
| Hash | `n/a` |

## SQL-Shape Rendering

SQL-shape rows should use only derived safe metadata:

```text
- SQL shape `<operation>` table `<table-or-tables>` columns `<columns-or-fields>` source `<sqlSourceKind>` shape `<queryShapeHash>` (<tier>) at `<safe-path>:<line>`
```

Allowed displayed properties:

- `operationName`
- `tableName`
- `tableNames`
- `columnNames`
- `fieldNames`
- `sqlSourceKind`
- `queryShapeHash`
- evidence tier
- safe file path and line span

Disallowed displayed properties:

- raw SQL text
- interpolated SQL text
- literal values
- source snippets
- config values
- connection strings
- raw URLs
- local absolute paths

The row label should use "SQL shape" rather than "SQL query executed" or similar runtime wording.

## Query-Builder Rendering

Query-builder rows preserve the existing field-oriented behavior:

```text
- Query builder `<operation>` fields `<fields>` pattern `<patternHash>` (<tier>) at `<safe-path>:<line>`
```

Field properties are combined deterministically from:

- `filterFields`
- `sortFields`
- `selectFields`
- `includeFields`
- `mutationFields`

If no field property is present, keep the existing `fields none` behavior for query-builder facts. That phrase should not appear for SQL-shape rows unless a SQL-shape fact truly has a rendered column list of `none`.

`patternHash` may be displayed when present. The hash is safe because it is derived evidence, but it must not be substituted with raw query text.

## .NET Implementation

Primary file:

```text
src/dotnet/TraceMap.Reporting/MarkdownReportWriter.cs
```

Suggested helper shape:

```csharp
private static string FormatQueryPattern(CodeFact fact)
private static bool IsSqlShapeQueryPattern(CodeFact fact)
private static string FormatSqlShapeQueryPattern(CodeFact fact)
private static string FormatQueryBuilderPattern(CodeFact fact)
private static string DisplayQueryBuilderFields(CodeFact fact)
```

The existing `Query Patterns` section should continue to select facts with `FactTypes.QueryPatternDetected`, but it should route each fact through `FormatQueryPattern`.

No extractor changes are expected in `CSharpSyntaxExtractor`. Existing `csharp.syntax.querypattern.v1` facts should still render operation names and extracted fields.

## TypeScript Implementation

Primary file:

```text
src/typescript/src/reporting/MarkdownReportWriter.ts
```

Suggested helper shape:

```typescript
function formatQueryPattern(fact: CodeFact): string
function isSqlShapeQueryPattern(fact: CodeFact): boolean
function formatSqlShapeQueryPattern(fact: CodeFact): string
function formatQueryBuilderPattern(fact: CodeFact): string
function displayQueryBuilderFields(fact: CodeFact): string
```

Existing Prisma/Base44-style query-builder facts should continue to render operation names and field lists. This slice should not add SQL extraction to the TypeScript adapter.

## JVM Implementation

Primary file:

```text
src/jvm/src/main/java/com/tracemap/jvm/reporting/MarkdownReportWriter.java
```

Add a `## Query Patterns` section using the same flavor rules.

The JVM writer should render:

- real SQL-shape facts if the current JVM sample/extractor output contains `QueryPatternDetected`;
- synthetic report-writer facts in tests if no stable sample currently emits them;
- future query-builder-style facts with the shared field-list behavior.

This slice should not change JVM extractor behavior.

## Python Implementation

Primary file for reference:

```text
src/python/tracemap_py/report.py
```

Python already renders SQL-shape query-pattern metadata. Code changes are not expected unless the implementation needs to align limitation wording with the shared docs. Python behavior should not regress.

## Limitations Wording

Reports that include query-pattern rows should include a stable limitation phrase with these terms:

```text
static shape evidence
runtime execution
```

Suggested wording:

```text
Query-pattern rows are static shape evidence. They do not prove runtime execution, database schema existence, SQL dialect validity, generated SQL equivalence, or branch feasibility.
```

This can live in the existing limitations section for each report writer.

## Rule Catalog Updates

Update `rules/rule-catalog.yml` limitations for query-pattern rules:

- `csharp.syntax.querypattern.v1`
- `typescript.integration.querypattern.v1`
- `jvm.integration.sql.v1`
- `python.integration.sql.v1`

The rule docs should state that reports may display derived metadata such as operation names, table names, column names, field names, and shape hashes, but must not display raw SQL text or literal values.

No new rule IDs are expected because this is report rendering over existing evidence rules.

## Validation Docs

Update `docs/VALIDATION.md` with inspection examples:

```bash
rg -n "Query Patterns|SQL shape|Query builder|static shape evidence" <scan-output>/report.md
rg -n "fields none" <scan-output>/report.md
```

The docs should explain that `fields none` is acceptable for query-builder facts with no extracted fields, but SQL-shape facts should render operation/table/column/hash metadata instead.

## Tests

Add focused report-writer tests rather than large fixture scans where possible.

.NET tests should cover:

- existing query-builder row rendering still includes extracted fields;
- synthetic SQL-shape row rendering includes operation, table, columns, source kind, and `queryShapeHash`;
- raw SQL text is not rendered;
- SQL-shape rows do not degrade to query-builder `fields none`.

TypeScript tests should cover:

- existing Prisma/Base44 query-builder row rendering still includes fields;
- synthetic SQL-shape row rendering includes derived metadata;
- raw SQL text is not rendered.

JVM tests should cover:

- `## Query Patterns` section appears when query-pattern facts exist;
- SQL-shape rows render derived metadata;
- query-builder fallback renders fields;
- raw SQL text is not rendered.

Python tests are not required unless Python report code changes. If Python changes, run the relevant Python adapter tests and confirm no report regression.

## Determinism and Safety

The implementation should preserve each report writer's existing fact ordering. If a writer currently sorts by fact order, keep that. If it sorts by file/span/type, keep that.

New formatting helpers must not:

- add timestamps;
- generate random IDs;
- read source files;
- show raw SQL or snippets;
- show local absolute paths;
- infer runtime execution.

## Validation Commands

Expected validation after implementation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm test --prefix src/typescript
./gradlew -p src/jvm test
./scripts/check-private-paths.sh
git diff --check
```

If the repository uses a different TypeScript or JVM test command at implementation time, use the command documented in that adapter's README or validation docs and record the substitution in the PR summary.
