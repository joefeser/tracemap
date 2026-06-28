# Site TraceMap Tools Incident Evidence Handoff Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Add a public site concept phase for an incident evidence handoff packet at the
stable route `/incident-evidence-handoff/`.

The page is for engineers, managers, and incident-adjacent reviewers who need
to bring static TraceMap evidence into a P1, production incident, or
post-incident conversation without implying runtime proof. It should answer
four handoff questions:

- What static evidence can I bring?
- Which proof path backs it?
- What does it not prove?
- Who should own the next runtime, release, or service-owner question?

This phase is a handoff packet/checklist, not another incident concept
overview. It is distinct from `/incident-call/`, `/static-triage/`,
`/review-room/`, `/manager-faq/`, `/packets/`, `/manager-packet/`,
`/manager-brief/`, and `/use-cases/incident-review/`.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded incident handoff concept route

The future implementation shall publish a concept-level incident evidence
handoff page at `/incident-evidence-handoff/`.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states `No public conclusion without evidence`.
- The page uses concept-level wording and does not imply a shipped operational
  workflow unless a later implementation has public proof for a stronger claim.
- The page presents a handoff packet/checklist rather than a general incident
  overview, product explainer, manager FAQ, or review meeting agenda.
- The page is explicitly about static source-to-artifact evidence that may be
  brought into an incident-adjacent conversation.
- The page states that runtime, production, telemetry, release, and ownership
  questions remain outside the static packet.

### Requirement 2: Differentiate the handoff packet from neighboring routes

The page shall make its relationship to existing incident and review pages
clear enough that readers do not confuse the surfaces.

Acceptance criteria:

- The page distinguishes `/incident-evidence-handoff/` from `/incident-call/`
  as a packet of bringable static evidence rather than a P1-call orientation
  narrative.
- The exact static-triage distinction line required below is also locked in
  validation per Requirement 7 and is mandatory regardless of word count.
- The page distinguishes `/incident-evidence-handoff/` from `/static-triage/`
  explicitly on the shared words "handoff" and "checklist": static triage is
  the engineer's live triage-framing checklist, while the incident evidence
  handoff packet is the bundle of already-framed static evidence, proof paths,
  limits, and next owners that gets carried into the next conversation. The
  page must not restate static triage's locked self-description verbatim.
- The page includes this exact rendered static-triage distinction line:
  `Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.`
- Before implementing the locked static-triage distinction line, the
  implementation agent must verify that `/static-triage/` still uses
  framing-question or checklist-adjacent language. If `/static-triage/` has
  changed its self-description, the agent must update the distinction line to
  reflect the actual differentiation while preserving the packet-vs-checklist
  boundary, and record the change in `implementation-state.md`.
- The page distinguishes `/incident-evidence-handoff/` from `/review-room/` as
  incident-adjacent evidence transfer rather than a meeting agenda for known,
  partial, and missing evidence.
- The page distinguishes `/incident-evidence-handoff/` from `/manager-faq/` as
  a packet/checklist for evidence handoff rather than a general manager-facing
  question-and-answer surface.
- The page distinguishes `/incident-evidence-handoff/` from `/packets/` as an
  incident-adjacent handoff packet scoped to a single conversation, rather than
  the general guide to reading TraceMap evidence packets.
- The page distinguishes `/incident-evidence-handoff/` from `/manager-packet/`
  and `/manager-brief/` as an engineer/manager handoff checklist for a specific
  incident-adjacent question, rather than a standing manager-facing packet or
  problem brief.
- The page distinguishes `/incident-evidence-handoff/` from
  `/use-cases/incident-review/` as the bringable evidence packet rather than
  the incident-review orientation narrative.
- The page includes the exact rendered line:
  `Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.`

### Requirement 3: Define the handoff packet content model

The page shall define a public-safe checklist for each static evidence item in
the handoff packet.

Acceptance criteria:

- The page includes a checklist with the fields static evidence, proof path,
  rule ID/evidence tier, coverage label, limitation, and next owner.
- `static evidence` describes a public-safe static finding or summary, such as
  a route, handler, DTO, package reference, configuration surface, SQL-facing
  reference, cross-app reference, dependency edge, or reducer-backed finding
  when public-safe evidence supports the example.
- `proof path` points to a public-safe source-to-artifact explanation, not a
  live production confirmation.
- `rule ID/evidence tier` keeps every visible claim attached to deterministic
  rule-backed evidence when such evidence is present.
- `coverage label` distinguishes complete, partial, reduced, gap, demo-only,
  or concept-only evidence using labels already accepted by neighboring public
  pages when possible.
- `limitation` states what the static evidence cannot prove.
- `next owner` identifies who should answer the next non-static question, such
  as telemetry owners, service owners, release owners, test owners, database
  owners, incident command, or code owners.
- The ownership split must include at minimum these static-side rows: route
  existence, DTO shape, package reference, dependency edge, and SQL-facing
  reference. Additional rows are permitted. The runtime/release side must
  include at minimum: telemetry, logs, traces, APM, release controls, tests,
  database ownership, service ownership, and incident command.
- The ownership split is distinct from the checklist: the checklist provides
  per-item next-owner fields, while the ownership split provides a consolidated
  view of the question-to-owner boundary.

### Requirement 4: Preserve static evidence boundaries and non-claims

The page shall keep all public claims bounded to deterministic static evidence
and visible limitations.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, complete product coverage, or production dependency
  understanding.
- The page does not imply TraceMap replaces telemetry, logs, traces, APM,
  incident command, incident timelines, ownership review, tests, release
  controls, service-owner judgment, database-owner judgment, or source review.
- The page avoids saying a surface, dependency, endpoint, package, database
  reference, or release is impacted unless a reducer-backed result and
  public-safe evidence are present.
- The page clearly labels analysis gaps, missing proof paths, syntax-only
  evidence, reduced coverage, and demo-only examples as partial or limited.
- The page states that static evidence can narrow questions for humans, but it
  cannot answer whether production traffic used a path, whether an outage was
  caused by a change, or whether a release is safe.

### Requirement 5: Publish only public-safe handoff material

The implementation shall publish authored concept copy, public-safe summaries,
or demo-safe evidence only.

Acceptance criteria:

- The page and metadata do not publish raw fact streams, raw SQLite databases,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, private
  sample names, connection strings, credentials, or local command output.
- Public examples are authored concept examples or are derived from public-safe
  generated summaries already acceptable for public site use.
- Any snippet-like text is explanatory copy or approved public demo material,
  not copied private source.
- Private repository evidence is described as requiring a private scan and
  private review before any summary becomes public copy.
- The page may mention public-safe report families, rule IDs, evidence tiers,
  coverage labels, limitations, commit SHA, extractor versions, file paths, and
  line spans only when those details come from public-safe summaries.

### Requirement 6: Link to proof surfaces and neighboring context

The page shall guide readers from the handoff packet into existing public-safe
proof, limitation, and neighboring incident context.

Acceptance criteria:

- The page links to `/proof-paths/`, `/validation/`, `/limitations/`,
  `/demo/result/`, `/incident-call/`, `/static-triage/`, `/review-room/`,
  `/manager-faq/`, `/packets/`, `/manager-packet/`, `/manager-brief/`, and
  `/use-cases/incident-review/` unless a route is unavailable at implementation
  time, in which case the gap is documented in `implementation-state.md`.
- Link text keeps the route roles distinct and does not imply runtime proof,
  release approval, production safety, incident cause, or complete coverage.
- The page links to `/docs/` or the most specific public docs page for
  rule/extractor context available at implementation time, or a future public
  rule catalog route when rule/extractor context is needed. The implementation
  documents any route gap or target choice in `implementation-state.md`.
- Existing relevant public pages may add minimal cross-links to
  `/incident-evidence-handoff/` using packet/checklist wording only.
- The route is not added to the canonical top navigation in this phase unless
  a separate navigation spec requires it.

### Requirement 7: Add discovery metadata and focused validation

The route shall be discoverable while preserving concept-level claim
boundaries and public-safety rules.

Acceptance criteria:

- Discovery metadata labels `/incident-evidence-handoff/` as `concept`.
- Discovery metadata uses the existing concept-page field shape and includes a
  fixed `preferredProofPath` of `/proof-paths/`, limitations, non-claims, and
  neighboring route hints.
- Page metadata includes title, description, canonical URL, Open Graph fields,
  and `og:type` consistent with neighboring concept pages.
- Sitemap metadata includes `/incident-evidence-handoff/`.
- Focused validation checks the route renders and contains
  `Public claim level: concept`, `No public conclusion without evidence`, the
  exact distinction lines from Requirement 2, and the checklist labels from
  Requirement 3.
- Focused validation checks required links and generated internal-link
  resolution.
- The focused validator must check that every link in the rendered page that
  targets a `/`-relative route also has a corresponding entry in
  `routes-index.json` or a `pages.json` sitemap entry. Any link target that
  does not resolve must either be listed as a documented gap in
  `implementation-state.md` or cause validation to fail.
- Focused validation enforces a bounded rendered word count between 400 and
  1800 words. The upper bound is a guard against scope drift, not a style
  target; a complete, non-redundant packet with all required sections should
  typically land between 600 and 1200 words.
- `Rendered word count` means visible body text of the page after stripping HTML
  tags, navigation, footer, sidebar, and metadata elements. It excludes
  `<title>`, `<meta>` content, `alt` attributes, and any elements rendered only
  in site navigation or the global footer. The validator must document the
  counting method in a comment so future authors can reproduce the count
  locally.
- Word-count enforcement must not cause removal of the required distinction
  line from Requirement 2 or the checklist labels from Requirement 3; those
  elements are mandatory regardless of total word count.
- If rendered word count is outside the 400 to 1800 word range, validation must
  fail with a clear word-count error. Authors must resolve overage by trimming
  nonmandatory copy, not by removing required distinction lines, checklist
  labels, ownership rows, proof-path links, or boundary statements.
- Focused validation asserts the generated `routes-index.json` entry for
  `/incident-evidence-handoff/` has `publicClaimLevel: concept`,
  `hintCategory: use-case`, `sourceType: site-page`, and
  `preferredProofPath: /proof-paths/`, matching neighboring concept
  validators.
- Focused validation checks that the ownership split contains at minimum the
  required static-side rows route existence, DTO shape, package reference,
  dependency edge, and SQL-facing reference, plus the required
  runtime/release-side rows telemetry, logs, traces, APM, release controls,
  tests, database ownership, service ownership, and incident command.
- Focused validation rejects forbidden runtime/AI positioning and exposed
  private or raw-artifact text. Denylist checks must be scoped to the
  `/incident-evidence-handoff/` route only, not to the full site build output,
  spec source files, validator source files, neighboring pages, or validator
  comments.
- The positioning denylist applies to normalized rendered body text, decoded HTML
  attributes, and public metadata values for the route and must reject overclaim
  phrases, at minimum: proves runtime behavior, proves production traffic,
  endpoint performance proof, proves outage cause, proves release safety, proves
  operational safety, AI-powered, LLM-powered, AI impact analysis engine, LLM
  impact analysis engine, complete product coverage, production dependency
  understanding, replaces telemetry, replaces logs, replaces traces, replaces
  APM, replaces incident command, replaces incident response, replaces
  ownership, replaces ownership review, replaces tests, replaces release
  controls, replaces service-owner judgment, replaces database-owner judgment,
  and replaces source review.
- The private/raw artifact denylist applies to the route's raw HTML, decoded
  HTML attributes, metadata values, and rendered body text, and must reject
  exposure phrases, at minimum: raw fact stream, raw SQLite, analyzer log, raw
  source snippet, raw SQL, raw config value, credential secret, local absolute
  path, raw remote, generated scan directory, private sample name, connection
  string, and credential. Boundary statements may use category words such as
  `config values` or `secrets` only when they do not match these exposure
  phrases.
- The validator must not rely solely on neighboring-validator inheritance for
  denylist coverage. Denylist checks should be case-insensitive substring
  matches, and any match on a denylist phrase in the scoped target should fail
  validation.
- The three denylist groups (non-claims, replacement boundaries, and
  private/raw artifacts) are organizational sections, not optional rollout
  phases. All three groups must be active and tested in the same validation
  pass.
- Implementation validation includes `git diff --check`, `npm test` from
  `site/`, `npm run validate` from `site/`, `npm run build` from `site/`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
  when feasible.
