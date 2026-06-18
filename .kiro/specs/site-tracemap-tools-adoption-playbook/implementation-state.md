# Site TraceMap Tools Adoption Playbook Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-adoption-playbook`

Future implementation must update this field with the implementation branch
name before beginning site work.

## Scope

This branch creates a spec-only site phase for a future public adoption
playbook page. The future page should explain how a team can introduce
TraceMap into review workflows by starting with public demo material,
identifying a candidate repository, running deterministic scans, reading
evidence packets, making gaps explicit, and assigning follow-up ownership.

No site code is implemented in this phase.

## Route Decision

Not selected yet. Future implementation must choose `/adoption/` or
`/playbook/` based on the site information architecture at that time and record
the decision here.

## Claim Boundaries

- Safe to specify: a concept-level onboarding workflow for deterministic static
  evidence review.
- Safe to specify: public copy may discuss rule IDs, evidence tiers, coverage
  labels, limitations, generated artifacts, demo summaries, proof paths, and
  review ownership.
- Not safe to claim: runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, complete product coverage, or replacement of CI/CD,
  tests, telemetry, ownership, human review, release approval, incident
  response, or governance.
- Public copy must not expose raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw fact streams, SQLite indexes, or
  analyzer logs.

## Validation

Spec-phase validation (already run; do not repeat at implementation time):

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-adoption-playbook --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-adoption-playbook --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- `git diff --check`
- `./scripts/check-private-paths.sh`

`npm run validate` existence should be confirmed in `site/package.json` before
running future implementation validation; record as a gap if absent.

Future implementation validation is defined in `requirements.md` and
`tasks.md`.

## Review Findings

- Opus spec review ran with reduced coverage because Kiro reported denied tool
  access. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T031738-016Z-spec-claude-opus-4.8.clean.md`.
- Sonnet spec review ran with full coverage and reported Medium findings for
  validator conventions, discovery source files, forbidden-positioning
  determinism, validation script gap handling, route-gap handling, and
  word-count tooling. The spec was patched to address these findings. Saved
  output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T032014-786Z-spec-claude-sonnet-4.6.clean.md`.
- Sonnet re-review ran with full coverage and confirmed the Medium findings
  were resolved. It reported Low findings for shared denylist reuse and
  explicit word-count lower-bound enforcement; those clarifications were
  patched. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T032211-283Z-re-review-claude-sonnet-4.6.clean.md`.
- Final Sonnet re-review ran with full coverage and reported no Medium or
  higher findings. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T032312-409Z-re-review-claude-sonnet-4.6.clean.md`.
- Opus re-review ran with full coverage and reported a Medium finding that the
  shared denylist constant requested by the spec does not currently exist. The
  spec was patched to match the current neighboring inline denylist pattern,
  while allowing a future implementation to introduce a shared exported
  denylist only if neighboring validators migrate in the same change. Saved
  output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T032407-400Z-re-review-claude-opus-4.8.clean.md`.
- Second Opus re-review ran with full coverage and reported a Medium finding
  that `docs-index.json` is generated only from `repo-doc` entries, so a
  `site-page` adoption route must be expected in `routes-index.json` only. The
  spec was patched accordingly and the partial-analysis validation sentence was
  clarified. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T032713-451Z-re-review-claude-opus-4.8.clean.md`.
- Third Opus re-review ran with full coverage and reported a Medium finding
  that `llms.txt` route-section inclusion depends on a mapped `hintCategory`
  and should be asserted by the validator. The spec was patched to require an
  `llms.txt` route-section-compatible `hintCategory`, `routes-index.json`
  validation, and `llms.txt` route-section validation. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T033003-191Z-re-review-claude-opus-4.8.clean.md`.
- Final Sonnet re-review ran with full coverage and reported no Medium or
  higher findings. Remaining Low notes were informational. Saved output:
  `.tmp/kiro-reviews/site-tracemap-tools-adoption-playbook/2026-06-18T033453-840Z-re-review-claude-sonnet-4.6.clean.md`.

## Follow-Ups

- Future implementation must select the public route and record the route
  decision.
- Future implementation must verify the current public routes for demo, docs,
  validation, limitations, proof paths, review room, and static triage before
  linking.
- Future implementation must update this file with implementation scope,
  validation results, oddities, and unresolved route gaps.
