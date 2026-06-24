# Legacy Data Model ORM Mapping Completion Design

## Overview

This phase finishes the extraction-depth work left after the legacy data
metadata MVP, model identity work, NHibernate MVP, and first reporting/export
integration slices.

The intended evidence chain is:

```text
DBML / EDMX / typed DataSet XSD / TableAdapter / NHibernate hbm.xml / old ORM config
  -> safe descriptor identities for entities, storage objects, columns, mappings, relationships
  -> generated or mapped code links only when scoped static evidence exists
  -> scan artifacts and existing legacy-data report/export readers
  -> follow-up report/export slices where broader integration is still too large
```

Every conclusion is static checked-in repository evidence. TraceMap must not
load provider runtimes, connect to databases, execute SQL, execute designers, or
claim runtime ORM behavior.

## Goals

- Close deterministic relationship extraction and gap behavior for existing
  legacy data metadata formats.
- Complete NHibernate `.hbm.xml` descriptor depth within safe static bounds.
- Make unsupported old ORM descriptors visible as explicit gaps.
- Tighten DBML, EDMX, typed DataSet, and TableAdapter precision where prior
  specs left deterministic cases or exact-gap tests unchecked.
- Preserve generated-code linkage boundaries and reduced-coverage behavior when
  builds fail.
- Prove privacy, determinism, and artifact completeness through public-safe
  fixtures or redacted smoke evidence.

## Non-Goals

- No product-code implementation in this spec PR.
- No runtime database connection, SQL execution, schema introspection, EF model
  loading, NHibernate session factory loading, config transform execution,
  designer execution, migration execution, service activation, or provider
  runtime behavior.
- No proof of runtime query execution, database existence, lazy loading, cascade,
  dirty tracking, permissions, deployment, production use, business impact, or
  release approval.
- No arbitrary DSL execution or code-first/fluent mapping evaluation.
- No new `legacy-data-model` dependency surface kind.
- No raw SQL, snippets, config values, connection strings, remotes, URLs, local
  absolute paths, private sample names, or secrets in default outputs.
- No LLM calls, embeddings, vector stores, prompt summaries, fuzzy matching, or
  AI classification in TraceMap core.

## Existing Foundation

The implementation should build on these already documented choices:

- Existing source fact types remain the default representation for scan-time
  evidence.
- `legacy.data.model.surface.v1` is projection-only for report/export rows and
  availability gaps, not a scan-time fact type.
- `AnalysisGap` facts under `legacy.data.*` rules are not terminal dependency
  surfaces.
- `legacy-data` remains the canonical dependency surface kind.
- Descriptor facts from DBML, EDMX, typed DataSet, config, and NHibernate are
  capped at `Tier2Structural`; generated-code links are separate facts.
- Generated designer files may be inspected inside descriptor-scoped logic
  without treating them as hand-authored business logic.

## Rule And Gap Ownership

### Source Rules

Use existing source rules wherever possible:

| Rule ID | Ownership in this slice |
| --- | --- |
| `legacy.data.dbml.v1` | DBML descriptors, associations, routines, deterministic DBML gaps. |
| `legacy.data.edmx.v1` | EDMX CSDL/SSDL/MSL descriptors, mappings, associations, deterministic EDMX gaps. |
| `legacy.data.typed-dataset.v1` | Typed DataSet tables, columns, relations, constraints, TableAdapters, command hash/shape evidence, deterministic XSD gaps. |
| `legacy.data.config.v1` | Static provider/connection metadata and config gaps. |
| `legacy.data.generated-link.v1` | Existing descriptor-scoped generated code links. |
| `legacy.data.model.generated-link.v1` | Use only for model-normalized linkage semantics beyond the existing generated-link rule. |
| `legacy.data.model.relationship.v1` | Derived relationship semantics over source `LegacyDataMappingDeclared` facts, not duplicate source emission. |
| `legacy.data.orm.nhibernate.v1` | NHibernate `.hbm.xml` source facts and NHibernate parser gaps. |
| `legacy.data.orm.unsupported.v1` | Recognized old ORM descriptor/config gaps outside parser MVP. |

If a required gap cannot be represented by an existing documented classification,
the implementation must update `rules/rule-catalog.yml`, requirements, and tasks
before emitting it.

### Gap Classifications

Prefer these stable classifications. "Existing" means the classification is
already documented or used in the current legacy-data specs/codebase. "Catalog
check" means the implementation must confirm catalog coverage or amend the
catalog before emitting it from new code.

| Classification | Status for this spec | Primary use |
| --- | --- | --- |
| `MalformedLegacyDataMetadata` | Existing | Safe XML parse failure. |
| `LegacyDataParserSecurityRejected` | Existing | DTD/entity/external XML rejection. |
| `LegacyDataMetadataTooLarge` | Existing | XML/parser bounds or deterministic descriptor caps exceeded. |
| `UnsupportedLegacyDataMetadataVersion` | Existing | Recognized metadata version/dialect outside MVP. |
| `UnrelatedXsdSchemaGated` | Existing | `.xsd` lacks typed DataSet/TableAdapter indicators. |
| `UnsupportedEdmxMappingShape` | Existing legacy metadata classification | EDMX-specific unsupported mapping shape when source rule already owns that vocabulary. |
| `AmbiguousEdmxMapping` | Existing legacy metadata classification | EDMX-specific ambiguous mapping when source rule already owns that vocabulary. |
| `DynamicTableAdapterCommand` | Existing | TableAdapter command text is not complete static text. |
| `EncryptedConfigSection` | Existing | Config section is encrypted or opaque. |
| `ExternalConfigInclude` | Existing | Config source/include is external and not loaded. |
| `ConfigTransformPresent` | Existing | Transform behavior is detected but not executed. |
| `DynamicConfigConnection` | Existing | Connection/provider selection is code-built or environment-selected. |
| `MissingGeneratedCode` | Existing | Descriptor names generated output absent from checked-in files/symbols. |
| `AmbiguousGeneratedCodeLink` | Existing | Multiple generated-code candidates remain after scoped matching. |
| `UnsupportedLegacyOrmDescriptor` | Existing | Old ORM descriptor family is recognized but not parsed. |
| `UnsupportedLegacyOrmMappingShape` | Existing | Supported family uses an unsupported deterministic mapping shape. |
| `AmbiguousLegacyDataModelIdentity` | Existing | Multiple descriptor identities remain plausible after scoped matching. |
| `DuplicateLegacyDataModelSurface` | Existing/projection catalog check | Downstream projection cannot distinguish duplicate stable identities. |
| `AmbiguousLegacyDataModelSelector` | Existing/projection catalog check | Selector matches multiple model identities and needs safe disambiguation. |
| `UnknownLegacyDataModelDescriptorRole` | Net-new only if needed | Future descriptor role reaches a touched reader and must become a schema/availability gap instead of a crash. |

Use `UnsupportedLegacyOrmDescriptor` for recognized descriptor families with no
MVP parser. Use `UnsupportedLegacyOrmMappingShape` when the family is supported
but the specific mapping construct is not deterministic enough. Use
`AmbiguousLegacyDataModelIdentity` when multiple supported descriptors remain
plausible after scoped matching.

## Descriptor Identity Model

Descriptor identities should be additive properties on existing `LegacyData*`
facts or derived report rows.

Recommended safe fields:

```text
metadataFormat
sourceArtifactType
modelKind
descriptorRole
stableModelKey
displayName
displayNameHash
containerName
containerHash
storageKind
mappingKind
modelRelationshipKind
relationshipEndpointKeys
sourceMetadataFactId
supportingFactIds
supportingEdgeIds
coverageLabel
displayClearance
redactions
limitations
```

`displayClearance` is a closed presentation hint, not a descriptor value. Use
`safe-cleartext` when the selected output profile may render the safe display
name, `hash-only` when only a hash may render, and `omitted` when neither should
render. Existing claim-level or output-profile helpers should own the decision;
the extractor should only preserve enough safe metadata for that decision.

Stable identity input should include:

```text
legacy-data-model-orm-mapping-completion/v1
source rule ID
metadata format
descriptor role
descriptor-local scope
source repo-relative path or path hash
line span or canonical absence token
safe display name or display hash
safe container name or container hash
supporting descriptor hashes
```

`descriptor-local scope` means a format-specific, deterministic metadata scope:

- DBML: the containing `<Database>` scope when singular, otherwise the file path
  plus the scoped table/type/function/association path or ambiguity gap.
- EDMX: the CSDL/SSDL/MSL section, container/entity set, entity/storage type, and
  mapping fragment scope where available.
- Typed DataSet: the DataSet/TableAdapter name plus the table/relation/command
  element path inside the `.xsd`.
- NHibernate: the `.hbm.xml` file path plus the root `hibernate-mapping`
  namespace/default namespace and the containing `class` XML element's safe name
  or hash; nested descriptors add their element path under that class.
- Unsupported ORM gaps: the safe descriptor family label or hash plus the
  file/config section scope that recognized the family.

Do not include raw SQL, source snippets, raw connection strings, config values,
provider-specific values, URLs, remotes, local absolute paths, timestamps,
machine paths, or SQLite row order.

## Relationship Extraction

Represent relationships as `LegacyDataMappingDeclared` where they are source
metadata facts. Preserve existing `mappingKind` values such as `association`,
`relation`, `property-column`, or `entity-storage`, and add relationship
semantics through additive fields such as `modelRelationshipKind`.

Source extractors emit one source `LegacyDataMappingDeclared` fact for each
deterministic relationship descriptor they own. `legacy.data.model.relationship.v1`
may derive normalized relationship semantics from those source facts for report
or export rows, but it must not re-emit the same descriptor as a second scan-time
fact or second terminal `legacy-data` surface. Tests should prove a relationship
with both source and projected metadata appears once in terminal surface counts,
with the source fact cited as support.

### Supported Deterministic Shapes

- DBML associations with scoped source and target descriptors.
- EDMX CSDL associations and unambiguous MSL association-set mappings.
- Typed DataSet relations and constraints with deterministic parent and child
  table/column descriptors.
- NHibernate `many-to-one`, `one-to-one`, deterministic collection/key,
  `one-to-many`, and deterministic `many-to-many` descriptors when both
  endpoints and join metadata are safely identifiable.

### Gap Shapes

Emit gaps instead of arbitrary mappings for:

- duplicate relationship names in an indistinguishable scope;
- multiple candidate containers or association sets after scoping;
- inherited, split, conditional, or complex EDMX mappings outside MVP;
- many-to-many shapes without deterministic join and endpoint evidence;
- composite keys when endpoint identity cannot be represented safely;
- formula-only, filter-only, custom SQL, dynamic component, or provider
  extension mappings;
- generated-code-only hints with no descriptor-scoped metadata.

Relationship rows must not claim database referential integrity, runtime ORM
load behavior, or query execution.

## NHibernate Mapping XML Completion

NHibernate `.hbm.xml` parsing remains XML-only and repository-local.

### Parser Safety

Use the shared safe XML loader used by legacy data metadata extraction. Current
documented bounds are:

- 2 MiB XML file size;
- 4 MiB maximum characters in document;
- 100,000 descendant nodes;
- depth 128.

If the shared helper changes, update tests for DBML, EDMX, typed DataSet,
config, and NHibernate together. Exceeding bounds emits
`LegacyDataMetadataTooLarge` or the existing truncation classification used by
the legacy data family.

### Descriptor Caps

Keep deterministic per-class caps:

- 500 property/column-like descriptors;
- 200 relationship/collection descriptors.

Caps apply per XML `class` element. `subclass`, `joined-subclass`, and
`union-subclass` elements are unsupported relationship/modeling shapes for this
slice unless a future amendment defines them; if an implementation inventories
them before gap emission, their descriptors are not folded into the parent
class cap. This keeps cap behavior deterministic and avoids logical inheritance
hierarchy reconstruction.

When a cap is exceeded, emit facts up to the cap in deterministic order and an
analysis gap for skipped descriptors. Do not treat truncation as clean absence.

### Supported MVP Additions

The implementation slice should complete deterministic extraction for:

- class and mapped type identity;
- table and collection table descriptors;
- id, version, discriminator, property, component, nested component, and column
  descriptors when safely scoped;
- many-to-one and one-to-one relationships;
- deterministic collection descriptors for `set`, `bag`, `list`, `map`,
  `array`, and `primitive-array` XML elements when their owner class, collection
  table/key, element or target class, and safe column metadata are scoped.
  Unsupported collection element names and custom collection types emit
  `UnsupportedLegacyOrmMappingShape`;
- key, element, one-to-many, many-to-many, index/list-index/map-key descriptors
  when endpoint and column evidence is deterministic;
- schema/catalog/provider/config-like values as hashes or omitted redactions,
  never raw values.

### Explicit Gaps

Emit `UnsupportedLegacyOrmMappingShape` or another documented gap for:

- joined-subclass, union-subclass, subclass, discriminator inheritance that is
  not safely represented;
- composite-id and composite-element when endpoint identity is ambiguous;
- dynamic-component;
- formula, filter, where, sql-insert/update/delete, loader, named-query,
  sql-query, resultset, import, typedef, user-type, or provider extension shapes
  unless a future spec defines redacted deterministic handling;
- custom collection types or dialect-specific behavior;
- config-referenced mappings that cannot be resolved without runtime config
  loading.

## Unsupported Old ORM Descriptor Detection

Unsupported detection is a coverage-gap feature, not a parser.

Candidate families:

- LLBLGen project/mapping descriptors;
- SubSonic config/provider descriptors;
- iBATIS.NET/MyBatis.NET mapping XML;
- Castle ActiveRecord XML/config descriptors;
- Fluent-only NHibernate or ActiveRecord mappings that require code execution;
- project-local mapping DSL candidates with a documented deterministic signal
  taxonomy.

Detection rules must use closed family labels or hashes. They must not render
raw mapping content, provider values, SQL/config strings, raw paths, remotes,
URLs, private labels, or secrets.

Project-local DSL detection is intentionally conservative. It should require
multiple deterministic signals, such as a known checked-in mapping file
extension plus a rule-cataloged marker namespace or config section. A class name
like `Mapping` or a directory named `Mappings` is not enough by itself.

Project-local DSL signal taxonomy is deferred until an implementation has a
specific candidate. That taxonomy must be documented in the rule catalog or a
spec amendment before detection ships. Minimum shape: at least two independent
deterministic signals, a closed safe family label, false-positive limitations,
redaction rules, and tests proving generic helper names do not trigger ORM gaps.

## Existing Format Precision

### DBML

Close deterministic cases around:

- multiple `<Database>` descriptors or duplicate descriptor scopes;
- provider-extension or unsupported attributes;
- associations and routines with unsafe names;
- non-ASCII or long names requiring hashes;
- exact gap classification tests and stable fact IDs.

### EDMX

Close deterministic cases around:

- missing MSL and reduced mapping precision;
- multiple containers;
- table-per-hierarchy, split, conditional, complex, and many-to-many mappings;
- valid outer XML with malformed CSDL/SSDL/MSL sections;
- duplicate names and ambiguous association endpoints;
- exact gap classification tests and descriptor tier ceilings.

### Typed DataSet And TableAdapter

Close deterministic cases around:

- stale or missing `.designer.cs`;
- dynamic stored-procedure command metadata with no static text;
- schemas with `msdata:` prefixes but no actual DataSet/TableAdapter content;
- relation and constraint endpoint precision;
- exact gap classification tests and descriptor tier ceilings.

TableAdapter command text may produce only existing SQL hash/length and safe
shape evidence when complete static command text is visible. Raw SQL is never
stored.

## Generated-Code Linkage

Generated-code linkage uses this tier model:

| Evidence | Tier |
| --- | --- |
| Roslyn resolves a generated or mapped type symbol to a descriptor under scoped identity. | `Tier1Semantic` |
| Metadata names generated output and the scoped project contains the checked-in file/type. | `Tier2Structural` |
| Scoped syntax fallback finds a matching partial type, DataSet row/table/adapter type, context, or ORM mapped class. | `Tier3SyntaxOrTextual` |
| Missing, stale, duplicate, global short-name-only, or ambiguous candidates. | `Tier4Unknown` gap |

Rules:

- global short-name matching is forbidden;
- generated-code facts keep descriptor identity hashes and supporting fact IDs;
- generated-code linkage does not upgrade descriptor fact tiers;
- generated code is not reclassified as hand-authored business logic;
- project-load failure is reduced coverage, not a clean repository.

## Downstream Boundary

This spec's first implementation slice is extraction-first. It may include
minimal downstream safeguards needed so new facts do not break existing readers.

Required compatibility:

- `tracemap scan` required outputs remain present.
- Existing combined/report/path/reverse/export readers either continue rendering
  safe `legacy-data` rows or emit availability gaps.
- `AnalysisGap` rows under `legacy.data.*` remain gaps, not terminal surfaces.
- Unknown future descriptor roles use safe defaults.

Follow-up unless the implementation finds a narrow safe path:

- model-specific reverse selectors;
- persisted derived surface tables;
- no-double-count behavior across every report/export workflow;
- broad diff, impact, release-review, portfolio, evidence graph, vault,
  RAG/docs-export, and static HTML expansion;
- route-flow supporting descriptor rows beyond current terminal surface
  behavior.

## Public-Safe Fixture And Smoke Guidance

Implementation should prefer small synthetic fixtures checked into the test tree
over private local repositories. Fixtures should use neutral names such as
`Customer`, `Order`, `Invoice`, `Item`, and `Archive`, and should avoid real
company, server, database, remote, URL, user, or environment labels.

Fixture coverage should include:

- a DBML association and routine;
- an EDMX mapping with one unambiguous relationship and one unsupported or
  ambiguous mapping;
- a typed DataSet relation, TableAdapter static command hash/shape, and a
  dynamic command gap;
- an NHibernate mapping with class/property/id/component/collection/relation
  descriptors and unsupported formula/filter/named-query gaps;
- at least one unsupported old ORM descriptor family;
- generated-code links for deterministic file/type matches and ambiguity gaps;
- unsafe values proving hash-only handling.

CLI smoke should scan a temporary committed fixture repository or a checked-in
public-safe sample and assert:

- `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log` exist;
- manifest includes a concrete commit SHA. Preferred evidence is a temporary
  fixture repository initialized and committed during the smoke so the SHA
  belongs to the scanned fixture. If a checked-in TraceMap sample is scanned
  directly, the enclosing repository SHA is acceptable only when
  `implementation-state.md` records that scope decision;
- facts include the expected legacy-data rules and `AnalysisGap` classifications;
- raw SQL/config/secrets/remotes/URLs/local paths/private labels are absent from
  facts, SQLite properties, report, and logs;
- coverage is full only when the evidence actually supports it, otherwise
  reduced coverage is visible.

Local private smoke may use ignored `.tmp/` manifests and neutral labels, but
raw artifacts must remain ignored and must not be referenced in committed files.

## Validation Commands

Implementation PR validation should run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataMetadataExtractorTests|LegacyDataModelDescriptorProjectionTests"
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo <public-safe-fixture-repo> --out <temporary-output>
./scripts/check-private-paths.sh
git diff --check
```

The focused test filter names existing test classes at spec time; implementation
should expand the filter or add categories if it creates additional fixture or
compatibility test classes for this slice.

If language adapters, combined graph behavior, vault/RAG export, portfolio,
impact, release-review, or static HTML output are changed, follow
`docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke
checks in `implementation-state.md`.

Kiro spec review commands for this spec:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If one model is unavailable or times out, record the exact command, exit status,
and artifact path in `implementation-state.md`; do not invent approval.
