# Site TraceMap Tools Proof Source Catalog Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: demo

## Branch

Implementation branch: `codex/impl-site-proof-source-catalog`

Base: `origin/dev`

Scope: public site source under `site/src/`, site validation scripts under
`site/scripts/`, and this spec state/checklist.

## Placement Decision

Selected placement: standalone public route `/proof-source-catalog/`.

Rejected alternative: adding the catalog as another section of `/proof-paths/`.
That page already maps evidence trails by artifact family, rule, tier,
coverage, proof path, limitation, and public status. The catalog has a different
axis: route-to-source mapping for public wording. Keeping it standalone avoids
turning `/proof-paths/` into a competing claim ledger while still linking back
to `/proof-paths/` for evidence-trail detail.

The page references the future `site-tracemap-tools-claim-ledger` spec by name
without linking to an unpublished route. The catalog links outward to published
proof/governance routes instead of restating claim-ledger authority.

## Implemented Scope

- Added `/proof-source-catalog/` with page-level `Public claim level: demo`.
- Added nine catalog rows, including one hidden aggregate placeholder and no
  per-capability hidden detail.
- Added claim-level and evidence-status mapping tables.
- Added row anchors derived from route and claim label, with the reserved hidden
  aggregate anchor.
- Added route metadata to sitemap and discovery data with
  `publicClaimLevel: demo`.
- Added bounded cross-links from `/proof-paths/`, `/roadmap/`, `/capabilities/`,
  `/docs/`, `/validation/`, and `/limitations/`.
- Added `site/scripts/proof-source-catalog.mjs` and focused tests.
- Wired the validator into `site/scripts/validate.mjs`.

## Claim-Boundary Decisions

- Page-level `demo` is the maturity label for the catalog page itself. It is
  not a ceiling for row-level `shipped` rows and does not upgrade `concept` or
  `hidden` rows.
- Row-level `Public claim level` uses exactly `shipped`, `demo`, `concept`, or
  `hidden`.
- Published evidence status excludes `not-yet-backed`; that value appears only
  in the mapping table as a pre-publication blocker.
- Hidden work is represented by a single aggregate placeholder with route
  `hidden`, proof path sentinel `hidden`, and no names, counts, cadence,
  sequencing, or in-flight status.
- The validator treats `public-safe` as allowed boundary vocabulary while still
  rejecting standalone affirmative `safe` claims in claim fields.

## Validation

Completed after implementation:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- `npm test` from `site/` passed: 141 tests.
- `npm run validate` from `site/` passed: 39 HTML files, 1124 internal
  references, 38 sitemap URLs, and 1 legacy story safety target.
- `npm run build` from `site/` passed.
- Browser sanity for `/proof-source-catalog/` passed on desktop and mobile.
  Desktop viewport: document scroll width 1440, client width 1440, no horizontal
  overflow, 9 catalog rows. Mobile viewport: document scroll width 390, client
  width 390, no horizontal page overflow, 9 catalog rows; table wrappers scroll
  internally.

Word-count bound selected for row fields: 55 words for each row's
`limitation` and `allowedPublicWording` fields.

Status vocabulary enumerated from the current `/capabilities/`, `/roadmap/`,
and `/proof-paths/` rendered output by validation. Encountered status phrases
must map to the catalog claim-level mapping.

## Review Findings

PR loop on PR #205 returned `actionable_findings` with
`UNRESOLVED_REVIEW_THREADS` for one Gemini thread in
`site/scripts/proof-source-catalog.mjs`.

Finding: hidden aggregate validation built `hiddenText` from `limitation` and
`nonClaims` without nullish coalescing, which could interpolate the string
`undefined` if a field failed to extract.

Fix: patched `hiddenText` to use empty-string fallbacks for missing
`limitation` and `nonClaims`, then reran validation.

## Oddities

- Local preview port `4173` was already occupied, so browser sanity used an
  alternate local preview port.
- The first browser metric capture was rerun sequentially because concurrent
  Playwright commands shared one session and mixed viewport state. The final
  recorded desktop and mobile metrics are sequential and current.

## Follow-Ups

- If a future claim-ledger route ships, update the catalog to link to it where
  claim wording, claim level, evidence status, limitations, or non-claims are
  already governed there.
- If new public status vocabulary appears on `/capabilities/`, `/roadmap/`, or
  `/proof-paths/`, add it to the claim-level mapping with rationale before
  publishing.
