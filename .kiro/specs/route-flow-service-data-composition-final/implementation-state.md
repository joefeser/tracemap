# Route Flow Service/Data Composition Final Implementation State

Status: implementation-pr2-cycle-gap-ack-patch-ready-to-push
Readiness: focused-cycle-slice-implemented-reviewed-ack-patched-and-validated
Spec branch: `codex/spec-route-flow-service-data-composition-final`
Implementation branch: `codex/implement-route-flow-service-data-composition-final`
Target base: `dev`
Primary issues: `#159`, `#179`, `#201`
Public claim level: static evidence only

## Summary

This is a spec-only completion packet for the remaining route-centered static
service/data composition work. The target product behavior is a conservative
`tracemap route-flow` trace that can start at endpoint/root method evidence,
stitch into service methods, continue through review-tier implementation
candidates where rule-backed static evidence allows, and render data/query/
dependency/value-origin rows or narrower gaps with evidence provenance.

The product implementation PR for this spec currently changes route-flow
reporting code, focused route-flow tests, and this spec state/task tracking.
No site files, generated outputs, rule catalog entries, scanner extraction
logic, LLM/vector/prompt logic, or sample artifacts are changed by this branch.

## Source Material Reviewed

- `AGENTS.md`
- GitHub issue #159: Add route-centered static call flow report
- GitHub issue #179: Compose route-flow evidence through service and data facts
- GitHub issue #201: Route-flow should stitch endpoint roots through call edges
  and implementation relationships
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/route-flow-service-data-composition-next/`
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `.kiro/specs/route-centered-static-flow-report/`
- `.kiro/specs/route-flow-endpoint-stitching/`
- `scripts/kiro-review.mjs`

## Scope Decisions

- This spec is a continuation and completion packet, not a rewrite.
- The implementation target remains `tracemap route-flow`.
- The JSON report type remains `route-flow`.
- JSON version remains `1.0`.
- The route-flow classification vocabulary remains unchanged.
- The `combined.route-flow.*` rule namespace remains the authority.
- The next implementation PR should focus on one route-centered composition
  slice: endpoint/root method to service/data trace completion over existing
  combined evidence, plus associated gaps, downgrades, deterministic output,
  and safety tests.
- This spec owns only service/data/query/dependency/value-origin composition and
  downgrade behavior for selected route-flow rows. It does not own broad
  endpoint trace summaries, site copy, scanner extraction, runtime proof, or
  public marketing claims.
- Interface/implementation evidence remains static candidate context and cannot
  prove runtime DI target selection.
- Adjacent but unjoined data/query/dependency/value-origin facts become scoped
  gaps or path-context rows, not inferred executed path edges.

## Safety Boundaries

The spec forbids committing private local paths, private sample names, private
route strings, raw SQL, raw config values, connection strings, secrets, raw
endpoint URLs, query strings, raw remotes, source snippets, hostnames, private
labels, or generated private outputs.

The implementation must not claim runtime request execution, endpoint
reachability, dependency-injection binding, dynamic dispatch, branch
feasibility, authorization behavior, SQL execution, database state, data
contents, production traffic, release approval, AI impact analysis, LLM
analysis, embeddings, vector search, semantic search, fuzzy matching, or prompt
classification.

## Review Plan

Required review loop for this spec-only branch:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patch Medium+ actionable findings. Then run one bounded final re-review with
Sonnet or Opus and record the command, coverage, artifact, findings, and
disposition here. If either model or tool path is unavailable or times out,
record the exact command, exit state, and evidence; do not invent approval.

## Review Log

Initial Opus spec review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T030749-645Z-spec-claude-opus-4.8.clean.md`
- Session: `050a3cfd-e4e2-4c6f-b03c-353aa433e92b`.
- Findings patched:
  - canonical sort order mismatch between requirements and design;
  - already-shipped versus remaining delta was too implicit;
  - gap-code versus rule-ID wording could encourage a parallel namespace;
  - implementation task-to-PR-boundary mapping was too loose;
  - missing tests for `--exit-code`, `classificationCap`, context-group weakest
    rollup, redaction rule citation, single-candidate caps, and Tier3-only
    downstream evidence.
- Local disposition for branch/diff blocker: the shared checkout had unrelated
  untracked/staged spec folders from other work. The final commit was prepared
  from a clean temporary worktree on
  `codex/spec-route-flow-service-data-composition-final`, and only this spec
  folder is staged there.

Initial Sonnet spec review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T031107-726Z-spec-claude-sonnet-4.6.clean.md`
- Session: `45ee925f-88e6-4427-ae8f-90ea5b07b673`.
- Findings patched:
  - review and validation state needed to be completed before merge;
  - strong classification needed an explicit Tier1/Tier2 stitched downstream
    evidence prerequisite;
  - full relevant coverage needed a concrete definition;
  - task scope needed to make clear that Tasks 5-10 are sequenced across PR
    boundaries, not one large implementation PR;
  - task 4 needed explicit gap-code catalog and live JSON field audits.

Bounded Sonnet re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T031524-218Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `a8b690b0-021d-47b8-b0d3-75fffafcbb16`.
- Findings patched after re-review:
  - relationship table now acknowledges an optional
    `static-dispatch-candidate-bridges` shared candidate contract when present
    on the implementation base;
  - duplicate/ambiguous endpoint root gap mapping now names `SelectorNoMatch`,
    `MissingRouteRoot`, and the need to amend `combined.route-flow.gap.v1` for
    any new code;
  - design now references Requirement 5.8 as the normative full-coverage
    definition to avoid drift;
  - additive field list now warns implementers to audit existing fields before
    treating them as new work.

Final readiness note: no product code was changed. Medium+ actionable spec
findings from the usable Opus/Sonnet reviews were patched. Remaining Kiro
re-review blockers were process-state observations that this file and
`tasks.md` were pending at review time; those are completed in this state
update.

Final Sonnet re-review from the clean worktree:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T032003-892Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `993d3ffa-0eb8-4b58-8a4c-0bf983293a95`.
- Result: no blocking findings. The review said the spec is ready to merge as
  is and suggested two optional clarifications. Both were patched:
  - Task 4 now explicitly audits requested predecessor spec ownership.
  - Follow-up notes now explain that `static-dispatch-candidate-bridges` is a
    conditional reference and may not exist on the implementation base.

## Validation Plan

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Also discover whether a dedicated spec/docs validation command exists. If none
exists, record the discovery command and result here.

Product implementation validation is documented in `tasks.md` and `design.md`,
but it is intentionally deferred because this branch is spec-only.

## Validation Log

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Spec/docs validation discovery:
  `find scripts -maxdepth 1 -type f | sort | rg 'spec|kiro|validate|lint|check'`
  found `scripts/check-private-paths.sh`, `scripts/kiro-review.mjs`, and
  `scripts/validate-legacy-codebases.sh`; no separate dedicated spec lint
  command was identified.
- Safety scan:
  a targeted `rg` scan for local path, raw URL, raw SQL, secret/config, private
  key, and private-route patterns returned no matches before this validation
  note was recorded.
- Diff scope in the clean worktree:
  `git status --short --branch` showed only
  `.kiro/specs/route-flow-service-data-composition-final/` as untracked before
  staging. Unrelated untracked/staged spec folders observed in the original
  shared checkout were not staged or committed for this PR.

## PR Loop Notes

- PR #311 opened against `dev` from
  `codex/spec-route-flow-service-data-composition-final`.
- First ACK PR loop returned `actionable_findings` with five unresolved Gemini
  review threads.
- Patched review findings:
  - changed the JSON `reportCoverage` example from placeholder wording to the
    live `FullEvidenceAvailable` value;
  - moved duplicate/ambiguous endpoint-root mapping out of the gap-code bullet
    list;
  - fixed a split Markdown code span for
    `route-centered-endpoint-trace-completeness`;
  - changed the Task 4 audit reference to requested predecessor specs so it
    does not introduce an extra ownership dependency.
- Post-patch validation: `git diff --check`, `./scripts/check-private-paths.sh`,
  and the targeted safety scan passed.

## Implementation Guidance For Next PR

Recommended next implementation flow:

1. Audit live `dev` route-flow state and update this file with exactly which
   unchecked task is selected.
2. Choose the smallest bridge/gap slice that improves endpoint/root method to
   service/data trace completion without broad refactors.
3. Add focused synthetic tests first where practical.
4. Patch route-flow report code only where evidence-backed tests expose a live
   gap.
5. Run focused route-flow tests, full .NET build/test, safety checks, and
   relevant pinned route-flow/reporting smokes or record explicit deferrals.

## Product Implementation PR 1

Branch: `codex/implement-route-flow-service-data-composition-final`
Base: `origin/dev` at `848e76f7eff53b6ddf427f50d8971d9bbd78bb8d`

Selected slice: Task 4 audit plus the smallest Task 5/endpoint-stitching gap
left by the live audit: duplicate normalized endpoint roots now emit a
deterministic selector ambiguity gap and downgrade the route-flow report instead
of allowing duplicate roots to look like a clean strong route-flow conclusion.

### Live Audit Notes

- `CombinedRouteFlowReport` already preserves `reportType = "route-flow"`,
  JSON `version = "1.0"`, the five `RouteFlowClassifications` values, the
  existing `combined.route-flow.*` rules, and the existing command surface.
- Live JSON top-level fields are `reportType`, `version`, `reportCoverage`,
  `coverageWarnings`, `query`, `snapshot`, `summary`, `entryEvidence`,
  `flowRows`, `logicRows`, `dependencySurfaces`, `touchedFiles`,
  `touchedSymbols`, `gaps`, `limitations`, and additive `contextGroups`.
- Existing implementation already covers route/root method bridges, downstream
  static call rows, object creation rows, parameter-forward traversal,
  interface candidate rows, attached query/data/dependency rows, fact-symbol and
  argument projection rows, context groups, touched files/symbols, schema gaps,
  identity/reduced-coverage gaps, redaction, byte-stability, and rule-catalog
  resolution tests.
- Existing route-flow gap catalog entries under `combined.route-flow.gap.v1`
  include the design taxonomy needed by this slice: `SelectorNoMatch`,
  `MissingRouteRoot`, `MissingMethodSymbolBridge`, `MissingCallEdge`,
  `MissingImplementationBridge`, `DataSurfaceAttachmentMissing`,
  `SchemaMissing`, `ExtractorUnavailable`,
  `ImplementationCandidateUnavailable`, `AmbiguousImplementationCandidates`,
  `IdentityGap`, `RuntimeBindingNotProven`, `ReducedCoverage`,
  `UnknownCommitSha`, `NoRouteFlowEvidence`, `UnknownAnalysisGap`,
  `TraversalBounds`, `TruncatedByLimit`, and `UnsafeValueOmitted`.
- Requested predecessor specs were audited for ownership boundaries:
  `route-centered-static-flow-report`, `route-flow-service-data-composition`,
  `route-flow-service-data-composition-next`,
  `route-centered-endpoint-trace-completeness`, and
  `route-flow-endpoint-stitching`. A conditional
  `static-dispatch-candidate-bridges` spec exists on this base.
- The unchecked predecessor gap that fit this PR boundary was duplicate
  normalized route roots. Broader direct-call, no-call, candidate, attachment,
  truncation, and smoke-hardening work remains follow-up scope.
- `classificationCap` was audited as an allowed additive field but is not
  emitted by the live route-flow model on this base. This PR does not add it
  because the selected duplicate-root slice needs only existing gap,
  classification, and coverage fields. If a future candidate or downgrade slice
  adds `classificationCap`, that PR should add the corresponding field tests.
- No new command, report type, JSON version, traversal engine, persisted rows,
  rule namespace, LLM calls, embeddings, vector database, semantic search, or
  prompt-based classification were introduced.

### Implementation Notes

- Added endpoint-root ambiguity detection in `CombinedRouteFlowReport` after
  source-scoped route/client root resolution and before traversal.
- When a normalized selector resolves to multiple endpoint roots, route-flow now
  emits a single deterministic `SelectorNoMatch` gap using
  `combined.route-flow.selector.v1`, `Tier4Unknown`, `ReducedCoverage`, a safe
  source label, affected root node ID, supporting route fact IDs, file span, and
  limitations.
- The report still renders static rows for the matched roots as review context,
  but the blocking selector gap forces `ReducedCoverage` and
  `UnknownAnalysisGap`; narrowing with `--from-source` removes the ambiguity.
- Added a synthetic two-source regression test proving the duplicate-root gap,
  downgrade, safe supporting evidence, and narrowed-selector behavior.
- Preserved the existing route-flow pattern where selector-miss gaps that arise
  from selector handling cite `combined.route-flow.selector.v1`; the broader
  `combined.route-flow.gap.v1` catalog entry still documents `SelectorNoMatch`
  as a public gap kind.

### Validation Log For PR 1

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`: passed, 32 tests after the first implementation slice and 33 tests after Kiro re-review patches. NuGet emitted existing
  `SQLitePCLRaw.lib.e_sqlite3` high-severity vulnerability warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 632 tests before review
  patches, 633 tests after the first Kiro patch set, and 634 tests after the
  final CLI precedence test, with the same existing `SQLitePCLRaw.lib.e_sqlite3`
  NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `./scripts/demo-public.sh <temporary-public-demo-output>`: passed.
  Generated artifacts stayed under a temporary output directory.
- Explicit public-safe route-flow smoke over the generated endpoint combined
  index passed:
  `dotnet run --no-build --project src/dotnet/TraceMap.Cli -- route-flow --index <temporary-public-demo-output>/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out <temporary-public-demo-output>/reports/route-flow/endpoint-to-sql`.
  It produced `route-flow-report.md` and `route-flow-report.json` with
  `reportType = "route-flow"`, `version = "1.0"`, static rows, dependency
  surfaces, and reduced-coverage gaps.
- Targeted safety scan of the explicit route-flow smoke artifacts found no
  raw local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, or raw GitHub remote matches.

### Kiro Implementation Review

Initial Sonnet implementation review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T041519-756Z-implementation-claude-sonnet-4.6.clean.md`
- Session: `49738086-13df-4a6c-9e32-aa0a19324c44`.
- Medium+/blocking findings patched:
  - added an explicit summary-level non-strong assertion for single
    implementation-candidate continuation;
  - added a strong-route-root plus Tier3 downstream service edge fixture proving
    Tier3 downstream evidence cannot satisfy `StrongStaticRouteFlow`;
  - recorded the `classificationCap` audit and deferral for this PR boundary.
- Non-blocking finding disposition:
  - duplicate-root selector ambiguity intentionally cites
    `combined.route-flow.selector.v1`, matching existing selector gap behavior;
    this state file records the decision.

Bounded Sonnet implementation re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T041939-659Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `3e3af9fe-ed24-4551-8d16-0d0a56ca299a`.
- Medium+/blocking findings patched or dispositioned:
  - explicit Task 5 follow-up mapping was added below for each still-unchecked
    Requirement 8.1 subcase;
  - added route-flow CLI validation-error precedence coverage for `--exit-code`
    so argument validation returns before classification mapping;
  - tightened the single implementation-candidate test to assert the summary
    cannot upgrade to `StrongStaticRouteFlow` or `ProbableStaticRouteFlow`. The
    live fixture currently rolls up to `UnknownAnalysisGap`, which is weaker
    than `NeedsReviewStaticRouteFlow` and remains inside the spec cap.

Final bounded Sonnet re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T042537-020Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `407ab576-8775-4499-8fcf-7b22d171371e`.
- Disposition:
  - corrected this state file so it no longer claims a positive
    `NeedsReviewStaticRouteFlow` summary assertion for the single-candidate
    fixture;
  - recorded the `NoRouteFlowEvidence` `--exit-code` follow-up below;
  - no further Kiro re-review was run because this was the second bounded
    re-review cycle and no product-code safety blocker remained.

### ACK PR Loop Review

Initial ACK PR loop for PR #318 waited for the required Codex/Qodo batch before
authorizing patches. After both required reviewers returned, ACK reported
`actionable_findings` with three unresolved review threads and
`patchAuthorized=true`.

ACK-authorized findings patched in the follow-up commit:

- preserved duplicate endpoint-root ambiguity gaps on the no-terminal early
  return path instead of dropping them when the selected terminal surface has no
  reachable nodes;
- removed redundant resorting inside duplicate-root gap generation by relying
  on the already sorted route-root list;
- added optional commit/extractor metadata to `RouteFlowGap` and threaded the
  duplicate-root root commit, extractor name, and extractor version into emitted
  evidence.

After the first follow-up commit was pushed, ACK reported one additional
actionable Qodo top-level finding about unbounded ambiguity-gap allocations.
The second local ACK patch:

- caps duplicate-root gap `SupportingFactIds` at 20 using a bounded sorted
  prefix, matching existing route-flow gap payload conventions;
- replaces the gap-ID `string.Join` over all ambiguous root IDs with an
  incremental SHA-256 hash over the deterministic root sequence;
- adds a truncation limitation that reports the cap and non-empty supporting
  fact reference count;
- adds a regression fixture with 25 duplicate route roots proving the cap and
  stable repeated gap ID.

Focused validation after the local ACK patch:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 33 tests, with the existing `SQLitePCLRaw.lib.e_sqlite3` NuGet
  vulnerability warnings.
- After the second ACK patch, the same focused command passed, 34 tests, with
  the same existing NuGet warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- First post-ACK `dotnet test src/dotnet/TraceMap.sln` run had one unrelated
  diagnostic test miss for `NuGetRestoreFailed` in generated artifact text.
  The single failing test then passed in isolation, and a full solution rerun
  passed, 634 tests, with the same existing NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `./scripts/demo-public.sh <temporary-public-demo-output>`: passed.
- Explicit route-flow smoke over the refreshed public endpoint combined index
  passed and produced `UnknownAnalysisGap`, `ReducedCoverage`, 3 entry evidence
  rows, 8 static flow rows, 5 business/data logic rows, 4 dependency surfaces,
  and 45 gaps.
- Targeted safety scan of the refreshed route-flow smoke artifacts found no
  raw local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, raw GitHub remote, or private feed/token matches.
- After the second ACK patch, `dotnet build src/dotnet/TraceMap.sln` passed,
  `dotnet test src/dotnet/TraceMap.sln` passed with 635 tests,
  `./scripts/check-private-paths.sh` passed, and `git diff --check` passed.
- The explicit route-flow smoke was rerun against the refreshed temporary
  public demo endpoint combined index and again produced `UnknownAnalysisGap`,
  `ReducedCoverage`, 3 entry evidence
  rows, 8 static flow rows, 5 business/data logic rows, 4 dependency surfaces,
  and 45 gaps. A targeted artifact scan again found no raw local path, raw SQL
  wildcard, private connection-string sample, password token, raw URL, raw
  GitHub remote, or private feed/token matches.

### Oddities

- The duplicate-root gap reuses `SelectorNoMatch` with
  `combined.route-flow.selector.v1`, matching existing route-flow selector
  filter behavior. No new gap code was added.
- The focused test intentionally leaves both static root traces visible because
  the rows are still evidence-backed review context; the summary downgrade is
  what prevents a clean conclusion.

### Follow-Ups

- Complete the remaining Task 5 direct-call tests: no direct call under full
  coverage, no direct call under reduced coverage, and deterministic ordering.
  This is the remaining Task 5 PR-1 backlog after the duplicate-root and
  cycle-gap sub-slices.
  - No direct call under full coverage: existing
    `Route_flow_emits_no_route_flow_evidence_only_after_clean_bridge_checks`
    covers the clean bridge/no downstream evidence shape; additional
    MissingCallEdge-specific full-coverage dead-end coverage remains a Task 5
    follow-up.
  - No direct call under reduced coverage: deferred to the next Task 5
    follow-up because this PR touched duplicate-root selector ambiguity only.
  - Cycles: covered in the takeover cycle-gap slice. Source-local service-call
    cycles now emit a deterministic `TraversalBounds` gap, suppress inherited
    clean `NoRouteFlowEvidence` path gaps when blocking endpoint-composition
    gaps are present, and downgrade the route-flow summary to
    `UnknownAnalysisGap`.
  - Deterministic ordering: existing route-flow smoke and primary route-flow
    test cover byte-stable repeated output, but duplicate-root-specific row/gap
    permutation coverage remains a Task 5 follow-up.
- Add a `NoRouteFlowEvidence`-specific `--exit-code` non-zero assertion in the
  next Task 9 PR boundary. This PR added validation-error precedence coverage
  for `--exit-code`, and existing route-flow CLI coverage still proves non-zero
  behavior for review/unknown summary mappings.
- Add the broader Task 10 negative safety matrix for raw SQL, raw config, URLs,
  private labels, snippets, remotes, and logs in the attached-row/public-safety
  PR boundary. This PR preserved existing route-flow safety checks and ran the
  private-path guard plus explicit route-flow artifact scan.
- Continue Task 6/7 follow-ups for candidate continuation and unjoinable
  service/data/query/dependency/value-origin attachment precision.
- For Task 7 projection/attachment precision, cap or batch large SQL parameter
  lists used when reading argument-pair and fact-symbol projection rows so
  large selected route-flow graphs do not exceed SQLite parameter limits.
- Run the ACK PR loop before merge readiness is claimed.

## Product Implementation PR 2 Takeover

Branch: `codex/implement-route-flow-service-data-composition-final`
Base: `origin/dev` at `1e9c56602d18a9e62f4523a66314d3755553b4fa`

Takeover notes:

- Reused the existing implementation worktree and branch.
- Fetched latest `origin/dev`; the previous duplicate-root slice was already
  merged as PR #318, so the branch fast-forwarded to current `origin/dev`.
- Left generated `.agent-control/tmp/` output untracked and unstaged.
- Selected the smallest remaining Task 5 cycle-gap slice rather than broadening
  into candidate continuation, attachment precision, scanner extraction, site
  work, or rule-catalog changes.

### Cycle-Gap Scope

- Endpoint route-flow traversal now records skipped source-local cycle edges
  when a method cannot expand without revisiting a node already in the current
  static path.
- The cycle gap reuses `TraversalBounds` under
  `combined.route-flow.gap.v1`, carries Tier4/ReducedCoverage, keeps safe file
  span evidence from the skipped cycle edge, and caps supporting fact IDs at the
  existing route-flow bounded-evidence limit.
- Inherited path-level `NoRouteFlowEvidence` gaps are filtered when endpoint
  composition has blocking gaps, so a known cycle/coverage gap is not rendered
  beside a clean absence claim.
- Added a synthetic regression proving deterministic repeated gap ID,
  rule/evidence/coverage fields, service-file span evidence, no completed flow
  rows, no clean no-evidence gap, and summary downgrade to
  `UnknownAnalysisGap`.

### Validation Log For PR 2

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 35 tests, with the existing `SQLitePCLRaw.lib.e_sqlite3` NuGet
  vulnerability warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet vulnerability warnings.
- First `dotnet test src/dotnet/TraceMap.sln` run had one unrelated
  diagnostic test miss for `NuGetRestoreFailed` in generated artifact text.
  The single failing diagnostic test passed in isolation, and the full solution
  rerun passed, 636 tests, with the same existing NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- After the Kiro B1 patch, focused route-flow tests passed again, 35 tests,
  with the same existing NuGet warnings.
- Final post-Kiro-patch `dotnet build src/dotnet/TraceMap.sln`: passed with
  the same existing `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- Final post-Kiro-patch `dotnet test src/dotnet/TraceMap.sln`: passed, 636
  tests, with the same existing NuGet warnings.
- Final post-Kiro-patch `./scripts/check-private-paths.sh`: passed.
- Final post-Kiro-patch `git diff --check`: passed.
- `./scripts/demo-public.sh /tmp/tracemap-route-flow-pr2-demo-20260624`:
  passed with the same existing NuGet warnings while building the .NET tools.
- Explicit route-flow smoke over the refreshed public endpoint combined index
  passed:
  `dotnet run --no-build --project src/dotnet/TraceMap.Cli -- route-flow --index <temporary-public-demo-output>/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out <temporary-public-demo-output>/reports/route-flow/endpoint-to-sql`.
  It produced `UnknownAnalysisGap`, `ReducedCoverage`, 3 entry evidence rows,
  8 static flow rows, 5 business/data logic rows, 4 dependency surfaces, and
  43 gaps.
- Targeted safety scan of the refreshed route-flow smoke artifacts found no raw
  local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, raw GitHub remote, private feed, secret-token,
  query-token, or connection-string key matches.
- PR #320 opened against `dev` from
  `codex/implement-route-flow-service-data-composition-final`.
- Initial ACK loop for PR #320 waited for the required Codex/Qodo batch before
  authorizing patches. After both required reviewers returned, ACK reported
  `actionable_findings` with three unresolved review threads and
  `patchAuthorized=true`.
- ACK-authorized findings patched locally:
  - changed bounded supporting fact ID emission to sort the selected `List` in
    place before returning it;
  - delayed cycle `TraversalBounds` gap emission until after implementation
    candidate expansion has had a chance to continue traversal;
  - populated cycle gap commit SHA, extractor name, and extractor version from
    available source/edge evidence.
- Added focused regression coverage proving cycle gap metadata and proving no
  premature cycle gap is emitted when a static implementation candidate can
  continue to a terminal surface.
- Focused validation after the local ACK patch:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
  passed, 36 tests, with the same existing NuGet warnings.
- Post-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with the same
  existing NuGet warnings.
- Post-ACK `dotnet test src/dotnet/TraceMap.sln`: passed, 637 tests, with the
  same existing NuGet warnings.
- Post-ACK `./scripts/check-private-paths.sh`: passed.
- Post-ACK `git diff --check`: passed.
- Post-ACK explicit route-flow smoke over the refreshed public endpoint
  combined index passed and produced `UnknownAnalysisGap`, `ReducedCoverage`, 3
  entry evidence rows, 8 static flow rows, 5 business/data logic rows, 4
  dependency surfaces, and 43 gaps.
- Post-ACK targeted safety scan of the refreshed route-flow smoke artifacts
  found no raw local workspace path, raw SQL wildcard, private
  connection-string sample, password token, raw URL, raw GitHub remote, private
  feed, secret-token, query-token, or connection-string key matches.
- Push and ACK rerun are pending in this takeover pass.

### Kiro Implementation Review For PR 2

Initial Sonnet implementation review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T050158-646Z-implementation-claude-sonnet-4.6.clean.md`
- Session: `38454468-69de-4cad-963d-042fcd53de03`.
- Medium+/blocking findings patched or dispositioned:
  - Patched B1 by changing bounded duplicate-root supporting fact selection to
    keep the first distinct fact IDs from deterministic root order and sort
    only the emitted bounded set. Added a regression assertion that the 20
    emitted duplicate-root supporting fact IDs come from the canonical first 20
    route facts, not the lexicographically smallest 20 IDs.
  - Dispositioned B2 as not correct for the live combined graph: the current
    method symbol node can retain caller-file evidence from the incoming call
    fact, while the skipped cycle edge carries the precise service-file span
    where the cycle edge was observed. Keeping the gap affected row on the
    current node and the file span on the skipped edge gives more precise
    evidence provenance for this slice. The regression asserts the service edge
    span.
- Non-blocking findings recorded as follow-ups unless a future PR selects that
  scope: `NoRouteFlowEvidence` and probable-route `--exit-code` boundary tests,
  redaction citation precision, large projection SQL parameter caps, and
  `combined_symbol_relationships` schema-gap coverage.

Bounded Sonnet re-review after the B1 patch:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T050642-522Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `01719e6f-c797-4027-9546-af5a71ee1ff4`.
- Findings:
  - Product-code readiness was accepted for the cycle-gap slice.
  - Remaining blockers were process closure items: record post-patch full
    validation and record the re-review result. Both are now recorded above.
  - Non-blocking notes were to preserve the cycle/no-evidence filtering
    design decision and large projection SQL parameter caps as follow-ups.

### Oddities / Design Decisions For PR 2

- Cycle gaps use `TraversalBounds` under `combined.route-flow.gap.v1`; no new
  gap code or rule namespace was added.
- For skipped cycle edges, the gap keeps the affected row on the method node
  where traversal stopped, but uses the skipped cycle edge file span because
  the live combined method node can retain caller-file evidence from the
  incoming call fact. This keeps the rendered gap span on the observed cycle
  edge.
- Path-level clean `NoRouteFlowEvidence` gaps are filtered when endpoint
  composition emits blocking gaps such as `TraversalBounds`, `MissingCallEdge`,
  or `DataSurfaceAttachmentMissing`, so the report does not render a clean
  absence claim beside a known coverage/composition gap.

## Follow-Up Notes

- Private legacy smoke validation may be useful during implementation, but any
  output must remain local-only and summarized generically.
- Public examples should use synthetic selectors such as
  `GET /api/admin/users/roles` or checked-in public fixtures.
- `static-dispatch-candidate-bridges` is referenced conditionally in
  `design.md` but may not exist as a checked-in spec on the implementation base.
  During Task 4, confirm whether a shared candidate contract is present in live
  code under another name or consume candidate evidence directly through
  existing route-flow interface bridge rows.
- If implementation requires a new gap code or row kind, update
  `rules/rule-catalog.yml` before emitting it and document limitations in the
  implementation state.
