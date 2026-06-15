# Site TraceMap Tools Manager Packet Tasks

Public claim level: demo

- [ ] Create `/manager-packet/` using existing static site layout patterns.
- [ ] Explain the manager-facing problem TraceMap solves without runtime or release-safety claims.
- [ ] Include visible public claim level, evidence ingredients, reader questions, limitations, and non-claims.
- [ ] Link to `/demo/result/`, `/demo/proof-upgrades/`, `/packets/`, `/capabilities/`, `/roadmap/`, and `/limitations/`.
- [ ] Add `/manager-packet/` to `site/src/_site/pages.json`.
- [ ] Use `changefreq: "monthly"` and `priority: "0.8"` for the `/manager-packet/` sitemap entry.
- [ ] Ensure `<nav class="top-nav">` in `site/src/manager-packet/index.html` exactly matches the canonical navigation enforced by `site/scripts/build.mjs`.
- [ ] Add a cross-link from the existing `/` `#packets` callout.
- [ ] Add a cross-link from `/packets/`.
- [ ] Add a cross-link from `/capabilities/`.
- [ ] Add a cross-link from `/demo/proof-upgrades/`.
- [ ] Keep public artifact safety boundaries visible.
- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for `/manager-packet/`, including no horizontal overflow and readable grid cards at narrow width.
