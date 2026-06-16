# Site TraceMap Tools Incident Review Use Case Implementation State

Status: implemented
Last verified: 2026-06-15
Current branch: codex/site-incident-review-use-case
Source of truth: implemented site page and spec packet
Public claim level: concept

## Summary

Implemented the public site route `/use-cases/incident-review/` as a
concept-level use case. The page explains how TraceMap static evidence can
orient review or incident-follow-up code questions without claiming runtime
proof, production state, endpoint performance, production dependency
understanding, P1 root cause, release safety, release approval, or AI impact
analysis.

The implementation also adds the sitemap/page metadata entry and bounded
cross-links from existing public pages so the new route is discoverable from the
use-case index, manager packet, packet guide, demo proof pages, capabilities,
limitations, and roadmap.

## Scope Completed

- Added `site/src/use-cases/incident-review/index.html`.
- Added the required `site/src/_site/pages.json` entry:
  `{ "path": "/use-cases/incident-review/", "changefreq": "monthly", "priority": "0.7" }`.
- Added role-specific reader questions for engineers, reviewers, managers, and
  architects.
- Added proof-path links to `/use-cases/`, `/manager-packet/`, `/packets/`,
  `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`,
  `/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`.
- Kept the new page's placeholder `<header class="site-header">` block so
  `site/scripts/build.mjs` can replace navigation with the canonical header.
- Confirmed no `site/dist/` or `site/output/` generated output was hand-edited.

## Conditional Cross-Links Added

- `/demo/proof-assets/`: Added "For incident-adjacent readers, use visuals only
  to orient." The copy says visuals help recognize static evidence shapes before
  inspecting generated reports and do not prove runtime cause, production
  behavior, endpoint performance, or release safety.
- `/demo/proof-upgrades/`: Added "Incident review is still concept-level." The
  copy says demo rows are static evidence and limitation examples only, and do
  not upgrade incident follow-up into runtime proof, production dependency
  understanding, P1 root cause, or release approval.
- `/limitations/`: Added "Incident-adjacent use does not relax these limits."
  The copy reinforces that static evidence routes code questions and does not
  infer runtime traffic, endpoint performance, deployment state, production
  cause, release safety, or approval.
- `/roadmap/`: Added the use case to the current public surface as `Status:
  concept` and to the next proof-upgrades list with explicit gates before it can
  move beyond concept.

## Boundaries Preserved

- No claim that TraceMap proves runtime behavior, production traffic,
  deployment state, endpoint performance, production dependency understanding,
  P1 root cause, release safety, release approval, or AI impact analysis.
- No implication that TraceMap replaces Dynatrace, logs, traces, incident
  response, ownership, tests, human review, code review approval, or release
  approval.
- No raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw repo remotes, raw `facts.ndjson`, `index.sqlite`, combined SQLite files,
  generated scan directories, analyzer logs, or private sample identities were
  added to public copy.
- The page remains `concept` and should not be upgraded to `demo` until a future
  checked-in public-safe demo slice supports route-specific examples.

## Validation

- Passed `git diff --check`.
- Passed `npm test` from `site/` with 32 passing tests.
- Passed `npm run validate` from `site/`; validation built the static site and
  checked 28 HTML files, 650 internal references, and 27 sitemap URLs.
- Passed `npm run build` from `site/`.
- Passed desktop browser sanity for `/use-cases/incident-review/` at
  1440x1000: canonical header present, no horizontal overflow, visible
  concept-level hero note, and no missing required proof-path links.
- Passed mobile browser sanity for `/use-cases/incident-review/` at 390x844:
  no horizontal overflow, visible concept-level copy, non-claims, and artifact
  safety language.
- Passed focused browser sanity for `/use-cases/`: incident review orientation
  card and link are visible, and the page has no horizontal overflow.
- Completed manual copy review for claim level, non-claims, artifact safety,
  static-evidence framing, and proof-path links.

## Follow-Ups

- Consider a future route-specific public demo slice only if it can remain
  public-safe and reproduce a bounded incident-review packet from checked-in
  samples.
- If build navigation logic changes later, consider adding a focused
  `currentNavValue` unit test for nested `/use-cases/*/` routes.
