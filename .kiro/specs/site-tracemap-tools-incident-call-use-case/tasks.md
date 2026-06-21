# Site TraceMap Tools Incident Call Use Case Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: implementation tasks should be completed before validation
tasks. Each future implementation commit should keep the site buildable and
reviewable, with validation-only commits made after the route and metadata
exist.

## Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, and initial implementation status before changing site code.
- [x] Add a bounded `/incident-call/` concept page or article.
- [x] Add copy for the P1 incident-call reader who needs static dependencies
  and proof paths around an endpoint or surface.
- [x] Add the `Public claim level: concept` label and shared site principle.
- [x] State the static evidence boundaries and explicitly avoid runtime,
  production traffic, endpoint performance, outage cause, APM replacement,
  release safety, and operational safety claims.
- [x] Link the page to public-safe proof paths, validation, docs, limitations,
  and demo/result surfaces where those routes exist.
- [x] Add disambiguation cross-links between `/incident-call/` and
  `/use-cases/incident-review/` explaining P1-call orientation versus broader
  post-incident review orientation.
- [x] Add public-safe examples using generated summaries or demo evidence,
  not raw facts, SQLite indexes, analyzer logs, source snippets, SQL, config,
  secrets, local paths, raw remotes, generated scan directories, or private
  sample identities.
- [x] Add title, description, canonical URL, and Open Graph metadata.
- [x] Add discovery metadata for the route with claim level `concept`.
- [x] Add sitemap and route-index coverage for the route.
- [x] Add validation for required copy, claim labels, proof-path links,
  limitations links, and forbidden private artifact text including raw artifact
  names, `.tracemap` paths, raw SQL patterns, and local absolute path patterns.
- [x] Add a validation check that required internal proof-path links resolve in
  generated site output.

## Validation Tasks

- [x] Run `git diff --check`.
- [x] Run focused site tests or route tests if available.
- [x] Run `npm run validate` from `site/`.
- [x] Confirm `npm run validate` is available; no fallback forbidden-text
  check was needed.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for the route.
- [x] Update `implementation-state.md`.
