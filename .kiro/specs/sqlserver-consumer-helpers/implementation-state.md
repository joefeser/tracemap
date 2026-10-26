# Implementation state — Phase 4
## PR 917 review repair

Follow-up provenance repair: state is now `sqlserver-helper-result/2`, C# extractor
`csharp-semantic/0.22.17`, VB `vb-semantic/0.10.20`. Old bounded v1 facts and old semantic
extractor versions remain unknown even with valid binding digests. The full run on
`37b56231d` was stopped as superseded; no full-suite pass is claimed for that repair.
SQL/Razor gate on the completed version update: 701 passed, 0 failed; helper-smoke and
linked-provider Python guards: 7 + 11 passed. A transient catalog mismatch during editing
was corrected before the complete successful rerun.

The settled review batch identified independent admission, resource-bound and presentation
issues. Helper bodies now admit only the selected Fill, its command/adapter/result allocations
and command/adapter disposal; signed provider identity alone does not admit unrelated calls.
Caller collection uses span deduplication and stops at the 65th unique invocation. C#/VB
regressions cover independent executions, other fills, allocations and the 64/65 boundary.
Reports retain source evidence by design, but now explicitly warn that they are local,
not sanitized shareable exports; public schema admission does not certify source-fact privacy.
The misleading public-input hash label is removed. Repair validation: helper-focused 106
passed and broader SQL 693 passed with Debug `-warnaserror`; private-path and whitespace
guards passed. Committed-head smoke and refreshed review/CI remain pending. The original
full-suite result below is historical evidence, not a full-suite rerun of this repair.

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
## Committed-head acceptance (implementation head `d39acb8cd4f192fc4be8165f7f1b8d04f1c3213b`)
Implementation commits `8c4bbe191` (state), `5daf36d3b` (route), `56a894107` (fixtures/smoke),
`25319c59f` (linked runtime) and the development-notes commit `d39acb8cd`. All gates below ran
at `d39acb8cd` with source, HEAD and shared build outputs frozen (`git status` empty and HEAD
unchanged before and after; every CLI-directory DLL hash identical before and after the smokes
and the linked run); no competing build, smoke or commit ran in this checkout. Results are
recorded by a later documentation-only commit whose SHA differs.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,266 passed, 0 failed, 3 expected skips** (HttpReportProvenanceInterop receipt env unset,
  Oracle full golden sweep opt-in, Windows-only deep projectless ASP.NET publish); 32m 8s tests,
  1,939 s wall; zero compiler/analyzer warnings. 5,173 Phase 3 baseline + 93 new; no retained
  test changed except the pinned C# extractor version string. (Timing recorded only.)
- Then sequentially, new directories outside Git (`/tmp/tracemap-hr-d39acb8-*`):
  - JSON 52-case parameter smoke **8/12/32**, 16 s, generator `16f7b62d…` (unchanged).
  - DDL 52-case parameter smoke **8/12/32**, 22 s, generator `118db6e0…` (unchanged).
  - Retained result smoke JSON **36/10/36**, DDL **36/10/58**, **20/20 refusals**, 73 s,
    generator `ce53c852…` (unchanged).
  - Retained table smoke **64 cases per language, 44/10/88**, **24/24 refusals**, 74 s,
    generator `7c40fa9d…` (unchanged).
  - Retained DataTable smoke **148 cases per language, 108/24/176**, **22/22 refusals**, 139 s,
    generator `08946e0d…` (unchanged).
  - Retained DataSet smoke **169 cases per language, 156/24/176**, **24/24 refusals**, 179 s,
    generator `33d80e40…` (unchanged). The DataTable and DataSet smokes' bounded inputs moved only
    through the extractor version strings in their projected facts.
  - New helper smoke **115 cases per language, 84/30/116** (per language 42/15/58, expected =
    observed), 0 gaps, **24/24 refusals**, byte-identical JSON+Markdown repeats, 99 s, generator
    `f6226aaa89ea7cfab9a86ef5e0000397fe8970a0d181aadebed5ceb73afb637f`, bounded input
    `b09d214e0ecd97f584880168aa93ae2e12e00c69c1b18bc1c8470833de6db906`.
  - Core SHA-256 `67f0cc20563a054fa5a18a41f91509ec851092ab772bbe65095bead9cb8d8c19` in every
    Core-bound receipt; ScriptDom parser unchanged `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Pinned OSS adapter smoke (`TRACEMAP_SKIP_BUILD=1
  TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic`, warm public
  cache): exit 0, 30 s; C# `csharp-semantic/0.22.16` 12,949 facts, VB `vb-semantic/0.10.19`
  114,806 facts (unchanged counts), both `Level1SemanticAnalysisReduced` / `FailedOrPartial`
  (documented expected partial coverage). Neither repo has a DatabaseOperationCandidate or
  `sqlServerHelperResultState`; helper evidence is covered by the synthetic corpus, direct tests
  and the linked matrix only.
- Linked live harness, after read-only revalidation of the reviewed context
  (`tracemap-sql-validation` → `ssh://dockercache`; native linux/x86_64, 8 CPUs, ~30.6 GiB,
  memory/swap/CFS/PID limits enforceable, Docker 29.9.2, 359 GiB free on the Docker data
  filesystem; pinned image present by RepoDigest, linux/amd64; no pre-existing harness
  containers): exit 0, 106 s. **22 linked cases = 12 match / 4 mismatch / 6 unknown** (expected =
  observed), byte-identical repeats, **42/42 runtime checks**, SQL Server **16.0.4205.1**,
  container limits 2 CPUs / 6 GiB memory+swap / SQL 2 GiB / 2 GiB tmpfs, network none, no ports,
  cleanup **owned-container-removed**, no harness containers afterwards, no credential text in
  either receipt. Linked receipt `/tmp/tracemap-linked-provider-hr-d39acb8/linked-receipt.json`
  (SHA-256 `ac750b24d02616d515e92b8edf23568d7b48487ca2b348f691046618b976e8c3`, generator
  `0fd27dd2…`, bounded input `f6738454…`); runtime receipt `runtime/receipt.json` (SHA-256
  `02cac9c60166211cfb6a5a074ea304ab07e172503d27f168f17fdf5abbc55705`, harness generator
  `3df876f7…`, provider bounded input `fc838862…`). HelperLaterTable returned 99 at runtime and
  stayed a static unknown (`dataset-later-table-unsupported`).
- Python guards at the frozen head: 39 `test_sqlserver_*`, 20 DNN harness, 11 linked provider
  passed. Private-path guard and `git diff --check 834d74fe8..HEAD` passed.
## Delivery
Docs-only results commit on top of `d39acb8cd`, next-slice prompt
`prompts/continue-sqlserver-internal-helpers.md`, branch pushed. No PR, merge, ACK-policy change
or review request. Not DNN, private-application, deployment or migration acceptance.
