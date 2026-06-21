# Site TraceMap Tools Evidence Review Room Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch
Medium or higher findings where feasible, then keep task status current as
implementation and validation complete.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.

## Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, and initial implementation status before changing site code.
- [x] Add a bounded `/review-room/` concept page using existing static site
  layout patterns.
- [x] Add the `Public claim level: concept` label and shared site principle.
- [x] Address managers, reviewers, architects, and engineers in a review
  meeting deciding what static dependency evidence is known, partial, or
  missing.
- [x] Include the review agenda items: claim, proof path, rule ID/evidence
  tier, coverage label, limitation, and owner decision gap.
- [x] State static evidence boundaries and explicitly avoid runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI/LLM impact analysis, and complete product coverage
  claims.
- [x] Avoid publishing raw facts, SQLite indexes, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, and private sample names.
- [x] Link the page to proof paths, evidence, validation, limitations, manager
  brief, manager packet, and related incident/review orientation.
- [x] Add title, description, canonical URL, and Open Graph metadata.
- [x] Add sitemap metadata for `/review-room/` in
  `site/src/_site/pages.json`.
- [x] Add discovery metadata for `/review-room/` in
  `site/src/_site/discovery.json` with claim level `concept`.
- [x] Add minimal safe cross-links from relevant existing pages such as
  `/manager-brief/`, `/manager-packet/`, and `/incident-call/`.
- [x] Add focused validation for required copy, required links, forbidden
  positioning/private text, route metadata, sitemap coverage, and word count.
- [x] Wire `validateReviewRoomDist` from `site/scripts/review-room.mjs` into
  `site/scripts/validate.mjs` alongside the existing concept validators.

## Validation Tasks

- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for `/review-room/` or
  document why they were deferred.
- [x] Update `implementation-state.md` with final validation and follow-up
  notes.
