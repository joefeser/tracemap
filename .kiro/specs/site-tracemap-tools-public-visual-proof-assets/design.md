# Site TraceMap Tools Public Visual Proof Assets Design

Public claim level: demo

## Route

Publish `/demo/proof-assets/` as a static HTML page under
`site/src/demo/proof-assets/index.html`.

## Information Architecture

The page uses the existing long-form site structure:

- Page hero with public claim level and static evidence boundary.
- Split section explaining why visuals exist and what they do not prove.
- Visual proof board using HTML/CSS cards for public-safe report
  representations.
- Evidence boundary section that separates safe visual labels from local-only
  raw artifacts.
- Cross-link section back to `/demo/proof-upgrades/`, `/demo/result/`,
  `/packets/`, `/capabilities/`, `/limitations/`, and `/roadmap/`.

## Visual Asset Strategy

Use committed HTML and CSS only. Do not commit generated screenshots, generated
scan directories, raw report archives, `facts.ndjson`, SQLite files, analyzer
logs, raw snippets, raw SQL, config values, local absolute paths, raw repository
remotes, or private sample identities.

The visual blocks should resemble report panels but contain only safe labels:
status, rule IDs, evidence tiers, coverage, counts already public on
`/demo/proof-upgrades/`, limitation text, and relative report-family names.

Use the existing `workflow-grid` article-card pattern for the main visual board,
with narrowly scoped `proof-visual` child classes only for stable report-like
rows, badges, meters, and checklist layouts inside each card.

## Styling

Reuse the existing site palette, typography, page hero, split section,
detail-list, boundary-section, link-grid, and button patterns. Add only
closely scoped CSS classes if the visual board needs stable dimensions or
responsive behavior that existing classes do not provide.

The page must remain readable on desktop and mobile. Visual blocks should stack
without horizontal overflow.

## Validation

- Kiro spec review where available.
- `git diff --check`.
- `npm test` from `site/`.
- `npm run validate` from `site/`.
- Desktop and mobile browser sanity checks for `/demo/proof-assets/`.
