# Design — one same-compilation static helper returning a freshly filled DataTable/DataSet
## Starting point (inspected before implementation)
- Every Fill fact carries `sqlServerDataTableState` (Phase 2) and `sqlServerDataSetState` (Phase 3).
  A helper whose body fills and returns a local is unknown in both today (`return table;` is a
  `datatable-alias-or-escape` / `dataset-alias-or-escape`); a helper receiving the adapter as a
  parameter is `fresh-sqldataadapter-local-required`. Both outputs stay unchanged.
- The command rules (`ClassifyCommand`) already admit a parameter reference as an inert
  constructor argument or `Connection` value, and a nonconstant `CommandText` write (hash null).
- `DatabaseOperationApi.AddProperties` receives only the Fill invocation; the fact path is the
  Fill's document. Callers therefore must be enumerable from that one document.
## Decisions
- **Where the evidence lives.** A new sibling property `sqlServerHelperResultState`
  (`sqlserver-helper-result/1`) on every `data-adapter-fill` fact (same fact, site, rule, tier).
  The helper execution (command, adapter, Fill overload, table name) is recorded once; each call
  site is one `callers[i]` entry with its own line span, calling member, procedure-argument hash,
  gap and returned-consumer accesses. No new fact type and no caller-side fact.
- **One binding per call site.** `sqlserver-helper-result-bindings/1` binds `(factId,
  callerIndex)`; `helperResultEvidenceSha256` is the digest of the contract-tagged projection of
  the fact identity, the helper state without its caller list, the caller index and exactly that
  caller entry. A binding therefore cannot be moved to another caller, another procedure or
  another helper, and DataTable/DataSet/reader/parameter digests never equal it.
- **Private only.** Only `private` helpers in a single-declaration (non-partial) type chain are
  admitted: every reference is then inside that one type declaration and hence that document, so
  the enumerated call sites are all compile-time call sites. `internal`/`public`/`protected`
  helpers may be called from other documents or (with InternalsVisibleTo or publicly) other
  compilations and are `helper-accessibility-unsupported`. Calls into a helper from another
  compilation are never evidence: that compilation has no Fill fact for it. Reflection is never
  claimed.
- **Parameters.** Exactly one `string` procedure parameter, referenced exactly once, as the
  command text (`new SqlCommand(procedure[, connection])` or one `command.CommandText =
  procedure` write); optionally one exact signed `SqlConnection` parameter used only as the
  command connection (needed for the runtime-linked fixtures). Callers pass a constant non-empty
  string positionally; the connection argument is a parameter or local reference.
- **Deferrals kept.** `Field<T>`, `new DataTable(name)`, named/typed DataSets, later tables,
  reader-returning helpers and multi-level helpers stay unknown. The Microsoft.Data.SqlClient
  fixture pair from #916 is reused for runtime linkage; no new provider foundation is built.
## Accepted syntax matrix
S = supported. Every other cell makes the whole helper state (helper rows) or one caller entry
(caller rows) `unknown` with the listed gap; the first failing check wins in the order given.
### Helper (whole state)
| Area | Supported (S) | Unsupported → gap |
| --- | --- | --- |
| Fill | `Fill(DataTable)` (Phase 2) → `fill-datatable`; `Fill(DataSet)` / `Fill(DataSet, constant)` (Phase 3) → `fill-dataset` / `fill-dataset-source-table`; standalone or discarded statement | other overloads → `fill-overload-unsupported`; result used → `fill-result-use-unsupported`; Phase 3 source-table gaps (`dynamic-source-table-unsupported`, `invalid-source-table`, `source-table-outside-identifier-vocabulary`) |
| Enclosing member | ordinary method | lambda, local function, accessor, constructor, operator → `helper-method-unsupported` |
| Receivers | fresh adapter local; fresh exact DataTable/DataSet local | `fresh-sqldataadapter-local-required`; `fresh-datatable-local-required` / `fresh-dataset-local-required`; operations > 20,000 → `operation-limit` |
| Return | exactly one explicit `return <filled local>` (C# `return table;`, VB `Return table`) | no such return (every inline Phase 2/3 consumer) → `helper-return-required`; a second explicit return → `helper-multiple-returns` |
| Symbol | `private`, static (C# `static`, VB `Shared` or Module member), non-generic, not an extension, single-declaration types, unique name in its type | virtual/abstract/override/interface member → `helper-virtual-or-interface-unsupported`; instance → `helper-static-required`; generic method or generic containing type → `helper-generic-unsupported`; async/iterator → `helper-async-unsupported`; extension → `helper-extension-unsupported`; not private → `helper-accessibility-unsupported`; partial method or any containing type with more than one declaration → `helper-partial-type-unsupported`; another member with the same name → `helper-overload-ambiguous` |
| Signature | `(string procedure)`, `(SqlConnection connection, string procedure)` or `(string procedure, SqlConnection connection)`, by value, no default, no params; return type exactly `System.Data.DataTable` / `System.Data.DataSet` matching the Fill | anything else → `helper-parameter-unsupported`; other return type → `helper-return-type-unsupported` |
| References | invocations of the helper in its document (≤ 64) | a reference inside the helper → `helper-recursion`; any non-invocation reference (method group, delegate, `AddressOf`, `nameof`) → `helper-reference-escape`; > 64 call sites → `helper-caller-limit` |
| Body | the Phase 2/3 command, adapter and construction rules unchanged; `using` on command/adapter | any if/loop/try/etc. → `unsupported-control-flow`; every Phase 2/3 adapter, command and construction gap; filled local declared with `using` → `helper-result-disposed`; implicit receiver → `implicit-receiver-unsupported`; procedure parameter written → `helper-parameter-mutation`; command text not exactly the procedure parameter (constant text, both sources, extra references) → `helper-command-text-parameter-required`; any other source-method call → `helper-body-unsupported`; filled-local mutation/escape → the Phase 2/3 table/DataSet gap (`datatable-mutation`, `dataset-mutation`, ...); any read, selector or guard of the filled local in the helper → `helper-body-unsupported` |
### Caller (one entry per call site)
| Area | Supported (S) | Unsupported → gap |
| --- | --- | --- |
| Arguments | positional; procedure a constant non-empty string (literal or const); connection a parameter or local | named → `helper-named-argument-unsupported`; nonconstant → `helper-procedure-argument-nonconstant`; null/empty → `helper-procedure-argument-invalid`; other connection expression → `helper-connection-argument-unsupported` |
| Result | direct initializer of a fresh local of the exact return type (`var`, explicit type, `using`, VB `Dim`/`Using`) | discarded, returned, chained, passed, assigned to an existing local/field, other local type → `helper-result-local-required` |
| Consumer (DataTable) | the Phase 2 table/rows/row/access rules and guards (`Rows.Count > 0`, foreach) on that local; `Dispose()` | every Phase 2 gap: `unsupported-control-flow`, `implicit-receiver-unsupported`, `datatable-mutation`, `datatable-reassignment`, `datatable-alias-or-escape`, `repeated-fill`, `fill-schema-unsupported`, rows/row gaps, `access-limit`, `operation-limit` |
| Consumer (DataSet) | the Phase 3 selector/table/rows rules with the helper's first-table name (`Table` or its constant source table) and guards (`Tables.Count > 0`, `Rows.Count > 0`, foreach) | every Phase 3 gap (`dataset-later-table-unsupported`, `dataset-table-name-unbound`, `dataset-mutation`, ...) |
Helper checks run: Fill overload/source table, enclosing member, Fill statement, receivers,
operation bound, return, symbol (virtual, static, generic, async, extension, accessibility,
partial, overload), signature, return type, references (recursion, escape, limit), multiple
returns, flow, adapter construction, table/DataSet construction, disposed result, implicit
receiver, adapter/command, parameter mutation, command text, body calls, filled-local uses.
Caller checks run: arguments, result local, operation bound, flow (selectors first for DataSet),
implicit receiver, local uses, rows, accesses.
## Contracts
| Contract | Meaning |
| --- | --- |
| `sqlserver-helper-result/1` | `sqlServerHelperResultState` on every Fill fact: version, coverage, gap, resultKind (`datatable`/`dataset`), commandType, commandTextSource (`constructor-parameter`/`command-text-parameter`), adapterAssociation, fillOverload, tableName (DataSet only), helperLine, adapterLine, resultLine, callers (source order). Caller: line, endLine, caller (calling member display string), coverage, gap, commandTextHash (SHA-256 of the constant procedure argument), resultLine, selectors, guards, accesses (Phase 2 DataRow access record). |
| `sqlserver-helper-result-bindings/1` | per call: `factId`, `callerIndex`, `commandText`, four-part `identity`, `helperResultEvidenceSha256`. |
| `sqlserver-helper-result-comparison/1` | rule `database.sqlserver.helper-result-column-comparison.v1`; rows carry factId, callerIndex, caller line span, accessIndex and the admitted-shape provenance as in the DataSet route. |
CLI (compare only): `tracemap sqlserver-helper-result compare --origin result-schema --declarations <json> | --origin ddl-result|ddl-table-result --input <manifest> --source-root <root>`, then `--schema --bindings --facts --repo --revision --out` → `helper-result-column-comparison.json`, `report.md`.
## Comparison
Per Fill fact: unsupported source extractor/tier; `helper-result-state-unavailable`; helper gap;
command type (`text-command-outside-scope` / `stored-procedure-intent-unavailable`);
`invalid-helper-result-state`; no callers → one `helper-caller-unavailable` row. Otherwise one
group per caller: caller gap; `binding-missing`; `exact-result-shape-unavailable`;
`command-binding-conflict` (binding text hash ≠ that caller's procedure hash); then one row per
access with the shared Phase 2 decision, or `no-bounded-column-access`. Rows are Tier2Structural
for match/mismatch and Tier4Unknown otherwise; source tier kept; coverage `partial`, verdict
`no-aggregate-clean-verdict`.
## Extractor versions
C# `csharp-semantic/0.22.16`, VB `vb-semantic/0.10.19` (additive sibling property on Fill facts).
## Independent native matrix (authored before running the extractor)
Fixture `samples/sqlserver-helper-result-evidence/{csharp,vb}`, same names in both languages.
Bounded helpers: `ReadTable(procedure)`; `ReadSet(procedure)`; `ReadNamedSet(procedure)`
(`Fill(set, "Items")`); `ReadTableText(procedure)` (`new SqlCommand()`, `CommandText = procedure`,
`SelectCommand` write); `ReadTableUsing(connection, procedure)` (`using`, constructor connection).
Shapes reuse `samples/sqlserver-result-column-evidence` (JSON base, `ddl/`, `ddl-table/`).
Core caller cells (each against JSON, constant DDL and table DDL; `#n` = nth call in a method):
TableName M; TableOrdinal M (row local, ordinal); TableForeach M; TableRowsGuard M (IsNull name);
OtherProcedure M (`dbo.ReadOther` → Title); TwoCallers#0 M (ReadItems) and TwoCallers#1 M
(ReadOther) in one method; SetIndex M; SetDefaultName M; SetSourceName M (ReadNamedSet,
`Tables["Items"]`); SetTableLocalGuards M (Tables.Count guard, table local, foreach); TextHelper M;
ConnectionHelper M; ConstProcedure M (const procedure); MissingName X (Title on ReadItems);
OrdinalOutOfRange X; OtherProcedureMissing X (Name on ReadOther); SetMissing X; Renamed X
(renamed shape); CaseOnly U; DynamicName U; AbsentBinding U (absent four-part identity);
Incomplete U (incomplete shape) → 23 cases × 3 origins = 69 cases, rows 42 / 15 / 12.
Helper state cells (table DDL only, one unknown row each; `bounded:` names the reason):
PublicHelper `helper-accessibility-unsupported`; InternalHelper `helper-accessibility-unsupported`;
InstanceHelper `helper-static-required`; VirtualHelper `helper-virtual-or-interface-unsupported`;
GenericHelper `helper-generic-unsupported`; OverloadedHelper#0/#1 `helper-overload-ambiguous`;
ExtensionHelper `helper-extension-unsupported`; PartialHelper `helper-partial-type-unsupported`;
RefHelper `helper-parameter-unsupported`; OptionalHelper `helper-parameter-unsupported`;
TwoStringHelper `helper-parameter-unsupported`; ObjectHelper `helper-return-type-unsupported`;
RecursiveHelper `helper-recursion`; DelegateHelper `helper-reference-escape`; MutatingHelper
`datatable-mutation`; GuardedHelper `unsupported-control-flow`; ExecutingHelper
`command-executed-outside-fill`; MultipleReturnHelper `helper-multiple-returns`;
ConstantTextHelper `helper-command-text-parameter-required`; ParameterWriteHelper
`helper-parameter-mutation`; CallingHelper `helper-body-unsupported`; DisposedHelper
`helper-result-disposed`; UncalledHelper `bounded:helper-caller-unavailable`; TextCommandHelper
`bounded:text-command-outside-scope`; InlineFill `helper-return-required` (its DataTable state
stays bounded); CallerRefill `helper-return-required` (the caller's own second Fill) → 27 cells.
Caller state cells (table DDL only, one unknown row each): CallerNamedArgument
`helper-named-argument-unsupported`; CallerDynamicProcedure `helper-procedure-argument-nonconstant`;
CallerNullProcedure `helper-procedure-argument-invalid`; CallerDiscarded, CallerChained,
CallerPassed, CallerObjectLocal `helper-result-local-required`; CallerRowWrite
`datatable-mutation`; CallerRefill `repeated-fill`; CallerReassigned#0 `datatable-reassignment`;
CallerReassigned#1 `helper-result-local-required`; CallerAlias `datatable-alias-or-escape`;
CallerTryCatch `unsupported-control-flow`; CallerLambda `unsupported-control-flow`;
CallerLaterTable `dataset-later-table-unsupported`; CallerSetMutation `dataset-mutation`;
CallerSourceNameMismatch `dataset-table-name-unbound`; CallerConnectionArgument
`helper-connection-argument-unsupported`; CallerNoReads `bounded:no-bounded-column-access` → 19.
Per language: **115 case reports, rows 42 / 15 / 58**; both languages 84 / 30 / 116. Every
unknown helper's own caller(s) exist in the fixture but produce no caller entry. Cross-property:
every helper and caller Fill fact keeps its Phase 2/3 states (helpers stay
`datatable-alias-or-escape` / `dataset-alias-or-escape` or their earlier gap; InlineFill keeps a
bounded DataTable state).
24 refusals: DataTable, DataSet, reader and parameter bindings as helper bindings; DataTable,
DataSet and stale digests; caller-index substitution (TwoCallers#0 binding retargeted to index
1), caller-digest substitution (index 1 digest at index 0), out-of-range index; duplicate
binding; stale revision; mutated caller access; mutated caller procedure hash; table export as
JSON schema; constant export as table schema; unknown origin; origin-option mismatch; mutated
JSON declarations; helper bindings in the DataTable, DataSet and reader routes; oversized
bindings; existing output preserved.
## Linked static/runtime extension (authored before execution)
Reuse `samples/sqlserver-live-provider` (Microsoft.Data.SqlClient 6.1.7). Add private static
helpers `ReadTable(connection, procedure)` and `ReadSet(connection, procedure)` and five callers
per language: HelperTableName (match / `sample`), HelperOtherProcedure (`dbo.ReadOther` → Title;
match / `other`), HelperSetName (match / `sample`), HelperMissingName (mismatch /
ArgumentException), HelperLaterTable (`Tables[1]`; unknown `dataset-later-table-unsupported` /
99). Runtime setup also creates `dbo.ReadOther AS SELECT N'other' AS Title`. Combined linked
denominator: 22 cases = **12 match / 4 mismatch / 6 unknown**; runtime 20 + 22 linked = **42**
assertions. Runtime success on HelperLaterTable must stay a static unknown.
