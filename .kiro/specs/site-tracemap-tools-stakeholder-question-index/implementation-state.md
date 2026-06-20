# Site TraceMap Tools Stakeholder Question Index Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-stakeholder-question-index`

Base: `origin/dev`

Target PR base: `dev`

Scope: spec packet only under
`.kiro/specs/site-tracemap-tools-stakeholder-question-index/`.

## Current State

This is a spec-only phase. No site source, route metadata, generated site
output, scanner code, reducer code, validation script, or generated artifact
has been implemented.

The spec defines a future public-safe stakeholder question index that starts
with reader questions and routes readers to existing evidence surfaces. The
surface is an orientation/index layer, not a new proof claim.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the proposed page or section is navigational and explanatory. It
does not create new public demo evidence or prove a new analyzer capability.
Rows may route to demo-backed surfaces such as public demo results, but the
question index itself should remain concept-level unless a future spec
amendment records checked-in public-safe evidence that supports a row or page
upgrade.

## Route and Placement Guidance

Candidate placements for future implementation:

- `/questions/`
- `/use-cases/questions/`
- section on `/use-cases/`

Future implementation must record the selected placement and rejected
alternatives before changing site source. It must also record substitutions or
omissions for any candidate target route that does not exist at implementation
time.

## Scope Decisions

- Keep the surface as a question-to-proof-path orientation index.
- Keep all implementation tasks unchecked until a future implementation phase
  changes site source and validation.
- Require rows for manager planning, engineer endpoint/change review,
  incident-adjacent handoff, modernization planning, reviewer claim checking,
  demo evaluation, proof-source inspection, and agent/bot discovery.
- Require each row to include audience, question, safe answer shape, target
  route, evidence surface, public claim level, proof path, limitation, and
  non-claim.
- Use `Public claim level: concept` unless future public-safe evidence
  justifies `demo`.
- Do not publish raw scanner artifacts, private material, hidden validation
  details, runtime telemetry, or raw source/config/SQL material.

## Review Commands

Spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-question-index --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-question-index --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Review results:

- `claude-opus-4.8`: command ran but Kiro reached the request quota after
  retries. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-question-index/2026-06-20T202804-580Z-spec-claude-opus-4.8.clean.md`.
  Exact clean output included: `Kiro rate limit reached: Request quota
  exceeded. Please wait a moment and try again.`
- `claude-sonnet-4.6`: command completed and saved review output at
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-question-index/2026-06-20T202835-100Z-spec-claude-sonnet-4.6.clean.md`.
- `claude-sonnet-4.6` re-review: command completed and saved review output at
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-question-index/2026-06-20T203209-608Z-spec-claude-sonnet-4.6.clean.md`.
- `claude-sonnet-4.6` final re-review: command completed and saved review
  output at
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-question-index/2026-06-20T203323-082Z-spec-claude-sonnet-4.6.clean.md`.

## Readiness Note

One of two planned model reviews, `claude-opus-4.8`, returned a quota error
with no review content. Ready-for-implementation status is based on the
`claude-sonnet-4.6` review findings and re-review findings. A future
implementation phase should re-run the Opus review before opening the first
site-source PR if quota allows.

Review findings and dispositions:

- H1: The reviewer warned that the `claude-opus-4.8` task might name an
  unavailable model. Disposition: not patched because the user explicitly
  required that exact command and the command reached a Kiro quota error
  rather than an unknown-model error. The exact command and error are recorded
  above.
- M1: The design matrix omitted safe answer shape, proof path, limitation, and
  non-claim columns. Patched by expanding the design matrix to the full row
  schema.
- M2: `impacted` wording was gated on reducer-backed public-safe evidence but
  did not define that evidence. Patched by requiring a public-safe reducer
  output with rule IDs, evidence tiers, coverage labels, and a published
  `demo` or higher claim level.
- M3: Forbidden-wording validation did not cover alt text, captions, fixtures,
  or test files. Patched in requirements, design, and tasks.
- L1: Follow-up did not call out `/vault-export/`. Patched below.
- L2: Requirements and tasks did not mirror the non-claim exception for
  forbidden terms. Patched in requirements and tasks.
- L3: Tasks did not explicitly gate future site PRs on a recorded route
  decision. Patched in tasks.
- Re-review M1: Spec-review checkboxes did not state that Opus produced no
  review content. Patched in tasks and this readiness note.
- Re-review M2: Design validation omitted the non-claim/limitation carve-out
  for forbidden terms. Patched in design.
- Re-review L2: Engineer row claim-level cell used conditional shorthand.
  Patched to require recorded public-safe evidence per Requirement 4.
- Final re-review M1: Future implementation tasks needed a stronger
  spec-only separator. Patched in tasks.
- Final re-review M2: `ready-for-implementation` could be misread as
  permission to implement site code on this branch. Patched in design and
  tasks with a deferred-implementation note.
- Final re-review L1: Oddities did not mention the Opus quota caveat. Patched
  below.
- Final re-review L2: Design copy rules omitted the `impacted` gate. Patched
  in design.

## Validation

Spec-branch validation completed before commit:

- `git diff --check` passed after adding the new spec files with
  intent-to-add so untracked content was included.
- `./scripts/check-private-paths.sh` passed.

PR-loop follow-up validation after patch:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.

Future implementation-branch validation:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- desktop and mobile browser sanity checks if route, layout, or interaction
  changes are made

## Oddities

- `claude-opus-4.8` review returned a quota error with no review content. See
  Readiness Note for details; re-run before the first site-source PR if quota
  allows.

## PR Loop Findings

Initial PR loop for PR #235 returned `actionable_findings` with three
unresolved review threads in `design.md`:

- Design matrix did not expose rule ID or rule-family handling despite
  requirements that proof paths preserve rule IDs where public-safe.
- Engineer and demo rows used conditional public claim level shorthand.
- Agent/bot discovery target surface used `sitemap/discovery metadata` in the
  route-oriented column instead of a route.

Fix: expanded the design matrix to include `Target route`, `Evidence surface`,
and `Rule ID/rule family` columns; changed all required rows to `concept`;
kept discovery metadata as the evidence surface for agents while using
`/proof-paths/`, `/validation/`, and `/limitations/` as concrete proof routes.

## Follow-Ups

- Future implementation should confirm current route names before linking to
  any candidate target route.
- Future implementation should explicitly confirm whether `/vault-export/`
  exists at implementation time or record the substitution or omission
  rationale in this file before publishing any row that targets it.
- Future implementation should decide whether `/questions/` is sufficiently
  clear for both humans and agents, or whether `/use-cases/questions/` better
  fits the existing information architecture.
