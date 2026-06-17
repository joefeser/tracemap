# Legacy Data Model Metadata Extraction Requirements

## Introduction

TraceMap already has a conservative legacy data metadata MVP for DBML, EDMX,
typed DataSet/TableAdapter XSD, provider config, and generated-code linkage. The
next old-codebase slice is to turn that metadata into stronger normalized data
model evidence that can connect WebForms, WCF, ASMX, Remoting, service, query,
and dependency-surface flows to likely database entities, tables, columns,
relationships, and old ORM descriptors.

This feature remains deterministic static extraction. It must not connect to a
database, load an Entity Framework runtime model, execute migrations, execute
SQL, run generated designers, call services, infer production usage, or claim
runtime data flow. It explains checked-in metadata and generated-code evidence
only, with rule IDs, evidence tiers, file spans, commit SHA, extractor versions,
coverage labels, and documented limitations.

Public claim level: hidden until public-safe fixtures or redacted legacy smoke
summaries have been reviewed.

## Relationship To Existing Legacy Data Metadata

This spec builds on `.kiro/specs/legacy-data-metadata-extraction/` and the
existing `legacy.data.*.v1` rules. It does not replace those facts or change
their static meaning. Implementations should reuse existing `LegacyData*` facts
where they can represent descriptor evidence safely. This spec does not add a
new dependency surface kind by default: model evidence extends the existing
`legacy-data` surface family with model-specific metadata. Any future new
surface kind requires a spec amendment.

## Scope

In scope:

- Normalize legacy data model identities across DBML, EDMX, typed DataSet XSD,
  TableAdapter descriptors, old ORM mapping XML/config, and generated code when
  deterministic linkage exists.
- Add deterministic extraction for a reviewable MVP of old ORM descriptors,
  starting with checked-in XML/config formats such as NHibernate `.hbm.xml` and
  project-local mapping sections that can be parsed without provider runtime
  loading.
- Preserve or project safe entity, storage object, column, relationship,
  routine, adapter, and mapping surfaces using stable keys and safe display
  values.
- Connect generated data types, contexts, row types, adapters, and old ORM model
  classes to metadata descriptors when semantic, structural, or scoped syntax
  evidence supports the link.
- Make legacy data model surfaces available to combined reports, dependency
  paths, reverse queries, impact, release-review, portfolio, evidence graph, and
  vault export readers without requiring those workflows to claim runtime use.
- Emit explicit `AnalysisGap` facts for unsupported metadata versions, ambiguous
  mappings, dynamic provider behavior, missing generated code, unsafe parser
  input, unavailable extractors, and old ORM descriptors outside the MVP.
- Add public-safe fixtures and validation guidance where possible, and keep any
  local legacy smoke output ignored and label/count-only.

Out of scope:

- No product-code implementation in this spec branch.
- No site specs or site files.
- No runtime database connections, live schema introspection, EF runtime model
  loading, migrations execution, SQL execution, provider-specific runtime
  behavior emulation, designer execution, IIS/service activation, or config
  transform execution.
- No proof of runtime data access, query execution, lazy loading, change
  tracking, branch feasibility, dependency injection binding, deployment,
  permissions, data contents, tenant behavior, production usage, future runtime
  behavior, query execution likelihood, or deployment reachability.
- No arbitrary `.xsd` table inference outside typed DataSet or gated metadata
  contexts.
- No raw source snippets, raw SQL, connection strings, config values, server or
  catalog names, URLs, raw remotes, local absolute paths, secrets, private sample
  identities, or generated smoke artifacts in committed files or reports by
  default.
- No LLM calls, embeddings, vector databases, prompt-based classification, fuzzy
  matching, or probabilistic model inference in TraceMap core.

## Requirements

### Requirement 1: Data Model Source Inventory

**User Story:** As a maintainer, I want TraceMap to identify legacy data model
metadata even when a legacy solution cannot build.

Acceptance Criteria:

1. WHEN a repository contains checked-in DBML, EDMX, typed DataSet XSD,
   TableAdapter metadata, generated data designer code, NHibernate mapping XML,
   or old ORM mapping/config descriptors in MVP scope THEN TraceMap SHALL emit
   inventory evidence with repo-relative path, line span where available, commit
   SHA, extractor version, rule ID, evidence tier, and safe descriptor kind.
2. WHEN an `.xsd` is evaluated THEN typed DataSet or TableAdapter extraction
   SHALL remain gated by XSD-intrinsic indicators; generated code or filename
   similarity SHALL NOT qualify an unrelated schema by itself.
3. WHEN an old ORM descriptor is recognized but unsupported in the MVP THEN
   TraceMap SHALL emit an `AnalysisGap` with a documented unsupported-descriptor
   classification rather than silently ignoring it or inventing facts.
4. WHEN metadata cannot be parsed safely, exceeds parser bounds, contains DTDs
   or external entities, or uses unsupported versions THEN TraceMap SHALL emit
   `AnalysisGap` facts using the legacy-data family's existing
   safety/malformed/too-large classification names and mark coverage reduced.
5. Inventory ordering, fact IDs, and stable model keys SHALL be deterministic
   across repeated scans of the same repository commit.

### Requirement 2: Normalized Data Model Identity

**User Story:** As a reviewer, I want model entities, storage objects, columns,
and relationships to have stable safe identities across legacy metadata formats.

Acceptance Criteria:

1. WHEN metadata declares a conceptual entity, generated entity, DataTable, row
   type, adapter type, old ORM class mapping, storage table/view/routine, column,
   property, field, association, relation, key, or foreign-key-like relationship
   THEN TraceMap SHALL emit or project a normalized legacy data model identity.
2. Normalized identities SHALL include safe local display names only when they
   pass the safe identifier policy; unsafe names SHALL be omitted or represented
   by stable hashes and explicit redaction properties.
3. Identity keys SHALL be derived from safe metadata kind, repository-relative
   file path, metadata-local scope, safe or hashed descriptor names, and commit
   stable evidence, not from volatile row IDs, machine paths, timestamps, or
   runtime environment values.
4. WHEN two descriptors share a display name but differ by namespace, container,
   mapping scope, source file, or storage identity THEN TraceMap SHALL preserve
   separate stable keys and emit an ambiguity or duplicate-identity gap where a
   downstream selector cannot distinguish them.
5. Model descriptor evidence SHALL be capped at `Tier2Structural` unless a
   separate generated-code link has compiler-resolved symbol evidence; that link
   SHALL NOT upgrade descriptor facts above their descriptor tier ceiling.

### Requirement 3: Old ORM Mapping XML And Config MVP

**User Story:** As a maintainer, I want common old ORM descriptors to produce
safe static metadata without loading provider runtimes.

Acceptance Criteria:

1. WHEN a checked-in NHibernate `.hbm.xml` mapping in MVP scope declares classes,
   components, properties, many-to-one, one-to-many, many-to-many, id, version,
   table, column, schema, catalog, discriminator, or collection metadata THEN
   TraceMap SHALL emit safe descriptor, relationship, and mapping evidence where
   deterministic.
2. WHEN NHibernate mapping references schema, catalog, connection, dialect,
   formula, filter, SQL fragment, query text, or provider-specific values THEN
   TraceMap SHALL omit or hash unsafe values and SHALL NOT store raw SQL or
   config values by default.
3. WHEN NHibernate query or named-query metadata is visible in MVP scope THEN
   TraceMap SHALL emit unsupported-shape or needs-review gap evidence unless a
   future spec defines deterministic query-shape extraction and redaction rules.
4. WHEN old ORM descriptors are recognized for formats outside the MVP, such as
   Fluent-only mappings, LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, Castle
   ActiveRecord, or project-specific mapping DSLs, TraceMap SHALL emit
   unsupported-descriptor gaps with limitations unless this spec is amended.
5. WHEN mappings use inheritance, components, joins, formulas, filters,
   composite keys, dynamic components, custom user types, or provider extensions
   beyond the MVP parser's deterministic rules THEN TraceMap SHALL emit
   needs-review or analysis-gap evidence instead of choosing arbitrary mappings.
6. Old ORM extraction SHALL NOT claim runtime session factory loading, lazy
   loading, cascade behavior, dirty tracking, database schema existence, or
   query execution.

### Requirement 4: Generated Code And Symbol Linkage

**User Story:** As a reviewer, I want metadata descriptors to connect to
generated or mapped code types only when static evidence supports the link.

Acceptance Criteria:

1. WHEN metadata names a generated file, custom tool output, namespace, class,
   entity type, context type, DataSet/table/row type, TableAdapter class, or ORM
   mapped class, and matching checked-in code is visible, TraceMap SHALL emit
   linkage evidence with supporting fact IDs.
2. WHEN Roslyn semantic analysis resolves generated or mapped symbols THEN the
   linkage MAY be `Tier1Semantic`; when only metadata-file or scoped syntax
   evidence aligns it SHALL be `Tier2Structural` or `Tier3SyntaxOrTextual`.
3. WHEN project load fails, generated files are skipped by general syntax
   extractors, or semantic symbols are unavailable THEN TraceMap SHALL continue
   with scoped syntax or structural fallback and label the coverage reduced.
4. WHEN multiple generated files, namespaces, partial classes, symbols, or ORM
   classes could satisfy the same descriptor THEN TraceMap SHALL emit ambiguity
   gaps rather than selecting a global short-name match.
5. Linkage facts SHALL preserve descriptor identity hashes and deterministic
   supporting fact IDs; they SHALL NOT reclassify generated code as hand-authored
   business logic.

### Requirement 5: Legacy Data Surfaces For Flow And Dependency Workflows

**User Story:** As a TraceMap user, I want legacy UI and service flows to show
static evidence near likely data model surfaces without runtime claims.

Acceptance Criteria:

1. WHEN WebForms, WCF, ASMX, Remoting, HTTP/API, query, call graph, or dependency
   path workflows encounter generated data types, TableAdapters, ORM mapped
   classes, or static SQL/query evidence with legacy data model descriptors THEN
   TraceMap MAY attach legacy data model surfaces as supporting context.
2. WHEN a flow reaches a generated data type or adapter that is deterministically
   linked to a model descriptor THEN reports MAY say it reaches "legacy data
   model metadata" or "likely table/entity descriptor" and SHALL NOT say it
   executed a query or touched a database.
3. WHEN only name-only, syntax-only, ambiguous, missing-generated-code, high
   fan-out, or reduced-coverage evidence supports a data surface THEN downstream
   classification SHALL be capped at a needs-review or reduced-coverage label.
4. WHEN a data model surface has no path from a root under available coverage
   THEN reports SHALL NOT infer absence of use unless relevant extractors and
   graph evidence are full; reduced coverage SHALL produce an explicit gap.
5. Surface projection SHALL use the existing `legacy-data` dependency-surface
   kind with model-specific metadata where possible and SHALL preserve rule IDs,
   evidence tiers, file spans, fact IDs, edge IDs, source labels, scan IDs,
   commit SHAs, and limitations.
6. WHEN source facts are `AnalysisGap` rows under `legacy.data.*` rules THEN
   surface projection SHALL exclude them from terminal data surfaces and expose
   them only as gaps, caveats, or limitations.
7. WHEN a derived legacy data surface row has already been projected or persisted
   by a future implementation THEN downstream readers SHALL NOT re-project it as
   a second terminal surface.

### Requirement 6: Combined Reports, Evidence Graph, And Vault Export

**User Story:** As a reviewer, I want legacy data model evidence to appear in
cross-index and export views with provenance and safe redaction.

Acceptance Criteria:

1. WHEN combined reports read indexes containing legacy data model facts THEN
   they SHALL either render safe `legacy-data` dependency surfaces with
   model-specific metadata or emit explicit availability gaps; they SHALL NOT
   fail because new rule IDs, facts, or properties exist.
2. WHEN path, reverse, diff, impact, release-review, or portfolio commands
   include legacy data model surfaces THEN every derived row SHALL cite its own
   rule ID plus the source legacy data model rule IDs that support it.
3. WHEN evidence graph or vault export reads legacy data model evidence THEN
   exported nodes and edges SHALL include rule IDs, evidence tiers, commit SHAs,
   coverage labels, supporting IDs, safe display labels or hashes, and
   limitations.
4. Vault and graph exports SHALL remain view/export layers over existing static
   evidence; they SHALL NOT introduce a new analyzer, authority layer, runtime
   topology claim, or AI classification.
5. Exported Markdown/JSON/graph artifacts SHALL exclude raw SQL, raw snippets,
   config values, connection strings, URLs, remotes, local absolute paths,
   private sample labels, and secrets.
6. WHEN command validators or selector allow-lists define accepted dependency
   surfaces THEN they SHALL either include `legacy-data` where model surfaces are
   selectable or return rule-backed availability gaps with safe error text; they
   SHALL NOT throw unhandled exceptions for recognized legacy data model
   evidence.
7. WHEN report, path, reverse, diff, impact, release-review, portfolio, graph,
   or vault readers switch on surface kind THEN unknown or future legacy model
   metadata SHALL use safe defaults or availability gaps rather than crashing.

### Requirement 7: Privacy, Redaction, And Artifact Safety

**User Story:** As a maintainer, I want data model evidence to be safe for
reviewed public fixtures and private legacy scans.

Acceptance Criteria:

1. Facts, SQLite properties, reports, logs, graph exports, vault exports, and
   validation summaries SHALL NOT include raw local absolute paths, private
   repository names, raw remotes, secrets, connection strings, config values,
   raw SQL, endpoint addresses, WSDL/schema URLs, provider-specific credentials,
   server/catalog names, source snippets, or unreviewed sample identifiers.
2. File paths in committed fixtures or reports SHALL be repo-relative or neutral
   sample labels only.
3. Raw SQL-like values found in TableAdapter, ORM named query, formula, filter,
   or config metadata SHALL use existing SQL hash/shape conventions only when
   complete static text is in scope; otherwise they SHALL be omitted or hashed
   with a limitation.
4. Logs SHALL avoid echoing raw selector values or parser errors that contain
   paths, connection strings, raw SQL, URLs, remotes, or secrets.
5. Any option that stores raw snippets or raw SQL SHALL be explicit, disabled by
   default, excluded from public-safe fixtures, and documented as unsafe for
   committed outputs.

### Requirement 8: Documentation, Rules, Tests, And Validation

**User Story:** As a maintainer, I want every data model conclusion to be
rule-backed, documented, and covered by deterministic validation.

Acceptance Criteria:

1. New or changed rule IDs SHALL be documented in `rules/rule-catalog.yml` with
   emitted fact types, evidence tiers, safe properties, and limitations before
   implementation is complete.
2. `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, and
   `docs/ACCEPTANCE.md` SHALL be updated if new fact types, report sections,
   validation commands, dependency-surface kinds, graph/vault fields, or
   reducer-facing keys are added.
3. Tests SHALL cover DBML, EDMX, typed DataSet/TableAdapter, NHibernate mapping
   XML MVP, generated-code linkage, old ORM unsupported descriptors,
   relationship extraction, ambiguity gaps, parser safety, deterministic stable
   keys, SQLite privacy, report privacy, and reduced coverage.
4. Integration tests or public-safe smokes SHALL prove combined reports, paths,
   reverse, diff, impact, release-review, portfolio, evidence graph, and vault
   export either consume legacy data model surfaces or emit explicit availability
   gaps.
5. Validation SHALL include `dotnet build`, `dotnet test`, focused legacy data
   model tests, `./scripts/check-private-paths.sh`, `git diff --check`, and
   relevant pinned smoke checks from `docs/VALIDATION.md`, or implementation
   state SHALL record exact deferrals with rationale.
6. Public-safe sample fixtures SHALL use synthetic or reviewed public metadata
   and SHALL NOT commit generated scan outputs, raw facts, SQLite indexes,
   analyzer logs, raw SQL, connection strings, config values, private labels,
   local absolute paths, or source snippets.
