# Site TraceMap Tools Roadmap Requirements

Public claim level: concept

## Objective

Create a public roadmap page for `tracemap.tools` that explains the site runway as a claim ledger: what is visible today, what proof is needed before future copy can be upgraded, and which evidence remains hidden until it is safe to summarize.

## Requirements

- The page must be published at `/roadmap/`.
- The page must describe roadmap items as claim-gated proof lanes, not product promises.
- The page must distinguish current `main`, demo, concept, and hidden public-claim levels.
- The page must link to existing public surfaces for current demo result, evidence packet guide, capabilities, docs, legacy validation, and vault export concept.
- The page must state that TraceMap is static evidence and does not prove runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.
- The page must avoid raw source snippets, raw SQL, config values, secrets, local absolute paths, raw repository remotes, or private validation details.
- The page must be added to generated sitemap metadata.
- Existing site pages that mention future/demo proof lanes should link to the roadmap where useful.

## Acceptance Criteria

- `/roadmap/` builds as a static page from `site/src/roadmap/index.html`.
- `site/src/_site/pages.json` includes `/roadmap/`.
- Demo, demo result, legacy validation, capabilities, and docs pages expose a path to `/roadmap/`.
- `npm test` and `npm run validate` pass from `site/`.
- A desktop and mobile browser sanity check confirms the new page renders and is usable.
