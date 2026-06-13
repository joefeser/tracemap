# Query Pattern Reporting Requirements

## Purpose

TraceMap already stores query-pattern facts from several language adapters. The next slice makes those facts readable in human reports so users can see database/query evidence without opening SQLite or raw NDJSON.

This is a presentation layer change. It must not add new analysis claims, mutate fact schemas, or change reducer behavior.

## Requirements

### 1. SQL-shape query patterns are readable

1. WHEN a report includes a `QueryPatternDetected` fact with `sqlSourceKind` THEN the report SHALL render it as SQL-shape evidence.
2. SQL-shape report rows SHALL include visible derived fields when present:
   - `operationName`
   - `tableName` or `tableNames`
   - `columnNames` or `fieldNames`
   - `sqlSourceKind`
   - `queryShapeHash`
3. SQL-shape rows SHALL include evidence tier and file/line span.
4. SQL-shape rows SHALL NOT claim runtime execution, database schema existence, generated SQL equivalence, branch feasibility, or dialect validity.
5. SQL-shape rows SHALL NOT display raw SQL text or literal values.

### 2. Existing query-builder pattern rendering is preserved

1. WHEN a `QueryPatternDetected` fact does not have `sqlSourceKind` THEN existing query-builder rendering SHALL continue to display fields from:
   - `filterFields`
   - `sortFields`
   - `selectFields`
   - `includeFields`
   - `mutationFields`
2. Existing C#, TypeScript, and JVM-style query-builder facts SHALL NOT regress to `fields none` when those properties are present.
3. Report rendering MAY use a shared helper per language/runtime, but it SHALL remain deterministic and schema-compatible.

### 3. Scan reports use the same presentation contract

1. The .NET Markdown scan report SHALL render SQL-shape query patterns distinctly from query-builder patterns.
2. The TypeScript Markdown scan report SHALL render SQL-shape query patterns distinctly if such facts are present in a TypeScript `ScanResult`.
3. The Python Markdown scan report SHALL add a query-pattern section and render Python SQL-shape facts with the same field vocabulary.
4. Report output SHALL remain useful when optional properties are absent by using explicit placeholders such as `unknown`, `none`, or `n/a`.

### 4. Documentation and tests

1. The implementation SHALL add tests covering SQL-shape query-pattern rendering.
2. The implementation SHALL add tests proving existing query-builder rendering remains intact.
3. `docs/VALIDATION.md` or equivalent validation notes SHALL include a way to inspect query-pattern report output from public samples.
4. `docs/LANGUAGE_ADAPTER_CONTRACT.md` SHALL remain the source of truth for SQL evidence semantics; this slice MAY clarify report behavior but SHALL NOT loosen evidence limitations.

## Non-goals

- No SQL parser dependency.
- No raw SQL storage or report display.
- No new fact type.
- No reducer classification changes.
- No endpoint alignment changes.
- No runtime dependency inference.
