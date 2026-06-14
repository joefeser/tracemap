# Tasks

- [x] 1. Record the discovery slice scope and launch state.
  - Add this `site-tracemap-tools-discovery` spec.
  - Add or update implementation-state notes for the launch and discovery site
    slices.

- [x] 2. Add stable crawlable topic pages.
  - Add `/evidence/`, `/outputs/`, `/workflows/`, `/validation/`,
    `/limitations/`, and `/demo/`.
  - Keep page claims bounded and tied back to TraceMap source docs.

- [x] 3. Update homepage navigation.
  - Replace section-only navigation with stable page URLs.
  - Keep GitHub and source-of-truth links visible.

- [x] 4. Update discovery metadata.
  - Add all stable pages to `sitemap.xml`.
  - Add a static 404 page.
  - Keep `robots.txt` crawl-friendly.

- [x] 5. Update deployment documentation.
  - Document Amplify app root, build command, output directory, and validation
    URLs in `site/README.md`.

- [x] 6. Validate.
  - Run `npm run build`.
  - Run a local preview smoke for homepage and new pages.
  - Check desktop and mobile overflow with a browser.
