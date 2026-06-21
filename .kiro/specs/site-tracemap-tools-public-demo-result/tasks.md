# Tasks

Status: implemented
Readiness: implemented
Public claim level: demo

- [x] 1. Add the public demo result spec and implementation state.
  - Include `Public claim level: demo`.
  - Record current first-slice demo boundaries and deferred sections.

- [x] 2. Add the public demo result page.
  - Publish `/demo/result/`.
  - Explain available sections, deferred sections, generated summaries, local-only artifacts, and sentinel checks.
  - Link to `scripts/demo-public.sh`.

- [x] 3. Add discovery links and metadata.
  - Link from `/demo/`, `/demo/start-here/`, `/packets/`, `/capabilities/`, and `/examples/`.
  - Add `/demo/result/` to `site/src/_site/pages.json`.

- [x] 4. Validate.
  - Run `npm test`.
  - Run `npm run validate`.
  - Run `git diff --check`.
  - Run desktop and mobile browser sanity checks for `/demo/result/` and linked doorway pages.

