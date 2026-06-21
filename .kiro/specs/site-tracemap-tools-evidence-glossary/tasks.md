# Site TraceMap Tools Evidence Glossary Tasks

Status: implemented
Readiness: implemented
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

- [x] Confirm or update `implementation-state.md` with branch, scope, route
  decision status, and initial implementation status before changing site code.
- [x] Evaluate `/glossary/`, `/docs/evidence-glossary/`, and folding the
  glossary into an existing public-safe route before choosing placement.
- [x] Record the selected route or placement and rejected alternatives in
  `implementation-state.md`.
- [x] Reconcile required glossary definitions with existing public vocabulary
  surfaces, especially `/evidence/`, `/proof-paths/`, and
  `/proof-source-catalog/`, and record canonical-source decisions in
  `implementation-state.md`.
- [x] Add the public evidence glossary page or section using existing static
  site layout patterns.
- [x] Add visible copy that says `Public claim level: concept`.
- [x] Add visible copy that says `No public conclusion without evidence`.
- [x] Address engineers, reviewers, managers, architects, and agents who need
  stable vocabulary before repeating public TraceMap claims.
- [x] Include required glossary terms: rule ID, evidence tier, proof path,
  coverage label, limitation, analysis gap, commit/source context, extractor
  version, supporting IDs, public claim level, and local-only artifact family.
- [x] For each required term, include a definition, public use, and limitation
  without implying the term is fully shipped everywhere.
- [x] Define evidence tiers
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` conservatively.
- [x] Explain local-only artifact families without linking to or publishing raw
  facts, SQLite indexes, analyzer logs, raw source snippets, raw SQL, config
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, or hidden validation details.
- [x] Add an explicit non-claims section for runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI/LLM impact analysis, complete product coverage, and raw artifact
  publication.
- [x] Link to existing public-safe routes such as `/proof-paths/`,
  `/evidence/`, `/validation/`, `/limitations/`, `/roadmap/`,
  `/capabilities/`, `/manager-brief/`, `/review-claim-checklist/`,
  `/proof-source-catalog/`, and `/docs/` where those routes exist.
- [x] Enforce the minimum required link set: `/evidence/`, `/proof-paths/`,
  `/proof-source-catalog/`, and `/limitations/`.
- [x] Resolve and document any expected link that is absent at implementation
  time before closing the link task.
- [x] Add minimal safe cross-links from existing public proof, validation,
  limitation, roadmap, capability, or manager routes where helpful.
- [x] Add title, description, canonical URL, and Open Graph metadata if a
  standalone route is chosen.
- [x] Add sitemap metadata if a standalone route is chosen.
- [x] Add discovery metadata with `publicClaimLevel: concept` if a standalone
  route is chosen.
- [x] If a standalone route is chosen, ensure discovery metadata follows the
  existing shape with `sourceType: site-page`, `hintCategory`,
  `preferredProofPath`, `limitations`, and `nonClaims`.
- [x] Confirm `concept` is accepted by discovery and validation tooling before
  using it as standalone `publicClaimLevel`, and record the result in
  `implementation-state.md`.
- [x] Preserve stable anchors for required terms and the non-claims section
  (`#non-claims`), especially if the glossary is folded into an existing page.
- [x] Add focused validation for required copy, required term list, stable
  anchors, public-safe links, forbidden positioning/private text, metadata,
  sitemap/discovery coverage where applicable, and word count.
- [x] Validate forbidden AI/LLM positioning as affirmative product claims or
  outside sanctioned sections so the required non-claims section does not fail
  its own validator.
- [x] Validate forbidden private/raw-material text as affirmative published
  content or outside sanctioned sections so boundary wording in the
  `local-only artifact family` entry and non-claims section remains possible.
- [x] Add positive and negative tests for required terms and forbidden
  claim/private-material validation.
- [x] Ensure validation checks rendered text, decoded HTML, and raw HTML
  attributes for forbidden overclaims and private/raw material.
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
  secrets, raw facts, raw SQLite index paths, raw analyzer log content, private
  sample names, and hidden validation details.
