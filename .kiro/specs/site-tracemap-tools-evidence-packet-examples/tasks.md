# Site TraceMap Tools Evidence Packet Examples Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks are checked only after the corresponding spec, review, future site
implementation, or validation work is complete.

## Spec-Only Phase

- [x] Create the spec packet with `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- [x] Keep all tracked changes inside
  `.kiro/specs/site-tracemap-tools-evidence-packet-examples/`.
- [x] Run Kiro spec review with `claude-opus-4.8` when available, or record
  the exact command and error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` when available, or record
  the exact command and error in `implementation-state.md`.
- [x] Patch or explicitly disposition all Medium or higher spec-review
  findings.
- [x] Rerun Kiro re-review where feasible after Medium or higher findings are
  patched.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run focused text checks for required status/readiness/claim-level copy,
  required examples, required packet fields, required placement alternatives,
  required neighboring route distinctions, required boundary wording,
  synthetic labeling, and forbidden raw/private material in the new spec files.
- [x] Update `implementation-state.md` with review commands, review findings,
  validation results, oddities, and follow-up items.

## Future Implementation Phase

- [x] Confirm this spec is `ready-for-implementation` before changing site
  source.
- [x] Choose final placement from `/packets/examples/`,
  `/examples/evidence-packets/`, a section on `/packets/`, or a section on
  `/packets/assembly/`.
- [x] Verify the selected standalone route or section anchor does not collide
  with an existing public route or anchor.
- [x] Record the final placement and rejected alternatives in
  `implementation-state.md`.
- [x] Add the concept-level page or section using existing static site layout,
  metadata, accessibility, and navigation patterns.
- [x] Include `Public claim level: concept` and
  `No public conclusion without evidence` on the rendered page or section.
- [x] Label examples as `synthetic public-safe example` unless backed by
  checked-in public demo artifacts.
- [x] Add the four required examples: demo-backed packet, reduced-coverage
  packet, gap-labeled packet, and stop-condition packet.
- [x] Include the required fields on every example: claim label, public claim
  level, proof path, rule ID or rule family, evidence tier, coverage label,
  synthetic public-safe path/span, commit or extractor placeholder, limitation,
  non-claim, next owner, and validation evidence.
- [x] Render stop-condition blocked fields with explicit blocked markers
  rather than omitting required field rows.
- [x] Use public-safe roles or review processes for next owner fields; do not
  use named people, private individuals, or internal team names.
- [x] Use only the TraceMap evidence tier vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Keep reduced, partial, syntax-only, unknown, gap, private-only, stopped,
  and demo-only coverage labels visible and do not normalize them into
  stronger wording.
- [x] Use only public-safe synthetic paths, spans, commit placeholders,
  extractor placeholders, and validation summaries unless checked-in public
  demo evidence supports a real value.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release approval, release safety,
  operational safety, complete coverage, AI impact analysis, LLM analysis,
  autonomous approval, autonomous review, and replacement of human review.
- [x] Avoid blame language around vendors, consultants, teams, maintainers,
  authors, or code quality.
- [x] Distinguish the examples surface from `/packets/`,
  `/packets/assembly/`, `/examples/scan-packet/`, `/demo/result/`,
  `/proof-source-catalog/`, and `/review-claim-checklist/`.
- [x] Link to neighboring routes when they exist and record substitutions,
  omissions, or deferred links for adjacent routes that do not exist at
  implementation time.
- [x] Add standalone route metadata, discovery metadata, and sitemap metadata
  if implemented as a standalone route.
- [x] Record the selected discovery `hintCategory` and justification in
  `implementation-state.md` if the implementation uses a fallback value.
- [x] Record section-anchor and sitemap notes if implemented as a section.
- [x] Add focused validation for example schema, labels, required links,
  metadata, discovery/sitemap metadata if standalone, forbidden claims,
  private/raw material, synthetic labeling, and word count bounds.
- [x] Validate rendered text, decoded HTML, raw HTML attributes, metadata,
  discovery metadata, tests, and fixtures for forbidden raw facts, SQLite,
  analyzer logs, source snippets, SQL, config values, secrets, local paths,
  raw remotes, generated scan directories, private sample names, raw command
  output, hidden validation details, credential-like values, and customer-like
  names.
- [x] Run `git diff --check` after implementation.
- [x] Run `./scripts/check-private-paths.sh` after implementation.
- [x] Run `npm test` from `site/` after implementation.
- [x] Run `npm run validate` from `site/` after implementation.
- [x] Run `npm run build` from `site/` after implementation.
- [x] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, unresolved
  gaps, and follow-up items.
