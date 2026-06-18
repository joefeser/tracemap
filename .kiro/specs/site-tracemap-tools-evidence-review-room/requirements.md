# Site TraceMap Tools Evidence Review Room Requirements

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Summary

Add a public site concept phase for an evidence review-room page at
`/review-room/`. The page should serve managers, reviewers, architects, and
engineers in a review meeting who need a bounded agenda for deciding what
static dependency evidence is known, partial, or missing.

This is a static evidence agenda story. It is not runtime behavior proof,
production traffic proof, endpoint performance analysis, outage diagnosis,
release approval, operational safety, AI impact analysis, or complete product
coverage.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded review-room concept route

The site shall publish a concept-level evidence review-room page at the stable
public route `/review-room/`.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states the shared site principle.
- The primary copy addresses managers, reviewers, architects, and engineers in
  a review meeting who need to decide what static dependency evidence is known,
  partial, or missing.
- The page describes TraceMap as a bounded evidence agenda for review
  conversation, not as an automated approval or runtime system.
- The page uses future-facing concept language and does not imply the review
  room is a shipped workflow beyond the public concept page.

### Requirement 2: Explain the bounded evidence agenda

The page shall show the meeting agenda TraceMap can support without overstating
what static analysis can prove.

Acceptance criteria:

- The page includes the agenda items `claim`, `proof path`, `rule ID/evidence
  tier`, `coverage label`, `limitation`, and `owner decision gap`.
- The page includes a deterministic, validator-checkable line:
  `Known evidence is reducer-backed and public-safe; partial evidence is
  reduced-coverage and labeled; missing evidence is an explicit gap for human
  review.`
- The page explains that each claim should stay attached to a proof path,
  rule ID, evidence tier, coverage label, limitation, and the remaining human
  decision.
- The page distinguishes known evidence, partial or reduced evidence, and
  missing evidence.
- The page avoids saying a surface is impacted unless a reducer-backed result
  and public-safe evidence are present.

### Requirement 3: Preserve static evidence boundaries

The page shall keep public claims bounded to deterministic TraceMap evidence
and clearly state non-claims.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI or LLM
  impact analysis, or complete product coverage.
- The page says TraceMap can help a meeting inspect static dependency
  evidence, rule IDs, evidence tiers, coverage labels, limitations, proof-path
  links, generated summaries, file paths, line spans, commit SHA, and extractor
  versions when those are available in public-safe summaries.
- The page clearly marks partial and missing evidence as gaps that require
  human review instead of clean or complete evidence.
- The page does not imply TraceMap replaces source review, ownership decisions,
  test results, telemetry, logs, traces, incident response, or release
  approval.

### Requirement 4: Publish only public-safe meeting material

The page shall publish public-safe agenda copy and links, not raw or private
scanner artifacts.

Acceptance criteria:

- The page and metadata do not publish raw `facts.ndjson`, `index.sqlite`,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  paths, raw remotes, generated scan directories, or private sample names.
- Public examples are authored agenda examples or derived from public-safe demo
  summaries.
- Private repository evidence is described as requiring private scans and
  human review before any summary becomes public copy.
- The page avoids local commands that expose ignored output paths or raw
  artifact names.

### Requirement 5: Link to proof surfaces and review context

The page shall guide readers from the meeting agenda into public-safe proof and
limitation surfaces.

Acceptance criteria:

- The page links to `/proof-paths/`, `/evidence/`, `/validation/`,
  `/limitations/`, `/manager-brief/`, and `/manager-packet/`.
- The page links to `/incident-call/` and `/use-cases/incident-review/` as
  related orientation without claiming incident cause or runtime proof.
- Existing relevant public pages may add minimal cross-links to
  `/review-room/` where they help readers move from manager/review orientation
  to the meeting agenda.
- Cross-links do not imply the review room proves release safety or operational
  safety.
- Cross-links from `/manager-brief/`, `/manager-packet/`, and
  `/incident-call/` are minimal and are validated by the standard generated
  internal-link resolution check once `/review-room/` exists.

### Requirement 6: Add discovery metadata and validation

The site shall make the concept discoverable and validate its claim boundaries.

Acceptance criteria:

- Discovery metadata labels `/review-room/` as `concept`.
- Discovery metadata for `/review-room/` is registered in
  `site/src/_site/discovery.json` with `publicClaimLevel: concept`,
  `hintCategory: use-case`, `sourceType: site-page`,
  `preferredProofPath: /proof-paths/`, and bounded `limitations` and
  `nonClaims`.
- Public page metadata includes a title, description, canonical URL, and Open
  Graph fields for the review-room route.
- `og:type` is set to `article` following the existing concept-page pattern.
- Sitemap metadata includes `/review-room/` in `site/src/_site/pages.json`.
- The page reuses the canonical site top navigation verbatim. `/review-room/`
  is not added to the top navigation for this phase.
- Validation checks confirm the route renders, includes the claim-level label,
  shared principle, required agenda text, required links, forbidden positioning,
  forbidden private/raw text, and a bounded word count.
- Validation asserts these required phrases verbatim: `Public claim level:
  concept`, `No public conclusion without evidence`, `claim`, `proof path`,
  `rule ID/evidence tier`, `coverage label`, `limitation`,
  `owner decision gap`, and the known/partial/missing line from Requirement 2.
- Required phrase matching is case-sensitive and must match exact rendered text
  after `normalizeRenderedText`. Page copy must use the exact casing shown
  here: `rule ID/evidence tier`, `coverage label`, and
  `owner decision gap`.
- The validator's `reviewRoomRequiredLinks` array includes `/proof-paths/`,
  `/evidence/`, `/validation/`, `/limitations/`, `/manager-brief/`,
  `/manager-packet/`, `/incident-call/`, and
  `/use-cases/incident-review/`.
- `reviewRoomRequiredLinks` is the authoritative required-link list for this
  phase; narrative link requirements must stay consistent with it.
- Validation checks confirm required internal links resolve in generated site
  output.
- Validation enforces a word count between 400 and 1500 words.
- The `review-room.test.mjs` fixture uses synthetic word-count filler
  following the `manager-brief.test.mjs` pattern so tests remain self-contained
  and the 400-word floor is verifiable without depending on real page copy.
- The `review-room.test.mjs` suite includes negative tests for fewer than 400
  words and more than 1500 words.
- Validation includes a forbidden AI/LLM positioning regex applied to decoded
  HTML, rendered text, and raw HTML attributes. The regex must include at
  minimum: `AI-powered`, `AI impact analysis`, `LLM-powered`, `LLM analysis`,
  `machine learning impact analysis`, `artificial intelligence impact
  analysis`, `intelligent analysis`, and `smart impact`, tested
  case-insensitively.
- Validation asserts `<meta property="og:type" content="article">`.
- `validateReviewRoomDist` validates `baseUrl` and pushes a clear error when it
  is not a valid absolute `http` or `https` URL, following the
  `manager-brief.mjs` pattern.
- Validation enforces the forbidden private/raw text set including `/Users/`,
  `C:\`, `file://`, `localhost`, `127.0.0.1`, `.tracemap`, `facts.ndjson`,
  `index.sqlite`, `logs/analyzer.log`, `analyzer.log`, `raw SQL`,
  `raw source snippets`, `ConnectionString`, `connection string`, `Server=`,
  `User Id=`, `Password=`, other connection-string credential tokens, secrets,
  raw remotes, generated scan directories, and private sample names.
- `site/scripts/validate.test.mjs` is updated to include the `/review-room/`
  route, a review-room page stub, and a matching discovery entry so existing
  `validateDist` tests continue to pass after `validateReviewRoomDist` is
  wired into `site/scripts/validate.mjs`. Its default `sitemapUrls` fixture
  includes `/review-room/`.
- Implementation validation includes `git diff --check`, `npm test` from
  `site/`, `npm run validate` from `site/`, `npm run build` from `site/`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
  when feasible.
