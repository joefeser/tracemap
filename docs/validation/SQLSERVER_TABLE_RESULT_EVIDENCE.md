# Bounded offline SQL Server table-backed result shapes

Phase 1 after #912 (`fc14857a54044e8edfaea170dab3b01141c0842a`). One question inside an
explicit static subset: **does a selected reader's column-name or ordinal access agree with
the first-result projection of a procedure that selects explicit columns from one admitted
table?** Everything is public synthetic, parsed and never executed: no SQL Server, procedure,
application method, container or remote host. Every report is `partial` /
`no-aggregate-clean-verdict`. #908 and #870 stay open; this is not DNN or private acceptance.

## Contracts and route

| Contract | Meaning |
| --- | --- |
| `sqlserver-ddl-table-result-input/1` | Authored manifest; same fields, path, digest and identity policy as `sqlserver-ddl-input/1`. |
| `sqlserver-ddl-table-result-schema/1` | Export: input, Core generator / parser / bounded-input SHA-256, `tables`, `declarations`, `projections`, `tableUnsupported`, `unsupported`. Extractor `sqlserver-ddl-table-result-shapes/1`, rule `database.sqlserver.ddl-table-result-shape.v1`; Tier2Structural complete tables and shapes, Tier4Unknown unsupported records. |
| `sqlserver-ddl-table-result-declarations/1` | Shape projection (origin `public-synthetic-tsql-ddl-table-result`) consumed by the unchanged #912 comparison engine. |
| `sqlserver-ddl-table-result-comparison/1` | Wrapper binding the admitted export and the inner `sqlserver-ddl-table-result-column-comparison/1` rows (`database.sqlserver.result-column-comparison.v1`). |

```sh
tracemap sqlserver-ddl-table-result produce --input <manifest.json> --source-root <public-root> --out <new.json>
tracemap sqlserver-ddl-table-result compare --input <manifest.json> --source-root <public-root> --schema <export.json> \
  --bindings <result-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```

Source evidence is the existing `sqlserver-reader/1` property and `sqlserver-result-bindings/1`
(exact `factId` + `readerEvidenceSha256`); no extractor changed, so C# `csharp-semantic/0.22.13`
and VB `vb-semantic/0.10.16` are unchanged. The parameter route and the constant result route
still reject `CREATE TABLE` (`top-level-statement-unsupported`); manifests, exports and
bindings never cross routes (version, origin and regeneration checks).

## Admission

`SqlServerDdlEvidence.ProduceCore` runs unchanged (strict UTF-8, exact file digests, raw-text
SQLCMD guard, 64 MiB parser thread, size/token/complexity bounds, parameter vocabulary,
malformed SQL) with one opt-in table visitor. In this route only, `CREATE TABLE` is admitted when
its name is two-part identifier vocabulary; one-part, `#temp` and three-part names reject the
whole input. Tables and procedures share one case-insensitive identity set, so duplicate,
case-only duplicate and table/procedure collisions reject (`ambiguous-object`). At most 256
tables. `ALTER TABLE`, `CREATE VIEW`, `DROP`, `CREATE INDEX` and every other statement still
reject. The admitted files are one unordered definition set: analysis starts after all files are
read, and no migration ordering, USE/connection inference or deployment is claimed.

### Table grammar

Columns: identifier name; built-in one-part type with at most two integer/MAX facets;
optional `NULL`/`NOT NULL`, `IDENTITY(...)`, column `PRIMARY KEY`/`UNIQUE`
(`CLUSTERED`/`NONCLUSTERED` allowed). Table constraints: `PRIMARY KEY`/`UNIQUE`. Keys with
index options or placement, and anything else, make the table `unknown` (Tier4 record, no
columns): `table-option-unsupported` (filegroups, WITH options, inline indexes, periods,
node/edge/file tables), `table-constraint-unsupported`, `computed-column-unsupported`,
`column-default-unsupported`, `column-collation-unsupported`, `column-type-unsupported`
(alias/CLR/xml), `column-constraint-unsupported`, `column-option-unsupported`,
`column-name-outside-identifier-vocabulary`, `duplicate-or-case-only-column-unsupported`,
`column-limit` (1,024). An unknown table never backs a shape.

### Procedure grammar

The #912 body checks are reused (one statement, optionally the only statement of one
BEGIN/END; IF, WHILE, TRY, EXEC/`sp_executesql`, RETURN, nested blocks, several statements
stay unknown). Without FROM, the #912 constant-projection rules apply unchanged. With FROM,
a complete shape requires all of:

- no CTE (`cte-unsupported`), INTO/hints/OPTION (`select-clause-unsupported`), set operation,
  `*`/`t.*`, SELECT assignment, WHERE/GROUP/HAVING/ORDER/TOP/OFFSET/FOR/WINDOW/DISTINCT;
- exactly one source (`multiple-table-sources-unsupported`) that is a plain named table:
  joins/APPLY (`join-unsupported`), derived tables (`subquery-unsupported`), table variables,
  functions, other references, hints, TABLESAMPLE/FOR SYSTEM_TIME are unknown;
- explicit two-part schema name (`table-reference-schema-required`; `#`/`##` →
  `temporary-table-unsupported`; server/database parts →
  `cross-database-or-server-reference-unsupported`, including the manifest's own database);
- exact ordinal resolution in the admitted table set (`table-reference-case-policy-unknown`
  for case-only matches, `table-dependency-missing` for views, procedures, wrong schemas and
  absent tables; `table-definition-unsupported` for unknown tables);
- every element a regular column reference without COLLATE (`subquery-unsupported`,
  `non-column-projection-unsupported`), optional identifier output alias (`AS x`, `x = col`,
  `col x`; `string-alias-unsupported`, `alias-outside-identifier-vocabulary`), qualifier
  absent or exactly the one simple table alias (`column-qualifier-without-alias-unsupported`,
  `column-qualifier-case-policy-unknown`, `column-qualifier-unbound`,
  `column-qualifier-unsupported`), column resolved exactly (`column-case-policy-unknown`,
  `column-dependency-missing`);
- unique (case-insensitive) output names (`duplicate-or-case-only-output-name-unsupported`).

A complete shape keeps output names in projection order with element lines, and the backing
table column's type spelling as an uncompared facet; `projections` records the table and the
source column per output. Unknown shapes have no columns and a Tier4 record at the offending
span. No body text, table alias or literal value is stored.

## Comparison

The #912 engine is reused unchanged: exact-case unique name = bounded match; absent name or
out-of-range ordinal = mismatch only against a complete shape; case-only, duplicate, dynamic,
unknown shape or unbound execution = unknown. The shape is the projection, not the table: a
column that exists in the table but is not selected is `missing-column`, and ordinal bounds use
the projection width. Bounded input hashes cover only the admitted public export, the
reader projections and binding `factId`/`readerEvidenceSha256`.

## Independent matrix

Templates: `samples/sqlserver-result-column-evidence/ddl-table/`. The native smoke
(`scripts/sqlserver-table-result-evidence/smoke.py`) and `SqlServerDdlTableResultEvidenceTests`
carry the same independently authored expectations, written before running the extractor.

- Replay: the 34 #912 sites per language against `tables.sql` + `base.sql` (`SELECT Id, Name,
  Total FROM dbo.Items`; the table also has Price and ItemCode) or the renamed / reordered /
  duplicate / IF variants; the #912 per-case expectations apply unchanged: 41 rows =
  18 / 5 / 18.
- Additional NameMatch cases: table alias, output aliases (`ItemCode AS Name`), bracketed and
  double-quoted identifiers → match; OrdinalOutOfRange `GetString(3)` against a four-column
  projection → match. Unknown: no table file, view, missing column, wrong schema, other
  database, unqualified, case-only table and column, case-only output, unbound and alias-less
  qualifiers, `*`, join, CTE, derived table, UNION, temporary table, dynamic SQL, EXEC, IF,
  two SELECTs, WHERE, function expression, table hint, computed-column and case-only-column
  table definitions (26).
- Per language **64 cases, 71 rows = 22 bounded matches / 5 mismatches / 44 unknowns**; both
  languages 44 / 10 / 88 (142 rows). Every report also keeps the other 33 executions as
  `binding-missing` unknown rows, excluded from the denominator. This is a separate
  denominator: the retained JSON 36/10/36, DDL 36/10/58 and 52-case 8/12/32 smokes are
  unchanged and run as before.

24 refusal checks: stale table digest, duplicate and case-only table definitions,
table/procedure collision, ALTER TABLE, CREATE VIEW, unqualified and temporary definitions,
malformed SQL, SQLCMD in a comment, symlinked source, constant and parameter manifest versions,
table manifest in the constant and parameter routes, constant export as table schema, mutated
shape and table columns in the export, stale export after a table change, stale revision,
stale reader digest, parameter bindings, table export in the constant route, existing output
preserved. Each must exit non-zero and publish nothing.

## Validation commands

```sh
dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerDdlTableResultEvidenceTests|FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~SqlServerDdlRuleCatalogTests|FullyQualifiedName~DnnSqlValidationTests'
python3 -m unittest discover -s scripts/validation -p 'test_sqlserver_*.py' -v
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
# Commit, then freeze source/HEAD/build outputs; no concurrent build/smoke/commit.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Sequentially, at the committed head, in new directories (fixture projects must be restored):
python3 scripts/sqlserver-parameter-evidence/smoke.py --output /tmp/json-smoke-new
python3 scripts/sqlserver-ddl-parameter-evidence/smoke.py --json-smoke /tmp/json-smoke-new --output /tmp/ddl-smoke-new
python3 scripts/sqlserver-result-column-evidence/smoke.py --output /tmp/result-smoke-new
python3 scripts/sqlserver-table-result-evidence/smoke.py --output /tmp/table-smoke-new
./scripts/check-private-paths.sh
git diff --check
```

## Non-claims and remaining unknowns

Not proven: CLR/SQL types, nullability, keys, defaults, row existence or cardinality, values,
execution, runtime exceptions, later result sets, catalog/live/deployed correspondence or
migration order. Explicitly unknown or absent: views, joins, CTEs, subqueries, UNION, WHERE and
other clauses, expressions, temporary tables, table variables, functions, dynamic SQL, EXEC,
conditional/multiple result sets, ALTER/migration scripts, DataTable/DataSet, adapters, ORMs,
DNN hydration and provider wrappers. This is not a SQL interpreter.

## Validated implementation head — 2026-10-26

Implementation commit **`c28dbd3df38ee80da684914677829dfee9a35644`** on
`codex/sqlserver-table-result-shapes`, from fresh `origin/dev` = #912 merge `fc14857a5`.
Recorded by a separate documentation-only commit; no implementation change follows the gates.

| Gate | Result |
| --- | --- |
| Focused Debug -warnaserror (table + result + DDL + parameter + catalog + DNN classes) | 495 passed, 0 failed/skipped (122 new) |
| Python guards | 15 `test_sqlserver_*` (5 new) + 17 DNN harness passed |
| Full Debug solution -warnaserror, frozen source/HEAD/outputs | 5,077 passed, 0 failed, 3 expected skips; 34m 4s (2,057 s wall); no warnings |
| Retained JSON / DDL 52-case parameter smokes | 8 / 12 / 32 each |
| Retained result smoke | JSON 36 / 10 / 36, DDL 36 / 10 / 58, 20/20 refusals |
| New table smoke | 64 cases per language; 44 / 10 / 88 (142 rows), expected = observed, 0 gaps, 24/24 refusals, repeat bytes identical |
| Private-path guard, `git diff --check` | passed |

Receipts are local (outside Git): `/tmp/tracemap-tr-c28dbd3-{json,ddl,result,table}/smoke-receipt.json`.
Table smoke generator `aff4dd7ef885e207923dd52034e36ee58c5f600348397356314ee95de417d080`, bounded
input `4b7eec018407f86876865670b4726e894a1f9ecf9338140195aeed96394652c6`; Core
`dce5cfff0c3f008249ea722b638478594678acbe3ae6071cabb7ad691281a8bd`; parser unchanged
`2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.

Coverage honesty: no source extractor changed, so the pinned OSS adapter smoke was not rerun;
those public repos contain no SQL calls and would not exercise this route. Table-backed shapes
are exercised only by the synthetic corpus and direct tests. Not DNN, private, catalog or
deployed acceptance. Follow-up (Phase 2):
[continue-sqlserver-datatable-consumers.md](../../prompts/continue-sqlserver-datatable-consumers.md).
