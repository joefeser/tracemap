# Site TraceMap Tools Stakeholder Objection Guide Tasks

Status: not-started
Readiness: ready-for-implementation
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

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route or section choice, scope decisions, review results,
  validation plan, and implementation status before changing site code.
- [ ] Choose final placement: `/objections/`, `/questions/objections/`, a
  section on `/questions/`, a section on `/manager-faq/`, or a recorded
  equivalent if site information architecture has changed.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why the guide is an
  objection-to-evidence handoff rather than a proof claim, FAQ replacement,
  limitation replacement, release gate, or runtime workflow.
- [ ] Add the concept-level objection guide page or section using existing
  static-site layout, typography, accessibility, metadata, and validation
  patterns.
- [ ] Include visible `Public claim level: concept`.
- [ ] Include visible `No public conclusion without evidence`.
- [ ] Explain that objections are valid review inputs that route to evidence,
  stop conditions, limitations, and owners.
- [ ] Keep the route or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [ ] Include the required objection category `Does this prove runtime
  behavior?`.
- [ ] Include the required objection category `Can I use this for release
  approval?`.
- [ ] Include the required objection category `Does this show production
  traffic or endpoint performance?`.
- [ ] Include the required objection category `Is this AI analysis?`.
- [ ] Include the required objection category `Does missing evidence mean no
  impact?`.
- [ ] Include the required objection category `Can I share raw artifacts?`.
- [ ] Include the required objection category `Who owns the next answer?`.
- [ ] Include the required objection category `What do we do under reduced
  coverage?`.
- [ ] Ensure each objection row includes safe short answer, evidence to check,
  stop condition, next owner, public claim level, limitation/non-claim, and a
  supporting public route.
- [ ] Use `concept` for row-level public claim level unless exact public-safe
  evidence supports a stronger claim and the decision is recorded in
  `implementation-state.md`.
- [ ] Use public role categories for next owner values, not private people or
  team names.
- [ ] Include stable anchors for every objection row.
- [ ] Verify supporting routes resolve in generated output before linking.
- [ ] Record substitutions, deferrals, or unavailable candidate routes in
  `implementation-state.md`.
- [ ] Link only to public-safe pages, public-safe generated summaries,
  documentation, validation pages, limitation pages, demo pages, or proof-path
  pages.
- [ ] Do not link directly to raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output,
  hidden validation details, or credential-like values.
- [ ] Keep artifact names as local-output categories or non-shareable examples
  only, not public proof content.
- [ ] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI/LLM analysis, embeddings, vector databases, prompt
  classification, autonomous approval, release approval, and absence-of-impact
  proof.
- [ ] State that TraceMap does not replace tests, code review, source review,
  runtime observability, release process, service-owner review, or human
  judgment.
- [ ] Keep reduced or partial coverage visible and route unknowns to owners
  instead of smoothing them into clean conclusions.
- [ ] Avoid blame language around vendors, consultants, teams, service owners,
  reviewers, or code quality.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, route index metadata, and
  discovery metadata with `publicClaimLevel: concept`.
- [ ] If implemented as a section, add a stable in-page anchor and verify host
  page metadata remains concept-level or more conservative.
- [ ] Add safe inbound links only where they help readers choose the correct
  evidence surface and do not imply a new proof claim.
- [ ] Add focused validation for required objection categories and required
  row fields.
- [ ] Add focused validation for supporting link resolution and recorded route
  substitutions or deferrals.
- [ ] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when standalone.
- [ ] Add focused validation for section anchor and host-page metadata when
  sectioned.
- [ ] Add focused validation for visible word count bounds of 900 to 2,400
  words unless amended, counting rendered body prose and objection row cell
  content while excluding page-level navigation, breadcrumbs, site headers,
  site footers, metadata blocks, and row-field label headers.
- [ ] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  complete-coverage, AI/LLM, embedding, vector database, prompt
  classification, autonomous-approval, and absence-of-impact wording in
  rendered text, decoded HTML, raw HTML attributes, alt text, captions,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages, while allowing required objection titles and bounded safe-answer,
  stop-condition, non-claim, limitation, or objection-boundary contexts.
- [ ] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [ ] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
