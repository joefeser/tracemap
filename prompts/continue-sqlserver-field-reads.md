# SQL Phase 6: `Field<T>` / `Field(Of T)` column-identity reads on bounded DataRow consumers
Continue the bounded SQL Server consumer work as a separate branch and PR after the Phase 5
internal-helper slice (branch `codex/sqlserver-internal-helpers`) is merged. SQL coverage stays
the priority. Test-suite splitting and wall-clock optimization remain deferred: record observed
timings only; do not reorganize test projects, change concurrency or weaken gates.
## Start from current evidence
Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_DATATABLE_EVIDENCE, SQLSERVER_DATASET_EVIDENCE,
SQLSERVER_HELPER_RESULT_EVIDENCE, SQLSERVER_INTERNAL_HELPER_RESULT_EVIDENCE and
SQLSERVER_LIVE_PROVIDER_VALIDATION documents and the complete `.kiro/specs/sqlserver-internal-helpers`
spec (including implementation-state). Inspect `SqlServerDataTableConsumers.cs`,
`SqlServerDataSetConsumers.cs`, `SqlServerHelperResults.cs`, `SqlServerInternalHelperResults.cs`,
`SqlServerCompilationScopes.cs`, their tests, every `samples/sqlserver-*-evidence` fixture, all
eight SQL smoke scripts and `scripts/dnn-sql-validation/{harness,linked_provider}.py` before
editing. Fetch origin/dev and verify the merged Phase 5 PR is an ancestor; if not, stop and
report. Create a clean dedicated `codex/` branch and worktree from the fetched dev head; preserve
every other worktree. New fixtures must be added to `TraceMap.Tests.csproj` as
`ReferenceOutputAssembly="false"` references in the same commit and verified with no
pre-existing fixture `bin/`/`obj/`.
## Scope: `DataRowExtensions.Field<T>` as a column-identity access only
Every Phase 2-5 consumer refuses `Field<T>` today with `datarow-field-extension-unsupported`
(`SetField` is `datatable-mutation`); `DataRowExtensions` lives in System.Data.DataSetExtensions,
outside the trusted provider set, and is classified by declaring type name only. Admit exactly one widening: `row.Field<T>("Name")` / `row.Field<T>(ordinal)`
(C# extension form and VB reduced form, classified by `(method.ReducedFrom ?? method)
.ContainingType`) as a column name/ordinal access on rows the existing rules already bound.
Author the accepted syntax matrix in a new spec before implementation, deciding at least:
- how the access record marks `Field<T>` so the generic type argument is recorded but never
  compared (no CLR/SQL type, conversion, nullability or `DBNull` claim);
- whether the access stays inside each existing state (a versioned successor of each affected
  state with historical records kept) or one distinct sibling; Phase 2-5 outputs, refusals and
  denominators must not change for any input without `Field<T>`;
- that `Field<T>(DataColumn)`, `DataRowVersion` overloads, `SetField`, `AsEnumerable()` /
  LINQ pipelines, `Field` on rows from unbound tables and assembly-identity mismatches stay
  typed unknowns.
Distinct versioned state, digest and bindings where outputs change; never let other routes'
bindings substitute. Bump extractor versions.
## Evidence and validation gates
Only independently authored public synthetic C#/VB/SQL inputs for the static corpus. The
existing disposable dockercache SQL workflow may extend the linked static/runtime matrix (for
example with `Field<T>` reads and a cross-document internal helper) only with the same
isolation, pinned image, limits, transient credentials, ownership-checked cleanup and failed-run
evidence; no private inputs, existing databases, published ports, persistent volumes, unrelated
container changes or work-machine requests. Author expected outcomes before running the
extractor, duplicate them in tests and a smoke with its own denominator, and keep every
retained smoke unchanged: 52-case parameter smokes (8/12/32), result smoke (JSON 36/10/36, DDL
36/10/58, 20 refusals), table smoke (per language 22/5/44, 24 refusals), DataTable smoke (per
language 54/12/88 over 148 cases, 22 refusals), DataSet smoke (per language 78/12/88 over 169
cases, 24 refusals), helper smoke (per language 42/15/58 over 115 cases, 24 refusals) and
internal-helper smoke (per language 36/12/30 over 78 cases, 24 refusals); linked matrix 12/4/6
with 42 runtime checks unless deliberately extended. If retained outputs change, stop and
explain before accepting a new denominator. Commit the implementation, freeze
source/head/build, run the full Debug solution with `-warnaserror`, then all smokes
sequentially at the committed head in new directories, Python guards, private-path and
whitespace checks, the pinned C#/VB OSS adapter smoke (report its reduced coverage honestly) and
the linked live harness. Do not change ACK policy, never tag Qodo, do not merge or request paid
review; open a PR only when separately asked.
Final handoff: branch/commit, exact supported syntax matrix and denominator, expected versus
observed outcomes, tests/timings, runtime receipt and cleanup, gaps/non-claims and one
next-slice prompt. Do not claim DNN, private-application, deployment or migration readiness.
