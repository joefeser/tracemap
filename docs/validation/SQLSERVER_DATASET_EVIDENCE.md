# Bounded offline SQL Server SqlDataAdapter.Fill(DataSet) → first-table DataRow evidence
Phase 3 after Phase 2 (#914, merge `f0850183d`). One question inside an explicit static subset:
**does a constant DataRow column-name or ordinal read of the first table of the DataSet filled by
one exact `SqlDataAdapter.Fill(DataSet)` or `Fill(DataSet, constant)` agree with an admitted
first-result shape?** Everything is public synthetic, compiled/parsed and never executed: no SQL
Server, procedure, application method, container or remote host. Every report is `partial` /
`no-aggregate-clean-verdict`. #908 and #870 stay open; this is not DNN or private acceptance.
## Contracts and route
| Contract | Meaning |
| --- | --- |
| `sqlserver-dataset/1` (`sqlServerDataSetState`) | Sibling of `sqlServerDataTableState` on every `data-adapter-fill` DatabaseOperationCandidate (no new fact type). The DataTable property is unchanged and keeps refusing DataSet overloads; the DataSet property refuses `Fill(DataTable)`. C# `csharp-semantic/0.22.15`, VB `vb-semantic/0.10.18`. |
| `sqlserver-dataset-bindings/1` | Exact `factId`, two-part command text, four-part identity and `dataSetEvidenceSha256` (contract-tagged DataSet projection; never equal to the DataTable, reader or parameter digests). |
| `sqlserver-dataset-comparison/1` | Rows under `database.sqlserver.dataset-column-comparison.v1`; binds the admitted shape export (route, version, generator, bounded input, extractor, rule, tier, origin, export SHA-256). |
```sh
tracemap sqlserver-dataset-result compare --origin result-schema --declarations <declarations.json> \
  --schema <result-export.json> --bindings <dataset-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
tracemap sqlserver-dataset-result compare --origin ddl-result|ddl-table-result --input <manifest.json> --source-root <public-root> \
  --schema <ddl-export.json> --bindings <dataset-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```
Shapes are produced and admitted only by the unchanged `sqlserver-result-schema`,
`sqlserver-ddl-result` and `sqlserver-ddl-table-result` routes. The facts input reuses the
DataTable route's explicit 64 MiB bound.
## Accepted syntax (summary; full matrix in the spec design)
[`.kiro/specs/sqlserver-dataset-consumers/design.md`](../../.kiro/specs/sqlserver-dataset-consumers/design.md)
lists every cell with its gap, authored from the signed reference metadata before
implementation (SqlClient 4.9.0 `SqlDataAdapter` is sealed and declares no `Fill`;
`DbDataAdapter.DefaultSourceTableName` is `"Table"`). Supported:
- command and adapter: the Phase 2 rules unchanged (the shared code paths);
- Fill: `adapter.Fill(set)` (`DataAdapter`/`DbDataAdapter.Fill(DataSet)`) or
  `adapter.Fill(set, "Src")` with a constant identifier-vocabulary source table, as a
  statement, C# `_ =`, VB `Call`;
- DataSet: fresh `new DataSet()` (exactly `System.Data.DataSet`), plain/`using`, `Dispose()`;
- first table: `set.Tables[0]` or the exact-case default name (`"Table"`, or the constant source
  table; VB `Tables("Table")`, `Tables!Table`), directly or via a fresh `DataTable` local;
- rows, rows guards, row locals and reads: the Phase 2 cells on that table;
- guards: `if (set.Tables.Count > 0)`, `if (<table>.Rows.Count > 0)` (no else) and foreach.
Unknown (exact gaps): `Fill(DataTable)` (the DataTable route), `Fill(DataSet, int, int, string)`,
dynamic/empty/non-identifier source tables, `Tables[k > 0]`, negative, dynamic, case-only,
other-name and namespace selectors, `Tables.Count` outside the guard, `Tables.Add`/`Remove`/
other members and aliases, `Merge`, `ReadXml`, `Relations`, `EnforceConstraints`,
`CaseSensitive`, other DataSet members, `Copy`/`GetChanges`, typed, named, field or
initializer DataSets, DataSet aliases/escapes/reassignment/use before Fill, every Phase 2
adapter, command, table, rows and row refusal, and every other control flow.
## Decisions
- Case-only table names stay `dataset-table-name-case-unknown` although
  `DataTableCollection[string]` falls back case-insensitively at runtime (same policy as columns).
- Later result sets stay unknown; an admitted complete first-result shape has at least one
  column, so the first result creates the first DataSet table.
- `new DataTable(name)` and `Field<T>` stay deferred; the trusted assembly set is unchanged. The
  net10.0 reference pack forwards `DataRowExtensions` to System.Data.Common, but .NET Framework
  targets keep it in System.Data.DataSetExtensions (b77a5c561934e089), and `Field<T>` adds
  conversion/DBNull semantics this slice does not compare.
- `new DataSet(name)` stays unsupported although DataSetName does not take part in mapping.
## Comparison
The Phase 2 DataRow access decision is reused unchanged (extracted into a shared helper): exact-
case unique name = bounded match; absent name or out-of-range ordinal = mismatch only against a
complete shape; case-only, duplicate renaming (`datatable-duplicate-column-renaming-unknown`),
dynamic, unknown shape and unbound Fill = unknown; `IsNull` adds `nullability-not-compared:IsNull`.
Source tier and extractor are preserved. Bounded input hashes cover the admitted public export,
binding `factId`/`dataSetEvidenceSha256`, the DataSet projections of every execution fact and
resolutions.
## Independent matrix
Fixtures: `samples/sqlserver-dataset-evidence/{csharp,vb}` (96 methods / 99 Fill sites per
language, same names; VB `BangName` uses `Tables!Table` and `row!Name`), wired into
`TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"` references so a clean checkout
restores and builds them through the normal test build. Shapes reuse
`samples/sqlserver-result-column-evidence`. The native smoke
(`scripts/sqlserver-dataset-evidence/smoke.py`) and `SqlServerDataSetEvidenceTests` carry the
same expectations, written before the extractor ran.
- 35 core cells × 3 origins: 38 rows per origin = 26 / 4 / 8.
- 64 state cells against the table DDL origin: each one unknown row with the exact state gap
  asserted on the fact first, plus the sibling DataTable state (bounded only for the
  `Fill(DataTable)` cell, `fill-overload-unsupported` everywhere else).
- Per language **169 case reports, 178 rows = 78 bounded matches / 12 mismatches / 88
  unknowns**; both languages 156 / 24 / 176.
24 refusal checks: DataTable, reader and parameter bindings; DataTable, reader, parameter and
stale digest substitution; duplicate binding; stale revision; mutated source facts; the four
origin cross-checks; unknown origin; origin/option mismatch; mutated declarations; mutated table
export; stale table digest; symlinked DDL source; DataSet bindings in the DataTable and reader
routes; oversized bindings; existing output preserved.
## Validation commands
```sh
dotnet build src/dotnet/TraceMap.sln -c Debug -warnaserror   # restores every SQL fixture through the test project graph
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerDataSetEvidenceTests|FullyQualifiedName~SqlServerDataTableEvidenceTests|FullyQualifiedName~SqlServerDdlTableResultEvidenceTests|FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~SqlServerDdlRuleCatalogTests|FullyQualifiedName~DnnSqlValidationTests'
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
python3 scripts/sqlserver-dataset-evidence/smoke.py --output /tmp/dataset-smoke-new
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic scripts/smoke-open-source-repos.sh <cache> <out>
./scripts/check-private-paths.sh
git diff --check
```
## Non-claims and remaining unknowns
Not proven: that Fill executes, that any table or row exists or how many, later result sets,
values, CLR/SQL types and conversions, nullability, runtime exceptions, schema mapping beyond a
fresh adapter's defaults, catalog/live/deployed correspondence. Outside scope or unknown: typed
and named DataSets, later tables, relations, `Field<T>`, `DataColumn`/`DataRowVersion` access,
reader-returning helpers, ORMs, DNN hydration and provider wrappers, Microsoft.Data.SqlClient
fixtures (the subset admits its signed types, but no public fixture exercises them here). This
is not a SQL interpreter or a runtime model of `DataAdapter`/`DataSet`.
## Follow-up work log
Bounded backlog, not an authorization to widen this branch. Related tracking: #870, #908,
#864 / #865.
1. **Next slice:** see the [next-slice prompt](../../prompts/continue-sqlserver-consumer-helpers.md).
2. **Deferred decisions:** `new DataTable(name)` / `new DataSet(name)`, `Field<T>` (trusted
   set), later result sets, DataColumn and DataRowVersion access, Microsoft.Data.SqlClient
   public fixtures.
3. **Deferred engineering work:** test wall-clock restructuring stays deferred; measure before
   changing project or concurrency boundaries.
