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
- `WcfOperationAliasDeclared`

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
  - Emits generated operation alias facts such as `FooAsync -> Foo` and
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
- `.wsdl`
- `.disco`
- `.xsd` when under a service-reference metadata folder

Recommended inventory kind: `ServiceReferenceMetadata`.

Do not inspect external URLs. Do not fetch imports. Only parse files present in
the repository.

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

### DISCO and XSD

For `.disco`, record safe metadata presence and hash URL-like references.

For `.xsd`, inventory and hash metadata documents but do not infer DTO/property
contract mappings in this slice. Schema-to-DTO mapping is a future spec.

## Operation Alias Strategy

Derive aliases only after WCF-generated evidence exists.

### Generated Client Methods

For `WcfGeneratedClientDeclared` method facts:

| Original | Alias | Rule |
| --- | --- | --- |
| `FooAsync` | `Foo` | suffix `Async` removal |
| `BeginFoo` | `Foo` | APM begin alias only when paired with `EndFoo` on same contract/type |
| `EndFoo` | `Foo` | APM end alias only when paired with `BeginFoo` on same contract/type |

Do not alias framework lifecycle operations unless metadata explicitly contains
the same operation name:

- `Open`
- `Close`
- `Abort`
- `Dispose`
- `OpenAsync`
- `CloseAsync`

### Operation Contracts

For `WcfOperationContractDeclared` facts:

- Keep the original `operationName`.
- Add alias facts for APM pairs when the same contract has both `BeginFoo` and
  `EndFoo`.
- Add metadata operation support when checked-in WSDL names `Foo`.

### Alias Fact Properties

Suggested properties:

| Property | Meaning |
| --- | --- |
| `originalOperationName` | Original method/operation name |
| `normalizedOperationName` | Derived operation alias |
| `normalizationKind` | `AsyncSuffix`, `ApmBeginEndPair`, or `MetadataOperation` |
| `clientContractName` | Client contract when available |
| `contractName` | Service contract when available |
| `metadataHash` | Supporting metadata hash when available |
| `supportingFactIds` | Semicolon-delimited sorted fact IDs where safe and stable enough |

Alias evidence should be `Tier2Structural` when backed by checked-in WCF metadata
or a credible APM pair. It should be `Tier3SyntaxOrTextual` when derived only from
generated-code naming.

## Mapping Strategy

Map in this order:

1. Exact current mapping path:
   `clientContractName + operationName + endpoint/contract`.
2. Metadata-backed normalized path:
   `clientContractName + normalizedOperationName + WSDL operation + contract`.
3. APM-pair normalized path:
   `clientContractName + normalizedOperationName + Begin/End operation pair`.
4. Generated-code-only normalized path:
   no stronger than `Tier3SyntaxOrTextual`, and only when contract identity is
   already aligned.

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

- If more than one operation remains after contract and normalized-name filtering,
  emit `AnalysisGap` with classification `AmbiguousWcfNormalizedMapping`.
- If metadata links multiple local WSDL files that each define the same operation
  under different safe contracts, emit an ambiguity gap.
- If only raw remote metadata exists and no checked-in metadata file can be parsed,
  emit a metadata gap rather than a mapping.

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
- lone `BeginFoo` does not normalize.
- lifecycle methods such as `CloseAsync` do not map without explicit metadata.
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

