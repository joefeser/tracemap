# UI Field and Property Lineage Design

## Overview

UI field/property lineage is a static composition layer over TraceMap evidence.
It starts from a UI or property root and follows rule-backed facts through
template/control binding, component or view-model properties, payload object
shapes, HTTP calls, endpoint alignment, server model binding, DTO/model
properties, validation/mapping/read/write evidence, service/repository calls,
and dependency/data surfaces.

The design preference is a new query/report command:

```bash
tracemap property-flow \
  --index <combined.sqlite> \
  --property <selector> \
  --max-roots 25 \
  --max-depth 10 \
  --max-paths 100 \
  --max-inventory 1000 \
  --out <path>
```

This keeps the user question distinct from `tracemap paths`, which starts from
endpoints, symbols, or sources, and from the route-centered report proposed in
issue #159, which starts from HTTP route/client call evidence. `property-flow`
may reuse those commands' graph readers, endpoint matcher, path search helpers,
safe rendering, coverage handling, and report models where compatible.

## Goals

- Start from UI field/control/template/model/property evidence.
- Preserve deterministic evidence rows with rule IDs, evidence tiers, file
  spans, source labels, commit SHAs, and extractor IDs/versions.
- Support Angular and Razor/cshtml roots in the first implementation plan.
- Connect property roots to TypeScript members, payload object shapes, HTTP
  calls, server DTO/model properties, validation branches, mapper/projection
  evidence, calls, query patterns, and data/dependency surfaces where existing
  facts support the chain.
- Reuse combined indexes, endpoint alignment, route-flow, combined paths,
  reverse paths, parameter/value-origin facts, object/projection shapes,
  symbol relationships, and vault export contracts.
- Emit safe Markdown and JSON reports with explicit coverage labels, gaps, and
  limitations.

## Non-Goals

- No runtime UI visibility proof.
- No proof that every user, role, tenant, feature flag, or app state sees the
  field.
- No branch feasibility or runtime validation outcome proof.
- No runtime dependency injection, reflection, serializer, dynamic dispatch, or
  browser/live HTTP certainty.
- No production login, credential capture, or private data capture.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No raw source snippets, raw SQL, raw remotes, local absolute paths, secrets,
  connection strings, captured credentials, or raw URLs by default.

## Current Evidence Families

Property-flow should compose evidence that already exists or is planned in
nearby specs:

- Angular/TypeScript facts: HTTP client calls, body field names, query names,
  class/function context, object shapes, argument/value-origin evidence, and
  reduced-coverage gaps.
- Razor/cshtml facts: planned binding/helper/tag-helper/form target evidence.
- Endpoint alignment: client/server HTTP method and normalized route matching.
- Route-centered static flow from issue #159: route/client call to server code
  path and downstream surfaces.
- Combined path graph: endpoint/symbol/source to terminal surfaces.
- Reverse path graph: surface to upstream endpoint/symbol/source roots.
- Parameter/value-origin facts: argument passed, aliases, field/member origin,
  parameter forwarding, callback/async boundaries, and gaps.
- Object/projection/query/data evidence: DTO/model/property facts, object
  shapes, AutoMapper/projection/manual mapping, query patterns, repository/data
  surfaces, and dependency surfaces.
- Evidence graph/vault export: stable safe nodes, edges, rule IDs, tiers,
  coverage labels, gaps, and limitations.

## Proposed Package Layout

The implementation can reuse existing .NET reporting projects and add focused
types instead of creating a separate scanner:

```text
src/dotnet/
  TraceMap.Reporting/
    PropertyFlow/
      PropertyFlowCommandOptions.cs
      PropertyFlowEngine.cs
      PropertyFlowGraphBuilder.cs
      PropertyFlowSelectorParser.cs
      PropertyFlowModels.cs
      PropertyFlowMarkdownWriter.cs
      PropertyFlowJsonWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    PropertyFlow*.cs
```

Scanner additions stay with the owning language adapters:

```text
src/typescript/src/extractors/
  AngularTemplateBindingExtractor.ts
  AngularFormBindingExtractor.ts

src/dotnet/TraceMap.Core/
  RazorBindingExtractor.cs
```

The exact names can follow the repository's implementation conventions, but the
ownership boundary should remain clear: language adapters emit facts; reporting
composes facts.

## Combined Index Schema Contract

The command accepts a combined index, not a single-language index. For v1, a
database is recognized as a combined index only when it contains:

| Object | Required? | Purpose |
| --- | --- | --- |
| `index_sources` | Required | Source labels, source index IDs, scan IDs, commit SHAs, manifest JSON, language, coverage, and build status. |
| `combined_facts` | Required | Source facts, including UI/property roots, endpoint facts, DTO/model facts, surfaces, gaps, and `combined_fact_id`. |
| `combined_dependency_edges` | Required view for path-like traversal | Existing summary dependency edges used by combined reporting and paths. Current `tracemap combine` creates this with its underlying edge tables. |
| `combined_symbols` | Optional precision table | Symbol nodes and source-local symbol identity. Missing table emits a schema gap and caps symbol-based precision. |
| `combined_fact_symbols` | Optional precision table | Fact-to-symbol attachment. Missing table emits a schema gap and may leave surfaces unlinked. |
| `combined_call_edges` | Optional precision/content table | Call traversal evidence. Current `tracemap combine` creates this table; zero rows or missing imported source data reduce call precision. If an older compatible schema somehow exposes `combined_dependency_edges` without this table, property-flow may continue from the view but must emit a schema gap. |
| `combined_object_creations` | Optional precision/content table | Object creation and construction context. Missing or empty evidence emits a schema/coverage gap where object context is needed. |
| `combined_symbol_relationships` | Optional precision/content table | Inheritance/interface/member relationship context. Missing or empty evidence emits a schema/coverage gap where relationship context is needed. |
| `combined_argument_flows` | Optional precision/content table | Argument-to-parameter evidence. Missing or empty evidence emits a schema/coverage gap where argument flow is needed. |
| `combined_local_aliases` | Optional precision/content table | Local alias context. Missing or empty evidence emits a schema/coverage gap where alias context is needed. |
| `combined_field_aliases` | Optional precision/content table | Field/member alias context. Missing or empty evidence emits a schema/coverage gap where member origin is needed. |
| `combined_parameter_forward_edges` | Optional precision/content table | Parameter forwarding evidence. Missing or empty evidence emits a schema/coverage gap where forwarding is needed. |
| `endpoint_matches` | Optional/report-only in current schema | Not required for v1. Property-flow should recompute combined endpoint matches using the existing combined endpoint matcher unless a future schema makes persisted rows authoritative. |
| `combined_route_flow_edges` or documented successor | Optional future table | Route-centered flow from issue #159. Absence emits `RouteFlowUnavailable` where downstream traversal depends on route-flow. |

Missing required objects fail with a sanitized schema error. Missing optional
objects produce `property-flow.schema.v1` gaps and reduced precision, not a
crash. Older combined indexes that have the required objects but lack optional
property-flow or route-flow objects are valid inputs with explicit schema gaps.

`fact:<combinedFactId>` selectors refer exactly to the
`combined_facts.combined_fact_id` value. Implementations must not invent a
second fact ID format for property-flow. Display IDs such as `fact:<id>` in
reports are presentation wrappers around the underlying `combined_fact_id`.

Endpoint alignment inside a combined index should reuse the same combined
endpoint matching behavior used by combined reporting and paths over
`combined_facts` and `index_sources`. Persisted endpoint-match ownership remains
a follow-up unless the combined schema changes.

The machine-checkable route-flow schema signal is undefined pending issue #159.
Until #159 supplies a concrete signal definition, all combined indexes are
treated as route-flow-unavailable; property-flow emits `RouteFlowUnavailable`
gaps where route-flow-specific traversal would be needed and must not invent a
fallback route-flow signal.

## Selector Model

`--property` accepts a closed set of selector prefixes:

| Selector | Meaning |
| --- | --- |
| `field:<name>` | UI field or safe visible field/control name. |
| `control:<name>` | Angular/Razor form control name such as `formControlName` or HTML `name`. |
| `binding:<name>` | Template binding expression/property path root. |
| `model:<type>.<property>` | Server/view model property selector; only model/view-model facts are candidates. |
| `dto:<type>.<property>` | DTO property selector; only DTO facts are candidates. |
| `symbol:<id-or-display>` | Source-local symbol ID or safe display selector. |
| `fact:<combinedFactId>` | Exact `combined_facts.combined_fact_id` root. |
| `--source <label>` | Optional source filter using deterministic case-insensitive exact source-label matching. |
| `--framework angular|razor|any` | Optional UI root framework filter; default is `any`. |

Selectors are safe display selectors, not source snippets. They should be
normalized by trimming whitespace, rejecting line breaks, rejecting local
absolute paths and raw URLs, lowercasing selector prefixes, and preserving the
selector value as a safe string only after safety checks. Unsafe selector input
is rejected with a sanitized diagnostic; the command does not run a hashed
best-effort query for unsafe selectors.

Generic names such as `id`, `name`, `type`, `value`, `state`, and `status`
should be allowed only as review-tier roots unless narrowed by source label,
type, symbol, or fact ID.

When a model and DTO selector could both describe the same underlying property,
the prefix controls the fact family. If one fact is legitimately both a model
and DTO property, the root remains valid but the overlap is reported as
ambiguous supporting metadata.

## Source Facts

### Angular Template Binding Facts

Candidate fact types:

- `UiTemplateBinding`
- `UiFormControlBinding`
- `UiEventBinding`
- `UiTemplateVariable`
- `UiBindingGap`

Candidate rule IDs:

- `typescript.angular.template-binding.v1`
- `typescript.angular.form-binding.v1`
- `typescript.angular.event-binding.v1`
- `typescript.angular.template-variable.v1`
- `typescript.angular.binding-gap.v1`

Required safe properties where available:

```json
{
  "uiFramework": "angular",
  "bindingKind": "interpolation|property|event|two-way|form-control|template-variable",
  "controlName": "email",
  "propertyPath": "user.email",
  "componentClass": "ProfileComponent",
  "componentSymbolId": "symbol-...",
  "memberName": "email",
  "memberSymbolId": "symbol-...",
  "eventName": "ngSubmit",
  "handlerName": "save",
  "formGroupName": "profileForm",
  "formControlName": "email",
  "expressionKind": "property-path",
  "expressionHash": "hash-...",
  "templateOrigin": "inline|templateUrl"
}
```

Tiers:

- Tier2Structural for direct Angular binding/form structures connected to a
  component by static metadata.
- Tier3SyntaxOrTextual for syntax-only binding evidence without component or
  member resolution.
- Tier4Unknown for template parse failures, dynamic binding gaps, unsupported
  custom directive binding, or external template resolution gaps.

Angular limitations:

- Template evidence does not prove runtime rendering, feature flags, auth, role
  visibility, component lifecycle, change detection outcome, browser state, or
  submitted values.
- Same-name control and property matching is review-tier unless direct
  assignment/member evidence supports it.

### Razor Binding Facts

Candidate fact types:

- `RazorBinding`
- `RazorFormTarget`
- `RazorModelBindingTarget`
- `RazorBindingGap`

Candidate rule IDs:

- `csharp.razor.binding.v1`
- `csharp.razor.form-target.v1`
- `csharp.razor.model-binding.v1`
- `csharp.razor.binding-gap.v1`

Required safe properties where available:

```json
{
  "uiFramework": "razor",
  "bindingKind": "asp-for|html-for|tag-helper|form-action|page-handler|model-binding",
  "controlKind": "input|select|textarea|label|validation|form",
  "modelType": "ProfileViewModel",
  "propertyPath": "Email",
  "propertyName": "Email",
  "handlerName": "OnPostSave",
  "actionName": "Save",
  "controllerName": "Profile",
  "pagePathHash": "hash-...",
  "httpMethod": "POST"
}
```

Tiers:

- Tier2Structural for direct Razor/tag-helper/helper shapes with static model
  expression evidence.
- Tier3SyntaxOrTextual for syntax fallback without model symbol binding.
- Tier4Unknown for dynamic model, ViewBag/ViewData, unsupported tag helper,
  partial/template ambiguity, or generated Razor gaps.

Razor limitations:

- Razor binding evidence does not prove runtime rendering, route selection,
  handler execution, model-binding success, auth, role visibility, validation
  outcome, or browser submission.

## Graph Model

Property-flow uses nodes and edges similar to `tracemap paths`.

Node kinds:

- `UiField`
- `UiControl`
- `TemplateBinding`
- `EventBinding`
- `ComponentMember`
- `ViewModelProperty`
- `PayloadField`
- `HttpClientCall`
- `EndpointRoute`
- `ActionOrHandler`
- `DtoProperty`
- `ModelProperty`
- `ValidationRead`
- `Mapping`
- `ServiceCall`
- `RepositoryCall`
- `QueryPattern`
- `DataSurface`
- `DependencySurface`
- `Gap`

Edge kinds:

- `template-binds-property`
- `control-binds-property`
- `event-calls-handler`
- `member-assigned-to-payload`
- `payload-sent-by-http`
- `endpoint-match`
- `route-flow`
- `model-bound-to-parameter`
- `property-read`
- `property-written`
- `validated-by`
- `mapped-to`
- `argument-passed`
- `parameter-forward`
- `calls`
- `queries`
- `surface-evidence`
- `analysis-gap`

Every node and edge must include:

- stable ID;
- source label and source index ID;
- scan ID and commit SHA;
- rule ID and evidence tier;
- file path and line span where available;
- supporting fact IDs and supporting edge IDs where applicable;
- extractor ID/version where available;
- safe display metadata.

Derived report edges may not have a language extractor. In that case
`extractorId` and `extractorVersion` are nullable, or the implementation may
use a report-engine identifier/version such as `property-flow-report` if the
repository has a stable version source. Source fact extractor metadata must be
preserved when present.

## Traversal Semantics

Traversal should be deterministic bounded breadth-first search unless an
implementation documents an equivalent deterministic algorithm.

Suggested defaults:

- `--max-roots 25`
- `--max-depth 10`
- `--max-paths 100`
- `--max-frontier 10000`
- `--max-inventory 1000`
- `--max-gaps 1000`

Traversal starts from selected UI/property roots and walks downstream edges.
Cross-source traversal is allowed only through explicit evidence:

- combined endpoint matching recomputed from combined facts or a documented
  persisted endpoint-match successor;
- route-flow from issue #159 or its implemented equivalent, detected through a
  documented schema signal;
- combined path edges with source transition evidence;
- future documented combined rules.

Razor/cshtml facts usually live in the .NET source index with server code, so a
Razor form-to-handler hop may be within one source. If a future scan layout
places Razor facts in a separate source index, the hop must use the same
documented cross-source mechanisms as any other source transition. When scan
layout differs or source ownership is uncertain, property-flow emits an
`UnknownAnalysisGap` rather than assuming a same-source hop.

Path conclusions are coverage-relative:

- reduced contributing coverage yields `UnknownAnalysisGap`;
- full credible coverage may yield `NoLineageEvidence`;
- selector misses yield `SelectorNoMatch`;
- caps yield `TruncatedByLimit`.

Until route-flow from issue #159 has a concrete schema, the expected v1 behavior
for server-internal traversal is an explicit `RouteFlowUnavailable` gap at the
point where property-flow would need route-centered edges. UI-to-component,
component-to-payload, payload-to-HTTP, combined endpoint matching, and direct
model-binding evidence can still be reported without route-flow.

Route-flow is not the only way to traverse server-internal evidence. Before
issue #159 lands, property-flow should still attempt endpoint-to-surface
traversal through existing combined path graph evidence such as call edges,
argument flows, parameter forwarding, symbol relationships, query patterns, and
dependency surfaces. `RouteFlowUnavailable` is reserved for hops that genuinely
require route-centered flow semantics or route-flow-specific derived edges.

## Classification Model

| Classification | Meaning |
| --- | --- |
| `StrongStaticLineage` | Every hop is strong semantic or equivalently precise static evidence under credible coverage. |
| `ProbableStaticLineage` | The trail uses structural UI/form/endpoint/object-shape evidence without lower-tier ambiguity. |
| `NeedsReviewLineage` | The trail depends on syntax-only, same-name, generic property, ambiguous endpoint, optional route, fallback, or review-tier mapping evidence. |
| `UnknownAnalysisGap` | Gaps or reduced coverage prevent a credible conclusion. |
| `NoLineageEvidence` | Full credible coverage found selected roots but no downstream path. |
| `SelectorNoMatch` | Selector matched no roots. |
| `TruncatedByLimit` | The report is partial because traversal or output caps were hit. |

Confidence is a fixed derived field, not model judgment:

- `StrongStaticLineage`: `High`
- `ProbableStaticLineage`: `Medium`
- every other classification: `Low`

Classification and gap rows must cite derived rule IDs in addition to source
fact rule IDs:

| Output | Derived rule ID |
| --- | --- |
| Selected root row | `property-flow.root.v1` |
| Derived lineage edge | `property-flow.edge.v1` |
| `StrongStaticLineage` | `property-flow.path.v1` |
| `ProbableStaticLineage` | `property-flow.path.v1` |
| `NeedsReviewLineage` | `property-flow.path.v1` |
| `UnknownAnalysisGap` from reduced coverage or no credible absence conclusion | `property-flow.coverage.v1` |
| `UnknownAnalysisGap` from missing optional schema | `property-flow.schema.v1` |
| `RouteFlowUnavailable` | `property-flow.schema.v1` until #159 defines a more specific rule |
| `NoLineageEvidence` | `property-flow.coverage.v1` |
| `SelectorNoMatch` | `property-flow.selector.v1` |
| `TruncatedByLimit` | `property-flow.truncation.v1` |
| Optional observed DOM metadata or validation discrepancy | `property-flow.observed-evidence.v1` |

Path classification is per path. Report coverage is separate and may be
`Partial` or `Reduced` even when one returned path is classified
`StrongStaticLineage`; truncation and reduced coverage must remain visible at
the report and gap levels.

Gap and boundary codes should be closed and documented before implementation
emits them:

| Gap code | Rule ID | Meaning |
| --- | --- | --- |
| `SelectorNoMatch` | `property-flow.selector.v1` | The selector matched no roots. |
| `AmbiguousSelector` | `property-flow.selector.v1` | The selector matched multiple roots and needs narrowing. |
| `UnsafeSelectorRejected` | `property-flow.selector.v1` | The selector contained unsafe local path, URL, snippet, or secret-like input. |
| `MissingRequiredSchema` | `property-flow.schema.v1` | The database is not a property-flow-compatible combined index. |
| `MissingOptionalSchema` | `property-flow.schema.v1` | Optional precision evidence is absent and a lower-precision result is emitted. |
| `RouteFlowUnavailable` | `property-flow.schema.v1` | Route-flow-specific traversal is unavailable pending issue #159 schema. |
| `EndpointMatchUnavailable` | `property-flow.schema.v1` | Endpoint matching evidence cannot be computed from available combined facts. |
| `DynamicTemplateExpression` | source binding gap rule or `property-flow.edge.v1` | Template expression cannot be resolved statically. |
| `DynamicUrlOrPayload` | source HTTP/value-flow gap rule or `property-flow.edge.v1` | HTTP URL or payload shape is too dynamic for a stronger hop. |
| `RuntimeBoundary` | `property-flow.coverage.v1` | Runtime DI, reflection, serializer behavior, auth, feature flag, or browser state blocks a stronger claim. |
| `ReducedCoverage` | `property-flow.coverage.v1` | Source coverage prevents an absence-of-lineage conclusion. |
| `TruncatedByLimit` | `property-flow.truncation.v1` | Depth, root, path, frontier, inventory, or gap caps truncated output. |
| `ObservedEvidenceOnly` | `property-flow.observed-evidence.v1` | Browser/computer-use evidence is demo metadata only. |

## Markdown Output

Sections:

1. Summary
2. Query
3. Sources and Coverage
4. Selected Roots
5. Lineage Paths
6. Gaps
7. Evidence Inventory
8. Optional Observed Evidence
9. Limitations

Markdown should keep evidence near claims. Each path row should include node
kind, edge kind, safe display name, source label, rule ID, evidence tier, file
span, and limitation/gap code where relevant.

Unsafe values must not render. This includes raw SQL, snippets, local absolute
paths, raw remotes, raw URLs, connection strings, secrets, credentials, and
private data.

## JSON Output

Top-level shape:

```json
{
  "reportType": "property-flow",
  "version": "1.0",
  "reportCoverage": "Full|Partial|Reduced|Unknown",
  "coverageWarnings": [],
  "query": {},
  "snapshot": {},
  "summary": {},
  "sources": [
    {
      "sourceIndexId": "source-...",
      "sourceLabel": "client",
      "repositoryIdentityHash": "hash-or-null",
      "scanId": "scan-...",
      "commitSha": "abc123",
      "analysisLevel": "Level2Structural",
      "buildStatus": "Reduced"
    }
  ],
  "selectedRoots": [],
  "lineagePaths": [],
  "gaps": [],
  "inventory": {},
  "observedEvidence": [],
  "limitations": []
}
```

Selected root shape:

```json
{
  "rootId": "root-...",
  "rootKind": "TemplateBinding",
  "classification": "ProbableStaticLineage",
  "sourceIndexId": "source-...",
  "sourceLabel": "client",
  "repositoryIdentityHash": "hash-or-null",
  "scanId": "scan-...",
  "commitSha": "abc123",
  "combinedFactId": "fact-...",
  "symbolId": "symbol-...",
  "ruleId": "typescript.angular.template-binding.v1",
  "evidenceTier": "Tier2Structural",
  "filePath": "src/app/profile/profile.component.html",
  "startLine": 12,
  "endLine": 12,
  "extractorId": "typescript",
  "extractorVersion": "1.0.0",
  "safeDisplay": {
    "propertyPath": "user.email",
    "controlName": "email"
  },
  "supportingFactIds": [],
  "limitations": []
}
```

Path, node, and edge shapes should align with `tracemap paths` where possible:
required IDs, nullable missing fields, sorted arrays, sorted metadata keys, and
no generated timestamps by default.

The `snapshot` object summarizes the input without exposing local identity:

```json
{
  "inputKind": "combined-index",
  "combinedIndexHash": "hash-or-null",
  "repositoryIdentityHash": "hash-or-null",
  "sourceCount": 2,
  "sources": [
    {
      "sourceIndexId": "source-...",
      "sourceLabel": "client",
      "repositoryIdentityHash": "hash-or-null",
      "scanId": "scan-...",
      "commitSha": "abc123",
      "analysisLevel": "Level2Structural",
      "buildStatus": "Reduced"
    }
  ],
  "schema": {
    "requiredObjectsPresent": true,
    "missingOptionalObjects": [],
    "routeFlowSignal": "unavailable"
  }
}
```

The hash is over safe input identity only. It must not encode local absolute
paths, raw remotes, private repo names, or timestamps.

## Optional Browser/Computer-Use Layer

Browser or computer-use evidence is an optional follow-up validation layer. It
can help demos by observing that a DOM field appeared in one run or by comparing
observed DOM attributes with static template facts.

It must not be required for core property-flow claims and must not replace
static facts. Browser-observed DOM evidence:

- is runtime observation metadata;
- may be public only when captured from public-safe fixtures without
  credentials or private data;
- does not prove all runtime states, auth states, feature flags, permissions,
  browsers, deployments, or user journeys;
- must be shown in the optional observed evidence section;
- cannot upgrade `NeedsReviewLineage` to `StrongStaticLineage` by itself.

## Safety Model

Property-flow should reuse the repository's safe rendering and private-path
guard behavior:

- render repository-relative paths only;
- omit or hash raw remotes and local roots;
- omit raw SQL, snippets, raw URLs, form values, secrets, tokens, credentials,
  connection strings, and private data;
- escape Markdown table/link characters;
- sort output deterministically;
- fail validation with sanitized diagnostics if unsafe strings appear.

## Rule Catalog Plan

Implementation should add rule catalog entries for any new source fact rules
and derived report rules before emitting those rows.

Likely new source rules:

- `typescript.angular.template-binding.v1`
- `typescript.angular.form-binding.v1`
- `typescript.angular.event-binding.v1`
- `typescript.angular.template-variable.v1`
- `typescript.angular.binding-gap.v1`
- `csharp.razor.binding.v1`
- `csharp.razor.form-target.v1`
- `csharp.razor.model-binding.v1`
- `csharp.razor.binding-gap.v1`

Likely derived report rules:

- `property-flow.root.v1`
- `property-flow.edge.v1`
- `property-flow.path.v1`
- `property-flow.selector.v1`
- `property-flow.coverage.v1`
- `property-flow.schema.v1`
- `property-flow.truncation.v1`
- `property-flow.observed-evidence.v1`

If an implementation reuses existing facts without new scanner output, it may
only need derived report rules. It must still preserve all source fact rule IDs
in report evidence rows.

This spec-only PR intentionally does not add rule catalog entries. The rule
catalog requirement applies to the first implementation slice that emits source
facts or derived property-flow rows.

## Validation Strategy

Spec-only validation:

- `git diff --check`
- `./scripts/check-private-paths.sh` if available
- Kiro review through `scripts/kiro-review.mjs` if local Kiro review tooling is available

Implementation validation:

- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- TypeScript adapter tests and build when Angular extraction changes
- Razor/.NET adapter tests when Razor extraction changes
- combined report/path/reverse tests when graph composition changes
- `./scripts/check-private-paths.sh`
- `git diff --check`
- relevant pinned smoke checks from `docs/VALIDATION.md`

Fixtures must be public-safe and synthetic. They should not include private
sample names, private paths, private remotes, production URLs, raw credentials,
or customer/application data.
