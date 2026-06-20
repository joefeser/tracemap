# Site TraceMap Tools Change Review Brief Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

- Branch: `codex/spec-site-change-review-brief`
- Target base: `dev`
- Base: `origin/dev` at `c3f3967aa825b2da3b29e2cf1ffa018088099273`
- Worktree: isolated spec worktree; absolute local path intentionally omitted
  from the checked-in state file for the private-path guard.
- Pull request: pending until PR creation; final report records the URL.
- Latest spec commit: current branch head; final report records the exact SHA
  to avoid self-referential amend churn in this checked-in state file.

## Scope

- Spec folder ownership:
  `.kiro/specs/site-tracemap-tools-change-review-brief/`
- Spec-only deliverables:
  `requirements.md`, `design.md`, `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- This phase does not implement site code, scanner behavior, reducer behavior,
  generated artifacts, validation scripts, public copy changes, route metadata,
  discovery metadata, sitemap metadata, or browser validation.

## Public Claim Level

- Public claim level: `concept`
- Rationale: the future page is a public-safe review-preparation concept. This
  spec does not cite checked-in public-safe demo evidence for a completed
  change-review brief route, so a stronger claim level is not justified.

## Scope Decisions

- Preferred route for future implementation: `/use-cases/change-review/`.
- Conditional route alternative: `/change-review/`.
- Conditional section alternatives: `/review-room/` or `/packets/` only if a
  future implementation records why section placement is safer than a
  standalone use-case route.
- Required route/placement decision remains open for the implementation phase.
- Required future page sections: `Change Context`, `Evidence Packet`, `Review
  Questions`, `Stop Conditions`, `Next Owners`, `Limitations`, and
  `Non-Claims`.
- Required cross-link candidates for future implementation:
  `/proof-paths/`, `/packets/`, `/review-room/`, `/validation/`,
  `/limitations/`, `/use-cases/endpoint-review/`,
  `/use-cases/incident-review/`, `/static-vs-runtime/`,
  `/review-claim-checklist/`, and `/use-cases/`.
- Current `origin/dev` route check at spec creation found the original
  candidate routes present in `site/src`; Opus review also identified
  `/use-cases/incident-review/` as an adjacent route to include in future
  cross-link and differentiation checks.
- Future implementation must verify all candidate routes in generated output
  before linking.
- Adjacent route differentiation that future implementation must record in
  copy and in this state file: `/team-evidence-handoff/`,
  `/manager-packet/`, `/static-triage/`, `/manager-brief/`, and
  `/deploy-audit/`. If the distinction from `/team-evidence-handoff/` cannot
  be stated crisply, implementation should choose section placement on the
  closest existing page rather than a near-duplicate standalone route.
- Future standalone route validation must follow the existing per-page
  validator convention: `site/scripts/change-review.mjs` exporting
  `validateChangeReviewDist`, registration in `site/scripts/validate.mjs`, and
  `site/scripts/change-review.test.mjs`. Section placement must extend the
  host page's validator and test instead.
- Future implementation must validate that replacement/approval non-claim copy
  renders, and that unsupported replacement or release-approval claims are
  caught outside the sanctioned `Non-Claims` region.
- Future `Evidence Packet` copy must include visible static dependency
  surfaces as references, not runtime behavior proof.
- Future validation must distinguish descriptive private/raw category phrases
  from actual private identifier values. Category phrases belong only inside
  sanctioned regions; actual private repository names, customer names, service
  names, owner names, private sample names, and real internal review dates are
  never allowed and require synthetic examples, private-path checks, and manual
  review because arbitrary values cannot be fully pattern-matched.

## Duplicate-Spec Check

- Checked `origin/dev` for
  `.kiro/specs/site-tracemap-tools-change-review-brief/`: no existing spec
  folder found.
- Checked remote heads matching `site-change-review-brief` and
  `site-tracemap-tools-change-review-brief`: no matching remote branch found.
- Checked open PRs by spec/name search: no matching open PR found.

## Spec Review

- Completed: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind spec --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T202847-148Z-spec-claude-opus-4.8.clean.md`
  - Findings: Medium finding on under-specified sanctioned-region validation;
    Medium finding on adjacent review/use-case cross-link and differentiation
    gaps; Low findings on overclaim-list consistency and explicit discovery
    `publicClaimLevel: concept` validation.
  - Disposition: patched requirements, design, tasks, and this state file to
    require sanctioned section IDs, sanctioned-region-stripped overclaim and
    AI/LLM scans, `/use-cases/incident-review/` cross-link consideration,
    adjacent-route differentiation notes, aligned overclaim vocabulary, and
    explicit discovery claim-level validation.
- Completed: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind spec --model
  claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T203222-989Z-spec-claude-sonnet-4.6.clean.md`
  - Findings: Low finding that `review-packet.md` omitted the newly added
    `/use-cases/incident-review/` cross-link; Low findings that spec-review
    task checkboxes lagged completed Opus/Sonnet review and Opus patch work.
  - Disposition: patched `review-packet.md`, updated spec-review task
    checkboxes, and split the re-review task so remaining work is explicit.
- Completed re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T203331-839Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that design-level differentiation from adjacent
    pages, especially `/team-evidence-handoff/`, remained under-specified;
    Low findings on section-placement validation, incomplete second-model
    review disposition, and conditional sitemap wording.
  - Disposition: patched requirements, design, tasks, and this state file to
    add adjacent-page positioning, strengthen `/team-evidence-handoff/`
    differentiation, define section-placement validation wiring, and name
    sitemap comparison routes.
- Pending: rerun re-review where feasible after these patches.
- Completed second re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T203821-850Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that forbidden-content scan partitioning
    contradicted the established validator pattern; Medium finding that the
    spec did not require a dedicated per-page validator module, registration,
    and tests for a standalone route. Low findings requested concrete
    sanctioned IDs, less collision-prone overclaim patterns, concrete metadata
    artifact names, and a word-count band.
  - Disposition: patched requirements, design, tasks, and this state file to
    define whole-page versus sanctioned-region-stripped scan partitioning,
    require standalone validator module registration and tests or host
    validator extension for section placement, name sanctioned IDs, name
    `og:type=article` and route-index `nonClaims`, and add a 600 to 1600 word
    count range. This word-count range was later superseded by
    neighboring-validator range guidance in the fifth re-review disposition.
- Pending: rerun re-review where feasible after the second re-review patches.
- Completed third re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T204432-494Z-re-review-claude-opus-4.8.clean.md`
  - Finding: Medium finding that replacement/approval boundaries were authored
    but not validated. Low findings requested bounded-anchor manual review,
    primary-navigation recording, section-placement word-count clarification,
    and re-review disposition.
  - Disposition: patched requirements, design, tasks, and this state file to
    require positive replacement/approval non-claim copy validation,
    sanctioned-region-stripped replacement/approval forbidden-copy checks,
    manual bounded-anchor and navigation placement recording, and
    section-placement word-count clarification.
- Pending: rerun re-review where feasible after the third re-review patch.
- Completed fourth re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T204719-615Z-re-review-claude-opus-4.8.clean.md`
  - Finding: Medium finding that `visible static dependency surfaces` was in
    the brief definition but not carried into the required `Evidence Packet`
    structure. Low findings requested neighboring-validator word-count
    alignment, task status cleanup, and private-path-safe state placeholders.
  - Disposition: patched requirements, design, tasks, and this state file to
    require visible static dependency surfaces in the `Evidence Packet`, align
    word-count range selection with neighboring validators, and clarify that
    the absolute local worktree path is omitted from checked-in state for the
    private-path guard.
- Pending: rerun re-review where feasible after the fourth re-review patch.
- Completed fifth re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T205034-599Z-re-review-claude-opus-4.8.clean.md`
  - Finding: Medium finding that a stale `600-word lower bound` phrase
    contradicted the neighboring-validator word-count guidance. Low findings
    noted that iterative re-review rounds are tracked in this state file.
  - Disposition: patched requirements and design to refer to the standalone
    route's neighboring-validator word-count range rather than an obsolete
    600-word lower bound; patched tasks to clarify that iterative re-review
    rounds are recorded here.
- Pending: rerun re-review where feasible after the fifth re-review patch.
- Completed sixth re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T205243-681Z-re-review-claude-opus-4.8.clean.md`
  - Finding: Medium finding that validation distinguished descriptive private
    category phrases but did not explicitly forbid actual private identifier
    values everywhere. Low findings requested re-review task cleanup,
    spec-only validation runs, incident-review differentiation list clarity,
    and a note that the earlier 600 to 1600 word-count range was superseded.
  - Disposition: patched requirements, design, tasks, and this state file to
    state the actual-private-identifier value boundary, note that arbitrary
    values require synthetic examples plus private-path checks and manual
    review, mark re-review rounds complete, clarify incident-review
    differentiation, and mark the old word-count range as superseded.
- Completed seventh re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-change-review-brief --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: exit 0, reduced coverage because Kiro reported denied shell-tool
    access; metadata `reviewComplete: true`, `toolDenied: true`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-change-review-brief/2026-06-20T205628-502Z-re-review-claude-opus-4.8.clean.md`
  - Findings: no High or Medium findings. Low findings requested optional
    accessibility wording, recording this re-review result, readiness update,
    and hedging `og:type` to the chosen neighboring pattern.
  - Disposition: applied the optional Low wording updates, recorded the reduced
    coverage caveat, and moved readiness to `ready-for-implementation` because
    Medium and higher findings are patched or absent.
- Readiness is `ready-for-implementation`; Medium and higher findings from
  spec review were patched or explicitly dispositioned before readiness moved.

## Validation

- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Deferred to future implementation: `npm test`, `npm run validate`, and
  `npm run build` from `site/` because this phase changes only spec files.
- Deferred to future implementation: browser sanity checks because this phase
  changes no route or layout.

## PR Review Loop

- Pending until PR creation.

## Oddities

- An initial Opus review wrapper attempt failed before model invocation because
  the draft spec files were accidentally created in the root checkout instead
  of this isolated worktree. The files were moved into this worktree, the
  accidental root copy was removed, and the review command then completed.
- The Sonnet spec review saved full-coverage review artifacts with metadata
  `status: 0`, but the wrapper returned exit 1 because `reviewComplete` was
  false.
- The first Opus re-review also saved full-coverage artifacts with metadata
  `status: 0`, but the wrapper returned exit 1 because `reviewComplete` was
  false.
- The final Opus re-review had reduced coverage because Kiro denied shell-tool
  access while checking one repo detail. The review still completed, read the
  packet, verified routes and validator conventions through file/glob/grep
  tools, and returned no High or Medium findings.

## Follow-ups

- Future implementation must re-check whether `/use-cases/change-review/` or
  `/change-review/` is the correct final placement before writing site code.
- Future implementation must keep public copy bounded to deterministic static
  evidence and must not publish private/raw artifact material.
