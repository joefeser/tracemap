# Site TraceMap Tools Endpoint Review Playbook Tasks

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

Implementation completed on branch `codex/impl-site-endpoint-review-playbook`.
Validation tasks passed before implementation tasks were checked complete.

- [x] Add `/use-cases/endpoint-review/` using existing static site layout
  patterns.
- [x] Include the canonical site top navigation using the existing top-nav link
  set unless the site's use-case-page convention changes before
  implementation.
- [x] Add page-level copy that says `Public claim level: concept`.
- [x] State `No public conclusion without evidence`.
- [x] Include the exact line `Endpoint review starts with static evidence, not
  certainty.`
- [x] Frame the page as an engineer playbook for a professional endpoint review
  candidate, not as a production diagnostic, release gate, incident analysis,
  runtime verification guide, endpoint performance page, or product coverage
  claim.
- [x] Avoid team-shaming, vendor-blaming, consultant-blaming, and scare-framing
  language. Do not reproduce the literal rejected phrase `this endpoint is
  trash` in rendered copy.
- [x] Add an evidence packet section naming `endpoint-adjacent static paths`,
  `packages`, `config surfaces`, `SQL-facing surfaces`, `coverage labels`, and
  `limitations`.
- [x] Explain that static coupling, dependency surfaces, analysis gaps, and
  repeated review friction are review cues only when backed by public-safe
  evidence.
- [x] Require readers to verify rule IDs before repeating any conclusion.
- [x] Require readers to verify evidence tiers such as `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- [x] Require readers to verify file paths, line spans, commit/source context,
  extractor versions, coverage labels, and limitations where available in a
  public-safe packet.
- [x] Keep gap labels visible instead of rewriting partial or unknown coverage
  as complete evidence.
- [x] Add a bounded review-question step that asks whether the endpoint
  deserves deeper review because a static packet shows coupling, dependency
  surfaces, gaps, or repeated review friction.
- [x] Add a static-paths step that separates direct, structural, and syntax-only
  evidence.
- [x] Add a package/framework surface step that avoids runtime or deployed-usage
  claims.
- [x] Add a config-surface step that does not publish config values or secrets.
- [x] Add a SQL-facing-surface step that does not publish raw SQL, connection
  strings, credentials, table dumps, or database contents.
- [x] Add a coverage-and-limitations step before any conclusion or follow-up
  decision.
- [x] Add a decision section with `deeper code review`, `targeted tests`,
  `telemetry question`, and `owner follow-up`.
- [x] State that telemetry questions require runtime data outside TraceMap's
  static packet and are not proven by TraceMap.
- [x] Add a stop condition for missing public-safe summaries, missing rule IDs,
  missing evidence tiers, missing coverage labels, missing limitations, missing
  source context, or forbidden private material.
- [x] Route unsupported conclusions to `/limitations/` or a gap outcome.
- [x] Use authored concept examples with neutral labels unless a future spec
  provides checked-in public proof.
- [x] If an example is promoted to demo-level proof, require a spec amendment or
  follow-up spec that names the public proof paths and validation gates.
- [x] Avoid saying an endpoint is impacted unless deterministic reducer output
  and public-safe evidence support that exact bounded statement.
- [x] Avoid claims that an endpoint is slow, high-traffic, broken, causing an
  outage, safe to release, unsafe to release, operationally safe, or fully
  covered.
- [x] Add an artifact-boundary section that distinguishes public-safe summaries
  and reviewed reports from local-only scanner artifacts and private review
  material.
- [x] Keep raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, and private sample names out of the page, metadata, discovery
  entries, tests, and implementation-state notes.
- [x] Mention artifact-family names such as `facts.ndjson`, `index.sqlite`,
  `report.md`, `scan-manifest.json`, and `logs/analyzer.log` only inside
  sanctioned artifact-boundary or red-flag sections.
- [x] Use neutral command placeholders such as `<repo>` and
  `<ignored-output-dir>` if command examples are needed.
- [x] Add safe wording patterns including `static evidence suggests a review
  candidate`, `rule ID <rule-id>, Tier2Structural, partial coverage`, and
  `gap-labeled packet: review question remains open`.
- [x] Add red-flag wording for runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, complete product coverage, team blame, vendor blame,
  and scare framing.
- [x] Add an escalation rule for claims requiring runtime telemetry, customer
  traffic, production deployment facts, incident context, ownership authority,
  or release policy.
- [x] Link the page to `/use-cases/`, `/evidence/`, `/proof-paths/`,
  `/validation/`, `/limitations/`, `/review-room/`, `/static-triage/`, and
  `/demo/runbook/` as the pinned public demo orientation route.
- [x] Link from `/use-cases/` to `/use-cases/endpoint-review/`.
- [x] Add minimal cross-links to `/use-cases/endpoint-review/` from
  `/review-room/` and `/static-triage/` if those pages still exist at
  implementation time.
- [x] Add `/use-cases/endpoint-review/` to `site/src/_site/pages.json` with
  `path`, `changefreq`, and `priority`.
- [x] Verify generated `sitemap.xml` includes `/use-cases/endpoint-review/`.
- [x] Add `/use-cases/endpoint-review/` to `site/src/_site/discovery.json` with
  `path: "/use-cases/endpoint-review/"`, `sourceType: "site-page"`,
  `hintCategory: "use-case"`, `publicClaimLevel: "concept"`, non-empty
  `title`, `summary`, `limitations`, and `nonClaims`, plus a public
  `preferredProofPath`.
- [x] Keep discovery `title` and `summary` free of artifact-family names,
  AI/LLM positioning, private tokens, runtime claims, production claims,
  release claims, and endpoint performance claims.
- [x] Keep discovery `title`, `summary`, and `limitations` free of
  shipped/availability wording such as `available`, `shipped`, `released`, and
  `deployed`.
- [x] Keep denied-phrase vocabulary and artifact-family names such as
  `facts.ndjson`, `index.sqlite`, and `logs/analyzer.log` in discovery
  `nonClaims` only when needed to state what the page does not claim or
  publish. Keep them out of discovery `title`, `summary`, `limitations`, and
  `preferredProofPath`.
- [x] Add a focused rendered-output validator such as
  `site/scripts/endpoint-review.mjs` exporting `validateEndpointReviewDist`,
  unless the naming convention changes before implementation.
- [x] Wire the focused validator into aggregate site validation so
  `npm run validate` exercises it.
- [x] Add companion tests covering pass and fail cases for required labels,
  required links, discovery metadata, artifact boundaries, forbidden private
  text, forbidden overclaims, unsupported endpoint conclusions, team-shaming,
  vendor-blaming, and scare framing.
- [x] Compose private-text fail-case fixtures at runtime instead of embedding
  local absolute path literals or private tokens in checked-in tests.
- [x] Validate exact rendered phrases: `Public claim level: concept`,
  `No public conclusion without evidence`, and `Endpoint review starts with
  static evidence, not certainty.`
- [x] Validate required evidence-dimension terms:
  `endpoint-adjacent static paths`, `packages`, `config surfaces`,
  `SQL-facing surfaces`, `coverage labels`, and `limitations`.
- [x] Validate required internal links to `/evidence/`, `/proof-paths/`,
  `/validation/`, `/limitations/`, `/review-room/`, `/static-triage/`,
  `/demo/runbook/`, and `/use-cases/`.
- [x] Validate that affirmative unsupported endpoint conclusions are rejected,
  including broken endpoint, slow endpoint, high-traffic endpoint, outage cause,
  release safety, operational safety, full coverage, and AI/LLM analysis
  claims. Use affirmative-claim patterns rather than bare keyword matching so
  negated non-claims and escalation statements remain allowed in sanctioned
  sections.
- [x] Validate that rendered copy rejects team-shaming, vendor-blaming, and
  scare-framing language, including the literal rejected phrase
  `this endpoint is trash`.
- [x] Allow boundary vocabulary for overclaims, blame, and scare framing only
  inside sanctioned claim-safe language, red-flag, or non-claims sections.
- [x] Validate that artifact-family names are allowed in rendered page copy only
  inside sanctioned artifact-boundary or red-flag sections, and in discovery
  output only inside the `nonClaims` field.
- [x] Validate that pattern-detectable raw or private content is rejected in
  rendered page copy, metadata, and discovery output.
- [x] Validate at minimum `.ndjson` file references, `.sqlite` file references,
  `analyzer.log`, `/Users/`, `/home/`, `C:\Users\`, `file://`, `localhost`,
  `127.0.0.1`, raw repository remote patterns, connection-string fragments,
  and generated scan-directory references, applying the artifact-family
  carve-outs for sanctioned page sections and discovery `nonClaims`.
- [x] Delegate non-generic checks for private sample names and raw source
  snippets to `./scripts/check-private-paths.sh`, authoring review, or explicit
  deny-lists.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for layout changes.
- [x] Run `git diff --check`.
- [x] Run `git diff --cached --check` after staging implementation changes.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with route, scope, validation, browser
  checks, review findings, oddities, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite paths, analyzer log content, generated scan
  directory paths, and private sample names.
