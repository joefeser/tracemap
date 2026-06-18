# Site TraceMap Tools Claim Ledger Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are future implementation work. They remain unchecked because this
phase is spec-only.

Note: validation tasks must pass before future implementation tasks are checked
complete.

- [ ] Choose `/claims/` or `/claim-ledger/` as the final route/page placement.
- [ ] Record the selected route and rejected alternate route in
  `implementation-state.md`.
- [ ] Add the public claim-ledger page or section using existing site styles and
  static-site patterns.
- [ ] Add page-level copy that says `Public claim level: concept`.
- [ ] Add a claim-level table with claim label, public claim level, evidence
  status, proof path, limitation, source-of-truth artifact family, and public
  wording status.
- [ ] Use stable public claim-level labels such as `shipped`, `demo`,
  `concept`, and `hidden`.
- [ ] Define one total mapping table for claim-level and evidence-status labels
  against existing site vocabulary, using real proof-path-index vocabulary such
  as page claim level `demo`, artifact types, evidence tiers, coverage labels,
  and the `dev-only` marker rather than invented tokens. Resolve capability
  matrix `dev` and proof-path index `dev-only` to the same claim-level label,
  keep `future` publicly shown as `future` distinct from dev-branch maturity
  publicly shown as `dev-only`, and map evidence-status labels to real
  evidence-tier and coverage-label vocabulary so automated cross-page review
  cannot get contradictory answers for the same claim.
- [ ] Use evidence-status labels that distinguish evidence-backed,
  partial/reduced coverage, future-only, hidden/internal, and not-yet-backed
  wording.
- [ ] Ensure hidden/internal rows use abstract placeholder labels and do not
  disclose unreleased capability names, internal routes, private sample
  identities, hidden-export specifics, counts, cadence, sequencing, or in-flight
  status; prefer a single aggregate row over one row per hidden item.
- [ ] Link proof paths only to public-safe routes, generated summaries,
  documentation, rule catalog pages, reports, or demo artifacts.
- [ ] For local-only SQLite, facts, reports, or rule catalog source material,
  name the artifact family and link only to a public-safe summary or route.
- [ ] Add explicit per-row limitations for demo-only, concept-only, partial,
  reduced, hidden, or unsupported claims.
- [ ] Add a non-claims section that rejects runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, and complete product coverage
  claims.
- [ ] Ensure no row or surrounding copy publishes raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, or raw
  analyzer logs.
- [ ] Add stable row identifiers or anchors for future automated claim review.
- [ ] Verify every claim-level and evidence-status label used on the ledger
  resolves through the mapping table, including evidence-status links to
  evidence tiers and coverage labels.
- [ ] Add route metadata, discovery metadata, and sitemap coverage if the
  ledger is implemented as a standalone page.
- [ ] Ensure page-level metadata carries the `concept` claim-level signal so
  discovery tools and automated reviewers cannot classify the page as shipped.
- [ ] Mark concept and hidden rows explicitly in any LLM discovery or
  bot-oriented discovery surface so machine consumers do not re-present them as
  shipped capability.
- [ ] Link to the proof path index and capability matrix where they already
  provide evidence trails or capability status instead of duplicating those
  surfaces.
- [ ] Link from relevant public proof or governance surfaces without upgrading
  concept, hidden, demo-only, or future-only claims.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Confirm `./scripts/check-private-paths.sh` exists before closing the
  validation task, and record a follow-up if it is absent.
- [ ] Run the site build and validation commands used by surrounding site specs
  after any site source changes.
- [ ] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [ ] Update `implementation-state.md` with route decisions, validation results,
  review findings, claim-boundary decisions, partial-work labels if applicable,
  and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, and raw analyzer log content.
