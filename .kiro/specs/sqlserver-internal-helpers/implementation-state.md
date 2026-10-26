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
