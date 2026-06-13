# Query Pattern Reporting Requirements

## Purpose

TraceMap's Python adapter now stores useful SQL-shape `QueryPatternDetected` facts, but the Python scan report only summarizes counts, tiers, rules, and gaps. This slice makes those facts readable in `report.md` so users can see database/query evidence without opening SQLite or raw NDJSON.

This is a presentation layer change. It must not add new analysis claims, mutate fact schemas, or change reducer behavior.

## Requirements

### 1. Python SQL-shape query patterns are readable

1. WHEN a Python scan emits a `QueryPatternDetected` fact with a non-empty `sqlSourceKind` property THEN the Python report SHALL render it as SQL-shape evidence.
2. SQL-shape report rows SHALL include visible derived fields when present:
   - `operationName`
   - `tableName` or `tableNames`
   - `columnNames`, preferring `columnNames` over duplicate `fieldNames`
   - `sqlSourceKind`
   - `queryShapeHash`
3. SQL-shape rows SHALL include evidence tier and file/line span.
4. SQL-shape rows SHALL NOT claim runtime execution, database schema existence, generated SQL equivalence, branch feasibility, or dialect validity.
5. SQL-shape rows SHALL NOT display raw SQL text or literal values.
6. The Python report SHALL include SQL-shape limitations near the query-pattern output or in the Python limitations section.

### 2. Python report output remains deterministic

1. The Python report SHALL preserve the existing deterministic fact emission order for the query-pattern section.
2. The Python report SHALL use explicit placeholders such as `unknown`, `none`, or `n/a` when optional properties are absent.
3. The Python report SHALL not add a row cap or truncation behavior in this slice.

### 3. Documentation and tests

1. The implementation SHALL add tests covering Python SQL-shape query-pattern rendering from `samples/python-fastapi-sample`.
2. The tests SHALL assert that the report includes `orders`, `id;status;total`, `orm-text` or another visible SQL source kind, and `queryShapeHash`.
3. The tests SHALL assert that raw SQL text such as `SELECT id, status, total FROM orders` is not displayed in `report.md`.
4. `docs/VALIDATION.md` SHALL include a Markdown report inspection command, such as a `grep` against the public Python sample scan output.
5. `rules/rule-catalog.yml` SHALL keep `python.integration.sql.v1` aligned with the report display contract by noting that reports display derived table/column/source-kind metadata without raw SQL text.
6. `docs/LANGUAGE_ADAPTER_CONTRACT.md` SHALL remain the source of truth for SQL evidence semantics; this slice MAY clarify report behavior but SHALL NOT loosen evidence limitations.

### 4. Follow-up renderer scope is explicit

1. .NET and TypeScript report renderers are out of scope for the first implementation PR because no current .NET or TypeScript adapter emits SQL-shape `QueryPatternDetected` facts with `sqlSourceKind`.
2. A future .NET or TypeScript renderer PR MAY add SQL-shape formatting as forward-compatibility scaffolding, but tests for that path SHALL be labeled synthetic unless an adapter emits real `sqlSourceKind` facts.
3. JVM report rendering is out of scope for this slice; current JVM reports do not render query-builder fields that could regress to `fields none`.

## Non-goals

- No SQL parser dependency.
- No raw SQL storage or report display.
- No new fact type.
- No reducer classification changes.
- No endpoint alignment changes.
- No runtime dependency inference.
- No .NET, TypeScript, or JVM report-renderer changes in the first implementation PR.
