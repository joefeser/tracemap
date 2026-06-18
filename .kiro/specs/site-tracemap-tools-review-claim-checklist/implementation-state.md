# Implementation State

Status: implemented
Readiness: ready-for-review
Last verified: 2026-06-18
Branch: codex/impl-site-review-claim-checklist
Worktree: dedicated implementation worktree; local absolute path omitted from
public spec notes
Base: origin/dev
Public claim level: concept

## Summary

Implemented the public-safe reviewer claim checklist as a standalone
concept-level site route. The page turns TraceMap's claim boundary into a
repeat-before-reuse ritual: before a public claim or internal review statement
is repeated, the reviewer checks claim level, proof path, rule ID or rule
family, evidence tier, coverage label, limitation, non-claims, source status,
owner follow-up, reviewer, review date, and decision.

This is site work only. No scanner, reducer, adapter, or core implementation
code was changed.

## Route Decision

- Selected `/review-claim-checklist/`.
- Rejected `/claim-checklist/` because the selected route is more explicit
  about the review ritual and aligns with adjacent `/review-room/` wording.
- Rejected section placement on an existing governance page because the spec
  requires standalone discovery metadata, sitemap metadata, stable anchors,
  inbound validation, and a dedicated checklist validator.
- The route is intentionally not added to top navigation; existing site
  navigation stays high-level, and discovery is through bounded links from
  adjacent governance/proof routes.

## Implemented Surface

- Added `site/src/review-claim-checklist/index.html`.
- Added sitemap metadata in `site/src/_site/pages.json`.
- Added discovery metadata in `site/src/_site/discovery.json` with
  `publicClaimLevel: concept`, `sourceType: site-page`,
  `hintCategory: use-case`, and `preferredProofPath: /proof-paths/`.
- Added bounded inbound links from `/review-room/`, `/manager-faq/`,
  `/proof-paths/`, and `/roadmap/`.
- No referenced adjacent route needed substitution or omission at
  implementation time.
- Added stable anchors for the ritual, row template, stop conditions,
  illustrative examples, non-claims, private material, and adjacent surfaces.
- Added synthetic example rows only; no real internal reviewer, owner, date,
  private sample, hidden capability, or in-flight detail is published.

## Claim Boundary Decisions

- The page remains `Public claim level: concept`.
- Checklist-row claim level uses the claim-ledger vocabulary:
  `shipped`, `demo`, `concept`, and `hidden`.
- Discovery metadata keeps the existing discovery enum and does not emit
  `shipped` as a metadata value.
- Review outcomes use only `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- The page says the checklist does not prove runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, or complete product coverage.
- The page names raw artifact families only inside private-material boundary
  copy and does not link to raw facts, SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, or private sample names.

## Validation

Implementation validation run on 2026-06-18:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `npm test` from `site/`: passed, 160 tests.
- `npm run validate` from `site/`: passed; built static site and validated
  41 HTML files, 1224 internal references, 40 sitemap URLs, and 1 legacy story
  safety target.
- `npm run build` from `site/`: passed.
- After PR-loop review fixes, `npm test`, `git diff --check`,
  `./scripts/check-private-paths.sh`, `npm run validate`, and
  `npm run build` were rerun and passed. The first attempted parallel
  validate/build rerun raced on `site/dist`; the commands passed when rerun
  sequentially.
- Browser sanity check for `/review-claim-checklist/`: desktop 1440x1000 and
  mobile 390x844 both rendered the expected title, hero, checklist table, and
  illustrative examples.
- Browser no-horizontal-overflow evidence: desktop document scroll width
  1440 equals viewport width 1440; mobile document scroll width 390 equals
  viewport width 390, with no protruding elements outside intended scrollable
  table wrappers.

## Review Findings

- Prior spec reviews are recorded in the spec branch history above this
  implementation. No Medium or higher spec findings remained before
  implementation began.
- Implementation-time validation initially found one discovery metadata
  wording issue: concept metadata used shipped-strength wording. The wording
  was softened to main-backed/demo-backed support language.
- Implementation-time validation also found one overclaim-guard issue around
  the heading `Proof-safe routes`. The heading was changed to
  `Proof-bounded routes`.
- First PR-loop pass stopped with three unresolved Gemini review threads in
  `site/scripts/review-claim-checklist.mjs`: make `overclaimPattern` global,
  reuse it directly in `hasUnsanctionedOverclaim`, and allow whitespace around
  `=` in sanctioned-section regexes. All three were patched.

## Oddities

- The checklist complements adjacent pages instead of duplicating them:
  `/review-room/` remains the meeting agenda, `/manager-faq/` remains
  stakeholder explanation, `/proof-paths/` remains the evidence-trail index,
  and `/roadmap/#claim-ledger` remains the claim-level vocabulary source.
- Wide checklist tables use the existing `.claim-ledger-wrap` horizontal
  scroll pattern. Browser checks confirmed the document itself does not create
  horizontal overflow on mobile.

## Follow-ups

- No implementation follow-ups are currently open.
- Final PR review-loop decision should be recorded after the rerun completes.
