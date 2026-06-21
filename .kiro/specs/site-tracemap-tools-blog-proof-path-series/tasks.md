# Site TraceMap Tools Blog Proof Path Series Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Spec Packet Tasks

- [x] Create `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md` for the spec-only phase.
- [x] Keep edits scoped to
  `.kiro/specs/site-tracemap-tools-blog-proof-path-series/`.
- [x] Include the initial `Status: not-started`, `Readiness: spec-review`, and
  `Public claim level: concept` headers. This is superseded by current
  `Readiness: ready-for-implementation` after review finding disposition.
- [x] Run Kiro spec review with `claude-opus-4.8`.
- [x] Run Kiro spec review with `claude-sonnet-4.6`.
- [x] Patch or disposition Medium+ review findings.
- [x] Move readiness to `ready-for-implementation` only after Medium+ review
  findings are patched or dispositioned.
- [x] Confirm spec-review gate is complete and readiness is
  `ready-for-implementation`; remaining spec-packet validation, commit, PR,
  and PR-loop work stays tracked below.
- [x] Run spec-only validation: `git diff --check`,
  `./scripts/check-private-paths.sh`, and focused text checks.
- [x] Commit, push, create a ready PR to `main`, and run the required PR loop.

## Future Implementation Tasks

- [ ] Confirm this spec is `ready-for-implementation` before writing site
  content.
- [ ] Choose final article count and record the rationale in
  `implementation-state.md`.
- [ ] Choose final article slugs and confirm they do not collide with
  `why-tracemap-exists`, `what-tracemap-solves-for-engineering-teams`, or
  `building-tracemap-with-codex-kiro-qodo`.
- [ ] Record rejected article ideas and why they were rejected or deferred.
- [ ] Decide whether each article is `concept` or, only with proof-path backing,
  `demo`; default to `concept`.
- [ ] Choose the claim-level mechanism: visible article-body claim-level text,
  or a `publicClaimLevel` blog metadata/rendering/validation extension; record
  the decision in `implementation-state.md`.
- [ ] Add article source content using existing blog patterns.
- [ ] Include `Public claim level: concept` in each article unless a
  demo-backed exception is recorded.
- [ ] Include opening problem blocks.
- [ ] Include evidence-backed claim examples.
- [ ] Include proof-path reading steps.
- [ ] Include limitations and non-claims.
- [ ] Include safe language examples.
- [ ] Include unsafe language examples that are clearly framed as wording to
  avoid.
- [ ] Include links to proof surfaces.
- [ ] Include closing handoff/action sections.
- [ ] Verify `/proof-paths/`, `/proof-source-catalog/`, `/evidence/`,
  `/packets/`, `/review-claim-checklist/`, `/static-vs-runtime/`,
  `/limitations/`, `/validation/`, `/demo/result/`, and `/questions/`.
- [ ] Link to relevant required proof surfaces from each article; cover the
  full required link set across the article set, or record per-article
  substitutions and series-level deferrals. For a single-article
  implementation, link only relevant routes and record remaining required
  routes as justified deferrals in `implementation-state.md`.
- [ ] Keep copy plainspoken and professional.
- [ ] Avoid blame toward consultants, vendors, teams, maintainers, or prior
  technical choices.
- [ ] Avoid internal workplace details, private project/customer/service names,
  raw command output, local paths, raw remotes, and generated scan dirs.
- [ ] Avoid runtime behavior proof, production traffic, endpoint performance,
  outage cause, release safety, operational safety, complete coverage, AI/LLM
  impact analysis, embeddings, vector databases, and prompt classification
  claims.
- [ ] Avoid publishing raw facts, raw SQLite content, analyzer logs, source
  snippets, SQL, config values, secrets, private names, hidden validation
  details, or local-only artifact paths.
- [ ] Register articles in the blog metadata source with unique slugs,
  conservative titles, descriptions, dates, canonical URLs, and body paths.
- [ ] Confirm each new article appears in generated `sitemap.xml`; blog slugs
  are currently auto-emitted by `generateSitemap`, so no manual sitemap entry
  is required unless build behavior changes.
- [ ] Add discovery, `llms.txt`, or `llms-full.txt` metadata when comparable
  blog articles are indexed there.
- [ ] Add focused validation for required content blocks, required links,
  metadata, blog registration, sitemap/discovery output, forbidden claims,
  private/raw material, and word count bounds.
- [ ] Follow the existing `site/scripts/` per-route validation-module pattern
  with a dedicated validation module and matching `*.test.mjs` file for article
  content and claim-boundary checks.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for each article and the
  blog index when layout or discovery changes are made.
- [ ] Update `implementation-state.md` with final scope decisions, validation,
  oddities, review-loop outcomes, and follow-up items.
