# Site TraceMap Tools Proof Path FAQ Tasks

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

These tasks track the completed spec review and public-site implementation phase. The implementation edits site source and validation only; generated output, scanner code, reducer code, and unrelated specs remain out of scope.

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

- [x] Confirm or update this spec-local `implementation-state.md` with
  branch, route choice, scope decisions, review results, validation plan, and
  initial implementation status before changing site code.
- [x] Choose the final route or placement:
  `/proof-paths/faq/`, section on `/proof-paths/`, section on
  `/proof-paths/tour/`, section on `/questions/`, or a recorded equivalent.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] Explain why the selected placement is an FAQ surface rather than the
  question index, proof-path overview, guided tour, evidence glossary,
  limitations page, static-versus-runtime explainer, or claim checklist.
- [x] Add the concept-level FAQ page or section using existing static-site
  layout, typography, accessibility, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept` copy.
- [x] Include visible `No public conclusion without evidence` copy.
- [x] Keep the page or section out of primary navigation unless
  implementation-state records a matching site information-architecture
  decision.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [x] If implemented as a section, record the host route's claim-level
  reconciliation and add stable anchors for the FAQ.
- [x] If implemented as a section, validate that the host route's title,
  description, canonical, Open Graph, sitemap, and discovery metadata are not
  upgraded above the host route's recorded claim level by the FAQ section, and
  that the FAQ section's stable anchors resolve in generated output and are
  unique within the host document.
- [x] If implemented as a section, add a focused duplicate-ID and
  anchor-resolution check on the generated host-page HTML and record the
  check command in `implementation-state.md`.
- [x] Include an answer for `What is a proof path?`.
- [x] Include an answer for `How do I read a proof path?`.
- [x] Include an answer for `What do evidence tiers mean?`.
- [x] Include an answer for `What do coverage labels mean?`.
- [x] Include an answer for `Why do limitations matter?`.
- [x] Include an answer for `What should I do when evidence is missing?`.
- [x] Include an answer for `How do proof paths relate to review packets?`.
- [x] Include an answer for `What can static evidence not prove?`.
- [x] Include an answer for `Can a proof path use private or raw artifacts?`.
- [x] Include an answer for `What should agents and reviewers preserve?`.
- [x] Define proof paths as public-safe routes or reference trails from a
  public claim to static evidence fields and limitations, not raw local
  evidence.
- [x] Require the proof-path reading order to preserve claim, public claim
  level, proof path, rule ID or rule family, evidence tier, coverage label,
  commit or public-safe source context, extractor version or schema family,
  limitation, non-claim, and next owner.
- [x] Explain `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` without implying complete coverage or failure.
- [x] Explain full, partial, reduced, unknown, unavailable, future-only, and
  gap-labeled coverage states as boundaries.
- [x] Present limitations as part of the claim, not a footer disclaimer.
- [x] Explain missing evidence as a stop, downgrade, internal-only, gap-label,
  or owner-follow-up condition.
- [x] Explain that review packets gather proof paths, limitations, review
  notes, and owner follow-ups but do not approve release, safety, runtime
  behavior, or missing evidence.
- [x] Include explicit non-claims for runtime proof, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI/LLM analysis, release approval, autonomous approval,
  and replacement of human review, tests, source review, runtime
  observability, or service-owner judgment.
- [x] Include safe answer patterns with bounded verbs such as `inspect`,
  `follow`, `compare`, `check`, `record`, `downgrade`, `hold`,
  `label the gap`, `hand off`, and `escalate`.
- [x] Include unsafe answer patterns that reject unsupported verbs such as
  `proves`, `guarantees`, `certifies`, `approves`, `replaces`, `resolves`,
  and unqualified `impacted`.
- [x] Ensure unsafe examples are framed as rejected patterns, not live claims.
- [x] Avoid blame language in page copy, metadata, examples, validation
  messages, and review-packet references.
- [x] Use only synthetic, authored, or already public-safe examples.
- [x] Label illustrative examples as illustrative unless they link to an
  existing public-safe demo or documentation surface.
- [x] Use placeholder rule IDs, commit-like values, extractor versions, claim
  labels, and review-packet labels only when visibly illustrative.
- [x] Link examples only to public-safe pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, demo artifacts, validation
  pages, limitations pages, proof paths, or review-packet surfaces.
- [x] Do not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer logs,
  raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, private sample names,
  command output, hidden validation details, or credential-like values.
- [x] Name local-only artifact families only inside boundary copy that
  explains they are not public raw material.
- [x] Do not include local commands that encourage readers to publish ignored
  output directories or raw generated scan artifacts.
- [x] Do not reveal internal reviewer identities, private sample identities,
  hidden capability names, in-flight sequencing, confidential customer
  context, or private repository details.
- [x] Link to `/questions/` when it exists and explain that it remains the
  broader question-to-surface orientation index.
- [x] Link to `/proof-paths/` when it exists and explain that it remains the
  canonical proof-path overview.
- [x] Link to `/proof-paths/tour/` when it exists and explain that it remains
  the guided step-by-step reading flow.
- [x] Link to `/evidence/` when it exists and explain that it remains the
  broader evidence vocabulary and artifact-shape surface.
- [x] Link to `/limitations/` when it exists and explain that it remains the
  canonical boundary and non-claim surface.
- [x] Link to `/static-vs-runtime/` when it exists and explain that it remains
  the static evidence versus runtime telemetry boundary.
- [x] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the decision ritual for repeating, downgrading, holding, or keeping
  claims internal.
- [x] For any adjacent route that is absent, renamed, or replaced, record the
  route status and substitution, deferral, or omission in
  `implementation-state.md`.
- [x] Verify every public FAQ link resolves in generated site output before
  publishing it.
- [x] Validate illustrative example safety: placeholder rule IDs, commit-like
  values, extractor versions, claim labels, and review-packet labels are
  visibly marked illustrative and are not presented as real TraceMap findings.
- [x] Validate that example outbound links resolve only to allowed public-safe
  target families: checked-in public pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, sanctioned demo artifacts,
  validation pages, limitations pages, proof paths, or review-packet
  surfaces, or are recorded as deferred, substituted, or omitted in
  `implementation-state.md`.
- [x] If accordions or progressive disclosure are used, ensure every answer's
  full text is in the static HTML and add focused validation that asserts it.
- [x] Record whether a bounded inbound link from `/proof-paths/`, the chosen
  host route, or another existing proof-path-family surface to the FAQ is
  added, coordinating with the proof-path-index owner without adding the FAQ
  to primary navigation.
- [x] Add focused validation for visible concept claim label and shared
  principle.
- [x] Add focused validation for required FAQ questions and stable anchors.
- [x] Add focused validation for safe and unsafe answer pattern regions.
- [x] Add focused validation for adjacent-route link resolution and recorded
  substitutions.
- [x] Add focused validation for standalone-route metadata and discovery
  `publicClaimLevel: concept` if a standalone route is chosen.
- [x] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  complete-coverage, AI/LLM, release-approval, autonomous-approval, and
  replacement-of-review claims across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests, sitemap/discovery
  output, and bot-oriented discovery surfaces, while allowing those terms only
  inside explicitly bounded non-claim, limitation, or unsafe-pattern contexts.
- [x] Add focused validation for unsupported conclusion verbs (`proves`,
  `guarantees`, `certifies`, `approves`, `replaces`, `resolves`, and
  unqualified `impacted`) across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests,
  sitemap/discovery output, and bot-oriented discovery surfaces, while
  allowing them only inside explicitly bounded unsafe-pattern, non-claim, or
  limitation regions. Allow `impacted` only when tied to a public-safe
  reducer-backed result.
- [x] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML, raw HTML attributes, metadata,
  sitemap/discovery output, fixtures, tests, and bot-oriented discovery
  surfaces.
- [x] Treat raw `facts.ndjson`, raw `index.sqlite`, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, and credential-like values as explicit forbidden
  private/raw validation targets.
- [x] Add focused validation for no blame language in visible copy, metadata,
  examples, and validation errors, checking at least a recorded advisory set
  of representative blame indicators such as `failed`, `fault`, `to blame`,
  `negligent`, `careless`, or attributing missing, reduced, or conflicting
  evidence to a named person, team, service, customer, or reviewer.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `npm test` from `site/` after site source is added.
- [x] Run `npm run validate` from `site/` after site source is added.
- [x] If `npm test` or `npm run validate` do not cover the new page, record
  the gap in `implementation-state.md` and add focused validation before
  closing the implementation phase.
- [x] Run `npm run build` from `site/` after site source is added.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
