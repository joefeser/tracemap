# Site TraceMap Tools Endpoint Review Playbook Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are future implementation work. They remain unchecked because this
phase is spec-only.

Note: validation tasks must pass before future implementation tasks are checked
complete.

- [ ] Add `/use-cases/endpoint-review/` using existing static site layout
  patterns.
- [ ] Include the canonical site top navigation using the existing top-nav link
  set unless the site's use-case-page convention changes before
  implementation.
- [ ] Add page-level copy that says `Public claim level: concept`.
- [ ] State `No public conclusion without evidence`.
- [ ] Include the exact line `Endpoint review starts with static evidence, not
  certainty.`
- [ ] Frame the page as an engineer playbook for a professional endpoint review
  candidate, not as a production diagnostic, release gate, incident analysis,
  runtime verification guide, endpoint performance page, or product coverage
  claim.
- [ ] Avoid team-shaming, vendor-blaming, consultant-blaming, and scare-framing
  language. Do not reproduce the literal rejected phrase `this endpoint is
  trash` in rendered copy.
- [ ] Add an evidence packet section naming `endpoint-adjacent static paths`,
  `packages`, `config surfaces`, `SQL-facing surfaces`, `coverage labels`, and
  `limitations`.
- [ ] Explain that static coupling, dependency surfaces, analysis gaps, and
  repeated review friction are review cues only when backed by public-safe
  evidence.
- [ ] Require readers to verify rule IDs before repeating any conclusion.
- [ ] Require readers to verify evidence tiers such as `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- [ ] Require readers to verify file paths, line spans, commit/source context,
  extractor versions, coverage labels, and limitations where available in a
  public-safe packet.
- [ ] Keep gap labels visible instead of rewriting partial or unknown coverage
  as complete evidence.
- [ ] Add a bounded review-question step that asks whether the endpoint
  deserves deeper review because a static packet shows coupling, dependency
  surfaces, gaps, or repeated review friction.
- [ ] Add a static-paths step that separates direct, structural, and syntax-only
  evidence.
- [ ] Add a package/framework surface step that avoids runtime or deployed-usage
  claims.
- [ ] Add a config-surface step that does not publish config values or secrets.
- [ ] Add a SQL-facing-surface step that does not publish raw SQL, connection
  strings, credentials, table dumps, or database contents.
- [ ] Add a coverage-and-limitations step before any conclusion or follow-up
  decision.
- [ ] Add a decision section with `deeper code review`, `targeted tests`,
  `telemetry question`, and `owner follow-up`.
- [ ] State that telemetry questions require runtime data outside TraceMap's
  static packet and are not proven by TraceMap.
- [ ] Add a stop condition for missing public-safe summaries, missing rule IDs,
  missing evidence tiers, missing coverage labels, missing limitations, missing
  source context, or forbidden private material.
- [ ] Route unsupported conclusions to `/limitations/` or a gap outcome.
- [ ] Use authored concept examples with neutral labels unless a future spec
  provides checked-in public proof.
- [ ] If an example is promoted to demo-level proof, require a spec amendment or
  follow-up spec that names the public proof paths and validation gates.
- [ ] Avoid saying an endpoint is impacted unless deterministic reducer output
  and public-safe evidence support that exact bounded statement.
- [ ] Avoid claims that an endpoint is slow, high-traffic, broken, causing an
  outage, safe to release, unsafe to release, operationally safe, or fully
  covered.
- [ ] Add an artifact-boundary section that distinguishes public-safe summaries
  and reviewed reports from local-only scanner artifacts and private review
  material.
- [ ] Keep raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, and private sample names out of the page, metadata, discovery
  entries, tests, and implementation-state notes.
- [ ] Mention artifact-family names such as `facts.ndjson`, `index.sqlite`,
  `report.md`, `scan-manifest.json`, and `logs/analyzer.log` only inside
  sanctioned artifact-boundary or red-flag sections.
- [ ] Use neutral command placeholders such as `<repo>` and
  `<ignored-output-dir>` if command examples are needed.
- [ ] Add safe wording patterns including `static evidence suggests a review
  candidate`, `rule ID <rule-id>, Tier2Structural, partial coverage`, and
  `gap-labeled packet: review question remains open`.
- [ ] Add red-flag wording for runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, complete product coverage, team blame, vendor blame,
  and scare framing.
- [ ] Add an escalation rule for claims requiring runtime telemetry, customer
  traffic, production deployment facts, incident context, ownership authority,
  or release policy.
- [ ] Link the page to `/use-cases/`, `/evidence/`, `/proof-paths/`,
  `/validation/`, `/limitations/`, `/review-room/`, `/static-triage/`, and
  `/demo/runbook/` as the pinned public demo orientation route.
- [ ] Link from `/use-cases/` to `/use-cases/endpoint-review/`.
- [ ] Add minimal cross-links to `/use-cases/endpoint-review/` from
  `/review-room/` and `/static-triage/` if those pages still exist at
  implementation time.
- [ ] Add `/use-cases/endpoint-review/` to `site/src/_site/pages.json` with
  `path`, `changefreq`, and `priority`.
- [ ] Verify generated `sitemap.xml` includes `/use-cases/endpoint-review/`.
- [ ] Add `/use-cases/endpoint-review/` to `site/src/_site/discovery.json` with
  `sourceType: "site-page"`, `hintCategory: "use-case"`,
  `publicClaimLevel: "concept"`, non-empty `title`, `summary`, `limitations`,
  and `nonClaims`, plus a public `preferredProofPath`.
- [ ] Keep discovery `title` and `summary` free of artifact-family names,
  AI/LLM positioning, private tokens, runtime claims, production claims,
  release claims, and endpoint performance claims.
- [ ] Keep discovery `title`, `summary`, and `limitations` free of
  shipped/availability wording such as `available`, `shipped`, `released`, and
  `deployed`.
- [ ] Keep denied-phrase vocabulary and artifact-family names such as
  `facts.ndjson`, `index.sqlite`, and `logs/analyzer.log` in discovery
  `nonClaims` only when needed to state what the page does not claim or
  publish. Keep them out of discovery `title`, `summary`, `limitations`, and
  `preferredProofPath`.
- [ ] Add a focused rendered-output validator such as
  `site/scripts/endpoint-review.mjs` exporting `validateEndpointReviewDist`,
  unless the naming convention changes before implementation.
- [ ] Wire the focused validator into aggregate site validation so
  `npm run validate` exercises it.
- [ ] Add companion tests covering pass and fail cases for required labels,
  required links, discovery metadata, artifact boundaries, forbidden private
  text, forbidden overclaims, unsupported endpoint conclusions, team-shaming,
  vendor-blaming, and scare framing.
- [ ] Compose private-text fail-case fixtures at runtime instead of embedding
  local absolute path literals or private tokens in checked-in tests.
- [ ] Validate exact rendered phrases: `Public claim level: concept`,
  `No public conclusion without evidence`, and `Endpoint review starts with
  static evidence, not certainty.`
- [ ] Validate required evidence-dimension terms:
  `endpoint-adjacent static paths`, `packages`, `config surfaces`,
  `SQL-facing surfaces`, `coverage labels`, and `limitations`.
- [ ] Validate required internal links to `/evidence/`, `/proof-paths/`,
  `/validation/`, `/limitations/`, `/review-room/`, `/static-triage/`,
  `/demo/runbook/`, and `/use-cases/`.
- [ ] Validate that affirmative unsupported endpoint conclusions are rejected,
  including broken endpoint, slow endpoint, high-traffic endpoint, outage cause,
  release safety, operational safety, full coverage, and AI/LLM analysis
  claims. Use affirmative-claim patterns rather than bare keyword matching so
  negated non-claims and escalation statements remain allowed in sanctioned
  sections.
- [ ] Validate that rendered copy rejects team-shaming, vendor-blaming, and
  scare-framing language, including the literal rejected phrase
  `this endpoint is trash`.
- [ ] Allow boundary vocabulary for overclaims, blame, and scare framing only
  inside sanctioned claim-safe language, red-flag, or non-claims sections.
- [ ] Validate that artifact-family names are allowed in rendered page copy only
  inside sanctioned artifact-boundary or red-flag sections, and in discovery
  output only inside the `nonClaims` field.
- [ ] Validate that pattern-detectable raw or private content is rejected in
  rendered page copy, metadata, and discovery output.
- [ ] Validate at minimum `.ndjson` file references, `.sqlite` file references,
  `analyzer.log`, `/Users/`, `/home/`, `C:\Users\`, `file://`, `localhost`,
  `127.0.0.1`, raw repository remote patterns, connection-string fragments,
  and generated scan-directory references.
- [ ] Delegate non-generic checks for private sample names and raw source
  snippets to `./scripts/check-private-paths.sh`, authoring review, or explicit
  deny-lists.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for layout changes.
- [ ] Run `git diff --check`.
- [ ] Run `git diff --cached --check` after staging implementation changes.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route, scope, validation, browser
  checks, review findings, oddities, and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite paths, analyzer log content, generated scan
  directory paths, and private sample names.
