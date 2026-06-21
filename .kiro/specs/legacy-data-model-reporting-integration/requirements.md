# Legacy Data Model Reporting Integration Requirements

## Introduction

TraceMap can extract legacy data metadata from deterministic static artifacts
such as DBML, EDMX, typed DataSet XSD/TableAdapter descriptors, checked-in
generated data code, provider config metadata, NHibernate mapping XML, and
recognized unsupported old ORM descriptors. The next reporting slice is to make
that evidence useful in user-facing reports and exports without overstating
what static metadata proves.

This feature is a report/export integration layer over existing or near-term
facts. It must preserve rule IDs, evidence tiers, source labels, file spans,
commit SHAs, extractor versions, coverage labels, supporting IDs, and
limitations. It must not connect to a database, load ORM providers, execute
SQL, prove schema existence, prove runtime database usage, or infer production
behavior.

This is a spec-only PR. Product code changes are out of scope for this branch.

## Relationship To Existing Specs

This spec builds on:

- `legacy-data-metadata-extraction`
- `legacy-data-model-metadata-extraction`
- `combined-dependency-reporting`
- `combined-dependency-paths`
- `reverse-impact-query`
- `route-flow-service-data-composition`
- `release-review-report`
- `deterministic-risk-scoring`
- `evidence-graph-vault-export`
- `static-html-evidence-explorer`

The integration must use the existing `legacy-data` dependency surface family.
It must not introduce a parallel `legacy-data-model` surface kind unless a
future spec explicitly changes selector compatibility and double-counting
rules.

The extraction implementation may be in flight. Reporters and exporters must
therefore handle both current facts and near-term additive fields, and must
emit rule-backed gaps when optional data model evidence is absent.

## Scope

In scope:

- Define how route-flow, reverse, combined reports, dependency paths, diff,
  impact, release-review, vault/RAG exports, and the static HTML explorer
  consume legacy data model evidence.
- Define safe descriptor rendering for model, entity, storage object, table,
  view, routine, column, relationship, adapter, generated-code link, provider
  config, ORM descriptor, and unsupported descriptor gaps.
- Define stable IDs, deterministic ordering, selector behavior, fallback
  behavior, and report/export schema additions.
- Preserve source rule IDs and use `legacy.data.model.surface.v1` only for
  derived report/export projection rows or projection gaps.
- Define behavior when optional source facts, combined tables, generated-code
  links, relationship metadata, or model identity fields are missing.
- Define release-review and deterministic review priority boundaries for legacy
  data model evidence.
- Define privacy, safety, and public/demo artifact constraints.
- Define a first implementation PR boundary and validation plan.

Out of scope:

- No product-code implementation in this spec branch.
- No scanner extractor changes in this spec branch.
- No database connections, live schema introspection, SQL execution, migration
  execution, EF/NHibernate runtime model loading, provider runtime behavior,
  config transform execution, designer execution, service activation, or
  production data access proof.
- No runtime reachability, query execution likelihood, lazy loading,
  change-tracking, cascade, tenancy, permission, business criticality,
  release-approval, vulnerability, compliance, or incident root-cause claims.
- No raw SQL, snippets, config values, connection strings, raw remotes, URLs,
  hostnames, local absolute paths, private sample labels, or secrets in
  committed specs, generated reports, exports, logs, or fixtures.
- No LLM calls, embeddings, vector databases, prompt-based classification, AI
  impact analysis, fuzzy matching, or probabilistic model inference in
  TraceMap core.

## Requirements

### Requirement 1: Safe Legacy Data Model Descriptor Rendering

**User Story:** As a reviewer, I want reports to show legacy data model
descriptors with provenance and limitations so I can understand static evidence
without seeing private values.

Acceptance Criteria:

1. WHEN a report or export renders a legacy data model row THEN it SHALL include
   a stable row or node ID, surface kind `legacy-data`, descriptor role,
   metadata format, safe display label or hash, source artifact type, rule ID,
   evidence tier, source label, scan ID, commit SHA, extractor version where
   available, file path, line span, supporting fact IDs, supporting edge IDs,
   coverage label, `displayClearance`, `claimLevelContextId` where available,
   and limitations.
2. WHEN model-specific properties are available THEN the row SHOULD render safe
   values for `modelKind`, `metadataFormat`, `descriptorRole`,
   `stableModelKey`, `displayName`, `displayNameHash`, `containerName`,
   `containerHash`, `storageKind`, `mappingKind`, `modelRelationshipKind`, and
   `sourceMetadataFactId`.
3. WHEN descriptor names are unsafe or unreviewed for the selected output
   profile THEN the renderer SHALL omit them or use stable hashes and SHALL
   record a redaction or hidden-evidence limitation.
4. WHEN source facts are `AnalysisGap` rows under `legacy.data.*` rules THEN
   reporters SHALL render them as gaps, caveats, or limitations, not as terminal
   legacy data surfaces.
5. WHEN a row is derived from multiple source facts THEN supporting IDs SHALL be
   sorted deterministically and the derived row SHALL be capped by the weakest
   supporting evidence tier and coverage label.
6. Descriptor wording SHALL use phrases such as "static descriptor evidence",
   "legacy data model metadata", "likely table/entity descriptor", and "needs
   review"; it SHALL NOT say that a query executed, a table exists, a database
   was touched, or production data was accessed.

### Requirement 2: Combined Dependency Reports And Paths

**User Story:** As a maintainer, I want combined reports and path queries to
include legacy data model surfaces without schema fragility or double-counting.

Acceptance Criteria:

1. WHEN a combined index contains `LegacyData*` facts or `legacy.data.*.v1`
   source rules THEN combined dependency reports SHALL project them into
   `legacy-data` dependency surface rows using safe model metadata when
   available.
2. WHEN a combined index contains model identity fields from near-term
   extraction THEN reports SHALL prefer `stableModelKey` and safe descriptor
   role fields over display-name-only identity.
3. WHEN model identity fields are absent THEN reports SHALL fall back to current
   legacy data descriptor properties such as metadata kind, mapping kind, safe
   entity/storage/table/column label, shape hash, or source fact ID, and SHALL
   mark precision as reduced where appropriate.
4. WHEN optional combined tables such as fact-symbol, relationship, argument,
   or parameter-forward tables are absent THEN path/report readers SHALL emit
   availability gaps and continue rendering available legacy data surfaces.
5. WHEN a future implementation persists derived legacy data model surface rows
   THEN readers SHALL not re-project the same source evidence as a second
   terminal surface.
6. Dependency paths may end at `legacy-data` surfaces or include them as
   supporting context, but SHALL classify syntax-only, name-only, ambiguous,
   generated-code-only, high fan-out, or reduced-coverage rows as needs-review
   or unknown rather than strong runtime evidence.
7. Combined reports and paths SHALL keep current selectors compatible with
   `legacy-data` and SHALL return rule-backed selector or availability gaps for
   unsupported model-specific selectors.

### Requirement 3: Route-Flow Terminal And Supporting Rows

**User Story:** As a TraceMap user, I want route-flow reports to show when a
route statically connects to legacy data model evidence while preserving static
evidence limits.

Acceptance Criteria:

1. WHEN route-flow composition reaches generated data types, TableAdapters, ORM
   mapped classes, static query-shape evidence, or fact-symbol attachments that
   link to legacy data model descriptors THEN route-flow MAY render terminal
   `legacy-data` rows or supporting descriptor rows.
2. Terminal rows SHALL include route-flow row ID, terminal surface ID,
   descriptor role, safe label/hash, source rule IDs, route-flow rule ID,
   evidence tier, source label, commit SHA, file span, supporting fact/edge IDs,
   coverage, and limitations.
3. Supporting rows SHALL be visibly secondary to route-flow path evidence and
   SHALL NOT imply that the route executed SQL, selected a runtime ORM provider,
   or reached a live database.
4. WHEN only descriptor evidence exists without a credible route-to-symbol or
   route-to-surface bridge THEN route-flow SHALL render an availability or
   supporting-evidence gap instead of creating a terminal path.
5. WHEN all reachable legacy data evidence for a given route path consists
   solely of `AnalysisGap` facts THEN route-flow SHALL emit a scoped
   availability gap citing those gap rule IDs rather than creating a supporting
   descriptor row.
6. WHEN optional legacy data model evidence is absent THEN route-flow SHALL
   preserve existing output and emit a scoped optional-extractor gap only when
   the absence affects a conclusion.
7. Route-flow classifications SHALL be capped by weakest evidence, reduced
   coverage, ambiguity, high fan-out, and generated-code uncertainty.

### Requirement 4: Reverse Query Support

**User Story:** As an investigator, I want to start from a legacy data model
surface and see upstream static roots under coverage-relative rules.

Acceptance Criteria:

1. WHEN `tracemap reverse` supports surface selectors THEN `legacy-data` SHALL
   be selectable by surface kind where safe, and model-specific selector fields
   MAY be added only with documented exact-match or hash-match semantics.
2. WHEN a user selects by descriptor display text THEN matching SHALL use
   case-insensitive exact matching over safe rendered labels only; unsafe or
   hidden descriptors SHALL require stable ID or hash selectors.
3. WHEN multiple legacy data surfaces match a selector THEN reverse SHALL cap
   deterministically and emit truncation or ambiguity gaps rather than merging
   unrelated descriptors.
4. WHEN reverse paths connect upstream roots to legacy data model surfaces THEN
   roots, selected surfaces, and paths SHALL preserve source legacy data rule
   IDs, reverse rule IDs, evidence tiers, file spans, supporting IDs, coverage,
   and limitations.
5. WHEN no reverse path is found under reduced coverage, missing optional graph
   tables, absent generated-code links, or unsupported ORM descriptor gaps THEN
   reverse SHALL emit a rule-backed gap with a registered rule ID, not a clean
   no-use conclusion. The implementation PR MUST choose or register that rule ID
   in the rule catalog before the gap is emitted. If an existing combined
   reverse rule already covers the gap cause, its documented scope and
   limitations must explicitly include the `legacy-data` surface kind; otherwise
   a new catalog entry is required. The tasks checklist MUST record which rule
   ID was chosen or registered before the implementation PR merges.
6. Reverse report wording SHALL say "static root can reach descriptor evidence"
   or "reverse static evidence"; it SHALL NOT say "database caller", "runtime
   usage", "production use", or "no callers" unless full credible static
   coverage supports the exact narrower claim.

### Requirement 5: Snapshot Diff, Impact, And Combined Reports

**User Story:** As a reviewer, I want changed legacy data model evidence to
appear in diff and impact reports without treating descriptor churn as runtime
database impact.

Acceptance Criteria:

1. WHEN before/after combined reports compare legacy data model surfaces THEN
   stable identity SHALL prefer `stableModelKey`, source label, metadata format,
   descriptor role, source artifact identity, safe/hash descriptor key, and rule
   ID over volatile row IDs or display-only labels.
2. WHEN before/after stable identities cannot be uniquely resolved due to
   ambiguous identity THEN diff SHALL mark the row `ambiguous-identity`, list
   the matching candidates as supporting rows, and classify the change as
   needs-review rather than a definite add or remove.
3. WHEN a legacy data model surface is added, removed, or changed THEN diff
   rows SHALL preserve source fact IDs, derived row IDs, rule IDs, evidence
   tiers, file spans, coverage, and limitations.
4. WHEN impact reducers consume changed legacy data model surfaces THEN they
   SHALL describe changed static descriptor evidence and MAY attach bounded
   path/reverse context; they SHALL NOT infer runtime schema change, migration
   execution, database compatibility, or production impact.
5. WHEN old ORM descriptors are recognized but unsupported THEN diff and impact
   reports SHALL expose unsupported-descriptor gaps and reduced coverage labels.
6. WHEN generated-code links are missing, stale, ambiguous, or syntax-only THEN
   impact classification SHALL downgrade to needs-review or unknown as defined
   by existing reducer rules.

### Requirement 6: Release Review And Deterministic Priority Scoring

**User Story:** As a release reviewer, I want legacy data model evidence in the
release packet and opt-in review priority inputs without approval or risk
oracle language.

Acceptance Criteria:

1. WHEN release-review includes top changed surfaces, path context, reverse
   context, checklist items, or review priority rows THEN legacy data model
   surfaces SHALL be eligible only as static evidence inputs with their source
   rule IDs and limitations intact.
2. Release-review SHALL classify legacy data model descriptor changes using
   existing rollup semantics such as `ReviewRecommended`, `PartialAnalysis`,
   `UnknownAnalysisGap`, or underlying strong/probable static evidence only when
   source identity and coverage allow it.
3. Deterministic review priority scoring MAY use closed inputs such as evidence
   tier, reduced coverage, truncation, unsupported descriptor gap, changed
   descriptor role, path/reverse context presence, and fan-out caps.
4. Review priority SHALL NOT use private descriptor names, raw table/column
   labels, business criticality, vulnerability labels, production telemetry,
   runtime database claims, LLM output, embeddings, or prompt classifications.
5. Checklist items SHALL refer to finding IDs, gap IDs, source labels, safe
   descriptor categories, and rule IDs; they SHALL NOT include raw SQL, config
   values, connection strings, hostnames, private routes, or private sample
   labels.
6. Absence of legacy data model changes SHALL NOT contribute to release
   approval language.

### Requirement 7: Vault, RAG, Evidence Graph, And Static Explorer

**User Story:** As a reviewer, I want exports and local explorers to render
legacy data model evidence as safe navigable static evidence.

Acceptance Criteria:

1. WHEN vault/RAG/evidence graph export reads legacy data model evidence THEN it
   SHALL create `surface` nodes with `surfaceKind = legacy-data`, safe labels or
   hashes, source rule IDs, evidence tiers, coverage, supporting IDs,
   limitations, and claim-level metadata.
2. WHEN exporter input lacks compatible legacy data model fields THEN the export
   SHALL either render category-only legacy data nodes from current facts or
   emit schema/availability gaps; it SHALL NOT fail the whole export unless
   required input schemas are unusable.
3. WHEN export claim level is public/demo THEN descriptor display names,
   endpoint keys, symbol names, table/column names, and relationship labels
   SHALL be hidden unless source-claim metadata or synthetic fixture status
   permits display.
4. RAG-oriented exports SHALL be deterministic static evidence packs only. They
   SHALL NOT create embeddings, vector databases, prompt summaries, or AI
   classifications in TraceMap core.
5. The static HTML explorer SHALL group legacy data model rows under the
   existing surfaces/evidence/gaps/rules views, include rule IDs and
   limitations, and make gaps visible before users inspect detailed rows.
6. Explorer filtering MAY include closed fields such as metadata format,
   descriptor role, surface kind, rule ID, evidence tier, coverage label, and
   gap kind; it SHALL NOT search hidden raw facts, repository files, SQL, or
   config values.
7. Vault notes, graph JSON, explorer data, and downloadable artifacts SHALL pass
   the same public/demo safety guard classes as existing TraceMap outputs.

### Requirement 8: Optional Evidence, Unsupported ORM Gaps, And Compatibility

**User Story:** As a maintainer, I want older indexes and partially implemented
extractors to remain useful and honestly labeled.

Acceptance Criteria:

1. WHEN `LegacyData*` facts exist without model-normalized fields THEN
   reports/exporters SHALL render current safe fields and mark model precision
   as reduced or unavailable.
2. WHEN model-normalized fields exist without relationship or generated-code
   links THEN reports/exporters SHALL render descriptor rows and emit scoped
   relationship/link availability gaps only where relevant.
3. WHEN neither legacy data facts nor extractor availability markers exist THEN
   reports/exporters SHALL emit optional-extractor availability gaps when a
   conclusion depends on data model coverage.
4. WHEN unsupported ORM descriptors are represented only by `AnalysisGap` facts
   THEN reports/exporters SHALL keep those gaps visible and SHALL NOT fabricate
   entity/table/column rows.
5. WHEN unknown future `legacy.data.*` rules or descriptor roles appear THEN
   readers SHALL preserve raw rule IDs and safe closed metadata, mark unknown
   vocabulary values as schema gaps, and continue unrelated sections. Unknown
   vocabulary gaps in report/export projection SHALL cite
   `legacy.data.model.surface.v1` unless the implementation registers a narrower
   rule ID before emitting them.
6. Schema additions SHALL be backward-compatible where possible: new JSON fields
   are additive, required arrays remain present, unknown fields are ignored by
   older readers, and absence of optional legacy model tables is not fatal.

### Requirement 9: Determinism And Stable IDs

**User Story:** As an automation author, I want byte-stable reports and exports
that do not depend on row order or local machine state.

Acceptance Criteria:

1. Row/provenance stable IDs SHALL be derived from schema version, source index
   ID or stable source identity, commit SHA presence/value where safe, source
   rule ID, metadata format, descriptor role, stable model key or safe/hash
   descriptor key, source artifact type, source artifact path hash,
   repo-relative file path or path hash, line span, and supporting IDs.
   Cross-snapshot descriptor identity keys used for diff matching SHALL exclude
   commit SHA, commit SHA display category, scan ID, extractor version, and
   profile-specific display policy so unchanged descriptors can match across
   before/after snapshots.
   WHEN optional fields such as stable model key, file path, or line span are
   absent, each absent field SHALL contribute a canonical absence token such as
   `field-absent` rather than being omitted from the hash input.
2. Stable IDs SHALL NOT use wall-clock time, random values, local absolute
   paths, raw remotes, connection strings, raw SQL, raw descriptor names that
   failed safety policy, or volatile SQLite row order.
3. Rows SHALL sort by surface kind, descriptor role, metadata format, safe
   display label or hash, source label, stable ID, and supporting IDs using
   ordinal comparison.
4. JSON output SHALL use deterministic object property order where existing
   writers support it, sorted arrays, LF endings, and final newline.
5. Markdown tables SHALL sort using the same row order as JSON and SHALL escape
   Markdown-sensitive display values.
6. Duplicate stable identities SHALL emit duplicate-identity gaps and downgrade
   affected selectors or rows instead of merging silently.

### Requirement 10: First PR Boundary And Test Plan

**User Story:** As a maintainer, I want an implementation plan that can land in
small reviewable slices with focused validation.

Acceptance Criteria:

1. The first implementation PR SHOULD add shared safe descriptor projection
   helpers and integrate `legacy-data` model metadata into combined reports,
   paths, reverse, and route-flow where existing readers already handle legacy
   data facts.
2. The first implementation PR SHOULD NOT add new extractor behavior, new raw
   snippet storage, runtime database behavior, public site changes, AI features,
   or broad report rewrites.
3. Tests SHALL cover safe rendering, absence of optional fields, absence of
   optional tables, unsupported ORM gaps, `AnalysisGap` exclusion from terminal
   surfaces, deterministic ordering, stable IDs, selector ambiguity, and
   public/demo redaction.
4. Tests SHALL include focused route-flow, combined report/path, reverse,
   release-review, vault/export, and explorer cases when those surfaces are
   touched by implementation.
5. Validation SHALL include `git diff --check`, `./scripts/check-private-paths.sh`,
   and any existing spec lint/check if present.
6. Implementation validation SHOULD run the relevant focused .NET tests and at
   least one CLI smoke over synthetic or public-safe sample artifacts when
   product code changes land.
