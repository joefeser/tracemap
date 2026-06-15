# Legacy Data Metadata Extraction Design

## Overview

Old .NET data access often hides important static evidence in checked-in
designer metadata:

```text
DBML / EDMX / typed DataSet XSD / TableAdapter metadata
  -> generated context/entity/DataSet/adapter code
  -> service handlers, WebForms event handlers, WCF clients, or repositories
  -> SQL/table/entity/dependency surfaces
  -> existing reports, paths, reverse queries, impact, and release-review
```

This phase adds a static metadata layer. It should parse repository-local
metadata files, emit safe facts and surfaces, and connect those facts to
generated code only when deterministic evidence exists. It must keep absence
coverage honest for old codebases that do not build locally.

Every emitted conclusion remains static evidence. TraceMap must not claim
runtime database connectivity, SQL execution, schema existence, environment
selection, branch feasibility, provider compatibility, lazy loading, change
tracking, deployment, or production usage.

## Non-Goals

- No runtime database connections, schema introspection, or SQL execution.
- No Entity Framework runtime model loading or LINQ expression evaluation.
- No config transform execution, external secret loading, or environment
  selection.
- No stored-procedure existence proof or command reachability proof.
- No arbitrary `.xsd` interpretation outside typed DataSet or gated metadata
  contexts.
- No source snippets, raw SQL, raw config values, raw connection strings, raw
  remotes, private sample identifiers, local absolute paths, or generated smoke
  artifacts in committed output.
- No LLM calls, embeddings, vector databases, prompt-based classification, fuzzy
  matching, or probabilistic inference.

## Proposed Fact Types

Prefer existing fact types where they already have the right reducer meaning:

- `QueryPatternDetected`
- `SqlTextUsed`
- `DatabaseColumnMapping`
- `ConfigKeyDeclared`
- `TypeDeclared`
- `PropertyAccessed`
- `MethodInvoked`
- `AnalysisGap`

Add language-specific fact types only where legacy data metadata has distinct
meaning:

| Fact type | Purpose |
| --- | --- |
| `LegacyDataMetadataDeclared` | Inventories parseable DBML, EDMX, typed DataSet, TableAdapter, and old ORM metadata documents. |
| `LegacyDataEntityDeclared` | Records safe conceptual/generated entity, DataSet table, row, context, or adapter type metadata. |
| `LegacyDataStorageObjectDeclared` | Records safe table, view, routine, entity set, or storage object descriptors from metadata. |
| `LegacyDataColumnDeclared` | Records safe column/property/field descriptors and hashes when names are unsafe. |
| `LegacyDataMappingDeclared` | Records unambiguous entity-to-table, property-to-column, DataSet-to-table, or adapter-to-command mappings. |
| `LegacyDataProviderConfigDeclared` | Records safe provider, connection-name, factory, and config-section metadata without raw values. |
| `LegacyDataGeneratedCodeLinked` | Links metadata descriptors to checked-in generated files or compiler-resolved symbols. |

### Fact Type Constants

Add these exact constants to the .NET fact model:

- `FactTypes.LegacyDataMetadataDeclared`
- `FactTypes.LegacyDataEntityDeclared`
- `FactTypes.LegacyDataStorageObjectDeclared`
- `FactTypes.LegacyDataColumnDeclared`
- `FactTypes.LegacyDataMappingDeclared`
- `FactTypes.LegacyDataProviderConfigDeclared`
- `FactTypes.LegacyDataGeneratedCodeLinked`

Metadata facts should carry enough safe keys for existing SQL and reducer
workflows to reuse them, but they must not overload `SqlTextUsed` or
`DatabaseColumnMapping` with guessed runtime semantics.

### Fact Type Selection Rules

- Use `LegacyDataMetadataDeclared` when the evidence is metadata document
  presence or parseability.
- Use `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, and
  `LegacyDataColumnDeclared` when metadata descriptors exist but code access is
  not proven.
- Use `LegacyDataMappingDeclared` for unambiguous descriptor-to-descriptor
  mappings such as entity-to-table or property-to-column.
- Use `LegacyDataProviderConfigDeclared` for checked-in provider or
  connection-name metadata; continue to use `ConfigKeyDeclared` for generic
  config-key evidence already emitted by the existing config extractor.
- Use `LegacyDataGeneratedCodeLinked` for deterministic links from metadata to
  generated files or symbols.
- Use existing `SqlTextUsed` and `QueryPatternDetected` only for complete static
  SQL command text and safe query shape evidence.
- Do not emit `DatabaseColumnMapping` from metadata alone. Require code-level
  access or an existing rule that specifically owns code-to-column mapping.
- Do not use `PropertyAccessed` or `MethodInvoked` for metadata descriptors.
  Those facts remain code-access and invocation evidence, including generated
  code when actual source evidence supports them.

## Proposed Rules

- `legacy.data.metadata.inventory.v1`
  - Emits metadata inventory facts for checked-in DBML, EDMX, typed DataSet XSD,
    TableAdapter, generated designer, and related old ORM descriptor files.
  - Limitations: static repository evidence only; malformed or unsupported
    metadata is a gap; arbitrary XSD files are gated.

- `legacy.data.dbml.v1`
  - Emits DBML entity, table, column, association, and routine descriptor facts.
  - Limitations: no database existence proof, no LINQ runtime behavior proof, no
    provider compatibility proof, no SQL execution proof.

- `legacy.data.edmx.v1`
  - Emits EDMX CSDL/SSDL/MSL entity, storage, property, function-import, and
    mapping facts.
  - Limitations: no EF runtime model loading proof, no lazy-loading/change
    tracking proof, no schema existence proof, ambiguous mappings become gaps.

- `legacy.data.typed-dataset.v1`
  - Emits typed DataSet, DataTable, DataColumn, relation, TableAdapter, and
    command descriptor facts.
  - Limitations: no adapter execution proof, no stored-procedure existence proof,
    no runtime command selection proof.

- `legacy.data.config.v1`
  - Emits provider/config/connection-name metadata facts from checked-in config
    with raw values hashed or omitted.
  - Limitations: no transform/environment selection proof, no secret
    availability proof, no provider installation proof.

- `legacy.data.generated-link.v1`
  - Emits deterministic links from metadata descriptors to generated files or
    symbols.
  - Limitations: generated files can be stale, absent, hand-edited, or partial;
    ambiguous links are gaps.

Existing SQL/query and reducer rules should continue to own conclusions about
SQL text, query shape, contract deltas, impact, paths, reverse roots, and
release-review sections. This spec only supplies additional static evidence.

### Rule To Fact And Tier Mapping

| Rule ID | Emits | Tier ceiling |
| --- | --- | --- |
| `legacy.data.metadata.inventory.v1` | `LegacyDataMetadataDeclared`, `AnalysisGap` | `Tier2Structural` for parseable metadata; `Tier4Unknown` for gaps |
| `legacy.data.dbml.v1` | `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, `LegacyDataColumnDeclared`, `LegacyDataMappingDeclared`, `AnalysisGap` | `Tier2Structural` for descriptor evidence; `Tier4Unknown` for gaps |
| `legacy.data.edmx.v1` | `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, `LegacyDataColumnDeclared`, `LegacyDataMappingDeclared`, `AnalysisGap` | `Tier2Structural` for descriptor and unambiguous mapping evidence; `Tier4Unknown` for gaps |
| `legacy.data.typed-dataset.v1` | `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, `LegacyDataColumnDeclared`, `LegacyDataMappingDeclared`, `SqlTextUsed`, `QueryPatternDetected`, `AnalysisGap` | `Tier2Structural` for descriptor and complete static command-text evidence; `Tier4Unknown` for gaps |
| `legacy.data.config.v1` | `LegacyDataProviderConfigDeclared`, `AnalysisGap` | `Tier2Structural` for checked-in config evidence; `Tier4Unknown` for gaps |
| `legacy.data.generated-link.v1` | `LegacyDataGeneratedCodeLinked`, `AnalysisGap` | `Tier1Semantic` for compiler-resolved generated-code links, `Tier2Structural` or `Tier3SyntaxOrTextual` for fallback links, `Tier4Unknown` for gaps |

Generated-code linkage tier is independent from metadata descriptor tier. A
`Tier1Semantic` link to generated code does not upgrade DBML, EDMX, typed
DataSet, or config descriptor facts above their rule tier ceiling.

### Rule Catalog Entries For Implementation

Task 1 should add catalog entries equivalent to:

```yaml
- id: legacy.data.metadata.inventory.v1
  name: Legacy data metadata inventory
  description: Inventories checked-in DBML, EDMX, typed DataSet XSD, TableAdapter, generated designer, config, and old ORM descriptor files with safe static metadata.
  evidenceTier: Tier2Structural or Tier4Unknown
  emits:
    - LegacyDataMetadataDeclared
    - AnalysisGap
  limitations:
    - Metadata inventory is static repository evidence and does not prove runtime data access, database existence, provider compatibility, deployment, or production usage.
    - Arbitrary XSD files are gated; unrelated schemas are not treated as typed DataSet metadata.
    - Malformed, unsupported, too-large, or parser-rejected metadata is emitted as an analysis gap rather than clean absence.
    - Raw SQL, connection strings, config values, URLs, remotes, local absolute paths, source snippets, and secrets are hashed or omitted.

- id: legacy.data.dbml.v1
  name: Legacy LINQ to SQL DBML metadata
  description: Extracts safe DBML entity, table, column, association, and routine descriptor evidence from checked-in metadata.
  evidenceTier: Tier2Structural or Tier4Unknown
  emits:
    - LegacyDataEntityDeclared
    - LegacyDataStorageObjectDeclared
    - LegacyDataColumnDeclared
    - LegacyDataMappingDeclared
    - AnalysisGap
  limitations:
    - DBML metadata is static design-time evidence and does not prove database existence, LINQ runtime behavior, routine existence, lazy loading, query execution, or provider compatibility.
    - Generated-code linkage is separate evidence and does not upgrade metadata descriptor tier.
    - Unsafe names, provider metadata, database names, connection names, and routine identifiers are hashed or omitted when required.

- id: legacy.data.edmx.v1
  name: Legacy Entity Framework EDMX metadata
  description: Extracts safe CSDL, SSDL, and unambiguous MSL entity/storage/property mapping evidence from checked-in EDMX metadata.
  evidenceTier: Tier2Structural or Tier4Unknown
  emits:
    - LegacyDataEntityDeclared
    - LegacyDataStorageObjectDeclared
    - LegacyDataColumnDeclared
    - LegacyDataMappingDeclared
    - AnalysisGap
  limitations:
    - EDMX metadata is static design-time evidence and does not prove EF runtime model loading, lazy loading, change tracking, query execution, provider compatibility, migrations, or schema existence.
    - Complex, inherited, split, conditional, many-to-many, duplicate-container, and provider-extension mappings are gaps unless a future spec defines deterministic handling.
    - Namespace URIs, provider details, unsafe identifiers, and raw metadata content are hashed or omitted.

- id: legacy.data.typed-dataset.v1
  name: Legacy typed DataSet and TableAdapter metadata
  description: Extracts safe typed DataSet table/column/relation, TableAdapter, command descriptor, and complete static command-text hash/shape evidence.
  evidenceTier: Tier2Structural or Tier4Unknown
  emits:
    - LegacyDataEntityDeclared
    - LegacyDataStorageObjectDeclared
    - LegacyDataColumnDeclared
    - LegacyDataMappingDeclared
    - SqlTextUsed
    - QueryPatternDetected
    - AnalysisGap
  limitations:
    - Typed DataSet metadata is static design-time evidence and does not prove adapter execution, stored-procedure existence, command reachability, SQL execution, provider compatibility, or database schema existence.
    - Raw SQL command text is never stored; complete static command text may produce only existing hash/length and safe shape evidence.
    - Unrelated XSD files, dynamic commands, incomplete schemas, unsupported provider metadata, and stale or missing generated code remain gaps or unsupported evidence.

- id: legacy.data.config.v1
  name: Legacy data provider and connection config metadata
  description: Extracts safe provider, connection-name, provider factory, Entity Framework provider, and ORM config metadata from checked-in config files.
  evidenceTier: Tier2Structural or Tier4Unknown
  emits:
    - LegacyDataProviderConfigDeclared
    - AnalysisGap
  limitations:
    - Config metadata is static checked-in evidence and does not prove runtime environment selection, transform application, secret availability, provider installation, database reachability, or production usage.
    - Raw connection strings, usernames, passwords, server names, catalog names, file paths, URLs, secret-looking values, and config values are hashed or omitted.
    - Encrypted sections, external config includes, transforms, and code-built connections are reduced-coverage gaps.

- id: legacy.data.generated-link.v1
  name: Legacy data generated-code linkage
  description: Links DBML, EDMX, and typed DataSet metadata descriptors to checked-in generated files or compiler-resolved generated symbols when deterministic evidence exists.
  evidenceTier: Tier1Semantic, Tier2Structural, Tier3SyntaxOrTextual, or Tier4Unknown
  emits:
    - LegacyDataGeneratedCodeLinked
    - AnalysisGap
  limitations:
    - Generated-code linkage is static evidence and does not prove generated files are fresh, runtime code paths execute, query execution occurs, or database access happens.
    - Global short-name matching is not allowed; ambiguous or missing generated-code candidates are gaps.
    - Linkage tier is recorded separately and does not upgrade the descriptor tier of the metadata evidence.
```

## Gap Classifications

Use exact classification strings so tests and reports remain stable:

| Classification | Use |
| --- | --- |
| `MalformedLegacyDataMetadata` | DBML, EDMX, typed DataSet, or config XML cannot be parsed safely. |
| `LegacyDataParserSecurityRejected` | Parser rejects DTD, entity expansion, external entity access, or unsafe XML behavior. |
| `LegacyDataMetadataTooLarge` | Metadata exceeds configured size, depth, or node-count bounds when such bounds exist. |
| `UnsupportedLegacyDataMetadataVersion` | Metadata version or dialect is recognized but unsupported in MVP. |
| `UnrelatedXsdSchemaGated` | `.xsd` lacks typed DataSet/TableAdapter indicators and is intentionally ignored. |
| `UnsupportedEdmxMappingShape` | EDMX mapping uses inheritance, complex type, split entity, condition, many-to-many, provider extension, or another unsupported shape. |
| `AmbiguousEdmxMapping` | Multiple conceptual/storage containers or mappings remain after scoping. |
| `DynamicTableAdapterCommand` | TableAdapter command text is not complete static text. |
| `EncryptedConfigSection` | Config section is encrypted or otherwise opaque. |
| `ExternalConfigInclude` | Config uses external include/source behavior not loaded by TraceMap. |
| `ConfigTransformPresent` | Config transform companion files or XDT transform attributes are present. |
| `DynamicConfigConnection` | Connection/provider selection is code-built, environment-selected, or otherwise not statically resolved. |
| `MissingGeneratedCode` | Metadata names generated output but the checked-in generated file or symbol is absent. |
| `AmbiguousGeneratedCodeLink` | Multiple generated files, types, namespaces, or symbols could satisfy a metadata descriptor. |
| `UnsupportedLegacyOrmDescriptor` | Old ORM descriptor was inventoried but no deterministic parser exists in MVP. |

This is the complete set of gap classifications for MVP legacy data metadata
extraction. Additional classifications require a spec amendment. If an
implementation finds an unmapped scenario, use the closest existing
classification and document the limitation in implementation state for future
spec work.

Absence of a metadata descriptor under reduced coverage should use an explicit
coverage gap rather than a clean "not present" conclusion.

## XML And Config Parser Safety

DBML, EDMX, typed DataSet, and old ORM descriptors are untrusted repository
inputs. XML parsing must:

- disable or prohibit DTD processing;
- set `XmlResolver = null`;
- avoid external entity resolution;
- avoid unbounded entity expansion;
- preserve line info when practical;
- enforce file-size and node-count bounds if existing scanner infrastructure
  supports them;
- emit `AnalysisGap` with safe classification when parser security settings
  reject the document.

Recommended .NET shape: use `XmlReaderSettings` with `DtdProcessing` set to
`Prohibit` or `Ignore`, `XmlResolver = null`, and load `XDocument` through the
configured reader. Config parsing should use the same safe XML reader rather
than framework APIs that may resolve external config or machine-level state.

### Required Parser Safety Tests

1. Malformed XML: unclosed tags, invalid UTF-8, and namespace errors emit
   `MalformedLegacyDataMetadata`.
2. DTD/entity rejection: documents with `<!DOCTYPE>`, external entities, or
   expansion payloads are rejected or become `LegacyDataParserSecurityRejected`.
3. No external fetch: parser tests prove no network or filesystem lookup occurs
   for external entity references.
4. Oversized documents: if scanner parser bounds exist, oversized or
   deeply-nested inputs emit `LegacyDataMetadataTooLarge`.

## Safe Identifier Policy

The implementation should centralize a safe-name policy for metadata fields.

Allowed cleartext:

- local identifiers such as entity, property, column, table, adapter, and type
  names when they are short, printable, non-secret-looking, and do not contain
  URI, filesystem, connection-string, credential, or environment syntax;
- repo-relative file paths already allowed by the fact contract;
- safe basenames for generated files.

Hashed or omitted:

- connection strings;
- server, host, catalog, database path, and file path values from config;
- usernames, passwords, tokens, API keys, or secret-looking values;
- raw SQL and command text, except existing `SqlTextUsed` hash/length facts;
- XML namespace URIs, provider manifest tokens, store schema definition
  language namespaces, and URL-like values;
- local absolute paths, raw remotes, private sample labels, and source snippets.

Hashes should use the repo's existing stable truncation convention where one
exists, and properties should say when a clear value was hashed because it was
unsafe.

Examples:

| Cleartext allowed when policy passes | Hash or omit |
| --- | --- |
| `Customer`, `OrderId`, `GetActiveUsers`, `dbo.Orders` | `Data Source=...;User ID=...` |
| `OrderAdapter`, `CustomerDataSet`, `GetById` | URL/URI values such as XML namespace URIs |
| `Orders`, `OrderItems`, `CreatedAt` | server/catalog values such as production database names |
| safe generated basenames such as `Northwind.designer.cs` | values containing `password`, `token`, `key`, `secret`, or `connectionString` case-insensitively |

Clear names longer than 128 characters should be hashed. Non-ASCII identifiers
may be retained only if they pass the same safety checks and report rendering
tests; otherwise hash them.

## DBML Extraction

File selection:

- include `.dbml` files anywhere in the scanned repository;
- include generated `.designer.cs` only as linkage candidates, not as source of
  raw metadata unless deterministic generated patterns are visible.

Extract:

- database descriptor hash and safe database name if allowed;
- tables, entity types, columns, associations;
- member-to-column metadata, nullable/primary-key/generated flags when visible;
- functions/method mappings and routine names as safe identifiers or hashes;
- generated code filename, namespace, context type, and class names when present.

Suggested properties:

| Property | Meaning |
| --- | --- |
| `metadataKind` | `Dbml` |
| `metadataHash` | Stable hash of the metadata document or logical section. |
| `entityName` | Safe entity/type name. |
| `storageObjectName` | Safe table/view/routine name, or omitted when unsafe. |
| `storageObjectHash` | Hash for unsafe or full storage identity. |
| `propertyName` | Safe entity member or property name. |
| `columnName` | Safe column name, when policy allows. |
| `columnHash` | Hash for unsafe column identity. |
| `mappingKind` | `entity-table`, `property-column`, `association`, `routine`. |
| `generatedCodeFileName` | Safe basename only. |
| `providerNameHash` | Hash of provider metadata when present and unsafe. |

Associations are static descriptors. They do not prove runtime navigation,
foreign-key enforcement, query shape, or lazy loading.

## EDMX Extraction

File selection:

- include `.edmx` files anywhere in the scanned repository;
- parse `edmx:Runtime` sections for CSDL, SSDL, and MSL metadata;
- ignore designer display sections except for safe metadata presence and line
  spans.

Extract:

- conceptual containers, entity sets, entity types, properties, navigation
  properties, associations, and function imports from CSDL;
- storage containers, entity sets, tables/views, columns, keys, functions, and
  provider metadata from SSDL;
- explicit entity-set, type, scalar-property, condition, association-set, and
  function-import mappings from MSL when unambiguous.

Mapping rules:

- `LegacyDataEntityDeclared` for conceptual entity/type evidence.
- `LegacyDataStorageObjectDeclared` for storage entity set/table/view/routine
  evidence.
- `LegacyDataColumnDeclared` for conceptual and storage member/column evidence.
- `LegacyDataMappingDeclared` only when MSL directly maps one conceptual member
  to one storage column or one entity set to one storage object.
- `AnalysisGap` for unsupported inheritance, split entities, conditional
  mappings, many-to-many relationships, duplicate names, or provider extensions
  where MVP cannot produce a deterministic safe mapping.

Evidence tier is `Tier2Structural` for parseable checked-in metadata and
unambiguous MSL mapping. Semantic generated-code linkage may separately upgrade
the generated-code link, not the metadata descriptor itself.

## Typed DataSet And TableAdapter Extraction

File selection:

- include `.xsd` files only when typed DataSet indicators are present;
- typed DataSet indicators are XSD-intrinsic and require at least one of:
  - XML namespace `urn:schemas-microsoft-com:xml-msdata` or an `msdata:` prefix;
  - attributes such as `msdata:IsDataSet="true"`,
    `msprop:Generator_UserTableName`, or `msprop:Generator_RowClassName`;
  - elements or annotations such as `xs:element` with `msdata:DataType` or
    `msdata:Relationship`;
- corroborating evidence is optional and used only after XSD-intrinsic
  indicators are present. Corroborating evidence includes checked-in
  `.designer.cs` with matching basename and generated DataSet markers such as
  `System.ComponentModel.DesignerCategoryAttribute("code")` or a
  `System.Data.DataSet` base class;
- do not gate initial `.xsd` selection on generated-code linkage or designer
  file presence alone;
- do not treat unrelated XML schemas, WCF schemas, vendor specs, docs, or
  fixtures as typed DataSet metadata.

Extract:

- DataSet, DataTable, DataColumn, relation, key, and constraint descriptors;
- TableAdapter names, method names, command kinds, command text hashes/lengths,
  command operation names, routine identifiers, and provider hints;
- generated row/table/adapter class names and generated file basenames when
  deterministic.

SQL handling:

- complete static command text may emit existing `SqlTextUsed` hash/length facts
  and safe `QueryPatternDetected` shape facts where the shared SQL extractor can
  parse a shape;
- raw SQL text must not be stored in metadata facts, reports, or validation
  summaries;
- dynamic command text, provider-specific command trees, or parameters without
  concrete text should emit a command metadata fact or analysis gap, not guessed
  table/column evidence.

## Config Provider And Connection Metadata

File selection:

- checked-in `app.config`, `web.config`, `*.dll.config`, and repository-local
  config files already scanned by the .NET adapter;
- no machine config, user secrets, environment variables, or external config
  includes are loaded.

Extract:

- `connectionStrings` entry names and provider invariant names;
- Entity Framework provider sections and default connection factory metadata;
- `DbProviderFactories` entries;
- ORM-specific config sections where deterministic and safe;
- references from DBML/EDMX/typed DataSet metadata to named connections when
  the name is safe or hashable.

Never store:

- raw connection strings;
- usernames/passwords/tokens;
- host/server/catalog names;
- file paths inside connection strings;
- provider-specific secret attributes;
- config transform output as if it were active runtime config.

### Relationship To Existing Config Extraction

The .NET adapter already emits generic config facts such as `ConfigKeyDeclared`.
The implementation should reuse the existing config file discovery and safe XML
parse path where practical, then add `LegacyDataProviderConfigDeclared` only for
data-provider and connection metadata with distinct meaning. Avoid duplicate
facts by keeping generic key evidence under existing config rules and storing
legacy data provider properties under `legacy.data.config.v1`.

Implementation details:

1. Reuse the current .NET config extraction path, expected to live in or near
   `ConfigExtractor.cs`, for `app.config`, `web.config`, and related file
   enumeration.
2. Reuse the same safe XML reader settings used by the legacy data metadata
   parser, or refactor both callers onto one helper.
3. For `<connectionStrings>`, `<DbProviderFactories>`, Entity Framework provider
   sections, and deterministic ORM config sections, emit
   `LegacyDataProviderConfigDeclared` for data-specific metadata such as
   provider name, connection name, and factory invariant name.
4. Continue emitting `ConfigKeyDeclared` for generic config-key evidence under
   existing config rules.
5. Do not emit duplicate `ConfigKeyDeclared` facts for keys already covered by
   the existing config extractor merely because the legacy data extractor also
   inspected the file.

### Config Transform Detection

Emit `ConfigTransformPresent` reduced-coverage gaps when static evidence shows:

- transform companion files such as `*.Debug.config`, `*.Release.config`, or
  other environment-specific config transforms;
- `xdt:Transform` or `xdt:Locator` attributes;
- project references to web publishing or transform targets.

Do not execute transforms. Report transform presence as a static limitation on
which config values are active at runtime.

Config linkage should remain `Tier2Structural` at best and must label transform,
external config, encrypted section, or dynamic code-built connection behavior as
reduced coverage.

## Generated-Code Linkage

Inputs:

- DBML generated code filename, namespace, context/entity class names;
- EDMX custom tool output, entity container, namespace, context/entity class
  names;
- typed DataSet generated `.designer.cs`, DataSet/table/row/adapter class names;
- compiler-resolved symbols when semantic analysis succeeds;
- syntax fallback for partial classes, generated attributes, and class names
  when builds fail.

Resolution order:

1. `Tier1Semantic`: compiler-resolved symbol linkage to a metadata descriptor.
2. `Tier2Structural`: explicit generated file or custom-tool metadata matches a
   checked-in generated file and expected type declarations.
3. `Tier3SyntaxOrTextual`: filename/type-name linkage is visible but semantic or
   structural corroboration is incomplete.
4. `Tier4Unknown`: missing, ambiguous, stale, malformed, or unsupported linkage.

Do not match generated types globally by short name alone. Scope candidates by
metadata file, generated filename, namespace/container, partial class, and
project folder where available. If multiple candidates remain, emit an ambiguity
gap.

Generated-code staleness is an explicit MVP limitation. Do not attempt timestamp,
custom-tool hash, or designer-version staleness detection in this slice. Emit
links when deterministic, `MissingGeneratedCode` when expected generated output
is absent, and `AmbiguousGeneratedCodeLink` when multiple candidates remain.

## Determinism

Sort metadata facts by:

1. metadata kind;
2. repo-relative file path;
3. source section, such as DBML table, EDMX CSDL/SSDL/MSL, typed DataSet table,
   TableAdapter command, or config section;
4. start line and start column when available;
5. safe local name or hashed identity;
6. fact ID.

Fact IDs should be derived only from stable scan ID, commit SHA, rule ID, fact
type, repo-relative path, line span, metadata kind, section kind, safe identity,
and stable hashes. Do not include timestamps, temp directories, process IDs,
machine names, local absolute paths, raw remotes, raw SQL, raw config values, or
row-order-dependent counters.

Use existing hash helpers and lowercase SHA-256 truncation conventions where
available. Repeated scans of the same commit with the same options should
produce byte-stable facts for these metadata rows.

## Report And Storage Integration

Minimum implementation:

- emit facts to `facts.ndjson` and `index.sqlite`;
- include counts and gap summaries in `report.md`;
- include extractor version and coverage labels in scan manifest/log output;
- preserve backwards compatibility for older indexes and combined imports;
- add rule catalog entries and documentation before implementation is complete.

Reducer-facing integration:

- reuse safe `typeName`, `propertyName`, `fieldName`, `tableName`, `columnName`,
  `targetSymbol`, and symbol role properties where evidence supports them;
- use metadata-specific fact types for descriptor evidence that should not be
  mistaken for runtime access;
- leave unsupported downstream consumers to ignore the facts or emit
  availability gaps.

Public wording:

- say "static design-time metadata evidence";
- say "metadata maps entity/property descriptor to table/column descriptor";
- do not say "query executes", "database is impacted", "table is used at
  runtime", or "application connects to this database" unless a separate reducer
  and supporting evidence rule actually prove the narrower static conclusion.

## Validation Strategy

Checked-in fixtures:

- DBML with safe and unsafe table/column names;
- DBML associations and routine mappings;
- EDMX with simple CSDL/SSDL/MSL entity-table and property-column mapping;
- EDMX ambiguous or unsupported mapping shapes;
- typed DataSet with DataTables, DataColumns, relations, TableAdapters, and
  static command text;
- typed DataSet `.xsd` gate that ignores unrelated schemas;
- config provider/connection metadata with redaction of connection strings and
  secret-looking values;
- generated-code linkage success, syntax fallback, missing generated file, and
  ambiguity;
- malformed XML and XXE/entity expansion rejection;
- deterministic ordering and stable fact IDs.

Validation commands for implementation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Optional ignored local smoke:

- use neutral labels only;
- keep manifests and raw outputs ignored/local-only;
- compare before/after counts for metadata files, metadata facts, mapping facts,
  SQL/query surfaces, provider/config facts, generated-code links, analysis
  gaps, and reduced coverage labels;
- commit only redacted label/count summaries if a future PR intentionally
  promotes public evidence.

## Scope Decisions

- `LegacyDataColumnDeclared` coexists with `DatabaseColumnMapping`. Metadata
  descriptors alone do not emit `DatabaseColumnMapping`; code-level mapping
  evidence must be present under an owning rule.
- EDMX MVP handles simple one-to-one scalar mappings first. Complex types,
  inheritance, condition-based mappings, split entities, many-to-many mappings,
  duplicate containers, and provider-specific extensions emit documented gaps.
- Old ORM descriptors outside DBML, EDMX, typed DataSet/TableAdapter, and config
  provider metadata are inventory-only or `UnsupportedLegacyOrmDescriptor` until
  a future spec defines deterministic parsing.
- Introduce `ScannerVersions.LegacyDataExtractor` before implementation emits
  `LegacyData*` facts.

## Suggested Implementation Slices

1. PR 1: rule catalog, fact model, extractor version constant, inventory,
   parser safety, safe identifier policy, DBML descriptor extraction, and report
   counts.
2. PR 2: config provider/connection metadata and integration with existing
   config extraction.
3. PR 3: EDMX CSDL/SSDL/MSL extraction and conservative mapping gaps.
4. PR 4: typed DataSet/TableAdapter extraction, SQL hash/shape reuse, and
   generated-code linkage.
5. PR 5: downstream report/reducer/combined-index availability handling and
   legacy validation summary integration if not already covered.

The tasks remain one implementation checklist, but PRs should keep slices small
and update this spec's implementation state as they land.
