# Site TraceMap Tools Proof Source Catalog Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

These tasks are future implementation work. They remain unchecked because this
phase is spec-only.

## Spec-Only Delivery Validation

- Before merging this spec branch, validate that the branch is spec-only and
  that all future implementation tasks below remain unchecked. CI enforcement
  for this gate is deferred to a future tooling task; reviewers must verify it
  manually.
- Confirm or update this spec-local `implementation-state.md` with branch,
  scope, review results, validation results, oddities, and follow-ups before
  changing site code.

## Future Implementation Tasks
- [x] Reconfirm that the page-level public claim level remains `demo`; if
  implementation evidence changes, record the justification before changing the
  page-level claim level. Confirm that page-level `demo` is not a ceiling: a
  row-level `shipped` row is not downgraded by the page label, and a row-level
  `concept` or `hidden` row is not upgraded by it.
- [x] Evaluate adding the catalog as a section of `/proof-paths/` before
  choosing the standalone `/proof-source-catalog/` route.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why `/proof-paths/` was or was not
  extended.
- [x] Add the proof source catalog page or section using existing static-site
  layout and metadata patterns.
- [x] Add visible copy that says `Public claim level: demo`.
- [x] Add a catalog table or equivalent scannable layout with route, claim
  label, allowed public wording or claim family, `Public claim level`, proof
  path, source artifact or source document, rule ID or rule family, evidence
  tier or coverage label, limitation, and non-claims.
- [x] Ensure every row includes the required `Public claim level` field with
  exactly one of `shipped`, `demo`, `concept`, or `hidden`.
- [x] Define one claim-level mapping table from existing site vocabulary to
  `shipped`, `demo`, `concept`, and `hidden`, covering route metadata,
  capability matrix statuses, roadmap wording, proof-path public statuses, and
  repository-doc source entries.
- [x] Map existing `main`, `shipped`, `shipped navigation`, repository docs on
  `main`, and `main with maturity caveats` to catalog `shipped`, while keeping
  maturity caveats in row limitations.
- [x] Map existing `demo`, `demo guidance`, `main/demo`, `public-demo`,
  checked-in public-safe demo summary, route metadata `publicClaimLevel: demo`,
  and proof-path public status `demo` to catalog `demo`.
- [x] Map existing `concept`, `concept-only`, `future`, `future-only`, `dev`,
  `dev-only`, route metadata `publicClaimLevel: concept`, and proof-path public
  status `future` to catalog `concept`.
- [x] Map hidden or internal-only placeholders to catalog `hidden` without
  disclosing unreleased names, route names, private samples, counts, cadence,
  sequencing, or in-flight status.
- [x] Define one evidence-status mapping table using only
  `source-backed`, `demo-evidence-backed`, `partial-or-reduced`,
  `gap-labeled-demo`, `future-only`, `hidden-or-internal`, and
  `not-yet-backed`.
- [x] Ensure no published catalog row uses `not-yet-backed`; any candidate row
  that requires `not-yet-backed` must be removed or rewritten before the page
  publishes. Publishing is blocked until this task passes.
- [x] Validate that the published claim-level mapping table matches the
  required starting mapping in Requirement 3, or record approved divergence
  with rationale in `implementation-state.md`.
- [x] Enumerate the status vocabulary currently used by `/capabilities/`,
  `/roadmap/`, and `/proof-paths/` source and assert each token maps to exactly
  one catalog claim level; fail on any unmapped token.
- [x] Validate that the published evidence-status mapping table matches the
  required starting mapping in Requirement 4, or record approved divergence
  with rationale in `implementation-state.md`.
- [x] Validate that every published row's claim-level and evidence-status pair
  matches the allowed matrix from Requirement 4, and reject hidden evidence
  status on non-hidden rows.
- [x] Preserve public-safe rule IDs where available and use rule-family labels
  with limitations where rule IDs are unavailable or too specific to publish.
- [x] Preserve evidence tier names `Tier1Semantic`, `Tier2Structural`,
  `Tier3SyntaxOrTextual`, and `Tier4Unknown` when cited by source material.
- [x] Transcribe coverage labels from cited source material, including labels
  such as `FullEvidenceAvailable`, `PartialAnalysis`, `not_requested`, and
  `unavailable`, instead of normalizing them into stronger wording.
- [x] Distinguish source-of-truth artifact family from proof-path link for each
  row.
- [x] Link proof paths only to public routes, checked-in public-safe repository
  docs, rule catalog entries, public-safe generated summaries, or sanitized
  public-demo report summaries.
- [x] For local-only scanner facts, SQLite indexes, scan reports, analyzer
  logs, or generated scan directories, name only the artifact family and link to
  a public-safe summary or route.
- [x] Ensure no row publishes raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, or
  hidden private-work details.
- [x] Add row-level limitations and non-claims for demo-only, concept-only,
  partial, reduced, gap-labeled, hidden, or unsupported wording.
- [x] Add a global non-claims section rejecting runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, and complete product coverage
  claims.
- [x] Add stable row identifiers or anchors for future automated claim review.
- [x] If implemented as `/proof-source-catalog/`, add route metadata, sitemap
  metadata, and discovery metadata with `publicClaimLevel: demo`.
- [x] Add safe cross-links from `/proof-paths/`, `/roadmap/`,
  `/capabilities/`, `/docs/`, `/validation/`, and `/limitations/` only where
  the link helps readers verify public wording.
- [x] Add focused validation for required labels, allowed claim levels, allowed
  evidence-status labels, required links, stable row anchors, route metadata,
  forbidden private/raw text, and forbidden overclaiming.
- [x] Validate the forbidden public wording pattern list from `design.md`; if
  the implementation extends the list, preserve all existing entries or record
  the reason for any approved change in `implementation-state.md`.
- [x] Add validation that every row includes all required Requirement 2 fields
  and that limitation and non-claims are non-empty.
- [x] Add validation that hidden rows collapse to at most one aggregate
  placeholder and reject any count, cadence, sequencing, in-flight status, or
  per-capability hidden naming.
- [x] Add validation that the hidden aggregate placeholder's `proofPath` is the
  bare sentinel `hidden`.
- [x] Add validation that any row whose primary content duplicates a
  `/proof-paths/` evidence trail links to that trail rather than restating it
  inline.
- [x] Add validation that every catalog row anchor either equals the reserved
  hidden anchor `proof-source-hidden-aggregate-placeholder` or conforms to the
  format `proof-source-{route-slug}-{claim-slug}` derived from `route` and
  `claimLabel`.
- [x] Add validation that every catalog row anchor is unique.
- [x] Record the future implementation's chosen word-count bound in
  `implementation-state.md` before checking word-count validation complete.

## Final Validation Gate

Note: these tasks are implementation-phase gate checks. They are not runnable
on this spec-only branch. Do not mark them complete until a site implementation
branch changes site code.

- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/` after implementation changes.
- [x] Run `npm run validate` from `site/` after implementation changes.
- [x] Run `npm run build` from `site/` after implementation changes.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [x] Update this spec's `implementation-state.md` with placement decisions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, raw analyzer log content, private
  sample names, and hidden private-work details.
