# API and DTO Contract Diff Design

## Overview

API and DTO Contract Diff is a deterministic report/query layer over existing TraceMap indexes. It compares contract-shaped evidence between two snapshots and explains static changes with rule-backed evidence.

```text
before index + after index
  -> snapshot validation
  -> contract evidence projection
  -> stable identity matching
  -> classification with coverage caveats
  -> Markdown + JSON report
```

The command does not scan repositories, generate OpenAPI, diff raw source text, execute code, resolve runtime routes, or infer serializer behavior. It only compares facts already present in TraceMap indexes.

## Goals

- Compare endpoint and DTO contract evidence across commits or releases.
- Support both single-language and combined indexes.
- Use stable identities for endpoints, DTO types, DTO properties, methods, route shapes, and request/response attachments.
- Preserve source identity, commit SHA, rule IDs, evidence tiers, file spans, extractor versions, and supporting fact IDs.
- Label reduced coverage and analysis gaps instead of overclaiming.
- Produce byte-stable Markdown and JSON.
- Feed future Contract Delta Impact V2 and Release Review Report workflows.

## Non-Goals

- No OpenAPI generation or completeness claims.
- No binary compatibility analysis.
- No runtime traffic, deployment, proxy, auth, base-path, or reachability inference.
- No runtime serializer alias, reflection, dynamic dispatch, DI, branch feasibility, collection contents, mutation, or taint analysis.
- No source-code semantic diff beyond indexed facts.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Relationship to Existing Commands

`tracemap diff` compares broad combined dependency evidence. API/DTO contract diff is narrower and contract-focused:

- endpoint contract rows;
- route shape rows;
- DTO type rows;
- DTO property/member rows;
- request/response endpoint attachment rows;
- method signature rows where indexed.

Contract Delta Impact V2 consumes a changed contract delta and asks what depends on it. API/DTO Contract Diff creates a changed contract evidence report between two snapshots. Release Review Report can later include API/DTO Contract Diff as one section.

## Command Shape

Proposed command:

```bash
tracemap contract-diff \
  --before <index.sqlite> \
  --after <index.sqlite> \
  --out <path> \
  [options]
```

Options:

```text
--format <markdown|json>
--scope <all|endpoints|dto-types|dto-properties|methods|request-response|route-shapes>
--source <label>
--endpoint "<METHOD> <PATH_KEY>"
--type <name>
--property <name>
--change-kind <kind>
--max-diff-rows <n>
--max-evidence-rows <n>
--max-gaps <n>
--exit-code
```

Output behavior follows the existing combined diff/report conventions:

- file output defaults to Markdown;
- directory/no-extension output writes `contract-diff-report.md` and `contract-diff-report.json`;
- `--format json` with a file writes JSON to that file;
- deterministic payloads contain no generated timestamps;
- input SQLite files are opened read-only;
- invalid input/schema errors return nonzero;
- `--exit-code` returns `1` only for actionable diff rows.
- actionable diff rows for `--exit-code` are exactly `Added`, `Removed`, and `ChangedEvidence`; review-tier, coverage-relative, no-evidence, and gap-only output returns `0`.
- `--scope` accepts comma-separated values; default is all contract row scopes.
- `--change-kind` is a row-kind filter with the closed values `endpoint`, `dto-type`, `dto-property`, `method`, `request-response`, and `route-shape`.

## Index Modes

### Single-Language Mode

Single-language mode compares two normal TraceMap scan indexes from the same repository/language identity. It reads:

- scan manifest and coverage metadata;
- facts and properties;
- symbol/occurrence tables where present;
- endpoint, DTO, method, HTTP, serializer, and framework facts emitted by adapters.

Single-language output uses source label `self` or a stable internal label unless the scan/index has a better source display label.

Single-language mode needs its own reader rather than delegating to the combined-diff reader. Mode detection is:

- combined index: `index_sources` table is present;
- single-language scan index: `facts` plus scan manifest metadata are present and `index_sources` is absent.

The single-language reader projects rows from `facts`, fact properties, symbol tables, spans, rule IDs, evidence tiers, extractor versions, and scan manifest metadata. It must use the same safe rendering and stable identity helpers as the combined reader. Missing optional precision tables produce schema gaps and downgrades; they do not make the index invalid.

### Combined Mode

Combined mode compares two combined indexes. It pairs sources by source label and validates identity per source. It reads:

- `index_sources`;
- `combined_facts`;
- `combined_symbols`;
- `combined_fact_symbols`;
- `combined_symbol_relationships` where relevant;
- `combined_parameter_forward_edges` / argument-flow tables only as supporting context when future implementation needs method attachment confidence.

Combined mode must reuse existing combined readers and safe rendering helpers where practical. It should not introduce a second endpoint alignment implementation.

Mixed single-vs-combined comparison is deferred because source pairing and output semantics differ.

## Snapshot Validation

Validation happens before evidence comparison:

1. detect index mode and schema (`index_sources` for combined indexes; `facts` plus scan manifest metadata for single-language scan indexes);
2. read source metadata;
3. pair sources;
4. validate repo identity/language/source label compatibility;
5. read coverage and analysis gaps;
6. collect extractor versions;
7. emit source identity and coverage gaps.

Identity conflicts stop comparison for affected sources unless a future override is explicitly specified. Missing identity downgrades comparison but does not necessarily stop it.

## Evidence Projection

Each snapshot is projected into comparable rows:

```text
EndpointContractRow
DtoTypeContractRow
DtoPropertyContractRow
MethodContractRow
RequestResponseAttachmentRow
RouteShapeContractRow
```

Each row carries:

- row kind;
- stable identity key;
- display name;
- safe metadata;
- evidence tier;
- source fact rule ID;
- file span;
- commit SHA;
- extractor version;
- source label;
- supporting fact IDs;
- coverage caveats;
- limitations.

Projection must preserve raw fact provenance in JSON through IDs, not source snippets.

Language evidence is uneven. For example, serializer-indexed members may expose aliases or declared field metadata while syntax-only object-shape evidence may expose only names and shape hashes. The projection must compare only indexed metadata and downgrade rows when declared type, nullability, requiredness, or alias evidence is missing.

Per-language identity metadata should be interpreted conservatively:

| Language family | Preferred DTO identity metadata | Caveat |
| --- | --- | --- |
| .NET | assembly name/version when indexed, namespace, fully qualified symbol/type name, symbol ID | Missing assembly/version downgrades cross-index certainty but does not block name-scoped review rows. |
| JVM | package, class name, nested type name, classfile/source symbol where indexed | Syntax-only or build-reduced scans are review-tier. |
| TypeScript | module or safe file identity hash, exported type/interface/class name, symbol ID where indexed | Structural object literals without exported type identity are review-tier. |
| Python | module, class/schema name, framework/schema source kind where indexed | Dynamic attributes and runtime model aliases require explicit indexed evidence. |

## Endpoint Rows

Endpoint row metadata:

| Field | Meaning |
| --- | --- |
| `endpointKind` | server route, client endpoint, aligned endpoint, or adapter-specific endpoint kind |
| `httpMethod` | normalized method when known |
| `normalizedPathKey` | normalized path key when known |
| `routeTemplate` | safe route template when known |
| `routeParameters` | sorted safe parameter names |
| `handlerSymbol` | stable symbol identity when known |
| `containingType` | safe containing type name when known |
| `framework` | safe framework/source kind when known |

Strong endpoint identity requires method plus normalized path key. Path-only or display-only route evidence is review-tier.

Handler symbol is compared as safe metadata under a stable method/path identity. If the handler changes while method and normalized path key remain stable, the row is `ChangedEvidence` or review-tier equivalent. It must not churn into a removed route plus an added route unless the route identity itself changes.

## DTO Type and Property Rows

DTO type metadata:

- language;
- namespace/module/package;
- assembly/package/module identity;
- fully qualified type name;
- symbol ID;
- type kind;
- serializer/schema source kind where indexed.

DTO property metadata:

- containing type identity;
- property/member name;
- declared type;
- nullability/requiredness where indexed;
- JSON/schema alias where explicitly indexed;
- field/property kind;
- source fact rule ID.

Alias and serializer metadata are only compared when explicitly indexed. Naming conventions alone do not prove serializer contract names.

## Request/Response Attachment Rows

Attachment rows connect endpoint identity to DTO identity:

```text
endpoint stable key + attachment kind + status code/response kind + DTO stable key
```

Attachment kinds:

- request body;
- request query/route binding;
- response body;
- status-code response;
- framework-specific attachment where indexed.

If status-code response evidence is absent, the report must not infer status code behavior.

Attachment comparison is evidence-gated. Current TraceMap adapters may not emit endpoint-to-request or endpoint-to-response DTO attachment facts for every language or framework. The implementation must inventory available attachment evidence first. When no credible attachment evidence exists for a source, this section emits `AttachmentEvidenceUnavailable` or equivalent gaps rather than claiming no request/response change.

## Method Signature Rows

Method rows are useful when indexed method signatures represent API/contract entry points or DTO-related handlers.

Identity should include:

- source label;
- containing type;
- method name;
- arity;
- fully qualified parameter types where available;
- return type where available.

If only method name is available, classification is review-tier.

## Route Shape Rows

Route shape rows compare route parameters independent of handler body:

```text
identity: source + method + normalized path key
metadata: sorted parameter signature
```

Route shape changes should identify parameter additions/removals/renames when indexed. They do not prove auth, deployment, or runtime reachability.

## Stable Identity Strategy

Stable identities must not use database row IDs, source snippets, local absolute paths, or raw URLs.

Endpoint key preference:

1. source label + endpoint kind + HTTP method + normalized path key;
2. source label + endpoint kind + HTTP method + normalized path key plus route-shape discriminator only when duplicate endpoint identities would otherwise collide;
3. safe metadata hash with review-tier caveat.

Handler symbol is not part of the primary endpoint key when method and normalized path key exist. It is changed metadata.

DTO type key preference:

1. source label + language + fully qualified symbol identity;
2. source label + language + namespace/module + type name + assembly/package/module;
3. safe metadata hash with review-tier caveat.

DTO property key preference:

1. source label + containing type key + explicit JSON/schema alias when the adapter marks that alias as the external contract identity;
2. source label + containing type key + property name, with explicit alias compared as changed metadata when it is not the identity;
3. source label + containing type display name + property name with review-tier caveat;
4. property-only key only for selector filtering, not strong identity.

Request/response key preference:

1. endpoint key + attachment kind + status/response kind + DTO key;
2. endpoint key + attachment kind + DTO display hash with review-tier caveat.

Method signature key preference:

1. source label + containing type key + method name + arity + fully qualified parameter type list + return type;
2. source label + containing type display name + method name + arity with review-tier caveat;
3. method-name-only key only for selector/filtering, not strong identity.

Route shape key preference:

1. source label + HTTP method + normalized path key, with route parameter signature compared as metadata;
2. source label + endpoint kind + route display hash with review-tier caveat when normalized path key is unavailable.

Duplicate stable identities emit `DuplicateContractIdentity` and downgrade affected rows.

## Classification Mapping

| Classification | Meaning | Confidence |
| --- | --- | --- |
| `Added` | Evidence exists only after under credible comparable coverage. | high |
| `Removed` | Evidence exists only before under credible comparable coverage. | high |
| `ChangedEvidence` | Same stable identity exists on both sides with changed safe metadata. | high |
| `AddedWithBeforeGap` | Evidence appears after but before coverage was reduced or relevant gaps exist. | medium |
| `RemovedWithAfterGap` | Evidence disappears after but after coverage was reduced or relevant gaps exist. | medium |
| `NeedsReviewDiff` | Evidence is syntax-only, ambiguous, name-only, duplicated, or identity-unverified. | review |
| `NoDiffEvidence` | No comparable evidence changed under credible selected scope. | none |
| `SelectorNoMatch` | Selectors matched neither snapshot. | none |
| `TruncatedByLimit` | Caps prevented full rendering/comparison. | unknown |
| `UnknownAnalysisGap` | Gaps prevent a credible conclusion. | unknown |

Coverage and identity caveats can only downgrade, never upgrade, a classification.

Exit-code behavior is intentionally narrower than "has any row": only `Added`, `Removed`, and `ChangedEvidence` produce exit code `1` when `--exit-code` is present.

## Safety and Rendering

Reports must use existing safe path and Markdown escaping helpers where possible.

Never render:

- raw source snippets;
- raw SQL;
- raw config values;
- connection strings;
- raw URLs;
- local absolute paths;
- unsanitized route/display strings.

Render safe route templates and DTO/member names only after validation and escaping. Unsafe values should be omitted or hashed with a limitation.

## JSON Contract

Top-level JSON:

```json
{
  "reportType": "api-dto-contract-diff-combined",
  "version": "1.0",
  "reportCoverage": "FullEvidenceAvailable",
  "coverageWarnings": [],
  "query": {},
  "beforeSnapshot": {},
  "afterSnapshot": {},
  "summary": {},
  "sourcePairs": [],
  "endpointDiffs": [],
  "dtoTypeDiffs": [],
  "dtoPropertyDiffs": [],
  "methodDiffs": [],
  "requestResponseDiffs": [],
  "routeShapeDiffs": [],
  "gaps": [],
  "limitations": []
}
```

All collections sort deterministically. Metadata maps should serialize as sorted key/value arrays when order could vary.

Single-language and combined reports share the same top-level JSON shape for schema stability. `reportType` follows existing hyphenated TraceMap report conventions: `api-dto-contract-diff-single` for two scan indexes and `api-dto-contract-diff-combined` for two combined indexes. `version` uses the existing report version style, such as `1.0`. `reportCoverage` uses the existing closed coverage vocabulary, including `FullEvidenceAvailable`, `ReducedCoverage`, and `UnknownAnalysisGap`. Single-language mode uses a synthetic `sourcePairs` entry for `self` with scan metadata, language, commit SHA, coverage, and extractor versions. Combined mode uses one `sourcePairs` entry per paired source label. Both modes keep empty arrays for unavailable row groups. JSON must not include wall-clock timestamps, imported-at times, volatile row IDs, local absolute paths, or raw source identities.

## Rule Catalog Plan

Implementation should add or update rules:

- `api.dto.contract.diff.endpoint.v1`;
- `api.dto.contract.diff.dto.v1`;
- `api.dto.contract.diff.attachment.v1`;
- `api.dto.contract.diff.identity.v1`;
- `api.dto.contract.diff.coverage.v1`;
- `api.dto.contract.diff.schema.v1`;
- `api.dto.contract.diff.selector.v1`;
- `api.dto.contract.diff.truncation.v1`.

Rule limitations must explicitly state that static API/DTO evidence is not OpenAPI completeness, runtime traffic proof, serializer runtime mapping, binary compatibility, deployment reachability, or auth behavior proof.

The `api.dto.contract.diff.*` prefix is intentionally a new contract-diff family. If implementation chooses a shorter catalog naming convention, it must preserve the same rule purposes and limitations.

The first implementation PR that emits any `api.dto.contract.diff.*` rule is blocked until matching `rules/rule-catalog.yml` entries exist with documented limitations. This includes row rules and gap rules for identity, coverage, schema/optional-table gaps, selectors, truncation, and attachment-unavailable cases.

## Implementation Slices

Recommended PR sequence:

1. Evidence inventory, rule catalog entries, shared models, CLI, single-index endpoint/DTO projection, Markdown/JSON skeleton.
2. Stable identity and classification engine with single-index tests.
3. Selector parsing, caps, exit-code behavior, and deterministic output tests.
4. Combined-index source pairing and combined projections.
5. Route-shape comparison plus request/response attachment gap rendering; changed attachment rows only where indexed evidence exists.
6. Safety, byte-stability, release-review integration hooks, and broader fixture validation.

## Validation

Implementation PRs should run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Language adapter smoke checks should run only when extraction behavior changes. This spec is primarily a reporting/query layer and should not require adapter behavior changes unless implementation discovers missing contract facts.
