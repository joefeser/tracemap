# Site TraceMap Tools Guided Proof-Path Tour Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch or
explicitly disposition Medium or higher findings, then keep task status
current as implementation and validation complete.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch or explicitly disposition Medium or higher spec findings and rerun
  re-review where feasible.
- [x] Update `Readiness` to `ready-for-implementation` in all five packet
  files (`requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, `review-packet.md`) only after Medium or higher
  review findings are patched or dispositioned.
- [x] Confirm `review-packet.md` review-focus checklist reflects the patched
  spec state and does not reference stale findings as open.

## Future Implementation Tasks

- [ ] Confirm or update `implementation-state.md` with branch, scope, route
  decision status, and implementation status before changing site code.
- [ ] Evaluate `/proof-paths/tour/`, `/demo/proof-path-tour/`, and a folded
  section on `/proof-paths/` before choosing placement.
- [ ] Record the selected route or placement and rejected alternatives in
  `implementation-state.md`.
- [ ] If a folded section is chosen and the containing route's page-level
  claim-level metadata is not `concept`, record how the visible
  `Public claim level: concept` section label is reconciled with page-level
  metadata.
- [ ] Distinguish the tour from `/proof-paths/`,
  `/proof-source-catalog/`, `/demo/evidence-trail/`, `/review-room/`,
  `/packets/`, `/validation/`, `/limitations/`, `/demo/runbook/`,
  `/review-claim-checklist/`, and `/glossary/`.
- [ ] Add the public guided proof-path tour page or section using existing
  static site layout patterns.
- [ ] Add visible copy that says `Public claim level: concept`.
- [ ] Add visible copy that says `No public conclusion without evidence`.
- [ ] Frame the page as a guided tour, not a proof engine, runtime trace,
  AI analysis, release approval, or operational approval flow.
- [ ] Include required proof steps: claim label, public claim level, proof
  path, rule ID or family, evidence tier, coverage label, commit SHA or source
  context, extractor version, supporting public route or artifact, limitation,
  non-claim, and next owner.
- [ ] Include at least one complete, authored, public-safe worked example that
  walks a single illustrative claim through every required proof step to an
  explicit bounded non-claim conclusion.
- [ ] Label every worked example as illustrative and not a real product claim.
- [ ] Include a stable `#where-to-stop` section that tells readers to stop
  when required evidence fields are absent or incomplete.
- [ ] Include a stable `#step-non-claim` anchor for the per-step non-claim
  field and reserve `#non-claims` for the boundary section.
- [ ] Include a stable `#non-claims` section for runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, complete coverage, AI/LLM impact analysis, raw artifact publication,
  embeddings, vector databases, and prompt classification.
- [ ] Ensure examples are authored and public-safe, without raw facts, raw
  SQLite, analyzer logs, raw source snippets, raw SQL, config values, secrets,
  local paths, raw remotes, generated scan directories, private sample names,
  or hidden validation details.
- [ ] Link to `/proof-paths/`, `/proof-source-catalog/`, `/validation/`, and
  `/limitations/`, `/review-claim-checklist/`, and `/glossary/` at minimum.
- [ ] Link to `/demo/evidence-trail/`, `/review-room/`, `/packets/`, and
  `/demo/runbook/` when those routes exist and the link text stays
  public-safe.
- [ ] Resolve and document any expected link that is absent, renamed, or
  replaced at implementation time before closing the link task.
- [ ] Add minimal safe cross-links from existing public routes where helpful.
- [ ] Add title, description, canonical URL, and Open Graph metadata if a
  standalone route is chosen.
- [ ] Add sitemap metadata if a standalone route is chosen.
- [ ] Add discovery metadata with `publicClaimLevel: concept` if a standalone
  route is chosen.
- [ ] If a standalone route is chosen, ensure discovery metadata follows the
  existing shape with `sourceType: site-page`, `hintCategory`,
  `preferredProofPath`, `limitations`, and `nonClaims`.
- [ ] Confirm `concept` is accepted by discovery and validation tooling before
  using it as standalone `publicClaimLevel`, and record the result in
  `implementation-state.md`.
- [ ] Confirm the current discovery `hintCategory` vocabulary before using the
  non-binding recommended value `evidence`, and record the selected value with
  rationale in `implementation-state.md`.
- [ ] Preserve stable anchors for required proof steps, `#where-to-stop`, and
  `#non-claims`, especially if the tour is folded into an existing page.
- [ ] Add focused validation for required copy, proof-step labels, stable
  anchors, public-safe links, metadata, sitemap/discovery coverage where
  applicable, forbidden claims, private/raw material, accessibility patterns,
  and word count.
- [ ] Decide and record in `implementation-state.md` the markup convention
  (CSS class, `data-` attribute, or comment sentinel) that marks sanctioned
  non-claim and boundary sections so validators can distinguish required
  boundary text from affirmative positioning.
- [ ] Validate forbidden AI/LLM positioning as affirmative product claims or
  outside sanctioned sections so required non-claims do not fail their own
  validator.
- [ ] Validate forbidden private/raw-material text as affirmative published
  content or outside sanctioned sections so boundary wording remains possible.
- [ ] Add positive and negative tests for required proof steps and forbidden
  claim/private-material validation.
- [ ] Add validation that every worked example is visibly labeled as
  illustrative and not a real product claim.
- [ ] Add validation that at least one worked example traverses the required
  proof-step fields to a bounded non-claim conclusion.
- [ ] Ensure validation checks rendered text, decoded HTML, and raw HTML
  attributes where appropriate.
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
  private sample names, and hidden validation details.
