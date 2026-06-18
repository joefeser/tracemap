# Site TraceMap Tools Manager Problem Brief Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Branch

Implementation branch: `codex/site-manager-problem-brief-impl`

## Scope

This branch implements the concept-level `/manager-brief/` public site route
from the site manager/problem brief spec. It adds the page, sitemap metadata,
discovery metadata, cross-links, focused validation, tests, and completed spec
state.

The page explains the coordination problem TraceMap is meant to reduce:
manual dependency questions, cross-team review pressure, partial static
evidence, and the cost of reconstructing dependency context during review.
It does not add runtime monitoring, production telemetry, release approval,
or core scanner/reducer behavior.

## Implemented

- Added `site/src/manager-brief/index.html`.
- Added `/manager-brief/` to `site/src/_site/pages.json`.
- Added `/manager-brief/` discovery metadata with `publicClaimLevel: concept`.
- Linked the route from `/manager-packet/` and `/use-cases/`.
- Added sanitized public examples using actual TraceMap rule IDs,
  evidence-tier labels, coverage/limitation framing, and public proof paths.
- Added `site/scripts/manager-brief.mjs` with route, sitemap,
  `routes-index.json`, required-copy, required-link, word-count,
  forbidden-positioning, and forbidden private/raw artifact text checks.
- Added `site/scripts/manager-brief.test.mjs`.
- Wired manager-brief validation into `site/scripts/validate.mjs`.
- Updated `site/scripts/validate.test.mjs` fixtures for the new validation.
- Marked the spec tasks as complete.

## Claim Boundaries

- Safe to say: the site now publishes a concept-level manager problem brief
  that explains how deterministic static evidence packets can reduce manual
  dependency-indexing and review burden.
- Safe to say: the route links readers to proof paths, validation,
  limitations, demo, docs, manager packet, incident-call orientation,
  capability matrix, and roadmap pages.
- Safe to say: sanitized examples can mention public rule IDs, evidence tiers,
  coverage labels, supporting IDs, and limitations.
- Not safe to say: TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  AI impact analysis, LLM analysis, or complete product coverage.
- Public copy avoids raw fact streams, SQLite indexes, analyzer logs, source
  excerpts, raw SQL, config values, secrets, local paths, repository remotes,
  scan directories, and private sample identities.

## Validation

Passed before PR:

- `git diff --check`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- `./scripts/check-private-paths.sh`

Browser sanity:

- Served the site locally with `PORT=4184 npm run dev`.
- Opened `http://localhost:4184/manager-brief/` with the Playwright CLI.
- Desktop check: page rendered with no visible text boxes offscreen or wider
  than the viewport at `1280x900`.
- Mobile check: page rendered with no visible text boxes offscreen or wider
  than the viewport at `390x844`.
- Stopped the local server after the check.

## Follow-Ups

- Future phases can add richer public-safe generated demo summaries for
  manager-facing packets, but `/manager-brief/` should remain concept-level
  until checked-in demo evidence supports stronger wording.
