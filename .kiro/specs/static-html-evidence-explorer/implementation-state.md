# Static HTML Evidence Explorer Implementation State

Status: spec-ready
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-static-html-evidence-explorer`
Base: `origin/dev`

## Scope

This is a spec-only branch for a future local static HTML evidence explorer.
The future feature should generate a local browser artifact from existing
TraceMap outputs so reviewers can navigate sources, coverage, surfaces, paths,
gaps, rules, limitations, and evidence rows.

No product code is implemented in this branch. Future implementation must not
add hosted services, hidden telemetry, live backends, runtime code analysis,
LLM calls, embeddings, vector databases, or prompt-based classification.

## Claim Level

Selected level: `concept`.

Rationale: the explorer is proposed but not implemented or validated with
public-safe fixtures. The spec requires public/demo safety validation before
any stronger demo claim.

## Scope Decisions

- The explorer is a local generated artifact, not the public `tracemap.tools`
  site.
- The explorer renders existing generated TraceMap artifacts and does not
  rescan source code or derive new conclusions.
- Core evidence boundaries remain unchanged: claims require rule IDs, evidence
  tiers, support IDs, coverage labels where available, and visible limitations.
- Scanner-only facts and path evidence must not use impact wording. Impact
  labels are allowed only for reducer-backed rows with supporting evidence.
- Public/demo output remains strict. Hidden/local output may redact, hash,
  category-label, or omit values only with visible labeling and manifest
  counts.
- No raw snippets are rendered by default. Any future snippet display must be
  explicit, hidden/local, and recorded in the explorer manifest.
- The spec intentionally includes accessibility, JavaScript-disabled baseline,
  no-network assets, and byte-stability requirements because local demos and
  review sessions should not depend on external services.

## Spec Review Commands And Results

Planned commands:

- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-opus-4.8` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with full coverage. Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T233536-940Z-spec-claude-opus-4.8.*`.
  Findings: 2 blocking and 4 important non-blocking items, plus suggested
  test and wording improvements. Patched safety wording, readiness consistency,
  manifest policy definitions, safety-policy reuse, closed-vocabulary source of
  truth, three absence-state tests, generated-site separation, data parity, and
  validation gaps.
- `claude-sonnet-4.6` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T233537-002Z-spec-claude-sonnet-4.6.*`.
  Findings: 4 blocking and 8 important non-blocking items. Patched rule catalog
  anchoring, review bookkeeping, endpoint-address wording, byte-stability test
  scope, provenance conflict policy, no-JavaScript baseline scope, safe-label
  derivation, safety-failure testing, safety slice ordering, and safety profile
  definition.

Re-review limit: at most two re-review cycles. Use `claude-sonnet-4.6` for the
final re-review unless Opus is clearly needed.

Follow-up re-review:

- `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T234122-308Z-re-review-claude-sonnet-4.6.*`.
  Findings: no blocking issues. Patched requested handoff clarifications for
  `facts.ndjson` raw-fact filtering, PR 1 rule-catalog gating, provenance
  conflict subtyping, and no-JavaScript row-threshold determinism.
- Final `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T234314-708Z-re-review-claude-sonnet-4.6.*`.
  Findings: no blocking issues. Patched final bookkeeping and minor
  cross-reference items: spec-review task status, source-map byte-stability
  wording, and manifest policy allowed-value cross-reference. No further Kiro
  re-review was run because the requested maximum of two re-review cycles had
  been reached.

## Validation

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed: `Private path guard passed.`

## Oddities

- The feature overlaps with existing Markdown, vault, docs-export, report, and
  public demo workflows, but this spec keeps the HTML explorer local and
  generated rather than hosted or marketing-oriented.
- The future implementation may choose a command name during CLI design. The
  requirements define behavior and safety boundaries rather than forcing a
  specific command spelling.
- No `review-prompts.md` is used for this spec because the standard
  `scripts/kiro-review.mjs` prompt includes the four spec files and the
  requirements/design/tasks are self-contained.

## Follow-Ups

- Future implementation should choose the exact CLI surface and update this
  state file with validation results.
- Future implementation should update rule catalog documentation for any
  explorer-specific rule IDs that are added.
- PR 1 must not ship generated explorer output until explorer rule catalog
  stubs are present for any new explorer-specific gaps, limitations, or
  validation failures.
- If PR 1 reads `facts.ndjson` directly, it must treat fact values as raw
  evidence and route them through the existing safety policy before rendering
  or embedding them.
