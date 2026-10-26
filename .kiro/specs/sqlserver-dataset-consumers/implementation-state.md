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
- First fixture build (before any extractor run) failed on fixture-authoring compile errors
  only: the C# methods `MissingSchemaAction`/`MissingMappingAction` shadowed the enum names
  (now `System.Data.`-qualified), VB rejected the keyword `Alias` as a local name (`aliased`) and
  a local named like its function (`Relations` → `relationSet`). No expectation changed.
- The first extractor run matched every authored expectation: 37/37 new tests on the first run
  (both native theories 78/12/88). A deliberate two-cell expectation mutation made both native
  theories fail with the exact key and gap; it was then restored.
## Development validation (dirty tree at 5060e95c3; not acceptance)
- Clean-fixture check: every `samples/sqlserver-*-evidence/{csharp,vb}` `bin/`/`obj/` moved out
  of the worktree, then the normal `dotnet test ... TraceMap.Tests.csproj -c Debug -warnaserror`
  (no separate fixture restore) restored and built all eight SQL fixture projects through the
  test-project graph; focused lane (DataSet + DataTable + table + result + DDL + parameter +
  catalog + DNN + Razor) 596 passed.
- Dev DataSet smoke `/tmp/tm-ds-smoke-dev`: 169 cases per language, 156 / 24 / 176 (expected =
  observed), 0 gaps, 24/24 refusals each for its intended reason, 159 s.
- Python guards: 32 `test_sqlserver_*` (7 new) + 17 DNN passed; private-path guard and
  `git diff --check` passed.
