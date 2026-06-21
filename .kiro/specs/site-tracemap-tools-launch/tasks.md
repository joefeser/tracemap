# Tasks

Status: implemented
Readiness: implemented
Public claim level: shipped

- [x] 1. Add the static site subproject.
  - Add `site/package.json` and lockfile.
  - Add static site build and serve scripts.
  - Keep editable source under `site/src/`.

- [x] 2. Add the first public launch page assets.
  - Add the homepage, stylesheet, favicon, robots file, and initial sitemap source.
  - Keep generated `site/dist/` and `site/output/` out of source control.
  - Keep launch copy bounded to deterministic static evidence and documented limitations.

- [x] 3. Add AWS Amplify publishing configuration.
  - Add root `amplify.yml`.
  - Use `site` as the Amplify app root.
  - Publish only generated `site/dist` artifacts.

- [x] 4. Document site deployment and local development.
  - Add `site/README.md`.
  - Document the existing repository deployment model.
  - Document future site work as `site-*` specs.

- [x] 5. Validate the launch slice.
  - Run the site build from `site/`.
  - Check the site server script.
  - Sanity-check the local preview at desktop and mobile widths.
  - Verify malformed requests do not break the local preview server.
  - Complete the PR review loop for PR #59.

- [x] 6. Record launch implementation state.
  - Mark the launch spec implemented.
  - Record the launch branch, PR, deployment model, validation, product boundaries, and follow-ups.
  - Keep later deployment-validation hardening tracked in follow-up `site-*` specs.
