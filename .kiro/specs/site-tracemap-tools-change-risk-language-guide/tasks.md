# Site TraceMap Tools Change-Risk Language Guide Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch or
explicitly disposition Medium or higher findings, then keep task status current
as implementation and validation complete. This packet is spec-only; future
implementation tasks intentionally remain unchecked.

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

- [ ] Confirm or update `implementation-state.md` with implementation branch,
  base, target PR base, selected scope, and current status before changing site
  code.
- [ ] Verify current site information architecture, candidate slugs, and
  adjacent route existence before selecting placement; record findings in
  `implementation-state.md`.
- [ ] Evaluate `/language/change-risk/`,
  `/review-claim-checklist/language/`, a section on
  `/review-claim-checklist/`, and a section on `/questions/objections/`.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Add the future public page or section using existing static-site layout
  patterns.
- [ ] Add visible copy that says `Public claim level: concept`.
- [ ] Add visible copy that says `No public conclusion without evidence`.
- [ ] Explain why wording matters without implying TraceMap proves impact,
  safety, runtime behavior, or release readiness.
- [ ] Add the required `safe static-evidence phrases` section.
- [ ] Add the required `unsafe phrases` section.
- [ ] Add the required `evidence-required wording` section.
- [ ] Add the required `reduced-coverage wording` section.
- [ ] Add the required `owner-handoff wording` section.
- [ ] Add the required `stop conditions` section.
- [ ] Add the required `non-claims` section.
- [ ] Add the required safe phrasing table.
- [ ] Add the required unsafe/blocked phrasing table.
- [ ] Add the required `when to use needs review` table.
- [ ] Add the required `when to say evidence shows` table.
- [ ] Include a `TraceMap found` row in the `when to say evidence shows` table
  that keeps the phrase scoped to deterministic static evidence only.
- [ ] Add the required `when to say coverage is reduced` table.
- [ ] Add the required `when to stop` table.
- [ ] Ensure tables include conditions, allowed or blocked wording, and
  evidence or boundary reasons.
- [ ] Add public-safe owner-handoff wording that asks for a decision without
  declaring impact, absence of impact, safety, or release readiness.
- [ ] Add explicit non-claims for impact proof, absence-of-impact proof,
  release approval/safety, operational safety, runtime proof, production
  traffic, endpoint performance, complete coverage, AI/LLM analysis, and
  replacement of human judgment.
- [ ] Avoid blame language throughout the page or section.
- [ ] Avoid raw facts, SQLite indexes, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, remotes, generated scan directories,
  private sample names, command output, hidden validation details, and
  credential-like values.
- [ ] Link to adjacent public-safe routes when they exist, especially
  `/review-claim-checklist/`, `/questions/objections/`,
  `/release-review-boundary/`, `/static-vs-runtime/`, `/proof-paths/faq/`,
  and `/manager-faq/`.
- [ ] Resolve and document any expected route that is absent at implementation
  time.
- [ ] Add title, description, canonical URL, and Open Graph metadata if a
  standalone route is chosen.
- [ ] Add sitemap metadata if a standalone route is chosen.
- [ ] Add discovery metadata with `publicClaimLevel: concept` if a standalone
  route is chosen.
- [ ] If a standalone route is chosen, ensure discovery metadata includes
  `sourceType: site-page`, a valid existing `hintCategory`,
  `preferredProofPath`, limitations, and non-claims.
- [ ] If a folded placement is chosen, add stable section anchors and
  discoverable inbound links without conflicting with the containing page's
  claim level.
- [ ] Add validation for required visible wording, required sections, required
  tables, required links, metadata, discovery/sitemap metadata where
  applicable, forbidden claims, private/raw material, credential-like values,
  and word-count bounds.
- [ ] Enforce standalone word-count bounds of 1000 to 2400 rendered words, or
  folded-section bounds of 650 to 1600 rendered words.
- [ ] Validate AI/LLM and private/raw-material non-claim wording without
  allowing affirmative product claims or private material exposure.
- [ ] Add positive and negative tests for wording tables, forbidden claims, and
  forbidden private/raw material.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made, or document why they were deferred.
- [ ] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, partial-work labels if
  applicable, and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, raw analyzer log content,
  private sample names, command output, and hidden validation details.
