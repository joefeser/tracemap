# Site TraceMap Tools Evidence Glossary Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch or
explicitly disposition Medium or higher findings, then keep task status current
as implementation and validation complete.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
  Note: if `claude-opus-4.8` is unavailable, record the exact unavailability
  message in `implementation-state.md` and close this checkbox with that note.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch or explicitly disposition Medium or higher spec findings and rerun
  re-review where feasible.

## Implementation Tasks

- [ ] Confirm or update `implementation-state.md` with branch, scope, route
  decision status, and initial implementation status before changing site code.
- [ ] Evaluate `/glossary/`, `/docs/evidence-glossary/`, and folding the
  glossary into an existing public-safe route before choosing placement.
- [ ] Record the selected route or placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Reconcile required glossary definitions with existing public vocabulary
  surfaces, especially `/evidence/`, `/proof-paths/`, and
  `/proof-source-catalog/`, and record canonical-source decisions in
  `implementation-state.md`.
- [ ] Add the public evidence glossary page or section using existing static
  site layout patterns.
- [ ] Add visible copy that says `Public claim level: concept`.
- [ ] Add visible copy that says `No public conclusion without evidence`.
- [ ] Address engineers, reviewers, managers, architects, and agents who need
  stable vocabulary before repeating public TraceMap claims.
- [ ] Include required glossary terms: rule ID, evidence tier, proof path,
  coverage label, limitation, analysis gap, commit/source context, extractor
  version, supporting IDs, public claim level, and local-only artifact family.
- [ ] For each required term, include a definition, public use, and limitation
  without implying the term is fully shipped everywhere.
- [ ] Define evidence tiers
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` conservatively.
- [ ] Explain local-only artifact families without linking to or publishing raw
  facts, SQLite indexes, analyzer logs, raw source snippets, raw SQL, config
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, or hidden validation details.
- [ ] Add an explicit non-claims section for runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI/LLM impact analysis, complete product coverage, and raw artifact
  publication.
- [ ] Link to existing public-safe routes such as `/proof-paths/`,
  `/evidence/`, `/validation/`, `/limitations/`, `/roadmap/`,
  `/capabilities/`, `/manager-brief/`, `/review-claim-checklist/`,
  `/proof-source-catalog/`, and `/docs/` where those routes exist.
- [ ] Enforce the minimum required link set: `/evidence/`, `/proof-paths/`,
  `/proof-source-catalog/`, and `/limitations/`.
- [ ] Resolve and document any expected link that is absent at implementation
  time before closing the link task.
- [ ] Add minimal safe cross-links from existing public proof, validation,
  limitation, roadmap, capability, or manager routes where helpful.
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
- [ ] Preserve stable anchors for required terms and the non-claims section
  (`#non-claims`), especially if the glossary is folded into an existing page.
- [ ] Add focused validation for required copy, required term list, stable
  anchors, public-safe links, forbidden positioning/private text, metadata,
  sitemap/discovery coverage where applicable, and word count.
- [ ] Validate forbidden AI/LLM positioning as affirmative product claims or
  outside sanctioned sections so the required non-claims section does not fail
  its own validator.
- [ ] Validate forbidden private/raw-material text as affirmative published
  content or outside sanctioned sections so boundary wording in the
  `local-only artifact family` entry and non-claims section remains possible.
- [ ] Add positive and negative tests for required terms and forbidden
  claim/private-material validation.
- [ ] Ensure validation checks rendered text, decoded HTML, and raw HTML
  attributes for forbidden overclaims and private/raw material.
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
  secrets, raw facts, raw SQLite index paths, raw analyzer log content, private
  sample names, and hidden validation details.
