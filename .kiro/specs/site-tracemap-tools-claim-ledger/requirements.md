# Site TraceMap Tools Claim Ledger Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public `/claims/` or `/claim-ledger/` site page that lists major
public site claims, their public claim level, proof path, evidence status,
limitations, and explicit non-claims.

This is a spec-only site phase. It does not implement site code. The future page
is presentation and claim governance only: SQLite indexes, fact streams,
reports, analyzer logs, rule catalog entries, commit metadata, coverage labels,
and documented limitations remain the source of truth.

## Claim Boundaries

- The page may catalog public-safe claims already made or planned for the
  public site, including whether the wording is `shipped`, `demo`, `concept`,
  or `hidden`.
- The page must label the page itself as `Public claim level: concept`.
- The page must not claim that TraceMap proves runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, or complete product coverage.
- The page must not publish raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw `facts.ndjson`, raw `index.sqlite`, or
  raw analyzer logs.
- Rows for `hidden` claim-level wording or `hidden/internal` evidence-status
  wording must not disclose unreleased or internal capability names, route
  names, private sample identities, or hidden-export specifics, and must not
  disclose the count, cadence, sequencing, or in-flight status of hidden or
  internal capabilities. Prefer a single aggregate
  statement, such as `internal-only capabilities exist and are not publicly
  described`, over per-capability hidden rows. Represent hidden/internal claims
  abstractly or omit them entirely.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs, evidence tiers, coverage labels, limitations, and generated
  artifacts.

## Relationship to Existing Site Surfaces

The claim ledger governs public claim wording: claim level, wording status,
evidence status, proof-path reference, limitations, and non-claims. It
references but does not duplicate the proof path index
(`site-tracemap-tools-proof-path-index`), which organizes evidence trails, or
the capability matrix (`site-tracemap-tools-capability-matrix`), which maps
current capability status. Where a claim's proof trail or capability status
already exists on those surfaces, the ledger row links to them instead of
restating their evidence.

## Requirements

### Requirement 1: Define route and page placement

The future implementation shall add a public claim-ledger page or section using
an explicit route decision.

Acceptance criteria:

- The implementation chooses either `/claims/` or `/claim-ledger/` and records
  the route decision in `implementation-state.md`.
- The chosen page says `Public claim level: concept`.
- The page is discoverable from the proof path index page and/or the capability
  matrix page where a link helps readers verify public wording. Link text must
  not imply concept or hidden claims are shipped capabilities.
- If the page is implemented as a standalone route, it is included in sitemap
  metadata and any existing static discovery metadata.
- The route/page placement does not imply that concept or hidden claims are
  shipped capabilities.
- The page does not duplicate the proof path index or capability matrix; rows
  link to those surfaces for evidence trails and capability status where they
  already exist.
- Any alternate route that is not chosen is recorded as a rejected option in
  `implementation-state.md` with a short reason.

### Requirement 2: Publish a claim-level table

The future page shall present major public site claims in a structured table or
equivalent scannable layout.

Acceptance criteria:

- Each row includes claim label, current public claim level, evidence status,
  proof path, limitation, source-of-truth artifact family, and public wording
  status.
- Public claim levels use stable labels such as `shipped`, `demo`, `concept`,
  and `hidden`.
- Evidence status distinguishes evidence-backed, partial/reduced coverage,
  future-only, hidden/internal, and not-yet-backed wording without inflating any
  status into a stronger product claim.
- Public wording status distinguishes wording that is live, demo-only,
  future-facing, hidden from public navigation, or forbidden.
- The table groups or filters rows so managers, reviewers, bots, and future
  agents can quickly distinguish shipped/demo/concept/hidden wording.
- Rows whose proof path is unavailable or future-only are labeled as such and
  are not described as evidence-backed.
- Rows whose public claim level is `hidden` or whose evidence status is
  `hidden/internal` do not name the underlying unreleased capability, internal
  route, private sample, or hidden-export detail; they use an abstract
  placeholder label and a limitation instead, and the ledger does not reveal
  the number or release cadence of hidden/internal capabilities.
- A `hidden` claim row or `hidden/internal` evidence-status row has no
  capability-matrix or proof-path index counterpart; a `dev-only` capability
  already shown publicly on those surfaces is `concept`, not `hidden`.
- The three row axes are orthogonal: public claim level says how strongly the
  public site may present the claim, evidence status says what public-safe proof
  exists, and public wording status says where the wording appears or whether it
  is forbidden.

### Requirement 3: Link proof paths without leaking private material

The future page shall link claim rows to public-safe proof paths while keeping
raw evidence artifacts local unless sanitized output is explicitly public.

Acceptance criteria:

- Proof paths link to checked-in public pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, or demo artifacts that are safe to
  publish.
- Proof paths do not link directly to raw `facts.ndjson`, raw `index.sqlite`,
  raw analyzer logs, raw source snippets, raw SQL, raw config values, secrets,
  local absolute paths, raw remotes, generated scan directories, or private
  sample names.
- When the source of truth is local-only SQLite, facts, reports, or rule catalog
  material, the row names the artifact family and links only to a public-safe
  summary or route.
- Rule IDs and evidence tiers are shown when public-safe and specific; otherwise
  the row uses a rule-family reference with a limitation.
- Proof-path link text is stable enough for link checking and future automated
  claim review.
- Missing or future proof paths remain visible as limitations rather than being
  hidden or treated as shipped evidence.

### Requirement 4: State limitations and non-claims

The future page shall make non-claims explicit so public readers and future
agents do not overstate TraceMap.

Acceptance criteria:

- The page includes an explicit non-claims section.
- The non-claims section says TraceMap does not prove runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI impact analysis, LLM analysis, or complete product
  coverage.
- The page explains that SQLite, fact streams, reports, rule catalog entries,
  commit metadata, coverage labels, and documented limitations are the source
  of truth.
- The page states that the claim ledger is a presentation/governance layer, not
  a replacement for deterministic scanner or reducer outputs.
- Limitations are attached to each claim row when a claim is demo-only,
  concept-only, partial, reduced, hidden, or unsupported.
- Limitations are not phrased as marketing disclaimers that obscure whether
  evidence exists.

### Requirement 5: Support public discovery metadata and automation

The future implementation shall expose enough metadata for readers, link
checkers, and future claim-review bots to inspect the ledger.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical path, and
  claim-level/discovery tags consistent with existing site metadata patterns.
- Sitemap and discovery outputs include the route if implemented as a
  standalone page.
- Claim rows use stable identifiers or anchors suitable for automated
  references.
- Machine-checkable labels use the same public claim-level and evidence-status
  vocabulary across rows.
- Claim-level and evidence-status vocabulary is reconciled with existing site
  vocabularies via a single mapping table that lists every term from both the
  claim-ledger and existing-surface vocabularies, and resolves each
  existing-surface status to exactly one claim-ledger label. The mapping table
  is defined once and is the single source for cross-page label comparison.
- The required starting mapping is:

  | Claim-ledger label | Capability matrix status | Proof-path index reference |
  | --- | --- | --- |
  | `shipped` | `main` | checked-in public artifact or resolvable public proof path |
  | `demo` | `demo` | checked-in demo artifact or per-entry demo status |
  | `concept` | `dev`, publicly shown as `dev-only`, or `future`, publicly shown as `future` | `dev-only` marker, `future`-labeled future-only entry, or no proof-path-index entry |
  | `hidden` | no capability-matrix row; omit or abstract | no proof-path-index entry; omit or abstract |

- The mapping is a function from each existing-surface status to exactly one
  claim-ledger label, not a bidirectional one-to-one map: `main` resolves to
  `shipped`, `demo` resolves to `demo`, `future` resolves to `concept`, and
  `dev` or public `dev-only` resolves to `concept`. `hidden` has no public
  capability-matrix or proof-path-index counterpart by design.
- Capability-matrix `future` status and proof-path-index `future` entries are
  publicly shown as `future`, not `dev-only`; both still resolve to
  claim-ledger `concept`. Only dev-branch maturity is publicly shown as
  `dev-only`.

- The required starting evidence-status mapping is:

  | Evidence-status label | Existing-surface vocabulary | Claim-ledger notes |
  | --- | --- | --- |
  | `evidence-backed` | `Tier1Semantic` or `Tier2Structural` with `Full` or `FullEvidenceAvailable` coverage | Rule IDs, evidence tiers, and proof paths are public-safe and specific. |
  | `partial/reduced coverage` | `Partial`, `Reduced`, or `ReducedCoverage` coverage, or `Tier3SyntaxOrTextual` evidence | Rule-family or proof-path reference exists, but coverage gaps are labeled. |
  | `future-only` | Proof-path-index `future` entry; no resolvable artifact yet | Proof path is not yet available; row is not evidence-backed. |
  | `hidden/internal` | No capability-matrix or proof-path-index counterpart | Aggregate abstract placeholder; discloses no unreleased detail. |
  | `not-yet-backed` | `Tier4Unknown`, `UnknownAnalysisGap`, or no cited artifact | No proof path exists; future-facing claim only. |

- Each capability-matrix status, including `main`, `dev`, `demo`, and `future`,
  and the proof-path-index `dev-only` marker resolves to exactly one claim-level
  counterpart before automation relies on the ledger. `dev` and `dev-only`
  describe the same dev-branch-only maturity and must resolve to `concept`, not
  split across `concept` and `hidden`. A public claim with no capability-matrix
  row and no proof-path-index entry maps to `hidden` so the mapping stays total.
- The page links back to relevant public proof surfaces without creating
  circular wording that upgrades a concept or hidden claim.
- Page-level discovery metadata, including title, description, canonical path,
  and claim-level/discovery tags, explicitly carries the `concept` claim-level
  signal so discovery tools and automated reviewers do not classify the page as
  a shipped capability page.
- If the ledger is exposed to LLM discovery or bot-oriented discovery surfaces,
  concept and hidden rows are explicitly marked so machine consumers do not
  re-present them as shipped capability.
- Future implementation records any metadata schema or discovery-file changes
  in `implementation-state.md`.

### Requirement 6: Validate forbidden overclaims and private text

The future implementation shall prove that the ledger does not introduce
overclaims or private material.

Acceptance criteria:

- Review copy for forbidden claims about runtime proof, production traffic,
  endpoint performance, outage cause, release safety, operational safety, AI
  impact analysis, LLM analysis, and complete product coverage.
- Review proof paths for raw source snippets, raw SQL, config values, secrets,
  local absolute paths, raw remotes, generated scan directories, private sample
  names, raw facts, raw SQLite indexes, and raw analyzer logs.
- Verify that every claim-level and evidence-status label used on the ledger
  resolves through the Requirement 5 mapping table, so cross-page label
  comparison against the capability matrix and proof path index stays
  consistent.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Confirm `./scripts/check-private-paths.sh` exists before treating it as a
  validation gate; if absent, record the gap in `implementation-state.md` and
  add script creation as a follow-up task before closing validation.
- Run the site build and validation commands used by the surrounding site
  specs when site source changes are made.
- Run desktop and mobile browser sanity checks if layout or interaction changes
  are made.

### Requirement 7: Keep implementation state current

The future implementation shall update the spec-local implementation state as
claim-ledger work proceeds.

Acceptance criteria:

- `implementation-state.md` records current branch, route decision, rejected
  route option, scope decisions, validation commands and results, claim-boundary
  decisions, and follow-up items.
- `implementation-state.md` must not contain local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, or raw analyzer log content;
  record path references as relative paths or artifact-family names.
- Review findings and any Medium or higher patches are summarized in
  `implementation-state.md`.
- Tasks are checked only after the corresponding implementation work and
  validation are complete.
- If implementation is partial, `implementation-state.md` labels it partial and
  records which claim rows, metadata, links, or validations remain incomplete.
