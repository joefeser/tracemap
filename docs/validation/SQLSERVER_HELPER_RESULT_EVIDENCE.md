# Bounded offline SQL Server helper-returned DataTable/DataSet evidence (one helper level)
Phase 4 after Phase 3 (#915, `b55b2d107`) and the linked provider matrix (#916, `834d74fe8`).
One question inside an explicit static subset: **does a constant DataRow column-name or ordinal
read, through the DataTable (or first DataSet table) returned by one bounded same-document static
helper, agree with the admitted first-result shape of the procedure that this call site passes?**
The static corpus is public synthetic, compiled/parsed and never executed. Every report is
`partial` / `no-aggregate-clean-verdict`. Not DNN, private-application, deployment or migration
acceptance.
## Contracts and route
| Contract | Meaning |
| --- | --- |
| `sqlserver-helper-result/2` (`sqlServerHelperResultState`) | Sibling of `sqlServerDataTableState` and `sqlServerDataSetState` on every `data-adapter-fill` DatabaseOperationCandidate (no new fact type). The helper execution (result kind, command type, command-text source, adapter association, Fill overload, DataSet table name) is recorded once; `callers[i]` is one entry per call site with its line span, calling member, gap, SHA-256 of the constant procedure argument and the Phase 2 DataRow access records of the returned local. C# `csharp-semantic/0.22.17`, VB `vb-semantic/0.10.20`. |
| `sqlserver-helper-result-bindings/1` | One binding per call site: `factId`, `callerIndex`, two-part command text, four-part identity and `helperResultEvidenceSha256` (contract-tagged projection of the fact identity, the helper state without callers, the caller index and exactly that caller). |
| `sqlserver-helper-result-comparison/1` | Rows under `database.sqlserver.helper-result-column-comparison.v1` carrying `callerIndex`, the caller line span and calling member; binds the admitted shape export as in the DataSet route. |
```sh
tracemap sqlserver-helper-result compare --origin result-schema --declarations <declarations.json> \
  --schema <result-export.json> --bindings <helper-result-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
tracemap sqlserver-helper-result compare --origin ddl-result|ddl-table-result --input <manifest.json> --source-root <public-root> \
  --schema <ddl-export.json> --bindings <helper-result-bindings.json> --facts <facts.ndjson> --repo <repo> --revision <sha> --out <new-dir>
```
Shapes come only from the unchanged `sqlserver-result-schema`, `sqlserver-ddl-result` and
`sqlserver-ddl-table-result` routes; facts keep the 64 MiB DataTable-route bound.
## Accepted syntax (summary; full matrix in the spec design)
[`.kiro/specs/sqlserver-consumer-helpers/design.md`](../../.kiro/specs/sqlserver-consumer-helpers/design.md)
lists every cell and gap, authored before implementation. Supported:
- helper: one `private` static (C# `static`, VB `Shared` or Module member), non-virtual,
  non-generic, non-extension, non-async ordinary method with a unique name in single-declaration
  types, signature `(string procedure)` or with one extra exact signed `SqlConnection`
  parameter, return type exactly `DataTable`/`DataSet`;
- helper body: exactly one Phase 2 `Fill(DataTable)` or Phase 3 `Fill(DataSet[, constant])`
  shape (fresh command/adapter/result locals, `using` on command/adapter), command text exactly
  the procedure parameter (constructor `cmdText` or one `CommandText` write), connection
  parameter used only as the command connection, no guard/read/other call, one explicit
  `return <filled local>`;
- callers: every invocation of the helper in its document, each a separate entry: a positional
  constant non-empty procedure argument (literal or const), a parameter/local connection
  argument, and the result as the direct initializer of a fresh local of the exact type that then
  follows the Phase 2 (DataTable) or Phase 3 (DataSet, with the helper's first-table name)
  consumer rules and guards.
Unknown with typed gaps: non-private helpers (`helper-accessibility-unsupported`; callers in
other documents or compilations cannot be enumerated), instance, virtual/interface, generic,
async, extension, partial and overloaded helpers, `ref`/`out`/optional/params/extra
parameters, other return types, recursion, method-group/`AddressOf`/`nameof` references,
more than 64 call sites, multiple returns, helper control flow, mutation, reads, second
executions, other calls, disposed results, command text other than the parameter, parameter
writes, every Phase 2/3 helper-body gap, and on the caller side named/nonconstant/null
procedure arguments, non-local results and every Phase 2/3 consumer gap.
## Decisions
- Only private helpers are admitted, so all compile-time call sites are in the Fill's document;
  enumerated call sites are still never a runtime-caller completeness claim (reflection).
- One binding per call site; the digest covers the helper execution plus exactly that caller,
  so another caller's index, digest or procedure never substitutes. A binding naming another
  caller's procedure is a per-row `command-binding-conflict`.
- The optional `SqlConnection` parameter exists so the #916 Microsoft.Data.SqlClient fixtures
  can execute the same helper methods; connection identity is never evidence.
- Phase 2/3 outputs are unchanged: the helper Fill keeps `datatable-alias-or-escape` /
  `dataset-alias-or-escape`, inline consumers keep their bounded states and get
  `helper-return-required` here.
## Comparison
Per Fill fact: unsupported source; `helper-result-state-unavailable`; helper gap; command type;
`invalid-helper-result-state`; no callers → `helper-caller-unavailable`. Otherwise per caller:
caller gap; `binding-missing`; `exact-result-shape-unavailable`; `command-binding-conflict`;
then the shared Phase 2 DataRow decision per access (exact-case unique match, mismatch only
against a complete shape, case-only / duplicate renaming / dynamic unknown, `IsNull`
nullability not compared) or `no-bounded-column-access`.
## Independent matrix
Fixtures `samples/sqlserver-helper-result-evidence/{csharp,vb}` (32 Fill sites per language,
same names), wired into `TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"`
references. Shapes reuse `samples/sqlserver-result-column-evidence`. The native smoke
(`scripts/sqlserver-helper-result-evidence/smoke.py`) and `SqlServerHelperResultEvidenceTests`
carry the same expectations, written before the extractor ran.
- 23 core caller cells × 3 origins (including TwoCallers#0/#1 of one helper with different
  procedures, ReadOther callers, ReadNamedSet `Tables["Items"]`, CommandText-write and
  connection helpers): rows 42 / 15 / 12.
- 27 helper state cells and 19 caller state cells against the table DDL origin: one unknown row
  each with the exact gap asserted on the source state first.
- Per language **115 case reports = 42 bounded matches / 15 mismatches / 58 unknowns**; both
  languages 84 / 30 / 116.
24 refusal checks: DataTable, DataSet, reader and parameter bindings; DataTable, DataSet and
stale digests; caller-index and caller-digest substitution between two callers of one helper;
out-of-range index; duplicate binding; stale revision; mutated caller access; mutated caller
procedure hash; table export as JSON schema; constant export as table schema; unknown origin;
origin/option mismatch; mutated declarations; helper bindings in the DataTable, DataSet and
reader routes; oversized bindings; existing output preserved.
## Validation commands
```sh
dotnet build src/dotnet/TraceMap.sln -c Debug -warnaserror   # restores every SQL fixture through the test project graph
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror \
  --filter 'FullyQualifiedName~SqlServerHelperResult|FullyQualifiedName~SqlServerDataSetEvidenceTests|FullyQualifiedName~SqlServerDataTableEvidenceTests|FullyQualifiedName~SqlServerDdlTableResultEvidenceTests|FullyQualifiedName~SqlServerResultColumnEvidenceTests|FullyQualifiedName~SqlServerDdlEvidenceTests|FullyQualifiedName~SqlServerParameterEvidenceTests|FullyQualifiedName~SqlServerDdlRuleCatalogTests|FullyQualifiedName~DnnSqlValidationTests|FullyQualifiedName~RazorSemanticModelBindingTests'
python3 -m unittest discover -s scripts/validation -p 'test_sqlserver_*.py' -v
python3 -m unittest discover -s scripts/validation -p 'test_dnn_sql_harness.py' -v
python3 -m unittest discover -s scripts/validation -p 'test_sql_linked_provider.py' -v
# Commit, then freeze source/HEAD/build outputs; no concurrent build/smoke/commit.
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror
# Sequentially, at the committed head, in new directories: the six retained SQL smokes, then
python3 scripts/sqlserver-helper-result-evidence/smoke.py --output /tmp/helper-result-smoke-new
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic scripts/smoke-open-source-repos.sh <cache> <out>
./scripts/check-private-paths.sh
git diff --check
```
The linked static/runtime extension is documented in
[SQLSERVER_LIVE_PROVIDER_VALIDATION.md](SQLSERVER_LIVE_PROVIDER_VALIDATION.md).
## Non-claims and remaining unknowns
Privacy: comparison JSON and Markdown are **local evidence reports, not sanitized shareable
exports**. They intentionally retain repo-relative source locations, caller identities and
column names required to trace findings. Public synthetic schema admission does not certify
the source facts as public. Keep reports and provenance hashes from private inputs private;
only the public synthetic corpus outputs are suitable for publication. No public-source
attestation or automatic redaction is implemented by this route.

Not proven: that the helper or Fill executes, that the call sites are all runtime callers, that
any table or row exists, later result sets, values, types, conversions, nullability, runtime
exceptions, catalog/live/deployed correspondence. Outside scope: multi-level, instance,
non-private, generic or wrapper helpers, reader-returning helpers, `Field<T>`, named/typed
DataSets, `DataColumn`/`DataRowVersion` access, ORMs, DNN hydration and provider wrappers.
## Follow-up work log
Bounded backlog, not an authorization to widen this branch.
1. **Next slice:** internal helpers called from other documents of the same compilation are the
   separate Phase 5 state, documented in
   [SQLSERVER_INTERNAL_HELPER_RESULT_EVIDENCE.md](SQLSERVER_INTERNAL_HELPER_RESULT_EVIDENCE.md);
   this Phase 4 state is unchanged.
2. **Deferred decisions:** multi-level and wrapper helpers, reader-returning helpers,
   `Field<T>`, named/typed DataSets, `new DataTable(name)`, later result sets.
3. **Deferred engineering work:** test wall-clock restructuring stays deferred.
## Validated implementation head — 2026-10-26
Implementation head **`d39acb8cd4f192fc4be8165f7f1b8d04f1c3213b`** on `codex/sqlserver-consumer-helpers`, from fresh `origin/dev` =
#916 merge `834d74fe8`. Recorded by a separate documentation-only commit; no implementation
change follows the gates.
| Gate | Result |
| --- | --- |
| Clean fixture graph (all ten SQL fixture `bin/`/`obj/` moved out, normal test-project build only) | all restored and built; focused lane 689 passed (93 new) |
| Full Debug solution -warnaserror, frozen source/HEAD/outputs | 5,266 passed, 0 failed, 3 expected skips; 32m 8s (1,939 s wall); no warnings |
| Retained JSON / DDL 52-case parameter smokes | 8 / 12 / 32 each |
| Retained result smoke | JSON 36 / 10 / 36, DDL 36 / 10 / 58, 20/20 refusals |
| Retained table smoke | 64 cases per language; 44 / 10 / 88, 24/24 refusals |
| Retained DataTable smoke | 148 cases per language; 108 / 24 / 176, 22/22 refusals |
| Retained DataSet smoke | 169 cases per language; 156 / 24 / 176, 24/24 refusals |
| New helper smoke | 115 cases per language; 84 / 30 / 116, expected = observed, 0 gaps, 24/24 refusals, repeat bytes identical |
| Pinned C#/VB OSS adapter smoke | exit 0; 12,949 / 114,806 facts at C# 0.22.16 / VB 0.10.19; reduced coverage, no SQL calls |
| Linked live harness (reviewed disposable context) | 22 cases 12 / 4 / 6 as authored; 42/42 runtime checks; SQL Server 16.0.4205.1; owned container removed |
| Python guards; private-path guard; `git diff --check` | 39 + 20 + 11 passed; passed; passed |
Receipts are local (outside Git): `/tmp/tracemap-hr-d39acb8-{json,ddl,result,table,datatable,dataset,helper}/smoke-receipt.json`
and `/tmp/tracemap-linked-provider-hr-d39acb8/`. Helper smoke generator
`f6226aaa89ea7cfab9a86ef5e0000397fe8970a0d181aadebed5ceb73afb637f`, bounded input
`b09d214e0ecd97f584880168aa93ae2e12e00c69c1b18bc1c8470833de6db906`; Core
`67f0cc20563a054fa5a18a41f91509ec851092ab772bbe65095bead9cb8d8c19`. Retained smoke generators
are unchanged.
Coverage honesty: the pinned public repositories contain no SQL calls; helper evidence is
exercised by the synthetic corpus, direct tests and the linked synthetic matrix only. Not DNN,
private-application, catalog, deployment or migration acceptance.
