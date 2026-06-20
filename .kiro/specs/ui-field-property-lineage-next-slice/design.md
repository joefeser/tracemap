# UI Field and Property Lineage Next Slice Design

## Overview

This continuation slice narrows the next property-flow implementation step to
static property identity and downstream composition. The baseline already finds
UI roots and emits property-flow reports. The missing value is a safer bridge
from those roots to DTO/model properties and then to existing static route,
path, reverse, data, dependency, vault, docs-export, and static explorer
evidence.

The implementation should remain report-layer and extractor-layer work:

- language adapters emit deterministic facts;
- `tracemap combine` carries those facts into a combined index;
- `tracemap property-flow` composes facts read-only;
- downstream artifact commands consume generated report JSON or combined index
  evidence without deriving runtime conclusions.

No core scanner, reducer, report, or export path should call an LLM, create
embeddings, write a vector database, perform prompt-based classification, run a
browser, issue live HTTP requests, or require credentials.

## Baseline To Reuse

Reuse the completed baseline rather than replacing it:

- selector parser and safety checks;
- `field:`, `control:`, `binding:`, `model:`, `dto:`, `symbol:`, and `fact:`
  root forms;
- source and framework filters;
- Angular `UiTemplateBinding`, `UiFormControlBinding`, `UiEventBinding`,
  `UiTemplateVariable`, and `UiBindingGap` facts;
- Razor `RazorBinding`, `RazorFormTarget`, and `RazorBindingGap` facts;
- generic property downgrade;
- existing stable property-flow report model, stable IDs, classifications,
  confidence mapping, gaps, inventory, limitations, Markdown, and JSON;
- existing combined endpoint alignment, route-flow, paths, reverse,
  dependency-surface, data-surface, vault export, docs-export, and static HTML
  explorer contracts.

The implementation may add nullable fields or additional rows to the current
report. It should not break existing `property-flow-report.json` consumers
without a version bump.

## Proposed Slice Boundary

First product-code PR after this spec:

1. Add model-binding target extraction for MVC/Razor Pages and/or strengthen
   existing property facts so `model:` and `dto:` selectors have precise type
   family identity.
2. Add property identity matching in property-flow:
   UI binding/control root -> component/view-model member -> payload field or
   form model property -> HTTP call or form action -> endpoint/action/handler
   -> DTO/model property.
3. Add first downstream composition into existing route-flow/path/reverse/data
   evidence only where existing graph evidence already provides the trail.
4. Add focused public-safe fixtures and tests for direct static hops,
   same-name review-tier hops, generic fan-out downgrade, ambiguity, gaps, and
   output stability.

Deferred product-code slices:

- deeper mapper/projection coverage beyond already indexed deterministic
  patterns;
- richer validation/read/write and service/repository property hops;
- advanced Angular custom directive and pipe semantics;
- optional browser/computer-use demo observation;
- persisted derived property-flow rows.

## Fact Model Additions

### Razor Model-Binding Target

The first missing extractor family is `RazorModelBindingTarget`, or an
equivalent rule-backed model-binding fact if implementation chooses a different
local name.

Rule IDs:

- `csharp.razor.model-binding.v1`

The existing rule catalog reserves `csharp.razor.model-binding.v1` for MVC
action parameters, Razor Page handler parameters, `[BindProperty]`,
`[FromBody]`, `[FromForm]`, page models, and view models. The next
implementation PR should use that rule and distinguish sub-families with
`bindingKind`, `parameterSource`, and safe type metadata unless it first adds
and reviews separate catalog entries. Dynamic or unsupported binding patterns
continue to use the existing `csharp.razor.binding-gap.v1` gap rule.

Safe properties:

```json
{
  "uiFramework": "razor",
  "bindingKind": "action-parameter|handler-parameter|bind-property|page-model|view-model",
  "modelKind": "model|view-model|dto|unknown",
  "modelType": "ProfileInput",
  "propertyName": "Email",
  "propertyPath": "Email",
  "parameterName": "input",
  "parameterSource": "body|form|route|query|unknown",
  "actionName": "Save",
  "controllerName": "Profile",
  "handlerName": "OnPostSave",
  "httpMethod": "POST"
}
```

`actionName` and `controllerName` are MVC-specific. Razor Pages should use
`handlerName` and a safe page identity such as a page path hash; fields that do
not apply to the binding kind should be `null` or omitted according to the
existing fact serialization convention.

Limitations:

- MVC/Razor static evidence does not prove runtime route selection, handler
  execution, model-binding success, authorization, validation outcome, or
  submitted values.
- Convention-only model-binding is review-tier unless semantic facts prove the
  target.
- Dynamic model usage, partials, editor templates, custom tag helpers,
  `ViewBag`, `ViewData`, reflection, and generated code produce gaps or
  downgraded evidence.

### TypeScript Value-To-Payload Hops

The baseline Angular facts identify template controls and HTTP calls. The next
slice should compose them only through existing value-origin or new
deterministic facts.

Candidate evidence:

- direct assignment from component member/control value to object literal field;
- local alias or field alias;
- argument-to-parameter flow;
- parameter forwarding;
- object shape field attached to HTTP call body;
- query/route parameter shape attached to HTTP call;
- handler method called by an event binding.

Candidate derived edge kinds:

- `event-calls-handler`
- `control-value-assigned`
- `member-assigned-to-payload`
- `payload-field-sent-by-http`
- `query-field-sent-by-http`
- `route-parameter-sent-by-http`

These are report-layer derived edges and use `property-flow.edge.v1` as the
derived rule ID while preserving the source fact rule IDs that justify the hop,
such as existing event binding, value-origin, alias, argument-flow,
parameter-forwarding, object-shape, HTTP call, body-field, query-field, or
route-parameter facts. If the TypeScript adapter must emit a new scanner fact
to support one of these hops, that implementation PR must add the scanner rule
and limitations to the rule catalog before emitting it.

Same-name-only joins remain `NeedsReviewLineage`.

### Property Mapping Hops

Mapping should be deterministic and limited to facts with concrete evidence.

Supported first-slice candidates:

- manual assignment from source property to target property;
- object initializer or projection member assignment;
- constructor argument matched to parameter/property with static identity;
- mapper configuration facts if already deterministic and rule-backed.

Property-flow mapping hops use `property-flow.edge.v1` as the derived report
rule and must preserve the backing source fact rule IDs for assignment,
object/projection shape, constructor argument, argument-flow, or mapper
configuration evidence. If a candidate mapping pattern lacks an existing
rule-backed source fact, the implementation must add a cataloged scanner rule
or emit `MapperEvidenceUnavailable`; it must not report a mapping edge from
uncataloged inference.

Deferred:

- runtime serializer configuration expansion;
- reflection-based mapping;
- complex convention mapping without source/target property evidence;
- collection element identity beyond static property names.

## Selector And Matching Rules

Selector matching is closed and deterministic.

| Selector | Root families | Notes |
| --- | --- | --- |
| `field:<name>` | UI field/control/binding facts | Generic names downgrade unless narrowed; same-name-only hops downgrade per Requirement 3 AC 6. |
| `control:<name>` | Angular/Razor form control facts | Matches safe control metadata only; same-name-only hops downgrade per Requirement 3 AC 6. |
| `binding:<name>` | Template/Razor binding facts | Static property path or binding name only; same-name-only hops downgrade per Requirement 3 AC 6. |
| `model:<type>.<property>` | model, view-model, model-binding facts | DTO-only facts are excluded; facts classified as both families remain candidates with ambiguity metadata. |
| `dto:<type>.<property>` | DTO/serializer/body contract facts | Model-only facts are excluded; facts classified as both families remain candidates with ambiguity metadata. |
| `symbol:<id-or-display>` | symbol-attached facts | Prefer exact symbol ID; display matching is bounded. |
| `fact:<combinedFactId>` | exact combined fact | Strongest root disambiguator. |

Matching order:

1. Reject unsafe selector input with sanitized category diagnostics.
2. Apply source and framework filters.
3. Match closed selector family.
4. Sort candidates by source label, file path, line span, fact type, rule ID,
   and combined fact ID.
5. Apply `--max-roots`.
6. Emit ambiguity metadata and gaps when total candidates exceed selected roots
   or when type/family identity overlaps.

Facts classified as both model and DTO families count once for candidate and
cap purposes. Ambiguity metadata records all matched families, while sorting
uses the fact's primary family, then the shared sort keys above, so truncation
remains deterministic.

Generic fan-out rules:

- Closed generic list starts with `id`, `name`, `type`, `value`, `state`, and
  `status`.
- The v1 high fan-out threshold is a deterministic count threshold, not a
  confidence score: 10 or more candidate property roots for the normalized
  property name in the filtered source/framework set emits `GenericPropertyFanOut`
  and caps the result at `NeedsReviewLineage`.
- The threshold may become configurable only behind a documented option whose
  default remains 10 and whose value is recorded in JSON query metadata and
  tests.
- Generic or high fan-out selectors cannot produce `StrongStaticLineage`
  without exact fact/symbol/type identity.
- When high fan-out and same-name-only rules both apply, emit both gap codes
  where the gap cap allows and apply the stricter classification cap.

## Graph Composition

The graph builder should prefer source facts and existing combined graph nodes
over invented display-name edges.

Recommended node additions:

- `FormAction`
- `ModelBindingTarget`
- `DtoProperty`
- `ModelProperty`
- `PayloadField`
- `ValidationRead`
- `Mapping`
- `DataSurface`
- `DependencySurface`
- `Gap`

Recommended edge additions:

- `form-targets-endpoint`
- `action-binds-model`
- `handler-binds-model`
- `payload-field-binds-property`
- `form-field-binds-property`
- `property-mapped-to-property`
- `property-read-by-validation`
- `property-used-by-query`
- `property-reaches-data-surface`
- `property-reaches-dependency-surface`
- `analysis-gap`

Cross-source transitions are allowed only through explicit evidence:

- combined endpoint alignment computed from `combined_facts` and
  `index_sources`, or a future documented authoritative `endpoint_matches`
  schema;
- route-flow schema through `combined_route_flow_edges`, or a documented
  successor, and route-flow edges;
- combined path/reverse edges with source transition evidence;
- future documented combined rules.

If a source transition cannot be justified by one of those evidence families,
property-flow emits a gap.

When `combined_facts` and `index_sources` are present but endpoint alignment
produces no match for the queried client call, form target, action, or handler,
property-flow should emit `EndpointAlignmentUnavailable` rather than a schema
gap. Missing required combined tables still fail as schema errors; missing
optional precision tables continue to use `MissingOptionalSchema`.

## Route-Flow, Path, Reverse, Data, And Dependency Evidence

Route-flow:

- Use route-flow only when the combined index or report input exposes
  `combined_route_flow_edges`, or a documented successor schema signal.
- Preserve route-flow rule IDs and evidence tiers as supporting evidence.
- Do not emit route-flow-unavailable gaps for local hops that can be explained
  without route-flow.

Paths and reverse:

- Existing path/reverse reports are supporting context.
- Property-flow should cite path/reverse evidence IDs, rule IDs, tiers, source
  labels, file spans, and limitations where available.
- Reverse evidence does not prove runtime reachability; it shows static
  upstream/downstream context under coverage constraints.

Data and dependency:

- Query, data/entity, package/config, and dependency surfaces are terminal
  static evidence.
- Raw SQL, raw config, raw URLs, hostnames, remotes, and source snippets remain
  omitted or hashed.
- A property reaching a terminal surface is not reducer impact by itself.
  Reducer impact remains a separate command with its own evidence rules.

## Report Contract

Markdown keeps the existing section order:

1. Summary
2. Query
3. Sources and Coverage
4. Coverage Warnings
5. Selected Roots
6. Lineage Paths
7. Gaps
8. Evidence Inventory
9. Optional Observed Evidence
10. Limitations

JSON keeps the existing top-level shape:

```json
{
  "reportType": "property-flow",
  "version": "1.0",
  "reportCoverage": "Full|Partial|Reduced|Unknown",
  "coverageWarnings": [],
  "query": {},
  "snapshot": {},
  "summary": {},
  "sources": [],
  "selectedRoots": [],
  "lineagePaths": [],
  "gaps": [],
  "inventory": {},
  "observedEvidence": [],
  "limitations": []
}
```

Next-slice additions are backward-compatible only when they add new rows,
closed-vocabulary values, nullable fields, or safe metadata inside existing
arrays or objects such as `selectedRoots`, `lineagePaths`, `nodes`, `edges`,
`gaps`, `inventory`, `observedEvidence`, or `limitations`. Those additions may
remain report version `1.0` because existing consumers are expected to treat
unknown row kinds and metadata keys as additive static evidence.

A new required top-level field, a required field inside existing rows, a
changed meaning for an existing field, removed field, changed section order, or
changed classification/confidence semantics requires a version bump. The next
minor version should be `1.1` unless the change is incompatible enough to
justify a future major version. Downstream consumers must either handle the new
version explicitly or emit their existing safe schema-incompatible or
unsupported-family gaps.

New closed-vocabulary kind values inside existing arrays are additive and do
not require a version bump. Changing the meaning of an existing kind value does
require a version bump.

Consumers must not treat `observedEvidence` as permanently empty in v1. A
future demo-only command may populate it without a version bump, as long as the
rows use additive closed-vocabulary kind values and preserve the static-claim
boundary.

New gap codes should be closed and documented before use. Candidate codes:

| Gap code | Meaning |
| --- | --- |
| `PropertyIdentityUnavailable` | No credible DTO/model property identity exists for a hop. |
| `ModelBindingUnavailable` | Endpoint/action/handler exists but action/handler binding target evidence is missing before property identity can be tested. |
| `EndpointAlignmentUnavailable` | Endpoint alignment schema is available, but no client/server or form/action match was produced for the queried evidence. |
| `PayloadFieldAmbiguous` | Multiple payload fields or aliases could match a property. |
| `SameNameOnlyPropertyMatch` | Same-name match exists without stronger value-origin evidence. |
| `GenericPropertyFanOut` | Generic/common property name matched too many candidates. |
| `MapperEvidenceUnavailable` | Mapping would require unsupported mapper/runtime behavior. |
| `MissingOptionalSchema` | Reuse the existing property-flow gap when an optional combined precision table is absent. |
| `RouteFlowUnavailable` | Reuse the existing property-flow gap when `combined_route_flow_edges` or its documented successor is absent and the hop genuinely requires route-flow semantics. |
| `ObservedEvidenceOnly` | Optional browser/demo observation is not static proof. |

## Export And Explorer Integration

Vault export, docs-export/RAG-targeted documentation, and static HTML explorer
should treat property-flow reports as generated static artifacts.

Integration rules:

- preserve property-flow rule IDs, evidence tiers, stable IDs, supporting IDs,
  coverage labels, gaps, limitations, source labels, commit SHAs, and safe file
  spans;
- do not infer runtime behavior, production traffic, live reachability,
  vulnerability, ownership, or reducer impact;
- emit the consumer's existing schema-incompatible, unsupported-schema, or
  unavailable-family gap rules when a new property-flow report version cannot
  be consumed; this slice does not require new downstream gap codes unless
  existing consumer rules cannot express the condition;
- keep public/demo-safe validation strict for snippets, raw SQL, raw URLs,
  local absolute paths, remotes, hostnames, secrets, config values, and private
  labels.

## Optional Browser Boundary

Browser/computer-use evidence remains deferred. A future opt-in demo command
may collect observed DOM metadata from public-safe fixtures, but that metadata:

- is not required by `tracemap property-flow`;
- is not a scanner fact unless mapped back to checked-in static source through
  a separate rule-backed mapping;
- cannot upgrade a static lineage classification by itself;
- must reject credentials, production login, private data, live secret-bearing
  HTTP, hostnames, and private routes;
- must be rendered only in Optional Observed Evidence or a separate demo
  artifact.

## Determinism And Safety

All new rows must preserve deterministic ordering by stable keys. Stable IDs
should be context-separated hashes over safe identifiers, not raw local paths,
raw remotes, raw URLs, snippets, SQL, config values, or private labels.

Generated Markdown must escape table and link syntax. JSON maps must use sorted
keys. Missing optional data should use `null`, empty arrays, or explicit gaps
according to the existing report contract.

Validation must include the private-path guard and whitespace check. Public
fixtures and expected outputs must be synthetic and safe.

## Validation Plan

Spec-only PR:

- Kiro spec review with Opus if available.
- Kiro spec review with Sonnet if available.
- At most two re-review cycles, preferring Sonnet for final re-review.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Existing spec lint/check if present; otherwise record that none exists.

First implementation PR:

- Focused property-flow tests for new matching/composition behavior.
- Focused Razor/MVC/Pages extractor tests for model-binding target facts.
- Focused TypeScript tests if event-handler/value-origin-to-payload extraction
  changes.
- Markdown/JSON byte-stability tests.
- Safe-output tests for reports and generated artifact consumers touched by
  the implementation.
- `dotnet build` and `dotnet test` for the relevant solution/project.
- Adapter smoke checks required by `docs/VALIDATION.md` for changed adapters.
- `./scripts/check-private-paths.sh`.
- `git diff --check`.
