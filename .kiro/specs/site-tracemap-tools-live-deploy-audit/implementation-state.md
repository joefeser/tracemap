# Site TraceMap Tools Live Deploy Audit Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: demo

## Branch

Implementation branch: `codex/site-live-deploy-audit`

## Scope

This phase adds a static deploy audit route and validation guard for
`tracemap.tools`. It checks generated build output under `site/dist`; it does
not inspect live AWS, DNS, TLS, CDN cache, crawler state, or runtime behavior.

## Implemented

- Added `/deploy-audit/`.
- Added `site/scripts/deploy-audit.mjs`.
- Added `site/scripts/deploy-audit.test.mjs`.
- Wired deploy audit validation into `site/scripts/validate.mjs`.
- Added sitemap and discovery metadata for `/deploy-audit/`.
- Linked the audit from `/validation/`, `/docs/`, and `/proof-paths/`.

## Claim Boundaries

- Safe to say: the local static build includes required public routes,
  `sitemap.xml`, `robots.txt`, `llms.txt`, `docs-index.json`, and
  `routes-index.json`.
- Not safe to say: AWS is serving the newest build, DNS/TLS/CDN are healthy,
  production traffic exists, runtime behavior is proven, release safety is
  proven, or deployment success is proven.
- Public copy does not publish raw facts, SQLite files, analyzer logs, source
  snippets, raw SQL, config values, secrets, local paths, raw remotes, or
  generated scan directories.

## Validation

Planned and run before PR:

- `git diff --check`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- `./scripts/check-private-paths.sh`

## Follow-Ups

- A future optional live smoke check could fetch `https://tracemap.tools` after
  Amplify deployment, but it should remain a separate live-environment check
  and must not be confused with this deterministic static build audit.
