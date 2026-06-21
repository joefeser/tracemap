# Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

- [x] 1. Add the site-facing concept page.
  - Create `/vault-export/` with future-facing copy.
  - Explain the manager, reviewer, architect, and engineer audience.
  - Preserve the source-of-truth boundary around facts, SQLite, reports, and
    the rule catalog.

- [x] 2. Explain the intended workflow and safety boundaries.
  - Describe run demo, generate vault export, open `Start Here.md`, and click
    through linked notes.
  - Describe endpoints, routes, symbols, packages, SQL/config surfaces, and
    paths as Markdown notes.
  - State that the export should omit raw snippets, raw SQL, config values,
    secrets, local absolute paths, and raw repo remotes.

- [x] 3. Add discovery links and metadata.
  - Link the concept from `/demo/` and `/workflows/` without claiming it is
    shipped.
  - Add `/vault-export/` to the sitemap and site README validation URLs.
  - Add spec-local implementation state.

- [x] 4. Validate.
  - Run `npm run build` from `site/`.
  - Smoke `/vault-export/`, `/demo/`, and `/workflows/` locally.
  - Check desktop and mobile browser layout.
