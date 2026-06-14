# Tasks

- [x] 1. Add sitemap source metadata.
  - Move public non-blog page URLs into private site metadata.
  - Keep canonical paths, changefreq values, and priorities aligned with the
    existing published sitemap.
  - Keep underscore-prefixed source folders out of `dist`.

- [x] 2. Generate sitemap output during build.
  - Generate `/sitemap.xml` from page metadata and blog article metadata.
  - Validate sitemap paths, changefreq values, priorities, and duplicates.
  - Preserve `https://tracemap.tools` canonical URLs.

- [x] 3. Update docs and prior implementation state.
  - Document generated sitemap ownership in `site/README.md`.
  - Mark the blog-template sitemap follow-up as handled by this spec.
  - Add spec-local implementation state.

- [x] 4. Validate.
  - Run `npm test` from `site/`.
  - Run `npm run build` from `site/`.
  - Smoke generated sitemap output locally.
