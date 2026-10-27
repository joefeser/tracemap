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
- Presence: omit the sibling payload only for a currently registered, complete, error-free scope when a bounded compilation-wide token check proves
  no possible Field read exists. Cross-document callers, escaped names, unresolved/lookalike
  calls and uncertain/oversized scopes remain conservative. Existing 64 MiB whole-facts and
  4 MiB per-record bounds are unchanged. No new positive evidence follows from omission.
  Scope metadata is never cached: registration/re-registration is checked before the syntax cache.
  Reduced, errored and unregistered scopes preserve their explicit helper refusal gaps.
- Provenance boundary: raw Field state is an embedded scan fact property, not a standalone
  derived export. It retains the fact's repo/revision, rule/tier, source span and extractor
  version. Its per-site binding digest covers the complete projected state/site; the derived
  comparison report carries the exact Core generator SHA-256 and bounded input projection
  SHA-256, including those projections. These hashes prove integrity, not raw-fact authenticity
  or deployment correspondence; copying hashes into each raw property would not add that proof.

PR #919 repair validation at `edc67537d`: full Debug `-warnaserror` **5,407 passed,
3 expected skips**, no failures (33m 20s); 61 focused tests, 59 Python SQL guards.
Field smoke remains **78/36/42**, now with **26 C# plus 26 VB refusals**. Retained
DataTable/DataSet/private-helper/internal-helper counts and refusals are unchanged.
Pinned C#/VB OSS smokes pass with their existing reduced coverage and no SQL calls.
The first near-limit test fixture incorrectly padded one JSON record beyond the
separate 4 MiB record bound; it was corrected to multiple bounded records, without
changing either production limit. Source and CLI/Core bytes were frozen for all
final gates. No live SQL or private inputs were used in this repair validation.
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
26 refusal checks per language (52 total): DataTable, DataSet, Phase 4 helper, internal-helper, reader and parameter
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
## Validated implementation head — 2026-10-26
Implementation head **`6aa8c40e975b9277418aa3bc4b561073887c7f3e`** on
`codex/sqlserver-field-reads`, from fresh `origin/dev` = #918 merge `7e96bca84`. Recorded by a
separate documentation-only commit; no implementation change follows the gates. Source, HEAD and
CLI-directory DLL hashes were identical before and after the smokes and the linked run; no
competing build, smoke or commit ran in the worktree.
| Gate | Result |
| --- | --- |
| Clean fixture graph (all fourteen SQL fixture `bin/`/`obj/` moved out, normal test-project build only) | all restored and built; focused lane 825 passed (774 retained + 51 new) |
| Full Debug solution -warnaserror, frozen source/HEAD | 5,402 passed, 0 failed, 3 expected skips; 31 m tests (1,871 s wall); no warnings |
| Retained JSON / DDL 52-case parameter smokes | 8 / 12 / 32 each (21 s / 30 s) |
| Retained result smoke | JSON 36 / 10 / 36, DDL 36 / 10 / 58, 20/20 refusals (80 s) |
| Retained table smoke | 64 cases per language; 44 / 10 / 88, 24/24 refusals (82 s) |
| Retained DataTable smoke | 148 cases per language; 108 / 24 / 176, 22/22 refusals (149 s) |
| Retained DataSet smoke | 169 cases per language; 156 / 24 / 176, 24/24 refusals (160 s) |
| Retained helper smoke | 115 cases per language; 84 / 30 / 116, 24/24 refusals (91 s) |
| Retained internal-helper smoke | 78 cases per language; 72 / 24 / 60, 24/24 refusals (54 s) |
| New Field-read smoke | 75 cases per language; 78 / 36 / 42, expected = observed, 0 gaps, 26/26 C# refusals only (VB refusals were missing in this historical run), repeat bytes identical (54 s) |
| Pinned C#/VB OSS adapter smoke | exit 0 (20 s); 12,949 / 114,806 facts at C# 0.22.20 / VB 0.10.23; both `Level1SemanticAnalysisReduced` / `FailedOrPartial`; no DatabaseOperationCandidate facts |
| Extended linked live harness (reviewed disposable context) | 34 cases 20 / 6 / 8; 54/54 runtime checks; SQL Server 16.0.4205.1; owned container removed (86 s) |
| Python guards; private-path guard; `git diff --check 7e96bca84..HEAD` | 59 + 20 + 13 passed; passed; passed |
Receipts are local (outside Git): `/tmp/tracemap-fr-6aa8c40-{json,ddl,result,table,datatable,dataset,helper,internal,field}/smoke-receipt.json`,
`/tmp/tracemap-fr-6aa8c40-oss/` and `/tmp/tracemap-linked-provider-fr-6aa8c40/`. Field-read smoke
generator `668916909ce9ebfb25a644c9e65cc0bb66b3e482cbfeaeaa85c4df10f32b932b`, bounded input
`5e9ffc9d47ad10907427f179810e020e2e992e72f57eb4f70433086ea2ccda60`; Core
`13fd3275c70fec0777a1af20c3e4b6ca63d23f4748475719a94430ccc3666ad7`; ScriptDom parser unchanged
`2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`. The eight retained smoke
scripts are byte-identical to `origin/dev`; their bounded inputs moved only through the extractor
version strings and the additive `sqlServerFieldReadState` property in projected facts.
Coverage honesty: the pinned public repositories contain no SQL calls; Field-read evidence is
exercised by the synthetic corpus, direct tests and the disposable linked matrix only. Not DNN,
private-application, catalog, deployment or migration acceptance.
