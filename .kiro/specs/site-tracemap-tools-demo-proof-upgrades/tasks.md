# Site TraceMap Tools Demo Proof Upgrades Tasks

Public claim level: demo

- [ ] Add `/demo/proof-upgrades/` page using existing site styles.
- [ ] Add a demo evidence ledger for combine/report, paths/reverse, portfolio, diff, impact, and release-review rows.
- [ ] Include proof paths, generated public-safe artifacts, fresh demo counts, limitations, and non-claims for each row.
- [ ] Explain why these rows are demo evidence now and which stronger claims remain out of bounds.
- [ ] Add explicit non-claims and artifact safety boundaries.
- [ ] Add `/demo/proof-upgrades/` to `site/src/_site/pages.json`.
- [ ] Link the page from `/demo/result/`, `/roadmap/`, and `/demo/`, and add links on the page to `/demo/result/`, `/roadmap/`, `/packets/`, and `/capabilities/`.
- [ ] Update `/demo/result/` so the formerly deferred six rows point to `/demo/proof-upgrades/` as available demo evidence rather than remaining future-only.
- [ ] Update `/roadmap/` so the demo proof-upgrades lane points to `/demo/proof-upgrades/` and does not describe the six rows as future-only.
- [ ] Before final page copy, confirm `samples/public-demo/before/` and `samples/public-demo/after/` exist and refresh demo counts if generated summaries changed.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run a desktop and mobile browser sanity check for layout changes.
