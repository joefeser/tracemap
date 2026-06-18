# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-18
Branch: codex/spec-site-public-demo-runbook
Worktree: not recorded (private-text guardrail)
Worktree path is intentionally omitted to satisfy the private absolute-path
guardrail in Requirement 3 and `check-private-paths.sh`.
Source of truth: spec files in branch `codex/spec-site-public-demo-runbook`
Public claim level: demo

## Summary

This phase creates a spec-only runway for a future public demo runbook page at
`/demo/runbook/`. The runbook should help a public demo operator run the demo,
inspect generated summaries, decide what can be shared, and avoid overstating
static evidence.

No site code is implemented in this phase.

## Scope

- Define requirements for a future `/demo/runbook/` page.
- Define future implementation tasks, leaving all tasks unchecked.
- Bridge `/demo/start-here/`, `/demo/result/`, `/demo/evidence-trail/`,
  `/demo/proof-upgrades/`, `/proof-paths/`, `/validation/`, and
  `/limitations/`.
- Require an operator checklist for pre-run, run, inspect, evidence-follow,
  validation, limitations, and sharing decisions.
- Require public-safe summary versus local-only artifact boundaries.
- Require claim-safe sharing guidance and explicit stop conditions.
- Require page/discovery metadata and focused validation for private text and
  overclaims.

## Claim Boundary Decisions

- Public claim level is `demo` because the future page is grounded in
  checked-in public demo samples, generated public-safe summaries, and existing
  public proof routes.
- The runbook is not a new source of proof. It must link back to existing
  public-safe proof surfaces and deterministic artifacts.
- Public-safe summaries and reviewed public-safe reports may be shareable after
  sentinel/private-text checks pass.
- Raw facts, raw SQLite, combined SQLite files, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw
  repository remotes, generated scan directories, and private sample names stay
  local-only.
- The future page must not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.

## Route Decision

- Selected future route: `/demo/runbook/`.
- Reason: the existing `/demo/start-here/` page is the first-run walkthrough;
  the runbook is a more operational checklist for running, inspecting, sharing,
  and stopping when evidence or public-safety gates fail.
- Rejected alternate: extend `/demo/start-here/` only. Reason: that page is
  already a guided first-run path, while the runbook needs a checklist,
  artifact-safety matrix, and sharing boundary that should be directly
  addressable.
- Rejected alternate: extend `/demo/proof-upgrades/` only. Reason: that page is
  the upgraded-row ledger, not the operator runbook for the whole demo.

## Spec Review Commands

Spec review commands used:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-public-demo-runbook --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-public-demo-runbook --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Review results:

- Sonnet spec review completed with full coverage and exited nonzero after
  writing review artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-public-demo-runbook/2026-06-18T062144-713Z-spec-claude-sonnet-4.6.clean.md`.
- Opus spec review completed with reduced coverage because Kiro reported denied
  tool access. Exact wrapper message: `Review coverage reduced: Kiro reported
  denied tool access. See meta analysisGaps.` Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-public-demo-runbook/2026-06-18T062144-732Z-spec-claude-opus-4.8.clean.md`.

## Validation

Spec phase validation to run on this branch:

- `git diff --check`
- `./scripts/check-private-paths.sh`

Spec phase validation results:

- `git diff --check` passed on 2026-06-18.
- `./scripts/check-private-paths.sh` passed on 2026-06-18 after staging the
  owned spec folder so the tracked-file guard included the new files.

Future implementation validation:

- `npm test` from `site/`.
- `npm run validate` from `site/`.
- `npm run build` from `site/`.
- Desktop and mobile browser sanity checks.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.

## Review Findings

- Sonnet found a High clarification issue for mixed coverage-label spelling.
  Patched Requirement 4 so future validation transcribes labels
  case-sensitively from cited artifacts instead of normalizing them.
- Sonnet found Medium issues requiring the runbook to avoid being interpreted
  as a demo-script implementation guide, to make `/proof-paths/`,
  `/validation/`, and `/limitations/` backlinks concrete, and to enumerate
  minimum private-text validator patterns. Patched `requirements.md`,
  `tasks.md`, and `design.md`.
- Opus found High contradictions between required red-flag/artifact vocabulary
  and whole-page forbidden-text validation. Patched the spec so the future
  validator allows artifact-family names and forbidden category labels only in
  sanctioned artifact-boundary, sharing-guidance, or red-flag sections, while
  rejecting actual raw/private content everywhere.
- Opus found a High contradiction between required AI/LLM red flags and the
  forbidden-positioning check. Patched Requirement 7 and tasks so the
  forbidden-positioning pattern is scoped outside sanctioned non-claim and
  red-flag sections.
- Opus found Medium issues for `impacted` validator scoping and discovery entry
  shape. Patched Requirement 4, Requirement 6, Requirement 7, and tasks.
- Opus noted that `design.md` exists for the review wrapper but was not listed
  in the review packet. Patched `review-packet.md`.
- Opus re-review found Medium issues requiring the focused runbook validator
  to be wired into aggregate `npm run validate`, companion `npm test` coverage
  for validator pass/fail cases, and an explicit canonical top-navigation
  requirement. Patched `requirements.md`, `tasks.md`, and `design.md`.
- Sonnet re-review found Low polish items for the design stop condition, a
  conditional discovery `description` field, and validation-result wording.
  Patched them.
- Final Sonnet re-review found Medium issues requiring the evidence-follow
  checklist step to be named distinctly and requiring the future implementer to
  verify whether discovery `description` is required. Patched
  `requirements.md` and `tasks.md`.
- Final Opus re-review found a High issue that discovery denied-phrase
  vocabulary is exempt only inside `nonClaims`, not `limitations`, and a Medium
  issue requiring sitemap registration to name `site/src/_site/pages.json`.
  Patched `requirements.md`, `tasks.md`, and `design.md`.
- Final Sonnet re-review found only Low issues after the discovery and sitemap
  patch. Patched the validation-result wording, `/demo/proof-assets/`
  back-link status, and discovery pre-check sequencing.
- Final Opus reduced-coverage re-review found one Medium issue requiring future
  fail-case tests to compose private-text fixtures at runtime so tests do not
  trip `./scripts/check-private-paths.sh` or violate the
  no-local-paths-in-tests rule. Patched `requirements.md`, `tasks.md`, and
  `design.md`.
- Final Opus full-coverage re-review found Medium issues requiring validator
  enforcement to distinguish pattern-detectable private content from
  authoring-review/private-denylist content, and requiring automated validation
  of required inbound links to `/demo/runbook/`. Patched `requirements.md`,
  `tasks.md`, and `design.md`.
- Final post-patch Sonnet re-review found no Medium or higher findings. Low
  clarity findings for `/demo/proof-assets/` and state wording were either
  already addressed or non-blocking.
- Final post-patch Opus full-coverage re-review found no Medium or higher
  findings. Optional Low polish for top-navigation wording,
  `/demo/proof-assets/` backlink wording, and the `check-private-paths.sh`
  cross-reference was patched.

## Oddities

- Existing public demo surfaces already cover walkthrough, result shape,
  evidence trail, proof upgrades, proof paths, validation, and limitations. The
  runbook should therefore connect and operationalize those surfaces instead of
  duplicating their proof rows.
- The future page may name raw artifact families to explain sharing boundaries,
  but must not publish raw generated content or local output roots.

## Follow-ups

- Future implementation must add route source, sitemap metadata, discovery
  metadata, focused validation, and cross-links.
- Future implementation must run browser sanity checks because the runbook is a
  content/layout page.
