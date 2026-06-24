# Site TraceMap Tools Review Packet Assembly Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks are checked only after the corresponding spec, review, future site
implementation, or validation work is complete.

## Spec-Only Phase

- [x] Create the spec packet with `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- [x] Keep all tracked changes inside
  `.kiro/specs/site-tracemap-tools-review-packet-assembly/`.
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
  required packet ingredients, required workflow sections, required stop
  conditions, required boundary wording, and forbidden raw/private material in
  the new spec files.
- [x] Update `implementation-state.md` with review commands, review findings,
  validation results, oddities, and follow-up items.

## Future Implementation Phase

- [x] Confirm this spec is `ready-for-implementation` before changing site
  source.
- [x] Choose final placement from `/packets/assembly/`, `/review-packet/`, a
  section on `/packets/`, or a section on `/review-room/`.
- [x] Record the final placement and rejected alternatives in
  `implementation-state.md`.
- [x] Add the concept-level page or section using existing static site layout,
  metadata, accessibility, and navigation patterns.
- [x] Include `Public claim level: concept` and `No public conclusion without
  evidence` on the rendered page or section.
- [x] State that the surface is a human review packet assembly workflow from
  existing evidence surfaces, not a generated packet-builder feature.
- [x] Publish the required packet ingredients: claim being reviewed, audience,
  proof path, public claim level, rule ID or rule family, evidence tier,
  coverage label, commit SHA, extractor version, public-safe file path and
  line span, limitations, non-claims, next owner, validation evidence, and
  unresolved gaps.
- [x] Use only the TraceMap evidence tier vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Keep coverage labels transcribed from the cited evidence surface and do
  not normalize unknown, reduced, partial, private-only, or pending coverage
  into stronger wording.
- [x] Add the required workflow sections: choose the question, collect
  public-safe evidence, attach limitations, name next owners, run claim
  checklist, stop conditions, and handoff notes.
- [x] Add stop conditions for missing proof path, private-only support, raw
  artifact leakage, unknown or reduced coverage without label, unsupported
  runtime, release, or safety wording, no next owner, and no validation
  evidence.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release approval or safety, operational
  safety, complete coverage, AI impact analysis, LLM analysis, autonomous
  review, and generated packet-builder behavior.
- [x] State that TraceMap does not replace human review, source review,
  ownership decisions, telemetry, logs, traces, APM, tests, release controls,
  incident response, manager judgment, service ownership, or database
  ownership.
- [x] Ensure examples are synthetic or already public-safe and do not include
  raw source, raw SQL, configuration values, secrets, local absolute paths, raw
  remotes, generated scan directories, private sample names, hidden capability
  names, real internal reviewer names, private owner names, or hidden
  validation details.
- [x] Link to `/packets/`, `/manager-packet/`, `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/review-room/`,
  `/review-claim-checklist/`, `/proof-source-catalog/`, `/proof-paths/`,
  `/limitations/`, and `/validation/` when those routes exist.
- [x] Record substitutions, omissions, or deferred links for adjacent routes
  that do not exist at implementation time.
- [x] Add standalone route metadata, discovery metadata, and sitemap metadata
  if implemented as a standalone route.
- [x] Record that section anchor and separate-sitemap notes are not applicable
  because the selected implementation is a standalone route.
- [x] Add focused validation for required copy, required links, metadata,
  discovery metadata, sitemap metadata if standalone, forbidden claims,
  private/raw material, and word count bounds.
- [x] Validate rendered text, decoded HTML, raw HTML attributes, and metadata
  for forbidden generated packet-builder, runtime, production,
  release-safety, operational-safety, AI/LLM, autonomous-review, and
  complete-coverage claims.
- [x] Validate rendered text, decoded HTML, raw HTML attributes, and metadata
  for forbidden raw or private material.
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
