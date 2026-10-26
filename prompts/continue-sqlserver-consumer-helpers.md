# SQL Phase 4: one-level same-compilation helper returning a filled DataTable/DataSet
Continue the bounded SQL Server consumer work as a separate branch and PR after the Phase 3
DataSet slice (branch `codex/sqlserver-dataset-consumers`) is merged. SQL coverage stays the
priority. Test-suite splitting and wall-clock optimization remain deferred: record observed
timings only; do not reorganize test projects, change concurrency or weaken gates.
## Start from current evidence
Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_PARAMETER_EVIDENCE,
SQLSERVER_DDL_PARAMETER_EVIDENCE, SQLSERVER_RESULT_COLUMN_EVIDENCE,
SQLSERVER_TABLE_RESULT_EVIDENCE, SQLSERVER_DATATABLE_EVIDENCE and SQLSERVER_DATASET_EVIDENCE
validation documents and the complete `.kiro/specs/sqlserver-dataset-consumers` spec (including
implementation-state). Inspect `SqlServerDataTableConsumers.cs` (shared helpers),
`SqlServerDataSetConsumers.cs`, `SqlServerDataSetEvidence.cs`, their tests,
`samples/sqlserver-dataset-evidence` and all six SQL smoke scripts before editing. Read #864,
#865, #870 and #908 as context only. Fetch origin/dev and verify the merged Phase 3 PR is an
ancestor; if not, stop and report. Create a clean dedicated `codex/` branch and worktree from the
fetched dev head; preserve every other worktree. New fixtures must be added to
`TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"` references in the same commit and
verified with no pre-existing fixture `bin/`/`obj/` (the #914 regression).
## Scope: one bounded helper boundary in C# and VB
Today every helper Fill is `fresh-sqldataadapter-local-required`. Admit exactly one level: a
non-virtual, non-generic `private`/`internal` static helper declared in the same compilation
whose body is precisely one supported Phase 2 or Phase 3 Fill shape with the command text taken
from one string parameter, returning the filled DataTable/DataSet local, called from a consumer
method with a constant procedure name and the returned value assigned to a fresh local that
then follows the Phase 2/3 table, rows and row rules. Author the accepted syntax matrix in the
spec before implementation, deciding at least:
- whether the evidence is a new caller-side state (for example `sqlServerHelperResultState`,
  `sqlserver-helper-result/1`) on a new call-site fact or a property on the existing helper Fill
  fact, and how one helper Fill fact with several callers is bound (one binding per call site);
- that recursion, overloads, virtual/interface/extension/generic/async helpers, `ref`/`out`,
  optional/named/nonconstant procedure arguments, helpers in other compilations or partial
  classes, multiple returns, and any helper mutation, guard or second execution stay unknown;
- whether a Microsoft.Data.SqlClient public fixture pair (signed 6.x) lands first as a smaller
  slice; `Field<T>`, named tables/DataSets and later result sets stay deferred unless decided.
Preserve every #912, Phase 2 and Phase 3 refusal and output. Distinct versioned state, digest and
bindings; never let DataSet, DataTable, reader or parameter bindings substitute. Bump extractor
versions; keep historical records.
## Evidence and validation gates
Only independently authored public synthetic C#/VB/SQL inputs. No work-machine requests, private
schemas, credentials, containers, SQL Server provisioning, database/application execution,
remote-host mutation or deployment. Not DNN or private-application acceptance. Author expected
outcomes before running the extractor, duplicate them in tests and a new native smoke with its own
denominator, and keep every retained smoke unchanged: 52-case parameter smokes (8/12/32), result
smoke (JSON 36/10/36, DDL 36/10/58, 20 refusals), table smoke (per language 22/5/44, 24 refusals),
DataTable smoke (per language 54/12/88 over 148 cases, 22 refusals) and DataSet smoke (per
language 78/12/88 over 169 cases, 24 refusals). If retained outputs change for their fixtures,
stop and explain before accepting a new denominator. Commit the implementation, freeze
source/head/build, run the full Debug solution with `-warnaserror`, then all smokes sequentially
at the committed head in new directories, Python guards, private-path and whitespace checks and
the pinned C#/VB OSS adapter smoke (report its reduced coverage honestly; those repos contain no
SQL calls). Do not change ACK policy, never tag Qodo, do not merge or request paid review; open a
PR only when separately asked.
Final handoff: branch/commit, exact supported syntax matrix and denominator, expected versus
observed outcomes, tests/timings, gaps/non-claims and one next-slice prompt.
