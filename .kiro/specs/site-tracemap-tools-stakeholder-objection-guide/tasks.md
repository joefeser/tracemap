# Site TraceMap Tools Stakeholder Objection Guide Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

This is a spec-only packet. Do not implement site source, scanner code,
reducer code, generated outputs, validation scripts, or public copy on this
branch. Future implementation tasks remain unchecked until a later
implementation phase completes them.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.
- [x] Run spec-only validation: `git diff --check`.
- [x] Run spec-only validation: `./scripts/check-private-paths.sh`.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route or section choice, scope decisions, review results,
  validation plan, and implementation status before changing site code.
- [x] Choose final placement: `/objections/`, `/questions/objections/`, a
  section on `/questions/`, a section on `/manager-faq/`, or a recorded
  equivalent if site information architecture has changed.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why the guide is an
  objection-to-evidence handoff rather than a proof claim, FAQ replacement,
  limitation replacement, release gate, or runtime workflow.
- [x] Add the concept-level objection guide page or section using existing
  static-site layout, typography, accessibility, metadata, and validation
  patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Explain that objections are valid review inputs that route to evidence,
  stop conditions, limitations, and owners.
- [x] Keep the route or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [x] Include the required objection category `Does this prove runtime
  behavior?`.
- [x] Include the required objection category `Can I use this for release
  approval?`.
- [x] Include the required objection category `Does this show production
  traffic or endpoint performance?`.
- [x] Include the required objection category `Is this AI analysis?`.
- [x] Include the required objection category `Does missing evidence mean no
  impact?`.
- [x] Include the required objection category `Can I share raw artifacts?`.
- [x] Include the required objection category `Who owns the next answer?`.
- [x] Include the required objection category `What do we do under reduced
  coverage?`.
- [x] Ensure each objection row includes safe short answer, evidence to check,
  stop condition, next owner, public claim level, limitation/non-claim, and a
  supporting public route.
- [x] Use `concept` for row-level public claim level unless exact public-safe
  evidence supports a stronger claim and the decision is recorded in
  `implementation-state.md`.
- [x] Use public role categories for next owner values, not private people or
  team names.
- [x] Include stable anchors for every objection row.
- [x] Verify supporting routes resolve in generated output before linking.
- [x] Record substitutions, deferrals, or unavailable candidate routes in
  `implementation-state.md`.
- [x] Link only to public-safe pages, public-safe generated summaries,
  documentation, validation pages, limitation pages, demo pages, or proof-path
  pages.
- [x] Do not link directly to raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output,
  hidden validation details, or credential-like values.
- [x] Keep artifact names as local-output categories or non-shareable examples
  only, not public proof content.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI/LLM analysis, embeddings, vector databases, prompt
  classification, autonomous approval, release approval, and absence-of-impact
  proof.
- [x] State that TraceMap does not replace tests, code review, source review,
  runtime observability, release process, service-owner review, or human
  judgment.
- [x] Keep reduced or partial coverage visible and route unknowns to owners
  instead of smoothing them into clean conclusions.
- [x] Avoid blame language around vendors, consultants, teams, service owners,
  reviewers, or code quality.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, route index metadata, and
  discovery metadata with `publicClaimLevel: concept`.
- [x] If implemented as a section, add a stable in-page anchor and verify host
  page metadata remains concept-level or more conservative. Not applicable:
  standalone `/questions/objections/` route selected and validated.
- [x] Add safe inbound links only where they help readers choose the correct
  evidence surface and do not imply a new proof claim.
- [x] Add focused validation for required objection categories and required
  row fields.
- [x] Add focused validation for supporting link resolution and recorded route
  substitutions or deferrals.
- [x] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when standalone.
- [x] Add focused validation for section anchor and host-page metadata when
  sectioned. Not applicable: standalone route selected.
- [x] Add focused validation for visible word count bounds of 900 to 2,400
  words unless amended, counting rendered body prose and objection row cell
  content while excluding page-level navigation, breadcrumbs, site headers,
  site footers, metadata blocks, and row-field label headers.
- [x] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  complete-coverage, AI/LLM, embedding, vector database, prompt
  classification, autonomous-approval, and absence-of-impact wording in
  rendered text, decoded HTML, raw HTML attributes, alt text, captions,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages, while allowing required objection titles and bounded safe-answer,
  stop-condition, non-claim, limitation, or objection-boundary contexts.
- [x] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [x] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
