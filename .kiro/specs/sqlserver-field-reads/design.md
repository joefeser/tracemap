# Design — `Field<T>` / `Field(Of T)` column-identity reads on bounded DataRow consumers
## Starting point (inspected before implementation)
- Every Phase 2-5 consumer classifies DataRow uses in one shared function,
  `SqlServerDataTableConsumers.RowUse`, reached through `RowsAndAccesses` from the DataTable and
  DataSet extractors, the Phase 4/5 helper body check and every helper caller. A
  `DataRowExtensions` call on a bound row is the whole-state (or whole-caller) gap
  `datarow-field-extension-unsupported`; `SetField` is `datatable-mutation`. The C# call is an
  unreduced extension invocation with the row as `Arguments[0]`; the VB call is reduced, with
  the row as `Instance` and `TargetMethod.ReducedFrom` set.
- `DataRowExtensions` is classified by declaring type name only. On .NET 10 the compile-time
  reference `System.Data.DataSetExtensions.dll` forwards the type, so Roslyn binds it to its
  defining assembly; .NET Framework defines it in `System.Data.DataSetExtensions`
  (b77a5c561934e089). The defining assembly must therefore be checked explicitly.
- `SqlServerSchemaEvidence.Json` writes null members and the strict reader disallows unmapped
  members, so adding a field to `SqlServerDataTableAccess` would change Phase 2-5 bytes or
  refusals. The Phase 2-5 smokes pin `datarow-field-extension-unsupported` cells
  (`FieldExtension` in the DataTable and DataSet fixtures).
## Decisions
- **Distinct sibling, not successors.** Versioned successors of four states would change every
  retained output (version strings, digests, smokes). One sibling property
  `sqlServerFieldReadState` (`sqlserver-field-read/1`) on every `data-adapter-fill` fact; the
  Phase 2-5 states, their refusals and their denominators are unchanged for every input, and a
  Field read stays `datarow-field-extension-unsupported` there.
- **Shared rules, one mode flag.** The four existing analyses gain an internal `fieldReads`
  mode (default off) that changes only the `DataRowExtensions` branch of `RowUse`. With the
  mode off the code path is byte-for-byte the Phase 2-5 decision. Internally accesses are a
  superset record `SqlServerFieldReadAccess` (the Phase 2 fields plus `typeArgument`); the
  Phase 2-5 states project it to `SqlServerDataTableAccess` losslessly because the mode-off
  path never records a Field access.
- **Origin.** The sibling runs the four analyses in Field mode and takes the one bounded origin,
  in this order: `datatable`, `dataset`, `helper` (Phase 4 private), `internal-helper` (Phase 5).
  They are mutually exclusive by construction (overload, returned local, accessibility); two
  bounded origins would be `field-read-origin-ambiguous`. With none bounded the state is
  `field-read-origin-unbound` and `originGaps` lists every origin's gap as `origin:gap`, in that
  order, so the specific refusal (for example `datatable:datacolumn-access-unsupported`) is
  explicit.
- **Sites and partition.** A bounded state has sites: one for a direct DataTable/DataSet Fill (the
  Fill span, its enclosing member and command-text hash), one per call site for helpers (the
  Phase 4/5 caller entries, with the Phase 5 repo-relative path). A site is `bounded` only when
  its rows carry at least one Field access; a site that the Field mode binds without any Field
  read is `field-read-absent` with no accesses (its evidence stays on its own Phase 2-5 route),
  so no access is ever compared on two routes. An unknown site keeps its Field-mode gap.
- **Type argument.** Recorded, never compared: the namespace-qualified name without keywords,
  generic arguments in square brackets separated by commas, arrays as `[]`/`[,]`, type parameters
  by name (`System.String`, `System.Nullable[System.Int32]`, `System.Byte[]`), at most 256
  characters in `[A-Za-z0-9_.,\[\]]`. `dynamic`, error and other types are the access-level gap
  `field-type-argument-unsupported`. Rows add the facet `field-type-argument-not-compared:<T>`.
  No type, conversion, nullability or `DBNull` claim.
- **Assembly identity.** The defining assembly of `(method.ReducedFrom ?? method)` must be
  `System.Data.Common` (b03f5f7f11d50a3a) or `System.Data.DataSetExtensions` (b77a5c561934e089);
  otherwise `datarow-field-extension-untrusted`. `DatabaseOperationApi.Trusted` is not widened.
- **Static form.** `DataRowExtensions.Field<T>(row, "Name")` (C#) and
  `DataRowExtensions.Field(Of T)(row, "Name")` (VB, unreduced) are outside the authored matrix:
  `datarow-field-static-call-unsupported`.
- **Linked runtime matrix.** Extended with Field reads (DataTable name/ordinal/missing/case-only,
  DataSet name, and a cross-document internal helper) under the unchanged harness safeguards.
## Accepted syntax matrix
S = supported. The checks run in `RowUse` on a DataRow the unchanged rules bound, after the
receiver is matched; first failing check wins in the order given. Every unsupported cell makes the
whole Field-mode analysis (direct origins) or that caller (helper origins) unknown with the gap.
| Area | Supported (S) | Unsupported → gap |
| --- | --- | --- |
| Declaring type | `(ReducedFrom ?? method).ContainingType` is `System.Data.DataRowExtensions` (else the unchanged non-Field row rules) | — |
| Member | `Field` | `SetField` → `datatable-mutation` (unchanged); any other name → `datarow-field-extension-unsupported` |
| Assembly | defining assembly `System.Data.Common`/b03f5f7f11d50a3a or `System.Data.DataSetExtensions`/b77a5c561934e089 | `datarow-field-extension-untrusted` |
| Call form | C# `row.Field<T>(...)` (receiver is the member-access expression); VB reduced `row.Field(Of T)(...)` | explicit static call (C# or VB) → `datarow-field-static-call-unsupported` |
| Overload | `Field<T>(string)`, `Field<T>(int)` | any `DataColumn` parameter → `datacolumn-access-unsupported`; `DataRowVersion` overloads → `row-version-access-unsupported` |
| Argument | compile-time constant name in the identifier vocabulary or constant ordinal | access-level `dynamic-column-name`, `column-name-outside-identifier-vocabulary`, `dynamic-ordinal` (unchanged Phase 2 vocabulary) |
| Type argument | renderable (see Decisions) | access-level `field-type-argument-unsupported` |
| Receiver row | rows-index, row-local, foreach-row of the bound table (unchanged Phase 2-5 rules) | unchanged Phase 2-5 gaps; rows of other tables are never bound (`field-read-absent` when no bound Field read remains) |
| Ownership | — | `AsEnumerable()` / LINQ pipelines → unchanged `ambiguous-row-ownership` |
| Helper body | — | Field read inside a helper body → `helper-body-unsupported` (Field mode does not widen helper bodies) |
Access kinds `field-name` / `field-ordinal`, member `Field`, plus the Phase 2 kinds for indexer and
`IsNull` reads at the same site.
## Contracts
| Contract | Meaning |
| --- | --- |
| `sqlserver-field-read/1` | `sqlServerFieldReadState`: `version, coverage, gap, origin, originGaps, resultKind, commandType, commandTextSource, adapterAssociation, fillOverload, tableName, sites`; each site `{path, line, endLine, caller, coverage, gap, commandTextHash, selectors, guards, accesses}`; each access `{kind, member, rowSource, rowIndex, name, ordinal, typeArgument, line, endLine, gap}`. |
| `sqlserver-field-read-bindings/1` | per site: `factId`, `siteIndex`, `commandText`, four-part `identity`, `fieldReadEvidenceSha256` (contract-tagged projection of the fact identity, the state without sites, the site index and exactly that site). |
| `sqlserver-field-read-comparison/1` | rule `database.sqlserver.field-read-column-comparison.v1`; rows carry factId, siteIndex, origin, site path/span/member, accessIndex and admitted-shape provenance. |
CLI: `tracemap sqlserver-field-read compare` with the Phase 2-5 options →
`field-read-column-comparison.json`, `report.md`. Facts keep the 64 MiB DataTable-route bound.
Comparison per fact: unsupported source; `field-read-state-unavailable`; state gap (plus its
`originGaps`); command type; `invalid-field-read-state`; no sites → `helper-caller-unavailable`.
Per site: site gap or `invalid-field-read-state`; `binding-missing`;
`exact-result-shape-unavailable`; `command-binding-conflict`; then the shared Phase 2 column
decision per access. Rows are Tier2Structural for match/mismatch, Tier4Unknown otherwise;
coverage `partial`, verdict `no-aggregate-clean-verdict`.
## Extractor versions
C# `csharp-semantic/0.22.20`, VB `vb-semantic/0.10.23` (additive sibling property on Fill facts).
## Independent native matrix (authored before running the extractor)
Fixture `samples/sqlserver-field-read-evidence/{csharp,vb}` (`FieldReads.csproj` /
`FieldReads.vbproj`), same names in both languages; documents `Tables`, `Sets`, `Helpers`
(`PrivateData` with private helpers `ReadPrivate(procedure)` and `ReadBody(procedure)` and
their callers; `InternalData` with internal helpers `ReadShared(procedure)` and
`ReadSharedSet(procedure)`) and `Callers` (`FieldCallers`, the internal-helper callers). Shapes
reuse `samples/sqlserver-result-column-evidence` (`dbo.ReadItems`: Id, Name, Total;
`dbo.ReadOther`: Title). 26 Fill sites per language.
Core cells (M match, X mismatch, U unknown; each against JSON, constant DDL and table DDL):
DataTable origin (`Tables`): TableFieldName M (`Field<string>("Name")`); TableFieldOrdinal M
(`Field<int>(0)`); TableFieldRowLocal M (row local, `Field<int?>("Id")`); TableFieldForeach M
(rows-count guard + foreach, `Field<decimal>("Total")`); TableFieldMixed M, M (indexer `"Id"` then
`Field<string>("Name")`); TableFieldMissing X; TableFieldOrdinalOutOfRange X (`Field<string>(3)`);
TableFieldCaseOnly U (`"name"`); TableFieldDynamic U (parameter name); TableFieldRenamed X
(renamed shape); TableFieldIncomplete U (incomplete shape). DataSet origin (`Sets`): SetFieldName
M (`Tables[0]`); SetFieldTableLocal M (`Tables["Table"]` local, foreach, `Field<int>(0)`);
SetFieldMissing X. Private helper origin (`PrivateData` callers of `ReadPrivate`): HelperFieldName
M; HelperFieldOtherProcedure M (`dbo.ReadOther` → `Title`); HelperFieldMissing X. Internal helper
origin (`FieldCallers` in `Callers`): InternalFieldName M; InternalFieldOrdinal M (row local,
`Field<decimal>(2)`); InternalFieldSet M (`ReadSharedSet`, `Tables[0]`); InternalFieldMissing X.
→ 21 cells × 3 = 63 cases, rows 39 / 18 / 9.
State cells (table DDL only, one unknown row each; Fill-keyed): TableFieldColumn
`datatable:datacolumn-access-unsupported`; TableFieldVersion
`datatable:row-version-access-unsupported`; TableSetField `datatable:datatable-mutation`;
TableFieldAsEnumerable `datatable:ambiguous-row-ownership`; TableFieldStatic
`datatable:datarow-field-static-call-unsupported`; TableFieldUnbound (Field read on a second,
unfilled table) site `field-read-absent`; TableNoField (indexer only) site `field-read-absent`;
SetFieldLaterTable `dataset:dataset-later-table-unsupported`; ReadBody (Field read in the helper
body) `helper:helper-body-unsupported`. Caller cells: HelperFieldColumn
`datacolumn-access-unsupported`; InternalFieldVersion `row-version-access-unsupported`;
InternalNoField `field-read-absent` → 12 cells.
Per language: **75 case reports, rows 39 / 18 / 21**; both languages 78 / 36 / 42.
Cross-property (smoke and tests): every core and Field-refusal cell keeps its Phase 2-5 gap
(`datarow-field-extension-unsupported` for Field reads; the unchanged gap otherwise), TableNoField
and TableFieldUnbound keep a bounded DataTable state, and helper callers keep their Phase 4/5
caller gaps.
Direct-test-only cells: assembly identity mismatch (a source-defined
`System.Data.DataRowExtensions` with the real assembly behind an extern alias), unrenderable
type arguments (`dynamic`), rendering of nullable/array/generic type arguments, and every Phase
2-5 extractor's mode-off output for Field-bearing inputs.
26 refusals: DataTable, DataSet, Phase 4 helper, internal-helper, reader and parameter bindings
as field bindings; DataTable and internal-helper digest substitution; stale digest; site-index and
site-digest substitution between two sites of `ReadShared`; out-of-range index; duplicate binding;
stale revision; mutated access name, type argument and site procedure hash; field bindings in
the DataTable, DataSet, helper, internal-helper and reader routes; table export as JSON schema;
unknown origin; oversized bindings; existing output preserved.
## Linked static/runtime extension (authored before running)
`samples/sqlserver-live-provider/{csharp,vb}` gain a second document `Shared` with an internal
(`Friend`) helper `ReadShared(connection, procedure)` and six cases in `Cases` compared through
the new route: FieldTableName M / runtime `sample`; FieldOrdinal M / `1`; FieldSetName M /
`sample`; FieldMissingName X / `ArgumentException`; FieldCaseOnly U / `sample` (runtime case
fallback); InternalFieldName (cross-document internal helper) M / `sample`. Per language 4 / 1 / 1;
the linked matrix becomes 34 cases = 20 / 6 / 8 and the runtime checks 42 + 12 = 54. The retained
22 cases keep their routes and outcomes; runtime results never change a static status.
