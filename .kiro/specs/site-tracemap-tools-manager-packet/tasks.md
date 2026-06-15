# Site TraceMap Tools Manager Packet Tasks

Public claim level: demo

- [x] Create `/manager-packet/` using existing static site layout patterns.
- [x] Explain the manager-facing problem TraceMap solves without runtime or release-safety claims.
- [x] Include visible public claim level, evidence ingredients, reader questions, limitations, and non-claims.
- [x] Link to `/demo/result/`, `/demo/proof-upgrades/`, `/packets/`, `/capabilities/`, `/roadmap/`, and `/limitations/`.
- [x] Add `/manager-packet/` to `site/src/_site/pages.json`.
- [x] Use `changefreq: "monthly"` and `priority: "0.8"` for the `/manager-packet/` sitemap entry.
- [x] Ensure `<nav class="top-nav">` in `site/src/manager-packet/index.html` exactly matches the canonical navigation enforced by `site/scripts/build.mjs`.
- [x] Add a cross-link from the existing `/` `#packets` callout.
- [x] Add a cross-link from `/packets/`.
- [x] Add a cross-link from `/capabilities/`.
- [x] Add a cross-link from `/demo/proof-upgrades/`.
- [x] Keep public artifact safety boundaries visible.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run desktop and mobile browser sanity checks for `/manager-packet/`, including no horizontal overflow and readable grid cards at narrow width.
