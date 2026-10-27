# SQL Phase 7: DataColumn-identity reads through the bound table's `Columns` collection
Continue the bounded SQL Server consumer work as a separate branch and PR after the Phase 6
Field<T> slice (branch `codex/sqlserver-field-reads`) is merged. SQL coverage stays the priority.
Test-suite splitting and wall-clock optimization remain deferred: record observed timings only;
do not reorganize test projects, change concurrency or weaken gates.
## Start from current evidence
Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_DATATABLE_EVIDENCE, SQLSERVER_DATASET_EVIDENCE,
SQLSERVER_HELPER_RESULT_EVIDENCE, SQLSERVER_INTERNAL_HELPER_RESULT_EVIDENCE,
SQLSERVER_FIELD_READ_EVIDENCE and SQLSERVER_LIVE_PROVIDER_VALIDATION documents and the complete
`.kiro/specs/sqlserver-field-reads` spec (including implementation-state). Inspect
`SqlServerDataTableConsumers.cs` (the shared `TableUse`, `RowsAndAccesses` and `RowUse` rules and
the Phase 6 `fieldReads` mode), `SqlServerDataSetConsumers.cs`, `SqlServerHelperResults.cs`,
`SqlServerFieldReads.cs`, `SqlServerFieldReadEvidence.cs`, their tests, every
`samples/sqlserver-*-evidence` fixture, all nine SQL smoke scripts and
`scripts/dnn-sql-validation/{harness,linked_provider}.py` before editing. Fetch origin/dev and verify
the merged Phase 6 PR is an ancestor; if not, stop and report. Create a clean dedicated `codex/`
branch and worktree from the fetched dev head; preserve every other worktree. New fixtures must be
added to `TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"` references in the same commit
and verified with no pre-existing fixture `bin/`/`obj/`.
## Scope: one DataColumn identity source only
Every Phase 2-6 consumer refuses `table.Columns` as `datatable-schema-access-unsupported` and
`row[column]` / `row.Field<T>(column)` as `datacolumn-access-unsupported`. Admit exactly one
widening: a `DataColumn` obtained from the same bound table's `Columns["Name"]` (constant name in
the identifier vocabulary) or `Columns[ordinal]` (constant), inline or through one fresh local
initialized once after the Fill, used as `row[column]`, `row.IsNull(column)` or
`row.Field<T>(column)` on rows the unchanged rules bound. Author the accepted syntax matrix in a new
spec before implementation, deciding at least:
- whether the column identity becomes the access name/ordinal directly or a distinct column-source
  record, and how `Columns` lookups (case-insensitive at runtime) and ordinal bounds are kept
  unclaimed where the static evidence cannot decide;
- whether the widening is a versioned successor of the Phase 6 state (historical records kept) or
  a distinct sibling; Phase 2-6 outputs, refusals and denominators must not change for any input
  without a bound `Columns` read;
- that `Columns.Add/Remove/Clear`, `ColumnName`/`Ordinal`/`DataType` writes, `Contains`/`IndexOf`,
  columns from other tables or DataSets, column locals that escape, are reassigned or cross a helper
  boundary, `PrimaryKey`, `Constraints`, `DataRowVersion` overloads and `SetField` stay typed
  unknowns.
Distinct versioned state, digest and bindings where outputs change; never let other routes'
bindings substitute. Bump extractor versions.
## Evidence and validation gates
Only independently authored public synthetic C#/VB/SQL inputs for the static corpus. The
existing disposable dockercache SQL workflow may extend the linked static/runtime matrix only with
the same isolation, pinned image, limits, transient credentials, ownership-checked cleanup and
failed-run evidence; no private inputs, existing databases, published ports, persistent volumes,
unrelated container changes or work-machine requests. Author expected outcomes before running the
extractor, duplicate them in tests and a smoke with its own denominator, and keep every retained
smoke unchanged: 52-case parameter smokes (8/12/32), result smoke (JSON 36/10/36, DDL 36/10/58, 20
refusals), table smoke (per language 22/5/44, 24 refusals), DataTable smoke (per language 54/12/88
over 148 cases, 22 refusals), DataSet smoke (per language 78/12/88 over 169 cases, 24 refusals),
helper smoke (per language 42/15/58 over 115 cases, 24 refusals), internal-helper smoke (per
language 36/12/30 over 78 cases, 24 refusals) and Field-read smoke (per language 39/18/21 over 75
cases, 26 refusals); linked matrix 20/6/8 over 34 cases with 54 runtime checks unless deliberately
extended. If retained outputs change, stop and explain before accepting a new denominator. Commit
the implementation, freeze source/head/build, run the full Debug solution with `-warnaserror`, then
all smokes sequentially at the committed head in new directories, Python guards, private-path and
whitespace checks, the pinned C#/VB OSS adapter smoke (report its reduced coverage honestly) and
the linked live harness. Do not change ACK policy, never tag Qodo, do not merge or request paid
review; open a PR only when separately asked.
Final handoff: branch/commit, exact supported syntax matrix and denominator, expected versus
observed outcomes, tests/timings, runtime receipt and cleanup, gaps/non-claims and one
next-slice prompt. Do not claim DNN, private-application, deployment or migration readiness.
