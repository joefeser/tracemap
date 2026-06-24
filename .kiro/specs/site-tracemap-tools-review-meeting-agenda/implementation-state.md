# Site TraceMap Tools Review Meeting Agenda Implementation State

Status: in-progress
Readiness: ready-for-implementation
Public claim level: concept

## Branch

- Branch: `codex/impl-site-review-meeting-agenda`
- Target base: `dev`
- Base: `origin/dev` at `5a6641961c5285210ae6fdbb3902d32fc517074d`
- Worktree: isolated implementation worktree; absolute local path intentionally omitted
  from the checked-in state file for the private-path guard.
- Pull request: pending.
- Latest implementation commit: pending until commit; final report records the exact SHA
  to avoid self-referential amend churn in this checked-in state file.

## Scope

- Spec folder ownership:
  `.kiro/specs/site-tracemap-tools-review-meeting-agenda/`
- Required files:
  `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Implementation ownership:
  `site/src/review-room/agenda/`, site metadata, route-specific validation,
  and this spec bookkeeping packet.
- This phase changes only public-site source, site validation scripts/tests,
  and this spec packet. It does not change scanner code, reducer code, or
  generated site output.

## Public Claim Level

- Public claim level: `concept`
- Rationale: the future surface is a public-site concept for a human evidence
  review meeting agenda. It does not cite checked-in public-safe demo evidence
  for an implemented agenda route or generated artifact, so a stronger public
  claim level is not justified.

## Scope Decisions

- Selected placement: `/review-room/agenda/`.
- Selection rationale: the live `/review-room/` page is a broad orientation
  route, while this agenda needs procedural table structure, direct linking,
  standalone metadata, sitemap inclusion, discovery metadata, and focused
  route validation without inflating the parent page.
- Rejected alternative: `/meetings/evidence-review/`; no live meeting
  hierarchy exists, so this would introduce a new information architecture
  branch for one concept page.
- Rejected alternative: section on `/review-room/`; the required agenda rows,
  stop-condition categories, neighboring distinctions, and validation checks
  would bloat the parent route and weaken its orientation focus.
- Rejected alternative: section on `/reviewer-quickstart/`; the agenda is a
  reusable meeting runbook rather than first-review onboarding.
- Metadata consequences: standalone route must carry title, description,
  canonical URL, Open Graph metadata, route-index metadata, sitemap metadata,
  and discovery metadata with `publicClaimLevel: concept`.
- Sitemap consequences: add `/review-room/agenda/` to `site/src/_site/pages.json`
  with monthly change frequency and concept-route priority.
- Discovery consequences: add `/review-room/agenda/` to
  `site/src/_site/discovery.json` with limitations and non-claims; add useful
  inbound discovery from `/review-room/` only, leaving primary navigation
  unchanged.
- Validation consequences: add a standalone route validator for required copy,
  agenda rows and cells, evidence checks, stop conditions, required links,
  neighbor distinctions, metadata, sitemap/discovery coverage, forbidden
  claims, private/raw material, unsupported certainty language, blame
  language, accessibility structure, and bounded word count.
- Primary navigation remains unchanged. The parent review-room page receives a
  contextual link because it improves discovery without promoting the agenda to
  primary navigation.
- Verified link set before publication: `/proof-paths/`, `/evidence/`,
  `/validation/`, `/limitations/`, `/review-room/`,
  `/reviewer-quickstart/`, `/packets/assembly/`, `/handoff/template/`,
  `/owners/follow-up/`, `/decisions/evidence-record/`, and the live manager
  presentation route `/demo/manager-script/`.
- Rejected manager route alias: `/manager-demo-script/` is not the live route;
  `/demo/manager-script/` resolves and is linked only as a neighboring
  distinction, not as meeting evidence.
- No unresolved, substituted, or deferred cross-links remain for this route.
- Manual public-safety reviewer signoff: completed by implementation owner.
  Automated validation checks blame-language patterns, unsupported certainty
  language, raw/private material, hard private material, and forbidden positive
  claims; this manual signoff confirms the page frames gaps as evidence states
  and follow-up ownership rather than fault.
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
- Word-count bounds: 700 to 1500 rendered main-content words.
- Word-count anchor page: concept-route validators such as evidence decision
  record and evidence gap register use route-specific upper/lower bounds; this
  agenda page is more compact than the gap register but larger than a simple
  orientation page because the agenda table and distinction block are required.

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
- Completed: `node --test site/scripts/review-meeting-agenda.test.mjs`
  passed.
- Completed: `npm test` from `site/` passed.
- Completed: `npm run validate` from `site/` passed; generated-site validator
  reported 67 HTML files, 2299 internal references, 66 sitemap URLs, 1 legacy
  story safety target, and 13 legacy modernization evidence-map rows.
- Completed: `npm run build` from `site/` passed.
- Completed: desktop and mobile browser sanity through Playwright against
  `http://localhost:4173/review-room/agenda/`.
  - Desktop viewport: 1440 by 1100.
  - Mobile viewport: 390 by 844.
  - Console check: 0 errors, 0 warnings.
  - Screenshots saved under `site/output/playwright/` for local review.
- Browser sanity result: layout rendered at both sizes, required agenda table
  and sections were reachable, and no obvious overlap was observed.
- Post-review fix validation:
  - Completed: `node --test site/scripts/review-meeting-agenda.test.mjs`
    passed with 12 tests after Qodo/Gemini-thread fixes.
  - Completed: `node --test scripts/validate.test.mjs` from `site/` passed.
  - Completed: `npm test` from `site/` passed with 489 tests.
  - Completed: sequential `npm run validate && npm run build` from `site/`
    passed.
  - Completed: `git diff --check` passed.
  - Completed: `./scripts/check-private-paths.sh` passed with
    `Private path guard passed.`
- Validation oddity: an intermediate parallel `npm run validate` and
  `npm run build` attempt failed because both commands rewrote `site/dist` at
  the same time. The commands passed when rerun sequentially.

## PR Loop

- Pull request: `#319`.
- Completed: created a ready PR targeting `dev` and waited 3 minutes before
  the first PR-loop run.
- Completed initial run: `agent-control pr-loop --repo joefeser/tracemap --pr
  319 --base dev --require-codex-review --quiet --json`.
- Initial stop: `actionable_findings` with `UNRESOLVED_REVIEW_THREADS`.
- Required-review batch initially held Qodo/Gemini thread patching while Codex
  was `request_posted`; no partial patch was made.
- Completed follow-up PR-loop polling after Codex returned.
- Patch authority: `requiredReviewBatch.patchAuthorized: true`,
  `reviewQuorum.waitPosture: all_returned`, and `nextAction:
  patch_actionable_findings`.
- Patched combined findings:
  - Removed the unconditional redundant implementation-state candidate path
    for real `site/dist` validation while retaining a conditional root-`dist`
    fixture fallback.
  - Added HTML-comment awareness to the section-end scanner and a regression
    test for commented closing tags inside bounded sections.
- Pending: push post-review fix commit and rerun the final PR loop.
- Repo-local lane config is expected at `.agent-control/lanes/pr-review-loop.yaml`.
- Codex and Qodo are required as a batch by the repo-local lane policy. Do not
  patch partial findings until ACK grants authority through returned reviewers,
  typed timeout, quorum, or another typed PR-loop state.

## Follow-Up Notes

- Future implementation should avoid local paths in checked-in state files and
  public copy.
- Future implementation should record any unavailable routes or substituted
  links before publishing public copy.
