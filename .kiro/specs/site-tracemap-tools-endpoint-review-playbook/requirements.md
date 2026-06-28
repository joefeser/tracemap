# Site TraceMap Tools Endpoint Review Playbook Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe site page at `/use-cases/endpoint-review/` for
engineers reviewing an endpoint that has enough static evidence to deserve
careful human review. The page should show how TraceMap evidence can help an
engineer inspect endpoint-adjacent static paths, package surfaces, config
surfaces, SQL-facing surfaces, coverage labels, and limitations before deciding
what needs deeper code review, tests, telemetry, or owner follow-up.

This is a spec-only site phase. It does not implement site code. The future page
is a professional review playbook, not a public diagnosis of any real endpoint.
It must not shame teams, blame consultants or vendors, or use scare framing such
as "this endpoint is trash."

## Shared Site Principle

No public conclusion without evidence.

## Public Claim Boundary

The page uses `Public claim level: concept` because this spec defines a review
workflow and safe language for a future public page. It does not anchor a
specific endpoint conclusion to checked-in public demo proof paths. A future
implementation may link to public demo or proof routes for evidence-model
orientation, but it must not claim that a specific endpoint is risky, messy,
broken, slow, heavily used, or safe unless a public-safe evidence packet with
rule IDs, evidence tiers, coverage labels, limitations, and source context
supports that exact claim.

The page may say static evidence can help reviewers decide where to inspect
next. It must not claim runtime behavior, production traffic, endpoint
performance, outage cause, release safety, operational safety, AI impact
analysis, LLM analysis, or complete product coverage.

## Relationship to Existing Public Surfaces

The playbook should connect to existing site surfaces without changing their
claim levels:

- `/evidence/` for rule IDs, evidence tiers, file spans, coverage labels, and
  the shared evidence model.
- `/proof-paths/` for examples of how public claims stay attached to proof
  paths.
- `/validation/` for deterministic checks and generated-output guardrails.
- `/limitations/` for non-claims and partial-coverage boundaries.
- `/review-room/` for meeting-agenda framing around known, partial, and missing
  evidence.
- `/static-triage/` for static triage orientation when a reader needs a broader
  review context.
- `/demo/runbook/` or `/demo/start-here/` only as public demo orientation, not
  as endpoint-specific proof for this page.

When a future implementation needs proof details, it must link to existing
public routes or public-safe repository sources rather than restating raw
generated internals.

## Requirements

### Requirement 1: Publish a bounded endpoint review playbook

The future implementation shall publish a concept-level endpoint review
playbook at `/use-cases/endpoint-review/`.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states `No public conclusion without evidence`.
- The page frames the endpoint as a `review candidate`, `endpoint review
  candidate`, or equivalent professional wording, not as broken, bad, trash, a
  root cause, a production danger, or a team failure.
- The page explains that TraceMap evidence can show static coupling,
  dependency surfaces, gaps, or repeated review friction only when those claims
  are backed by public-safe evidence.
- The page describes the workflow as a pre-review inspection aid for engineers,
  not a release gate, runtime diagnostic, operational safety check,
  performance investigation, incident review, owner assignment system, or
  product-coverage guarantee.
- The page reuses existing static site layout and top-navigation patterns.
  `/use-cases/endpoint-review/` is not added to the canonical top navigation
  unless the site already uses a comparable pattern for all use-case pages.
- The implementation records the final route, source files, validation, browser
  checks, review findings, and follow-ups in this spec's
  `implementation-state.md`.

### Requirement 2: Explain the static endpoint evidence packet

The future page shall define the public-safe evidence packet an engineer should
inspect before drawing review conclusions.

Acceptance criteria:

- The page includes a deterministic, validator-checkable line:
  `Endpoint review starts with static evidence, not certainty.`
- The page names the evidence dimensions `endpoint-adjacent static paths`,
  `packages`, `config surfaces`, `SQL-facing surfaces`, `coverage labels`, and
  `limitations`.
- The page explains that review-worthy signals may include static coupling,
  dependency surfaces, analysis gaps, or repeated review friction, but only as
  cues for human inspection.
- The page tells readers to verify rule IDs before repeating any conclusion.
- The page tells readers to verify evidence tiers such as `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- The page tells readers to verify file paths, line spans, commit/source
  context, extractor versions, coverage labels, and limitations when those
  fields are available in a public-safe packet.
- The page keeps gap labels visible; it must not rewrite partial or unknown
  coverage as complete evidence.
- The page avoids source snippets by default. If examples need source context,
  they use public-safe file path and line-span descriptions or snippet hashes,
  not raw private source.

### Requirement 3: Provide a professional endpoint review workflow

The future page shall show a concrete review workflow for deciding what needs
deeper review after inspecting static evidence.

Acceptance criteria:

- The workflow includes a first step that states the review question in bounded
  language, such as whether an endpoint deserves deeper code review because a
  static packet shows coupling, dependency surfaces, gaps, or repeated review
  friction.
- The workflow includes a step for listing endpoint-adjacent static paths and
  separating direct, structural, and syntax-only evidence.
- The workflow includes a step for inspecting package and framework surfaces
  without implying package runtime behavior or deployed usage.
- The workflow includes a step for inspecting config surfaces without
  publishing config values or secrets.
- The workflow includes a step for inspecting SQL-facing surfaces without
  publishing raw SQL, connection strings, credentials, table dumps, or database
  contents.
- The workflow includes a step for reading coverage labels and limitations
  before deciding whether the packet supports a conclusion, a gap, or a
  follow-up question.
- The workflow includes a decision section with at least these outcomes:
  `deeper code review`, `targeted tests`, `telemetry question`, and
  `owner follow-up`.
- The workflow states that telemetry questions require runtime data outside
  TraceMap's static packet and must not be presented as already proven by
  TraceMap.
- The workflow includes a stop condition: if a conclusion lacks rule IDs,
  evidence tiers, coverage labels, limitations, source context, or a public-safe
  summary, the page must route the reader to `/limitations/` or a gap outcome
  instead of publishing the conclusion.

### Requirement 4: Keep examples authored, sanitized, and evidence-bounded

The future page shall use authored or public-demo-oriented examples without
publishing private endpoint data or unsupported endpoint claims.

Acceptance criteria:

- Any example packet is explicitly labeled as authored concept copy unless it
  is tied to a checked-in public-safe demo source.
- Authored examples use neutral names such as `Endpoint A`, `Review packet`,
  `Package surface`, and `Config surface`.
- If a future implementation promotes any example to `demo` claim level, it
  requires a spec amendment or follow-up spec that names the public proof paths
  and validation gates.
- Examples may show rule-ID-shaped placeholders such as `<rule-id>` and
  evidence-tier names, but must not invent a real rule result for a real
  endpoint.
- Examples must not say an endpoint is impacted unless a deterministic reducer
  output and public-safe evidence row support that exact bounded statement.
- Examples must not claim the endpoint is slow, highly trafficked, broken,
  causing an outage, safe to release, unsafe to release, operationally safe, or
  fully covered.

### Requirement 5: Preserve public-safe artifact boundaries

The future page shall distinguish public-safe summaries from local-only scanner
artifacts and private review material.

Acceptance criteria:

- The page may mention public-safe summaries, reviewed reports, proof paths,
  rule IDs, evidence tiers, file paths, line spans, snippet hashes, commit/source
  context, extractor versions, coverage labels, and limitations as shareable
  only when they are derived from checked-in public or explicitly reviewed
  public-safe material.
- The page must not publish raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw remotes,
  generated scan directories, or private sample names.
- Artifact-family names such as `facts.ndjson`, `index.sqlite`, `report.md`,
  `scan-manifest.json`, and `logs/analyzer.log` may appear only inside a
  sanctioned artifact-boundary or red-flag section explaining what stays local.
- The page must not link directly to raw generated output directories or
  machine-local artifact paths.
- If command examples are needed, they use neutral placeholders such as
  `<repo>` and `<ignored-output-dir>` instead of workstation-specific paths.
- Public copy, metadata, discovery entries, tests, and implementation-state
  notes must avoid local absolute paths, raw remotes, secrets, raw generated
  artifacts, and private sample names.

### Requirement 6: Add claim-safe language and non-claims

The future page shall give engineers copy-safe language for discussing an
endpoint review candidate without overstating the evidence.

Acceptance criteria:

- The page includes safe wording patterns such as `static evidence suggests a
  review candidate`, `rule ID <rule-id>, Tier2Structural, partial coverage`,
  and `gap-labeled packet: review question remains open`.
- The page includes forbidden wording or red flags for runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI impact analysis, LLM analysis, complete product
  coverage, team blame, vendor blame, and scare framing.
- The page says static evidence can route inspection, but cannot approve a
  release, diagnose a production outage, prove endpoint performance, prove
  production usage, assign fault, or certify operational safety.
- The page includes an escalation rule: when a claim requires runtime telemetry,
  customer traffic, production deployment facts, incident context, ownership
  authority, or release policy, link to `/limitations/` or route the question
  to the appropriate human review process instead of making the claim.
- The page uses the phrase `deserves review` only in relation to evidence-backed
  review cues, not as a moral judgment about teams, vendors, or code quality.

### Requirement 7: Integrate with discovery, sitemap, and related pages

The future implementation shall make the playbook discoverable from relevant
public site surfaces without weakening claim boundaries.

Acceptance criteria:

- Add `/use-cases/endpoint-review/` to `site/src/_site/pages.json` with `path`,
  `changefreq`, and `priority`.
- Add `/use-cases/endpoint-review/` to `site/src/_site/discovery.json` with
  `path: "/use-cases/endpoint-review/"`, `sourceType: "site-page"`,
  `hintCategory: "use-case"`,
  `publicClaimLevel: "concept"`, non-empty `title`, non-empty `summary`,
  non-empty `limitations`, non-empty `nonClaims`, and a
  `preferredProofPath` that resolves to an existing public route such as
  `/proof-paths/`.
- Discovery `title` and `summary` must describe a concept-level endpoint review
  playbook and must not contain artifact-family names, AI/LLM positioning,
  private tokens, runtime claims, production claims, release claims, or endpoint
  performance claims.
- Discovery `title`, `summary`, and `limitations` must avoid
  shipped/availability wording such as `available`, `shipped`, `released`, and
  `deployed`, consistent with non-shipped claim-level validation.
- Discovery denied-phrase vocabulary, including artifact-family names such as
  `facts.ndjson`, `index.sqlite`, and `logs/analyzer.log`, may appear only in
  `nonClaims` when needed to state what the page does not claim or publish,
  consistent with existing `discovery.json` convention. They must not appear in
  `title`, `summary`, `limitations`, or `preferredProofPath`.
- Link from `/use-cases/` to `/use-cases/endpoint-review/`.
- Link from `/use-cases/endpoint-review/` to `/use-cases/`, `/evidence/`,
  `/proof-paths/`, `/validation/`, `/limitations/`, `/review-room/`,
  `/static-triage/`, and `/demo/runbook/` as the pinned public demo
  orientation route. If a different demo route is chosen in a future spec
  amendment, update this list and the focused validator list together.
- Add minimal cross-links to `/use-cases/endpoint-review/` from `/review-room/`
  and `/static-triage/` if those pages still exist at implementation time.
- Cross-links must not imply runtime proof, release safety, operational safety,
  endpoint performance proof, outage cause, or complete coverage.
- Verify generated `sitemap.xml` includes `/use-cases/endpoint-review/`.

### Requirement 8: Validate the implementation phase

The future implementation shall add focused validation for the endpoint review
playbook and run the normal site checks.

Acceptance criteria:

- Add a focused rendered-output validator following existing page-validator
  patterns, such as `site/scripts/endpoint-review.mjs` exporting
  `validateEndpointReviewDist`, unless the site convention changes before
  implementation.
- Wire the focused validator into aggregate site validation so
  `npm run validate` exercises it.
- Add companion tests covering pass and fail cases for required labels,
  required links, discovery metadata, artifact boundaries, forbidden private
  text, forbidden overclaims, and unsupported endpoint conclusions.
- Compose private-text fail-case fixtures at runtime instead of embedding local
  absolute path literals or private tokens in checked-in tests.
- Validation checks assert these exact rendered phrases:
  `Public claim level: concept`, `No public conclusion without evidence`, and
  `Endpoint review starts with static evidence, not certainty.`
- Validation checks assert the required evidence-dimension terms:
  `endpoint-adjacent static paths`, `packages`, `config surfaces`,
  `SQL-facing surfaces`, `coverage labels`, and `limitations`.
- Validation checks assert required internal links to `/evidence/`,
  `/proof-paths/`, `/validation/`, `/limitations/`, `/review-room/`,
  `/static-triage/`, `/demo/runbook/`, and `/use-cases/`.
- Validation rejects affirmative unsupported endpoint conclusions, such as copy
  asserting an endpoint is broken, slow, high-traffic, causing an outage,
  release-safe, operationally safe, fully covered, or analyzed by AI/LLM
  methods. Validation must use affirmative-claim patterns, not bare keyword
  matching, so negated non-claims and escalation statements remain allowed in
  sanctioned sections.
- Validation rejects team-shaming, vendor-blaming, and scare-framing language.
  The rendered page must describe prohibited framings rather than reproduce
  them verbatim; the literal phrase `this endpoint is trash` must not appear in
  rendered copy.
- Boundary vocabulary for overclaims, blame, and scare framing may appear only
  inside sanctioned claim-safe language, red-flag, or non-claims sections, the
  same way artifact-family names are confined to artifact-boundary or red-flag
  sections.
- Validation allows artifact-family names in discovery output only inside the
  `nonClaims` field, mirroring the page-copy artifact-boundary and red-flag
  carve-out. It rejects them in discovery `title`, `summary`, `limitations`, and
  `preferredProofPath`. In rendered page copy, artifact-family names remain
  allowed only in sanctioned artifact-boundary or red-flag sections.
- Validation rejects pattern-detectable raw or private content everywhere while
  preserving the sanctioned artifact-family carve-outs above. Raw generated
  artifact contents, local output roots, private paths, source snippets, SQL
  text, connection strings, credentials, and remotes are never allowed.
- Validation checks at minimum for `.ndjson` file references, `.sqlite` file
  references, `analyzer.log`, `/Users/`, `/home/`, `C:\Users\`, `file://`,
  `localhost`, `127.0.0.1`, raw repository remote patterns, connection-string
  fragments, and generated scan-directory references in rendered page copy,
  metadata, and discovery output, applying the artifact-family carve-outs for
  sanctioned page sections and discovery `nonClaims`.
- Delegate non-generic checks for private sample names and raw source snippets
  to `./scripts/check-private-paths.sh`, authoring review, or explicit
  deny-lists rather than attempting open-ended detection.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks for layout changes.
- Run `git diff --check`.
- Run `git diff --cached --check` after staging implementation changes.
- Run `./scripts/check-private-paths.sh`.
- Record commands, results, review findings, oddities, and follow-ups in this
  spec's `implementation-state.md`.
