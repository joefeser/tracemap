# Implementation state — Phase 4
Branch `codex/sqlserver-consumer-helpers`, worktree
`~/.codex/worktrees/sqlserver-consumer-helpers/tracemap`, created from freshly fetched
`origin/dev` = `834d74fe86e1a1e769c9d1be583317b09295e92a` (#916 merge; #915 `b55b2d107` is an
ancestor). Upstream tracking was removed so nothing can be pushed to `dev` by accident. The user
checkout (`codex/sqlserver-result-column-prompt`) and every other worktree were not touched.
## Scope decisions
- Evidence is a sibling property `sqlServerHelperResultState` on every Fill fact; one helper
  execution, one caller entry per call site, one binding per `(factId, callerIndex)`.
- Only `private` static helpers in single-declaration types are admitted, so every call site is
  in the Fill's document; other accessibilities stay `helper-accessibility-unsupported`.
- One optional exact `SqlConnection` parameter is admitted besides the `string` procedure
  parameter so the #916 runtime-linked fixtures can execute the same helper methods.
- The #916 Microsoft.Data.SqlClient fixture pair and disposable harness are reused and extended;
  no new provider foundation.
## Oddities noticed
- `nameof(Helper)` binds the method group only as a candidate symbol (no `SymbolInfo.Symbol`);
  the reference scan treats candidate bindings as references so it stays `helper-reference-escape`.
- In the first direct-test draft a guard on another caller's table (foreach over `a.Rows` while
  analysing `b`) made `b` unknown; that is the unchanged Phase 2 rule (guards bound to the
  selected table only), so the test, not the extractor, was changed.
- The first extractor run over the new fixtures matched every authored expectation in both
  languages (native theories and the dev smoke). A deliberate two-cell expectation mutation
  (TableName → mismatch, CallerAlias → reassignment) failed both theories with the exact key
  and gap; it was restored.
- The linked runner reuses the result smoke's `case_keys` for direct sites; helper call sites are
  keyed by the caller member display string (`CSharpErrorMessageFormat`) with an authored
  `(helper, procedure)` table, so a missing or duplicate call site refuses.
## Development validation (not acceptance)
- Clean-fixture check: every `samples/sqlserver-*-evidence/{csharp,vb}` `bin/`/`obj/` (ten
  projects, including the two new helper fixtures) moved out of the worktree; the normal
  `dotnet test ... TraceMap.Tests.csproj -c Debug -warnaserror` restored and built all ten through
  the test-project graph. Focused lane (helper + DataSet + DataTable + table + result + DDL +
  parameter + catalog + DNN + Razor) **689 passed** (596 retained + 93 new).
- Dev helper smoke at `5daf36d3b` + working tree: 115 cases per language, 84 / 30 / 116
  (expected = observed), 0 gaps, 24/24 refusals each for its intended reason, ~103 s.
- Linked static half (runtime intentionally not executed, Docker untouched): 22 rows =
  12 match / 4 mismatch / 6 unknown as authored; HelperLaterTable carries
  `dataset-later-table-unsupported`. Dry-run output deleted; it has no receipt.
- Python guards: 39 `test_sqlserver_*` (7 new), 20 DNN harness (1 new), 11 linked provider
  (1 new) passed; private-path guard and `git diff --check` passed.
