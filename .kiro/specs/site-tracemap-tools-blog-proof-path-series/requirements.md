# Site TraceMap Tools Blog Proof Path Series Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future bounded public blog/content phase for `tracemap.tools` that
adds one or more articles explaining TraceMap proof paths and deterministic
static evidence use cases. The articles should help developers, reviewers,
managers, and agents understand why evidence-backed claims matter, how to read
proof paths, and what TraceMap cannot prove.

This is a spec-only site phase. It must not implement site source, generated
output, scanner behavior, reducer behavior, runtime services, or validation
scripts in this PR.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

Future articles may explain TraceMap's concept-level static evidence model:
rule IDs, evidence tiers, coverage labels, limitations, generated artifact
families, proof paths, and public-safe review handoffs. They must keep every
substantive claim tied to checked-in public site surfaces or public-safe demo
proof, not runtime outcomes.

Future articles must not claim that TraceMap proves runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, complete coverage, AI impact analysis, LLM analysis,
embeddings, vector databases, or prompt classification. The articles must not
imply TraceMap replaces telemetry, logs, traces, tests, owners, human review,
or release process.

## Candidate Articles

The future implementer shall choose the final article count, article slugs,
article ordering, and article metadata. Candidate article ideas are:

- `What a proof path is`
- `How to read static evidence without overclaiming`
- `What TraceMap can bring to a review before runtime telemetry`
- `Why no public conclusion without evidence matters`

The implementation may publish one article, a short series, or defer a
candidate when it would duplicate existing site material. The implementation
state note must record selected articles, rejected article ideas, final slugs,
and rationale.

## Existing Blog Posts To Avoid Duplicating

The future implementation shall check the current blog registry before writing
or registering new articles. At spec time, existing public blog slugs include:

- `why-tracemap-exists`
- `what-tracemap-solves-for-engineering-teams`
- `building-tracemap-with-codex-kiro-qodo`

New articles must not repeat those posts' primary jobs. They may link to those
posts when useful, but must add a distinct proof-path reading or static
evidence claim-boundary angle.

## Requirements

### Requirement 1: Publish bounded proof-path blog article content

The future implementation shall add one or more public blog articles that
explain proof paths and deterministic static evidence without increasing the
site's public claim level beyond available proof.

Acceptance criteria:

- The future implementation chooses article count and final slugs before
  writing page content.
- The chosen articles use existing blog generation, article page layout,
  navigation, footer, accessibility, and metadata patterns.
- Each article says or clearly preserves `Public claim level: concept` unless
  a specific demo-backed article justifies `Public claim level: demo`.
- Because the current blog metadata schema in `site/src/_blog/articles.json`
  does not expose a claim-level field and existing blog bodies do not render a
  claim-level line, the future implementation makes the article claim level
  explicit by one of these mechanisms:
  - Render `Public claim level: concept` or `Public claim level: demo` in the
    article body using the public text convention used by other site pages.
  - Extend the blog metadata schema with a `publicClaimLevel` field and update
    blog validation/rendering so the claim level is visible or otherwise
    machine-checkable in the generated article.
- The chosen claim-level mechanism and rationale are recorded in
  `implementation-state.md`.
- `Public claim level: demo` is allowed only when the article is backed by a
  public proof path or public demo route, and the backing route is linked from
  the article.
- If blog discovery metadata supports claim levels, article discovery entries
  default to `Public claim level: concept`.
- If blog discovery metadata supports claim levels and an article uses `demo`,
  the metadata records `demo` only with proof-path backing and a linked public
  demo surface.
- The implementation-state note records selected articles, rejected article
  ideas, final slugs, final claim levels, and rationale.
- The implementation avoids duplicating `why-tracemap-exists`,
  `what-tracemap-solves-for-engineering-teams`, and
  `building-tracemap-with-codex-kiro-qodo`.
- The articles do not introduce a runtime service, form, client-side state,
  scanner/reducer behavior, analytics dependency, or claim-evaluation logic.

### Requirement 2: Include required article content blocks

Each published article shall include the content blocks needed to teach proof
paths without overclaiming.

Acceptance criteria:

- Each article includes an opening problem that explains a claim-review or
  evidence-reading difficulty.
- Each article includes an evidence-backed claim example that names the kind
  of evidence required before repeating the claim.
- Each article includes proof-path reading steps that explain how to move from
  a public claim to supporting route, evidence family, limitation, and follow-up
  question.
- Each article includes limitations and non-claims.
- Each article includes safe language examples.
- Each article includes unsafe language examples that are explicitly framed as
  wording to avoid.
- Each article links to proof surfaces relevant to its topic.
- Each article ends with a closing handoff or action, such as checking a claim,
  opening a proof path, taking questions to owners, or pairing static evidence
  with telemetry/tests where appropriate.
- The article set collectively covers why deterministic evidence matters, how
  to read proof paths, and what TraceMap cannot prove.

### Requirement 3: Verify and use required links before publishing

The future implementation shall verify required public routes before linking
or publishing the articles.

Acceptance criteria:

- Before publishing, the implementation verifies `/proof-paths/`.
- Before publishing, the implementation verifies `/proof-source-catalog/`.
- Before publishing, the implementation verifies `/evidence/`.
- Before publishing, the implementation verifies `/packets/`.
- Before publishing, the implementation verifies `/review-claim-checklist/`.
- Before publishing, the implementation verifies `/static-vs-runtime/`.
- Before publishing, the implementation verifies `/limitations/`.
- Before publishing, the implementation verifies `/validation/`.
- Before publishing, the implementation verifies `/demo/result/`.
- Before publishing, the implementation verifies `/questions/`.
- Each article links to the required routes that are relevant to its content.
- The article set should cover the full required link set unless the
  implementation-state note records a route substitution or justified deferral.
  For a single-article implementation, link only the routes relevant to the
  article scope and record remaining required routes as justified deferrals;
  full required-route coverage is a series-level goal, not a per-article
  mandate.
- If any required route is unavailable at implementation time, the future
  implementation either blocks publication until a safe public target exists or
  records a public-safe substitution, rationale, and intended correction in
  `implementation-state.md`.
- Link anchor text does not imply runtime proof, production proof, release
  approval, operational safety, or complete coverage.

### Requirement 4: Preserve public-safe editorial tone

The articles shall be plainspoken, professional, and safe for public sharing.

Acceptance criteria:

- The copy uses clear language for developers, managers, reviewers, and agents.
- The copy avoids blame toward consultants, vendors, teams, maintainers, or
  prior technical decisions.
- The copy avoids internal workplace details.
- The copy avoids private project names, customer names, service names, repo
  names, hostnames, local paths, private remotes, and generated scan dirs.
- The copy avoids raw command output.
- The copy avoids scare framing and competitor-first positioning.
- The copy keeps examples sanitized or based on existing public demo/proof
  surfaces.
- The copy does not publish hidden validation details.
- The implementer shall choose and record a target word count range in
  implementation-state. A reasonable range is 900 to 1,800 words per article,
  with shorter companion articles allowed when the article's job is narrow.
  The implementer may record a single range or per-article tiers such as
  primary article and companion article so deterministic word-count validation
  matches the selected article shapes.

### Requirement 5: Preserve forbidden-claim boundaries

The articles shall make static-evidence limits visible and shall not upgrade
proof paths into runtime, production, release, operational, or AI/LLM claims.

Acceptance criteria:

- The articles do not claim TraceMap proves runtime behavior.
- The articles do not claim TraceMap proves production traffic.
- The articles do not claim TraceMap proves endpoint performance.
- The articles do not claim TraceMap proves outage cause.
- The articles do not claim TraceMap proves release safety.
- The articles do not claim TraceMap proves operational safety.
- The articles do not claim TraceMap proves complete coverage.
- The articles do not describe TraceMap as AI impact analysis, LLM analysis,
  embedding-based impact analysis, vector-database analysis, prompt
  classification, AI-powered, LLM-powered, intelligent impact analysis,
  automated release approval, operational assurance, or production
  observability.
- The articles do not imply TraceMap replaces telemetry, logs, traces, tests,
  service owners, human review, or release process.
- The articles do not say a finding is `impacted`, `safe`, `unsafe`,
  `approved`, `blocked`, `root cause`, `validated for release`, or
  `production proven` unless the phrase is explicitly framed as a non-claim,
  unsafe-language example, or wording to avoid.
- The articles distinguish static evidence from runtime telemetry and direct
  readers to runtime owners or runtime evidence when runtime questions remain.

### Requirement 6: Keep private and raw material out of public articles

The articles shall publish only public-safe explanatory copy and links.

Acceptance criteria:

- The articles do not publish raw source snippets.
- The articles do not publish raw SQL.
- The articles do not publish config values.
- The articles do not publish secrets, tokens, keys, connection strings, or
  credential-shaped examples.
- The articles do not publish local absolute paths, raw repository remotes,
  private repository names, private sample names, generated scan directories,
  raw `facts.ndjson`, raw SQLite content, raw analyzer logs, or hidden
  validation details.
- The articles may mention artifact names such as `scan-manifest.json`,
  `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log` only as
  local output types or generated artifact families, not as raw public content.
- The articles may use sanitized rule IDs, evidence tiers, coverage labels,
  limitations, snippet hashes, public route paths, and public demo summary
  names.
- The future implementation records any manual public-safety review decisions
  in `implementation-state.md`.

### Requirement 7: Register metadata and discovery conservatively

The future implementation shall make the articles discoverable without
overstating the public claim level.

Acceptance criteria:

- Each article is registered through the existing blog article metadata source.
- Each article has a unique slug that does not collide with existing blog
  slugs.
- Each article satisfies all required fields enforced by `validateArticle` in
  `site/scripts/build.mjs`; at spec time those fields include `body`,
  `calloutHeading`, `calloutHtml`, `cardDescription`, `category`,
  `description`, `h1`, `hero`, `ogDescription`, `published`,
  `publishedDisplay`, `slug`, and `title`.
- Canonical URLs are derived from article slugs by the existing blog build
  pattern unless the build changes in the future.
- Metadata descriptions stay within existing site limits, or 160 characters
  when no local limit exists.
- Metadata titles stay within existing site limits, or 70 characters when no
  local limit exists.
- Metadata uses `concept` as the default public claim level when claim-level
  metadata exists.
- Metadata uses `demo` only when the article links to public proof-path or
  public demo backing.
- The articles appear in sitemap output; existing blog articles are currently
  emitted by sitemap generation.
- At spec time, blog articles are not represented in discovery metadata,
  `llms.txt`, or `llms-full.txt`. If future blog or discovery build behavior
  adds comparable article entries, the new articles shall be registered there
  with bounded claim-level metadata.
- Discovery and sitemap metadata do not claim runtime proof, production proof,
  release safety, operational safety, complete coverage, AI impact analysis,
  LLM analysis, embeddings, vector databases, or prompt classification.

### Requirement 8: Validate future implementation

The future implementation shall run focused public-site validation and record
results in this spec's implementation-state note.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Run the existing site validation commands required for comparable blog
  changes, including `npm test`, `npm run validate`, and `npm run build` from
  `site/` unless the implementation-state note records a tool failure or
  explicit deferral.
- Validate required article copy blocks: opening problem, evidence-backed claim
  example, proof-path reading steps, limitations/non-claims, safe language
  examples, unsafe language examples, links to proof surfaces, and closing
  handoff/action.
- Validate required link resolution for `/proof-paths/`,
  `/proof-source-catalog/`, `/evidence/`, `/packets/`,
  `/review-claim-checklist/`, `/static-vs-runtime/`, `/limitations/`,
  `/validation/`, `/demo/result/`, and `/questions/`, or record substitutions.
- Validate article metadata, blog article registration, unique slugs,
  canonical URLs, sitemap metadata, and discovery or `llms` metadata where
  applicable.
- Follow the existing per-route validation-module convention for focused
  article content validation: add a dedicated `site/scripts/<name>.mjs` module
  plus a matching `<name>.test.mjs` file that is discovered by `npm test`
  through `node --test scripts/*.test.mjs`. Generic `npm run validate`
  link/sitemap/robots checks are required but are not sufficient for article
  content-block, claim-level, forbidden-claim, private/raw-material, or word
  count validation.
- Validate rendered article copy for forbidden runtime, production, outage,
  release-safety, operational-safety, complete-coverage, AI/LLM, embedding,
  vector-database, and prompt-classification claims.
- Scope forbidden finding-label validation to verdict-style usage such as
  `is safe`, `marked unsafe`, `impacted finding`, `approved`, `blocked`, or
  similar conclusion phrasing. Do not fail sanctioned compound or instructional
  terms such as `public-safe`, `safe language examples`, `unsafe language
  examples`, and explicit wording-to-avoid blocks.
- Validate rendered article copy for private or raw material listed in this
  spec.
- Validate the article word count bounds selected by the implementer.
- Run desktop and mobile browser sanity checks for each new article page and
  the blog index when layout or discovery changes are made.
- Record validation commands, route/link checks, selected article count,
  selected slugs, rejected article ideas, claim-level decisions, review-loop
  outcomes, and follow-up items in `implementation-state.md`.
