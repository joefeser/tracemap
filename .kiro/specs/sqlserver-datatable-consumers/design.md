# Design — SqlDataAdapter.Fill → DataTable/DataRow column reads
## Reference surface inspected (before implementation)
Signed reference metadata, read with MetadataLoadContext (no code executed):
- `System.Data.SqlClient.SqlDataAdapter` (4.9.0, token b03f5f7f11d50a3a) and
  `Microsoft.Data.SqlClient.SqlDataAdapter` (6.x, token 23ec7fc2d6eaa4a5), both deriving from
  `System.Data.Common.DbDataAdapter`: constructors `()`, `(SqlCommand)`, `(string, SqlConnection)`,
  `(string, string)`; properties `SelectCommand`/`InsertCommand`/`UpdateCommand`/`DeleteCommand`.
- `DbDataAdapter` (System.Data.Common 10.0, b03f5f7f11d50a3a): non-virtual `Fill(DataTable)`,
  `Fill(DataSet, string)`, `Fill(DataSet, int, int, string)`, `Fill(int, int, params DataTable[])`,
  virtual `Fill(DataSet)`; `FillSchema` overloads. `DataAdapter`: `TableMappings`,
  `MissingSchemaAction`, `MissingMappingAction`, `AcceptChangesDuringFill`,
  `AcceptChangesDuringUpdate`, `ContinueUpdateOnError`, `FillLoadOption`,
  `ReturnProviderSpecificTypes`, `FillError`.
- `DataTable`: constructors `()`, `(string)`, `(string, string)`; `Rows`, `Columns`,
  `Constraints`, `PrimaryKey`, `CaseSensitive`, `Locale`, `TableName`, `DefaultView`, ...
- `DataRowCollection`: `Count`, indexer `this[int]` only; enumerator from
  `InternalDataCollectionBase.GetEnumerator()` (non-generic, element `object`).
- `DataRow`: indexers `[string]`, `[int]`, `[DataColumn]` and the three `DataRowVersion`
  variants; `IsNull(string|int|DataColumn|DataColumn, DataRowVersion)`.
- `DataRowExtensions.Field<T>` lives in `System.Data.DataSetExtensions` (token
  b77a5c561934e089), outside the existing trusted ADO.NET assembly set.
## Accepted syntax matrix
S = supported cell. Every other cell is unsupported and makes the whole Fill state `unknown`
with the listed gap (first failing check wins; order below), or an access-level gap where noted.
| Area | Supported (S) | Unsupported → gap |
| --- | --- | --- |
| Fill call | `adapter.Fill(table)` = `DbDataAdapter.Fill(DataTable)` as a standalone statement; C# `_ = adapter.Fill(table);`; VB `Call adapter.Fill(table)` | `Fill(DataSet)`, `Fill(DataSet, string)`, `Fill(DataSet, int, int, string)`, `Fill(int, int, params DataTable[])` → `fill-overload-unsupported`; result used (local, return, condition, argument) → `fill-result-use-unsupported`; FillSchema is a different operation (no state) |
| Adapter | receiver is a fresh local declared `new SqlDataAdapter(command)` (signed System.Data.SqlClient / Microsoft.Data.SqlClient), or `new SqlDataAdapter()` plus exactly one standalone `adapter.SelectCommand = command;` before Fill outside any guard; plain, `using` statement or using declaration; `Dispose()` statement | non-local/parameter/field/other type → `fresh-sqldataadapter-local-required`; not `new SqlDataAdapter` → `fresh-sqldataadapter-required`; string/connection constructors → `adapter-constructor-unsupported`; object initializer → `adapter-initializer-unsupported`; constructor command plus a write, or two writes → `multiple-select-command-writes`; no command → `select-command-unbound`; write after Fill → `select-command-after-fill`; nested/non-statement write → `select-command-write-unsupported`; value not a local → `select-command-local-required`; `TableMappings` → `adapter-table-mappings-unsupported`; `MissingSchemaAction` → `adapter-missing-schema-action-unsupported`; `MissingMappingAction` → `adapter-missing-mapping-action-unsupported`; `AcceptChangesDuringFill` → `adapter-accept-changes-during-fill-unsupported`; `ContinueUpdateOnError` → `adapter-continue-update-on-error-unsupported`; `FillLoadOption` → `adapter-fill-load-option-unsupported`; `ReturnProviderSpecificTypes` → `adapter-provider-specific-types-unsupported`; any other member (including reading `SelectCommand`) → `unsupported-adapter-member`; another Fill of the same table → `repeated-fill`; `FillSchema` → `fill-schema-unsupported`; other executions (Fill of another table, Update) → `adapter-reused`; reassignment → `adapter-reassignment`; any other use → `adapter-alias-or-escape` |
| Command | the #912 rules unchanged, with the adapter association as the cut-off: fresh `new SqlCommand(...)` local, no initializer, inert arguments; standalone unconditional `CommandText` (constant), `CommandType`, `Connection` (local/parameter) writes and `Parameters.Add/AddWithValue` before the association; the association itself (constructor argument or `SelectCommand` value) | every #912 gap (`fresh-sqlcommand-required`, `implicit-receiver-unsupported`, `unsupported-constructor-argument`, `nested-command-assignment`, `conditional-command-assignment`, `unsupported-command-assignment`, `unsupported-command-member`, `command-reassignment`, `command-alias-or-escape`); any write or parameter add after the association → `command-mutation-after-adapter-association`; any execution on the command → `command-executed-outside-fill`; StoredProcedure intent and constant text are still required by the comparison (`text-command-outside-scope`, `stored-procedure-intent-unavailable`, `command-binding-conflict`) |
| Table | fresh local `new DataTable()` (trusted System.Data/System.Data.Common), plain, `using` or using declaration; the selected Fill argument; `table.Rows` read only as noted below; `Dispose()` statement | not a local → `fresh-datatable-local-required`; other creation → `fresh-datatable-required`; `new DataTable(name[, ns])` → `datatable-constructor-unsupported` (TableName participates in adapter table-mapping lookup; deferred); initializer → `datatable-initializer-unsupported`; property write (TableName, CaseSensitive, Locale, PrimaryKey, Namespace, ...) or `Load`, `Merge`, `ImportRow`, `LoadDataRow`, `NewRow`, `Clear`, `Reset`, `AcceptChanges`, `RejectChanges`, `BeginLoadData`, `EndLoadData`, `BeginInit`, `EndInit` → `datatable-mutation`; `Columns`, `Constraints`, `PrimaryKey`, relations → `datatable-schema-access-unsupported`; `Select`, `DefaultView`, `Copy`, `Clone`, `GetChanges`, `CreateDataReader`, `AsEnumerable` and other DataTableExtensions → `ambiguous-row-ownership`; other members → `unsupported-datatable-member`; reassignment → `datatable-reassignment`; argument (including ref/out), return, alias, capture → `datatable-alias-or-escape`; use positioned before the Fill → `table-use-before-fill` |
| Rows | `table.Rows[k]` with constant `k >= 0`; `foreach (DataRow row in table.Rows)` / VB `For Each row As DataRow In table.Rows`; `table.Rows.Count` only as the guard below | nonconstant `k` → `dynamic-row-index-unsupported`; negative → `invalid-row-index`; `Add`, `Remove`, `RemoveAt`, `InsertAt`, `Clear` → `datatable-mutation`; other members → `unsupported-rows-member`; `Rows` as a value → `rows-alias-or-escape`; loop variable not `DataRow` → `foreach-row-type-unsupported` |
| Row | used directly (`table.Rows[k][...]`), through a fresh `DataRow` local initialized directly from `table.Rows[k]` and never written, or the foreach variable | local reassignment → `row-reassignment`; alias/argument/return → `row-alias-or-escape`; indexer write → `datatable-mutation`; `[DataColumn]`/`IsNull(DataColumn...)` → `datacolumn-access-unsupported`; `DataRowVersion` overloads → `row-version-access-unsupported`; `Field<T>` (DataSetExtensions) → `datarow-field-extension-unsupported`; other members (`ItemArray`, `Delete`, `BeginEdit`, `RowState`, `Table`, ...) → `unsupported-row-member` |
| Reads | `row["Name"]`, `row[0]`, VB `row("Name")`, `row(0)` and `row!Name`, `row.IsNull("Name")`, `row.IsNull(0)`; converted integer constants keep the converted value | access-level gaps: nonconstant name → `dynamic-column-name`; non-identifier constant → `column-name-outside-identifier-vocabulary`; nonconstant ordinal → `dynamic-ordinal` |
| Guards / flow | `if (table.Rows.Count > 0) { ... }` (no else) on the selected table; foreach above; using lifetimes | any other if/loop/switch/try/lambda/local function/`?.`/`??`/`&&`/`||`/compound/increment/deconstruction/dynamic/late-bound/with/invalid, guards on another table, reader, adapter or comparison → `unsupported-control-flow`; implicit receiver → `implicit-receiver-unsupported`; bounds 20,000 operations / 256 accesses → `operation-limit` / `access-limit` |
Checks run in this order: Fill overload and statement shape, adapter receiver, table argument,
method-wide control flow, adapter and table construction, implicit receivers (initializers,
VB `With`), adapter members/association, command, table members and position, rows, row
members, accesses.
## Contracts
| Contract | Meaning |
| --- | --- |
| `sqlserver-datatable/1` | Property `sqlServerDataTableState` on every `data-adapter-fill` DatabaseOperationCandidate (the existing Fill fact: same site, rule, tier; no new fact type). Fields: version, coverage (`bounded`/`unknown`), gap, commandType, commandTextHash, adapterAssociation (`constructor-command`/`select-command-property`), adapter/table lines, guard (`none`, `rows-count`, `foreach`, `rows-count+foreach`), accesses. |
| `sqlserver-datatable-bindings/1` | `factId`, `commandText`, four-part `identity`, `dataTableEvidenceSha256` = digest of the contract-tagged DataTable projection of the fact (never equal to the reader or parameter digest). |
| `sqlserver-datatable-comparison/1` | Report under rule `database.sqlserver.datatable-column-comparison.v1`; binds the admitted shape export (version, generator, input, extractor, rule, tier, origin, export digest). |
CLI (compare only; shapes are produced by the existing routes):
`tracemap sqlserver-datatable-result compare --origin result-schema --declarations <json> | --origin ddl-result|ddl-table-result --input <manifest> --source-root <root>`, then
`--schema --bindings --facts --repo --revision --out` → `datatable-column-comparison.json`, `report.md`.
Each origin is admitted by its unchanged route (`SqlServerResultEvidence.Admit`,
`SqlServerDdlResultEvidence.Admit`, `SqlServerDdlTableResultEvidence.Admit`).
## Comparison
Per DatabaseOperationCandidate, reasons (in order): unsupported source extractor/tier;
`datatable-state-unavailable`; state gap; command type; invalid state; `binding-missing`;
`exact-result-shape-unavailable`; `command-binding-conflict`. Then one row per access or one
`no-bounded-column-access` row. Access semantics reuse the #912 engine exactly (exact-case unique
match; `missing-column`/`ordinal-out-of-range` mismatch only against a complete shape;
case-only, duplicate, dynamic and unknown shape remain unknown). DataTable decisions:
- DataTable column lookup falls back to a case-insensitive match at runtime; this is not
  claimed. Case-only stays `column-case-policy-unknown:<name>`.
- Fill renames duplicate result names (e.g. `Name`, `Name1`). When the shape has any
  case-insensitive duplicate, a name absent from the shape is
  `datatable-duplicate-column-renaming-unknown:<name>`, never a mismatch.
- `IsNull` adds `nullability-not-compared:IsNull`; declared types stay facets.
Rows are Tier2Structural for match/mismatch and Tier4Unknown otherwise; source tier is kept.
Bounded input: admitted export, binding `factId`/`dataTableEvidenceSha256`, DataTable
projections of all execution facts, and resolutions. Coverage `partial`, verdict
`no-aggregate-clean-verdict`.
## Extractor versions
C# `csharp-semantic/0.22.14`, VB `vb-semantic/0.10.17` (additive property on Fill facts only).
## Independent matrix (authored before running the extractor)
Fixture `samples/sqlserver-datatable-evidence/{csharp,vb}`, same method names in both languages;
shapes reuse `samples/sqlserver-result-column-evidence` (JSON base, `ddl/`, `ddl-table/`).
Core cells (each run against JSON, constant DDL and table DDL; variants as in #912):
CtorName M; SelectCommandProperty M; DiscardFill M; RowLocalOrdinal M; ForeachRows M,M;
CountGuard M; CountGuardForeach M; IsNullName M; IsNullOrdinal M; BangName M (VB `row!Name`);
UsingLifetime M; WithParameter M; TwoFills#0 M; TwoFills#1 M (ReadOther); ConvertedOrdinal M;
MissingName X; OrdinalOutOfRange X; IsNullMissing X; Renamed X; Reordered M,M; CaseOnly U;
DuplicateDeclared U; DuplicateRenamed U (`Name1`); DynamicName U; DynamicOrdinal U;
NonIdentifierName U; Incomplete U; AbsentBinding U → 28 cases, 30 rows = 18/4/8 per origin.
State cells (table DDL only; each U with the exact state gap asserted on the fact): 64 cases
covering every unsupported row above, unrelated-table/reader/adapter guards and helper Fill.
Per language: 148 case reports, 54 / 12 / 88; both languages 108 / 24 / 176.
