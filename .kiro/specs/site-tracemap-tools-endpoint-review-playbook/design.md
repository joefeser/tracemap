# Site TraceMap Tools Endpoint Review Playbook Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Purpose

This design note gives the Kiro spec-review wrapper and future implementer a
concrete shape for the `site-tracemap-tools-endpoint-review-playbook` phase.
The phase is spec-only. It defines a future public-safe endpoint review
playbook and does not implement site code.

## Proposed Route

Use `/use-cases/endpoint-review/` for the future page. The route belongs under
`/use-cases/` because the page is an engineer-facing use case for reviewing a
single endpoint-adjacent packet, not a new proof source, scanner output, or
public demo result.

Rejected route options:

- `/endpoint-review/`: clear, but less consistent with the current use-case
  grouping.
- `/demo/endpoint-review/`: too strong for this phase because the spec does not
  anchor a specific endpoint review packet to checked-in public demo proof.
- `/review-room/endpoint/`: too meeting-oriented; `/review-room/` already owns
  agenda framing while this page owns the engineer playbook.

## Page Shape

Use existing long-form static page composition:

- Hero or intro with `Public claim level: concept`, the shared principle, and
  the exact line `Endpoint review starts with static evidence, not certainty.`
- Evidence packet section naming endpoint-adjacent static paths, packages,
  config surfaces, SQL-facing surfaces, coverage labels, and limitations.
- Review workflow section for bounded review question, static paths, packages,
  config surfaces, SQL-facing surfaces, coverage and limitations, and decision
  outcomes.
- Decision section with `deeper code review`, `targeted tests`,
  `telemetry question`, and `owner follow-up`.
- Artifact-boundary section separating public-safe summaries and reviewed
  reports from local-only scanner artifacts and private review material.
- Claim-safe language section with safe wording, red flags, and escalation
  rules.
- Related links section to `/use-cases/`, `/evidence/`, `/proof-paths/`,
  `/validation/`, `/limitations/`, `/review-room/`, `/static-triage/`, and
  `/demo/runbook/`.

## Copy Model

The page should make review feel practical without sounding accusatory. Good
framing: "this endpoint deserves review because the static packet shows
coupling, dependency surfaces, gaps, or repeated review friction." Bad framing:
"this endpoint is trash," "the team missed this," "the vendor caused this,"
"TraceMap proved the endpoint is unsafe," or any runtime or release claim.

Examples should be authored concept examples unless a future spec supplies
checked-in public proof. Authored examples should use neutral names and
placeholders. They can illustrate a packet shape, but they cannot invent a real
finding for a real endpoint.

The bad-framing examples above are spec guidance only. On the rendered page,
describe these failure modes, such as avoiding scare framing or blame, rather
than quoting exact rejected phrases. That keeps the focused validator's
forbidden-phrase checks satisfiable while still teaching the boundary.

## Evidence Model

The future page should explain that endpoint-adjacent evidence is useful only
when it stays attached to:

- Rule IDs.
- Evidence tiers.
- Public-safe file paths and line spans when available.
- Commit/source context and extractor versions when available.
- Coverage labels.
- Documented limitations.
- Gaps that remain open.

The page may describe static coupling, package surfaces, config surfaces, and
SQL-facing surfaces as review cues. It must not describe them as runtime
traffic, production topology, endpoint performance, incident cause, release
approval, operational safety, or complete dependency coverage.

## Validation Shape

Future implementation should add a focused rendered-output validator using the
site's existing validator pattern. The validator should check:

- The route renders at `/use-cases/endpoint-review/`.
- Required labels and exact phrases are present.
- Required evidence-dimension terms are present.
- Required related links resolve.
- Discovery metadata and sitemap metadata include the route.
- Unsupported runtime, production, release, operational, endpoint-performance,
  AI/LLM, team-shaming, vendor-blaming, and scare-framing claims are absent.
- Artifact-family names appear in rendered page copy only in sanctioned
  artifact-boundary or red-flag sections, and appear in discovery output only in
  `nonClaims`.
- Pattern-detectable raw or private text is absent from rendered HTML,
  metadata, and discovery output.

The validator should have companion tests with positive and negative fixtures.
Private-text negative fixtures should be assembled at runtime so checked-in test
files do not introduce local absolute path literals or known-private tokens.

Note: the sibling `/use-cases/incident-review/` page is currently covered by
aggregate `validate.mjs` route and link checks rather than a dedicated focused
validator. This spec deliberately prefers a dedicated `endpoint-review.mjs` for
the page's claim-safety, artifact-boundary, and overclaim assertions. If the
future implementer instead extends only the aggregate validator, record that
decision in `implementation-state.md`.

## Metadata Shape

Future implementation should add:

- `site/src/_site/pages.json` entry for `/use-cases/endpoint-review/`.
- `site/src/_site/discovery.json` entry with
  `path: "/use-cases/endpoint-review/"`, `publicClaimLevel: "concept"`,
  `sourceType: "site-page"`, `hintCategory: "use-case"`, non-empty `summary`,
  `limitations`, and `nonClaims`, and a public `preferredProofPath`.
- Page metadata and social metadata that describe a concept-level endpoint
  review playbook without runtime, production, release, operational,
  endpoint-performance, or AI/LLM claims.

Discovery `nonClaims` may name denied categories and artifact-family names when
needed to state what the page does not claim or publish. Discovery `title`,
`summary`, `limitations`, and proof path fields should stay free of
artifact-family names and denied-positioning vocabulary unless an existing site
schema requires a different placement and the implementation records that
decision.
Discovery `title`, `summary`, and `limitations` must also avoid non-shipped
availability wording such as `available`, `shipped`, `released`, and
`deployed`.

## Non-Goals

- Do not implement site code in this spec phase.
- Do not introduce a runtime service, client-side data fetch, analytics
  dependency, generated evidence artifact, or new scanner behavior.
- Do not publish raw facts, raw SQLite, analyzer logs, raw source snippets, raw
  SQL, config values, secrets, local absolute paths, raw remotes, generated
  scan directories, or private sample names.
- Do not claim runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- Do not present static evidence as a replacement for code review, tests,
  telemetry, ownership decisions, incident response, or release policy.
- Do not shame teams, blame consultants or vendors, or use scare framing.
