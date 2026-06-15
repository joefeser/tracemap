# Tasks

- [x] 1. Wire validation into Amplify.
  - Change the Amplify site build command to `npm run validate`.
  - Keep `site/dist` as the published artifact directory.
  - Preserve the existing `site/` app root and `npm ci` prebuild step.

- [x] 2. Add pull request site validation.
  - Add a GitHub Actions workflow for site-related changes.
  - Use Node.js 20 and `npm ci`.
  - Run `npm test` and `npm run validate`.

- [x] 3. Update docs and prior implementation state.
  - Update the site README deployment command.
  - Mark the link-validation follow-up as handled by this spec.
  - Add spec-local implementation state.

- [x] 4. Validate.
  - Run `npm test` from `site/`.
  - Run `npm run validate` from `site/`.
  - Check workflow and Amplify YAML syntax shape.
