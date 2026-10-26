# Implementation state — Phase 5
Branch `codex/sqlserver-internal-helpers`, worktree
`~/.codex/worktrees/sqlserver-internal-helpers/tracemap`, created from freshly fetched
`origin/dev` = `7169ee6e46252669bbe515d091a33b0c6a1f7429` (the #917 merge commit itself, so the
Phase 4 merge is an ancestor). Upstream tracking was removed so nothing can be pushed to `dev`
by accident. The user checkout and every other worktree were not touched.
## Scope decisions
- Distinct sibling state `sqlserver-internal-helper-result/1`; Phase 4 `sqlserver-helper-result/2`
  is unchanged and keeps internal helpers `helper-accessibility-unsupported`.
- Whole-compilation enumeration uses a per-compilation document scope registered by both
  adapters; without it the state is `helper-compilation-scope-unavailable`.
- The linked runtime matrix is not extended; it runs unchanged as a regression gate.
## Oddities noticed
- First native run: C# matched every authored cell; VB made every internal helper
  `helper-compilation-errors`. Cause: the scope re-evaluated every compilation error, but VB
  MSBuildWorkspace loads always carry location-less injected My-template errors that the VB
  adapter already excludes from coverage. Fixed in `b08be39bb` (each adapter passes its own
  coverage-reducing error decision); design amended. No expectation was changed.
- A deliberate two-cell expectation mutation in the native theory (CrossTableName → mismatch,
  CrossAlias → `datatable-reassignment`) failed both languages with the exact key and gap; it
  was restored.
- Phase 4 bindings presented to the internal route are refused by the strict JSON reader
  (unmapped `helperResultEvidenceSha256`) before the version check; the Phase 4-digest-in-an-
  internal-binding refusal separately exercises the digest check.
- The new Python guard's suppression scan initially read SDK-generated `obj/` sources; it now
  skips build outputs.
## Development validation (not acceptance)
- Clean-fixture check: all 24 `bin/`/`obj/` directories of the twelve
  `samples/sqlserver-*-evidence/{csharp,vb}` projects (including the two new internal-helper
  fixtures) moved out of the worktree; the normal `dotnet test ... TraceMap.Tests.csproj -c
  Debug -warnaserror` restored and built all twelve through the test-project graph. Focused lane
  (internal helper + helper + DataSet + DataTable + table + result + DDL + parameter + catalog +
  DNN + Razor) **762 passed** (705 retained + 57 new).
- Dev smoke at `7f4e40b7f` + working tree: 78 cases per language, 72 / 24 / 60 (expected =
  observed), 0 gaps, 24/24 refusals each for its intended reason, ~59 s.
- Python guards: 50 `test_sqlserver_*` (9 new), 20 DNN harness, 11 linked provider passed;
  private-path guard passed.
## Committed-head acceptance (implementation head `d811d71937b5d4b46aaa965433cc8d9c84bcc112`)
Implementation commits `1504bb620` (state), `7f4e40b7f` (route), `b08be39bb` (adapter error
policy), `ee0811d93` (fixtures/smoke) and the development-notes commit `d811d7193`. All gates ran at
`d811d7193` with source and HEAD frozen (`git status` empty before and after) and every
CLI-directory DLL hash identical before and after the smokes and the linked run.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,339 passed, 0 failed, 3 expected skips** (Oracle full golden sweep opt-in,
  HttpReportProvenanceInterop receipt env unset, Windows-only deep projectless ASP.NET
  publish); 32m 52s tests, 1,982 s wall; zero compiler/analyzer warnings. No failure occurred,
  so no rerun was needed or counted.
- Retained smokes unchanged: 52-case 8/12/32 ×2; result JSON 36/10/36, DDL 36/10/58, 20/20;
  table 44/10/88, 24/24; DataTable 108/24/176, 22/22; DataSet 156/24/176, 24/24; helper
  84/30/116, 24/24. New internal-helper smoke 72/24/60 (per language 36/12/30 over 78 cases),
  0 gaps, 24/24.
- Pinned OSS smoke: exit 0; 12,949 / 114,806 facts, both `Level1SemanticAnalysisReduced` /
  `FailedOrPartial`; no DatabaseOperationCandidate, so no internal-helper state.
- Unchanged linked live harness: 12/4/6, 42/42 runtime checks, owned container removed.
- Python guards 50 + 20 + 11; private-path guard; `git diff --check 7169ee6e4..HEAD`.
## Delivery
Docs-only results commit on top of `d811d7193`, next-slice prompt
`prompts/continue-sqlserver-field-reads.md`, branch pushed. No PR, merge, ACK-policy change or
review request. Not DNN, private-application, deployment or migration acceptance.
