# Tasks

- [x] 1. Add a site validation command.
  - Build the site before validation.
  - Check generated `sitemap.xml` exists and its TraceMap URLs resolve in
    `dist`.
  - Check generated `robots.txt` points at the canonical sitemap URL.

- [x] 2. Validate internal HTML references.
  - Walk generated HTML files.
  - Check internal `href` and `src` targets without network access.
  - Report source file context for missing targets.

- [x] 3. Add tests and docs.
  - Add validator tests for passing output, broken sitemap URLs, broken HTML
    links, and missing robots sitemap directive.
  - Add `npm run validate`.
  - Document validation in `site/README.md`.

- [x] 4. Validate.
  - Run `npm test` from `site/`.
  - Run `npm run build` from `site/`.
  - Run `npm run validate` from `site/`.
