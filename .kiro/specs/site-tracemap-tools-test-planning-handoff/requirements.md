# Site TraceMap Tools Test Planning Handoff Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Create a future public-site concept page or section that helps readers turn
TraceMap static evidence into better test-planning questions. The surface
should show how proof paths, evidence tiers, coverage labels, and limitations
can guide targeted conversations with test owners without claiming TraceMap
generates tests, proves behavior, approves releases, or replaces QA.

Candidate placements are `/test-planning/`,
`/reviewer-quickstart/test-planning/`, a section on `/reviewer-quickstart/`,
or a section on `/packets/assembly/`. The future implementation must choose the
least confusing placement after checking the live neighboring routes.

This is a concept-level public handoff surface. It is not a test generator, QA
workflow, runtime validation guide, release gate, performance test plan,
coverage proof, or AI-assisted impact-analysis page.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded test-planning handoff surface

The future implementation shall publish a concept-level public page or section
that helps readers translate TraceMap static evidence into targeted
test-planning questions.

Acceptance criteria:

- The surface visibly says `Public claim level: concept`.
- The surface visibly states `No public conclusion without evidence`.
- The surface uses one of the candidate placements: `/test-planning/`,
  `/reviewer-quickstart/test-planning/`, a section on `/reviewer-quickstart/`,
  or a section on `/packets/assembly/`.
- The implementation records the selected placement and rationale in
  `implementation-state.md` before editing site source.
- If the selected placement is standalone, the implementation adds route,
  sitemap, and discovery metadata consistent with neighboring concept pages.
- If the selected placement is an existing page section, the implementation
  updates only the minimum metadata needed for discovery and validation, and
  records why a standalone route was not selected.
- The surface explains that TraceMap static evidence can help humans ask more
  focused test-planning questions.
- The surface does not claim TraceMap generates tests, proves behavior,
  proves runtime behavior, proves production traffic, validates release
  safety, provides complete coverage, or replaces QA.

### Requirement 2: Distinguish from neighboring public pages

The surface shall make its role clear relative to existing reviewer, packet,
claim, validation, proof-path, and objections pages.

Acceptance criteria:

- The surface distinguishes itself from `/reviewer-quickstart/` as a
  test-planning handoff aid, not the general reviewer orientation.
- The surface distinguishes itself from `/packets/assembly/` as a question
  translation layer, not packet assembly instructions.
- The surface distinguishes itself from `/review-claim-checklist/` as
  test-conversation framing, not public claim review.
- The surface distinguishes itself from `/validation/` as planning questions,
  not validation proof or release validation.
- The surface distinguishes itself from `/proof-paths/tour/` as a use of proof
  paths for test owner conversations, not a proof-path walkthrough.
- The surface distinguishes itself from `/questions/objections/` as practical
  test-planning handoff language, not an objection-handling page.
- Cross-links use bounded anchor text and do not imply generated tests,
  complete test coverage, runtime proof, production validation, release
  approval, endpoint performance proof, or QA replacement.
- If any referenced neighboring route is unavailable or renamed at
  implementation time, the implementation records the route gap or substitute
  target in `implementation-state.md`.

### Requirement 3: Define the static evidence input model

The surface shall identify the public-safe fields a reader can carry from
TraceMap evidence into a test-planning conversation.

Acceptance criteria:

- The surface includes these required fields exactly as visible labels: claim
  label, proof path, rule ID/family, evidence tier, coverage label, changed
  surface, limitation, suggested test question, next owner, validation
  evidence, and non-claim.
- `claim label` is a bounded, human-readable summary of what static evidence
  supports.
- `proof path` points to the public-safe route, packet, or explanation that
  backs the claim label.
- `rule ID/family` identifies the deterministic rule or rule family behind the
  evidence when public-safe summaries provide it.
- `evidence tier` uses the TraceMap evidence tier language, such as
  Tier1Semantic, Tier2Structural, Tier3SyntaxOrTextual, or Tier4Unknown when
  applicable.
- `coverage label` identifies complete, partial, reduced, gap, syntax-only,
  demo-only, concept-only, or the closest existing public-site coverage label
  available at implementation time, using the existing public-site
  coverage-label vocabulary as the canonical source rather than introducing
  new labels. If that vocabulary is unavailable or ambiguous, the
  implementation records the gap in `implementation-state.md` and uses the
  evidence-tier language from the relevant public TraceMap documentation as a
  fallback.
- `changed surface` identifies the route, handler, DTO, dependency edge,
  package reference, SQL-facing reference, configuration surface, or other
  public-safe static surface that caused the question.
- `limitation` states what the static evidence cannot prove.
- `suggested test question` is phrased as a question for a human test,
  service, database, release, or QA owner, not as a generated test case.
- `next owner` identifies the human owner by role or review function, such as
  service owner, QA owner, database owner, release owner, security review, or
  source reviewer. Public examples and metadata must not name an individual
  person as the owner.
- `validation evidence` identifies what kind of future human validation would
  answer the question, such as unit tests, integration tests, contract tests,
  manual QA notes, telemetry review, release checklist evidence, or source
  review. It must not imply TraceMap has produced or executed that validation.
- `non-claim` states what the handoff is not asserting.

### Requirement 4: Include required handoff sections

The surface shall give readers a complete but compact handoff structure for
test-planning conversations.

Acceptance criteria:

- The surface includes a `static evidence input` section that explains what
  public-safe TraceMap evidence may be used as input.
- The surface includes a `test-planning questions` section with examples of
  safe question wording.
- The surface includes a `coverage caveats` section explaining partial,
  reduced, gap, demo-only, concept-only, and syntax-only boundaries.
- The surface includes `examples of safe handoff language` that preserve proof
  path, limitation, next owner, and non-claim together.
- Safe handoff examples may omit some fields for readability, but must not
  omit proof path, limitation, next owner, validation evidence, or non-claim.
- The surface includes `stop conditions` that tell readers when static
  evidence is insufficient for a test-planning conclusion, including when the
  only available evidence is concept-only or demo-only rather than backed by a
  real scan.
- The surface includes a `test owner handoff` section that assigns follow-up
  question ownership without blame language.
- The surface includes a `non-claims` section that explicitly states what
  TraceMap is not asserting.
- The surface may include compact examples, but examples must be authored
  concept examples or public-safe demo summaries, not private scan material.

### Requirement 5: Preserve claim and replacement boundaries

The surface shall keep public copy bounded to deterministic static evidence and
human-owned test planning.

Acceptance criteria:

- The surface does not claim generated tests, test sufficiency, runtime
  behavior proof, production traffic proof, endpoint performance proof, release
  safety, release approval, complete coverage, AI/LLM analysis, or complete
  product understanding.
- The surface does not imply TraceMap replaces QA, tests, source review,
  runtime observability, telemetry, logs, traces, APM, service-owner judgment,
  database-owner judgment, release-owner judgment, security review, compliance
  review, or human judgment.
- The surface does not say a change is `approved for release`, `ready to
  release`, `fully covered`, `tested by TraceMap`, `generated by TraceMap`, or
  `production-proven` unless the phrase appears only as a clearly labeled
  forbidden non-claim.
- The surface avoids blame language toward test owners, QA, service owners,
  reviewers, or previous authors.
- The surface states that static evidence can narrow and prioritize questions,
  but humans still choose, write, run, review, and interpret tests.
- The surface states that a missing or weak proof path is a stop condition for
  public conclusions, not a reason to infer test coverage.

### Requirement 6: Publish only public-safe material

The future implementation shall publish authored concept copy and public-safe
summaries only.

Acceptance criteria:

- The surface and metadata do not publish raw facts, SQLite content, analyzer
  logs, source snippets, SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, credential-like values, connection strings, tokens, keys,
  private repository identifiers, named individuals, or personal owner names.
- The surface does not expose raw fact records, `facts.ndjson`,
  `index.sqlite`, `logs/analyzer.log`, local scan output names, local machine
  paths, or private repository remotes.
- Public examples use synthetic concept language or already-approved
  public-safe demo summaries.
- Any reference to validation evidence remains categorical and does not reveal
  private test names, private command output, private CI logs, private
  credentials, or hidden review details.
- The implementation runs the repository private-path guard before PR
  creation.

### Requirement 7: Add focused validation expectations

The future implementation shall include validation that keeps the surface
bounded, discoverable, and visually usable.

Acceptance criteria:

- Focused validation checks required copy: `Public claim level: concept`,
  `No public conclusion without evidence`, all required section labels, and all
  required field labels.
- Focused validation checks required links to the selected placement's
  neighboring routes, including `/reviewer-quickstart/`,
  `/packets/assembly/`, `/review-claim-checklist/`, `/validation/`,
  `/proof-paths/tour/`, and `/questions/objections/` when those routes exist.
- Focused validation checks that the rendered surface includes a
  distinguishing statement for each required neighbor:
  `/reviewer-quickstart/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/validation/`, `/proof-paths/tour/`, and
  `/questions/objections/`. A link alone does not satisfy this check.
- Focused validation checks that all seven stop conditions are present as
  individually identifiable items in the stop-conditions section: missing proof
  path, private-only evidence, reduced coverage, concept-only or demo-only
  evidence, no validation evidence, uncertain owner, and a question requiring
  runtime observability.
- Focused validation checks page or section metadata, canonical URL and Open
  Graph metadata when standalone, and the selected concept claim level.
- Focused validation checks discovery and sitemap metadata when the surface is
  standalone; when embedded, validation checks whatever metadata is used to
  make the section discoverable from the existing route.
- Focused validation checks forbidden claims for generated tests, test
  sufficiency, runtime proof, production traffic, endpoint performance,
  release safety, release approval, complete coverage, AI/LLM analysis, and QA
  replacement.
- Focused validation checks forbidden positive claims using phrase-scoped
  patterns, such as `tested by TraceMap`, `generated by TraceMap`, `this change
  is safe`, `approved for release`, `ready to release`, `fully covered`, and
  `production-proven`, not bare tokens. The validator must allow boundary
  vocabulary used by this surface, including `public-safe`, `safe handoff
  language`, and `safe to share`, and must allow the required non-claims
  section to restate forbidden phrases when they appear as clearly labeled
  non-claims.
- Focused validation checks private/raw material exposure in rendered text,
  decoded attributes, public metadata, and raw HTML.
- Focused validation enforces a rendered visible-body word count between 450
  and 1600 words for a standalone route, or between 250 and 900 words for an
  embedded section.
- If the required sections, field labels, and neighbor distinctions cannot fit
  within the embedded-section word-count bound while remaining complete, the
  implementation shall prefer a standalone `/test-planning/` or nested
  `/reviewer-quickstart/test-planning/` route instead of dropping required
  content, and record that constraint in `implementation-state.md`.
- Word count excludes global navigation, footer, metadata, hidden validation
  data, alt attributes, and inline script and style blocks; required labels
  must be checked independently of word count.
- Future implementation runs standard site validation and a desktop and mobile
  browser sanity check for layout, link usability, text wrapping, overflow, and
  a basic accessibility check for heading order, descriptive link text, alt
  text, and color contrast consistent with neighboring concept pages.
