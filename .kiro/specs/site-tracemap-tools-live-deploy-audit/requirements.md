# Site TraceMap Tools Live Deploy Audit Requirements

Status: implemented
Readiness: ready-for-review
Public claim level: demo

## Summary

Add a public site phase that documents and validates the static deployment
surface for `tracemap.tools`. The feature is a static build-output audit, not a
live AWS, DNS, TLS, CDN, uptime, crawler, or runtime monitor.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a deploy audit route

The site shall publish `/deploy-audit/` as a demo-level page that explains the
static deploy audit and its boundaries.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page states the shared site principle.
- The page names the static files and routes covered by the audit.
- The page explicitly says the audit is not live AWS state, runtime behavior
  proof, deployment success proof, endpoint performance proof, release approval,
  or release safety.

### Requirement 2: Validate generated static entrypoints

Site validation shall fail when deployment-critical static entrypoints are
missing from `site/dist`.

Acceptance criteria:

- Validate required generated files: `sitemap.xml`, `robots.txt`, `llms.txt`,
  `docs-index.json`, and `routes-index.json`.
- Validate required public routes including `/`, `/docs/`, `/validation/`,
  `/limitations/`, `/demo/`, `/demo/result/`, `/proof-paths/`,
  `/legacy-evidence/`, and `/deploy-audit/`.
- Validate required routes appear in `sitemap.xml`.
- Validate selected required routes appear in `routes-index.json`.
- Validate `/deploy-audit/` rendered content contains required boundaries and
  avoids private/local artifact text.

### Requirement 3: Preserve public-safe boundaries

The deploy audit shall not publish private evidence or imply stronger product
claims than the static build output supports.

Acceptance criteria:

- The public page and discovery metadata do not publish raw facts, SQLite files,
  analyzer logs, source snippets, raw SQL, config values, secrets, local paths,
  raw remotes, or generated scan directories.
- Discovery metadata labels the route as `demo`.
- The page links to validation, proof paths, docs, and limitations.
- Existing validation and docs surfaces link back to the audit.

### Requirement 4: Validate the phase

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run `./scripts/check-private-paths.sh`.
