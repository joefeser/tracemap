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
## Committed-head acceptance (implementation `c46a0e6054d90975d30d4fadc5bf83f280d4df0e`)
All gates below ran at the implementation commit with source, HEAD and shared build outputs
frozen (`git status` empty and HEAD unchanged before and after); no competing build, smoke or
commit ran in this checkout during the full suite. Results are recorded by a later
documentation-only commit whose SHA differs.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,173 passed, 0 failed, 3 expected skips** (HttpReportProvenanceInterop receipt env unset,
  Oracle full golden sweep opt-in, Windows-only deep projectless ASP.NET publish); 29m 23s tests,
  1,773 s wall; zero compiler/analyzer warnings. (Timing recorded only.)
- Then sequentially (one at a time; run order result, table, JSON, DDL, DataTable, DataSet), new
  directories outside Git (`/tmp/tracemap-ds-c46a0e6-*`):
  - JSON 52-case parameter smoke: **8/12/32**, 19 s, generator `16f7b62d…` (unchanged).
  - DDL 52-case parameter smoke reusing those scans: **8/12/32**, 25 s, generator `118db6e0…` (unchanged).
  - Retained result smoke: JSON **36/10/36**, DDL **36/10/58**, **20/20 refusals**, 0 gaps, 72 s,
    generator `ce53c852…` (unchanged).
  - Retained table smoke: **64 cases per language, 44/10/88**, **24/24 refusals**, 0 gaps, 70 s,
    generator `7c40fa9d…` (unchanged).
  - Retained DataTable smoke: **148 cases per language, 108/24/176**, **22/22 refusals**, 0 gaps,
    161 s, generator `08946e0d…` (unchanged); only its bounded input moved (extractor version
    strings in the projected facts).
  - New DataSet smoke: **169 cases per language, 156/24/176** (per language 78/12/88, expected =
    observed), 0 gaps, **24/24 refusals**, byte-identical JSON+Markdown repeats, 174 s, generator
    `33d80e40ed5a8389548a4717322cc48190d944f041bb7275ab454cf432c9f141`, bounded input
    `ea4957233857713e5fa109d6ab965433de6bdfb8799b09eb34473123f808346e`.
  - Core SHA-256 `5df9d3938ee4810ae4df57ca7a1a7ee7c38493d12b242682951b5ab26a78dae5` in every
    Core-bound receipt; ScriptDom parser unchanged `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Pinned OSS adapter smoke (`TRACEMAP_SKIP_BUILD=1
  TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic`, warm public
  cache): exit 0, 23 s, all artifacts each; C# `csharp-semantic/0.22.15` 12,949 facts, VB
  `vb-semantic/0.10.18` 114,806 facts (same counts as the Phase 2 record), both
  `Level1SemanticAnalysisReduced` / `FailedOrPartial` (documented expected partial coverage).
  Neither repo has a DatabaseOperationCandidate or `sqlServerDataSetState`, so this only shows the
  change does not disturb unrelated extraction; DataSet evidence is covered by the synthetic corpus
  and direct tests only. Non-.NET adapters, Windows and private/application checks not run.
- Private-path guard and `git diff --check f0850183d..HEAD` passed at the implementation commit.
## Delivery
Docs-only results commit on top of `c46a0e605`, next-slice prompt
`prompts/continue-sqlserver-consumer-helpers.md`, branch pushed. No PR, merge, review request,
SQL execution, container or private input.
