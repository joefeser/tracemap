# Site TraceMap Tools Evidence Handoff Template Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Create a future public-site concept page or section that gives readers a
reusable, public-safe template for handing TraceMap evidence to another
reviewer, owner, or follow-up role. The template keeps the claim, proof path,
evidence fields, limitations, non-claims, next owner, validation evidence, and
stop condition together so a static-evidence handoff does not become an
unsupported conclusion.

Candidate placements are `/handoff/template/`,
`/team-evidence-handoff/template/`, a section on
`/team-evidence-handoff/`, or a section on `/packets/assembly/`. The future
implementation must choose the least confusing placement after checking the
live neighboring routes.

This is a concept-level public template. It is not a generated handoff
feature, real organization ownership map, runtime proof surface, release
approval workflow, operational safety claim, complete-coverage claim, AI or
LLM analysis page, or replacement for human review.

## Shared Site Principle

No public conclusion without evidence.

## Relationship to Neighboring Surfaces

The evidence handoff template is a reusable field structure for one bounded
claim or question. It complements but must remain distinct from these existing
or planned surfaces:

- `/team-evidence-handoff/` focuses on receiver-specific handoff language. The
  template defines the required fields that travel with any receiver-facing
  handoff.
- `/incident-evidence-handoff/` scopes evidence transfer to incident-adjacent
  conversations. The template is not incident-specific and must not imply
  outage proof.
- `/packets/assembly/` explains how a human assembles public-safe review
  packet ingredients. The template is the reusable handoff form for one claim
  after the ingredients have been selected.
- `/reviewer-quickstart/` orients a reviewer. The template is not a general
  onboarding page.
- `/owners/follow-up/` is owner follow-up framing if it exists at
  implementation time. The template may name an owner role to ask, but it is
  not an ownership workflow or real org chart.
- `/decisions/evidence-record/` is decision-record framing if it exists at
  implementation time. The template captures handoff context, not a final
  decision record.

## Requirements

### Requirement 1: Publish a bounded template surface

The future implementation shall publish a concept-level public page or section
that presents a reusable evidence handoff template.

Acceptance criteria:

- The surface visibly says `Public claim level: concept`.
- The surface visibly states `No public conclusion without evidence`.
- Unless the stop/needs-revision criterion below applies, the surface uses one
  of the candidate placements:
  `/handoff/template/`, `/team-evidence-handoff/template/`, a section on
  `/team-evidence-handoff/`, or a section on `/packets/assembly/`.
- The implementation records the selected placement and rejected alternatives
  in `implementation-state.md` before changing site source.
- If none of the four candidate placements are acceptable after inspecting the
  live site, the implementation stops, records all conflicts in
  `implementation-state.md`, and moves the spec back to needs-revision before
  changing site source.
- If the selected placement is standalone, the implementation adds route,
  canonical metadata, Open Graph metadata, sitemap metadata, and discovery
  metadata consistent with neighboring concept pages.
- If the selected placement is an existing page section, the implementation
  records why section placement is clearer than a standalone route and how the
  section can be linked directly.
- Section placement is viable only if all required fields, examples, stop
  conditions, non-claims, and six neighbor distinctions fit the embedded
  word-count bound without weakening required content. Otherwise the
  implementation selects a standalone route and records the decision in
  `implementation-state.md`.
- The surface explains that the template helps humans transfer bounded static
  evidence and follow-up questions.
- The surface does not claim that TraceMap generates handoffs, proves runtime
  behavior, proves production traffic, approves releases, provides operational
  safety, provides complete coverage, performs AI or LLM analysis, or replaces
  human review.

### Requirement 2: Distinguish from neighboring pages

The surface shall make its role clear enough that readers do not confuse the
template with neighboring handoff, packet, reviewer, owner, or decision
surfaces.

Acceptance criteria:

- The surface distinguishes itself from `/team-evidence-handoff/` as a
  reusable field template rather than receiver-specific handoff guidance.
- The surface distinguishes itself from `/incident-evidence-handoff/` as a
  general static-evidence handoff template rather than an incident-adjacent
  packet.
- The surface distinguishes itself from `/packets/assembly/` as a filled
  handoff form for one bounded claim rather than the broader workflow for
  assembling packet ingredients.
- The surface distinguishes itself from `/reviewer-quickstart/` as a template
  that travels with evidence rather than reviewer onboarding.
- The surface distinguishes itself from `/owners/follow-up/` as a role-to-ask
  field rather than a real organization ownership claim or follow-up workflow.
- The surface distinguishes itself from `/decisions/evidence-record/` as
  handoff context rather than a final decision record.
- Cross-links use bounded anchor text and do not imply runtime proof, release
  approval, operational safety, complete coverage, AI or LLM analysis, or
  replacement of human review.
- If any neighboring route is unavailable or renamed at implementation time,
  the implementation records the route gap, substitute, or deferred link in
  `implementation-state.md` instead of adding a dead link.
- The surface links to support routes `/proof-paths/`, `/limitations/`, and
  `/validation/` when those routes exist at implementation time, using bounded
  anchor text. If a support route is unavailable or renamed, the implementation
  records the route gap, substitute, or deferred link in
  `implementation-state.md` instead of adding a dead link.

### Requirement 3: Define required template fields

The surface shall publish a reusable template with all 15 required fields
visible as labels.

Acceptance criteria:

- The required visible field labels are: handoff question, audience, proof
  path, public claim level, rule ID/family, evidence tier, coverage label,
  public-safe path/span, commit SHA, extractor version, limitation, non-claim,
  validation evidence, owner to ask, and stop condition.
- `handoff question` is the bounded question or claim being transferred, not a
  broad investigation topic.
- `audience` identifies the receiving role or review context without naming a
  private individual. The rendered template should distinguish audience as
  the receiver of the handoff from `owner to ask` as the role responsible for
  the next open question.
- `proof path` points to public-safe pages, public-safe summaries,
  documentation, rule catalog material, report-family summaries, or a private
  review location referenced only categorically by role or function, such as
  internal PR review or private index. It must never expose a local path,
  private remote, private repository identifier, generated scan directory, or
  raw material, and it must never name or imply a specific private repository,
  remote, or private index identifier.
- `public claim level` remains `concept` for this page and may only name
  stronger claim levels when quoting a cited public-safe evidence surface.
  Synthetic examples keep `concept` unless they quote a real public-safe
  evidence surface. Stronger claim levels must come from the existing
  public-site claim-level vocabulary used by site metadata or discovery
  conventions at implementation time; implementers must not invent new claim
  levels for this template.
- `rule ID/family` names the deterministic rule basis when public-safe. If a
  specific rule ID cannot be named publicly, the template uses a rule family
  and records the limitation.
- `evidence tier` uses TraceMap vocabulary:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or
  `Tier4Unknown`.
- `coverage label` is copied from the cited evidence surface or recorded as
  concept-only, demo-only, partial, reduced, gap, unknown, syntax-only, or the
  closest existing public-site coverage label available at implementation
  time.
- `public-safe path/span` may name checked-in public demo paths or sanitized
  source references, but must not expose local absolute paths, private
  repository paths, private remotes, generated scan directories, or source
  snippets.
- `commit SHA` and `extractor version` identify scan context only when those
  values are public-safe. Missing or private-only values remain explicit
  limitations.
- `limitation` states what the static evidence cannot prove.
- `non-claim` states the runtime, release, safety, ownership, completeness, AI
  or LLM, or human-review replacement claim the handoff is not making.
- `validation evidence` names a public-safe validation result, command family,
  review evidence, or test summary without publishing command output, private
  logs, hidden validation details, credentials, or raw artifacts.
- `owner to ask` identifies a role or review function for the next question,
  such as code owner, service owner, database owner, telemetry owner, test
  owner, release owner, security reviewer, source reviewer, or manager.
- `stop condition` names the reason the handoff must pause, downgrade, remain
  private, or move to another owner.

### Requirement 4: Include required sections and examples

The surface shall give readers a compact but complete handoff structure.

Acceptance criteria:

- The surface includes sections for when to use it, neighbor distinctions,
  template, filled synthetic example, unsafe example, handoff checklist, stop
  conditions, and non-claims.
- `when to use it` explains that the template is for transferring one bounded
  static-evidence claim, question, or proof path to another human reviewer or
  owner.
- `template` contains all required fields from Requirement 3.
- `filled synthetic example` uses clearly labeled synthetic concept data and
  populates every required field from Requirement 3, including audience and
  limitation. It uses synthetic public-safe paths, synthetic rule families or
  rule IDs, synthetic commit-like values when needed, and no private sample
  names. Fields that would be private or unavailable are shown as an explicit
  limitation rather than omitted.
- `unsafe example` shows what not to publish or repeat, using clearly labeled
  synthetic wording and without exposing real raw material.
- `handoff checklist` requires handoff question, public claim level, proof
  path, rule ID or family, evidence tier, coverage label, limitation,
  non-claim, validation evidence, owner to ask, and stop condition to stay
  attached, and either includes `audience` as a visible field label or includes
  a visible note stating that audience is omitted when the receiver and owner
  to ask are the same role.
  If the checklist is intentionally a reduced subset of the full template,
  the surface states which fields are omitted and why. The handoff checklist is
  expected to be a reduced subset of the full template: it may omit
  public-safe path/span, commit SHA, and extractor version because those remain
  in the full template, and it may treat audience as conditional when the
  receiver and owner to ask are the same role.
- Validation passes if the checklist includes `audience` as a visible field
  label or includes the visible same-role omission note.
- `stop conditions` lists at minimum missing proof path, private-only support,
  raw or private material, unknown or reduced coverage without label,
  unsupported runtime proof wording, unsupported release or safety wording,
  unsupported complete-coverage wording, AI or LLM analysis wording, no
  validation evidence, no owner to ask, and blame language.
- `non-claims` explicitly states what the template does not prove, approve,
  automate, or replace.
- The surface avoids blame language toward owners, reviewers, service teams,
  test owners, incident participants, database owners, previous authors, or
  future implementers.

### Requirement 5: Preserve public-safe boundaries

The future implementation shall publish authored concept copy, public-safe
summaries, or synthetic examples only.

Acceptance criteria:

- The surface and metadata do not publish raw facts, SQLite content, analyzer
  logs, source snippets, SQL, config values, secrets, local paths, remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, credential-like values, connection strings, tokens, keys,
  private repository identifiers, named individuals, or personal owner names.
- The surface does not expose `facts.ndjson`, `index.sqlite`,
  `logs/analyzer.log`, local scan output names, local machine paths, private
  repository remotes, raw SQL text, raw configuration values, or source
  snippets.
- Public examples use synthetic concept language or already-approved
  public-safe demo summaries.
- Any public-safe path/span examples are synthetic, public-demo, or sanitized;
  they do not expose private directory structures or copied source text.
- Any reference to validation evidence remains categorical and does not reveal
  private test names, private command output, private CI logs, credentials,
  hidden review details, or internal reviewer names.
- The implementation runs the repository private-path guard before PR
  creation.

### Requirement 6: Preserve claim, ownership, and replacement boundaries

The surface shall keep public copy bounded to deterministic static evidence and
human-owned review.

Acceptance criteria:

- The surface does not claim generated handoff feature behavior, real org
  ownership knowledge, runtime proof, production traffic knowledge, endpoint
  performance measurement, outage cause, release approval, release safety,
  operational safety, complete coverage, AI impact analysis, LLM analysis,
  autonomous review, or replacement of human review.
- The surface does not imply TraceMap replaces human review, source review,
  ownership decisions, telemetry, logs, traces, APM, tests, release controls,
  incident response, service-owner judgment, database-owner judgment, security
  review, compliance review, manager judgment, or human judgment.
- The surface does not say a change, route, endpoint, dependency, package,
  database reference, owner, release, incident, or service is impacted, safe,
  unsafe, approved, blocked, owned, root cause, validated for release, or
  production proven unless the phrase appears only as a clearly labeled
  forbidden non-claim or unsafe example.
- The surface may say the template helps humans carry deterministic static
  evidence, limits, and next questions together.
- The surface states that a missing proof path, weak evidence tier, reduced
  coverage, or missing owner is a stop condition for public conclusions, not a
  reason to infer stronger proof.

### Requirement 7: Validate copy, metadata, links, and safety

The future implementation shall include focused validation that keeps the
template bounded, discoverable, and usable.

Acceptance criteria:

- Focused validation checks required rendered copy:
  `Public claim level: concept`, `No public conclusion without evidence`, all
  required field labels, all required section labels, required stop
  conditions, and required non-claims.
- Focused validation checks that the rendered template distinguishes
  `audience` as the handoff receiver from `owner to ask` as the role
  responsible for the next open question.
- Focused validation checks required links to neighboring routes that exist at
  implementation time, including `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/packets/assembly/`,
  `/reviewer-quickstart/`, `/owners/follow-up/`,
  `/decisions/evidence-record/` when those routes are present.
- Focused validation checks required support links to `/proof-paths/`,
  `/limitations/`, and `/validation/` when those routes are present.
- Focused validation checks that the rendered surface includes a
  distinguishing statement for each required neighboring route:
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`. These distinctions are required even when a
  live link is unavailable and the route is recorded as a gap. A link alone
  does not satisfy this check.
- If the selected placement is standalone, validation checks page title,
  description, canonical URL, Open Graph fields, `og:type`, discovery
  metadata, sitemap metadata, `publicClaimLevel: concept`, bounded
  limitations, and bounded non-claims.
- If the selected placement is embedded, validation checks the selected
  section anchor, parent-page metadata changes, discovery affordance, required
  rendered copy, and documented sitemap decision.
- Focused validation checks rendered text, decoded HTML, raw HTML attributes,
  and metadata for forbidden generated handoff, real ownership, runtime,
  production, release-safety, operational-safety, AI or LLM, autonomous-review,
  complete-coverage, human-review replacement, and blame claims.
- Focused validation checks rendered text, decoded HTML, raw HTML attributes,
  and metadata for forbidden private or raw material.
- Focused validation checks rendered text, decoded HTML, raw HTML attributes,
  and metadata for named individuals, personal owner names, and real
  organization names using a denylist or named-entity check where available.
  Where automated detection is not reliable, the implementation records a
  required manual public-safety review gate in `implementation-state.md`, and
  the page uses only role-based or synthetic names.
- Forbidden-material scanning includes shape-based patterns for
  credential-like and scan-context values: realistic commit SHAs, including
  full 40-character hex strings and abbreviated 7-12 character lowercase hex
  strings, API tokens, keys, and connection strings, across rendered examples,
  decoded HTML, raw HTML attributes, and metadata.
  Synthetic scan context must use clearly non-realistic forms such as
  `synthetic-sha-0001`; a realistic-looking value fails validation even inside
  a labeled synthetic example.
- Positive-claim and forbidden-material scans are scoped so that the required
  non-claims section, unsafe example, stop conditions, handoff checklist,
  template field descriptions, neighbor distinctions, and when-to-use section
  may mention forbidden topics only in clearly negated, clearly labeled, or
  clearly cautionary contexts. A forbidden claim or forbidden private/raw
  phrase outside a labeled non-claim, unsafe example, stop-condition,
  checklist, template-field-description, neighbor-distinction, when-to-use, or
  caution context fails validation.
- Forbidden positive-claim checks use phrase-scoped patterns, not bare tokens.
  Example patterns include `proves runtime`, `production proven`,
  `approved for release`, `fully covered`, `generates handoffs`, and
  `AI-powered impact analysis`. Validation allows bounded boundary vocabulary
  and negated forms such as `does not prove runtime`,
  `unsupported runtime proof wording`, `No public conclusion without evidence`,
  `public-safe`, and `static evidence`.
- A rendered context is machine-checkably labeled when its nearest section
  heading or visible example label contains `non-claim`, `not claimed`,
  `unsafe example`, `not recommended`, `stop condition`,
  `stop conditions`, `handoff checklist`, `template field`,
  `neighbor distinction`, `distinguish`, `when to use`, or `caution`,
  or when the containing element uses an explicit context marker such as
  `data-context="non-claim"`, `data-context="unsafe-example"`,
  `data-context="stop-condition"`, `data-context="template-field"`,
  `data-context="neighbor-distinction"`, `data-context="when-to-use"`, or
  `data-context="caution"`. A bare `template` section heading does not by
  itself label every descendant as an allowed forbidden-term context. Metadata
  has no rendered section context, so forbidden terms in metadata must appear
  only in explicit limitations or non-claims fields.
- Presence checks for `non-claim` field labels, `non-claims` section labels,
  `stop condition` field labels, `stop conditions` section labels, and
  `public claim level` intro or field labels use exact, context-scoped tokens
  so substring collisions do not satisfy the wrong validation rule.
- Focused validation checks that examples are labeled synthetic or derived
  from already-approved public-safe demo summaries.
- Focused validation checks that the filled synthetic example populates all 15
  required fields from Requirement 3, with private or unavailable values shown
  as explicit limitations rather than omitted.
- Focused validation enforces a rendered visible-body minimum of 500 words for
  a standalone route or 300 words for an embedded section, and a target maximum
  of 1600 words standalone or 900 words embedded. Exceeding the standalone
  target emits a tightening warning, not a hard failure, provided all required
  field labels, required sections, required stop conditions, required
  non-claims, and all six neighbor distinctions are present and pass
  independently. Embedded placement remains a hard failure if required content
  cannot fit the 900-word bound without weakening required content.
- Embedded placement should be selected only if the required fields,
  examples, stop conditions, non-claims, and neighbor distinctions can fit the
  embedded word-count bound without dropping required content. Otherwise the
  future implementation should select a standalone route and record that
  decision in `implementation-state.md`.
- Standalone copy that exceeds the 1600-word target is a signal to tighten
  descriptions and examples, not to drop required field labels, sections, stop
  conditions, non-claims, or neighbor distinctions.
- Word count excludes global navigation, footer, metadata, hidden validation
  data, alt attributes, and inline script and style blocks. Required labels,
  required sections, and neighbor distinctions must pass independently of word
  count.
- The implementation records the existing public-site claim-level vocabulary
  and coverage-label source consulted in `implementation-state.md` so reviewers
  can verify that the template did not invent stronger claim levels or
  coverage labels.
- Future implementation runs standard site validation, private-path guard,
  build validation, and desktop and mobile browser sanity checks for layout,
  link usability, text wrapping, overflow, heading order, descriptive link
  text, and accessibility patterns consistent with neighboring concept pages.
