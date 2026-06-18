# Site TraceMap Tools Adoption Playbook Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-adoption-playbook`
Implementation branch: `codex/impl-site-adoption-playbook`

## Scope

This implementation adds a concept-level `/adoption/` page that explains how a
team can introduce TraceMap into review workflows by starting with public demo
material, identifying a candidate repository, running deterministic scans,
reading evidence packets, making gaps explicit, and assigning follow-up
ownership.

Site code is implemented for this phase.

## Route Decision

Selected route: `/adoption/`.

Rejected route: `/playbook/`.

Reason: `/adoption/` is clearer as the public URL for teams introducing
TraceMap, while "playbook" remains the page framing and visible copy.

Verified routes linked from the page:

- `/demo/`
- `/demo/result/`
- `/docs/`
- `/validation/`
- `/limitations/`
- `/proof-paths/`
- `/review-room/`
- `/static-triage/`

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

Implementation validation completed on 2026-06-18:

- `git diff --check` passed.
- `npm test` from `site/` passed.
- `npm run validate` from `site/` passed.
- `npm run build` from `site/` passed.
- `./scripts/check-private-paths.sh` passed from the repository root.
- Desktop browser sanity check for `/adoption/` at 1440px width confirmed the
  expected title, H1, claim-level text, shared principle, and no horizontal
  overflow.
- Mobile browser sanity check for `/adoption/` at 390px width confirmed no
  horizontal overflow.

Oddity: an initial parallel run of `npm run validate` and `npm run build`
failed because both commands mutate generated site output. The commands passed
when rerun sequentially.

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

- No unresolved route gaps.
- Future site work can consider whether `/adoption/` should become a top-level
  navigation item after traffic or reader feedback justifies it.
