# Tasks

Status: implemented
Readiness: implemented
Public claim level: hidden

- [x] 1. Add shared header rendering.
  - Export canonical navigation data from the build script.
  - Render static and blog page headers from one helper.
  - Preserve current-page and current-section markers.

- [x] 2. Share navigation data with validation.
  - Use the build script's canonical link list in generated-site validation.

- [x] 3. Add focused build coverage.
  - Verify stale source navigation is replaced in generated output.
  - Verify generated output includes `Capabilities` and `Docs`.

- [x] 4. Validate.
  - Run `npm test` from `site/`.
  - Run `npm run validate` from `site/`.

