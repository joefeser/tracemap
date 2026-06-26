# Site TraceMap Tools Change-Risk Language Guide Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch or
explicitly disposition Medium or higher findings, then keep task status current
as implementation and validation complete.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch or explicitly disposition Medium or higher spec findings and rerun
  re-review where feasible.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are handled or explicitly dispositioned.

## Future Implementation Tasks

- [x] Confirm or update `implementation-state.md` with implementation branch,
  base, target PR base, selected scope, and current status before changing site
  code.
- [x] Verify current site information architecture, candidate slugs, and
  adjacent route existence before selecting placement; record findings in
  `implementation-state.md`.
- [x] Evaluate `/language/change-risk/`,
  `/review-claim-checklist/language/`, a section on
  `/review-claim-checklist/`, and a section on `/questions/objections/`.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] Add the future public page or section using existing static-site layout
  patterns.
- [x] Add visible copy that says `Public claim level: concept`.
- [x] Add visible copy that says `No public conclusion without evidence`.
- [x] Explain why wording matters without implying TraceMap proves impact,
  safety, runtime behavior, or release readiness.
- [x] Add the required `safe static-evidence phrases` section.
- [x] Add the required `unsafe phrases` section.
- [x] Add the required `evidence-required wording` section.
- [x] Add the required `reduced-coverage wording` section.
- [x] Add the required `owner-handoff wording` section.
- [x] Add the required `stop conditions` section.
- [x] Add the required `non-claims` section.
- [x] Add the required safe phrasing table.
- [x] Add the required unsafe/blocked phrasing table.
- [x] Add the required `when to use needs review` table.
- [x] Add the required `when to say evidence shows` table.
- [x] Include a `TraceMap found` row in the `when to say evidence shows` table
  that keeps the phrase scoped to deterministic static evidence only.
- [x] Add the required `when to say coverage is reduced` table.
- [x] Add the required `when to stop` table.
- [x] Ensure tables include conditions, allowed or blocked wording, and
  evidence or boundary reasons.
- [x] Add public-safe owner-handoff wording that asks for a decision without
  declaring impact, absence of impact, safety, or release readiness.
- [x] Add explicit non-claims for impact proof, absence-of-impact proof,
  release approval/safety, operational safety, runtime proof, production
  traffic, endpoint performance, complete coverage, AI/LLM analysis, and
  replacement of human judgment.
- [x] Avoid blame language throughout the page or section.
- [x] Avoid raw facts, SQLite indexes, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, remotes, generated scan directories,
  private sample names, command output, hidden validation details, and
  credential-like values.
- [x] Link to adjacent public-safe routes when they exist, especially
  `/review-claim-checklist/`, `/questions/objections/`,
  `/release-review-boundary/`, `/static-vs-runtime/`, `/proof-paths/faq/`,
  and `/manager-faq/`.
- [x] Resolve and document any expected route that is absent at implementation
  time.
- [x] Add title, description, canonical URL, and Open Graph metadata if a
  standalone route is chosen.
- [x] Add sitemap metadata if a standalone route is chosen.
- [x] Add discovery metadata with `publicClaimLevel: concept` if a standalone
  route is chosen.
- [x] If a standalone route is chosen, ensure discovery metadata includes
  `sourceType: site-page`, a valid existing `hintCategory`,
  `preferredProofPath`, limitations, and `nonClaims`.
- [x] Not applicable: folded placement was rejected, so standalone route
  metadata, sitemap metadata, and discovery metadata were added instead.
- [x] Add validation for required visible wording, required sections, required
  tables, required links, metadata, discovery/sitemap metadata where
  applicable, forbidden claims, private/raw material, credential-like values,
  and word-count bounds.
- [x] Enforce standalone word-count bounds of 1000 to 2400 rendered words, or
  folded-section bounds of 650 to 1600 rendered words.
- [x] Validate AI/LLM and private/raw-material non-claim wording without
  allowing affirmative product claims or private material exposure.
- [x] Add positive and negative tests for wording tables, forbidden claims, and
  forbidden private/raw material.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made, or document why they were deferred.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, partial-work labels if
  applicable, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, raw analyzer log content,
  private sample names, command output, and hidden validation details.
