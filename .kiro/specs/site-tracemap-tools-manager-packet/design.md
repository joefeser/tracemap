# Site TraceMap Tools Manager Packet Design

Public claim level: demo

## Surface

Add a static page at `/manager-packet/` with source in
`site/src/manager-packet/index.html`. The page uses the existing site chrome,
`page-hero`, `section`, `split-section`, `workflow-grid`, `detail-list`,
`boundary-section`, and `link-section` patterns.

## Content Structure

1. Hero: answer the manager-facing question and state `Public claim level:
   demo`.
2. Problem framing: explain the evidence conversation before impact or safety
   claims.
3. Reader cards: give managers, reviewers, architects, and leads concrete
   questions to ask.
4. Evidence ingredients: list static dependencies, coverage labels, rule IDs,
   summaries, gaps, and limitations.
5. Proof path map: connect the page to `/demo/result/`,
   `/demo/proof-upgrades/`, `/packets/`, `/capabilities/`, and `/roadmap/`.
6. Non-claims and artifact safety: keep runtime, production, release, telemetry,
   AI, and raw-artifact boundaries visible.

## Cross-Link Plan

- Home page: add `/manager-packet/` to the first-look or packet callout path.
- Home page placement: update the existing `#packets` callout section rather
  than the top navigation or hero.
- `/packets/`: link to `/manager-packet/` as the higher-level reader path.
- `/capabilities/`: link to `/manager-packet/` from source material or boundary
  context.
- `/demo/proof-upgrades/`: link to `/manager-packet/` for non-technical readers.
- `/manager-packet/`: link to `/demo/result/`, `/demo/proof-upgrades/`,
  `/packets/`, `/capabilities/`, `/roadmap/`, and `/limitations/`.
- Sitemap metadata: add `/manager-packet/` with `changefreq: "monthly"` and
  `priority: "0.8"`.
- Navigation: copy the existing canonical `<nav class="top-nav">` block exactly
  so `site/scripts/build.mjs` and `npm run validate` remain satisfied. Do not
  add `/manager-packet/` to the global top navigation in this phase.

## Safety

The page must not publish raw snippets, SQL, config values, secrets, local paths,
raw remotes, raw facts, SQLite files, generated scan directories, analyzer logs,
combined SQLite files, or private sample identities. The page may summarize
public-safe generated reports and checked-in public demo proof paths.

## Validation

Use the existing site build/test/validate workflow and browser sanity checks for
desktop and mobile layout. The browser pass should confirm the `workflow-grid`
cards are readable at narrow width, `page-hero` and `hero-note` text do not
overflow, and there is no page-level horizontal scroll.
