# Legacy Data Model Relationship Completion Requirements

## Introduction

TraceMap already extracts static legacy data model evidence for DBML, EDMX,
typed DataSet/TableAdapter XSD, NHibernate `.hbm.xml`, unsupported old ORM
descriptors, generated-code links, and downstream `legacy-data` projections.
The merged `legacy-data-model-orm-mapping-completion` implementation slice 1
closed regression coverage around existing rule-catalog vocabulary,
cross-format model identity separation, and relationship no-double-count
reporting. It did not add new extractor behavior.

This follow-up spec narrows the next implementation slice to deterministic
relationship extraction and relationship-gap behavior that remains open after
the merged slice. The goal is not runtime ORM analysis and not complete
coverage. The goal is to make ambiguous or unsupported relationship shapes
visible as rule-backed `AnalysisGap` evidence or needs-review projection labels,
while preserving existing deterministic relationship facts.

This is a spec-only PR. Product code changes are out of scope for this branch.

Public claim level: hidden.

## Current Context

- `origin/dev` was fetched and inspected before drafting this spec.
- Initial authoring base:
  `4b5844ff07199969eacd040e9383037d0b266d49`.
- `origin/dev` was refreshed before PR delivery to:
  `6bec000244340311cc385e4ebdeee4655a7251d4`.
- `legacy-data-model-orm-mapping-completion` status:
  `implementation-slice-1-merged-with-follow-ups`.
- `legacy-data-model-metadata-extraction` status:
  `implementation-slice-11-merged-with-follow-ups`.
- `legacy-data-metadata-extraction` status: `implemented-mvp`.
- Existing live code already includes baseline relationship extraction for DBML
  associations, EDMX CSDL/MSL associations, typed DataSet relationships and
  constraints, and NHibernate XML relationship descriptors. This spec must not
  duplicate the merged slice-1 evidence regression work.

## Relationship To Existing Specs

This spec is a narrow continuation of:

- `legacy-data-metadata-extraction`
- `legacy-data-model-metadata-extraction`
- `legacy-data-model-orm-mapping-completion`

It reuses existing fact types and rule IDs unless a cataloged rule update is
required before implementation:

- `LegacyDataMappingDeclared`
- `AnalysisGap`
- `legacy.data.dbml.v1`
- `legacy.data.edmx.v1`
- `legacy.data.typed-dataset.v1`
- `legacy.data.orm.nhibernate.v1`
- `legacy.data.orm.unsupported.v1`
- `legacy.data.model.relationship.v1`
- `legacy.data.model.surface.v1`

No implementation may emit a new gap string, relationship label, coverage label,
or needs-review caveat until its owning rule catalog entry documents the emitted
value and its limitations.

## Scope

In scope:

- Define the next small implementation slice for deterministic relationship
  extraction/gap behavior across legacy data model metadata.
- Prefer PR 1 as either:
  - one shared relationship gap classifier and focused harness reused by DBML,
    EDMX, typed DataSet, and NHibernate extractors; or
  - one relationship family with a clear gap vocabulary, fixture set, and
    downstream needs-review behavior.
- Preserve deterministic relationship evidence that is already emitted.
- Add `AnalysisGap` evidence for ambiguous or unsupported relationship shapes
  instead of inventing endpoint surfaces.
- Ensure downstream `legacy-data` projections keep relationship facts
  review-tier when relationship coverage is reduced or ambiguous.
- Require privacy assertions for default outputs and SQLite properties.
- Require deterministic ordering, stable fact IDs, commit SHA preservation, rule
  IDs, evidence tiers, file paths, line spans, extractor versions, and documented
  limitations.
- Require tests for ambiguity and unsupported shapes, especially shapes that
  should not produce terminal relationship surfaces.

Out of scope:

- No product-code implementation in this spec PR.
- No duplicate slice-1 regression work for rule-catalog vocabulary ownership,
  cross-format identity separation, or report no-double-counting unless a new
  implementation touches that behavior.
- No database connections, live schema introspection, EF runtime model loading,
  NHibernate session factory loading, query execution, migration execution,
  config transform execution, designer execution, or service activation.
- No proof of runtime relationship loading, lazy loading, cascade behavior,
  referential integrity, table existence, stored procedure existence, provider
  compatibility, deployment state, production usage, business impact, or impact
  reduction.
- No arbitrary Fluent mapping execution, project-local DSL execution, dynamic
  mapping evaluation, or heuristic AI classification.
- No LLM calls, embeddings, vector databases, prompt-based classification, fuzzy
  matching, or probabilistic inference in TraceMap core.
- No raw SQL, raw config values, connection strings, source snippets, local
  absolute paths, raw remotes, URLs, private sample labels, provider values, or
  secrets in default outputs or committed validation artifacts.

## Requirements

### Requirement 1: Relationship Gap Ownership

**User Story:** As a maintainer, I want every relationship gap emitted by
TraceMap to have an owning rule and documented limitation before code emits it.

Acceptance Criteria:

1. WHEN an implementation needs a new relationship gap string, coverage label,
   needs-review caveat, or relationship classifier THEN it SHALL first update
   `rules/rule-catalog.yml` with the owning rule ID, emitted value, fact type,
   evidence tier, safe properties, and limitations.
2. WHEN an existing gap string is reused THEN tests SHALL prove the expected
   rule catalog entry documents that value or its exact vocabulary family.
3. WHEN a relationship shape is recognized but unsupported THEN TraceMap SHALL
   emit `AnalysisGap` under the source rule or `legacy.data.model.relationship.v1`
   according to catalog ownership, not clean absence.
4. Relationship gap facts SHALL include repo-relative path, line span where
   available, commit SHA through the scan manifest, extractor version, rule ID,
   evidence tier, coverage label or classification, and limitation text.
5. Rule limitations SHALL explicitly state that static relationship evidence
   does not prove runtime ORM behavior, database referential integrity, query
   execution, or impact.

### Requirement 2: Deterministic Relationship Evidence Preservation

**User Story:** As a reviewer, I want existing deterministic relationship facts
to stay stable while new gaps only cover uncertain shapes.

Acceptance Criteria:

1. WHEN DBML, EDMX, typed DataSet, or NHibernate metadata exposes deterministic
   relationship endpoints already supported by TraceMap THEN the implementation
   SHALL preserve existing fact types, source rule IDs, `mappingKind` values,
   model relationship fields, stable identities, and tier ceilings.
2. WHEN relationship extraction is uncertain THEN TraceMap SHALL not choose an
   arbitrary endpoint, table, association set, collection target, or constraint.
3. Relationship descriptor facts SHALL remain static metadata evidence capped at
   `Tier2Structural` unless an existing source rule requires a lower tier.
4. Generated-code or symbol links SHALL remain separate supporting evidence and
   SHALL NOT upgrade source relationship descriptor evidence.
5. Output ordering and fact IDs SHALL be deterministic across repeated scans of
   the same repository commit.

### Requirement 3: Shared Relationship Gap Classifier

**User Story:** As an implementer, I want a shared way to classify relationship
ambiguity so DBML, EDMX, typed DataSet, and NHibernate do not drift.

Acceptance Criteria:

1. IF PR 1 implements the preferred shared classifier slice THEN it SHALL define
   a small internal relationship gap classifier or equivalent helper that maps
   unsupported/ambiguous relationship conditions to cataloged classifications,
   coverage labels, endpoint coverage labels, limitations, and evidence tiers.
   IF PR 1 takes a one-family alternate path instead THEN implementation state
   SHALL explain why shared-helper indirection was deferred, and the next
   relationship PR SHALL either add/wire the shared helper or justify permanent
   per-family divergence with cataloged reason-code tests.
2. The helper SHALL be deterministic and SHALL NOT inspect runtime provider
   state, execute code, execute SQL, call ORM runtimes, use LLMs, or read
   external resources.
3. The helper SHALL distinguish at least these generic conditions when cataloged
   and tested: missing endpoint, duplicate relationship identity, ambiguous
   endpoint candidates, unsupported relationship shape, reduced parser coverage,
   unsafe/redacted endpoint identity, and not-in-scope descriptors.
4. The first implementation MAY wire the helper to one relationship family only
   if wiring all four families is too large, but tests SHALL prove that the
   helper behavior is reusable and does not change unrelated deterministic
   relationship facts.
5. The implementation SHALL add a compact fixture harness or builder for
   relationship-gap tests so future family-specific slices can add DBML, EDMX,
   typed DataSet, and NHibernate shapes without copy-heavy setup.
6. The helper or one-family alternate SHALL define deterministic precedence for
   overlapping conditions and SHALL test at least one overlapping-condition
   descriptor for stable classification, limitations, order, and fact IDs.

### Requirement 4: DBML Relationship Gaps

**User Story:** As a maintainer, I want DBML association ambiguity to produce
reviewable evidence instead of invented links.

Acceptance Criteria:

1. WHEN DBML associations have deterministic source and target type/key
   metadata THEN TraceMap SHALL preserve existing relationship evidence.
2. WHEN DBML associations have duplicate names in a scope, missing target type,
   ambiguous key endpoints, duplicate table/type scopes, provider-extension
   relationship metadata, unsafe endpoint names that cannot render safely, or
   other unsupported association shape THEN TraceMap SHALL emit a cataloged
   `AnalysisGap` or reduced relationship fact with needs-review limitations.
3. DBML gaps SHALL not infer referential integrity, generated runtime
   relationship behavior, or table existence.
4. Tests SHALL cover at least one ambiguous or unsupported DBML relationship
   shape producing a gap or review-tier label, not an invented surface, when the
   DBML family is touched.

### Requirement 5: EDMX Relationship Gaps

**User Story:** As a maintainer, I want EDMX relationship shapes that require
runtime model interpretation to be downgraded.

Acceptance Criteria:

1. WHEN EDMX CSDL associations or MSL association-set mappings have exactly
   deterministic endpoints THEN TraceMap SHALL preserve existing relationship
   evidence.
2. WHEN EDMX metadata contains ambiguous association endpoints, duplicate
   association sets, multiple plausible containers, inherited endpoints, split
   entity mappings, conditional mappings, complex mappings, many-to-many
   mappings without deterministic join evidence, provider extensions, or missing
   MSL relationship metadata THEN TraceMap SHALL emit cataloged gaps or
   needs-review relationship evidence.
3. EDMX gaps SHALL not claim EF runtime model load, lazy loading, change
   tracking, referential integrity, provider compatibility, or query execution.
4. Tests SHALL cover at least one unsupported EDMX relationship shape producing
   `AnalysisGap` or needs-review labels when the EDMX family is touched.

### Requirement 6: Typed DataSet Relationship Gaps

**User Story:** As a maintainer, I want typed DataSet relations and constraints
to show when endpoint evidence is incomplete or ambiguous.

Acceptance Criteria:

1. WHEN typed DataSet `msdata:Relationship`, `xs:key`, `xs:unique`, and
   `xs:keyref` metadata expose deterministic parent/child endpoints THEN
   TraceMap SHALL preserve existing relationship evidence.
2. WHEN relation endpoints are missing, selectors are ambiguous, referenced
   constraints are duplicate, key/keyref fields cannot be safely matched, schema
   indicators are present without real DataSet content, or TableAdapter metadata
   appears to imply a relationship only through SQL text THEN TraceMap SHALL
   emit cataloged gaps or reduced relationship evidence.
3. Typed DataSet relationship extraction SHALL NOT infer relationships from raw
   SQL, generated code names alone, or filename conventions.
4. Tests SHALL cover at least one ambiguous or unsupported typed DataSet
   relationship shape producing `AnalysisGap` or needs-review labels when the
   typed DataSet family is touched.

### Requirement 7: NHibernate Relationship Gaps

**User Story:** As a maintainer, I want NHibernate XML relationships to stay
static and conservative when mapping XML is complex.

Acceptance Criteria:

1. WHEN NHibernate `.hbm.xml` relationship descriptors such as `many-to-one`,
   `one-to-one`, deterministic collections, keys, `one-to-many`, or
   deterministic `many-to-many` shapes expose safe endpoints THEN TraceMap
   SHALL preserve existing relationship evidence.
2. WHEN NHibernate mappings use composite ids, composite keys, formula-only
   joins, filters, custom SQL, dynamic components, inheritance/joined subclass,
   union subclass, custom user types, provider extensions, ambiguous collection
   elements, missing target classes, or runtime-loaded config references THEN
   TraceMap SHALL emit cataloged gaps or needs-review evidence rather than
   inventing endpoints.
3. NHibernate parsing SHALL continue to use safe XML parsing and deterministic
   bounds. Bounds or caps SHALL produce cataloged gaps and reduced coverage.
4. Raw schema, catalog, formula, filter, SQL fragment, query text, provider,
   dialect, connection, path, URL, remote, and secret-like values SHALL be
   omitted or represented by stable hashes only.
5. Tests SHALL cover at least one unsupported NHibernate relationship shape
   producing `AnalysisGap` or needs-review labels when the NHibernate family is
   touched.

### Requirement 8: Downstream Needs-Review Behavior

**User Story:** As a reviewer, I want reduced relationship coverage to remain
visible downstream without becoming impact proof.

Acceptance Criteria:

1. WHEN relationship evidence carries reduced coverage, ambiguity, unsupported
   shape, or missing endpoint limitations THEN downstream touched workflows
   SHALL classify selected or rendered relationship surfaces as needs-review or
   reduced coverage according to existing deterministic rules.
2. `AnalysisGap` facts under `legacy.data.*` SHALL not become terminal
   `legacy-data` surfaces.
3. Source relationship facts and derived relationship projections SHALL not
   double-count the same source descriptor in terminal `legacy-data` surface
   counts.
4. Implementation slices SHALL only add downstream tests for workflows they
   touch. Broader combined/path/reverse/vault/RAG/static HTML expansion remains
   deferred unless directly changed.
5. Downstream wording SHALL avoid "impacted" unless reducer evidence supports a
   reducer finding; relationship extraction alone is not impact proof.

### Requirement 9: Privacy And Artifact Safety

**User Story:** As a maintainer, I want relationship output to be useful without
leaking private metadata.

Acceptance Criteria:

1. Default outputs SHALL NOT contain raw SQL, raw config values, connection
   strings, provider values, server/catalog/user names, URLs, raw remotes, local
   absolute paths, source snippets, private sample labels, or secrets.
2. Unsafe relationship names, endpoint names, key names, storage names, schema
   names, catalog names, join formulas, filters, SQL fragments, and provider
   extension values SHALL be omitted or represented by stable hashes plus
   redaction metadata.
3. Privacy tests SHALL inspect `facts.ndjson`, `index.sqlite`, `report.md`, and
   touched downstream exports where applicable.
4. Local validation artifacts SHALL remain uncommitted unless explicitly
   redacted and approved for the spec or implementation.
5. Fixtures SHALL use synthetic public-safe labels only.
6. WHEN validating or checking repo-relative paths for specific directory
   segments THEN the implementation SHALL split paths into segments and check
   individual parts instead of using slash-wrapped string containment. This
   prevents root-level directory segments from being missed by checks that
   assume a leading slash.

### Requirement 10: Validation

**User Story:** As an implementer, I want the relationship slice to be
verifiable with focused tests and one bounded smoke.

Acceptance Criteria:

1. Implementation PRs SHALL run focused tests for touched extractors,
   relationship gap classification, rule catalog ownership, privacy, and any
   touched downstream workflow.
2. Implementation PRs SHALL run `dotnet build src/dotnet/TraceMap.sln` and
   `dotnet test src/dotnet/TraceMap.sln` unless explicitly deferred with an
   evidence-backed reason.
3. Implementation PRs SHALL run a CLI scan smoke against a synthetic committed
   fixture repository or an existing public-safe committed fixture and verify
   `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
   `logs/analyzer.log`.
4. CLI smoke evidence SHALL record the scanned repository commit SHA and any
   reduced-coverage labels. A failed build or reduced project load SHALL not be
   described as clean.
5. Spec and implementation PRs SHALL run `git diff --check` and
   `./scripts/check-private-paths.sh`.
