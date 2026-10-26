# SQL Phase 3: SqlDataAdapter.Fill(DataSet) to single-table DataRow column reads
Continue the bounded SQL Server consumer work as a separate branch and PR after the Phase 2
DataTable slice (branch `codex/sqlserver-datatable-consumers`) is merged. SQL coverage stays
the priority. Test-suite splitting and wall-clock optimization remain deferred: record observed
timings only; do not reorganize test projects, change concurrency or weaken gates.
## Start from current evidence
Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_PARAMETER_EVIDENCE,
SQLSERVER_DDL_PARAMETER_EVIDENCE, SQLSERVER_RESULT_COLUMN_EVIDENCE,
SQLSERVER_TABLE_RESULT_EVIDENCE and SQLSERVER_DATATABLE_EVIDENCE validation documents and the
complete `.kiro/specs/sqlserver-datatable-consumers` spec (including implementation-state).
Inspect `SqlServerDataTableConsumers.cs`, `SqlServerDataTableEvidence.cs`,
`SqlServerReaderColumns.ClassifyCommand`, their tests, `samples/sqlserver-datatable-evidence`
and all five SQL smoke scripts before editing. Read #864, #865, #870 and #908 as context only.
Fetch origin/dev and verify the merged Phase 2 PR is an ancestor; if not, stop and report. Create
a clean dedicated `codex/` branch and worktree from the fetched dev head; preserve every other
worktree. A fresh worktree needs `dotnet restore` of the `samples/sqlserver-{parameter,
result-column,datatable}-evidence/{csharp,vb}` projects before smokes.
## Scope: one bounded DataSet path in C# and VB
Same command/adapter rules as Phase 2 (unchanged), one fresh `DataSet` filled exactly once by
`Fill(DataSet)` or `Fill(DataSet, constantSourceTable)`, and constant reads through rows of
exactly one table selected by `set.Tables[0]` or by the exact constant name that a fresh adapter
with empty `TableMappings` and default `MissingMappingAction`/`MissingSchemaAction` would create
(`"Table"`, or the constant source-table name). Inspect the signed reference metadata first and
author the accepted syntax matrix in the spec before implementation, deciding at least:
- whether `Tables["Table"]`/`Tables[srcTable]` naming is admitted or only `Tables[0]`, and how
  case-insensitive DataTableCollection lookup interacts with the exact-case policy;
- that any second table index, `Tables.Count` other than a bounded guard, `Tables.Add`,
  `Merge`, `ReadXml`, relations, `EnforceConstraints`, `CaseSensitive` and other DataSet members
  are unsupported cells;
- multiple result sets: `Tables[1]` and beyond stay unknown (first result only);
- whether the Phase 2 deferrals `new DataTable(name)` and `Field<T>` stay deferred; widening the
  trusted assembly set for System.Data.DataSetExtensions needs its own explicit decision.
Preserve every Phase 2 and #912 refusal. Distinct versioned state (for example
`sqlServerDataSetState`, `sqlserver-dataset/1`), digest and bindings (`sqlserver-dataset-bindings/1`);
decide whether this extends the Phase 2 Fill fact property or adds a sibling property, and never
let DataTable, reader or parameter bindings substitute. Bump extractor versions; keep historical
records.
## Evidence and validation gates
Only independently authored public synthetic C#/VB/SQL inputs. No work-machine requests, private
schemas, credentials, containers, SQL Server provisioning, database/application execution,
remote-host mutation or deployment. Not DNN or private-application acceptance. Author expected
outcomes before running the extractor, duplicate them in tests and a new native smoke with its own
denominator, and keep every retained smoke unchanged: 52-case parameter smokes (8/12/32), result
smoke (JSON 36/10/36, DDL 36/10/58, 20 refusals), table smoke (per language 22/5/44, 24 refusals)
and the Phase 2 DataTable smoke (per language 54/12/88 over 148 cases, 22 refusals). If the
Phase 2 DataTable outputs change for its fixtures, stop and explain before accepting a new
denominator. Commit the implementation, freeze source/head/build, run the full Debug solution
with `-warnaserror`, then all smokes sequentially at the committed head in new directories,
Python guards, private-path and whitespace checks and the pinned C#/VB OSS adapter smoke (report
its reduced coverage honestly; those repos contain no SQL calls). Do not change ACK policy, never
tag Qodo, do not merge or request paid review; open a PR only when separately asked.
Final handoff: branch/commit, exact supported syntax matrix and denominator, expected versus
observed outcomes, tests/timings, gaps/non-claims and one next-slice prompt.
