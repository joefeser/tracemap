# Site TraceMap Tools Review Meeting Agenda Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

- Branch: `codex/spec-site-review-meeting-agenda`
- Target base: `dev`
- Base: `origin/dev` at `749de71fbb55660e235fa2530bd3c3f9398037ad`
- Worktree: isolated spec worktree; absolute local path intentionally omitted
  from the checked-in state file for the private-path guard.
- Pull request: pending.
- Latest spec commit: pending until commit; final report records the exact SHA
  to avoid self-referential amend churn in this checked-in state file.

## Scope

- Spec folder ownership:
  `.kiro/specs/site-tracemap-tools-review-meeting-agenda/`
- Required files:
  `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- This phase is spec-only. It does not change `site/src`, generated output,
  scanner code, reducer code, validation scripts, or existing specs.

## Public Claim Level

- Public claim level: `concept`
- Rationale: the future surface is a public-site concept for a human evidence
  review meeting agenda. It does not cite checked-in public-safe demo evidence
  for an implemented agenda route or generated artifact, so a stronger public
  claim level is not justified.

## Scope Decisions

- Initial candidate placements:
  `/review-room/agenda/`, `/meetings/evidence-review/`, section on
  `/review-room/`, or section on `/reviewer-quickstart/`.
- Recommended starting placement for future implementation:
  `/review-room/agenda/`.
- Future implementation must inspect live neighboring routes before changing
  site source and record the selected placement, rejected alternatives,
  metadata consequences, sitemap consequences, and validation consequences.
- Primary navigation should remain unchanged unless a future
  information-architecture review records otherwise.
- Required future sections:
  `Before the meeting`, `Agenda`, `Evidence checks`, `Gap capture`,
  `Owner assignment`, `Decision record handoff`, `Stop conditions`, and
  `Non-claims`.
- Required future agenda rows:
  `question framing`, `proof path check`,
  `evidence tier/coverage check`, `limitation check`,
  `gap register check`, `owner follow-up`, `decision record`, and
  `closeout`.
- Required future neighboring distinctions:
  `/review-room/`, `/reviewer-quickstart/`, `/packets/assembly/`,
  `/handoff/template/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, and the verified manager demo or
  presentation route if present.
- Word-count bounds: pending future implementation placement decision.
- Word-count anchor page: pending future implementation placement decision.
- If no comparable neighboring concept-page word-count pattern exists, future
  implementation should use a 400 to 1000 rendered main-content word fallback
  and record the rationale here.

## Spec Review

- Completed: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind spec --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, reduced coverage because Kiro
    reported denied tool access.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T031201-260Z-spec-claude-opus-4.8.clean.md`
  - Findings: Medium finding that the design agenda table omitted the
    evidence-input dimension required by Requirement 4; Medium finding that
    validation checked agenda row labels but not per-row purpose, evidence
    input, and stop-or-handoff output structure. Low findings requested
    unsupported-certainty task wording, required-link strictness consideration,
    and word-count decision placeholders.
  - Disposition: patched requirements, design, tasks, and this state file to
    add evidence-input cells to every agenda row, require agenda-structure
    validation, add unsupported-certainty validation task wording, and reserve
    word-count bounds and anchor fields for implementation.
- Completed: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind spec --model
  claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T031350-034Z-spec-claude-sonnet-4.6.clean.md`
  - Findings: no Medium or higher findings. Low findings requested explicit
    implementation-state private-path guidance, a manual blame-language review
    gate if automated detection is not implemented, and fallback word-count
    guidance if no neighboring concept-page pattern exists.
  - Disposition: patched requirements and design to forbid absolute local
    paths, raw remotes, private sample names, and credential-like values in
    `implementation-state.md`; require role-based public-safety reviewer
    signoff when automated blame-language checks are not implemented; and add a
    400 to 1000 word fallback when no comparable page pattern exists.
- Pending: rerun re-review where feasible after Medium finding patches.
- Completed re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: exit 0, full coverage.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T031524-500Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that `design.md` validation did not carry the
    unsupported-certainty word list even though requirements and tasks required
    that validation. Low findings noted the checked-in PR-loop repo slug,
    missing manual blame-language signoff wording in tasks, and still-pending
    re-review/readiness task checkboxes.
  - Disposition: patched design to list unsupported-certainty vocabulary in
    validator guidance and patched tasks to include manual public-safety
    reviewer signoff when automated blame-language detection is not
    implemented. The PR-loop repo slug is retained because the operator
    explicitly required that exact final command; private-path validation must
    confirm it is not flagged.
- Pending: rerun re-review where feasible after the unsupported-certainty
  design patch.
- Completed second re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T031821-112Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that the `decision record` agenda row evidence
    input omitted limitations, conflicting with Requirement 2 and the design's
    decision-record handoff section. Low findings requested a conditional
    `/manager-demo-script/` cross-link note, validator anchoring for rule ID or
    rule family checks, and stop-condition body validation.
  - Disposition: patched design to include limitations in the decision-record
    row evidence input; patched requirements, design, and tasks to require rule
    ID or rule family in evidence checks, enumerate stop-condition trigger
    content, and treat `/manager-demo-script/` as a conditional
    neighboring-distinction link only.
- Pending: rerun re-review where feasible after the decision-record limitation
  patch.
- Completed third re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T032047-018Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that the `decision record` agenda row output did
    not explicitly preserve limitations even though the evidence input did.
    Low findings requested an explicit owner/repo PR-loop slug allowance tied
    to private-path validation, a task note pointing to this iterative
    re-review log, and a page-structure cross-reference to enumerated stop
    triggers.
  - Disposition: patched the decision-record row output to preserve question,
    evidence state, limitations, gaps, owners, and non-claims without stronger
    claims; patched requirements and design to allow the operator-required
    owner/repo PR-loop slug only when private-path validation passes; patched
    tasks and design to reduce stale-status and stop-condition ambiguity.
- Pending: rerun re-review where feasible after the decision-record output
  patch.
- Completed fourth re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T032326-331Z-re-review-claude-opus-4.8.clean.md`
  - Findings: Medium finding that validation omitted an accessibility gate for
    agenda table or list semantics, descriptive anchors, and heading
    hierarchy; Medium finding that `/manager-demo-script/` was required as a
    neighbor distinction even though the live site may expose a different
    manager demo route. Low findings requested non-canonical tier checks,
    defined required core links, and a review-packet readiness-gating note.
  - Disposition: patched requirements, design, and tasks to add accessibility
    validation, route-verified manager demo or presentation distinction
    language, required core links for `/proof-paths/`, `/validation/`, and
    `/limitations/`, and non-canonical tier validation. Patched
    `review-packet.md` with the readiness-gating note in the same pass.
- Pending: rerun re-review where feasible after the accessibility and manager
  route guard patch.
- Completed fifth re-review: `node scripts/kiro-review.mjs --phase
  site-tracemap-tools-review-meeting-agenda --kind re-review --model
  claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  - Result: wrapper exit 1; metadata status 0, full coverage, but
    `reviewComplete: false`.
  - Review artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-review-meeting-agenda/2026-06-24T032749-771Z-re-review-claude-opus-4.8.clean.md`
  - Findings: no remaining spec-content Medium or higher findings. The only
    Medium was a process/disposition gap that this confirming review had not
    yet been recorded. Low findings requested manager-demo task wording cleanup,
    carrying pending word-count bounds into future implementation, and running
    the private-path guard before readiness flip.
  - Disposition: recorded this confirming review, patched the manager-demo task
    wording, ran `git diff --check`, ran `./scripts/check-private-paths.sh`,
    and moved packet headers to `ready-for-implementation`.
- Current readiness is `ready-for-implementation`; Medium or higher findings
  from the spec-review cycle are patched or explicitly dispositioned.

## Validation

- Completed: `git diff --check` passed.
- Completed: `./scripts/check-private-paths.sh` passed with
  `Private path guard passed.`
- Site implementation validation is deferred because this phase intentionally
  does not change site source.

## PR Loop

- Pending: create a ready PR targeting `dev`.
- Pending: wait 3 minutes after PR creation.
- Pending: run `agent-control pr-loop --repo joefeser/tracemap --pr
  <PR_NUMBER> --base dev --require-codex-review --quiet --json`.
- Repo-local lane config is expected at `.agent-control/lanes/pr-review-loop.yaml`.
- Codex and Qodo are required as a batch by the repo-local lane policy. Do not
  patch partial findings until ACK grants authority through returned reviewers,
  typed timeout, quorum, or another typed PR-loop state.

## Follow-Up Notes

- Future implementation should avoid local paths in checked-in state files and
  public copy.
- Future implementation should record any unavailable routes or substituted
  links before publishing public copy.
