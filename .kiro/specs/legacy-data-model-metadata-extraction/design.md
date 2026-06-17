# Legacy Data Model Metadata Extraction Design

## Overview

This phase turns existing legacy data metadata into normalized model-level
evidence and expands coverage to a small, deterministic old ORM mapping MVP.

Intended evidence chain:

```text
DBML / EDMX / typed DataSet XSD / TableAdapter / NHibernate hbm.xml / config
  -> safe model identities for entity, table, column, relationship, adapter
  -> generated or mapped code symbols when static linkage exists
  -> WebForms, WCF, ASMX, Remoting, HTTP/API, SQL/query, and dependency paths
  -> combined reports, reverse/impact, evidence graph, and vault export
```

Every output is static design-time evidence. TraceMap must not claim runtime
database access, SQL execution, EF/NHibernate runtime model loading, lazy
loading, schema existence, migrations correctness, branch feasibility,
deployment, permissions, production usage, future runtime behavior, query
execution likelihood, deployment reachability, or business impact.

## Goals

- Normalize legacy data model identities across old .NET metadata formats.
- Add a reviewable old ORM mapping MVP without runtime provider loading.
- Improve generated-code and mapped-symbol linkage while preserving reduced
  coverage and ambiguity labels.
- Project safe legacy data model surfaces into combined/path/reverse/impact and
  export workflows.
- Preserve rule IDs, evidence tiers, file spans, commit SHA, extractor version,
  source labels, fact IDs, edge IDs, coverage, and limitations.
- Keep privacy defaults strict: no raw SQL, snippets, connection strings,
  config values, remotes, URLs, secrets, or local absolute paths.

## Non-Goals

- No product-code implementation in this spec branch.
- No runtime database connections, live schema introspection, SQL execution, EF
  runtime model loading, NHibernate session factory loading, migration execution,
  designer execution, service calls, or config transform execution.
- No arbitrary `.xsd` interpretation outside gated typed DataSet evidence.
- No proof of runtime data flow, query execution, schema existence, provider
  compatibility, lazy loading, change tracking, production usage, or impact.
- No site work.
- No LLM calls, embeddings, vector databases, prompt-based classification,
  fuzzy matching, or probabilistic inference.

## Existing Foundation

The prior `legacy-data-metadata-extraction` spec introduced:

- `LegacyDataMetadataDeclared`
- `LegacyDataEntityDeclared`
- `LegacyDataStorageObjectDeclared`
- `LegacyDataColumnDeclared`
- `LegacyDataMappingDeclared`
- `LegacyDataProviderConfigDeclared`
- `LegacyDataGeneratedCodeLinked`

and rules:

- `legacy.data.metadata.inventory.v1`
- `legacy.data.dbml.v1`
- `legacy.data.edmx.v1`
- `legacy.data.typed-dataset.v1`
- `legacy.data.config.v1`
- `legacy.data.generated-link.v1`

This spec should reuse those fact types and rules when they express descriptor
evidence accurately. Additive model-level fields and derived surfaces are
preferred over schema-breaking changes.

The current reporting layer already has a terminal dependency surface kind named
`legacy-data`. This spec reuses that surface kind and adds model-specific
metadata such as `modelKind`, `metadataFormat`, and `stableModelKey`. It does
not introduce `legacy-data-model` as a separate surface kind for MVP because
parallel names would split selectors and could double-count the same evidence.

## Proposed Additions

### Fact Type And Rule Ownership

MVP implementation should reuse existing `LegacyData*` fact types with additive
properties instead of adding new scan fact types. Existing DBML, EDMX, typed
DataSet, TableAdapter, config, and generated-link extractors keep their current
primary rule IDs when they emit source facts. This spec adds model-level
properties to those facts where implementation changes their source extractors,
and adds new source emission only for NHibernate and unsupported old ORM
descriptor evidence.

| Existing fact type | Existing/source emitting rules | Model additions |
| --- | --- | --- |
| `LegacyDataMetadataDeclared` | `legacy.data.metadata.inventory.v1`, `legacy.data.dbml.v1`, `legacy.data.edmx.v1`, `legacy.data.typed-dataset.v1`, `legacy.data.config.v1`; new NHibernate inventories use `legacy.data.orm.nhibernate.v1`. | Add `metadataFormat`, safe descriptor family, and `stableModelKey` where available. |
| `LegacyDataEntityDeclared` | Existing DBML/EDMX/typed DataSet rules; new NHibernate class mappings use `legacy.data.orm.nhibernate.v1`. | Add `modelKind`, `metadataFormat`, `descriptorRole`, and normalized identity properties. |
| `LegacyDataStorageObjectDeclared` | Existing DBML/EDMX/typed DataSet rules; new NHibernate storage mappings use `legacy.data.orm.nhibernate.v1`. | Add model storage role, stable keys, and safe display/hash fields. |
| `LegacyDataColumnDeclared` | Existing DBML/EDMX/typed DataSet rules; new NHibernate property/id/key mappings use `legacy.data.orm.nhibernate.v1`. | Add property/column role, model identity, and redaction metadata. |
| `LegacyDataMappingDeclared` | Existing DBML/EDMX/typed DataSet generated mapping rules; new NHibernate mappings use `legacy.data.orm.nhibernate.v1`. | Preserve existing source `mappingKind` values and add model relationship semantics through additive properties such as `modelRelationshipKind` or derived surface fields. |
| `LegacyDataGeneratedCodeLinked` | Existing `legacy.data.generated-link.v1`; optionally `legacy.data.model.generated-link.v1` if model-normalized links need a distinct rule. | Add descriptor identity hashes and scoped model linkage metadata. |
| `AnalysisGap` | Existing source rules and new old ORM rules. | Represents parser, unsupported descriptor, ambiguity, missing generated-code, selector, and availability gaps. |

No `LegacyDataModelSurfaceProjected` fact is introduced. `legacy.data.model.surface.v1`
owns derived report/export rows, not scan-time facts, so existing prefix-based
fact readers cannot re-project an already projected surface fact.

If a later implementation needs dedicated relationship or model identity fact
types, that change requires a spec amendment and compatibility plan.

`legacy.data.model.identity.v1` and `legacy.data.model.relationship.v1` document
normalized identity and relationship projection semantics over source facts.
They do not re-emit existing DBML/EDMX/typed DataSet facts under a second rule
ID. Source extractors remain responsible for their source fact provenance.

### Rule IDs

Proposed new rule IDs:

| Rule ID | Emits | Tier ceiling |
| --- | --- | --- |
| `legacy.data.model.identity.v1` | derived identity semantics/report rows, availability gaps | Derived from source facts; descriptor conclusions capped at `Tier2Structural`; `Tier4Unknown` for gaps |
| `legacy.data.model.relationship.v1` | derived relationship semantics/report rows, availability gaps | Derived from source facts; descriptor conclusions capped at `Tier2Structural`; `Tier4Unknown` for gaps |
| `legacy.data.orm.nhibernate.v1` | `LegacyDataMetadataDeclared`, `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, `LegacyDataColumnDeclared`, `LegacyDataMappingDeclared`, `AnalysisGap` | `Tier2Structural`; `Tier4Unknown` for gaps |
| `legacy.data.orm.unsupported.v1` | `AnalysisGap` | `Tier4Unknown` |
| `legacy.data.model.generated-link.v1` | `LegacyDataGeneratedCodeLinked`, `AnalysisGap` | `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown` |
| `legacy.data.model.surface.v1` | derived `legacy-data` report/export rows, availability gaps | `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`, capped by the weakest supporting source evidence |

Existing `legacy.data.generated-link.v1` may remain the extractor rule for
generated-code links. Use `legacy.data.model.generated-link.v1` only if the
implementation needs model-normalized link semantics beyond the existing rule.
Redaction stays in the source rule limitations and safe properties; no separate
`legacy.data.model.redaction.v1` rule is added for MVP.

### Rule Limitations

Every rule must document:

- static repository/design-time evidence only;
- no runtime data access, query execution, schema existence, provider
  compatibility, lazy loading, change tracking, migrations, deployment, or
  production usage proof;
- descriptor tiers are not upgraded by generated-code links;
- unsafe identifiers and values are hashed or omitted;
- ambiguity, reduced coverage, unsupported metadata, and parser failures are
  explicit gaps.

## Data Model Identity Shape

Recommended safe properties:

```text
modelKind            entity | storage-object | column | relationship | adapter | routine | mapped-type
metadataFormat       dbml | edmx | typed-dataset | tableadapter | nhibernate-hbm | config | generated-code
descriptorRole       conceptual | storage | generated | orm-mapped | provider-config
stableModelKey       deterministic hash over safe scope and descriptor identity
displayName          safe local display name, when allowed
displayNameHash      hash when display name is unsafe or omitted
containerName        safe container or namespace segment, when allowed
containerHash        hash when unsafe or omitted
storageKind          table | view | column | routine | entity-set | relation | unknown
mappingKind          existing source value, such as association | relation | entity-storage | property-column | adapter-command | orm-class | orm-property
modelRelationshipKind relationship | none, when a source mapping represents model relationship semantics
sourceMetadataFactId supporting descriptor fact ID
supportingFactIds    sorted deterministic supporting facts
supportingEdgeIds    sorted deterministic supporting edges, if any
coverageLabel        full | reduced | unknown, using existing vocabulary where available
limitations          stable limitation codes
```

Do not include raw SQL, source snippets, raw connection strings, provider config
values, URLs, remotes, machine paths, schema locations, or secret-looking values.

## Old ORM MVP

### NHibernate Mapping XML

MVP parser responsibilities:

- safely parse checked-in `.hbm.xml` or config-referenced mapping XML using the
  same safe XML helper used by `LegacyDataMetadataExtractor`, currently
  `SafeXml.LoadDocument`;
- inventory mapping documents with `legacy.data.orm.nhibernate.v1`;
- extract class/entity mapping descriptors when `class` elements provide scoped
  `name` and optional safe table metadata;
- extract safe property, id, version, many-to-one, one-to-one, set/list/bag/map,
  key, element, many-to-many, and component descriptors only when deterministic;
- emit relationship evidence for deterministic relation descriptors;
- hash or omit schema, catalog, formula, filter, SQL fragment, query text,
  dialect, connection, and provider-specific unsafe values;
- emit gaps for unsupported inheritance, joined subclass, union subclass,
  dynamic component, composite id, formula-only mapping, filters, named queries,
  custom SQL, and provider extensions unless handled by a future spec.
- cap descriptor emission to parser-safe bounds. MVP should reuse the same XML
  helper and bounds as the implemented legacy data metadata extractor, currently
  `SafeXml`: 2 MiB XML file size, 4 MiB maximum characters in document, 100,000
  descendant nodes, and depth 128. If the shared helper changes, DBML, EDMX,
  typed DataSet, config, and NHibernate parser-bound tests must be updated
  together. Exceeding bounds emits the existing
  `LegacyDataMetadataTooLarge` classification, not a clean absence conclusion.
- cap per-class descriptor emission at 500 property/column-like descriptors and
  200 relationship/collection descriptors, then emit a truncation/too-large gap
  for the skipped descriptors. These caps are deterministic and may be tightened
  by implementation if tests document the behavior.

The MVP should not parse Fluent mappings or execute NHibernate configuration.

### Unsupported Old ORM Descriptors

Recognized but unsupported descriptors should produce `AnalysisGap` under
`legacy.data.orm.unsupported.v1` when enough evidence exists to identify the
descriptor family safely. Candidate families include:

- LLBLGen project/mapping files;
- SubSonic provider/config descriptors;
- iBATIS.NET/MyBatis.NET mapping XML;
- Castle ActiveRecord XML/config;
- project-local mapping DSL files;
- Fluent-only mappings that require code execution or provider runtime behavior.

These gaps help users understand reduced coverage without pretending the
metadata was parsed.

## Relationship Extraction Rules

Relationship evidence is represented as `LegacyDataMappingDeclared` with
source-specific mapping metadata. Existing source `mappingKind` values such as
`association` and `relation` remain stable; model-normalized relationship
semantics should be carried by additive properties such as
`modelRelationshipKind = relationship` or by derived `legacy-data` surface
fields.

- Bidirectional relationships are emitted as one relationship evidence row with
  both endpoint identities when both ends are deterministic.
- Unidirectional relationships are emitted as one relationship evidence row with
  the known endpoint and a limitation indicating the missing inverse.
- Many-to-many join-table inference is deferred unless the metadata provides a
  deterministic join descriptor with safe endpoint identities; otherwise emit
  `UnsupportedLegacyOrmMappingShape`.
- EDMX split entities, conditional mappings, unsupported inheritance, and
  ambiguous MSL relationship mappings emit `UnsupportedLegacyOrmMappingShape` or
  `AmbiguousLegacyDataModelIdentity`.
- Duplicate relationship names in different scopes keep separate stable keys;
  selector ambiguity emits `AmbiguousLegacyDataModelIdentity`.

## Generated-Code Linkage

Generated-code linkage should operate in tiers:

| Evidence | Tier |
| --- | --- |
| Roslyn resolves generated or mapped type symbol to a metadata descriptor using scoped identity | `Tier1Semantic` |
| Metadata declares generated output and the repo contains the expected checked-in file/type in the scoped project | `Tier2Structural` |
| Scoped syntax fallback finds a matching partial type, DataSet row/table/adapter type, context, or ORM mapped class without semantic resolution | `Tier3SyntaxOrTextual` |
| Missing, stale, duplicate, or ambiguous generated/mapped code | `Tier4Unknown` gap |

Global short-name matching is not allowed. Generated-code links do not upgrade
descriptor facts above their descriptor tier ceiling.

## Dependency Surface Projection

Canonical surface kind: `legacy-data`.

### Relationship To Existing `legacy-data`

The existing combined paths reader already projects legacy data facts as
`legacy-data` terminal surfaces. This spec tightens that behavior:

- `AnalysisGap` facts under `legacy.data.*` rules are never terminal surfaces;
  they remain gaps, caveats, or limitations. This corrects a pre-existing
  projection hazard for current DBML/EDMX/typed DataSet/config gaps as well as
  new old ORM gaps.
- Already projected derived rows are not re-consumed as source facts, preventing
  double projection.
- Existing `legacy-data` surfaces from the earlier data metadata MVP continue to
  render unchanged unless model-specific metadata is present.
- DBML, EDMX, typed DataSet, TableAdapter, and NHibernate descriptors for the
  same table display name remain distinct detail rows because stable keys include
  `metadataFormat` and source descriptor scope. Summary views may group by
  `storageKind` and safe display name, but detail views must preserve separate
  provenance. If a selector cannot distinguish duplicates, emit
  `DuplicateLegacyDataModelSurface` or `AmbiguousLegacyDataModelSelector`.

Suggested surface fields:

```text
surfaceKind          legacy-data
surfaceSubtype       data-model
modelKind            entity | storage-object | column | relationship | adapter | routine | mapped-type
metadataFormat       dbml | edmx | typed-dataset | tableadapter | nhibernate-hbm | config | generated-code
stableSurfaceKey     stableModelKey or derived deterministic hash
safeDisplayName      safe name or redacted hash label
sourceLabel          combined source label, where applicable
scanId               source scan ID
commitSha            source commit SHA
ruleId               legacy.data.model.surface.v1 for derived rows
sourceRuleIds        sorted source rule IDs
evidenceTier         weakest supporting evidence tier
filePath/start/end   source span when available
supportingFactIds    sorted fact IDs
supportingEdgeIds    sorted edge IDs
limitations          stable limitation codes
```

The surface projection is a bridge for report/query/export workflows. It is not
proof that the database object exists or was used at runtime.

## Integration Points

### Scan Outputs

`tracemap scan` should continue emitting required artifacts:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

Reports should summarize counts, metadata formats, coverage, rule IDs,
evidence tiers, and limitations without raw unsafe values.

### Combined Reports And Paths

Combined report readers should recognize model-enriched `legacy-data` surfaces
using the same provenance model as SQL, package, HTTP, config, and other
dependency surfaces. Paths and reverse queries may use these surfaces as
terminal evidence only when a selector can be derived from stable identity.
Name-only, ambiguous, syntax-only, or reduced-coverage links cap downstream
classification.

Reverse and diff command validators currently use explicit surface allow-lists.
Implementation must update those guards and their human-readable "must be one
of" messages to include `legacy-data` if model surfaces are selectable there.
If a workflow intentionally does not support legacy data selectors yet, it must
return a rule-backed availability gap rather than an unhandled exception.

### Impact, Release Review, And Portfolio

Diff/impact/release-review/portfolio readers should either consume
model-enriched `legacy-data` surfaces or emit explicit availability gaps. They
must not fail when indexes contain new model facts, and they must not say
"impacted" without derived rule IDs, source fact IDs, coverage, and limitations.
Switches on surface kind should have safe defaults for recognized future
metadata, and selector no-match or unsupported-surface cases should be reported
as gaps with safe messages.

### Evidence Graph And Vault Export

Evidence graph and vault export should render legacy data model evidence as
safe nodes and edges:

- source/repository nodes;
- metadata document nodes;
- model identity nodes;
- generated or mapped symbol nodes;
- dependency-surface nodes;
- evidence-backed edges with rule IDs and supporting IDs;
- limitation/gap nodes where useful.

Vault files remain public-shareable only after generated-output sentinel checks
and private-path scans pass. Hidden claim-level evidence may be omitted with a
gap rather than exposed.

## Parser Safety

All XML/config parsing must use safe settings:

- prohibit or ignore DTD processing;
- set `XmlResolver = null`;
- avoid external entity resolution;
- avoid unbounded entity expansion;
- preserve line info when practical;
- enforce file-size and node-count bounds, and a depth bound when the selected
  reader supports one;
- emit parser-security or malformed XML gaps instead of clean absence.

The parser should not use framework APIs that resolve machine config, external
config includes, provider assemblies, or environment-specific values.

MVP implementations should reuse the parser helper used by
`LegacyDataMetadataExtractor`, currently `SafeXml`, so DBML, EDMX, typed DataSet,
config, and NHibernate parser behavior stays aligned: 2 MiB file size, 4 MiB
maximum characters in document, 100,000 descendant nodes, and depth 128. If that
shared helper changes, pin the new bounds in tests and apply them consistently
to the legacy data family. Exceeding a bound emits `LegacyDataMetadataTooLarge`;
malformed XML emits `MalformedLegacyDataMetadata`; DTD/entity/security rejection
emits `LegacyDataParserSecurityRejected`; scan continues with reduced coverage.

## Gap Classifications

Use stable gap classifications so reports and tests can assert behavior:

| Classification | Use |
| --- | --- |
| `LegacyDataParserSecurityRejected` | Unsafe XML behavior such as DTD/entity expansion/external entity is rejected. Reuse the existing legacy-data family classification. |
| `MalformedLegacyDataMetadata` | XML/config/model metadata cannot be parsed safely. Reuse the existing legacy-data family classification. |
| `LegacyDataMetadataTooLarge` | File exceeds configured parser bounds. Reuse the existing legacy-data family classification. |
| `UnsupportedLegacyDataModelVersion` | Recognized format or version is outside MVP parser support. |
| `UnsupportedLegacyOrmDescriptor` | Old ORM descriptor family recognized but not parsed by MVP. |
| `UnsupportedLegacyOrmMappingShape` | Supported ORM family uses a mapping shape outside deterministic MVP handling. |
| `AmbiguousLegacyDataModelIdentity` | Multiple descriptors share an unresolved identity or selector target. |
| `AmbiguousLegacyDataModelSelector` | A selector matches multiple model identities and cannot be disambiguated without additional scope. |
| `DuplicateLegacyDataModelSurface` | Downstream surface projection cannot distinguish duplicate stable identities. |
| `MissingGeneratedDataModelCode` | Metadata references generated or mapped code absent from the repo. |
| `AmbiguousGeneratedDataModelLink` | Multiple generated/mapped code candidates match the descriptor. |
| `DynamicLegacyDataModelConfig` | Model/provider/config selection depends on runtime or environment behavior. |
| `LegacyDataModelExtractorUnavailable` | Downstream workflow reads an index that predates required extractor facts. |
| `LegacyDataModelSurfaceUnavailable` | A report/export workflow cannot project a surface from available model facts. |

Gap grouping for reports:

| Group | Classifications |
| --- | --- |
| Safety/parser | `LegacyDataParserSecurityRejected`, `MalformedLegacyDataMetadata`, `LegacyDataMetadataTooLarge` |
| Unsupported metadata | `UnsupportedLegacyDataModelVersion`, `UnsupportedLegacyOrmDescriptor`, `UnsupportedLegacyOrmMappingShape` |
| Ambiguity/duplicates | `AmbiguousLegacyDataModelIdentity`, `AmbiguousLegacyDataModelSelector`, `DuplicateLegacyDataModelSurface`, `AmbiguousGeneratedDataModelLink` |
| Missing or dynamic evidence | `MissingGeneratedDataModelCode`, `DynamicLegacyDataModelConfig`, `LegacyDataModelExtractorUnavailable`, `LegacyDataModelSurfaceUnavailable` |

Additions require a spec amendment or implementation-state note explaining why
existing classifications were insufficient.

## Test Strategy

Focused tests should cover:

- DBML normalized entity/table/column/relationship identity;
- EDMX CSDL/SSDL/MSL identity and ambiguous mapping gaps;
- typed DataSet and TableAdapter identity with unrelated XSD suppression;
- NHibernate `.hbm.xml` class/property/table/relationship MVP;
- NHibernate formula/filter/query redaction, proving no raw SQL-like values
  appear in facts, SQLite, reports, graph, or vault output;
- unsupported old ORM descriptor gaps;
- generated-code semantic link, structural link, syntax fallback, missing code,
  duplicate candidates, and no descriptor-tier upgrade;
- descriptor tier ceiling where a `Tier1Semantic` generated-code link does not
  upgrade a `Tier2Structural` descriptor or downstream surface;
- parser safety for malformed XML, DTD/entity rejection, external entity no-op,
  oversized/deep metadata where bounds exist, pinned to the shared legacy data
  parser helper's bounds;
- safety/malformed/too-large gap classification tests proving NHibernate uses
  the same strings as the existing legacy data family;
- deterministic stable keys and byte-stable reports for identical inputs;
- SQLite property redaction and scan report redaction;
- relationship ambiguity gaps and selector downgrade behavior;
- negative projection tests proving `AnalysisGap` facts under `legacy.data.*`
  are not rendered as terminal surfaces, including a pre-existing source rule
  such as `legacy.data.dbml.v1`;
- no-double-projection tests proving derived surface rows are not re-consumed;
- backward compatibility tests proving existing `legacy-data` surfaces continue
  to project unchanged;
- combined report/path/reverse/diff/impact/release-review/portfolio
  compatibility, including selector validation for `legacy-data`;
- no-double-count tests proving new model properties and NHibernate rule IDs do
  not cause inventory, summary, combined, or portfolio readers to count the same
  source fact twice;
- switch-default tests proving report/export readers handle future model
  metadata without throwing;
- evidence graph and vault export redaction.

## Fixture Strategy

New checked-in fixtures for this spec should live under
`samples/legacy-data-model/` when added. Existing DBML, EDMX, and typed DataSet
fixtures under `samples/` may remain in place for backward compatibility. Use
synthetic, neutral names such as `Customer`, `Order`, `Product`, `LineItem`, and
`InventoryItem`. Fixtures may include minimal DBML, EDMX, typed DataSet XSD,
TableAdapter, and NHibernate `.hbm.xml` examples plus unsupported-descriptor
sentinels. Do not commit `.tracemap/`, scan outputs, SQLite indexes, analyzer
logs, raw SQL beyond synthetic values needed to test hashing, real project
metadata, private sample names, local paths, connection strings, URLs, remotes,
or secrets. Before committing fixture or generated-output changes, run
`./scripts/check-private-paths.sh`.

## Validation

Implementation validation should include:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModel
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When language-adapter behavior changes, follow `docs/VALIDATION.md` and run or
explicitly defer the relevant pinned smoke checks in implementation state.
Public-safe legacy data model smokes should use checked-in synthetic fixtures or
reviewed public repositories only. Local legacy smokes must remain ignored under
`.tmp/` and summarize neutral labels/counts only.

## Suggested Implementation Slices

1. Rule catalog, fact constants, model identity helper, and redaction policy.
2. Normalized DBML/EDMX/typed DataSet model identity projection over existing
   facts.
3. NHibernate mapping XML MVP and unsupported old ORM descriptor gaps.
4. Generated-code and mapped-symbol linkage hardening.
5. Combined/path/reverse/impact/release-review/portfolio surface integration.
6. Evidence graph/vault export integration and public-safe validation fixtures.

Each slice should include tests and update task checkboxes only when the
implementation lands.
