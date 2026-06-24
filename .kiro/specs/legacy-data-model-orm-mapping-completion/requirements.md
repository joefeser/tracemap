# Legacy Data Model ORM Mapping Completion Requirements

## Introduction

TraceMap already extracts conservative legacy data metadata for DBML, EDMX,
typed DataSet/TableAdapter XSD, checked-in generated data code, provider config,
NHibernate `.hbm.xml` MVP mappings, unsupported old ORM descriptor gaps, and
safe `legacy-data` projection in several downstream reports.

This spec defines the remaining extraction-depth slice for old .NET data model
metadata. The goal is not a new runtime ORM analyzer. The goal is to finish the
static mapping evidence that is still incomplete: relationship edge cases,
NHibernate descriptor depth, unsupported old ORM descriptor gaps, precision for
DBML/EDMX/DataSet mappings where deterministic, generated-code linkage
boundaries, and public-safe smoke evidence.

This is a spec-only PR. Product code changes are out of scope for this branch.

Public claim level: hidden until public-safe fixtures or redacted smoke
summaries prove the behavior.

## Relationship To Existing Specs

This spec consolidates unfinished pieces from:

- `legacy-data-metadata-extraction`
- `legacy-data-model-metadata-extraction`
- `legacy-data-model-reporting-integration`
- `legacy-codebase-validation`

It does not replace those specs. It narrows their remaining work into one
implementation-ready slice for extraction and smoke proof. Existing rules and
fact types should be reused where possible:

- `LegacyDataMetadataDeclared`
- `LegacyDataEntityDeclared`
- `LegacyDataStorageObjectDeclared`
- `LegacyDataColumnDeclared`
- `LegacyDataMappingDeclared`
- `LegacyDataProviderConfigDeclared`
- `LegacyDataGeneratedCodeLinked`
- `AnalysisGap`

Existing and near-term rules remain authoritative unless this spec explicitly
requires a catalog amendment:

- `legacy.data.dbml.v1`
- `legacy.data.edmx.v1`
- `legacy.data.typed-dataset.v1`
- `legacy.data.config.v1`
- `legacy.data.generated-link.v1`
- `legacy.data.model.identity.v1`
- `legacy.data.model.relationship.v1`
- `legacy.data.orm.nhibernate.v1`
- `legacy.data.orm.unsupported.v1`
- `legacy.data.model.generated-link.v1`
- `legacy.data.model.surface.v1`

The implementation should keep using the existing `legacy-data` dependency
surface kind. A separate `legacy-data-model` surface kind is out of scope.

## Scope

In scope:

- Complete deterministic relationship extraction and gap behavior for DBML,
  EDMX, typed DataSet, and NHibernate where checked-in metadata is sufficient.
- Extend NHibernate `.hbm.xml` MVP depth for deterministic class, property,
  component, collection, key, and relationship descriptors without loading
  NHibernate or executing configuration.
- Recognize old ORM descriptor/config families that are outside parser MVP and
  emit explicit unsupported or analysis-gap evidence.
- Tighten DBML, EDMX, typed DataSet, and TableAdapter mapping precision where
  current tests or implementation-state notes identify incomplete deterministic
  cases.
- Preserve generated-code linkage boundaries, including semantic links when
  available and scoped syntax/structural fallback when project load fails.
- Emit safe normalized descriptors with cleartext only for safe local
  identifiers and stable hashes for sensitive or unsafe names/values.
- Add public-safe fixtures or synthetic smoke inputs that prove relationship,
  NHibernate, unsupported-ORM, generated-link, gap, and redaction behavior.
- Add validation commands and smoke evidence expectations for the implementation
  PR.
- Record downstream combined/path/reverse/vault/RAG/export expectations as
  follow-up when they are too large for this extraction-depth slice.

Out of scope:

- No product-code implementation in this spec branch.
- No database connections, live schema introspection, migration execution, SQL
  execution, EF runtime model loading, NHibernate session factory loading,
  config transform execution, designer execution, IIS/service activation, or
  service calls.
- No proof of runtime data access, query execution, table existence, provider
  compatibility, lazy loading, cascade behavior, change tracking, permissions,
  tenancy, deployment, production usage, or business impact.
- No Fluent NHibernate execution, provider runtime behavior, arbitrary code DSL
  execution, or dynamic mapping evaluation.
- No arbitrary `.xsd` table inference outside typed DataSet/TableAdapter-gated
  metadata contexts.
- No raw SQL, raw source snippets, raw config values, connection strings,
  server/catalog/user names, URLs, raw remotes, local absolute paths, private
  sample labels, secrets, or unchecked smoke artifacts in committed files or
  default outputs.
- No LLM calls, embeddings, vector databases, prompt-based classification,
  fuzzy matching, or probabilistic inference in TraceMap core.

## Requirements

### Requirement 1: Static ORM And Mapping Inventory Completion

**User Story:** As a maintainer, I want TraceMap to inventory old data model
mapping artifacts even when a legacy solution cannot build.

Acceptance Criteria:

1. WHEN a repository contains checked-in DBML, EDMX, typed DataSet XSD,
   TableAdapter metadata, generated data designer code, NHibernate `.hbm.xml`,
   or recognized old ORM mapping/config descriptors THEN TraceMap SHALL emit
   inventory or gap evidence with repo-relative path, line span where available,
   commit SHA, extractor version, rule ID, evidence tier, descriptor family,
   and documented limitations.
2. WHEN a mapping/config shape is recognized but not understood by the MVP THEN
   TraceMap SHALL emit `AnalysisGap` under the appropriate legacy-data rule,
   using `UnsupportedLegacyOrmDescriptor`, `UnsupportedLegacyOrmMappingShape`,
   or a narrower documented classification. It SHALL NOT silently ignore the
   descriptor or invent entity/table facts.
3. WHEN parser safety bounds, malformed XML, DTD/entity rejection, unsupported
   metadata versions, encrypted or external config, or excessive descriptor
   counts prevent extraction THEN TraceMap SHALL emit `AnalysisGap` facts,
   mark coverage reduced, and continue scanning other files.
4. Inventory and gap output SHALL be deterministic across repeated scans of the
   same repository commit.
5. Inventory facts SHALL remain static repository evidence and SHALL NOT imply
   runtime provider selection, configuration load, or database reachability.

### Requirement 2: Safe Normalized Descriptor Identity

**User Story:** As a reviewer, I want data model descriptors to have stable,
safe identities across old mapping formats.

Acceptance Criteria:

1. WHEN metadata declares an entity, mapped class, component, storage object,
   column/property/field, routine, adapter, relation, association, collection,
   key, foreign-key-like endpoint, or generated-code link THEN TraceMap SHALL
   emit or project a normalized descriptor identity when deterministic.
2. Descriptor identity SHALL include metadata format, descriptor role,
   descriptor scope, repository-relative source identity, line span where
   available, safe local display name or hash, source rule ID, supporting fact
   IDs, coverage label, and limitations.
3. Cleartext names SHALL be retained only when they pass the safe identifier
   policy. Unsafe names, private labels, provider values, SQL/config-like values,
   host/path/URL/remote-shaped values, and secret-looking tokens SHALL be
   omitted or represented by stable hashes plus redaction metadata.
4. Stable keys SHALL not include local absolute paths, raw remotes, timestamps,
   machine-specific paths, volatile row IDs, raw SQL/config values, or runtime
   environment data.
5. WHEN descriptors share a display name but differ by namespace, container,
   file, mapping scope, metadata format, source artifact, or storage identity
   THEN TraceMap SHALL preserve separate stable keys and emit ambiguity gaps
   when a downstream selector cannot distinguish them.
6. WHEN a selector matches multiple legacy data model identities that cannot be
   disambiguated by safe scope, stable ID, or hash THEN TraceMap SHALL emit
   `AmbiguousLegacyDataModelSelector` or `AmbiguousLegacyDataModelIdentity`
   according to the owning rule catalog entry.
7. Descriptor facts SHALL be capped at `Tier2Structural`. A separate
   generated-code link MAY have compiler-resolved symbol evidence, but that link
   SHALL NOT upgrade the descriptor fact tier.

### Requirement 3: Relationship Extraction And Gaps

**User Story:** As a maintainer, I want relationship evidence to be extracted
when metadata is deterministic and downgraded when it is not.

Acceptance Criteria:

1. WHEN DBML associations, EDMX associations/MSL relationship mappings, typed
   DataSet relations/constraints, or NHibernate relation descriptors expose
   deterministic endpoints THEN TraceMap SHALL emit relationship mapping
   evidence with endpoint identities, relationship kind, source spans, rule IDs,
   evidence tier, coverage label, and limitations.
2. WHEN only one endpoint is deterministic THEN TraceMap MAY emit unidirectional
   relationship evidence with a missing-inverse limitation and capped evidence
   tier.
3. WHEN many-to-many, split entity, conditional mapping, inherited mapping,
   duplicate relationship names, ambiguous association sets, composite keys,
   formula-only joins, dynamic components, or provider extensions prevent a
   deterministic endpoint decision THEN TraceMap SHALL emit
   `UnsupportedLegacyOrmMappingShape`, `AmbiguousLegacyDataModelIdentity`, or a
   documented relationship gap instead of choosing a winner.
4. Relationship evidence SHALL preserve existing source `mappingKind` values
   where stable and add model relationship semantics through additive properties
   or derived surface fields.
5. Relationship source facts and any derived relationship projection rows SHALL
   use distinct identities and SHALL NOT double-count the same source mapping as
   two terminal `legacy-data` surfaces.
6. Relationship facts SHALL NOT claim referential integrity exists in the
   database, that a runtime ORM relationship loads, that lazy loading occurs, or
   that queries execute.

### Requirement 4: NHibernate Mapping XML Completion

**User Story:** As a maintainer, I want common NHibernate mapping XML to produce
safe static metadata without loading NHibernate.

Acceptance Criteria:

1. WHEN a checked-in `.hbm.xml` file declares deterministic class, joined
   property, id, version, component, many-to-one, one-to-one, one-to-many,
   many-to-many, collection, key, table, column, discriminator, schema, catalog,
   or collection table metadata THEN TraceMap SHALL emit safe descriptor or
   relationship evidence where the shape is deterministic.
2. NHibernate parsing SHALL use the shared safe XML parser and bounds used by
   legacy data metadata extraction. Exceeding file, character, node, depth, or
   per-class descriptor caps SHALL emit a deterministic too-large/truncation gap.
3. Raw schema, catalog, formula, filter, SQL fragment, named query, query text,
   dialect, connection, provider, and config values SHALL be omitted or hashed.
4. WHEN NHibernate mappings use inheritance, joined/union subclass, composite
   id, dynamic component, formula-only mappings, filters, named queries, custom
   SQL, custom user types, provider extensions, or unsupported collection shapes
   beyond deterministic MVP rules THEN TraceMap SHALL emit needs-review or
   analysis-gap evidence.
5. NHibernate facts SHALL remain `Tier2Structural` or lower for descriptor
   evidence and SHALL NOT claim session factory load, runtime cascade behavior,
   dirty tracking, lazy loading, table existence, or query execution.

### Requirement 5: Unsupported Old ORM Descriptor Gaps

**User Story:** As a reviewer, I want unsupported old ORM descriptors to be
visible as coverage gaps rather than invisible blind spots.

Acceptance Criteria:

1. WHEN TraceMap recognizes public-safe indicators for old ORM families outside
   parser MVP, such as LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, Castle
   ActiveRecord, Fluent-only mappings, provider-specific XML/config descriptors,
   or project-local mapping DSL candidates THEN it SHALL emit
   `UnsupportedLegacyOrmDescriptor` or `UnsupportedLegacyOrmMappingShape` gaps
   with safe descriptor family labels.
2. Project-local DSL recognition SHALL require a documented deterministic signal
   taxonomy. Filename or helper-class similarity alone SHALL NOT be enough to
   classify a private DSL as ORM metadata.
3. Unsupported descriptor gaps SHALL include rule ID, evidence tier
   `Tier4Unknown`, repo-relative path, line span where available, safe family
   label or hash, coverage label, and limitations.
4. Unsupported descriptor detection SHALL NOT store raw mapping content, raw SQL,
   config values, provider values, URLs, raw remotes, local paths, private labels,
   or secrets.
5. Recognized unsupported descriptors SHALL not produce entity/table/column
   facts until a future spec defines deterministic parsing and redaction rules.

### Requirement 6: DBML, EDMX, Typed DataSet, And TableAdapter Precision

**User Story:** As a maintainer, I want the existing metadata extractors to close
known deterministic precision gaps without overreaching.

Acceptance Criteria:

1. WHEN DBML contains multiple database descriptors, provider extensions,
   ambiguous descriptors, non-ASCII or unsafe identifiers, routines, associations,
   or generated-code hints THEN TraceMap SHALL emit deterministic descriptor
   facts or exact gaps according to documented rule limitations.
2. WHEN EDMX contains missing MSL, multiple containers, inherited or split
   entities, condition mappings, many-to-many or complex mappings, duplicate
   names, provider extensions, malformed inner sections, or ambiguous association
   endpoints THEN TraceMap SHALL emit deterministic descriptors only for
   supported shapes and gaps for unsupported shapes.
3. WHEN typed DataSet or TableAdapter metadata contains relations, constraints,
   dynamic commands, stored-procedure command metadata with no static text,
   stale/missing designer output, or schemas with typed DataSet prefixes but no
   actual DataSet/TableAdapter content THEN TraceMap SHALL emit deterministic
   descriptors, command hash/shape evidence, or exact gaps as appropriate.
4. Arbitrary `.xsd` files SHALL remain gated by XSD-intrinsic typed
   DataSet/TableAdapter indicators. Generated code or filename similarity SHALL
   not qualify an unrelated XSD by itself.
5. TableAdapter SQL or command text SHALL follow existing SQL hash/shape
   conventions and SHALL NOT store raw SQL.
6. Existing descriptor tier ceilings and exact gap classifications SHALL remain
   stable unless a rule catalog amendment documents the change.

### Requirement 7: Generated-Code Linkage Boundaries

**User Story:** As a reviewer, I want mappings to link to generated or mapped
code only when static evidence supports the link.

Acceptance Criteria:

1. WHEN metadata names a generated file, custom tool output, namespace, class,
   context, DataSet/table/row/adapter type, or ORM mapped class and matching
   checked-in code is visible THEN TraceMap SHALL emit linkage evidence with
   supporting fact IDs and descriptor identity hashes.
2. WHEN Roslyn semantic analysis resolves generated or mapped symbols to a
   descriptor under scoped identity THEN linkage MAY be `Tier1Semantic`.
3. WHEN project load fails, generated files are skipped by general extractors, or
   semantic symbols are unavailable THEN TraceMap SHALL continue with scoped
   structural or syntax fallback and label the coverage reduced.
4. Global short-name matching SHALL NOT be used. Multiple files, namespaces,
   partial types, duplicate declarations, stale generated hints, or missing
   generated output SHALL emit gaps rather than arbitrary links.
5. Generated-code linkage facts SHALL NOT reclassify generated designer code as
   hand-authored business logic and SHALL NOT upgrade descriptor facts above
   their source tier ceiling.

### Requirement 8: Downstream Compatibility Boundary

**User Story:** As a TraceMap user, I want deeper model evidence to remain safe
for existing reports while larger integrations stay explicitly deferred.

Acceptance Criteria:

1. WHEN deeper mapping facts are emitted THEN `tracemap scan` SHALL continue to
   produce `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
   `logs/analyzer.log` with rule IDs, evidence tiers, commit SHA, coverage
   labels, extractor versions, and limitations.
2. Combined reports, paths, reverse, diff, impact, release-review, portfolio,
   evidence graph, vault, RAG/docs-export, and static HTML readers SHALL either
   consume the new facts safely through existing `legacy-data` behavior or emit
   explicit availability gaps. They SHALL NOT crash because a new descriptor
   role, relationship property, or unsupported-ORM gap appears.
   Implementation SHALL include at least one concrete non-crash regression for
   an unknown or future descriptor role/property in a touched reader.
3. Broad report/export work, new selectors, persisted derived surface rows,
   no-double-count behavior across every workflow, and full vault/RAG/static HTML
   expansion MAY be follow-up work when too large for this extraction slice.
4. Any downstream row that is implemented in this slice SHALL cite its own rule
   ID plus source legacy-data rule IDs and SHALL render only safe labels or
   hashes.
5. Reports SHALL use static wording such as "legacy data model metadata",
   "static descriptor evidence", "relationship descriptor", "unsupported ORM
   descriptor gap", or "needs review". They SHALL NOT say a database was touched,
   a query executed, or runtime impact is definite without reducer evidence.

### Requirement 9: Privacy, Public-Safe Samples, And Validation

**User Story:** As a maintainer, I want implementation evidence that proves the
feature works without leaking private legacy details.

Acceptance Criteria:

1. Facts, SQLite properties, reports, logs, graph/export artifacts, smoke
   summaries, and committed fixtures SHALL exclude raw SQL, raw snippets, config
   values, connection strings, secrets, URLs, raw remotes, local absolute paths,
   private sample labels, and unchecked private descriptors by default.
2. Public-safe fixtures SHOULD use synthetic neutral names and small checked-in
   metadata files that exercise DBML, EDMX, typed DataSet/TableAdapter,
   NHibernate, unsupported-ORM gaps, generated-code linkage, parser safety,
   ambiguity, and redaction.
3. Local legacy smoke validation MAY use ignored manifests under `.tmp/` with
   neutral labels only. Raw local scan artifacts SHALL remain ignored and SHALL
   not be copied into specs, docs, reports, or PR descriptions.
4. Validation SHALL include focused tests for touched extractor/report layers,
   deterministic output, exact gap classifications, privacy suppression, SQLite
   property privacy, descriptor tier ceilings, cross-format stable-key
   separation for identical display names, relationship no-double-count behavior,
   and NHibernate parser-bound or descriptor-cap gaps.
5. Validation SHALL include a CLI scan smoke against at least one public-safe or
   synthetic fixture repository that emits the required scan artifacts and a
   concrete commit SHA. The preferred smoke creates or uses a temporary committed
   fixture repository so the SHA belongs to the scanned fixture; scanning a
   checked-in TraceMap sample may use the enclosing repository SHA only when the
   implementation state records that scope decision explicitly.
6. Implementation validation SHALL run `dotnet test`, `git diff --check`,
   `./scripts/check-private-paths.sh`, and relevant pinned smoke checks from
   `docs/VALIDATION.md` or explicitly defer them with rationale in
   `implementation-state.md`.
