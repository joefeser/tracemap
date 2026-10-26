# Implementation state — Phase 2
Branch `codex/sqlserver-datatable-consumers`, worktree
`~/.codex/worktrees/sqlserver-datatable-consumers/tracemap`, created from fresh `origin/dev` =
`4f664daf5` (#913 merge; Phase 1 implementation `c28dbd3df` verified as ancestor). The user
checkout (`codex/sqlserver-result-column-prompt`) and all other worktrees were not touched.
## Scope decisions
- State lives on the existing Fill DatabaseOperationCandidate (`operationKind`
  `data-adapter-fill`) as `sqlServerDataTableState`; no new fact type. Every Fill fact gets the
  property (unknown when outside the subset), mirroring how reader state covers every
  ExecuteReader fact.
- `new DataTable(name)` is unsupported: TableName feeds the adapter's table-mapping lookup;
  deferred to keep the fresh-table/empty-mapping argument trivial.
- `Field<T>` is unsupported: it lives in System.Data.DataSetExtensions, outside the trusted
  ADO.NET assembly set, and would widen trust.
- Case-only names stay unknown although DataTable lookup falls back case-insensitively at
  runtime. Duplicate shape names make absent-name results unknown because Fill renames
  duplicates (`Name1`).
- New fixture directory so the retained #912/Phase 1 fixtures, denominators and receipts are
  unchanged; shapes are reused from `samples/sqlserver-result-column-evidence`.
- The #912 command classification and unsupported-flow predicate were extracted into shared
  helpers (`SqlServerReaderColumns.ClassifyCommand` / `UnsupportedFlow`) in a separate
  behavior-preserving commit; the reader lane (368 tests) passed unchanged before any new code.
## Oddities noticed
- First dev scan of the new fixtures (dirty tree, before any comparison code): 180 of 184
  Fill states matched the authored gaps. Three mismatches were code defects, fixed in code:
  VB reduced extension calls (`AsEnumerable`, `Field(Of T)`) carry the receiver as `Instance`
  rather than as an argument (now classified by `ReducedFrom` in both shapes); one was an
  authoring inconsistency: the documented check order ran the implicit-receiver flow check
  before adapter construction, so `new SqlDataAdapter { SelectCommand = ... }` reported
  `implicit-receiver-unsupported` instead of the matrix's `adapter-initializer-unsupported`.
  Construction checks now precede the implicit-receiver check; the order line in design.md was
  corrected to match. No expectation was changed to fit output.
- The CLI facts input initially reused the 4 MiB public-artifact bound; the new fixture's scan
  `facts.ndjson` is ~7 MiB, so the first dev smoke failed `input-size-limit`. The DataTable
  route now reads facts with an explicit 64 MiB bound; the existing routes are unchanged (their
  fixtures stay below 4 MiB).
- `rows-count+foreach` was the first combined guard spelling; System.Text.Json escapes `+`
  (`\u002B`), so the Python mirror's digest disagreed with Core. Renamed to
  `rows-count-and-foreach` before any acceptance run.
## Development validation (dirty tree at d3bce6012; not acceptance)
- Dev DataTable smoke `/tmp/tm-dt-smoke-dev3`: 148 cases per language, 108 / 24 / 176 (expected =
  observed), 0 gaps, 22/22 refusals each for its intended reason, ~2m18s.
- New test class 59 passed on first run; a deliberate two-row expectation mutation made both
  native theories fail (then restored). Focused lane (new + table + result + DDL + parameter +
  catalog + DNN + Razor + SQL matrix + VB data boundary) 581 passed with `-warnaserror`.
- Python guards: 24 `test_sqlserver_*` (6 new) + 17 DNN passed.
## Committed-head acceptance (implementation `f4f74f01cc92867f3542c41ee110a0378d6d9145`)
All gates below ran at the implementation commit with source, HEAD and shared build outputs
frozen (`git status` empty and HEAD unchanged before and after); no competing build, smoke or
commit ran in this checkout during the full suite. Results are recorded by a later
documentation-only commit whose SHA differs.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,136 passed, 0 failed, 3 expected skips** (HttpReportProvenanceInterop receipt env unset,
  Oracle full golden sweep opt-in, Windows-only deep projectless ASP.NET publish); 37m 43s tests,
  2,274 s wall; zero compiler/analyzer warnings. (Timing recorded only; test restructuring
  stays deferred.)
- Then sequentially, new directories outside Git (`/tmp/tracemap-dt-f4f74f0-*`):
  - JSON 52-case parameter smoke: **8/12/32**, 17 s, generator `16f7b62d…` (unchanged).
  - DDL 52-case parameter smoke reusing those scans: **8/12/32**, 22 s, generator `118db6e0…` (unchanged).
  - Retained result smoke: JSON **36/10/36**, DDL **36/10/58**, **20/20 refusals**, 0 gaps, 62 s,
    generator `ce53c852…` (script unchanged).
  - Retained table smoke: **64 cases per language, 44/10/88**, **24/24 refusals**, 0 gaps, 65 s,
    generator `7c40fa9d…` (script unchanged since the #913 repair).
  - New DataTable smoke: **148 cases per language, 108/24/176** (per language 54/12/88,
    expected = observed), 0 gaps, **22/22 refusals**, byte-identical JSON+Markdown repeats,
    130 s, generator `08946e0d0266cd688240deff2a8b4ba18e52a0ffc6f18672b5cdd83ecf7fa7dc`, bounded
    input `3bfeca89d6a36fdad3af9ed778d61a9e0551ebd30667212f1f56334f630d7d9b`.
  - Core SHA-256 `4a43b45da23025fbfb1a674c525c9f2332795e5255e3cc9ce4deb06e6727d631` in every
    Core-bound receipt; ScriptDom parser unchanged `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Reader outputs for the retained fixtures did not change: the retained result and table smokes
  reproduce their denominators and refusals exactly; only the extractor version string moved.
- Pinned OSS adapter smoke (`TRACEMAP_SKIP_BUILD=1
  TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic`, public
  pinned clones fetched into a fresh cache): exit 0, all artifacts each; C# `csharp-semantic/0.22.14`
  12,949 facts, VB `vb-semantic/0.10.17` 114,806 facts (same counts as the #912 record), both
  `Level1SemanticAnalysisReduced` / `FailedOrPartial` (documented expected partial coverage).
  Neither repo has a DatabaseOperationCandidate or `sqlServerDataTableState`, so this only shows
  the change does not disturb unrelated extraction; DataTable evidence is covered by the
  synthetic corpus and direct tests only. Non-.NET adapters, Windows and private/application
  checks not run (no such code changed).
- Private-path guard and `git diff --check 4f664daf5..HEAD` passed at the implementation commit.
## Delivery
Docs-only results commit on top of `f4f74f01c`, next-slice prompt
`prompts/continue-sqlserver-dataset-consumers.md`, branch pushed. No PR, merge, review request,
SQL execution, container or private input.

## PR #914 clean-fixture repair
- CI's rest shard ran successfully but the two native fixture theories failed with
  `FailedOrPartial`: both new fixture projects were missing from the test project's
  restore/build graph. Prior explicit local fixture restores masked the omission.
- Added both projects as `ReferenceOutputAssembly="false"` references, matching the
  existing SQL fixtures. Added a Python guard for both unconditional references.
- Moved both fixtures' generated bin/obj directories aside, then ran the normal test
  project command (no separate fixture restore): both projects restored and built,
  and all 59 DataTable tests passed with Debug `-warnaserror`.
- All 25 SQL Python guards, private-path and whitespace checks passed. This is a
  test-build wiring repair; no extractor, comparison or smoke semantics changed.
  The full suite was not repeated locally for this leaf repair; CI must validate
  the pushed head. ACK reported no current findings or unresolved review threads.
