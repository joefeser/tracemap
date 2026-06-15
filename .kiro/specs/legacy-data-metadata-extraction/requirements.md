# Legacy Data Metadata Extraction Requirements

## Introduction

TraceMap now has stronger static evidence for old WCF service metadata and
WebForms event flow, but many legacy .NET applications describe their data
surface in design-time metadata rather than modern code-first patterns. Common
examples include LINQ to SQL `.dbml`, Entity Framework `.edmx`, typed
DataSet `.xsd`, TableAdapter command metadata, `app.config` / `web.config`
provider and connection declarations, and old ORM or service data descriptors.

This spec defines a deterministic metadata extraction layer that turns those
checked-in artifacts into safe TraceMap facts and SQL/table/entity surfaces. It
must help reducers and reports explain static evidence such as "this generated
entity property maps to this table/column metadata" without claiming runtime
query execution, database existence, deployment, configuration selection, or
branch feasibility.

Public claim level: hidden until validated with redacted label-only summaries or
checked-in public fixtures.

## Scope

In scope:

- Inventory checked-in `.dbml`, `.edmx`, typed DataSet `.xsd`, `.xsd` designer
  companions, TableAdapter metadata, and related generated `.designer.cs` files.
- Extract safe entity/table/column/association/stored-procedure/function-import
  metadata from DBML, EDMX CSDL/SSDL/MSL, and typed DataSet schemas.
- Extract static provider, connection-name, provider-invariant-name, and
  connection-string-presence metadata from `app.config`, `web.config`, and
  related checked-in config files without storing raw connection strings.
- Connect metadata to generated code surfaces when deterministic file/type
  linkage is visible.
- Feed existing SQL/query, persistence, dependency-surface, WebForms, WCF, path,
  reverse, and reducer workflows where evidence keys are safe and stable.
- Emit explicit `AnalysisGap` facts for malformed XML, unsupported metadata
  versions, ambiguous generated-code linkage, dynamic config selection, or
  unsupported ORM descriptors.
- Update rule catalog, adapter contract docs, validation guidance, scan reports,
  and legacy validation summaries when implementation lands.

Out of scope:

- No scanner implementation in this spec branch.
- No site pages or site specs.
- No runtime database connections, database introspection, migration execution,
  SQL execution, service calls, IIS activation, or config transform execution.
- No proof of runtime data flow, branch feasibility, query plan, permissions,
  schema existence, provider compatibility, or production usage.
- No parsing of arbitrary raw SQL from config values or design-time metadata
  beyond existing safe hash/shape evidence rules.
- No global `.xsd` table/entity inference outside typed DataSet or gated data
  metadata contexts.
- No ORM-specific runtime behavior emulation, lazy-loading proof, change tracking
  proof, or LINQ expression evaluation.
- No raw SQL, connection strings, provider-specific secrets, config values,
  source snippets, raw remotes, private sample names, local absolute paths, or
  unchecked sample artifacts in committed files.
- No LLM calls, embeddings, vector databases, prompt-based classification, fuzzy
  matching, or probabilistic model inference in TraceMap core.

## Requirements

### Requirement 1: Legacy Data Metadata Inventory

**User Story:** As a maintainer, I want TraceMap to recognize old data/ORM
metadata even when the legacy solution cannot build.

Acceptance Criteria:

1. WHEN a repository contains checked-in `.dbml`, `.edmx`, typed DataSet `.xsd`,
   TableAdapter metadata, generated `.designer.cs`, or data-related config files
   THEN TraceMap SHALL inventory those files with safe repo-relative paths, line
   spans where available, commit SHA, extractor version, rule ID, and evidence
   tier.
2. WHEN `.xsd` files are evaluated for typed DataSet evidence THEN TraceMap SHALL
   require typed DataSet indicators such as dataset namespaces, DataSet designer
   annotations, TableAdapter annotations, or deterministic generated-code
   linkage; arbitrary schemas SHALL NOT become data metadata facts merely because
   they use `.xsd`.
3. WHEN generated `.designer.cs` files are inspected for data metadata linkage
   THEN TraceMap SHALL treat them as generated static evidence and SHALL NOT
   classify them as hand-authored business logic.
4. WHEN metadata files cannot be parsed or are too large for configured bounds
   THEN TraceMap SHALL emit `AnalysisGap` with `Tier4Unknown` and a gap
   classification rather than claiming absence of data metadata.
5. Inventory ordering and fact IDs SHALL be deterministic across repeated scans
   of the same repository commit.

### Requirement 2: DBML Metadata Extraction

**User Story:** As a maintainer, I want LINQ to SQL DBML files to expose static
entity, table, column, association, and routine surfaces.

Acceptance Criteria:

1. WHEN a parseable `.dbml` contains database, table, type, column, association,
   function, or method metadata THEN TraceMap SHALL emit safe metadata facts for
   visible entity and persistence surfaces.
2. DBML table and column identifiers SHALL be stored only when they pass the safe
   identifier policy; otherwise TraceMap SHALL store stable hashes and clear
   unsafe-name classification properties.
3. WHEN a DBML function, stored procedure, or method mapping is visible THEN
   TraceMap SHALL emit routine metadata as static descriptor evidence and SHALL
   NOT claim the routine exists, is reachable, or executes.
4. WHEN DBML metadata references provider, connection, or database names THEN
   TraceMap SHALL hash or omit unsafe values and SHALL NOT store connection
   strings, server names, catalog names, credentials, or config values.
5. DBML facts SHALL be no stronger than `Tier2Structural` unless a future
   semantic pass links generated code symbols with compiler-resolved evidence.
   Generated-code linkage tier SHALL be recorded separately and SHALL NOT
   upgrade the descriptor tier.

### Requirement 3: EDMX Metadata Extraction

**User Story:** As a reviewer, I want Entity Framework EDMX metadata to explain
conceptual entities, storage tables, and deterministic mappings without
overclaiming runtime EF behavior.

Acceptance Criteria:

1. WHEN a parseable `.edmx` contains CSDL entity sets, entity types, properties,
   navigation properties, associations, SSDL entity sets, storage properties,
   function imports, or MSL mappings THEN TraceMap SHALL emit safe metadata facts
   with source section, file span when available, rule ID, and evidence tier.
2. WHEN CSDL-to-SSDL mapping is explicit and unambiguous in MSL THEN TraceMap
   SHALL emit entity-to-table and property-to-column mapping facts at
   `Tier2Structural`.
3. WHEN conceptual or storage names are namespace-qualified, URL-like, provider
   specific, or unsafe for public reports THEN TraceMap SHALL keep safe local
   names only where policy allows and SHALL hash or omit unsafe components.
4. WHEN EDMX metadata has multiple containers, ambiguous mappings, unsupported
   inheritance shapes, complex types, many-to-many mappings, or provider
   extensions outside MVP scope THEN TraceMap SHALL emit needs-review or
   analysis-gap evidence rather than choosing an arbitrary mapping.
5. EDMX extraction SHALL NOT claim EF runtime model loading, lazy loading,
   change tracking, provider compatibility, migrations, query execution, or
   database schema existence.
6. EDMX descriptor and mapping facts SHALL be no stronger than
   `Tier2Structural`; generated-code linkage tier SHALL be recorded separately
   and SHALL NOT upgrade the descriptor tier.

### Requirement 4: Typed DataSet And TableAdapter Extraction

**User Story:** As a maintainer, I want typed DataSet and TableAdapter metadata
to become static table, column, adapter, and command surfaces.

Acceptance Criteria:

1. WHEN a typed DataSet `.xsd` contains tables, columns, relations, constraints,
   TableAdapters, commands, or provider annotations THEN TraceMap SHALL emit safe
   typed DataSet metadata facts.
2. WHEN TableAdapter command metadata contains static command text THEN TraceMap
   SHALL reuse existing SQL hash/shape evidence conventions where possible and
   SHALL NOT store raw SQL text.
3. WHEN TableAdapter command metadata references stored procedures or command
   names THEN TraceMap SHALL store safe identifiers or hashes and SHALL NOT prove
   procedure existence or execution.
4. WHEN generated typed DataSet `.designer.cs` code links to dataset/table/row
   types or TableAdapter classes THEN TraceMap SHALL emit generated-code linkage
   evidence only when file or type identity is deterministic.
5. WHEN typed DataSet metadata is incomplete, generated code is missing/stale, or
   command text is dynamic or provider-specific THEN TraceMap SHALL emit partial
   coverage or needs-review gaps.
6. Typed DataSet and TableAdapter descriptor facts SHALL be no stronger than
   `Tier2Structural`; generated-code linkage tier SHALL be recorded separately
   and SHALL NOT upgrade the descriptor tier.

### Requirement 5: Config Provider And Connection Metadata

**User Story:** As a reviewer, I want config files to explain provider and
connection-name evidence without leaking secrets or implying runtime selection.

Acceptance Criteria:

1. WHEN checked-in `app.config`, `web.config`, or related config files contain
   `connectionStrings`, `DbProviderFactories`, Entity Framework provider
   sections, named provider metadata, or ORM configuration sections THEN
   TraceMap SHALL emit safe provider/config metadata facts.
2. Raw connection strings, usernames, passwords, server names, catalog names,
   filesystem paths, URLs, and secret-looking values SHALL be omitted or hashed.
3. Named connection references MAY be retained only when they pass the safe
   identifier policy; otherwise they SHALL be hashed.
4. Config facts SHALL say that config metadata is static checked-in evidence and
   does not prove runtime environment selection, transform application, secret
   availability, provider installation, or database reachability.
5. WHEN config transforms, environment-specific overrides, code-built
   connections, encrypted sections, or external config files are detected THEN
   TraceMap SHALL emit reduced-coverage or unsupported-config gaps.

### Requirement 6: Generated-Code And Surface Linkage

**User Story:** As a maintainer, I want old metadata descriptors to connect to
generated code and existing dependency surfaces when static evidence supports
the link.

Acceptance Criteria:

1. WHEN DBML, EDMX, or typed DataSet metadata names a generated code file, custom
   tool output, namespace, class, entity type, row type, or adapter type, and a
   matching checked-in generated file or symbol is visible, TraceMap SHALL emit
   linkage evidence with supporting fact IDs.
2. WHEN semantic analysis resolves generated entity, context, DataSet, row, or
   adapter symbols THEN linkage MAY be `Tier1Semantic`; when only file/type
   metadata aligns it SHALL be `Tier2Structural` or `Tier3SyntaxOrTextual` as
   appropriate.
3. WHEN multiple generated files, namespaces, partial classes, or symbols could
   satisfy the same metadata descriptor THEN TraceMap SHALL emit ambiguity gaps
   rather than selecting an arbitrary winner.
4. WHEN WebForms event flow, WCF mappings, SQL/query facts, or reducer context
   refer to generated data types or adapter calls, TraceMap MAY include these
   metadata surfaces as supporting static context without claiming runtime data
   flow.
5. Linkage properties SHALL preserve original metadata identity hashes and
   deterministic supporting fact IDs where safe.

### Requirement 7: Reporting, Reducer, And Combined-Index Integration

**User Story:** As a TraceMap user, I want legacy data metadata to appear in
scan reports and downstream static-evidence workflows without changing the
meaning of existing reducer conclusions.

Acceptance Criteria:

1. WHEN `tracemap scan` emits legacy data metadata facts THEN `facts.ndjson`,
   `index.sqlite`, `report.md`, and `logs/analyzer.log` SHALL include
   deterministic counts, rule IDs, evidence tiers, coverage labels, and known
   limitations.
2. WHEN metadata surfaces correspond to existing SQL/table/entity concepts THEN
   TraceMap SHALL reuse existing reducer-compatible keys where possible, such as
   `tableName`, `columnName`, `fieldName`, `propertyName`, `typeName`,
   `targetSymbol`, and safe hash fields.
3. WHEN existing reducers consume metadata-derived surfaces THEN findings SHALL
   remain evidence-backed and SHALL NOT say "impacted" without reducer rule IDs,
   supporting facts, coverage labels, and limitations.
4. WHEN combined reports, paths, reverse queries, impact, release-review, or
   portfolio commands encounter legacy data metadata facts they do not yet
   consume, they SHALL either ignore them safely or emit explicit availability
   gaps; they SHALL NOT fail because newer metadata facts exist.
5. Reports SHALL phrase these facts as static design-time metadata evidence, not
   runtime data access, SQL execution, or production database use.

### Requirement 8: Rule Catalog, Documentation, And Validation

**User Story:** As a reviewer, I want every legacy data metadata conclusion to be
rule-backed, documented, and testable.

Acceptance Criteria:

1. New or changed rule IDs SHALL be documented in `rules/rule-catalog.yml` with
   emitted fact types, evidence tiers, safe properties, and limitations before
   implementation is considered complete.
2. `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, and
   `docs/ACCEPTANCE.md` SHALL be updated if new fact types, report sections,
   validation commands, or reducer-facing keys are added.
3. Checked-in fixtures SHALL cover DBML, EDMX, typed DataSet, TableAdapter,
   config provider metadata, generated-code linkage, ambiguity, malformed XML,
   unsafe value suppression, deterministic ordering, and reduced coverage.
4. Local legacy smoke validation, when used, SHALL remain ignored/local-only and
   label-only; committed summaries SHALL NOT include local paths, raw remotes,
   private sample names, raw SQL, connection strings, config values, secrets, or
   snippets.
5. Validation SHALL run .NET build/test, validation-summary tests when touched,
   private-path guard, `git diff --check`, and any relevant pinned smoke checks
   from `docs/VALIDATION.md`.
