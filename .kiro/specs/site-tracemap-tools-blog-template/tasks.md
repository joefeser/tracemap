# Tasks

- [x] 1. Add a blog template source structure.
  - Add article metadata for the current three blog posts.
  - Move article body HTML into private blog source fragments.
  - Keep public article slugs and canonical URLs unchanged.

- [x] 2. Generate the blog index and article pages during build.
  - Exclude private underscore-prefixed source directories from `dist`.
  - Generate `/blog/` from article metadata.
  - Generate `/blog/<slug>/` article pages from shared page chrome.

- [x] 3. Update local development behavior and docs.
  - Serve generated `dist` output during `npm run dev`.
  - Update site README notes for generated blog files.
  - Add spec-local implementation state.

- [x] 4. Validate.
  - Run `npm run build` from `site/`.
  - Smoke the generated blog URLs locally.
  - Check desktop and mobile browser layout.
