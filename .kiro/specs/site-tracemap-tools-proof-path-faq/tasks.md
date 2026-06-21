# Site TraceMap Tools Proof Path FAQ Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are for a future implementation phase unless explicitly labeled
as spec-review work. This spec branch must not edit site source, generated
output, scanner code, reducer code, or existing specs.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or the best available
  Opus-class model at review time, or record the exact unavailable-tool/model
  error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] If both `claude-opus-4.8` and `claude-sonnet-4.6` are unavailable,
  record the exact errors, run the review with the best available model, and
  record the substitution and rationale in `implementation-state.md`. Do not
  skip the review entirely; the readiness gate requires at least one completed
  review.
- [x] Patch Medium or higher spec findings and rerun re-review where
  feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with
  branch, route choice, scope decisions, review results, validation plan, and
  initial implementation status before changing site code.
- [ ] Choose the final route or placement:
  `/proof-paths/faq/`, section on `/proof-paths/`, section on
  `/proof-paths/tour/`, section on `/questions/`, or a recorded equivalent.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Explain why the selected placement is an FAQ surface rather than the
  question index, proof-path overview, guided tour, evidence glossary,
  limitations page, static-versus-runtime explainer, or claim checklist.
- [ ] Add the concept-level FAQ page or section using existing static-site
  layout, typography, accessibility, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept` copy.
- [ ] Include visible `No public conclusion without evidence` copy.
- [ ] Keep the page or section out of primary navigation unless
  implementation-state records a matching site information-architecture
  decision.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [ ] If implemented as a section, record the host route's claim-level
  reconciliation and add stable anchors for the FAQ.
- [ ] If implemented as a section, validate that the host route's title,
  description, canonical, Open Graph, sitemap, and discovery metadata are not
  upgraded above the host route's recorded claim level by the FAQ section, and
  that the FAQ section's stable anchors resolve in generated output and are
  unique within the host document.
- [ ] If implemented as a section, add a focused duplicate-ID and
  anchor-resolution check on the generated host-page HTML and record the
  check command in `implementation-state.md`.
- [ ] Include an answer for `What is a proof path?`.
- [ ] Include an answer for `How do I read a proof path?`.
- [ ] Include an answer for `What do evidence tiers mean?`.
- [ ] Include an answer for `What do coverage labels mean?`.
- [ ] Include an answer for `Why do limitations matter?`.
- [ ] Include an answer for `What should I do when evidence is missing?`.
- [ ] Include an answer for `How do proof paths relate to review packets?`.
- [ ] Include an answer for `What can static evidence not prove?`.
- [ ] Include an answer for `Can a proof path use private or raw artifacts?`.
- [ ] Include an answer for `What should agents and reviewers preserve?`.
- [ ] Define proof paths as public-safe routes or reference trails from a
  public claim to static evidence fields and limitations, not raw local
  evidence.
- [ ] Require the proof-path reading order to preserve claim, public claim
  level, proof path, rule ID or rule family, evidence tier, coverage label,
  commit or public-safe source context, extractor version or schema family,
  limitation, non-claim, and next owner.
- [ ] Explain `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` without implying complete coverage or failure.
- [ ] Explain full, partial, reduced, unknown, unavailable, future-only, and
  gap-labeled coverage states as boundaries.
- [ ] Present limitations as part of the claim, not a footer disclaimer.
- [ ] Explain missing evidence as a stop, downgrade, internal-only, gap-label,
  or owner-follow-up condition.
- [ ] Explain that review packets gather proof paths, limitations, review
  notes, and owner follow-ups but do not approve release, safety, runtime
  behavior, or missing evidence.
- [ ] Include explicit non-claims for runtime proof, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI/LLM analysis, release approval, autonomous approval,
  and replacement of human review, tests, source review, runtime
  observability, or service-owner judgment.
- [ ] Include safe answer patterns with bounded verbs such as `inspect`,
  `follow`, `compare`, `check`, `record`, `downgrade`, `hold`,
  `label the gap`, `hand off`, and `escalate`.
- [ ] Include unsafe answer patterns that reject unsupported verbs such as
  `proves`, `guarantees`, `certifies`, `approves`, `replaces`, `resolves`,
  and unqualified `impacted`.
- [ ] Ensure unsafe examples are framed as rejected patterns, not live claims.
- [ ] Avoid blame language in page copy, metadata, examples, validation
  messages, and review-packet references.
- [ ] Use only synthetic, authored, or already public-safe examples.
- [ ] Label illustrative examples as illustrative unless they link to an
  existing public-safe demo or documentation surface.
- [ ] Use placeholder rule IDs, commit-like values, extractor versions, claim
  labels, and review-packet labels only when visibly illustrative.
- [ ] Link examples only to public-safe pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, demo artifacts, validation
  pages, limitations pages, proof paths, or review-packet surfaces.
- [ ] Do not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer logs,
  raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, private sample names,
  command output, hidden validation details, or credential-like values.
- [ ] Name local-only artifact families only inside boundary copy that
  explains they are not public raw material.
- [ ] Do not include local commands that encourage readers to publish ignored
  output directories or raw generated scan artifacts.
- [ ] Do not reveal internal reviewer identities, private sample identities,
  hidden capability names, in-flight sequencing, confidential customer
  context, or private repository details.
- [ ] Link to `/questions/` when it exists and explain that it remains the
  broader question-to-surface orientation index.
- [ ] Link to `/proof-paths/` when it exists and explain that it remains the
  canonical proof-path overview.
- [ ] Link to `/proof-paths/tour/` when it exists and explain that it remains
  the guided step-by-step reading flow.
- [ ] Link to `/evidence/` when it exists and explain that it remains the
  broader evidence vocabulary and artifact-shape surface.
- [ ] Link to `/limitations/` when it exists and explain that it remains the
  canonical boundary and non-claim surface.
- [ ] Link to `/static-vs-runtime/` when it exists and explain that it remains
  the static evidence versus runtime telemetry boundary.
- [ ] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the decision ritual for repeating, downgrading, holding, or keeping
  claims internal.
- [ ] For any adjacent route that is absent, renamed, or replaced, record the
  route status and substitution, deferral, or omission in
  `implementation-state.md`.
- [ ] Verify every public FAQ link resolves in generated site output before
  publishing it.
- [ ] Validate illustrative example safety: placeholder rule IDs, commit-like
  values, extractor versions, claim labels, and review-packet labels are
  visibly marked illustrative and are not presented as real TraceMap findings.
- [ ] Validate that example outbound links resolve only to allowed public-safe
  target families: checked-in public pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, sanctioned demo artifacts,
  validation pages, limitations pages, proof paths, or review-packet
  surfaces, or are recorded as deferred, substituted, or omitted in
  `implementation-state.md`.
- [ ] If accordions or progressive disclosure are used, ensure every answer's
  full text is in the static HTML and add focused validation that asserts it.
- [ ] Record whether a bounded inbound link from `/proof-paths/`, the chosen
  host route, or another existing proof-path-family surface to the FAQ is
  added, coordinating with the proof-path-index owner without adding the FAQ
  to primary navigation.
- [ ] Add focused validation for visible concept claim label and shared
  principle.
- [ ] Add focused validation for required FAQ questions and stable anchors.
- [ ] Add focused validation for safe and unsafe answer pattern regions.
- [ ] Add focused validation for adjacent-route link resolution and recorded
  substitutions.
- [ ] Add focused validation for standalone-route metadata and discovery
  `publicClaimLevel: concept` if a standalone route is chosen.
- [ ] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  complete-coverage, AI/LLM, release-approval, autonomous-approval, and
  replacement-of-review claims across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests, sitemap/discovery
  output, and bot-oriented discovery surfaces, while allowing those terms only
  inside explicitly bounded non-claim, limitation, or unsafe-pattern contexts.
- [ ] Add focused validation for unsupported conclusion verbs (`proves`,
  `guarantees`, `certifies`, `approves`, `replaces`, `resolves`, and
  unqualified `impacted`) across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests,
  sitemap/discovery output, and bot-oriented discovery surfaces, while
  allowing them only inside explicitly bounded unsafe-pattern, non-claim, or
  limitation regions. Allow `impacted` only when tied to a public-safe
  reducer-backed result.
- [ ] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML, raw HTML attributes, metadata,
  sitemap/discovery output, fixtures, tests, and bot-oriented discovery
  surfaces.
- [ ] Treat raw `facts.ndjson`, raw `index.sqlite`, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, and credential-like values as explicit forbidden
  private/raw validation targets.
- [ ] Add focused validation for no blame language in visible copy, metadata,
  examples, and validation errors, checking at least a recorded advisory set
  of representative blame indicators such as `failed`, `fault`, `to blame`,
  `negligent`, `careless`, or attributing missing, reduced, or conflicting
  evidence to a named person, team, service, customer, or reviewer.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] If `npm test` or `npm run validate` do not cover the new page, record
  the gap in `implementation-state.md` and add focused validation before
  closing the implementation phase.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
