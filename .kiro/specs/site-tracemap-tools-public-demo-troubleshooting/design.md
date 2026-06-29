# Site TraceMap Tools Public Demo Troubleshooting Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Purpose

This design describes a future public-safe troubleshooting page or section for
the TraceMap public demo. It gives implementation a stable placement choice,
content model, row matrix, validation plan, and boundary vocabulary while
keeping this branch spec-only.

The design does not implement site code, generated output, scanner behavior,
reducer behavior, support tooling, runtime diagnostics, or validation scripts.

## Placement Options

Recommended starting options:

- `/demo/troubleshooting/`: preferred standalone route when the guidance is
  expected to grow or when the demo needs a durable public troubleshooting
  URL.
- `/demo/help/`: acceptable standalone route if the site's demo IA prefers
  plain-language help over a troubleshooting label.
- Section on `/demo/runbook/`: use when troubleshooting should stay attached
  to the demo reading sequence and the matrix can remain compact.
- Section on `/demo/start-here/`: use when the page is primarily first-visit
  orientation and the section can stay short.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/demo/runbook/`, because the runbook should remain the reading
  sequence and process orientation.
- Replacing `/demo/start-here/`, because start-here should remain onboarding
  rather than a row-by-row troubleshooting table.
- Replacing `/demo/result/`, because the result route should remain the
  public-safe demo outcome and proof pointers.
- Replacing `/demo/proof-upgrades/`, because proof upgrades should remain
  future proof-improvement planning.
- Replacing `/validation/`, because validation should remain check and
  evidence-quality orientation.
- Replacing `/limitations/`, because limitations should remain canonical
  boundaries and non-claims.
- Replacing `/questions/objections/`, because stakeholder objections require
  broader answers than route-level demo troubleshooting.
- Adding to primary navigation before information-architecture review, because
  concept-level demo guidance is likely secondary support content.

## Page Model

Recommended sections:

1. Opening: state `Public claim level: concept`, `No public conclusion without
   evidence`, and the short promise that troubleshooting routes confusion to
   public-safe checks.
2. When to use this: list route, summary, proof expectation, coverage label,
   private-only evidence, unsupported wording, validation mismatch, and next
   owner questions.
3. What it is not: support contract, runtime diagnostic tool, production proof,
   release safety or approval, endpoint performance proof, complete coverage,
   AI/LLM analysis, or validation replacement.
4. Troubleshooting matrix: include the required rows and fields.
5. Safe wording: provide bounded phrases that preserve labels and stop
   conditions.
6. Rejected wording: show unsupported phrases only inside a programmatically
   identifiable rejected-pattern region.
7. Adjacent routes: distinguish the troubleshooting surface from demo runbook,
   start-here, result, proof-upgrades, validation, limitations, and questions.
8. Owner handoff: name role-based next owners and route choices without naming
   private teams, customers, services, repositories, local paths, or raw
   artifacts.
9. Stop conditions and non-claims: restate when to downgrade, keep internal,
   or ask for public-safe evidence.

For section placement, the matrix may render as a compact card/list pattern
instead of a wide table, but the required fields must remain programmatically
associated with each row. The troubleshooting section must not visually or
structurally dominate the host page's primary orientation. Implementation
must record a host-page crowding check in `implementation-state.md`.
When the crowding check uses a word-count basis, it must count total rendered
section words including the troubleshooting matrix because the matrix is the
dominant element. This is distinct from the requirements word-count bound,
which excludes required matrix text. A rendered-height basis inherently
includes the matrix.

## Required Matrix

| Scenario | Symptom | Likely public-safe cause | What to check | What not to conclude | Next owner/route | Stop condition | Non-claim |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Missing route | A linked public demo or proof route returns missing, redirects unexpectedly, or is absent from discovery. | Route not implemented yet, moved, intentionally omitted, or not published in the current public build. | Check the demo runbook, start-here route, sitemap/discovery metadata, and visible route-deferral labels. | Do not conclude the evidence exists, does not exist, proves the claim, or disproves the claim. | Site owner or demo owner; route to `/demo/runbook/`, `/demo/start-here/`, or `/limitations/`. | Stop before repeating proof wording until a resolving public-safe route or explicit deferral exists. | Missing route is not proof, runtime diagnosis, release approval, or complete coverage. |
| Outdated demo summary | Public summary text appears older than surrounding demo routes or proof wording. | Summary copy may be stale, a proof upgrade may be planned, or validation language may have changed. | Check demo result, proof-upgrades route, validation route, visible date metadata, and visible claim-level metadata. | Do not conclude current-head, current-release, current-proof, or live production state. | Demo owner or site owner; route to `/demo/result/`, `/demo/proof-upgrades/`, or `/validation/`. | Stop before current wording until the public summary and proof expectation match. | Stale summary is not current proof or a clean-demo claim. |
| Broken proof expectation | A page promises proof that a public visitor cannot follow or validate. | Link, label, route, artifact summary, or explanation may be incomplete or intentionally deferred. | Check the named proof route, validation expectations, limitations, and any visible deferred or unavailable label. | Do not conclude public proof exists or that private/raw evidence may stand in for public proof. | Demo owner, validation owner, or site owner; route to `/validation/` or `/limitations/`. | Stop before public proof wording until the proof path is public-safe and resolvable. | Incomplete proof path is not production proof, runtime proof, or release safety. |
| Reduced coverage label | Demo copy shows reduced, partial, syntax fallback, unavailable, or unknown coverage. | The public demo may be preserving a real evidence boundary rather than hiding a result. | Check the coverage label, evidence tier wording, limitation, validation expectation, and adjacent explanation. | Do not conclude complete coverage, absence of impact, clean repo, release safety, or Tier1 semantic certainty. | Validation owner or limitations route; route to `/validation/` and `/limitations/`. | Stop before strengthening the claim or removing the reduced label. | Reduced coverage is review input, not complete proof. |
| Private-only evidence | The demo refers to internal support, private-only evidence, or unavailable details. | Evidence may exist outside the public site but lacks a public-safe summary. | Check for a public-safe summary, limitations note, proof-upgrades plan, or owner handoff. | Do not cite private-only material as public proof or publish raw/private details. | Site owner or reviewer; route to `/limitations/` or `/demo/proof-upgrades/`. | Stop before public proof wording until a public-safe summary exists. | Private-only evidence is not public proof. |
| Unsupported claim wording | Copy implies stronger proof than the visible public evidence supports. | Claim wording may have drifted ahead of the proof route, validation state, or limitation. | Check the exact wording against the visible proof route, validation route, limitations, and public claim level. | Do not repeat support SLA, runtime diagnosis, production proof, release approval, endpoint performance, complete coverage, AI/LLM, or replacement-of-review claims. | Site owner or reviewer; route to `/limitations/`, `/validation/`, or `/questions/objections/`. | Stop, downgrade, or remove wording until evidence and non-claims match. | Unsupported wording is not evidence. |
| Validation mismatch | Demo text and validation route describe different expectations or status. | Validation copy, demo copy, or discovery metadata may be stale or scoped differently. | Check `/validation/`, route metadata, sitemap/discovery metadata, and the demo page's visible validation expectation. | Do not claim validation passed, failed, or covers the demo unless the public validation surface says so. | Validation owner or site owner; route to `/validation/`. | Stop before validation-pass wording until the public validation expectation is aligned. | Mismatch is not validation evidence or runtime proof. |
| Where to ask next | The visitor cannot tell which public-safe page or owner should resolve the question. | The route may need a clearer handoff, owner label, or adjacent-route pointer. | Check owner labels, adjacent route links, non-claims, and whether the question belongs to demo, validation, limitations, or objections. | Do not conclude the question is resolved, approved, or diagnosed because it was handed off. | Demo owner, site owner, validation owner, or reviewer; route to the most specific public-safe adjacent page. | Stop after recording the evidence question, requested public-safe next check, and non-claim. | Asking next transfers the question; it does not prove or approve anything. |

The implementation may adjust wording for house style, but it must preserve
the scenario set, fields, boundaries, and stop conditions.

## Safe Wording Patterns

Use bounded wording:

- `The public route is missing or moved; wait for a resolving route or recorded
  deferral before repeating proof wording.`
- `The demo summary may be stale, so current-proof wording should wait.`
- `The proof expectation is incomplete and needs a public-safe route or
  summary.`
- `Coverage is reduced, so the label stays visible.`
- `Private-only evidence needs a public-safe summary before it can support
  public copy.`
- `This wording should be downgraded, removed, or routed to limitations and
  validation.`
- `The validation expectation does not match yet, so passed-validation wording
  should wait.`
- `Ask the named owner for the next public-safe check; the handoff is not a
  conclusion.`

Preferred verbs:

- `check`
- `label`
- `preserve`
- `route`
- `downgrade`
- `remove`
- `defer`
- `hand off`
- `record`
- `stop`

## Unsafe Wording Patterns

Reject unsupported wording:

- `The demo route is missing, so the claim is proven.`
- `The summary is probably fine, so current release wording is safe.`
- `Private-only evidence is enough public proof.`
- `Reduced coverage proves no impact.`
- `The proof route is confusing, but the release is approved.`
- `The page diagnoses runtime behavior or endpoint performance.`
- `TraceMap provides AI or LLM impact analysis.`
- `The demo uses prompt-based classification, embedding search, or vector
  database analysis.`
- `Troubleshooting replaces validation, tests, owner review, or human review.`

Unsafe examples must be framed as rejected patterns, not live claims. They
must not appear in page metadata, link text, summaries, captions, or discovery
records as affirmative statements.

An explicitly bounded rejected-pattern region must use a programmatically
identifiable marker such as a dedicated component, wrapper element, or data
attribute. Visual-only styling or prose-only labels are not enough for
validation.

If the site does not already define a standard rejected-pattern marker, the
implementation must introduce one and record the component name, element type,
or data attribute in `implementation-state.md` before writing site source. The
validation step for forbidden claims outside rejected regions must target the
same marker.

Required non-claim, limitation, and matrix `what not to conclude` and
`non-claim` copy must also use a programmatically identifiable non-claim
marker that is distinct from the rejected-pattern marker. If the site does not
already define a standard non-claim marker, implementation must introduce one
and record the component name, element type, or data attribute in
`implementation-state.md` before writing site source. Forbidden-claim
detection should target affirmative claim context and exclude both marked
rejected-pattern regions and marked non-claim regions, while private/raw
material detection remains global.

## Adjacent Surface Distinctions

The troubleshooting page should answer "What should I check when public demo
guidance does not line up?"

Neighboring pages answer different questions:

- `/demo/runbook/`: "What is the intended demo reading sequence?"
- `/demo/start-here/`: "Where should a first-time visitor begin?"
- `/demo/result/`: "What public-safe result or summary is being shown?"
- `/demo/proof-upgrades/`: "Which proof improvements are planned or tracked?"
- `/validation/`: "What checks ran or should run, and what do they validate?"
- `/limitations/`: "What are the boundaries and non-claims?"
- `/questions/objections/`: "How should stakeholders understand common
  concerns?"

If an adjacent route is absent, moved, or concept-only at implementation time,
record that fact in `implementation-state.md` and avoid dead links. When an
adjacent route does not yet exist, omit the link and name the surface without a
hyperlink, or include a visible `(planned)` qualifier. Do not link to a known
missing route.

Owner handoff copy is descriptive, not a tracked ticket. Recording the
evidence question, requested next check, and non-claim must not imply response
times, support channels, guaranteed answers, or service-level commitments.

## Metadata And Discovery

Standalone route metadata should:

- Use concept-level title and description wording.
- Include `publicClaimLevel: concept` or the current equivalent site metadata.
- Prefer discovery categories aligned with demo guidance, validation,
  limitations, proof paths, or public-site orientation.
- Include non-claims for support SLA, runtime diagnosis, production proof,
  endpoint performance, release approval or safety, operational safety,
  complete coverage, AI/LLM analysis, prompt-based classification, embedding
  search, vector database analysis, absence-of-impact proof, clean-repo claims
  under reduced analysis, and replacement of validation or human review.
- Avoid affirmative metadata phrases that imply a troubleshooting route proves
  or diagnoses anything.

Section placement metadata should:

- Preserve the host route's claim level and non-claims.
- If the host route's visible claim level is not `concept`, scope the
  troubleshooting label to the section, such as `Troubleshooting guidance --
  public claim level: concept`, and record the host claim level plus
  reconciliation wording in `implementation-state.md`.
- Add stable anchors for the troubleshooting sections.
- Validate duplicate IDs and anchor resolution in generated HTML.
- Validate that host and section claim-level statements do not contradict.

## Validation Plan

Future implementation should add focused validation that checks:

- Visible `Public claim level: concept`.
- Visible `No public conclusion without evidence`.
- Required rows and required fields in rendered HTML.
- Required row fields in rendered HTML with table-header association or an
  equivalent programmatic field-label association marker.
- Required adjacent-route distinctions and allowed links.
- Bounded anchor text for adjacent-route links.
- Absence of public-facing checks that point visitors to internal spec
  artifacts such as `implementation-state.md`, `tasks.md`, `.kiro/specs/`, or
  other non-public author material. Relative path validation for directory
  segments such as `.kiro` and `specs` must split candidate paths into
  segments and check individual segment matches rather than using string
  containment or slash-wrapped substring matching.
- Standalone route metadata, sitemap metadata, and discovery metadata if
  standalone.
- Section host metadata and anchor uniqueness if implemented as a section.
- Section-level claim-label scoping and host claim-level reconciliation when a
  section is placed on a host whose visible claim level is not `concept`.
- A measurable host-page crowding check for section placement, including
  rendered-height or word-count relationship to the host page's primary
  content. Validation records whether the crowding basis is rendered height or
  matrix-inclusive word count, and must not reuse the matrix-excluding
  word-count base for the crowding measure.
- Forbidden live claims and unsupported wording outside marked rejected
  regions and marked non-claim regions. Forbidden-claim validation targets
  affirmative claims and may exclude marked rejected-pattern regions and marked
  non-claim regions because rejected claim examples and required negated
  non-claims are allowed only there.
- Rejected-pattern regions carry the same programmatic marker used by
  forbidden-claim validation.
- Required non-claim, limitation, and matrix `what not to conclude` and
  `non-claim` regions carry the programmatic non-claim marker used by
  forbidden-claim validation.
- Forbidden private/raw/local material everywhere with no exception, including
  marked rejected-pattern regions, marked non-claim regions, rendered text,
  decoded HTML, attributes, metadata, sitemap output, discovery output,
  fixtures, tests, and bot-oriented discovery surfaces.
- Illustrative examples are synthetic or already public-safe.
- Owner-handoff and `where to ask next` copy does not imply response times,
  support channels, ticketing, guaranteed answers, or service-level
  commitments.
- Word count bounds: 700 to 1,500 visible body words for a standalone route,
  or 350 to 900 visible body words for a section placement, excluding
  navigation, metadata, code blocks, and required matrix text. Required matrix
  text means the content of the required troubleshooting matrix rows and their
  column headers, but not introductory prose, section headings, adjacent-route
  descriptions, safe-wording examples, or rejected-wording examples outside
  the matrix. For section placement, the matrix-inclusive crowding check is the
  governing size guard for total rendered section dominance; this word-count
  bound applies only to surrounding prose.
- Matrix cells outside the marked non-claim fields, and safe-wording examples,
  are subject to forbidden-claim scanning and must be phrased to avoid
  affirmative forbidden claims.
- Desktop and mobile browser sanity after layout changes.
