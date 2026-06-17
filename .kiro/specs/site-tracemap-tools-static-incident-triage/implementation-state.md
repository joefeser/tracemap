# Site TraceMap Tools Static Incident Triage Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Branch

Implementation branch: `codex/site-static-incident-triage`

Base: `origin/main`

## Scope

This phase adds a concept-level `/static-triage/` public site page for
engineers and incident leads on a P1 or production call. The page focuses on a
static code evidence checklist and handoff questions around a named endpoint,
package, configuration surface, SQL-facing surface, or nearby dependency
surface.

This phase does not add scanner/reducer behavior, runtime monitoring,
telemetry, incident diagnosis, release approval, operational safety claims,
AI/LLM impact analysis, or complete product coverage claims.

## Spec Review Plan

Requested before implementation:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-incident-triage --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-incident-triage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results before implementation:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-incident-triage --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 after producing reduced-coverage review artifacts because Kiro
  reported denied tool access. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-static-incident-triage/2026-06-17T215528-796Z-spec-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-static-incident-triage/2026-06-17T215528-796Z-spec-claude-opus-4.8.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-incident-triage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 after producing full-coverage review artifacts. The review noted
  that implementation files were not expected to exist yet and requested two
  Medium spec tightenings: an enforceable engineer-checklist distinction phrase
  and a required safe reciprocal cross-link from `/incident-call/`. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-static-incident-triage/2026-06-17T215740-019Z-spec-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-static-incident-triage/2026-06-17T215740-019Z-spec-claude-sonnet-4.6.meta.json`.
- Patched the Medium spec findings before site implementation.

## Implemented

- Created the spec directory and initial requirements, tasks, implementation
  state, and review packet.
- Ran pre-implementation Kiro spec reviews and patched Medium findings for the
  engineer-checklist distinction phrase and required incident-call reciprocal
  cross-link before coding began.
- Ran a Sonnet re-review after those patches. The re-review completed with
  full coverage and no remaining spec-only blocker; its remaining High findings
  are implementation tasks that this branch will address.
- Added `site/src/static-triage/index.html`.
- Added `/static-triage/` to `site/src/_site/pages.json`.
- Added `/static-triage/` discovery metadata with `publicClaimLevel: concept`.
- Added a safe `/incident-call/` cross-link to the static triage checklist.
- Added `site/scripts/static-triage.mjs` with route, sitemap,
  `routes-index.json`, required-copy, required-link, word-count,
  forbidden-positioning, and forbidden-private-text checks.
- Added `site/scripts/static-triage.test.mjs`.
- Wired static triage validation into `site/scripts/validate.mjs`.
- Updated `site/scripts/validate.test.mjs` fixtures for the new validation.
- Updated `site/scripts/incident-call.mjs` and
  `site/scripts/incident-call.test.mjs` so the reciprocal checklist link is
  checked.
- Marked the spec tasks as complete.

## Claim Boundaries

- Safe to say: this phase frames a concept-level static incident triage
  checklist and handoff page.
- Safe to say: TraceMap can organize static evidence questions with rule IDs,
  evidence tiers, coverage labels, limitations, file paths, line spans, commit
  SHA, extractor versions, and public-safe generated artifact families when
  those are available in public-safe summaries.
- Not safe to say: TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete product coverage, AI impact analysis, or LLM impact analysis.
- Public copy must not expose raw fact streams, SQLite indexes, analyzer logs,
  raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, or private sample identities.

## Validation

Run before PR:

- `git diff --check` passed.
- `cd site && npm test` passed with 84 tests.
- `cd site && npm run validate` passed: 34 HTML files, 903 internal
  references, 33 sitemap URLs, and 1 legacy story safety target.
- `cd site && npm run build` passed.
- `./scripts/check-private-paths.sh` passed.

Browser sanity:

- Served the site locally with `PORT=4184 npm run dev`.
- Opened `http://localhost:4184/static-triage/` with the Playwright CLI.
- Desktop check at `1280x900`: snapshot rendered the static triage page, and a
  DOM overflow check reported `overflowCount: 0`.
- Mobile check at `390x844`: snapshot rendered the static triage page, and a
  DOM overflow check reported `overflowCount: 0`.
- Stopped the local server and closed the browser after the check.

Oddity:

- A parallel final sweep of `npm run validate` and `npm run build` caused a
  transient `site/dist` read race because both commands rewrite generated
  output. Reran `npm run validate && npm run build` sequentially, and both
  passed.

## Follow-Ups

- None yet.
