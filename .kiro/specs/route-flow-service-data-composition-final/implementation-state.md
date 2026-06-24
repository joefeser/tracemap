# Route Flow Service/Data Composition Final Implementation State

Status: ready-for-implementation
Readiness: ready-for-implementation
Spec branch: `codex/spec-route-flow-service-data-composition-final`
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
  - Task 4 now explicitly audits `route-flow-endpoint-composition` ownership.
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
