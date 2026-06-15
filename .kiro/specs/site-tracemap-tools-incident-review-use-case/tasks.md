# Site TraceMap Tools Incident Review Use Case Tasks

Public claim level: concept

- [ ] Create `/use-cases/incident-review/` using existing static site layout patterns, and add the matching `site/src/_site/pages.json` entry in the same commit so the sitemap never points to a missing HTML file.
- [ ] Include visible `Public claim level: concept` copy and explain why the page is concept-level.
- [ ] Explain how TraceMap static evidence can orient review or incident-follow-up code questions without claiming runtime proof.
- [ ] Add role-specific question sections for engineers, reviewers, managers, and architects.
- [ ] Include proof-path links to `/use-cases/`, `/manager-packet/`, `/packets/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, and `/roadmap/`.
- [ ] Keep non-claims visible for runtime behavior, production traffic, deployment state, endpoint performance, production dependency understanding, P1 root cause, release safety, release approval, and AI impact analysis.
- [ ] Keep artifact safety visible: no raw snippets, raw SQL, config values, secrets, local absolute paths, raw repo remotes, raw facts, SQLite files, combined SQLite files, generated scan directories, analyzer logs, or private sample identities.
- [ ] Use the required `site/src/_site/pages.json` entry shape: `{ "path": "/use-cases/incident-review/", "changefreq": "monthly", "priority": "0.7" }`.
- [ ] Add a cross-link from `/use-cases/` near the existing "Release review" or "Reviewer handoff" cards.
- [ ] Add a cross-link from `/manager-packet/`.
- [ ] Add a cross-link from `/packets/`.
- [ ] Add a cross-link from `/demo/proof-assets/` only with orientation-only wording.
- [ ] Add a cross-link from `/demo/proof-upgrades/` only with demo-proof and limitations wording.
- [ ] Add a cross-link from `/capabilities/`.
- [ ] Add a cross-link from `/limitations/` only if it reinforces non-claims.
- [ ] Add a cross-link from `/roadmap/` in an existing concept/future planning section, or record the bounded reason for deferring it in `implementation-state.md`.
- [ ] Ensure the new page top navigation link set matches the canonical navigation enforced by `site/scripts/build.mjs`, with `aria-current` left to the build step.
- [ ] Confirm no `site/dist/` or `site/output/` generated output is hand-edited.
- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks for `/use-cases/incident-review/`.
- [ ] Run a focused browser sanity check for at least one touched doorway page.
- [ ] Manually review visible copy for `Public claim level: concept`, non-claims, artifact-safety boundaries, static-evidence framing, and proof-path links.
- [ ] Record each conditional cross-link added from `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/limitations/`, and `/roadmap/`, including the wording that keeps the link bounded.
- [ ] If any conditional incoming link is deferred, record the reason; still keep the new page's outgoing links to `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/limitations/`, and `/roadmap/`.
- [ ] Confirm internal links resolve from each touched doorway page and from `/use-cases/incident-review/` back to `/use-cases/`, `/manager-packet/`, `/packets/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, and `/roadmap/`.
- [ ] Update this spec's `implementation-state.md` with implementation scope, validation results, and follow-ups.
