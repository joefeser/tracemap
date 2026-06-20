# Site TraceMap Tools Stakeholder Question Index Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-stakeholder-question-index`

Implementation branch: `codex/impl-site-stakeholder-question-index`

Base: `origin/dev`

Target PR base: `dev`

Scope: implementation for this spec only, covering this spec packet and the
site source, route metadata, discovery metadata, validation, and tests needed
for the stakeholder question index.

## Current State

Implemented as a standalone public route at `/questions/`.

The route is a public-safe orientation index that starts with reader questions
and routes readers to existing evidence surfaces. It is not a new proof claim,
capability matrix, claim ledger, FAQ replacement, scanner behavior, reducer
behavior, generated artifact, runtime workflow, or AI/LLM analysis surface.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the page is navigational and explanatory. It does not create new
public demo evidence or prove a new analyzer capability. Rows route to current
public-safe surfaces, including some demo-backed surfaces, but every matrix row
uses row-level `concept` because the row answer shape is orientation and route
selection.

## Route and Placement Guidance

Selected placement: `/questions/`.

Rejected alternatives:

- `/use-cases/questions/`: rejected because the index is intended to serve
  managers, reviewers, incident participants, architects, demo evaluators, and
  agents in addition to use-case readers.
- Section on `/use-cases/`: rejected because the matrix needs route metadata,
  sitemap discovery, discovery metadata, stable row anchors, and focused
  validation as its own public-safe orientation surface.
- Primary navigation placement: rejected because the route remains
  concept-level orientation and existing primary navigation already has dense
  evidence, validation, limitation, demo, and docs links.

Route substitutions or omissions: none. All candidate target routes existed at
implementation time, including `/manager-packet/`,
`/use-cases/endpoint-review/`, `/incident-evidence-handoff/`,
`/legacy-modernization/evidence-map/`, `/proof-paths/`,
`/proof-source-catalog/`, `/review-claim-checklist/`,
`/static-vs-runtime/`, `/demo/result/`, `/vault-export/`, `/limitations/`,
and `/validation/`.

## Scope Decisions

- Keep the surface as a question-to-proof-path orientation index.
- Require rows for manager planning, engineer endpoint/change review,
  incident-adjacent handoff, modernization planning, reviewer claim checking,
  demo evaluation, proof-source inspection, and agent/bot discovery.
- Require each row to include audience, question, safe answer shape, target
  route, evidence surface, public claim level, proof path, limitation, and
  non-claim.
- Include `rule ID or rule family` as a visible matrix column so proof-path
  handling stays attached to each row.
- Use page-level and row-level `Public claim level: concept`.
- Keep target-route claim levels separate from page-level and row-level claim
  levels.
- Keep `/questions/` out of primary navigation. Add a bounded inbound link from
  `/use-cases/` only.
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

Implementation-branch validation:

- `npm test` from `site/`: passed.
- `npm run validate` from `site/`: passed and validated 47 HTML files, 1501
  internal references, 46 sitemap URLs, one legacy story safety target, and 13
  legacy modernization evidence-map rows.
- `npm run build` from `site/`: passed.
- Browser sanity: passed on `/questions/` using desktop 1440 by 1000 and
  mobile 390 by 844 viewports. The visible route showed the concept claim
  label, shared principle, quick links, matrix, route targets, and non-claims.
  The mobile matrix preserved stable columns inside the existing horizontal
  table wrapper.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

PR-loop patch validation:

- Initial PR loop for implementation PR returned `actionable_findings` with
  one unresolved review thread in `site/scripts/stakeholder-question-index.mjs`.
- Patch: added `ruleIdOrFamily` to the focused validator's required row
  fields and added a regression test that removes the field from the
  agent/bot discovery row.
- `npm test` from `site/`: passed with 236 tests.
- `npm run build` from `site/`: passed.
- `npm run validate` from `site/`: passed and validated 47 HTML files, 1501
  internal references, 46 sitemap URLs, one legacy story safety target, and 13
  legacy modernization evidence-map rows.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

## Oddities

- `claude-opus-4.8` review returned a quota error with no review content. See
  Readiness Note for details; re-run before the first site-source PR if quota
  allows.
- Local browser sanity initially found port `4173` already in use. The local
  sanity server used port `4187` instead.
- During PR-loop patch validation, running `npm run validate` and
  `npm run build` concurrently caused a local `site/dist` race. The build
  passed, and `npm run validate` passed when rerun by itself.

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

- Re-run validation after PR-loop patches, if any.
- Do not promote row-level claim levels to `demo` unless a future amendment
  records exact public-safe evidence for the row answer shape.
