# Site TraceMap Tools Demo Proof Upgrades Tasks

Status: implemented
Readiness: implemented
Public claim level: demo

- [x] Add `/demo/proof-upgrades/` page using existing site styles.
- [x] Add a demo evidence ledger for combine/report, paths/reverse, portfolio, diff, impact, and release-review rows.
- [x] Include proof paths, generated public-safe artifacts, fresh demo counts, limitations, and non-claims for each row.
- [x] Explain why these rows are demo evidence now and which stronger claims remain out of bounds.
- [x] Add explicit non-claims and artifact safety boundaries.
- [x] Add `/demo/proof-upgrades/` to `site/src/_site/pages.json`.
- [x] Link the page from `/demo/result/`, `/roadmap/`, and `/demo/`, and add links on the page to `/demo/result/`, `/roadmap/`, `/packets/`, and `/capabilities/`.
- [x] Update `/demo/result/` so the formerly deferred six rows point to `/demo/proof-upgrades/` as available demo evidence rather than remaining future-only.
- [x] Update `/roadmap/` so the demo proof-upgrades lane points to `/demo/proof-upgrades/` and does not describe the six rows as future-only.
- [x] Before final page copy, confirm `samples/public-demo/before/` and `samples/public-demo/after/` exist and refresh demo counts if generated summaries changed.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run a desktop and mobile browser sanity check for layout changes.
