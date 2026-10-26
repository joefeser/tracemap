# SQL Phase 2: SqlDataAdapter.Fill to DataTable column reads

Implement Phase 2 of `prompts/build-sqlserver-table-results-and-datatable-consumers.md` as a
separate branch and PR. SQL coverage stays the priority. Test-suite splitting and wall-clock
optimization remain deferred: record observed timings only; do not reorganize test projects,
change concurrency, weaken gates or spend this slice on infrastructure cleanup.

## Start from current evidence

Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_PARAMETER_EVIDENCE,
SQLSERVER_DDL_PARAMETER_EVIDENCE, SQLSERVER_RESULT_COLUMN_EVIDENCE and
SQLSERVER_TABLE_RESULT_EVIDENCE validation documents, and the complete
`.kiro/specs/sqlserver-result-column-evidence` and `.kiro/specs/sqlserver-table-result-shapes`
specs (including implementation-state). Inspect `SqlServerReaderColumns.cs`,
`SqlServerResultEvidence.cs`, `SqlServerDdlTableResultEvidence.cs`, their tests, the
`samples/sqlserver-result-column-evidence` fixtures and all four SQL smoke scripts before
editing. Read #864, #865, #870 and #908 as backlog context only.

Fetch origin/dev and verify the merged Phase 1 PR (branch
`codex/sqlserver-table-result-shapes`, implementation commit `c28dbd3df`, whatever merge commit
the owner created) is an ancestor. If Phase 1 is not merged, stop and report; do not build on
the unmerged branch. Create a clean dedicated `codex/` branch and worktree from the fetched dev
head; preserve every other worktree. A fresh worktree needs `dotnet restore` of the
`samples/sqlserver-{parameter,result-column}-evidence/{csharp,vb}` projects before smokes.

## Scope: one bounded consumer path in C# and VB

A fresh trusted `SqlCommand` local with constant stored-procedure intent (the existing #912
command rules, unchanged), one fresh `SqlDataAdapter` tied to that command, one fresh
`DataTable` filled exactly once, and constant name/ordinal reads through a directly associated
`DataRow`. Inspect the signed System.Data.SqlClient / Microsoft.Data.SqlClient and
System.Data reference assemblies first and author the accepted syntax matrix in the spec before
implementation, including at least these decisions:

- adapter construction: `new SqlDataAdapter(command)` versus `new SqlDataAdapter()` plus one
  standalone `adapter.SelectCommand = command`; the string-text/connection constructors,
  object initializers and any other SelectCommand writes are unsupported cells;
- Fill: only `adapter.Fill(table)` (DbDataAdapter.Fill(DataTable)) as a standalone statement
  or its discarded int result; Fill(DataSet[, string]), Fill(int, int, params DataTable[]),
  FillSchema, async/other overloads are unsupported cells;
- table: `new DataTable()` (decide on `new DataTable(constantName)`) local, never reassigned,
  aliased, passed elsewhere, merged, loaded, cleared, or mutated (Columns/Rows writes,
  TableName, CaseSensitive, Locale, PrimaryKey, Constraints, Load, Merge, ImportRow...);
- row association: `table.Rows[constantInt]` used directly or via a fresh unreassigned
  `DataRow` local, and `foreach (DataRow row in table.Rows)`; reads `row["Name"]`, `row[0]`,
  VB `row("Name")`/`row!Name`, and decide explicitly on `IsNull`, `Field<T>` (DataSetExtensions)
  and `DataColumn` overloads;
- the small guard needed for ordinary consumption: `if (table.Rows.Count > 0)` /
  `foreach` over that exact table, with tests that bind the guard to the selected table and
  reject guards on an unrelated table, reader or adapter.

Unsupported alternatives must not count as supported cells. Refuse or mark unknown: aliases,
reassignment, ref/out escapes, helper boundaries, repeated Fill, command mutation after
adapter association, table/column mutation, `TableMappings`, `MissingSchemaAction`,
`MissingMappingAction`, `AcceptChangesDuringFill`, `ContinueUpdateOnError`, ambiguous row
ownership (rows from another table, `Select`, `DefaultView`, `Copy`, LINQ), switch/try/lambdas
and other control flow the reader route already rejects. Preserve every #912 mutation,
alias, nested-assignment, ref and control-flow protection on the command.

## Evidence contracts

- Bind consumer evidence to the exact command, Fill invocation, adapter, table and row
  association. Use a distinct versioned property (for example `sqlServerDataTableState`,
  `sqlserver-datatable/1`) on the Fill-backed operation fact; do not overload
  `sqlServerReaderState` or reader-only fields, and do not let parameter or reader bindings
  substitute for this consumer identity (distinct digest and bindings version, e.g.
  `sqlserver-datatable-bindings/1`). Decide and document which fact carries the state (the
  existing Fill DatabaseOperationCandidate, if one exists, or why a new fact is needed).
- Bump C#/VB extractor versions and update current version assertions without rewriting
  historical validation records.
- Compare names/ordinals against both the Phase 1 table-backed DDL shapes and the existing
  independent JSON and constant-DDL shapes, preserving partial/unknown outcomes and the
  existing comparison semantics (exact-case unique match; mismatch only against a complete
  shape; case-only, duplicate, dynamic and unbound = unknown). DataTable column lookup is
  case-insensitive when unambiguous at runtime: decide and document how that interacts with the
  exact-case policy (keeping case-only as unknown is the conservative default).
- Preserve original source tier; structural schema correlation never upgrades source tier or
  proves runtime behavior. Failed semantic loading retains its gaps. No claim that rows exist,
  values convert safely or Fill executes.
- Out of scope (separate later slices): DataSet, reader-returning helpers, ORMs, DNN hydration,
  broader provider wrappers, type/nullability comparison and multiple-result semantics.

## Evidence and validation gates

Only independently authored public synthetic C#/VB/SQL inputs. No work-machine requests,
private schemas, credentials, containers, SQL Server provisioning, database/application
execution, remote-host mutation or deployment. Not DNN or private-application acceptance.

Author expected outcomes before running the extractor and duplicate them in tests and a new
native smoke: positive cells, every unsupported cell, unrelated-table/reader guards, missing/
renamed names, invalid ordinals, case/duplicate ambiguity, stale digests, wrong-origin and
parameter/reader-binding substitution refusals, deterministic repeats. Every derived artifact
needs exact generator SHA-256 and privacy-projected bounded-input SHA-256.

Run focused lanes while iterating. Commit the implementation, freeze source/head/build, run
the full Debug solution with `-warnaserror` (shared core and both adapters change), then run
sequentially at the committed head in new directories: both retained 52-case parameter smokes
(8/12/32), the retained result smoke (JSON 36/10/36, DDL 36/10/58, 20 refusals), the Phase 1
table smoke (per language 64 cases, 22/5/44; 24 refusals) and the new DataTable smoke with its
own denominator. If the reader extractor's outputs change for the existing fixtures, stop and
explain before accepting a new retained denominator. Run Python guards, private-path and
whitespace checks; run the pinned C#/VB OSS adapter smoke because the adapters change and
report its reduced coverage honestly (those repos contain no SQL calls). Do not change ACK
policy; if PR work is requested, read the current pr-review-loop skill and verify the canonical
installation and lane first. Never tag Qodo. Do not merge or request paid review; open a PR only
when separately asked.

Final handoff: branch/commit, exact supported syntax matrix and consumer denominator, expected
versus observed outcomes, tests/timings, gaps/non-claims and one next-slice prompt. Keep test
wall-clock refactoring explicitly deferred.
