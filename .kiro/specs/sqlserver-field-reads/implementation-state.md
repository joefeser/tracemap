# Implementation state — Phase 6
Branch `codex/sqlserver-field-reads`, worktree `~/.codex/worktrees/sqlserver-field-reads/tracemap`,
created from freshly fetched `origin/dev` = `7e96bca84aed04258577f1b02b6505186863bcfa` (the #918
merge commit itself, so the Phase 5 merge and its escaped-identifier and unavailable-input
repairs are ancestors). Push with an explicit `origin codex/sqlserver-field-reads` refspec only;
never to `dev`. The user checkout and every other worktree were not touched.
## Scope decisions
- Distinct sibling state `sqlserver-field-read/1`; Phase 2-5 states unchanged (Field reads stay
  `datarow-field-extension-unsupported` there).
- The four Phase 2-5 analyses share one `fieldReads` mode; the sibling picks the single bounded
  origin and partitions sites (`field-read-absent` when no Field read remains).
- The linked runtime matrix is extended by six cases per language (design.md).
## Oddities noticed
- On .NET 10 the `System.Data.DataSetExtensions` reference assembly is a facade; Roslyn binds
  `DataRowExtensions` to `System.Data.Common` (b03f5f7f11d50a3a). The .NET Framework identity is
  `System.Data.DataSetExtensions` (b77a5c561934e089). Both are admitted explicitly for Field only.
- First direct run: lambdas (`Select(r => r.Field<T>(...))`) and `row?.Field<T>` hit the unchanged
  method-wide `unsupported-control-flow` before any row rule, in both Phase 2 and Field mode, so the
  AsEnumerable cell uses `table.AsEnumerable().First().Field<string>("Name")` (still
  `ambiguous-row-ownership`); design clarified, no expectation weakened. Nullable and tuple type
  arguments needed `ExpandNullable` / `ExpandValueTuple` to stay inside the bounded vocabulary.
## Development validation (not acceptance)
- Step 6.3: 44 new direct tests; SQL lane with Razor and the new tests 818 passed.
- Step 6.4: an unknown site (no hash) bound by a field binding reports only its own gap, not
  `command-binding-conflict` (Phase 5 adds the conflict for unknown callers too; the Field route
  keeps the reason list to the site's own refusal). 49 Field tests passed.
- Step 6.5: first dev smoke run (working tree on top of `3244949a2`) matched every authored cell:
  75 cases per language, 78 / 36 / 42 both languages, 26/26 refusals each for its intended reason,
  ~75 s. Native theories 2/2; Python guards 59 (`test_sqlserver_*`, 9 new).
- Step 6.6 static-only dry run of the extended linked runner at `94a62b0cd` (runtime stubbed,
  output deleted, no receipt): 34 rows = 20 / 6 / 8 as authored; Field rows match their
  authored statuses in both languages (`missing-column:Missing`, `column-case-policy-unknown:name`).
- Step 6.7 clean-fixture check: all 28 `bin/`/`obj/` directories of the fourteen
  `samples/sqlserver-*-evidence/{csharp,vb}` projects (including the two new Field-read fixtures)
  moved out of the worktree; the normal `dotnet test ... TraceMap.Tests.csproj -c Debug
  -warnaserror` restored and built all fourteen through the test-project graph. Focused lane
  (Field read + helper + internal helper + DataSet + DataTable + table + result + DDL + parameter
  + catalog + DNN + Razor) **825 passed** (818 + 7). Python guards: 59 `test_sqlserver_*`, 20 DNN
  harness, 13 linked provider passed.
## Committed-head acceptance (implementation head `6aa8c40e975b9277418aa3bc4b561073887c7f3e`)
Implementation commits `c4f9936c0` (state), `3244949a2` (route), `c64677e3c` (fixtures/smoke),
`94a62b0cd` (linked extension) and the development-notes commit `6aa8c40e9`. All gates ran at
`6aa8c40e9` with source and HEAD frozen (`git status` empty before and after) and every
CLI-directory DLL hash identical before and after the smokes and the linked run.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,402 passed, 0 failed, 3 expected skips** (Oracle full golden sweep opt-in,
  HttpReportProvenanceInterop receipt env unset, Windows-only deep projectless ASP.NET
  publish); 31 m tests, 1,871 s wall; zero compiler/analyzer warnings. No failure occurred, so no
  rerun was needed or counted.
- Retained smokes unchanged: 52-case 8/12/32 ×2; result JSON 36/10/36, DDL 36/10/58, 20/20;
  table 44/10/88, 24/24; DataTable 108/24/176, 22/22; DataSet 156/24/176, 24/24; helper
  84/30/116, 24/24; internal-helper 72/24/60, 24/24. New Field-read smoke 78/36/42 (per language
  39/18/21 over 75 cases), 0 gaps, 26/26.
- Pinned OSS smoke: exit 0; 12,949 / 114,806 facts, both `Level1SemanticAnalysisReduced` /
  `FailedOrPartial`; no DatabaseOperationCandidate, so no Field-read state.
- Extended linked live harness: 34 cases 20/6/8, 54/54 runtime checks, owned container removed.
- Python guards 59 + 20 + 13; private-path guard; `git diff --check 7e96bca84..HEAD`.
## Delivery
Docs-only results commit on top of `6aa8c40e9`, next-slice prompt
`prompts/continue-sqlserver-datacolumn-reads.md`, branch pushed. No PR, merge, ACK-policy change
or review request. Not DNN, private-application, deployment or migration acceptance.
