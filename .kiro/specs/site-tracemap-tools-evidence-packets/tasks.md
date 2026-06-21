# Tasks

Status: implemented
Readiness: implemented
Public claim level: demo

- [x] 1. Add the evidence packet spec and implementation state.
  - Include `Public claim level: demo`.
  - Record the site-only scope and claim boundaries.

- [x] 2. Add the public evidence packet page.
  - Publish `/packets/`.
  - Explain packet contents, reader paths, inspection workflow, and limitations.
  - Keep SQLite, facts, reports, logs, manifests, rule IDs, evidence tiers, commit SHA, coverage labels, and limitations as the evidence model.

- [x] 3. Add discovery links and sitemap metadata.
  - Link from homepage, demo, capabilities, and docs.
  - Add `/packets/` to `site/src/_site/pages.json`.

- [x] 4. Validate.
  - Run `npm test`.
  - Run `npm run validate`.
  - Run `git diff --check`.

