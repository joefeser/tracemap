# Site TraceMap Tools Review Room Demo Path Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Implementation branch:
`codex/spec-site-review-room-demo-path-20260626002510`

Base: `origin/dev`

Base commit: `419cfeb1 Normalize completed site spec state (#352)`

Target branch: `dev`

## Scope

This phase creates a spec-only packet for a future `tracemap.tools` guided path
that starts from existing review-room surfaces and walks visitors through a
demo-safe static evidence review flow.

Tracked changes are limited to:

- `.kiro/specs/site-tracemap-tools-review-room-demo-path/requirements.md`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/design.md`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/tasks.md`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/review-packet.md`

This phase does not implement site code, scanner code, reducer code,
validation scripts, generated outputs, package changes, runtime proof,
production traffic proof, endpoint performance claims, outage cause claims,
release safety claims, complete coverage claims, or AI/LLM impact-analysis
claims.

## Placement And Claim Decisions

- Default public claim level: `concept`.
- Demo claim level is not justified in this packet because this phase does not
  require checked-in public-safe demo proof for every future guided-path step.
- Non-binding recommended placement: `/review-room/demo-path/`.
- Allowed alternatives: section on `/review-room/`, section on
  `/review-room/agenda/`, or section on `/demo/start-here/`.
- Future implementation must record the selected placement, rejected
  alternatives, link existence checks, metadata choice, and validation scope
  before changing site code.

## Existing Surface Check

The base branch includes these relevant public source routes:

- `/review-room/`
- `/review-room/agenda/`
- `/proof-paths/`
- `/proof-paths/tour/`
- `/review-claim-checklist/`
- `/packets/`
- `/packets/assembly/`
- `/packets/examples/`
- `/demo/`
- `/demo/start-here/`
- `/demo/evidence-trail/`
- `/demo/proof-assets/`
- `/demo/result/`
- `/demo/runbook/`
- `/demo/troubleshooting/`
- `/limitations/`
- `/validation/`
- `/owners/follow-up/`

Future implementation must re-check generated output before adding links.

## Review Status

- Kiro Opus spec review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-room-demo-path --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 after saving full-coverage review artifacts and review text.
  Saved cleaned review text:
  `.tmp/kiro-reviews/site-tracemap-tools-review-room-demo-path/2026-06-26T052818-327Z-spec-claude-opus-4.8.clean.md`.
- Kiro Sonnet spec review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-room-demo-path --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 after saving full-coverage review artifacts and review text.
  Saved cleaned review text:
  `.tmp/kiro-reviews/site-tracemap-tools-review-room-demo-path/2026-06-26T053011-641Z-spec-claude-sonnet-4.6.clean.md`.
- Medium or higher actionable findings patched:
  - Promoted owner-routing role vocabulary and the non-proof disclaimer into
    `requirements.md` and validation requirements.
  - Required the evidence-packet step to link at least one packet route or
    retain the step with a visible limitation and recorded omission.
  - Clarified that role labels satisfy next-owner requirements when
    `/owners/follow-up/` is unavailable.
  - Required the nine guided-path steps to render in order as one contiguous
    block for standalone and section placements.
- Narrow Low findings patched:
  - Added `/packets/examples/` to the required link set when present.
  - Pinned discovery `hintCategory` to `use-case`.
  - Named the fallback Open Graph type as `article`.
- Bounded re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-room-demo-path --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 after saving full-coverage review artifacts and review text.
  Saved cleaned review text:
  `.tmp/kiro-reviews/site-tracemap-tools-review-room-demo-path/2026-06-26T053221-416Z-spec-claude-sonnet-4.6.clean.md`.
- Medium findings from bounded re-review patched:
  - Required non-empty limitation, stop condition, and next owner or route
    text for every guided-path step.
  - Mirrored the evidence-packet silent-removal guard in `design.md` and added
    a validation negative test for a missing evidence-packet step.
- Narrow Low findings from bounded re-review patched:
  - Echoed public-safe source-context constraints in design.
  - Added a negative test for missing final stop step.
  - Made design owner-label vocabulary exhaustive.
  - Updated review task status.
- No second re-review was run; the requested bounded re-review pass has been
  completed, and all Medium actionable findings from that pass were patched.

## Validation Plan

- Confirm diff is limited to this spec folder.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.

Site tests, site validation, site build, and browser sanity are future
implementation validation tasks because this packet intentionally does not
change `site/`.

## Validation Results

- Diff scope confirmed limited to
  `.kiro/specs/site-tracemap-tools-review-room-demo-path/`.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- Site tests, site validation, site build, and browser sanity were not run
  because this phase is spec-only and does not change `site/`.

## ACK Status

- PR: `#354`
- First ACK command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 354 --base dev --require-codex-review --quiet --json`
- First ACK decision: `actionable_findings`.
- Stop reason: `UNRESOLVED_REVIEW_THREADS`.
- Merge status: not merge-ready.
- Action taken: patched the readiness lifecycle wording and updated completed
  publish/ACK task checkboxes without merging.

## Follow-Ups

- Future implementation should preserve visible copy:
  `Public claim level: concept` and `No public conclusion without evidence`.
- Future implementation should keep demo links demo-safe and should not
  upgrade to `demo` claim level unless checked-in public-safe demo proof is
  required and validated for every step.
- Future implementation should record missing, renamed, or substituted routes
  instead of creating dead links.
