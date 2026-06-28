# Site TraceMap Tools Proof Source Catalog Requirements

Status: implemented
Readiness: implemented
Public claim level: demo

## Summary

Define a future public-safe proof source catalog for `tracemap.tools`. The
catalog maps existing public site routes and major claim labels to the source
material that allows that public wording: route, required public claim level,
proof path, source artifact or source document, rule ID or rule family where
available, evidence tier or coverage label where available, limitation, and
explicit non-claims.

This is a spec-only site phase. It does not implement site code. The future page
or section is an index and orientation layer for managers, reviewers, and bots
asking: what public page is allowed to say this, and what evidence backs it?
SQLite indexes, fact streams, reports, rule catalog entries, checked-in source
docs, route metadata, coverage labels, and documented limitations remain the
source of truth.

The catalog page itself may be `demo` because it indexes current checked-in
public site metadata, public-safe demo summary rows, public routes, and
repository source documents. The page-level `demo` label reflects the maturity
of the catalog page itself as a new, unvalidated public surface, not the claim
strength of the rows it indexes; future validation may promote it to `shipped`
once the page is stable and fully validated. Row-level claims may still be
`shipped`, `demo`, `concept`, or `hidden`; a row-level `concept` or `hidden`
value must not be promoted by the page-level `demo` label.
A `shipped` row on a demo-labeled page retains its shipped status; the
page-level label reflects the maturity of the catalog itself, not a ceiling on
individual row claim levels. Row-level shipped rows are not downgraded by the
page label.

## Claim Boundaries

- The page must say `Public claim level: demo`.
- Each catalog row must include the required field `Public claim level` with
  exactly one value from `shipped`, `demo`, `concept`, or `hidden`.
- The page may index current public site routes, checked-in route metadata,
  public-safe demo summaries, public-safe report-family names, repository docs,
  and rule catalog references.
- The page must not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The page must not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, or counts/details that identify hidden private work.
- The page must keep SQLite, facts, reports, rule catalog entries, route
  metadata, repository source docs, coverage labels, and documented limitations
  as source-of-truth material. The catalog may point to public-safe summaries or
  source document links, but it must not replace those artifacts.
- The page must not describe hidden work with per-capability names, unreleased
  routes, private repository names, counts, cadence, sequencing, or in-flight
  status. Hidden material is represented only as an aggregate placeholder or is
  omitted.

## Relationship to Existing Site Surfaces

The proof source catalog is not the proof path index, the roadmap, the
capability matrix, or the claim ledger. The proof path index (`/proof-paths/`)
organizes public-safe evidence trails by artifact, rule, tier, coverage, proof
path, and public status. The roadmap (`/roadmap/`) explains claim gates and how
wording moves from concept to demo or shipped. The capability matrix
(`/capabilities/`) lists capability status and proof references. The claim
ledger (spec `site-tracemap-tools-claim-ledger`, future `/claims/` or
`/claim-ledger/`) governs claim wording, claim level, evidence status,
limitations, and non-claims for individual public claims.

The catalog's distinct axis is route-to-source mapping: which route is allowed
to make a claim and which source material backs that route's wording. To avoid
a competing claim-governance surface, the catalog must reference the claim
ledger and proof path index for claim wording and evidence trails rather than
restating them, and must link to those surfaces wherever a row's claim level or
evidence status already exists there.

The catalog's job is narrower: it answers which route is allowed to make a
claim and which source material backs that route's wording. Future
implementation should either publish a standalone route at
`/proof-source-catalog/` or add a clearly labeled section to `/proof-paths/`.
If a standalone route is chosen, it must link to `/proof-paths/`, `/roadmap/`,
`/capabilities/`, `/docs/`, `/validation/`, and `/limitations/` where those
routes exist.

## Requirements

### Requirement 1: Choose the catalog placement

The future implementation shall choose a single public placement for the proof
source catalog without creating a competing claim ledger.

Acceptance criteria:

- The implementation evaluates adding a section to `/proof-paths/` first,
  because that page already maps routes to evidence trails.
- If a standalone route is chosen, the route is `/proof-source-catalog/`.
- The chosen page or section says `Public claim level: demo`.
- The page explains that route-level catalog rows can be `shipped`, `demo`,
  `concept`, or `hidden`, and that page-level `demo` status does not upgrade
  concept or hidden rows.
- The implementation records the chosen placement and rejected alternatives in
  `implementation-state.md`, including why `/proof-paths/` was or was not
  extended.
- If implemented as a standalone page, the route is added to sitemap metadata
  and public discovery metadata using existing site patterns.
- Existing public proof/governance surfaces link to the catalog only where the
  link helps readers verify wording, and link text does not imply that concept
  or hidden rows are available product capabilities.
- The implementation records how the catalog avoids duplicating the claim
  ledger (`site-tracemap-tools-claim-ledger`). Where a row's claim level,
  evidence status, or proof trail already exists on a published governance
  surface such as `/proof-paths/`, `/capabilities/`, or `/claims/` once that
  route ships, the catalog row links to it instead of restating it. Until the
  claim-ledger route is published, reference the
  `site-tracemap-tools-claim-ledger` spec by name without an outbound
  hyperlink.

### Requirement 2: Publish a proof source catalog table

The future page shall present a structured catalog that maps public routes and
claim labels to their allowed proof sources.

Acceptance criteria:

- Each row includes route, claim label, allowed public wording or claim family,
  `Public claim level`, evidence status, proof path, source artifact or source
  document, rule ID or rule family, evidence tier or coverage label,
  limitation, and non-claims.
- `Public claim level` is a required row field and uses exactly one of
  `shipped`, `demo`, `concept`, or `hidden`.
- The catalog distinguishes source-of-truth artifact family from proof-path
  link. For example, source material may be repository docs, rule catalog,
  public-safe demo summary, checked-in route metadata, public-safe report
  family, or local-only scanner artifact family; the proof path must be a
  public-safe route or source document link.
- Where no public-safe proof path exists for a publishable row, `proofPath`
  uses exactly one public-safe sentinel from the design schema:
  `future-only` or `hidden`. Free-text `not available` is not permitted.
  `blocked-pending-validation` is a pre-publication candidate state only; a row
  with that value must be removed, rewritten, or linked to a public-safe proof
  path before the catalog page publishes.
- Rows group or filter by route and by `Public claim level` so managers,
  reviewers, and bots can quickly answer whether wording is shipped, demo,
  concept, or hidden.
- Rows whose proof path is unavailable, future-only, hidden, or local-only are
  labeled with a limitation and are not described as evidence-backed.
- Hidden rows are represented as one aggregate placeholder at most, without
  unreleased capability names, hidden route names, private sample identities,
  counts, cadence, sequencing, or in-flight status.
- If a hidden aggregate placeholder is published, it must include all required
  fields from this requirement. It uses route `hidden`, `Public claim level:
  hidden`, evidence status `hidden-or-internal`, a stable anchor, a limitation
  stating that details are not disclosed publicly, and non-claims stating that
  the row does not represent any specific capability, route, private sample,
  count, cadence, sequence, or in-flight work.
- The page includes stable row identifiers or anchors suitable for automated
  claim-review references.
- The table avoids saying a route is allowed to say `impacted`, `safe`, `clean`,
  `complete`, or `production-proven` unless a reducer-backed public-safe result
  and supporting source material explicitly allow that exact wording. The
  starting catalog should not include any such wording.

### Requirement 3: Normalize public claim-level vocabulary

The future implementation shall define a single mapping from existing site
status vocabulary to the catalog's required `Public claim level` field.

Acceptance criteria:

- The mapping is defined once and reused for route metadata, capability matrix
  statuses, roadmap wording, proof-path public statuses, and repository-doc
  source entries.
- Every existing-surface status maps to exactly one catalog claim level.
- The required starting mapping is:

  | Existing-surface vocabulary | Catalog `Public claim level` | Notes |
  | --- | --- | --- |
  | `main`, `shipped`, `shipped navigation`, repository docs on `main`, `main with maturity caveats` | `shipped` | The source is true on the main branch or in source docs, but maturity caveats remain row limitations. |
  | `demo`, `demo guidance`, `main/demo`, `public-demo`, checked-in public-safe demo summary, route metadata `publicClaimLevel: demo`, proof-path public status `demo` | `demo` | Backed by public-safe demo summaries, checked-in sample material, or demo route metadata. |
  | `concept`, `concept-only`, `future`, `future-only`, `dev`, `dev-only`, route metadata `publicClaimLevel: concept`, proof-path public status `future` | `concept` | Future-facing, planning, or dev-only wording; do not upgrade until evidence is true on main or public-demo proof exists. |
  | `hidden`, `hidden pending validation`, no public capability row, no public proof-path counterpart, internal-only aggregate placeholder | `hidden` | Omit or abstract; do not disclose unreleased names, counts, cadence, sequencing, or private proof details. |

- The mapping explains that `shipped` is the public catalog label even when an
  existing source says `main`; public copy should prefer `shipped` in the
  required row field and keep `main` as source vocabulary or limitation text.
- The mapping explains that capability-matrix `dev` and `future` statuses
  resolve to catalog `concept`, not `hidden`, when they are already visible on
  public pages.
- The mapping explains that hidden work has no public capability-matrix or
  proof-path-index counterpart by design.
- The implementation enumerates the status vocabulary actually present in the
  current `/capabilities/`, `/roadmap/`, and `/proof-paths/` source at
  implementation time and asserts every encountered status token maps to
  exactly one catalog `Public claim level`. Any unmapped token fails validation
  and must be added to the mapping, with rationale recorded in
  `implementation-state.md`, before publish.

### Requirement 4: Normalize evidence-status and proof-source vocabulary

The future implementation shall define evidence-status labels that preserve
rule IDs, evidence tiers, coverage labels, source artifact families, and gaps
without inflating proof strength.

Acceptance criteria:

- Each row uses exactly one evidence-status label from:
  `source-backed`, `demo-evidence-backed`, `partial-or-reduced`,
  `gap-labeled-demo`, `future-only`, `hidden-or-internal`, or
  `not-yet-backed`.
- `not-yet-backed` is a pre-publication classification label only. No published
  catalog row may use `not-yet-backed`; any candidate row that would require
  that label must be removed, rewritten, or kept out of public output until a
  public-safe proof source exists.
- Each row's `Public claim level` and evidence-status label must form an
  allowed combination. The required starting matrix is:

  | Public claim level | Allowed evidence-status values |
  | --- | --- |
  | `shipped` | `source-backed`, `demo-evidence-backed`, `partial-or-reduced` |
  | `demo` | `demo-evidence-backed`, `partial-or-reduced`, `gap-labeled-demo` |
  | `concept` | `future-only` |
  | `hidden` | `hidden-or-internal` |

  `not-yet-backed` is allowed in no published combination. `hidden-or-internal`
  is allowed only on the hidden aggregate placeholder, whose route must be
  `hidden`. Implementations may tighten this matrix, but must record any
  divergence in `implementation-state.md`. A `shipped` row with
  `demo-evidence-backed` is permitted only when the route itself ships but the
  backing evidence artifact is a public-safe demo summary rather than full
  semantic analysis. The row must carry a limitation that evidence is
  demo-grade and does not imply full production analysis coverage.
- The required starting evidence-status mapping is:

  | Evidence-status label | Existing source vocabulary | Catalog notes |
  | --- | --- | --- |
  | `source-backed` | Repository docs, rule catalog, source code, validation docs, or route metadata on main | Use for source-document claims, not for runtime proof. |
  | `demo-evidence-backed` | `Tier1Semantic` or `Tier2Structural` with `Full`, `FullEvidenceAvailable`, or public-safe generated summary evidence | Show rule IDs, tiers, coverage, and proof path when public-safe. |
  | `partial-or-reduced` | `Partial`, `PartialAnalysis`, `Reduced`, `ReducedCoverage`, or `Tier3SyntaxOrTextual` evidence | Keep coverage gaps visible and do not restate as complete. |
  | `gap-labeled-demo` | `not_requested`, `unavailable`, `Tier4Unknown`, or explicit gap labels on demo rows | The gap label remains visible and cannot support parity or success claims. |
  | `future-only` | Public `future` or concept row with no resolvable source artifact yet | Future-facing only; not evidence-backed. |
  | `hidden-or-internal` | No public-safe proof path or no public row counterpart | Aggregate only; disclose no unreleased detail. |
  | `not-yet-backed` | No cited artifact, no rule family, or no public-safe proof path | The claim is unsupported public wording and should be forbidden or rewritten. |

- Rule IDs are shown directly when public-safe and specific; otherwise the row
  uses a rule-family reference with a limitation.
- Evidence tiers use the established names `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Coverage labels are transcribed from cited public-safe source material rather
  than normalized away.
- Local-only artifact families may be named as families, such as scanner facts,
  SQLite index, scan report, analyzer log, or rule catalog, but raw local
  artifact paths or contents are not linked or published.

### Requirement 5: Preserve public-safe proof paths

The future page shall link only to public-safe proof paths and shall keep raw
scanner material local unless a sanitized public artifact is explicitly checked
in.

Acceptance criteria:

- Proof paths link to public routes, checked-in public-safe source documents,
  repository docs, rule catalog entries, public-safe generated summaries, or
  sanitized public-demo report summaries.
- Proof paths do not link directly to raw `facts.ndjson`, raw `index.sqlite`,
  raw analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, or private sample
  names.
- Rows may name required scanner output families such as `scan-manifest.json`,
  `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log` only to
  explain source-of-truth boundaries. They must not publish raw contents or
  private paths.
- Public-safe proof-path link text is stable and suitable for automated link
  checking.
- Missing or future proof paths remain visible as limitations and are not hidden
  behind optimistic copy.

### Requirement 6: State limitations and non-claims

The future page shall attach limitations and non-claims to each catalog row and
to the page as a whole.

Acceptance criteria:

- Each row includes a limitation field.
- Each row includes a non-claims field or references a row-specific non-claims
  group.
- The page includes a global non-claims section stating that TraceMap does not
  prove runtime behavior, production traffic, endpoint performance, outage
  cause, release safety, operational safety, AI impact analysis, LLM analysis,
  or complete product coverage.
- The page states that the catalog is an index/orientation layer and that
  SQLite, facts, reports, rule catalog entries, source docs, route metadata,
  coverage labels, and documented limitations remain the source of truth.
- Limitations are direct and evidence-specific; they are not marketing
  disclaimers that obscure whether evidence exists.
- The page explicitly says concept rows are not shipped capabilities, demo rows
  are scoped to public-demo proof, and hidden rows should not be repeated as
  public claims.

### Requirement 7: Validate metadata, links, and claim boundaries

The future implementation shall add validation that makes the catalog useful to
humans and automated claim-review bots.

Acceptance criteria:

- If implemented as a standalone route, page metadata includes title,
  description, canonical path, Open Graph metadata, and discovery metadata with
  `publicClaimLevel: demo`.
- If implemented as a standalone route, sitemap metadata includes
  `/proof-source-catalog/`.
- Validation asserts the rendered page includes `Public claim level: demo` and
  the required row field text `Public claim level`.
- Validation asserts every catalog row contains all required fields from
  Requirement 2: route, claim label, allowed public wording or claim family,
  `Public claim level`, evidence status, proof path, source artifact or source
  document, rule ID or rule family, evidence tier or coverage label,
  limitation, and non-claims.
- Validation asserts each row's limitation and non-claims fields are non-empty.
- Validation asserts every row uses one of `shipped`, `demo`, `concept`, or
  `hidden` for `Public claim level`.
- Validation asserts every row uses one allowed evidence-status label.
- Validation asserts no published row uses the evidence-status label
  `not-yet-backed`; any row that would require `not-yet-backed` must be removed
  or rewritten before the page publishes.
- Validation rejects any row whose `Public claim level` and evidence-status
  pair is outside the allowed matrix from Requirement 4. Validation also rejects
  any non-hidden row using route `hidden` or evidence status
  `hidden-or-internal`.
- Validation asserts every published row's `proofPath` is either a resolvable
  public-safe link or exactly one of `future-only` or `hidden`.
- Validation rejects `blocked-pending-validation` in published output; that
  value may appear only in pre-publication candidate data before the row is
  removed, rewritten, or linked to a public-safe proof path.
- Validation asserts hidden-level rows collapse to at most one aggregate
  placeholder and that no row discloses unreleased capability names, hidden
  route names, private sample identities, or the count, cadence, sequencing, or
  in-flight status of hidden or internal work.
- Validation asserts the hidden aggregate placeholder's `proofPath` equals the
  bare sentinel `hidden`, not explanatory free text.
- Validation asserts that any catalog row whose primary content is an evidence
  trail, such as rule ID, tier, and coverage without a route-to-source mapping
  purpose, either links to its `/proof-paths/` counterpart or includes a
  limitation noting that evidence-trail detail is deferred to `/proof-paths/`.
- Validation checks that required public-safe links resolve in generated site
  output or to allowed repository source documents.
- Validation reuses `scripts/check-private-paths.sh`, or its token source, as
  the canonical denylist for private path and private sample/project/app names
  rather than redefining those tokens in catalog validation.
- Validation rejects forbidden public wording when it appears in affirmative
  claim fields such as claim label, allowed public wording, or capability
  assertions, including raw artifact disclosure, private paths, private sample
  names, runtime proof claims, production traffic claims, endpoint performance
  claims, outage-cause claims, release-safety claims, operational-safety claims,
  AI impact analysis claims, LLM analysis claims, and complete product coverage
  claims. Limitation and non-claims text may name these concepts in negated form
  and must not be rejected for doing so.
- Validation includes a bounded word count applied to individual row fields,
  including at minimum `limitation` and `allowedPublicWording`, as well as
  stable anchors for bot-oriented references. The implementation records the
  chosen per-field word-count bound in `implementation-state.md`.
- Validation asserts all row anchors are unique.
- Future implementation validation includes `git diff --check`, `npm test`
  from `site/`, `npm run validate` from `site/` if the script exists in
  `site/package.json` and otherwise adding it before this task is checkable,
  `npm run build` from `site/`, `./scripts/check-private-paths.sh`, and
  desktop/mobile browser sanity checks if layout or interaction changes are
  made.
