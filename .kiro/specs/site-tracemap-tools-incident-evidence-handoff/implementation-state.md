# Site TraceMap Tools Incident Evidence Handoff Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-incident-evidence-handoff`
Base branch: `origin/dev`
Target PR base: `dev`

## Scope

Implemented the concept-level public site route `/incident-evidence-handoff/`
as a static evidence handoff packet/checklist. The page answers what static
evidence can be brought, which proof path backs it, what it does not prove, and
who owns the next runtime, telemetry, logs, traces, APM, release, test,
service-owner, database-owner, or incident-command question.

Site code only changed. No scanner, reducer, runtime monitoring, incident
diagnosis, release approval, ownership automation, AI impact-analysis, or
production proof behavior was added.

## Route And Content Decisions

- Source route: `site/src/incident-evidence-handoff/index.html`.
- The route is not in top navigation.
- Sitemap metadata includes `/incident-evidence-handoff/`.
- Discovery metadata uses `publicClaimLevel: concept`, `hintCategory:
  use-case`, `sourceType: site-page`, and `preferredProofPath:
  /proof-paths/`.
- Neighboring route hints are expressed in page copy and existing discovery
  fields rather than a custom discovery field, because generated route indexes
  preserve the existing discovery schema.
- The page renders both required distinction lines exactly after whitespace
  normalization.
- The page includes checklist labels for static evidence, proof path, rule
  ID/evidence tier, coverage label, limitation, and next owner.
- The page includes required static-side rows for route existence, DTO shape,
  package reference, dependency edge, and SQL-facing reference.
- The page includes required runtime/release-side owner rows for telemetry,
  logs, traces, APM, release controls, tests, database ownership, service
  ownership, and incident command.
- Minimal reciprocal links were added from `/incident-call/`,
  `/static-triage/`, and `/review-room/` using incident evidence handoff packet
  wording.

## Neighbor Verification

- `/static-triage/` still describes itself as an engineer-facing static
  evidence checklist and handoff-adjacent page, so the required packet-vs-
  checklist distinction remains accurate.
- `/incident-call/` still describes itself as incident/P1 orientation using
  static dependency evidence, so the handoff packet distinction remains
  accurate.

## Validation

Completed:

- `git diff --check` passed.
- `npm test` from `site/` passed.
- `npm run validate` from `site/` passed.
- `npm run build` from `site/` passed.
- `./scripts/check-private-paths.sh` passed.
- Desktop browser sanity check for `/incident-evidence-handoff/` passed: locked
  copy and required links rendered, no horizontal overflow, no out-of-viewport
  main content, and no console errors.
- Mobile browser sanity check for `/incident-evidence-handoff/` passed at a
  narrow viewport: locked copy and required links rendered, no horizontal
  overflow, no out-of-viewport main content, and no console errors.

## Validation Coverage Added

- Added a focused route validator for `/incident-evidence-handoff/`.
- Validator checks route existence, sitemap entry, generated route-index fields,
  limitations/nonClaims metadata, required rendered copy, required links, page
  metadata, main-content internal route resolution, ownership rows, rendered
  word-count bounds, route-scoped positioning denylist phrases, and route-
  scoped private/raw artifact denylist phrases.
- Word count is computed from normalized visible text inside `<main>`, excluding
  head metadata, global top navigation, and footer text.
- Added focused validator tests for success, href spacing, missing locked copy,
  missing metadata, metadata regressions, missing links, unresolved links, word
  count, forbidden positioning, encoded private/raw text, and missing ownership
  rows.

## Oddities

- The route-specific denylist intentionally rejects exact exposure phrases such
  as `raw fact stream` and exact overclaim phrases such as `complete product
  coverage`. Page and discovery boundary copy therefore uses safer category
  wording while preserving the public-safety boundary.
- The first local dev-server attempt found the default port already in use, so
  browser sanity checks used an alternate local port. No site code depends on
  that port.

## Follow-Ups

- No known implementation follow-ups.
