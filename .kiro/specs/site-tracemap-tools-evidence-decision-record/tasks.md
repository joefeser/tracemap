# Site TraceMap Tools Evidence Decision Record Tasks

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
- [x] If either automated review command is unavailable or blocked, record the
  exact command and error in `implementation-state.md` and perform manual
  review in its place.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.
- [x] Run spec-only validation: `git diff --check`.
- [x] Run spec-only validation: `./scripts/check-private-paths.sh`.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route or section choice, scope decisions, review results,
  validation plan, and implementation status before changing site code.
- [ ] Choose final placement: `/decisions/evidence-record/`,
  `/review-room/decision-record/`, a section on `/review-room/`, a section on
  `/packets/assembly/`, or a recorded equivalent if site information
  architecture has changed.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why the surface is a
  decision-after-evidence record rather than a review-room agenda, packet
  assembly checklist, claim checklist, manager packet, objection guide,
  proof-path tour, release gate, runtime workflow, approval workflow, or
  autonomous decision system.
- [ ] Record the selected placement rationale separately from the three
  unchosen candidate placements in the rejected-alternatives record.
- [ ] Add the concept-level evidence decision record page or section using
  existing static-site layout, typography, accessibility, metadata, and
  validation patterns.
- [ ] Include visible `Public claim level: concept`.
- [ ] Include visible `No public conclusion without evidence`.
- [ ] Include visible wording that TraceMap provides evidence, not the
  decision.
- [ ] Keep the route or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [ ] Include the required section `why record the decision`.
- [ ] Include the required section `record template`.
- [ ] Include the required section `example safe record`.
- [ ] Include the required section `unsafe record examples`.
- [ ] Include the required section `stop conditions`.
- [ ] Include the required section `follow-up owners`.
- [ ] Include the required section `non-claims`.
- [ ] Include the required field `decision question`.
- [ ] Include the required field `decision owner`.
- [ ] Include the required field `public claim level`.
- [ ] Include the required field `proof path`.
- [ ] Include the required field `rule ID/family`.
- [ ] Include the required field `evidence tier`.
- [ ] Include the required field `coverage label`.
- [ ] Include the required field `commit SHA`.
- [ ] Include the required field `extractor version`.
- [ ] Include the required field `limitation`.
- [ ] Include the required field `non-claim`.
- [ ] Include the required field `validation evidence`.
- [ ] Include the required field `rejected interpretation`.
- [ ] Include the required field `follow-up owner`.
- [ ] Include the required field `review date placeholder`.
- [ ] Include the required field `residual risk`.
- [ ] Use only TraceMap evidence tier values: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [ ] Use `concept` for page-level and example record public claim level unless
  exact public-safe evidence supports a stronger claim and the decision is
  recorded in `implementation-state.md`.
- [ ] Use public role categories or placeholders for decision owner and
  follow-up owner values, not private people or team names.
- [ ] Include stable anchors for the template, safe example, unsafe examples,
  stop conditions, follow-up owners, and non-claims.
- [ ] Verify supporting routes resolve in generated output before linking.
- [ ] Record substitutions, deferrals, or unavailable candidate routes in
  `implementation-state.md`.
- [ ] Link only to public-safe pages, public-safe generated summaries,
  documentation, validation pages, limitation pages, demo pages, proof-path
  pages, or private review locations named without raw material.
- [ ] Do not link directly to raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output, hidden
  validation details, or credential-like values.
- [ ] Keep artifact names as local-output categories or non-shareable examples
  only, not public proof content.
- [ ] Add explicit non-claims for autonomous decisions, approval workflows,
  release approval, release safety, operational safety, runtime proof,
  production proof, endpoint performance proof, outage cause,
  absence-of-impact proof, complete coverage, AI/LLM analysis, embeddings,
  vector databases, prompt classification, and replacement of human judgment
  or governance.
- [ ] State that TraceMap does not replace tests, code review, source review,
  runtime observability, release process, service-owner review, governance, or
  human judgment.
- [ ] Keep reduced, partial, unknown, private-only, or unavailable coverage
  visible and route remaining questions to owners.
- [ ] Avoid blame language around teams, vendors, consultants, service owners,
  reviewers, prior implementers, or code quality.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, route index metadata, and
  discovery metadata with `publicClaimLevel: concept`.
- [ ] If implemented as a section, add a stable in-page anchor and verify host
  page metadata remains concept-level or more conservative.
- [ ] Add focused validation for required record fields and required sections.
- [ ] Add focused validation for supporting link resolution and recorded route
  substitutions or deferrals.
- [ ] Add focused validation that the placement decision record collectively
  references all four spec-defined candidate placements: the selected
  placement with rationale plus the three unchosen rejected alternatives.
- [ ] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when standalone.
- [ ] Use the site's existing closed vocabulary for discovery source type and
  hint category.
- [ ] Add focused validation for section anchor and host-page metadata when
  sectioned.
- [ ] Add focused validation for visible word count bounds of 700 to 2500
  words unless amended, counting rendered body prose and record field values
  while excluding page-level navigation, breadcrumbs, site headers, site
  footers, metadata blocks, and repeated field-label headers.
- [ ] If word count bounds are amended, record a rationale in
  `implementation-state.md`, keep the floor at or above 400 rendered words,
  and keep the ceiling at or below 2500 rendered words.
- [ ] Add focused validation for forbidden approval, decision, release-safety,
  operational-safety, runtime-proof, production-proof,
  endpoint-performance, outage-cause, absence-of-impact, complete-coverage,
  AI/LLM, embedding, vector database, prompt-classification,
  autonomous-decision, and replacement-of-human-judgment wording in rendered
  text, decoded HTML, raw HTML attributes, alt text, captions, link anchor
  text, link title attributes, metadata, sitemap output, discovery output,
  tests, fixtures, and generated pages, while allowing required non-claim,
  limitation, stop-condition, rejected-interpretation, residual-risk, and
  unsafe-example contexts.
- [ ] Identify allowed forbidden-term contexts through stable structural
  markers such as section anchors, field-level containers, or validator-owned
  data attributes, and add negative tests proving unsupported positive claims
  cannot pass by hiding inside mislabeled wrappers.
- [ ] Use the reference marker convention
  `data-tracemap-validation-context="<context>"` for allowed forbidden-term
  contexts, or record an equivalent substitute and rationale in
  `implementation-state.md`.
- [ ] Add a structural-boundary negative fixture that places a forbidden
  positive claim inside `data-tracemap-validation-context="unsafe-example"`
  but outside any allowed unsafe-example section or field container, then
  asserts validation fails.
- [ ] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- [ ] Add focused validation that example review date placeholders use a
  placeholder pattern such as `YYYY-MM-DD`, not real private review dates or
  internal cadence.
- [ ] Add focused validation that example commit SHA and extractor version
  values use synthetic placeholders, redacted placeholders, or explicit
  public-demo provenance, not real private scan identifiers.
- [ ] Add focused validation that example validation evidence values are
  synthetic, public-safe, or explicit public-demo summaries and exclude raw
  command output, private CI/job identifiers, hidden validation details, and
  credential-like values.
- [ ] Add an accessibility check for the responsive record template, including
  semantic table headers or definition-list labels and screen-reader-accessible
  field names.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] If `npm run validate` requires explicit route or section registration,
  add the registration, document the pattern, and record it in
  `implementation-state.md`.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [ ] For section-only implementations, include browser scroll-to-section
  sanity checks on desktop and mobile.
- [ ] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
