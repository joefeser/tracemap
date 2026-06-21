# Site TraceMap Tools Claim Ledger Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

- [x] Evaluate extending `/roadmap/` as the canonical claim-ledger surface
  before choosing `/claims/` or `/claim-ledger/` as a standalone route.
- [x] Record the selected placement and rejected alternates, including
  `/roadmap/` if it is not chosen, in `implementation-state.md`.
- [x] Add the public claim-ledger page or section using existing site styles and
  static-site patterns.
- [x] Add page-level copy that says `Public claim level: concept`.
- [x] Add a claim-level table with claim label, public claim level, evidence
  status, proof path, limitation, source-of-truth artifact family, and public
  wording status.
- [x] Use stable public claim-level labels such as `shipped`, `demo`,
  `concept`, and `hidden`.
- [x] Define one total mapping table for claim-level and evidence-status labels
  against existing site vocabulary from the roadmap, capability matrix,
  proof-path index, and route/discovery metadata, using real proof-path-index
  vocabulary such as page claim level `demo`, artifact types, evidence tiers,
  coverage labels, and the `dev-only` marker rather than invented tokens.
  Resolve capability matrix `dev` and proof-path index `dev-only` to the same
  claim-level label, keep `future` publicly shown as `future` distinct from
  dev-branch maturity publicly shown as `dev-only`, map roadmap labels such as
  `shipped navigation`, `demo guidance`, `main/demo`, `future`, and `hidden
  pending validation`, map coverage labels such as `PartialAnalysis`, and map
  gap labels such as `not_requested` and `unavailable` to evidence-status
  labels so automated cross-page review cannot get contradictory answers for
  the same claim.
- [x] Use evidence-status labels that distinguish evidence-backed,
  partial/reduced coverage, future-only, hidden/internal, and not-yet-backed
  wording.
- [x] Ensure `hidden` claim rows and `hidden/internal` evidence-status rows use
  abstract placeholder labels and do not disclose unreleased capability names,
  internal routes, private sample identities, hidden-export specifics, counts,
  cadence, sequencing, or in-flight status; prefer a single aggregate row over
  one row per hidden item.
- [x] Link proof paths only to public-safe routes, generated summaries,
  documentation, rule catalog pages, reports, or demo artifacts.
- [x] For local-only SQLite, facts, reports, or rule catalog source material,
  name the artifact family and link only to a public-safe summary or route.
- [x] Add explicit per-row limitations for demo-only, concept-only, partial,
  reduced, hidden, or unsupported claims.
- [x] Add a non-claims section that rejects runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, and complete product coverage
  claims.
- [x] Ensure no row or surrounding copy publishes raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, or raw
  analyzer logs.
- [x] Add stable row identifiers or anchors for future automated claim review.
- [x] Verify every claim-level and evidence-status label used on the ledger
  resolves through the mapping table, including evidence-status links to
  evidence tiers and coverage labels.
- [x] Add route metadata, discovery metadata, and sitemap coverage if the
  ledger is implemented as a standalone page.
- [x] Ensure page-level metadata carries the `concept` claim-level signal so
  discovery tools and automated reviewers cannot classify the page as shipped.
- [x] Mark concept and hidden rows explicitly in any LLM discovery or
  bot-oriented discovery surface so machine consumers do not re-present them as
  shipped capability.
- [x] Link to the proof path index and capability matrix where they already
  provide evidence trails or capability status instead of duplicating those
  surfaces.
- [x] Link from relevant public proof or governance surfaces without upgrading
  concept, hidden, demo-only, or future-only claims.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm `./scripts/check-private-paths.sh` exists before closing the
  validation task, and record a follow-up if it is absent.
- [x] Run the site build and validation commands used by surrounding site specs
  after any site source changes.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [x] Update `implementation-state.md` with route decisions, validation results,
  review findings, claim-boundary decisions, partial-work labels if applicable,
  and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, and raw analyzer log content.
