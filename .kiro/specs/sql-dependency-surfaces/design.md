# SQL Dependency Surfaces Design

## Overview

SQL is not a standalone TraceMap language adapter in this slice. It is a shared dependency-surface layer that every application-language adapter can contribute to.

This design aligns SQL evidence across adapters and downstream combined analysis:

```text
language scanner -> SqlTextUsed / QueryPatternDetected / DatabaseColumnMapping
                 -> index.sqlite
                 -> combine
                 -> report / paths / reverse / diff / impact
```

The implementation remains deterministic and static. It never connects to a database, executes SQL, validates schemas, or infers runtime behavior.

## Goals

- Make simple SQL text surfaces comparable across .NET, TypeScript, JVM, and Python.
- Preserve hash-only SQL evidence when shape extraction is not credible.
- Emit SQL-shape `QueryPatternDetected` only when static table/column/operation metadata is safe.
- Normalize combined `sql-query` surfaces so paths/reverse/diff/impact can reason over them consistently.
- Keep raw SQL and literal values out of facts and reports.

## Non-Goals

- No full SQL parser dependency in this slice.
- No dialect-specific grammar.
- No runtime database connection.
- No migration execution or schema introspection.
- No generated SQL equivalence for ORMs.
- No taint analysis from request values into predicates.
- No branch feasibility, transaction, permission, tenant, or row-level security claims.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Current State

| Area | Current behavior | Gap |
| --- | --- | --- |
| Python | Emits `SqlTextUsed` and SQL-shape `QueryPatternDetected` for simple visible SQL. | Use as reference and preserve behavior. |
| .NET | Emits SQL text/framework evidence and query-builder facts. | Direct SQL literals and `.sql` files need consistent SQL-shape metadata. |
| TypeScript | Emits Prisma/Base44 query-builder facts. | Direct SQL client literals need SQL text/hash/shape evidence; query-builder facts must not overclaim SQL. |
| JVM | Emits SQL resource/JDBC/JPA text evidence. | Shape metadata needs to be emitted consistently when simple static SQL is visible. |
| Combined report/path/reverse/diff/impact | Already recognizes `sql-query` surfaces. | Stable keys and details should be documented and hardened around shape vs hash-only evidence. |

## Shared SQL Fact Semantics

### `SqlTextUsed`

`SqlTextUsed` is hash/text-length evidence. It should be emitted when an adapter sees complete statically visible SQL text.

Required properties:

| Property | Meaning |
| --- | --- |
| `textHash` | SHA-256 over exact raw SQL text bytes encoded as UTF-8, truncated to 32 lowercase hex chars |
| `textLength` | Raw SQL text length, when raw text is statically visible |

Recommended properties:

| Property | Meaning |
| --- | --- |
| `operationName` | Uppercase leading operation when safely visible |
| `sqlSourceKind` | Shared source kind |
| `targetSymbol` | Containing method, file, or boundary symbol for reducer/report display |

`SqlTextUsed` must not carry table/column guesses. Table/column metadata belongs on `QueryPatternDetected`.

Dynamic SQL boundaries where complete static SQL text is not visible should emit `AnalysisGap` or adapter-equivalent reduced-coverage evidence rather than complete `SqlTextUsed`. If an adapter can safely hash a static prefix or literal fragment, it must label the evidence as partial and avoid shape claims.

### `QueryPatternDetected`

`QueryPatternDetected` is structural SQL-shape or query-builder evidence. SQL-shape facts are identified by non-empty `sqlSourceKind`.

SQL-shape properties:

| Property | Meaning |
| --- | --- |
| `operationName` | Uppercase visible operation |
| `sqlSourceKind` | Shared source kind |
| `queryShapeHash` | Hash of normalized masked SQL text using the Python v1 reference semantics |
| `tableName` | Primary safe table identifier |
| `tableNames` | Semicolon-delimited safe table identifiers |
| `columnNames` | Semicolon-delimited safe column identifiers |
| `fieldNames` | Semicolon-delimited safe field identifiers when columns are not exact SQL columns |
| `textHash` | Optional link back to associated SQL text evidence |

SQL-shape facts should be omitted when the adapter cannot safely derive at least an operation, table, column, or shape hash. Hash-only evidence remains `SqlTextUsed`.

`QueryPatternDetected` must not carry raw SQL text, predicate literal values, source snippets, generated SQL text, or secret/config content. The optional `textHash` property is the only link back to SQL text evidence.

### `DatabaseColumnMapping`

`DatabaseColumnMapping` remains schema/mapping evidence from ORM/declarative mapping constructs. It may contribute to `sql-query` surfaces but is not the same as a query execution or query text.

## Shared Source Kinds

Use the existing source-kind vocabulary from `docs/LANGUAGE_ADAPTER_CONTRACT.md`:

| Value | Meaning |
| --- | --- |
| `literal-string` | SQL text literal embedded in application code |
| `sql-file` | Standalone `.sql` resource |
| `orm-text` | ORM helper that directly wraps SQL text |
| `dbapi-execute` | Direct DB API execute call with literal SQL |
| `dynamic-boundary` | Dynamic SQL construction detected; concrete SQL not claimed |

`migration-file` remains reserved vocabulary from the broader language contract, but production migration-file classification is deferred for this slice unless an adapter already has that behavior. This spec may add examples, but it should not add source-kind values without updating docs, rules, tests, and combined rendering.

## Lightweight SQL Shape Extraction

The extractor is deliberately conservative.

### Supported Starts

Trim leading whitespace. Recognize these visible leading operations:

```text
SELECT INSERT UPDATE DELETE MERGE CREATE ALTER DROP TRUNCATE CALL EXEC EXECUTE
```

`WITH` and CTEs are not supported in v1 unless the implementation can safely find the first real operation without comment/dialect parsing. The default behavior is `SqlTextUsed` only.

### Supported Shapes

Initial v1 extraction should support:

| Shape | Derived metadata |
| --- | --- |
| `SELECT a, b FROM table` | operation `SELECT`, `tableName`, `columnNames` |
| `SELECT * FROM table` | operation `SELECT`, `tableName`, no `columnNames` |
| `INSERT INTO table (a, b)` | operation `INSERT`, `tableName`, `columnNames` |
| `UPDATE table SET a = ...` | operation `UPDATE`, `tableName`, `columnNames` from simple set names |
| `DELETE FROM table` | operation `DELETE`, `tableName` |
| `CREATE TABLE table (a ..., b ...)` | operation `CREATE`, `tableName`, `columnNames` when simple |
| `ALTER TABLE table ...` | operation `ALTER`, `tableName` |
| `DROP TABLE table` | operation `DROP`, `tableName` |
| `TRUNCATE TABLE table` | operation `TRUNCATE`, `tableName` |
| `CALL proc(...)` / `EXEC proc` | operation only in v1; omit procedure names from `tableName`/`tableNames` to avoid conflating routines with tables |

The extractor should ignore table aliases, joins, predicates, literal values, and expression details in v1 unless a deterministic simple rule exists.

### Unsafe or Unsupported Shapes

Do not emit table/column properties for:

- quoted identifiers;
- bracketed identifiers;
- comments inside identifier positions;
- interpolated or concatenated SQL fragments;
- subqueries and CTEs;
- `SELECT` expressions that are not simple identifiers;
- dialect-specific hints;
- multiple statements unless split deterministically and safely;
- strings containing obvious secret/config content.

Emit `SqlTextUsed` only, plus an `AnalysisGap` where current adapter conventions require one.

## Query Shape Hash

`queryShapeHash` uses the existing Python adapter behavior as the v1 cross-language reference. The hash input is normalized masked SQL text, not the raw SQL text and not a structural metadata tuple.

Reference normalization from `src/python/tracemap_py/sql_text.py`:

- strip `-- ...` and `/* ... */` comments;
- mask single-quoted string literal contents to a placeholder;
- mask double-quoted string literal contents to a placeholder;
- collapse whitespace to single spaces;
- trim leading/trailing whitespace;
- strip trailing semicolons;
- preserve remaining casing and token text;
- hash the normalized string with SHA-256 and truncate to 32 lowercase hex characters.

This intentionally preserves current Python `queryShapeHash` semantics. A future structural-hash v2 can be introduced with a new property or explicit shape version after migration planning.

If no table/column metadata is safe but the operation/source are visible, the adapter may still emit a shape hash only when the resulting fact is useful and not misleading. Otherwise keep `SqlTextUsed` only.

## Golden Fixtures

Add shared fixture cases under `samples/sql-shape-fixtures/` or an equivalent test fixture location. Each adapter should prove the same input SQL produces the same:

- `textHash`;
- `queryShapeHash`;
- `operationName`;
- safe `tableName`/`tableNames`;
- safe `columnNames`/`fieldNames`;
- unsupported/dynamic no-shape behavior.

The fixture contract is the shared behavior; implementation code can be duplicated per runtime rather than forced through generated cross-language code.

## Safe Identifier Policy

Reuse the query-pattern reporting v2 safe identifier policy from `.kiro/specs/query-pattern-reporting-v2/design.md`:

- allow letters, digits, underscore, dot, dash, and limited table-name spaces;
- reject quotes, semicolons, comments, parentheses, operators, braces, brackets, newlines, tabs, URL-like text, and SQL statement keywords as standalone tokens;
- do not split snake_case identifiers on underscore for keyword checks;
- cap table identifiers at 100 chars and column identifiers at 80 chars;
- omit unsafe identifier properties in scanner facts; reporters may hash unsafe values defensively if they encounter them.

Scanner facts should prefer omission over storing unsafe identifier hashes because hashes of unsafe identifier fragments are less useful than safe derived names.
Combined reporters must re-validate identifiers before rendering because scanner bugs should not become report leaks.

## Adapter Plans

### .NET

Candidate extraction points:

- `SqlFileExtractor`
- `CSharpIntegrationSyntaxExtractor`
- Dapper call detection
- ADO.NET `SqlCommand` / command text detection
- EF raw SQL APIs such as `FromSqlRaw`, `ExecuteSqlRaw`, and close equivalents already recognized by existing extractors

Implementation should add a shared .NET helper, for example `SqlShapeExtractor`, used by SQL file and integration extractors.

`SqlTextUsed` emitted from C# code should use the containing method/class/file symbol as `targetSymbol` where available. The old generic sentinel style, such as `sql-string-literal`, is not enough for path and reverse linkage.

Tests should cover:

- `.sql` file `SELECT id, status FROM orders`;
- Dapper or ADO.NET literal `SELECT id, status FROM orders WHERE id = @id`;
- EF raw SQL literal such as `FromSqlRaw("SELECT id, status FROM orders")`;
- dynamic SQL boundary that still emits hash/gap evidence but no shape;
- no raw SQL in facts/report.

### TypeScript

Candidate extraction points:

- direct client calls named `query`, `execute`, `raw`, or adapter-recognized SQL methods;
- tagged template literals from known SQL helpers where static text is visible;
- existing integration extractor paths for Prisma/Base44 should remain query-builder evidence unless direct SQL text is present.

Implementation should avoid turning Prisma `where` objects into SQL-shape facts. Those remain query-builder `QueryPatternDetected` without `sqlSourceKind`.

Direct SQL text detection should use a new documented rule ID, expected to be `typescript.integration.sql.v1`. Existing Prisma/Base44 query-builder facts should keep their current rule IDs unless their evidence behavior changes.

Tests should cover:

- static direct SQL literal;
- static tagged SQL text;
- template literal with expression as dynamic/reduced evidence;
- existing Prisma/Base44 query-builder regression.

### JVM

Candidate extraction points:

- `SqlResourceExtractor`
- `JavaSyntaxExtractor` JDBC/JPA literal detection
- Kotlin syntax extraction if it already recognizes SQL-like literals

Tests should cover:

- `.sql` resource;
- JDBC `prepareStatement("SELECT id, status FROM orders WHERE id = ?")`;
- JPA query literal if supported;
- unsupported/dynamic SQL that does not emit false table/column metadata.

### Python

Python already has the strongest current SQL-shape path. Keep changes minimal:

- align source-kind values;
- preserve current normalized masked SQL text shape-hash normalization as the v1 reference;
- publish/update golden fixture expected values from the Python behavior;
- preserve SQLAlchemy/DB-API tests and report safety tests.

## Combined Surface Normalization

Current combined reporting already has `CombinedDependencySurfaceRow` and recognizes `sql-query`.

This slice should harden the model around:

- `SurfaceKind = sql-query`;
- source label;
- operation;
- table name(s);
- column/field name(s);
- source kind;
- query shape hash;
- text hash;
- evidence tier;
- rule ID;
- safe path and span;
- supporting fact IDs.

Stable key precedence:

1. `sourceLabel + sql-query + queryShapeHash` when present.
2. `sourceLabel + sql-query + operationName + tableName/tableNames + columnNames/fieldNames + sqlSourceKind`.
3. `sourceLabel + sql-query + textHash` for hash-only evidence.
4. `sourceLabel + sql-query + factId hash` only as a review-tier fallback with a documented gap/note.

This intentionally replaces the current table-name-first SQL stable key behavior. Shape hash must win over table name because two distinct queries against the same table can have different columns, operations, and review implications.

Hash-only surfaces must render an explicit `HashOnlyEvidence` caveat and remain review-tier for diff/impact classification. Fact-ID fallback surfaces must render a `VolatileIdentity` or equivalent gap and remain review-tier because the identity is not robust across changed extraction coverage.

Combined Markdown should render SQL details as:

```text
op <operation> table <table-or-n/a> columns <columns-or-n/a> source <source-kind> shape <shape-or-text-hash-or-n/a>
```

It must not render raw SQL, literal values, snippets, URLs, or absolute paths.

## Path and Reverse Semantics

`tracemap paths --to-surface sql-query` succeeds only when a static graph path reaches a SQL surface node.

Important distinctions:

- `SqlTextUsed` attached to a method/file can be a terminal SQL surface if reachable through facts/symbols.
- `DatabaseColumnMapping` can be a terminal persistence surface, but it does not prove a query executes.
- Unreachable SQL facts are reported as `UnlinkedSurface` or equivalent gaps, not successful endpoint paths.
- Reverse query from `sql-query` to endpoints should preserve path classification and reduced-coverage caveats.

## Rule Catalog Updates

Expected rule updates:

- `database.sql.text.v1`
- `database.dapper.invocation.v1`
- `csharp.syntax.querypattern.v1`
- `typescript.integration.querypattern.v1`
- `typescript.integration.sql.v1`
- `jvm.integration.sql.v1`
- `python.integration.sql.v1`
- any new adapter-local rule introduced for direct SQL client detection

No new rule ID should be introduced unless there is genuinely new evidence behavior. Report-only normalization should reuse existing rule IDs.

## Tests

Test layers:

- shared SQL-shape helper tests for supported/unsupported shapes;
- cross-adapter golden fixture tests for Python-compatible `queryShapeHash`;
- adapter extractor tests for `.sql` files and language literals;
- .NET EF raw SQL shape tests;
- report-writer safety tests where rendered SQL surfaces change;
- combined report tests for stable `sql-query` surface projection;
- combined report stable-key collision tests for same table / different shape hash;
- path/reverse tests proving reachable SQL surfaces;
- path/reverse tests proving unlinked SQL facts are gaps, not successful paths;
- reducer/diff/impact tests only where classification behavior changes.

## Validation Commands

Expected validation after implementation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
./scripts/smoke-combined-paths.sh
./scripts/check-private-paths.sh
git diff --check
```

If implementation touches only one adapter in an incremental PR, run that adapter's tests plus shared .NET combined tests for any combined surface behavior.

## Suggested PR Slices

This spec is valuable but broad. Recommended implementation slices:

1. Shared SQL-shape helper + .NET backfill + combined projection tests.
2. TypeScript direct SQL surfaces + query-builder regression tests.
3. JVM SQL-shape backfill.
4. Python alignment only if shared normalization requires it.
5. Combined path/reverse/diff/impact hardening and smoke updates.

Each slice should preserve public report safety and avoid raw SQL output.
