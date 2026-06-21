# Site TraceMap Tools Incident Review Use Case Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

- [x] Create `/use-cases/incident-review/` using existing static site layout patterns, and add the matching `site/src/_site/pages.json` entry in the same commit so the sitemap never points to a missing HTML file.
- [x] Include visible `Public claim level: concept` copy and explain why the page is concept-level.
- [x] Explain how TraceMap static evidence can orient review or incident-follow-up code questions without claiming runtime proof.
- [x] Add role-specific question sections for engineers, reviewers, managers, and architects.
- [x] Include proof-path links to `/use-cases/`, `/manager-packet/`, `/packets/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`.
- [x] Keep non-claims visible for runtime behavior, production traffic, deployment state, endpoint performance, production dependency understanding, P1 root cause, release safety, release approval, and AI impact analysis.
- [x] Keep artifact safety visible: no raw snippets, raw SQL, config values, secrets, local absolute paths, raw repo remotes, raw facts, SQLite files, combined SQLite files, generated scan directories, analyzer logs, or private sample identities.
- [x] Use the required `site/src/_site/pages.json` entry shape: `{ "path": "/use-cases/incident-review/", "changefreq": "monthly", "priority": "0.7" }`.
- [x] Add a cross-link from `/use-cases/` near the existing "Release review" or "Reviewer handoff" cards.
- [x] Add a cross-link from `/manager-packet/`.
- [x] Add a cross-link from `/packets/`.
- [x] Add a cross-link from `/demo/proof-assets/` only with orientation-only wording.
- [x] Add a cross-link from `/demo/proof-upgrades/` only with demo-proof and limitations wording.
- [x] Add a cross-link from `/capabilities/`.
- [x] Add a cross-link from `/limitations/` only if it reinforces non-claims.
- [x] Add a cross-link from `/roadmap/` in an existing concept/future planning section, or record the bounded reason for deferring it in `implementation-state.md`.
- [x] Ensure the new page includes a placeholder `<header class="site-header">` block so `site/scripts/build.mjs` can replace it with the canonical navigation.
- [x] Confirm no `site/dist/` or `site/output/` generated output is hand-edited.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for `/use-cases/incident-review/`.
- [x] Run a focused browser sanity check for at least one touched doorway page.
- [x] Manually review visible copy for `Public claim level: concept`, non-claims, artifact-safety boundaries, static-evidence framing, and proof-path links.
- [x] Record each conditional cross-link added from `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/limitations/`, and `/roadmap/`, including the wording that keeps the link bounded.
- [x] If any conditional incoming link is deferred, record the reason; still keep the new page's outgoing links to `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/limitations/`, and `/roadmap/`.
- [x] Confirm internal links resolve from each touched doorway page and from `/use-cases/incident-review/` back to `/use-cases/`, `/manager-packet/`, `/packets/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`.
- [x] Update this spec's `implementation-state.md` with implementation scope, validation results, and follow-ups.
