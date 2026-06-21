# Site TraceMap Tools Guided Proof-Path Tour Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-guided-proof-path-tour`
Base: `origin/main`
Target PR base: `main`

## Scope

This phase creates a spec-only packet for a future public-site guided
proof-path tour. It intentionally does not implement site code, scanner code,
reducer code, generated outputs, validation scripts, or existing specs.

Write scope for this phase is limited to:

- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/`

## Claim Boundary

The future page or section is concept-level guidance for reading existing
public-safe evidence surfaces. It must visibly say
`Public claim level: concept` and
`No public conclusion without evidence`.

The future tour is not a proof engine, runtime trace, AI analysis, release
approval, operational approval flow, or production diagnostic surface. It must
not claim runtime behavior, production traffic, endpoint performance, outage
cause, release safety, operational safety, complete coverage, AI/LLM impact
analysis, embeddings, vector databases, or prompt classification.

The future tour must not publish raw facts, raw SQLite indexes, analyzer logs,
raw source snippets, raw SQL, config values, secrets, local absolute paths,
raw repository remotes, generated scan directories, private sample names, or
hidden validation details.

## Route Decision Status

Status: deferred to future implementation.

Candidate placements to evaluate:

- `/proof-paths/tour/`
- `/demo/proof-path-tour/`
- Folded section on `/proof-paths/`

Non-binding recommendation: `/proof-paths/tour/`, because it keeps the guided
reading flow close to the canonical proof-path surface while avoiding a claim
that the tour is itself a demo result or base proof-path reference.

Open question raised in `review-packet.md`: the route or placement choice was
not resolved before spec review. The future implementer must evaluate all
three candidates, record the selected route, rejected alternatives, and short
reasons in this file before beginning site code changes.

Future implementation must record the selected route or placement, rejected
alternatives, sitemap/discovery consequences, and any route substitutes for
missing or renamed public-safe targets.

Discovery note: `concept` is already an established public claim level in
site discovery metadata, so the future implementation confirmation task is a
quick compatibility check rather than an expected blocker. The final
`hintCategory` must still be selected from the current discovery vocabulary
and recorded here with rationale.

Discovery vocabulary reference (non-binding): the current discovery
`hintCategory` set is `start`, `evidence`, `limitations`, `demo`,
`repo-doc`, `roadmap`, and `use-case`. For an evidence-reading tour,
`evidence` is the non-binding recommendation; the final value must still be
confirmed against the discovery vocabulary at implementation time and recorded
here with rationale.

## Review Status

- `claude-opus-4.8` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T023637-924Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T023917-374Z-spec-claude-sonnet-4.6.clean.md`.
- Medium findings patched: where-to-stop field clarity, required worked
  example, illustrative example labeling, per-step non-claim anchor naming,
  route-choice open-question state, all-file readiness update task, and
  sanctioned-section markup convention task.
- Low findings patched: established `concept` note, stable `#where-to-stop`
  anchor wording, header lockstep requirement, combined proof-path/supporting
  route anchor clarity, follow-up for sanctioned-section convention, and
  review-packet checklist detail for `hintCategory` and concept confirmation.
- `claude-opus-4.8` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T024143-487Z-re-review-claude-opus-4.8.clean.md`.
- Re-review Medium finding patched: added discovery `hintCategory` vocabulary
  reference and non-binding `evidence` recommendation to requirements and
  implementation state.
- Re-review Low findings patched: added worked-example completeness validation
  and folded-section word-count guidance.
- `claude-sonnet-4.6` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T024633-691Z-re-review-claude-sonnet-4.6.clean.md`.
- Sonnet re-review Medium findings patched: updated the review-packet
  checklist for the patched spec state and added design guidance for the
  sanctioned-section markup convention.
- Sonnet re-review Low findings patched: documented local-only `.tmp/` review
  artifacts, added a review-packet sync task, and summarized word-count bounds
  in design.
- Final `claude-opus-4.8` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T024838-966Z-re-review-claude-opus-4.8.clean.md`.
- Final `claude-sonnet-4.6` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T024839-043Z-re-review-claude-sonnet-4.6.clean.md`.
- Final re-review Medium findings patched: added folded-section claim-level
  reconciliation, fixed folded word-count floor/ceiling guidance, and added
  worked-example traversal validation to design.
- Final re-review Low findings patched: harmonized folded-section terminology,
  added `#step-non-claim` to requirements, clarified worked examples as
  ending in bounded non-claim conclusions, added worked-example traversal to
  follow-ups, and scoped the review-packet checklist to requirements and
  design validation sections.
- Last-pass `claude-opus-4.8` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T025305-159Z-re-review-claude-opus-4.8.clean.md`.
- Last-pass `claude-sonnet-4.6` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-guided-proof-path-tour --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/2026-06-21T025305-224Z-re-review-claude-sonnet-4.6.clean.md`.
- Last-pass Medium findings patched: added differentiation and link
  requirements for `/review-claim-checklist/` and `/glossary/`, mirrored the
  `hintCategory` vocabulary reference into requirements and design, and kept
  the non-binding `evidence` recommendation explicit.
- Last-pass Low findings patched: clarified Requirement 7's initial-state
  wording and added public-site accessibility validation expectations.
- Additional Kiro re-review was deferred after this patch pass because the
  last two re-reviews both had reduced coverage from denied tool access after
  repeated review cycles, and the remaining patched findings were direct
  spec-text clarifications. PR review-loop remains the external gate.
- Current Medium or higher findings: none known after patches. Review coverage
  remains reduced where noted above.

Note: `.tmp/` review artifacts are local-only and not committed to the
repository.

## Validation

Spec-only validation passed on 2026-06-21:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused text checks for required spec packet fields and forbidden scope
  edits: passed.

Future site implementation validation is listed in `tasks.md` and includes
site test, validation, build, and browser sanity expectations.

## Follow-Ups

- Future implementer must make and record the final route or placement
  decision.
- Future implementer must decide and record the sanctioned-section markup
  convention before writing forbidden-claim and forbidden-private/raw-material
  validators. See `tasks.md` for the required task.
- Future implementer must add validation that at least one worked example
  traverses the required proof-step fields to a bounded non-claim conclusion,
  not only that an example is present and labeled illustrative.
- Future implementer must preserve the distinction from
  `/review-claim-checklist/` and `/glossary/` when choosing the final route
  and writing public copy.
- Future implementer must add validation for required copy, links, metadata,
  discovery metadata, sitemap metadata when standalone, forbidden claims,
  private/raw material, word count, and desktop/mobile browser sanity.
