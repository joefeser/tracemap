# Implementation State

Status: implemented
Readiness: implemented
Last verified: 2026-06-18
Branch: codex/impl-site-claim-ledger
Worktree: dedicated implementation worktree; absolute path omitted from this note
Source of truth: `/roadmap/` source in `site/src/roadmap/index.html`
Public claim level: concept

## Summary

The claim-ledger concept is implemented by extending the existing `/roadmap/`
route. The roadmap remains a concept-level public claim governance surface and
now includes a structured claim ledger, one total vocabulary mapping table,
explicit non-claims, route/discovery metadata, and validation coverage.

This is site work only. Scanner, reducer, adapter, and core CLI code were not
changed.

## Scope

- Extended `/roadmap/` with a claim-ledger table covering claim label, public
  claim level, evidence status, proof path, limitation, source-of-truth
  artifact family, and public wording status.
- Added stable row anchors and machine-readable `data-*` labels for future
  automated claim review.
- Added a single mapping table for claim-level and evidence-status vocabulary
  across roadmap, capability matrix, proof path index, and discovery metadata
  terms.
- Kept `hidden` and `hidden/internal` wording abstract through one aggregate
  row that does not disclose unreleased names, private samples, counts, cadence,
  sequencing, or in-flight status.
- Updated `/capabilities/` and `/proof-paths/` with bounded links to the
  canonical claim ledger.
- Updated roadmap discovery metadata so generated route indexes retain
  `publicClaimLevel: concept` and use `/proof-paths/` as the preferred proof
  path.
- Added `site/scripts/roadmap-claim-ledger.mjs` and tests, and wired the
  validator into `site/scripts/validate.mjs`.

## Route Decision

Selected placement: extend `/roadmap/`.

Reason: `/roadmap/` already described itself as the public claim ledger and was
already present in sitemap and discovery metadata with concept-level claim
metadata. Extending it avoids creating two competing claim-governance sources
and keeps the proof path index and capability matrix as linked evidence/status
surfaces instead of duplicating them.

Rejected options:

- `/claims/`: rejected because it would create another governance route while
  `/roadmap/` already owns the concept.
- `/claim-ledger/`: rejected for the same reason, and because no standalone
  route was needed to satisfy discovery or validation.

Standalone route metadata note: no new route was created, so sitemap coverage
continues through the existing `/roadmap/` entry. Discovery metadata for the
existing route was updated with concept-level claim-ledger wording.

## Claim-Boundary Decisions

- The ledger names source-of-truth artifact families but links only to
  public-safe routes or public repository documentation.
- The ledger states that SQLite indexes, fact streams, reports, rule catalog
  entries, commit metadata, coverage labels, and documented limitations remain
  the source of truth.
- Public copy does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The hidden/internal row is aggregate-only and has no capability-matrix or
  proof-path-index counterpart.

## Validation

Passed on 2026-06-18:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `npm test` from `site/`:
  135 tests passed after the Qodo patches.
- `npm run validate` from `site/`:
  built static output and validated 38 HTML files, 1063 internal references,
  37 sitemap URLs, and 1 legacy story safety target.
- `npm run build` from `site/`:
  built static site output.

Browser sanity checks:

- Desktop viewport 1280x900 on `/roadmap/#claim-ledger`: no page-level
  horizontal overflow; 7 claim rows and 10 mapping rows present; both ledger
  wrappers fit the viewport; console error count 0.
- Mobile viewport 390x844 on `/roadmap/#claim-ledger`: no page-level
  horizontal overflow; table overflow stays inside the ledger wrappers; 7 claim
  rows and 10 mapping rows present; console error count 0.

Oddity:

- The default dev-server port was already occupied during browser validation,
  so an alternate local dev-server port was used for the browser-only check.

## Review Findings

- PR loop stopped on one Qodo unresolved review thread for
  `site/scripts/roadmap-claim-ledger.mjs`: the private-text guard compared
  forbidden text with case-sensitive checks. Patched the validator to compare
  HTML, decoded HTML, and rendered text case-insensitively, and added a
  `LOCALHOST` regression test.
- Qodo also recommended tightening mapping-row correctness because unexpected
  `data-ledger-label` values were not rejected when required labels were still
  present. Patched per-axis label validation and added a typo-row regression
  test.

## Follow-Ups

- None for this implementation phase.
