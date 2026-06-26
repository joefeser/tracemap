# Site TraceMap Tools Evidence Decision Record Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

This packet now includes the public-site implementation. Site source and
validation changed for the evidence decision record route; scanner code,
reducer code, generated outputs, runtime telemetry, decision automation,
approval workflow, and private evidence artifacts remain out of scope.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] If either automated review command is unavailable or blocked, record the
  exact command and error in `implementation-state.md` and perform manual
  review in its place.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.
- [x] Run spec-only validation: `git diff --check`.
- [x] Run spec-only validation: `./scripts/check-private-paths.sh`.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route or section choice, scope decisions, review results,
  validation plan, and implementation status before changing site code.
- [x] Choose final placement: `/decisions/evidence-record/`,
  `/review-room/decision-record/`, a section on `/review-room/`, a section on
  `/packets/assembly/`, or a recorded equivalent if site information
  architecture has changed.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why the surface is a
  decision-after-evidence record rather than a review-room agenda, packet
  assembly checklist, claim checklist, manager packet, objection guide,
  proof-path tour, release gate, runtime workflow, approval workflow, or
  autonomous decision system.
- [x] Record the selected placement rationale separately from the three
  unchosen candidate placements in the rejected-alternatives record.
- [x] Add the concept-level evidence decision record page or section using
  existing static-site layout, typography, accessibility, metadata, and
  validation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Include visible wording that TraceMap provides evidence, not the
  decision.
- [x] Keep the route or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [x] Include the required section `why record the decision`.
- [x] Include the required section `record template`.
- [x] Include the required section `example safe record`.
- [x] Include the required section `unsafe record examples`.
- [x] Include the required section `stop conditions`.
- [x] Include the required section `follow-up owners`.
- [x] Include the required section `non-claims`.
- [x] Include the required field `decision question`.
- [x] Include the required field `decision owner`.
- [x] Include the required field `public claim level`.
- [x] Include the required field `proof path`.
- [x] Include the required field `rule ID/family`.
- [x] Include the required field `evidence tier`.
- [x] Include the required field `coverage label`.
- [x] Include the required field `commit SHA`.
- [x] Include the required field `extractor version`.
- [x] Include the required field `limitation`.
- [x] Include the required field `non-claim`.
- [x] Include the required field `validation evidence`.
- [x] Include the required field `rejected interpretation`.
- [x] Include the required field `follow-up owner`.
- [x] Include the required field `review date placeholder`.
- [x] Include the required field `residual risk`.
- [x] Use only TraceMap evidence tier values: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Use `concept` for page-level and example record public claim level unless
  exact public-safe evidence supports a stronger claim and the decision is
  recorded in `implementation-state.md`.
- [x] Use public role categories or placeholders for decision owner and
  follow-up owner values, not private people or team names.
- [x] Include stable anchors for the template, safe example, unsafe examples,
  stop conditions, follow-up owners, and non-claims.
- [x] Verify supporting routes resolve in generated output before linking.
- [x] Record substitutions, deferrals, or unavailable candidate routes in
  `implementation-state.md`.
- [x] Link only to public-safe pages, public-safe generated summaries,
  documentation, validation pages, limitation pages, demo pages, proof-path
  pages, or private review locations named without raw material.
- [x] Do not link directly to raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output, hidden
  validation details, or credential-like values.
- [x] Keep artifact names as local-output categories or non-shareable examples
  only, not public proof content.
- [x] Add explicit non-claims for autonomous decisions, approval workflows,
  release approval, release safety, operational safety, runtime proof,
  production proof, endpoint performance proof, outage cause,
  absence-of-impact proof, complete coverage, AI/LLM analysis, embeddings,
  vector databases, prompt classification, and replacement of human judgment
  or governance.
- [x] State that TraceMap does not replace tests, code review, source review,
  runtime observability, release process, service-owner review, governance, or
  human judgment.
- [x] Keep reduced, partial, unknown, private-only, or unavailable coverage
  visible and route remaining questions to owners.
- [x] Avoid blame language around teams, vendors, consultants, service owners,
  reviewers, prior implementers, or code quality.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, route index metadata, and
  discovery metadata with `publicClaimLevel: concept`.
- [x] If implemented as a section, add a stable in-page anchor and verify host
  page metadata remains concept-level or more conservative.
- [x] Add focused validation for required record fields and required sections.
- [x] Add focused validation for supporting link resolution and recorded route
  substitutions or deferrals.
- [x] Add focused validation that the placement decision record collectively
  references all four spec-defined candidate placements: the selected
  placement with rationale plus the three unchosen rejected alternatives.
- [x] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when standalone.
- [x] Use the site's existing closed vocabulary for discovery source type and
  hint category.
- [x] Add focused validation for section anchor and host-page metadata when
  sectioned.
- [x] Add focused validation for visible word count bounds of 700 to 2500
  words unless amended, counting rendered body prose and record field values
  while excluding page-level navigation, breadcrumbs, site headers, site
  footers, metadata blocks, and repeated field-label headers.
- [x] If word count bounds are amended, record a rationale in
  `implementation-state.md`, keep the floor at or above 400 rendered words,
  and keep the ceiling at or below 2500 rendered words.
- [x] Add focused validation for forbidden approval, decision, release-safety,
  operational-safety, runtime-proof, production-proof,
  endpoint-performance, outage-cause, absence-of-impact, complete-coverage,
  AI/LLM, embedding, vector database, prompt-classification,
  autonomous-decision, and replacement-of-human-judgment wording in rendered
  text, decoded HTML, raw HTML attributes, alt text, captions, link anchor
  text, link title attributes, metadata, sitemap output, discovery output,
  generated pages, passing validation fixtures, and public-output fixtures,
  while allowing required non-claim, limitation, stop-condition,
  rejected-interpretation, residual-risk, and unsafe-example contexts.
- [x] Identify allowed forbidden-term contexts through stable structural
  markers such as section anchors, field-level containers, or validator-owned
  data attributes, and add negative tests proving unsupported positive claims
  cannot pass by hiding inside mislabeled wrappers.
- [x] Use the reference marker convention
  `data-tracemap-validation-context="<context>"` for allowed forbidden-term
  contexts, or record an equivalent substitute and rationale in
  `implementation-state.md`.
- [x] Add a structural-boundary negative fixture that places a forbidden
  positive claim inside `data-tracemap-validation-context="unsafe-example"`
  but outside any allowed unsafe-example section or field container, then
  asserts validation fails.
- [x] Exclude isolated failing negative fixtures from public-output absence
  sweeps and passing fixture sweeps, while still asserting those negative
  fixtures fail validation.
- [x] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, generated pages, passing
  validation fixtures, and public-output fixtures.
- [x] Add focused validation that example review date placeholders use a
  placeholder pattern such as `YYYY-MM-DD`, not real private review dates or
  internal cadence.
- [x] Add focused validation that example commit SHA and extractor version
  values use synthetic placeholders, redacted placeholders, or explicit
  public-demo provenance, not real private scan identifiers.
- [x] Add focused validation that example validation evidence values are
  synthetic, public-safe, or explicit public-demo summaries and exclude raw
  command output, private CI/job identifiers, hidden validation details, and
  credential-like values.
- [x] Add an accessibility check for the responsive record template, including
  semantic table headers or definition-list labels and screen-reader-accessible
  field names.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] If `npm run validate` requires explicit route or section registration,
  add the registration, document the pattern, and record it in
  `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [x] For section-only implementations, include browser scroll-to-section
  sanity checks on desktop and mobile.
- [x] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
