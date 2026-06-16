# Legacy WCF Metadata Normalization Design

## Overview

The previous WCF slice established the first static service-reference evidence:

```text
config endpoint + generated client + operation contract + service host
```

Real old WCF repositories add another layer:

```text
Reference.svcmap
  -> local WSDL / DISCO / XSD files
  -> generated Reference.cs
  -> generated client methods: FooAsync
  -> generated operation contracts: BeginFoo / EndFoo
  -> service operation: Foo
```

This phase adds a deterministic metadata layer and a conservative operation alias
layer so the mapper can join old generated code without using runtime behavior or
remote service discovery.

## Current Evidence From Local Smoke

A local ignored smoke against public WCF/SVC samples produced these relevant
observations:

| Label | Commit | Current WCF signal |
| --- | --- | --- |
| `zero-k-infrastructure` | `48f6f09bc1d0266f204026580671ce867f75d6bd` | client endpoint, service endpoints, generated clients, service contracts, operation contracts, hosts, but zero mappings |
| `splendidcrm-community` | `34a56ece1fe3287c92ec80f4788fcc877716f0af` | many ASMX/SVC hosts and operations, very large reduced-coverage scan |
| `witsml` | `61e47ce2a0f0bf90bfaf08dc13f9bc361a6cc893` | service endpoint, service contract, operations, host |
| `skyquery` | `0afb09c2468f2790b25aac4744ca6943c181c28f` | service hosts, reduced coverage |
| `wcf-rest-no-svc` | `8ca2451ce488d826aedab4c420fe8e18474de7e5` | service contracts but no `.svc` host |

The key implementation driver is the `zero-k-infrastructure` shape:

- generated client contract: `PlanetWars.ServiceReference.IPlanetWarsService`;
- generated client methods: `BuildShipAsync`, `GetPlayerDataAsync`, etc.;
- operation contract methods: `BeginBuildShip`, `BeginGetPlayerData`, etc.;
- checked-in `.svcmap`, `.wsdl`, `.disco`, and `.xsd` metadata exists.

The raw smoke artifacts remain local-only under ignored validation output and are
not part of the spec.

## Proposed Fact Types

Add fact types only if existing fact types cannot safely express the evidence.
Suggested additions:

- `WcfServiceReferenceMetadataDeclared`
- `WcfMetadataOperationDeclared`

Operation alias facts are not proposed for the first implementation. Operation
aliases should be computed in-process and rendered on mapping facts or ambiguity
gaps through safe properties such as `normalizationKind`,
`normalizedOperationName`, and supporting fact IDs or metadata hashes. This
avoids a new stable-key surface and keeps the public fact stream focused on
evidence, not intermediate join state.

Existing fact types reused:

- `WcfGeneratedClientDeclared`
- `WcfOperationContractDeclared`
- `WcfServiceReferenceMapping`
- `AnalysisGap`

## Proposed Rules

Suggested new rules:

- `legacy.wcf.metadata.v1`
  - Emits service-reference metadata and metadata operation facts from checked-in
    `.svcmap`, `.wsdl`, `.disco`, and related files.
  - Limitations: static checked-in metadata only, no remote fetch, no runtime
    reachability, no binding compatibility proof.

- `legacy.wcf.operation-normalization.v1`
  - Governs deterministic operation aliasing used by metadata-backed mappings
    and ambiguity gaps, such as `FooAsync -> Foo` and
    `BeginFoo`/`EndFoo -> Foo`.
  - Limitations: deterministic naming convention only, no fuzzy matching, no
    semantic proof of implementation dispatch.

Existing `legacy.wcf.mapping.v1` should continue to emit final mapping facts and
ambiguity gaps. Mapping properties should identify which evidence path was used.

## Metadata Extraction

### File Selection

Inspect repository files that are already inventoried or can be safely added to
inventory:

- `.svcmap`
- `.wsdl` only when scoped to a service-reference folder
- `.disco` for inventory-only metadata presence
- `.xsd` for inventory-only metadata presence when co-located with a `.svcmap`
  file or when its repository-relative path contains a service-reference segment
  such as `Service Reference` or `ServiceReference`

Recommended inventory kind: `ServiceReferenceMetadata`.

The shared service-reference folder rule is:

- the file is co-located with a `.svcmap`; or
- the repository-relative path contains a segment named `Service Reference` or
  `ServiceReference`.

Do not globally inventory arbitrary `.wsdl`, `.disco`, or `.xsd` files. Vendor
specifications, fixtures, docs, typed DataSet schemas, and unrelated XML schemas
must not become WCF metadata merely because of their extension.

Do not inspect external URLs. Do not fetch imports. Only parse files present in
the repository.

### XML Parser Safety

Metadata files come from scanned repositories and must be treated as untrusted
input. XML parsing for `.svcmap`, `.wsdl`, `.disco`, and `.xsd` metadata must:

- disable or prohibit DTD processing;
- set `XmlResolver = null`;
- avoid external entity resolution;
- avoid expanding unbounded entities;
- emit `AnalysisGap` with classification `MalformedWcfMetadata` when parsing
  fails or parser security settings reject the document.

In .NET this should use `XmlReaderSettings` with `DtdProcessing` set to
`Prohibit` or `Ignore` and `XmlResolver = null`, then load `XDocument` from the
configured reader.

## Fact Property Contracts

### `WcfServiceReferenceMetadataDeclared`

Suggested evidence tier: `Tier2Structural` for parseable checked-in metadata,
`Tier4Unknown` only through `AnalysisGap` for malformed metadata.

Allowed properties:

| Property | Meaning |
| --- | --- |
| `metadataKind` | `SvcMap`, `Wsdl`, `Disco`, or `Schema` |
| `metadataHash` | Hash of the checked-in metadata document or logical metadata group |
| `metadataFileName` | Safe basename only, when available |
| `serviceReferenceFolder` | Safe folder label or hash, never a local absolute path |
| `generatedCodeFileName` | Safe generated code basename from `.svcmap`, when available |
| `metadataSourceHash` | Hash of remote URL/source value, when present |
| `localMetadataFileNames` | Semicolon-delimited safe basenames from `.svcmap`, when available |
| `sourceFormat` | `svcmap`, `wsdl`, `disco`, or `xsd` |

Never store raw remote URLs, raw source URLs, SOAP action values, schema
locations, local absolute paths, or full metadata contents.

### `WcfMetadataOperationDeclared`

Suggested evidence tier: `Tier2Structural` when the operation comes from a
parseable checked-in WSDL `portType/operation`.

Allowed properties:

| Property | Meaning |
| --- | --- |
| `operationName` | Safe WSDL operation NCName |
| `portTypeName` | Safe WSDL portType NCName, when available |
| `contractName` | Safe contract/portType identity used for joining, when available |
| `metadataHash` | Supporting WSDL metadata hash |
| `metadataFileName` | Safe WSDL basename |
| `serviceReferenceFolder` | Safe folder label or hash |
| `metadataSourceKind` | `checked-in-wsdl` |
| `sourceFormat` | `wsdl` |

Optional `serviceName`, `bindingName`, and `portName` may be included only when
they pass the safe identifier policy. URL-like namespace values, SOAP actions,
endpoint locations, and schema locations must be hashed or omitted.

### `.svcmap`

Parse XML with line info. Extract safe, deterministic fields:

- metadata file basenames or relative-safe names where available;
- metadata kind;
- generated code filename basename where available;
- remote URL hash for any URL-like metadata source;
- local metadata file hash or metadata group hash;
- service reference folder label if it can be derived from safe relative path
  segments.

Do not store:

- raw remote URLs;
- local absolute paths;
- full source snippets;
- config values.

### WSDL

Parse XML with namespace-insensitive local-name matching. Extract:

- `portType` names when safe;
- `operation` names under `portType` when safe;
- optional safe service/binding/port names;
- metadata document hash;
- metadata source kind.

Avoid over-parsing:

- no SOAP action raw values;
- no endpoint location raw URLs;
- no schema body storage;
- no message payload shape inference in this slice.
- no raw `targetNamespace`, XML namespace URI, schema namespace, or SOAP action
  namespace values; URL-like namespace values must be hashed or omitted, and only
  safe local NCName identifiers may be retained.

### DISCO and XSD

For `.disco`, record safe metadata presence and hash URL-like references. Do not
derive operation metadata from `.disco` in this slice.

For `.xsd`, inventory and hash metadata documents but do not infer DTO/property
contract mappings in this slice. Schema-to-DTO mapping is a future spec. Do not
globally inventory all `.xsd` files; use the service-reference folder/co-location
gate from file selection to avoid typed-DataSet and unrelated schema noise.

## Operation Alias Strategy

Derive aliases only after WCF-generated evidence exists.

### Generated Client Methods

For `WcfGeneratedClientDeclared` method facts:

| Original | Alias | Rule |
| --- | --- | --- |
| `FooAsync` | `Foo` | suffix `Async` removal only when the client is WCF-generated and the alias is corroborated by WSDL metadata, a same-contract sync sibling, or an aligned service operation |
| `BeginFoo` | `Foo` | APM begin alias only when paired with `EndFoo` on same contract/type |
| `EndFoo` | `Foo` | APM end alias only when paired with `BeginFoo` on same contract/type |

Keep the original operation name as a live candidate. The normalized alias is an
additional candidate for mapping, not a replacement.

Do not alias framework lifecycle operations unless checked-in metadata explicitly
contains the same operation name. Apply the exclusion to both raw method names and
normalized base names:

- `Open`
- `Close`
- `Abort`
- `Dispose`
- `OpenAsync`
- `CloseAsync`
- `BeginOpen` / `EndOpen`
- `BeginClose` / `EndClose`
- `BeginAbort` / `EndAbort`
- any `BeginX` / `EndX` pair where `X` is an excluded lifecycle verb

### Operation Contracts

For `WcfOperationContractDeclared` facts:

- Keep the original `operationName`.
- Derive alias data for APM pairs when the same contract has both `BeginFoo` and
  `EndFoo`.
- Add metadata operation support when checked-in WSDL names `Foo`.

### Alias Data Properties

Alias data should be carried on final mapping facts or ambiguity gaps when it is
used. Suggested properties:

| Property | Meaning |
| --- | --- |
| `originalOperationName` | Original method/operation name |
| `normalizedOperationName` | Derived operation alias |
| `normalizationKind` | `AsyncSuffix`, `ApmBeginEndPair`, or `MetadataOperation` |
| `clientContractName` | Client contract when available |
| `contractName` | Service contract when available |
| `metadataHash` | Supporting metadata hash when available |
| `supportingFactIds` | Semicolon-delimited sorted fact IDs where safe and stable enough |

Alias evidence can be considered `Tier2Structural` when backed by checked-in WCF
metadata or a credible APM pair. The final mapping fact still follows the mapping
tier rules: without aligned config endpoint evidence, the mapping is no stronger
than `Tier3SyntaxOrTextual`.

## Mapping Strategy

Map in this order:

1. Exact current mapping path:
   `clientContractName + operationName + endpoint/contract`.
2. Metadata-backed normalized path:
   `clientContractName + normalizedOperationName + WSDL operation + contract`.
   The WSDL operation must be connected to the generated client by `.svcmap`
   local metadata linkage, service-reference folder identity, or safe
   `portType`/contract identity. A random repository-level WSDL operation name is
   not sufficient corroboration.
3. APM-pair normalized path:
   `clientContractName + normalizedOperationName + Begin/End operation pair`.
4. Generated-code-only normalized path:
   no stronger than `Tier3SyntaxOrTextual`, and only when contract identity is
   already aligned.

Before ambiguity counting, group candidates by logical operation identity:

```text
clientContractName + contractName + normalizedOperationName + metadata identity
```

Convergent generated forms such as `Foo`, `FooAsync`, `BeginFoo`, and `EndFoo`
on the same contract represent one logical operation when they share the same
normalized operation name and supporting metadata/contract identity. They should
collapse to one mapping candidate. They must not re-trigger the existing
`matchingOperations.Length > 1` ambiguity behavior merely because Begin/End or
sync/async forms both exist.

When several generated forms support the same logical operation, choose one
mapping fact by deterministic confidence order and record the selected
`normalizationKind`:

1. Exact original operation name.
2. Metadata-backed async suffix alias.
3. Metadata-backed APM pair alias.
4. Generated-code-only alias.

If two candidates have the same confidence but distinct contracts, metadata
identities, endpoint identities, or host candidates, emit ambiguity instead of
choosing a winner.

Mapping properties should include:

- `mappingKind`
  - `config-contract-and-operation-name`
  - `metadata-operation-normalized`
  - `apm-operation-normalized`
  - `generated-client-operation-normalized`
- `operationName`
- `normalizedOperationName`
- `clientContractName`
- `contractName`
- `endpointCount`
- `hostCount`
- `metadataOperationCount`
- `normalizationKind`

Ambiguity rules:

- If more than one logical operation remains after contract, normalized-name, and
  metadata filtering,
  emit `AnalysisGap` with classification `AmbiguousWcfNormalizedMapping`.
- If metadata links multiple local WSDL files that each define the same operation
  under different safe contracts, emit `AnalysisGap` with classification
  `AmbiguousWcfMetadataContractMapping`.
- If only raw remote metadata exists and no checked-in metadata file can be parsed,
  emit `AnalysisGap` with classification `MissingLocalWcfMetadata` rather than a
  mapping.
- If metadata XML is malformed or rejected by safe XML parser settings, emit
  `AnalysisGap` with classification `MalformedWcfMetadata`.
- If metadata exists but cannot be connected to the generated client by `.svcmap`,
  service-reference folder, or safe portType/contract identity, emit
  `AnalysisGap` with classification `UnlinkedWcfMetadata`.

## Safety

Do not render or store by default:

- raw endpoint addresses;
- raw WSDL URLs;
- SOAP action values;
- schema contents;
- local absolute paths;
- private repository names;
- raw source snippets;
- raw SQL;
- config values;
- secrets or connection strings.

Safe identifiers must pass the existing safe identifier policy or be hashed/omitted.

## Determinism

- Sort metadata files by repository-relative path.
- Sort XML extracted records by line number, then safe name.
- Sort supporting IDs/hashes with ordinal ordering.
- Do not add timestamps to deterministic output.
- Use lowercase SHA-256 truncation via existing helpers.

## Validation Plan

Focused tests:

- `.svcmap` with local WSDL link emits metadata fact and no raw URL.
- WSDL `portType/operation` emits safe metadata operation facts.
- `FooAsync` generated client maps to WSDL/operation `Foo`.
- `BeginFoo`/`EndFoo` pair maps to normalized operation `Foo`.
- `Foo`, `FooAsync`, and `BeginFoo`/`EndFoo` converging on one contract collapse
  to one logical operation and produce one mapping candidate.
- lone `BeginFoo` and lone `EndFoo` do not normalize.
- lifecycle methods such as `CloseAsync` do not map without explicit metadata.
- `BeginOpen`/`EndOpen`, `BeginClose`/`EndClose`, and `BeginAbort`/`EndAbort` do
  not produce lifecycle aliases.
- ambiguous normalized candidates emit gap and no normal mapping.
- malformed metadata emits `AnalysisGap`.

Local smoke:

```bash
python3 scripts/legacy_codebase_validation.py \
  .tmp/legacy-codebase-validation/wcf-svc-smoke.local.json \
  .tmp/legacy-codebase-validation/wcf-svc-smoke-out
```

The smoke manifest and raw outputs remain ignored. Summaries are label-only and
hidden public claim level.

Required final validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

## Follow-Ups

- Semantic WCF enrichment when `MSBuildWorkspace` succeeds.
- WebForms event-to-service path reporting.
- ASMX method metadata beyond host declaration.
- DBML/EDMX entity/table mapping.
- WSDL/XSD DTO schema-to-code mapping.
- Progress reporting and artifact-size controls for very large legacy smokes.
