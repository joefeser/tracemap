# Design — SqlDataAdapter.Fill(DataSet) → first-table DataRow column reads
## Reference surface inspected (before implementation)
Signed reference metadata read with MetadataLoadContext (no code executed): System.Data.SqlClient
4.9.0 `lib/net8.0` (token b03f5f7f11d50a3a) and the net10.0 reference pack (System.Data.Common,
token b03f5f7f11d50a3a).
- `SqlDataAdapter` is **sealed**, derives from `DbDataAdapter` and declares no `Fill` member:
  constructors `()`, `(SqlCommand)`, `(string, SqlConnection)`, `(string, string)` only.
- `DataAdapter.Fill(DataSet)` is `virtual newslot`; `DbDataAdapter.Fill(DataSet)` overrides it
  (`virtual`, not newslot). Non-virtual `DbDataAdapter.Fill(DataSet, string)`,
  `Fill(DataSet, int, int, string)`, `Fill(DataTable)`, `Fill(int, int, params DataTable[])`.
  Because the receiver type is the sealed `SqlDataAdapter`, a call bound to either
  `DataAdapter.Fill(DataSet)` or `DbDataAdapter.Fill(DataSet)` dispatches to
  `DbDataAdapter.Fill(DataSet)`.
- `DbDataAdapter.DefaultSourceTableName` = `"Table"` (the source table of the first result for
  `Fill(DataSet)`); `Fill(DataSet, srcTable)` uses `srcTable`. With an empty `TableMappings` and
  the default `MissingMappingAction.Passthrough`, the DataSet table name equals the source table
  name; later results get `"Table1"`, `"Table2"`, ... (`srcTable1`, ...).
- `DataSet`: constructors `()`, `(string)`; `Tables`, `Relations`, `CaseSensitive`,
  `EnforceConstraints`, `DataSetName`, `Locale`, `Namespace`, `Prefix`, `ExtendedProperties`,
  `DefaultViewManager`, `RemotingFormat`, `SchemaSerializationMode`, `HasErrors`,
  `IsInitialized`, `Site`; `Merge`(7), `ReadXml`(8), `ReadXmlSchema`, `InferXmlSchema`,
  `Load`(3), `Clear`, `Reset`, `AcceptChanges`, `RejectChanges`, `BeginInit`, `EndInit`,
  `Copy`, `Clone`, `GetChanges`, `CreateDataReader`, `GetXml`, `GetXmlSchema`, `HasChanges`,
  `WriteXml`, `WriteXmlSchema`; events `Initialized`, `MergeFailed`.
- `DataTableCollection` (sealed): indexers `[int]`, `[string]`, `[string, string]`; `Add`(4),
  `AddRange`, `Remove`(3), `RemoveAt`, `Clear`, `Contains`(2), `IndexOf`(3), `CanRemove`,
  `CopyTo`, events; `Count` and the non-generic enumerator from `InternalDataCollectionBase`.
- `System.Data.DataSetExtensions` (token b77a5c561934e089) in the net10.0 reference pack only
  forwards `DataRowExtensions`/`DataTableExtensions` to System.Data.Common.
## Decisions
- **Selectors.** `set.Tables[0]` and the exact-case constant name the fresh adapter creates for
  the first result (`"Table"` for `Fill(DataSet)`, the constant `srcTable` for
  `Fill(DataSet, srcTable)`; VB `set.Tables("Table")` and `set.Tables!Table`) are admitted. At
  runtime `DataTableCollection[string]` falls back to a case-insensitive match; this is not
  claimed (consistent with the column policy), so a case-only name is
  `dataset-table-name-case-unknown`. Any other exact name is `dataset-table-name-unbound` (it
  may name a later result, e.g. `"Table1"`, or nothing).
- **First result only.** `Tables[k]` with constant `k > 0` is `dataset-later-table-unsupported`;
  later result sets stay unknown.
- **Fresh untyped DataSet only.** The local and the creation must be exactly
  `System.Data.DataSet` (typed DataSets carry pre-declared tables); `new DataSet(name)` stays
  unsupported (`dataset-constructor-unsupported`, deferred though DataSetName does not take
  part in table mapping); initializers are unsupported.
- **Deferrals kept.** `new DataTable(name)` stays a Phase 2 refusal; `Field<T>` stays
  `datarow-field-extension-unsupported`. Admitting it would need its own decision: on .NET
  Framework it lives in System.Data.DataSetExtensions (b77a5c561934e089, outside the trusted
  set); the net10.0 pack forwards it to System.Data.Common; it also adds conversion/DBNull
  semantics this slice does not compare. The trusted assembly set is unchanged.
- **Sibling property.** `sqlServerDataSetState` is added next to `sqlServerDataTableState` on
  every `data-adapter-fill` fact; the Phase 2 property, its digest projection and its Fill
  overload refusal for DataSet calls are unchanged. Each property names only its own overloads
  (`Fill(DataTable)` facts carry DataSet state `fill-overload-unsupported`).
- **Shared rules.** The adapter, command, table-member, rows, row and access rules are the
  Phase 2 code paths, extracted behavior-preservingly into shared helpers before any DataSet
  code (the Phase 2 lane and smoke must not change).
## Accepted syntax matrix
S = supported cell. Every other cell is unsupported and makes the whole DataSet state `unknown`
with the listed gap (first failing check wins; order below), or an access-level gap where noted.
| Area | Supported (S) | Unsupported → gap |
| --- | --- | --- |
| Fill call | `adapter.Fill(set)` bound to `DataAdapter.Fill(DataSet)` or `DbDataAdapter.Fill(DataSet)`; `adapter.Fill(set, "Src")` = `DbDataAdapter.Fill(DataSet, string)` with a constant identifier-vocabulary `srcTable`; standalone statement, C# `_ = ...`, VB `Call ...` | `Fill(DataTable)`, `Fill(DataSet, int, int, string)`, `Fill(int, int, params DataTable[])` → `fill-overload-unsupported`; result used → `fill-result-use-unsupported`; nonconstant `srcTable` → `dynamic-source-table-unsupported`; empty/null → `invalid-source-table`; other constant → `source-table-outside-identifier-vocabulary` |
| Adapter | Phase 2 cells unchanged (fresh local `new SqlDataAdapter(command)` or `()` plus one `SelectCommand` write; `using`; `Dispose()`) | every Phase 2 adapter gap unchanged (`fresh-sqldataadapter-local-required`, `fresh-sqldataadapter-required`, `adapter-constructor-unsupported`, `adapter-initializer-unsupported`, `multiple-select-command-writes`, `select-command-unbound`, `select-command-after-fill`, `select-command-write-unsupported`, `select-command-local-required`, `adapter-table-mappings-unsupported`, `adapter-missing-schema-action-unsupported`, `adapter-missing-mapping-action-unsupported`, `adapter-accept-changes-during-fill-unsupported`, `adapter-continue-update-on-error-unsupported`, `adapter-fill-load-option-unsupported`, `adapter-provider-specific-types-unsupported`, `unsupported-adapter-member`, `repeated-fill` (another Fill of the same DataSet), `fill-schema-unsupported`, `adapter-reused`, `adapter-reassignment`, `adapter-alias-or-escape`) |
| Command | #912 rules with the adapter association as cut-off (Phase 2 unchanged) | every #912/Phase 2 command gap unchanged; `text-command-outside-scope` / `stored-procedure-intent-unavailable` / `command-binding-conflict` in the comparison |
| DataSet | fresh local `new DataSet()` (exactly `System.Data.DataSet`, trusted), plain, `using` or using declaration; the Fill argument; `set.Tables[selector]`; `set.Tables.Count > 0` as the guard below; `Dispose()` statement | local not exactly DataSet (typed DataSet, field, parameter) → `fresh-dataset-local-required`; other creation → `fresh-dataset-required`; `new DataSet(name)` → `dataset-constructor-unsupported`; initializer → `dataset-initializer-unsupported`; property write (`EnforceConstraints`, `CaseSensitive`, `DataSetName`, `Locale`, `Namespace`, `Prefix`, ...) or `Merge`, `ReadXml`, `ReadXmlSchema`, `InferXmlSchema`, `Load`, `Clear`, `Reset`, `AcceptChanges`, `RejectChanges`, `BeginInit`, `EndInit` → `dataset-mutation`; `Relations` → `dataset-relations-unsupported`; `Copy`, `Clone`, `GetChanges`, `CreateDataReader` → `ambiguous-row-ownership`; other members/events → `unsupported-dataset-member`; another adapter's Fill → `repeated-fill`; `FillSchema` argument → `fill-schema-unsupported`; reassignment → `dataset-reassignment`; argument (including ref/out), return, alias, capture → `dataset-alias-or-escape`; use positioned before the Fill → `dataset-use-before-fill` |
| Tables | `set.Tables[0]`; `set.Tables["Table"]` / `set.Tables[srcTable]` (exact case; VB `Tables("Table")`, `Tables!Table`); `set.Tables.Count > 0` guard | constant index `> 0` → `dataset-later-table-unsupported`; negative → `invalid-table-index`; nonconstant index or name → `dynamic-table-selector-unsupported`; case-only name → `dataset-table-name-case-unknown`; other name → `dataset-table-name-unbound`; `[name, namespace]` → `dataset-table-namespace-unsupported`; `Tables.Count` elsewhere → `dataset-tables-count-unsupported`; `Add`, `AddRange`, `Remove`, `RemoveAt`, `Clear` → `dataset-mutation`; other members → `unsupported-dataset-tables-member`; `Tables` as a value (alias, argument, foreach source) → `dataset-tables-alias-or-escape` (a foreach over `Tables` is first rejected as `unsupported-control-flow`) |
| Selected table | the selector expression itself, or a fresh `DataTable` local initialized directly from it (`var table = set.Tables[0];`) and never written; members exactly as the Phase 2 table rules (only `Rows`) | the Phase 2 table gaps (`datatable-mutation`, `datatable-schema-access-unsupported`, `ambiguous-row-ownership`, `unsupported-datatable-member`, `datatable-reassignment`, `datatable-alias-or-escape`, `repeated-fill`, `fill-schema-unsupported`); `Dispose()` on the selected table → `unsupported-datatable-member` |
| Rows / Row / Reads | Phase 2 cells unchanged on the selected table: `Rows[k]` constant `k >= 0`, fresh `DataRow` local, foreach `DataRow`; `row["Name"]`, `row[0]`, VB `row("Name")`, `row(0)`, `row!Name`, `IsNull(name|ordinal)` | Phase 2 gaps unchanged (`dynamic-row-index-unsupported`, `invalid-row-index`, `datatable-mutation`, `unsupported-rows-member`, `rows-alias-or-escape`, `foreach-row-type-unsupported`, `row-reassignment`, `row-alias-or-escape`, `datacolumn-access-unsupported`, `row-version-access-unsupported`, `datarow-field-extension-unsupported`, `unsupported-row-member`; access-level `dynamic-column-name`, `column-name-outside-identifier-vocabulary`, `dynamic-ordinal`) |
| Guards / flow | `if (set.Tables.Count > 0) { ... }`, `if (<selected>.Rows.Count > 0) { ... }` (no else) and `foreach (DataRow row in <selected>.Rows)`, nested in any combination | every other if/loop/switch/try/lambda/local function/`?.`/`??`/`&&`/`||`/compound/increment/deconstruction/dynamic/late-bound/with/invalid, guards on another DataSet/table/reader/adapter → `unsupported-control-flow`; implicit receiver → `implicit-receiver-unsupported`; 20,000 operations / 256 accesses → `operation-limit` / `access-limit` |
Checks run in this order: Fill overload and statement shape, adapter receiver, DataSet argument,
source table, operation bound, table selectors, method-wide control flow, adapter construction,
DataSet construction, implicit receivers, adapter members/association and command, DataSet
members and position, selected-table locals, rows, row members, accesses.
## Contracts
| Contract | Meaning |
| --- | --- |
| `sqlserver-dataset/1` | Property `sqlServerDataSetState` on every `data-adapter-fill` DatabaseOperationCandidate (sibling of `sqlServerDataTableState`; same fact, site, rule and tier). Fields: version, coverage (`bounded`/`unknown`), gap, commandType, commandTextHash, adapterAssociation, fillOverload (`fill-dataset`/`fill-dataset-source-table`), tableName (`Table` or the constant srcTable), adapter/DataSet lines, selectors (sorted subset of `table-local`, `tables-index`, `tables-name`), guards (sorted subset of `foreach`, `rows-count`, `tables-count`), accesses (the Phase 2 DataRow access record). |
| `sqlserver-dataset-bindings/1` | `factId`, `commandText`, four-part `identity`, `dataSetEvidenceSha256` = digest of the contract-tagged DataSet projection (never equal to the DataTable, reader or parameter digests). |
| `sqlserver-dataset-comparison/1` | Report under rule `database.sqlserver.dataset-column-comparison.v1`; binds the admitted shape export (route, version, generator, input, extractor, rule, tier, origin, export digest). |
CLI (compare only): `tracemap sqlserver-dataset-result compare --origin result-schema --declarations <json> | --origin ddl-result|ddl-table-result --input <manifest> --source-root <root>`, then `--schema --bindings --facts --repo --revision --out` → `dataset-column-comparison.json`, `report.md`.
## Comparison
Reasons in order: unsupported source extractor/tier; `dataset-state-unavailable`; state gap;
command type; `invalid-dataset-state`; `binding-missing`; `exact-result-shape-unavailable`;
`command-binding-conflict`. Then one row per access (the Phase 2 access decision, shared:
exact-case unique match, mismatch only against a complete shape, case-only
`column-case-policy-unknown`, duplicate-shape renaming `datatable-duplicate-column-renaming-unknown`,
`nullability-not-compared:IsNull`, declared types uncompared) or `no-bounded-column-access`.
The admitted first-result shape is complete only with at least one column, so the first result
creates the first DataSet table. Rows are Tier2Structural for match/mismatch and Tier4Unknown
otherwise; source tier is kept. Coverage `partial`, verdict `no-aggregate-clean-verdict`.
## Extractor versions
C# `csharp-semantic/0.22.15`, VB `vb-semantic/0.10.18` (additive sibling property on Fill facts).
## Independent matrix (authored before running the extractor)
Fixture `samples/sqlserver-dataset-evidence/{csharp,vb}`, same method names in both languages;
shapes reuse `samples/sqlserver-result-column-evidence` (JSON base, `ddl/`, `ddl-table/`).
Core cells (each against JSON, constant DDL and table DDL; variants as in Phase 2):
IndexZero M; DefaultName M; SourceTableName M; SourceTableIndexZero M; SelectCommandProperty M;
DiscardFill M; TableLocal M; RowLocalOrdinal M; ForeachRows M,M; TableLocalForeach M;
TablesCountGuard M; RowsCountGuard M; NestedGuards M; IsNullName M; IsNullOrdinal M; BangName M
(VB `set.Tables!Table` and `row!Name`); UsingLifetime M; WithParameter M; TwoFills#0 M;
TwoFills#1 M (ReadOther); MixedSelectors M,M; ConvertedOrdinal M; Reordered M,M; MissingName X;
OrdinalOutOfRange X; IsNullMissing X; Renamed X; CaseOnly U; DuplicateDeclared U;
DuplicateRenamed U; DynamicName U; DynamicOrdinal U; NonIdentifierName U; Incomplete U;
AbsentBinding U → 35 cases, 38 rows = 26/4/8 per origin.
State cells (table DDL only; each U with the exact state gap asserted on the fact; `bounded:`
names the comparison reason):
FillDataTable fill-overload-unsupported; FillRange fill-overload-unsupported; FillResultUsed
fill-result-use-unsupported; DynamicSourceTable dynamic-source-table-unsupported;
NonIdentifierSourceTable source-table-outside-identifier-vocabulary; EmptySourceTable
invalid-source-table; SecondTableIndex dataset-later-table-unsupported; NegativeTableIndex
invalid-table-index; DynamicTableIndex dynamic-table-selector-unsupported; DynamicTableName
dynamic-table-selector-unsupported; LaterTableName dataset-table-name-unbound; CaseOnlyTableName
dataset-table-name-case-unknown; SourceNameMismatch dataset-table-name-unbound; TableNamespace
dataset-table-namespace-unsupported; TablesCountUse dataset-tables-count-unsupported;
TablesCountWrongGuard unsupported-control-flow; TablesAdd dataset-mutation; TablesAlias
dataset-tables-alias-or-escape; TablesMember unsupported-dataset-tables-member; TablesForeach
unsupported-control-flow; MergeDataSet dataset-mutation; ReadXml dataset-mutation; Relations
dataset-relations-unsupported; EnforceConstraints dataset-mutation; CaseSensitiveDataSet
dataset-mutation; CopyDataSet ambiguous-row-ownership; DataSetMember unsupported-dataset-member;
NamedDataSet dataset-constructor-unsupported; DataSetInitializer dataset-initializer-unsupported;
DataSetReassigned dataset-reassignment; DataSetAlias dataset-alias-or-escape; DataSetEscape
dataset-alias-or-escape; RefDataSet dataset-alias-or-escape; UseBeforeFill
dataset-use-before-fill; SharedDataSet fresh-dataset-required; TypedDataSet
fresh-dataset-local-required; HelperFill fresh-sqldataadapter-local-required; RepeatedFill#0/#1
repeated-fill; AdapterReused#0/#1 adapter-reused; FillSchemaFirst fill-schema-unsupported;
TableMappings adapter-table-mappings-unsupported; MissingSchemaAction
adapter-missing-schema-action-unsupported; MissingMappingAction
adapter-missing-mapping-action-unsupported; CommandTypeAfterAdapter
command-mutation-after-adapter-association; TextCommand bounded:text-command-outside-scope;
TableLocalReassigned datatable-reassignment; TableLocalEscape datatable-alias-or-escape;
TableColumns datatable-schema-access-unsupported; TableSelect ambiguous-row-ownership; LinqRows
ambiguous-row-ownership; TableClear datatable-mutation; RowsAdd datatable-mutation; RowWrite
datatable-mutation; FieldExtension datarow-field-extension-unsupported; DynamicRowIndex
dynamic-row-index-unsupported; RowAlias row-alias-or-escape; ForeachObjectRow
foreach-row-type-unsupported; GuardElse unsupported-control-flow; TablesAndRowsGuard
unsupported-control-flow; OtherDataSetGuard unsupported-control-flow; TryCatch
unsupported-control-flow; NoReads bounded:no-bounded-column-access → 64 cases.
Per language: 99 Fill sites in 96 methods, **169 case reports, 178 rows = 78 / 12 / 88**; both
languages 156 / 24 / 176. Cross-property checks: every Fill(DataSet) fact keeps DataTable state
`fill-overload-unsupported`; the FillDataTable fact keeps bounded DataTable state.
24 refusal checks (DataTable/reader/parameter bindings and digests, stale digest, duplicate,
stale revision, mutated facts, the Phase 2 origin cross-checks, mutated declarations/export,
stale table digest, symlinked source, DataSet bindings in the DataTable and reader routes,
oversized bindings, existing output preserved).
