# Bounded offline SQL Server SqlDataAdapter.Fill → DataTable/DataRow evidence
Phase 2 after Phase 1 (#913, merge `4f664daf5`). One question inside an explicit static subset:
**does a constant DataRow column-name or ordinal read of the DataTable filled by one exact
`SqlDataAdapter.Fill(DataTable)` agree with an admitted first-result shape?** Everything is
public synthetic, compiled/parsed and never executed: no SQL Server, procedure, application
method, container or remote host. Every report is `partial` / `no-aggregate-clean-verdict`.
#908 and #870 stay open; this is not DNN or private acceptance.
## Contracts and route
| Contract | Meaning |
| --- | --- |
| `sqlserver-datatable/1` (`sqlServerDataTableState`) | Source consumer state on every `data-adapter-fill` DatabaseOperationCandidate (the existing Fill fact; no new fact type). Separate from `sqlServerReaderState` and `sqlServerParameterState`. Introduced at C# `csharp-semantic/0.22.14`, VB `vb-semantic/0.10.17`; current C# 0.22.15 / VB 0.10.18 add only the sibling [DataSet state](SQLSERVER_DATASET_EVIDENCE.md), DataTable state unchanged. |
| `sqlserver-datatable-bindings/1` | Exact `factId`, two-part command text, four-part identity and `dataTableEvidenceSha256` (digest of a contract-tagged DataTable projection; never equal to the reader or parameter digests). |
| `sqlserver-datatable-comparison/1` | Rows under `database.sqlserver.datatable-column-comparison.v1`; binds the admitted shape export (route, version, generator, bounded input, extractor, rule, tier, origin, export SHA-256). |
```sh
tracemap sqlserver-datatable-result compare --origin result-schema --declarations <declarations.json> \
  --schema <result-export.json> --bindings <datatable-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
tracemap sqlserver-datatable-result compare --origin ddl-result|ddl-table-result --input <manifest.json> --source-root <public-root> \
  --schema <ddl-export.json> --bindings <datatable-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```
Shapes are produced and admitted only by the unchanged `sqlserver-result-schema`,
`sqlserver-ddl-result` and `sqlserver-ddl-table-result` routes (regenerate-and-compare); each
origin reads only its own manifest/export version. Those routes and their reader bindings,
outputs and denominators are unchanged. The facts input has its own 64 MiB bound because a
whole scan's `facts.ndjson` exceeds the 4 MiB public-artifact bound.
## Accepted syntax (summary; full matrix in the spec design)
[`.kiro/specs/sqlserver-datatable-consumers/design.md`](../../.kiro/specs/sqlserver-datatable-consumers/design.md)
lists every supported and unsupported cell with its gap, authored from the signed reference
metadata before implementation. Supported:
- command: the #912 rules unchanged (fresh signed `SqlCommand` local, constant text,
  StoredProcedure, standalone unconditional writes, `Parameters.Add/AddWithValue`) with the
  adapter association as the cut-off; any later write/parameter add or any execution on the
  command is unknown;
- adapter: fresh `new SqlDataAdapter(command)`, or `new SqlDataAdapter()` plus exactly one
  standalone `adapter.SelectCommand = command;` before Fill; plain/`using`; `Dispose()`;
- Fill: `adapter.Fill(table)` (`DbDataAdapter.Fill(DataTable)`) as a statement, C# `_ =`, VB `Call`;
- table: fresh `new DataTable()` never reassigned, aliased, escaped, mutated or read before Fill;
- rows: `table.Rows[k]` (constant `k >= 0`) directly or via a fresh `DataRow` local, and
  `foreach (DataRow row in table.Rows)` / VB `For Each row As DataRow In table.Rows`;
- reads: `row["Name"]`, `row[0]`, VB `row("Name")`, `row!Name`, `row.IsNull("Name")`, `row.IsNull(0)`;
- guards: `if (table.Rows.Count > 0)` without else and foreach, both on the selected table.
Unknown (exact gaps): `Fill(DataSet[, string])`, `Fill(DataSet, int, int, string)`,
`Fill(int, int, params DataTable[])`, Fill results used, FillSchema, string/connection
adapter constructors, initializers, double/late/nested SelectCommand writes, `TableMappings`,
`MissingSchemaAction`, `MissingMappingAction`, `AcceptChangesDuringFill`,
`ContinueUpdateOnError`, `FillLoadOption`, `ReturnProviderSpecificTypes`, events, adapter
reuse/alias/reassignment, repeated Fill, `new DataTable(name)`, table/rows/row mutation,
`Columns`/`Constraints`/`PrimaryKey`, `Select`/`DefaultView`/`Copy`/`GetChanges`/LINQ,
`Field<T>`, `DataColumn` and `DataRowVersion` overloads, dynamic row index, row aliases,
helpers, guards on another table/reader/adapter and every other control flow.
## Comparison
The #912 column engine is reused (exact-case unique name = bounded match; absent name or
out-of-range ordinal = mismatch only against a complete shape; case-only, duplicate,
dynamic, unknown shape and unbound Fill = unknown). DataTable decisions:
- DataTable lookup falls back to a case-insensitive match at runtime; not claimed, so case-only
  stays `column-case-policy-unknown:<name>`.
- Fill renames duplicate result names (`Name`, `Name1`); with any case-insensitive duplicate in
  the shape an absent name is `datatable-duplicate-column-renaming-unknown:<name>`.
- `IsNull` adds `nullability-not-compared:IsNull`; declared types stay uncompared facets.
Source tier and extractor are preserved; structural correlation never upgrades them.
Bounded input hashes cover the admitted public export, binding `factId` /
`dataTableEvidenceSha256`, the DataTable projections of every execution fact and resolutions.
## Independent matrix
Fixtures: `samples/sqlserver-datatable-evidence/{csharp,vb}` (89 methods / 92 Fill sites per
language, same names; VB `BangName` uses `row!Name`). Shapes reuse
`samples/sqlserver-result-column-evidence` (JSON base, `ddl/`, `ddl-table/`). The native smoke
(`scripts/sqlserver-datatable-evidence/smoke.py`) and `SqlServerDataTableEvidenceTests` carry
the same expectations, written before the extractor ran.
- 28 core cells × 3 origins (JSON, constant DDL, table DDL; renamed / reordered / duplicate /
  incomplete variants): 30 rows per origin = 18 / 4 / 8.
- 64 state cells against the table DDL origin: each one unknown row with the exact state gap
  asserted on the fact first.
- Per language **148 case reports, 154 rows = 54 bounded matches / 12 mismatches / 88
  unknowns**; both languages 108 / 24 / 176. Every report also keeps the other Fill and
  execution facts as `binding-missing` unknown rows, excluded from the denominator.
22 refusal checks: reader and parameter bindings, reader/parameter/stale digest substitution,
duplicate binding, stale revision, mutated source facts, table export as JSON schema, constant
export as table schema, table manifest in the constant origin and vice versa, unknown origin,
origin/option mismatch, mutated declarations, mutated table export, stale table digest,
symlinked DDL source, DataTable bindings in the reader and table routes, oversized bindings,
existing output preserved.
## Validation commands
```sh
for p in samples/sqlserver-{parameter,result-column}-evidence/{csharp,vb}/*.*proj samples/sqlserver-datatable-evidence/{csharp,vb}/*.*proj; do dotnet restore "$p"; done
dotnet build src/dotnet/TraceMap.sln -c Debug -warnaserror
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerDataTableEvidenceTests|FullyQualifiedName~SqlServerDdlTableResultEvidenceTests|FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~SqlServerDdlRuleCatalogTests|FullyQualifiedName~DnnSqlValidationTests'
python3 -m unittest discover -s scripts/validation -p 'test_sqlserver_*.py' -v
python3 -m unittest discover -s scripts/validation -p test_dnn_sql_harness.py -v
# Commit, then freeze source/HEAD/build outputs; no concurrent build/smoke/commit.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Sequentially, at the committed head, in new directories:
python3 scripts/sqlserver-parameter-evidence/smoke.py --output /tmp/json-smoke-new
python3 scripts/sqlserver-ddl-parameter-evidence/smoke.py --json-smoke /tmp/json-smoke-new --output /tmp/ddl-smoke-new
python3 scripts/sqlserver-result-column-evidence/smoke.py --output /tmp/result-smoke-new
python3 scripts/sqlserver-table-result-evidence/smoke.py --output /tmp/table-smoke-new
python3 scripts/sqlserver-datatable-evidence/smoke.py --output /tmp/datatable-smoke-new
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic scripts/smoke-open-source-repos.sh <cache> <out>
./scripts/check-private-paths.sh
git diff --check
```
## Non-claims and remaining unknowns
Not proven: that Fill executes, that rows exist or how many, values, CLR/SQL types and
conversions, nullability, runtime exceptions, later result sets, schema mapping beyond a
fresh adapter's defaults, catalog/live/deployed correspondence. Outside scope or unknown:
DataSet, named tables, `Field<T>`, `DataColumn`/`DataRowVersion` access, reader-returning
helpers, ORMs, DNN hydration and provider wrappers, multiple result sets, Microsoft.Data.SqlClient
fixtures (the subset admits its signed types, but no public fixture exercises them here).
This is not a SQL interpreter or a runtime model of `DataAdapter`.
## Follow-up work log
Bounded backlog, not an authorization to widen this branch. Related tracking: #870 (schema
contracts), #908 (DNN validation), #864 / #865 (extraction coverage).
1. **Next slice:** `Fill(DataSet)` to single-table DataRow reads; see the
   [Phase 3 prompt](../../prompts/continue-sqlserver-dataset-consumers.md), implemented in
   [DataSet evidence](SQLSERVER_DATASET_EVIDENCE.md).
2. **Deferred decisions:** `new DataTable(name)` (table-mapping interaction), `Field<T>`
   (would widen the trusted assembly set to System.Data.DataSetExtensions), DataColumn and
   DataRowVersion access, Microsoft.Data.SqlClient public fixtures.
3. **Deferred engineering work:** test wall-clock restructuring stays deferred; the full gate
   took 37m 43s at `f4f74f01c` (5,136 tests). Measure before changing project or concurrency
   boundaries; preserve denominators, isolation and frozen build provenance.
## Validated implementation head — 2026-10-26
Implementation commit **`f4f74f01cc92867f3542c41ee110a0378d6d9145`** on
`codex/sqlserver-datatable-consumers`, from fresh `origin/dev` = #913 merge `4f664daf5`.
Recorded by a separate documentation-only commit; no implementation change follows the gates.
| Gate | Result |
| --- | --- |
| Focused Debug -warnaserror (DataTable + table + result + DDL + parameter + catalog + DNN + Razor + SQL matrix + VB data boundary) | 581 passed, 0 failed/skipped (59 new) |
| Python guards | 24 `test_sqlserver_*` (6 new) + 17 DNN harness passed |
| Full Debug solution -warnaserror, frozen source/HEAD/outputs | 5,136 passed, 0 failed, 3 expected skips; 37m 43s (2,274 s wall); no warnings |
| Retained JSON / DDL 52-case parameter smokes | 8 / 12 / 32 each |
| Retained result smoke | JSON 36 / 10 / 36, DDL 36 / 10 / 58, 20/20 refusals |
| Retained table smoke | 64 cases per language; 44 / 10 / 88, 24/24 refusals |
| New DataTable smoke | 148 cases per language; 108 / 24 / 176 (308 rows), expected = observed, 0 gaps, 22/22 refusals, repeat bytes identical |
| Pinned C#/VB OSS adapter smoke | exit 0; 12,949 / 114,806 facts at C# 0.22.14 / VB 0.10.17; reduced coverage, no SQL calls |
| Private-path guard, `git diff --check` | passed |
Receipts are local (outside Git): `/tmp/tracemap-dt-f4f74f0-{json,ddl,result,table,datatable}/smoke-receipt.json`.
DataTable smoke generator `08946e0d0266cd688240deff2a8b4ba18e52a0ffc6f18672b5cdd83ecf7fa7dc`,
bounded input `3bfeca89d6a36fdad3af9ed778d61a9e0551ebd30667212f1f56334f630d7d9b`; Core
`4a43b45da23025fbfb1a674c525c9f2332795e5255e3cc9ce4deb06e6727d631`; parser unchanged
`2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
Coverage honesty: the pinned public repositories contain no SQL calls, so DataTable evidence is
exercised only by the synthetic corpus and direct tests. Not DNN, private, catalog or deployed
acceptance.
