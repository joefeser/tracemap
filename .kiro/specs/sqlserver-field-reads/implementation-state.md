# Implementation state — Phase 6

## PR #919 scope-preservation follow-up

ACK authorized one new Baz finding on `9980043fd`: the presence optimization did not
consult registered coverage and could omit the explicit reduced-scope refusal.
Omission now requires current registered, complete, error-free coverage before consulting
the immutable syntax cache. Scope metadata is deliberately not cached. C#/VB tests cover
complete absence, excluded Field caller, compiler errors and no registration, including
pre-registration extraction and re-registration after cached absence. Extractor versions
are 0.22.23 / 0.10.26. First development compile failed because the test used a nonexistent
Project.WithFilePath API; corrected to ProjectInfo.Create. Focused extraction gate: 109 passed.
First full gate at `2095fea75`: 5,407 passed, 2 failed, 3 expected skips (34m54s).
Both failures were the prior C#/VB near-limit fixture's omission assertion: that fixture
used an unregistered direct compilation. It now explicitly registers its complete synthetic
document scope before expecting omission. Artifact limits and expected admission are unchanged.
Failure receipt is retained at `/tmp/tracemap-pr919-scope-full/pr919-scope-full.trx`.
Final frozen head `bfe395d40a613d59db1a133f94c1fc63313d8a60`: 111 focused passed;
full Debug `-warnaserror` 5,409 passed, 3 expected skips, zero failures (38m59s).
Fresh TRX: `/tmp/tracemap-pr919-scope-full-final/pr919-scope-full-final.trx`.
All nine SQL smokes passed sequentially with unchanged denominators; Field 78/36/42
and 52 refusals (26 per language). Pinned OSS: 12,949 / 114,806 facts, reduced coverage,
no SQL calls; not SQL application evidence. Receipts: `/tmp/pr919-bfe395-*/`.
59 Python guards and private-path/diff guards passed. One initial DDL smoke invocation
omitted mandatory `--json-smoke` and exited at argument parsing; corrected invocation passed.
Core hash `eab482c4e832c543c4ce9282ee4ee39ec286459b2777295cd922bcd342101340`
matches the Field receipt after all gates. No live SQL rerun for this scope-metadata-only
repair; prior live evidence is historical, not current-head runtime acceptance.

## PR #919 review repair

Exact-head ACK 0.5.9 authorized five independent/mixed findings at `035f4890d`.
Repairs: reject bounded state/site with non-null gap; run and report the 26
refusals independently for C# and VB (52 total); test unsigned trusted-name
lookalike assemblies; conservatively omit Field state only when bounded
compilation-wide token inspection proves absence of any possible Field call.
Uncertain scopes and possible cross-document calls keep all refusal analysis.
No facts/record bound was raised. Native C#/VB tests accept just below 64 MiB
and refuse above it. Their first development form incorrectly padded one record
beyond the independent 4 MiB record bound; fixed the synthetic test shape, not
either production limit. First attempt: two test failures; corrected focused
gate: 61 passed. SQL Python guards: 59 passed.

Raw-state provenance feedback is dispositioned architecturally: an embedded scan
property is not a new standalone derived export. Existing site digests and the
derived report bind the complete projection and exact generator; a new regression
checks generator identity, stable input hash and changed-state hash sensitivity.
No raw-fact authenticity claim was added. Prior 26-refusal evidence was C# only;
the historical record is corrected rather than relabeled as a VB pass.

Extractor versions 0.22.22 / 0.10.25 avoid colliding with the stacked child's
already-recorded 0.22.21 / 0.10.24. Child integration must retain these newer
versions and update its aggregate Field refusal expectation from 26 to 52.
Frozen repair implementation `edc67537ddf694f3938292e05112cf3ff32b034e`:
- Full Debug `-warnaserror`: 5,407 passed, 3 expected skips, zero failures,
  33m 20s. TRX: `/tmp/tracemap-pr919-review-full/pr919-full.trx`.
- Field smoke: 78/36/42 unchanged; 26 C# + 26 VB refusals (52 total), all
  refused and language-labelled; repeated reports byte-identical.
- Retained DataTable 108/24/176 (22 refusals), DataSet 156/24/176 (24), private
  helper 84/30/116 (24), internal helper 72/24/60 (24), unchanged and passing.
- Pinned OSS C#/VB: 12,949 / 114,806 facts, expected reduced/partial analysis;
  no SQL calls in these repositories, so no new SQL coverage claim.
- 61 focused tests and 59 Python SQL guards; private-path/whitespace checks pass.
- Source/HEAD/CLI/Core bytes frozen throughout final gates. Outputs under
  `/tmp/pr919-edc675-{field,datatable,dataset,helper,internal,oss}`.
This result record is docs-only. No live SQL rerun: this repair changes admission,
metadata emission and test evidence, not the provider/runtime matrix. No private
inputs, child checkout changes, ACK policy changes, new review spend or merge.
Settlement remains a separate exact-head ACK operation after the validated push.

## Original implementation history
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
  75 cases per language, 78 / 36 / 42 both languages, 26/26 C# refusal checks (not per-language; corrected by PR review),
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
