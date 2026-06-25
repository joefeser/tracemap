# Site TraceMap Tools Public Demo Troubleshooting Tasks

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
  higher review findings are patched or explicitly dispositioned,
  `git diff --check` passes, `./scripts/check-private-paths.sh` passes or its
  documented substitute is recorded, and all five spec headers are updated.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] If `./scripts/check-private-paths.sh` is absent on this branch, record
  the absence in `implementation-state.md` and substitute a manual grep for
  absolute paths, raw credential patterns, and private sample names; record the
  substitution and results before marking the task complete.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, placement decision, review results, validation plan, oddities, and
  follow-up items before changing site code.
- [ ] Choose the final placement: `/demo/troubleshooting/`, `/demo/help/`,
  section on `/demo/runbook/`, or section on `/demo/start-here/`.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Explain why the selected placement is public-demo troubleshooting rather
  than the demo runbook, start-here page, result page, proof-upgrades page,
  validation page, limitations page, or objections page.
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
- [ ] If implemented as a section on a host page whose visible claim level is
  not `concept`, scope the troubleshooting label to the section, record the
  host claim level and reconciliation wording in `implementation-state.md`,
  and validate that host and section claim-level statements do not contradict.
- [ ] If implemented as a section, validate and record that the
  troubleshooting content does not visually or structurally dominate the host
  page's primary purpose using a measurable basis such as rendered height or
  matrix-inclusive visible word count relative to host primary content.
- [ ] Include sections for when to use the page, what it is not, the
  troubleshooting matrix, safe wording, rejected wording, adjacent routes,
  owner handoff, stop conditions, and non-claims.
- [ ] Include the required row for missing route.
- [ ] Include the required row for outdated demo summary.
- [ ] Include the required row for broken proof expectation.
- [ ] Include the required row for reduced coverage label.
- [ ] Include the required row for private-only evidence.
- [ ] Include the required row for unsupported claim wording.
- [ ] Include the required row for validation mismatch.
- [ ] Include the required row for where to ask next.
- [ ] Ensure every required row includes symptom, likely public-safe cause,
  what to check, what not to conclude, next owner/route, stop condition, and
  non-claim.
- [ ] State that a missing public route cannot support a conclusion that the
  evidence exists, does not exist, or proves the claim.
- [ ] State that stale summary wording cannot support current-head,
  current-release, or current-proof claims.
- [ ] State that a broken or incomplete proof path cannot support public proof
  wording.
- [ ] State that reduced coverage cannot support complete coverage,
  absence-of-impact, clean-repo, or release-safety wording.
- [ ] State that private-only evidence cannot be used as public proof until
  summarized through a public-safe route.
- [ ] State that unsupported wording must be downgraded, removed, or routed to
  limitations and validation rather than repeated.
- [ ] State that mismatched public validation expectations cannot support a
  passed-validation claim.
- [ ] State that asking the next owner transfers the evidence question and does
  not prove, approve, or diagnose anything by itself.
- [ ] Add safe wording examples for missing route, stale summary, incomplete
  proof expectation, reduced coverage, private-only evidence, unsupported
  wording, validation mismatch, and owner handoff.
- [ ] Add unsafe wording examples only inside explicitly bounded
  rejected-pattern regions.
- [ ] Ensure rejected-pattern regions use a programmatically identifiable
  marker such as a dedicated component, wrapper element, or data attribute.
- [ ] If the site does not already define a standard rejected-pattern marker,
  introduce one and record the component name, element type, or data attribute
  in `implementation-state.md` before writing site source.
- [ ] Ensure required non-claim, limitation, and matrix `what not to conclude`
  and `non-claim` regions use a programmatically identifiable non-claim marker
  distinct from the rejected-pattern marker.
- [ ] If the site does not already define a standard non-claim marker,
  introduce one and record the component name, element type, or data attribute
  in `implementation-state.md` before writing site source.
- [ ] Forbid live support SLA, runtime diagnosis, production proof, release
  safety or approval, endpoint performance proof, complete coverage,
  absence-of-impact proof, clean-repo claim under reduced analysis, AI/LLM
  analysis, prompt-based classification, embedding search, vector database
  analysis, autonomous approval, and replacement of validation or human review.
- [ ] Avoid unqualified `impacted`, `safe`, `approved`, `certified`,
  `guaranteed`, `resolved`, or `clean` wording outside rejected-pattern,
  limitation, non-claim, or public-safe proof contexts.
- [ ] Add an owner-handoff section using role labels rather than real people,
  private team names, customers, service names, repository identities, or
  private sample names.
- [ ] Ensure handoff examples include symptom, public-safe cause, requested
  next check, route or owner, stop condition, and non-claim.
- [ ] Ensure handoff copy says it transfers the evidence question and does not
  approve a release, prove runtime behavior, assign blame, or replace human
  review.
- [ ] Ensure owner-handoff and `where to ask next` copy does not state or
  imply response times, support channels, ticketing, guaranteed answers, or
  service-level commitments.
- [ ] Link to `/demo/runbook/` when it exists and explain that it remains the
  demo reading sequence.
- [ ] Link to `/demo/start-here/` when it exists and explain that it remains
  first-visitor orientation.
- [ ] Link to `/demo/result/` when it exists and explain that it remains the
  public-safe result surface.
- [ ] Link to `/demo/proof-upgrades/` when it exists and explain that it
  remains future proof-improvement planning.
- [ ] Link to `/validation/` when it exists and explain that it remains the
  validation surface.
- [ ] Link to `/limitations/` when it exists and explain that it remains the
  canonical boundary and non-claim surface.
- [ ] Link to `/questions/objections/` when it exists and explain that it
  remains the stakeholder objection surface.
- [ ] Record absent, moved, or unavailable adjacent routes in
  `implementation-state.md` with substitution, omission, or deferral notes.
- [ ] Do not publish raw facts, raw SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  or credential-like values.
- [ ] Name local-only artifact families only inside boundary copy that
  explains they need public-safe summaries before public linking.
- [ ] Use only synthetic, authored, or already public-safe examples.
- [ ] Label illustrative examples as illustrative unless they link to an
  existing public-safe demo or documentation surface.
- [ ] Add focused validation for visible concept claim label and shared
  principle.
- [ ] Add focused validation for required rows and required row fields.
- [ ] Add focused validation that required matrix row fields are present in
  static HTML with table header association or an equivalent programmatic
  field-label association marker.
- [ ] Add focused validation for required links and adjacent-route
  distinctions.
- [ ] Add focused validation that all cross-links to adjacent surfaces use
  bounded anchor text.
- [ ] Add focused validation that no public row directs visitors to internal
  spec artifacts such as `implementation-state.md`, `tasks.md`,
  `.kiro/specs/`, or other non-public author material. Relative path
  validation for directory segments such as `.kiro` and `specs` must split
  candidate paths into segments and check individual segment matches rather
  than using string containment or slash-wrapped substring matching.
- [ ] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata if a standalone route is chosen.
- [ ] Add focused validation for section host metadata, duplicate IDs, and
  anchor resolution if a section placement is chosen.
- [ ] Add focused validation for section-level claim-label scoping and host
  claim-level reconciliation when the host route's visible claim level is not
  `concept`.
- [ ] Add focused validation for measurable section-crowding checks, including
  rendered-height or word-count relationship to the host page's primary
  content. Validation must record whether the basis is rendered height or
  matrix-inclusive word count and must not reuse the matrix-excluding
  word-count base.
- [ ] Add focused validation for forbidden support SLA, runtime diagnosis,
  production proof, release safety or approval, endpoint performance, complete
  coverage, absence-of-impact, clean-repo under reduced analysis, AI/LLM,
  prompt-based classification, embedding search, vector database analysis,
  autonomous approval, response-time or ticketing commitments, and
  replacement-of-validation or human-review claims.
- [ ] Add focused validation that every rejected-pattern region carries the
  programmatic marker and that forbidden-claim scanning keys off that marker
  rather than styling.
- [ ] Add focused validation that required non-claim, limitation, and matrix
  `what not to conclude` and `non-claim` regions carry the programmatic
  non-claim marker.
- [ ] Add focused validation that forbidden-claim scanning targets affirmative
  claims and excludes text inside marked rejected-pattern regions and marked
  non-claim regions because rejected examples and required negated non-claims
  are allowed only there.
- [ ] Add focused validation that forbidden private/raw/local-material scanning
  applies everywhere with no exception, including rejected-pattern regions,
  non-claim regions, fixtures, tests, rendered text, decoded HTML, raw HTML
  attributes, metadata, sitemap output, discovery output, and bot-oriented
  discovery surfaces.
- [ ] Add focused validation that copy, row labels, and handoff examples avoid
  blame language.
- [ ] Add focused validation for forbidden private/raw material across
  rendered text, decoded HTML, raw HTML attributes, metadata, sitemap output,
  discovery output, fixtures, tests, and bot-oriented discovery surfaces.
- [ ] Add focused validation that unsafe wording appears only in rejected,
  limitation, non-claim, or validation-warning context.
- [ ] Add focused validation that illustrative examples are synthetic or
  already public-safe.
- [ ] Add focused validation for word count bounds: 700 to 1,500 visible body
  words for a standalone route, or 350 to 900 visible body words for a section
  placement, excluding navigation, metadata, code blocks, and required matrix
  text. Required matrix text means the content of the required troubleshooting
  matrix rows and their column headers, but not introductory prose, section
  headings, adjacent-route descriptions, safe-wording examples, or
  rejected-wording examples outside the matrix.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are implemented.
