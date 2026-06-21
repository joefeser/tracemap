# tracemap.tools Launch Implementation State

Status: implemented
Readiness: implemented
Public claim level: shipped

## Branch And PR

- Branch: `codex/tracemap-tools-site`
- PR: `https://github.com/joefeser/tracemap/pull/59`
- Base: `dev`
- Latest fix commit: `fb54d06 Handle site dev server request errors`

## Scope

This launch added the first static `tracemap.tools` publishing surface inside the existing public `joefeser/tracemap` repository.

Added:

- root `amplify.yml`
- `site/package.json`
- `site/package-lock.json`
- `site/scripts/build.mjs`
- `site/scripts/serve.mjs`
- `site/src/index.html`
- `site/src/styles.css`
- `site/src/favicon.svg`
- `site/src/robots.txt`
- `site/src/sitemap.xml`
- `site/README.md`
- `.gitignore` entries for `site/dist/` and `site/output/`

## Deployment Model

- Repo root remains `joefeser/tracemap`.
- Amplify app root is `site`.
- Prebuild command is `npm ci`.
- Build command is `npm run build`.
- Publish/output directory is `dist`.
- Node requirement is `20+`.
- Amplify publishes only generated `site/dist` artifacts.

## Product Boundaries

- The site is a public discovery and documentation surface.
- The source of truth remains the repository, scanner facts, indexes, reports, docs, and rule catalog.
- The site is generated/static publishing output, not core scanner or reducer logic.
- Public claims should stay bounded to deterministic static evidence, rule IDs, evidence tiers, coverage labels, and limitations.
- Do not introduce LLM/AI impact-analysis claims into site copy.

## Validation

Validation completed in PR #59:

- `npm run build` from `site/`
- `node --check scripts/serve.mjs`
- local preview checked at desktop width
- local preview checked at 390px mobile width
- malformed request `/%E0%A4%A` returned `400 Bad Request`
- homepage returned `200` before and after malformed request
- GitHub private path guard passed
- PR review loop completed and review threads resolved

## Follow-Ups

- Configure AWS Amplify against `main` after the dev-to-main promotion includes the site setup.
- Add future site work as `site-*` Kiro specs.
- Keep generated output under `site/dist/` ignored and unedited by hand.
- For layout/content changes, run `npm run build` plus desktop/mobile browser sanity checks.
