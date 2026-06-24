# Route Flow Service/Data Composition Final Implementation State

Status: implementation-pr1-ready-for-pr-loop
Readiness: product-slice-implemented-and-validated
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

No product code, site files, generated outputs, rule catalog entries, or sample
artifacts are changed by this branch.

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
- `./scripts/demo-public.sh /tmp/tracemap-route-flow-final-demo`: passed.
  Generated artifacts stayed under `/tmp`.
- Explicit public-safe route-flow smoke over the generated endpoint combined
  index passed:
  `dotnet run --no-build --project src/dotnet/TraceMap.Cli -- route-flow --index /tmp/tracemap-route-flow-final-demo/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out /tmp/tracemap-route-flow-final-demo/reports/route-flow/endpoint-to-sql`.
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

### Oddities

- The duplicate-root gap reuses `SelectorNoMatch` with
  `combined.route-flow.selector.v1`, matching existing route-flow selector
  filter behavior. No new gap code was added.
- The focused test intentionally leaves both static root traces visible because
  the rows are still evidence-backed review context; the summary downgrade is
  what prevents a clean conclusion.

### Follow-Ups

- Complete the remaining Task 5 direct-call tests: no direct call under full
  coverage, no direct call under reduced coverage, cycles, and deterministic
  ordering. This is the remaining Task 5 PR-1 backlog after the duplicate-root
  sub-slice.
  - No direct call under full coverage: existing
    `Route_flow_emits_no_route_flow_evidence_only_after_clean_bridge_checks`
    covers the clean bridge/no downstream evidence shape; additional
    MissingCallEdge-specific full-coverage dead-end coverage remains a Task 5
    follow-up.
  - No direct call under reduced coverage: deferred to the next Task 5
    follow-up because this PR touched duplicate-root selector ambiguity only.
  - Cycles: deferred to the next Task 5 follow-up; live traversal is already
    cycle-safe in code, but this final-spec task remains unchecked until a
    duplicate-root/direct-call-specific regression is added.
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
- Run the ACK PR loop before merge readiness is claimed.

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
