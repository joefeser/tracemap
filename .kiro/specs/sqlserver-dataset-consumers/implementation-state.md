# Implementation state — Phase 3
Branch `codex/sqlserver-dataset-consumers`, worktree
`~/.codex/worktrees/sqlserver-dataset-consumers/tracemap`, created from fresh `origin/dev` =
`f0850183d` (#914 merge; Phase 2 implementation `f4f74f01c` and the fixture-graph repair
`efbf433b5` are ancestors). The user checkout (`codex/sqlserver-result-column-prompt`) and all
other worktrees were not touched.
## Scope decisions
- Sibling property `sqlServerDataSetState` on every `data-adapter-fill` fact; the Phase 2
  `sqlServerDataTableState` is unchanged (Fill(DataSet) stays `fill-overload-unsupported` there).
- Admitted selectors: `Tables[0]` and the exact-case default name (`"Table"` / constant
  srcTable); case-only names and later tables stay unknown. Typed and named DataSets,
  `new DataTable(name)` and `Field<T>` stay unsupported; the trusted assembly set is unchanged.
- Shared Phase 2 rules are extracted first in a behavior-preserving commit.
- Fixtures are wired into `TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"` project
  references in the same commit that adds them (the #914 regression); this worktree started
  with no fixture `bin/`/`obj/` and no manual fixture restore is run before the test build.
## Oddities noticed
- The net10.0 reference pack forwards `DataRowExtensions`/`DataTableExtensions` from
  System.Data.DataSetExtensions to System.Data.Common; the Phase 2 note about the
  DataSetExtensions token applies to .NET Framework targets.
