# Tasks

Status: implemented
Readiness: implemented
Public claim level: hidden

- [x] 1. Add top-navigation validation.
  - Compare generated HTML top-navigation links with the canonical list.
  - Ignore page-local current markers while checking link text and targets.
  - Report generated file paths on drift.

- [x] 2. Update generated blog navigation.
  - Add `Capabilities` and `Docs` links to generated blog pages.

- [x] 3. Add focused tests.
  - Cover accepted canonical navigation.
  - Cover missing or stale navigation failures.

- [x] 4. Validate.
  - Run `npm test` from `site/`.
  - Run `npm run validate` from `site/`.

