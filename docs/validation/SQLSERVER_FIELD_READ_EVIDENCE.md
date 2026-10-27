# Bounded offline SQL Server `Field<T>` / `Field(Of T)` column-identity read evidence
Phase 6 after Phase 5 (#918, merge `7e96bca84`). One question inside an explicit static subset:
**does a constant `row.Field<T>("Name")` / `row.Field<T>(ordinal)` read, on a DataRow that the
unchanged DataTable, DataSet, private-helper or internal-helper rules bound, agree with the
admitted first-result shape of the procedure that this site executes?** The type argument is
recorded and never compared. The static corpus is public synthetic, compiled/parsed and never
executed. Every report is `partial` / `no-aggregate-clean-verdict`. Not DNN, private-application,
deployment or migration acceptance.
## Contracts and route
| Contract | Meaning |
| --- | --- |
| `sqlserver-field-read/1` (`sqlServerFieldReadState`) | Sibling of the DataTable, DataSet, Phase 4 helper and Phase 5 internal-helper properties on every `data-adapter-fill` DatabaseOperationCandidate (no new fact type): `origin` (the single bounded origin `datatable`, `dataset`, `helper` or `internal-helper`), `originGaps` (every origin's Field-mode gap as `origin:gap` when none is bounded) and `sites` (one per direct Fill, one per helper call site, with the Phase 5 repo-relative caller path for internal helpers). Accesses add `typeArgument`. C# `csharp-semantic/0.22.20`, VB `vb-semantic/0.10.23`. |
| `sqlserver-field-read-bindings/1` | One binding per site: `factId`, `siteIndex`, two-part command text, four-part identity and `fieldReadEvidenceSha256` (contract-tagged projection of the fact identity, the state without sites, the site index and exactly that site). |
| `sqlserver-field-read-comparison/1` | Rows under `database.sqlserver.field-read-column-comparison.v1` carrying `siteIndex`, `origin`, `sitePath`, the site span and member. |
```sh
tracemap sqlserver-field-read compare --origin result-schema --declarations <declarations.json> \
  --schema <result-export.json> --bindings <field-read-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
tracemap sqlserver-field-read compare --origin ddl-result|ddl-table-result --input <manifest.json> --source-root <public-root> \
  --schema <ddl-export.json> --bindings <field-read-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```
Shapes come only from the unchanged `sqlserver-result-schema`, `sqlserver-ddl-result` and
`sqlserver-ddl-table-result` routes; facts keep the 64 MiB DataTable-route bound.
## Accepted syntax (summary; full matrix in the spec design)
[`.kiro/specs/sqlserver-field-reads/design.md`](../../.kiro/specs/sqlserver-field-reads/design.md)
lists every cell and gap, authored before implementation. Exactly one widening of the shared row
rules, applied only in the sibling's Field mode:
- `DataRowExtensions.Field<T>(string)` and `Field<T>(int)` on a bound row, in the C# extension
  form (`row.Field<T>(...)`, also through parentheses) and the VB reduced form
  (`row.Field(Of T)(...)`), classified by `(method.ReducedFrom ?? method).ContainingType`;
- defining assembly `System.Data.Common` (b03f5f7f11d50a3a; on .NET 10 the
  `System.Data.DataSetExtensions` reference assembly forwards the type there) or
  `System.Data.DataSetExtensions` (b77a5c561934e089, .NET Framework). `DatabaseOperationApi.Trusted`
  is not widened;
- access kinds `field-name` / `field-ordinal`, member `Field`, with the Phase 2 constant-name
  vocabulary and access-level gaps; indexer and `IsNull` reads at the same site are compared too.
Typed unknowns: `datarow-field-extension-untrusted` (lookalike assembly),
`datarow-field-static-call-unsupported` (explicit `DataRowExtensions.Field<T>(row, ...)` in C# or
VB), `datacolumn-access-unsupported`, `row-version-access-unsupported`, unchanged `datatable-mutation`
(`SetField`), `ambiguous-row-ownership` (`AsEnumerable()`), `unsupported-control-flow` (LINQ lambdas,
`row?.Field<T>`), `helper-body-unsupported` (Field reads inside a helper body) and the access-level
`field-type-argument-unsupported` (for example `dynamic`).
Type arguments render as namespace-qualified names without keywords, generic arguments in square
brackets, arrays as `[]` / `[,]` (`System.String`, `System.Nullable[System.Int32]`, `System.Byte[]`),
at most 256 characters in `[A-Za-z0-9_.,[]]`. Rows add `field-type-argument-not-compared:<T>`.
## Decisions
- Distinct state: versioned successors of the four existing states would change every retained
  output. The Phase 2-5 states, refusals and denominators are unchanged for every input, and a
  Field read stays `datarow-field-extension-unsupported` there.
- One shared rule set: the four analyses gain an internal `fieldReads` mode that changes only the
  `DataRowExtensions` branch; mode off is the Phase 2-5 decision.
- Partition: a site is bounded only when its rows carry a Field read. A site bound without one is
  `field-read-absent` here and stays the evidence of its own route, so no access is compared on
  two routes.
- The linked runtime matrix is extended (see
  [SQLSERVER_LIVE_PROVIDER_VALIDATION.md](SQLSERVER_LIVE_PROVIDER_VALIDATION.md)).
## Comparison
Per Fill fact: unsupported source; `field-read-state-unavailable`; state gap plus its `originGaps`;
command type; `invalid-field-read-state`; no sites → `helper-caller-unavailable`. Otherwise per
site: site gap or `invalid-field-read-state` (including a path that does not match the origin, a
bounded site without a Field read or an unbounded type argument); `binding-missing`;
`exact-result-shape-unavailable`; `command-binding-conflict` (bounded sites only); then the shared
Phase 2 DataRow decision per access.
## Independent matrix
Fixtures `samples/sqlserver-field-read-evidence/{csharp,vb}` (26 Fill sites per language over four
documents, same names), wired into `TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"`
references. Shapes reuse `samples/sqlserver-result-column-evidence`. The native smoke
(`scripts/sqlserver-field-read-evidence/smoke.py`), `SqlServerFieldReadNativeTests` and the Python
guards carry the same expectations, written before the extractor ran.
- 21 core cells × 3 origins (11 DataTable, 3 DataSet, 3 private-helper and 4 internal-helper
  sites; TableFieldMixed has an indexer and a Field row): rows 39 / 18 / 9.
- 9 state cells and 3 site cells against the table DDL origin: one unknown row each with the
  exact gap asserted on the source state first.
- Per language **75 case reports = 39 bounded matches / 18 mismatches / 21 unknowns**; both
  languages 78 / 36 / 42.
26 refusal checks: DataTable, DataSet, Phase 4 helper, internal-helper, reader and parameter
bindings; DataTable and internal-helper digests; stale digest; site-index and site-digest
substitution; out-of-range index; duplicate binding; stale revision; mutated access, type argument
and procedure hash; field bindings in the DataTable, DataSet, helper, internal-helper and reader
routes; table export as JSON schema; unknown origin; oversized bindings; existing output preserved.
Direct Roslyn tests (`SqlServerFieldReadExtractionTests`) cover the assembly identity (a
source-defined `System.Data.DataRowExtensions` with the real assembly behind an extern alias),
`dynamic` and other type-argument renderings, LINQ and conditional-access flow, the mode-off
output of every Phase 2-5 state, and the internal-helper origin over the Phase 5 registered-scope
harness, including the #918 escaped-identifier and unavailable-input repairs.
## Validation commands
```sh
dotnet build src/dotnet/TraceMap.sln -c Debug -warnaserror   # restores every SQL fixture through the test project graph
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerFieldRead|FullyQualifiedName~SqlServerHelperResult|FullyQualifiedName~SqlServerInternalHelperResult|FullyQualifiedName~SqlServerDataSetEvidenceTests|FullyQualifiedName~SqlServerDataTableEvidenceTests|FullyQualifiedName~SqlServerDdlTableResultEvidenceTests|FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~SqlServerDdlRuleCatalogTests|FullyQualifiedName~DnnSqlValidationTests|FullyQualifiedName~RazorSemanticModelBindingTests'
python3 -m unittest discover -s scripts/validation -p 'test_sqlserver_*.py' -v
python3 -m unittest discover -s scripts/validation -p 'test_dnn_sql_harness.py' -v
python3 -m unittest discover -s scripts/validation -p 'test_sql_linked_provider.py' -v
# Commit, then freeze source/HEAD/build outputs; no concurrent build/smoke/commit.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Sequentially, at the committed head, in new directories: the eight retained SQL smokes, then
python3 scripts/sqlserver-field-read-evidence/smoke.py --output /tmp/field-read-smoke-new
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic scripts/smoke-open-source-repos.sh <cache> <out>
./scripts/check-private-paths.sh
git diff --check
```
## Non-claims and remaining unknowns
Privacy: comparison JSON and Markdown are **local evidence reports, not sanitized shareable
exports**; they retain repo-relative source and site paths, member identities, column names and
type arguments. Keep reports and provenance hashes from private inputs private.
Not proven: that Fill or a helper executes, that any table or row exists, later result sets,
values, CLR/SQL types, conversions, nullability or `DBNull` behavior, runtime exceptions,
catalog/live/deployed correspondence, or helper caller completeness. Outside scope:
`Field<T>(DataColumn)`, `DataRowVersion` overloads, `SetField`, LINQ pipelines, Field reads in
helper bodies, typed or named DataSets, ORMs, DNN hydration, multi-level helpers and provider
wrappers.
## Follow-up work log
Bounded backlog, not an authorization to widen this branch.
1. **Next slice:** see the [next-slice prompt](../../prompts/continue-sqlserver-datacolumn-reads.md).
2. **Deferred decisions:** `Field<T>(DataColumn)` through a bound `table.Columns["Name"]`, typed
   value comparison, LINQ `AsEnumerable()` pipelines, named/typed DataSets, `new DataTable(name)`,
   later result sets, multi-level helpers.
3. **Deferred engineering work:** test wall-clock restructuring stays deferred.
