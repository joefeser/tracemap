# Site TraceMap Tools Reduced Coverage Playbook Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks track a spec-only packet now and a future implementation phase.
This branch must not edit site source, generated output, scanner code, reducer
code, or existing specs.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` using
  `scripts/kiro-review.mjs`, or record the exact unavailable-tool/model error
  in `implementation-state.md`.
- [x] Verify the `claude-opus-4.8` model identifier against the harness or
  available-model output before running. If the identifier has changed, update
  this task and `implementation-state.md` with the correct identifier before
  recording a substitution.
- [x] Run Kiro spec review with `claude-sonnet-4.6` using
  `scripts/kiro-review.mjs`, or record the exact unavailable-tool/model error
  in `implementation-state.md`.
- [x] If either or both requested models are unavailable, record exact errors,
  run the review with the best available model if the harness offers one,
  record the substitution and rationale in `implementation-state.md`, and note
  which models were used and which were unavailable.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher review findings are patched or explicitly dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] If `./scripts/check-private-paths.sh` is absent on this branch, record
  the absence in `implementation-state.md` and substitute a manual grep for
  absolute paths, raw credential patterns, and private sample names; record
  the substitution and results before marking the task complete.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, placement decision, review results, validation plan, oddities, and
  follow-up items before changing site code.
- [ ] Choose the final placement: `/coverage/reduced/`,
  `/limitations/reduced-coverage/`, section on `/limitations/`, section on
  `/validation/`, or a recorded equivalent that preserves the same boundaries.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Explain why the selected placement is a reduced-coverage playbook rather
  than the limitations page, validation page, static-versus-runtime explainer,
  objections guide, proof-path FAQ, or review-claim checklist.
- [ ] Add the concept-level page or section using existing static-site layout,
  typography, accessibility, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept` copy.
- [ ] Include visible `No public conclusion without evidence` copy.
- [ ] Keep the page or section out of primary navigation unless
  `implementation-state.md` records a matching site information-architecture
  decision.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [ ] If implemented as a section, validate that the host route metadata,
  claim-level wording, sitemap entry, discovery entry, and stable anchors
  preserve the host boundary and do not upgrade the section.
- [ ] Include required sections for what reduced coverage means, how to label
  it, safe conclusions, unsafe conclusions, next evidence to collect, owner
  handoff, stop conditions, and non-claims.
- [ ] Include the required row for build/load failure.
- [ ] Include the required row for syntax fallback.
- [ ] Include the required row for missing semantic evidence.
- [ ] Include the required row for unsupported framework surface.
- [ ] Include the required row for missing generated artifact.
- [ ] Include the required row for private-only support.
- [ ] Include the required row for stale commit context.
- [ ] Include the required row for unknown evidence tier.
- [ ] Ensure every required row includes coverage label, evidence tier,
  evidence available, what cannot be concluded, next owner, safe wording, stop
  condition, and proof/validation link.
- [ ] State that reduced coverage can remain useful review input when labels,
  evidence tiers, limitations, commit context, extractor versions, and
  public-safe proof links stay attached.
- [ ] State that a failed or reduced build/load state cannot support a
  clean-repo claim.
- [ ] State that syntax fallback cannot support compiler-resolved symbol
  conclusions.
- [ ] State that missing semantic evidence cannot support Tier1 conclusions.
- [ ] State that unsupported framework surfaces cannot support complete
  framework coverage.
- [ ] State that missing generated artifacts cannot support public proof-link
  claims.
- [ ] State that private-only support cannot be cited as public proof until
  summarized through a public-safe route.
- [ ] State that stale commit context cannot support current-head or
  current-release wording.
- [ ] State that an unknown evidence tier cannot be upgraded by confidence,
  repetition, or reviewer seniority.
- [ ] Add safe wording examples for reduced coverage, syntax fallback,
  missing semantic evidence, missing public-safe artifact output, private-only
  evidence, stale source context, and unknown tier.
- [ ] Add unsafe wording examples only inside explicitly bounded
  rejected-pattern regions.
- [ ] Forbid absence-of-impact proof, clean-repo claim under failed/reduced
  analysis, runtime proof, release approval or safety, operational safety,
  complete coverage, AI/LLM analysis, prompt-based classification, embedding
  search, vector database analysis, and replacement of human review.
- [ ] Avoid unqualified `impacted`, `safe`, `approved`, `certified`,
  `guaranteed`, `resolved`, or `clean` wording outside rejected-pattern or
  public-safe proof contexts.
- [ ] Add a next-evidence section mapping required rows to public-safe
  evidence targets.
- [ ] Add an owner-handoff section using role labels rather than real people,
  private team names, customers, service names, repository identities, or
  private sample names.
- [ ] Ensure handoff examples include current label, evidence available,
  missing evidence, requested next action, public-safe proof target, stop
  condition, and non-claim.
- [ ] Ensure handoff copy says it transfers the evidence question and does not
  approve a release, prove runtime behavior, assign blame, or replace human
  review.
- [ ] Link to `/limitations/` when it exists and explain that it remains the
  canonical boundary and non-claim surface.
- [ ] Link to `/validation/` when it exists and explain that it remains the
  check and validation surface.
- [ ] Link to `/static-vs-runtime/` when it exists and explain that it remains
  the static evidence versus runtime telemetry boundary.
- [ ] Link to `/questions/objections/` when it exists and explain that it
  remains the stakeholder objection surface.
- [ ] Link to `/proof-paths/faq/` when it exists and explain that it remains
  the proof-path explanation surface.
- [ ] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the claim repetition ritual.
- [ ] Record absent, moved, or unavailable adjacent routes in
  `implementation-state.md` with substitution, omission, or deferral notes.
- [ ] Do not publish raw facts, raw SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation
  details, or credential-like values.
- [ ] Name local-only artifact families only inside boundary copy that
  explains they need public-safe summaries before public linking.
- [ ] Use only synthetic, authored, or already public-safe examples.
- [ ] Label illustrative examples as illustrative unless they link to an
  existing public-safe demo or documentation surface.
- [ ] Add focused validation for visible concept claim label and shared
  principle.
- [ ] Add focused validation for required sections.
- [ ] Add focused validation for required rows and required row fields.
- [ ] Add focused validation for required links and adjacent route
  distinctions.
- [ ] Add focused validation that all cross-links to adjacent surfaces use
  anchor text that names the destination boundary or topic rather than generic
  phrases or claim-asserting phrases.
- [ ] Add focused validation for required adjacent surface distinctions,
  verifying that the page includes a distinguishing statement for each of
  `/limitations/`, `/validation/`, `/static-vs-runtime/`,
  `/questions/objections/`, `/proof-paths/faq/`, and
  `/review-claim-checklist/`, or records each absent or moved route in
  `implementation-state.md`.
- [ ] Add focused validation that every required row's proof/validation link
  field is non-empty, non-placeholder, and resolves to an allowed public-safe
  target or is explicitly recorded as deferred, substituted, or omitted in
  `implementation-state.md`.
- [ ] Add focused validation that no more than two required rows have
  deferred, substituted, or omitted proof/validation links at one time, and
  that each deferral records a specific public-safe target type and follow-up
  task referencing this spec.
- [ ] Add focused validation that every required row has an evidence tier
  using only `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or
  `Tier4Unknown`; rows with unavailable or private-only source evidence use
  `Tier4Unknown` with a visible unavailable or private-only label rather than
  a custom tier value.
- [ ] Add focused validation for exact supplementary state markers:
  `unavailable`, `private-only`, and `stale`, while allowing a row to list
  more than one valid evidence-tier token when the row explains multiple
  possible evidence states.
- [ ] Add focused validation for standalone metadata and discovery metadata if
  a standalone route is chosen.
- [ ] Add focused validation for section host metadata, duplicate IDs, and
  anchor resolution if a section placement is chosen.
- [ ] Add focused validation for forbidden absence-of-impact, clean-repo,
  runtime, production-traffic, endpoint-performance, outage-cause, release,
  autonomous approval, operational, complete-coverage, AI/LLM, prompt-based
  classification, embedding search, vector database analysis, and
  replacement-of-review claims.
- [ ] Add focused validation that copy, row labels, and handoff examples avoid
  blame language and preserve neutral phrasing while keeping the required
  build/load failure row label.
- [ ] Confirm that the `build/load failure` row label is present verbatim and
  that surrounding copy does not attribute the state to a person, team,
  service, customer, or reviewer.
- [ ] Add focused validation that forbidden-claim and unqualified-term
  detection excludes explicitly bounded rejected-pattern, non-claim,
  limitation, and validation-warning regions, and matches claim phrases rather
  than bare substrings such as `safe`, `clean`, or `resolved`.
- [ ] Add focused validation that rejected-pattern regions use a
  programmatically identifiable marker such as a dedicated component, wrapper
  element, or data attribute.
- [ ] Add focused validation for forbidden private/raw material across
  rendered text, decoded HTML, raw HTML attributes, metadata, sitemap output,
  discovery output, fixtures, tests, and bot-oriented discovery surfaces.
- [ ] Add focused validation that required matrix row fields are present in
  static HTML with table header association or an equivalent programmatic
  field-label association marker; manual desktop/mobile browser sanity does
  not replace this check.
- [ ] Add focused validation that unsafe wording appears only in rejected,
  limitation, non-claim, or validation-warning context.
- [ ] Add focused validation that illustrative examples are synthetic or
  already public-safe.
- [ ] Add focused validation for word count bounds: 1,000 to 1,900 visible
  body words for a standalone route, or 500 to 1,100 visible body words for a
  section placement, excluding navigation, metadata, code blocks, and required
  reduced-coverage matrix element text, including column headers, captions,
  footnotes, and in-cell links, while counting body prose outside the matrix;
  require standalone placement if the full matrix cannot fit a section without
  dropping required rows or fields.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, partial-work
  labels if applicable, and follow-up items.
