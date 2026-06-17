# Site TraceMap Tools Incident Call Use Case Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Branch

Implementation branch: `codex/site-incident-call-use-case-impl`

## Scope

This branch implements the concept-level `/incident-call/` site route from the
site incident-call use case spec. It adds the public page, sitemap metadata,
discovery metadata, cross-links, focused validation, tests, and completed spec
state. It does not add runtime monitoring, APM integration, incident diagnosis,
or core scanner/reducer behavior.

## Implemented

- Added `site/src/incident-call/index.html`.
- Added `/incident-call/` to `site/src/_site/pages.json`.
- Added `/incident-call/` discovery metadata with `publicClaimLevel: concept`.
- Linked the route from `/use-cases/` and `/use-cases/incident-review/`.
- Added `site/scripts/incident-call.mjs` with route, sitemap,
  `routes-index.json`, required-copy, required-link, and forbidden-text checks.
- Added `site/scripts/incident-call.test.mjs`.
- Wired incident-call validation into `site/scripts/validate.mjs`.
- Updated `site/scripts/validate.test.mjs` fixtures for the new validation.
- Marked the spec tasks as complete.

## Claim Boundaries

- Safe to say: the site now publishes a concept-level incident-call orientation
  route that explains how static dependency evidence can narrow inspection
  during a P1 or production incident call.
- Safe to say: the route links readers to proof paths, validation, docs,
  limitations, demo result, and incident review orientation.
- Not safe to say: TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  production dependency completeness, or APM replacement.
- Public copy avoids raw fact streams, SQLite indexes, analyzer logs, source
  excerpts, raw SQL, config values, secrets, local paths, repository remotes,
  scan directories, and private sample identities.

## Validation

Run before PR:

- `git diff --check`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- `./scripts/check-private-paths.sh`

Browser sanity:

- Served the site locally with `PORT=4183 npm run dev`.
- Opened `http://localhost:4183/incident-call/` with the Playwright CLI.
- Desktop check at `1280x900`: page rendered with no visible text boxes
  offscreen or wider than the viewport.
- Mobile check at `390x844`: page rendered with no visible text boxes offscreen
  or wider than the viewport.
- Captured a mobile viewport screenshot under `.playwright-cli/`.
- Stopped the local server after the check.

## Follow-Ups

- Future phases can add a richer public-safe generated demo summary for an
  endpoint-to-route-to-surface trail, but must keep the route at concept level
  until checked-in demo evidence supports stronger wording.
