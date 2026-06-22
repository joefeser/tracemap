# UI Field Property Lineage Continuation Design

## Overview

This continuation spec extends the existing `tracemap property-flow` report from
root selection and first-hop property identity into downstream static context.
The implementation should remain a reporting/query-layer improvement over
existing combined indexes. It should not introduce a second traversal engine,
runtime observation, live browser capture, or new public claims.

The key product question is:

> Starting from a UI field, form control, Razor binding, DTO/model property, or
> fact ID, what static evidence shows how that property reaches endpoint,
> service, mapping, query, data, or dependency context?

The answer should be a conservative evidence packet, not a runtime proof.

## Existing State

Already implemented:

- `tracemap property-flow` over combined indexes.
- Angular UI facts:
  - `UiTemplateBinding`
  - `UiFormControlBinding`
  - `UiEventBinding`
  - `UiTemplateVariable`
  - `UiBindingGap`
- Razor facts:
  - `RazorBinding`
  - `RazorFormTarget`
  - `RazorModelBindingTarget`
  - `RazorBindingGap`
- Selector prefixes:
  - `field:`
  - `control:`
  - `binding:`
  - `model:`
  - `dto:`
  - `symbol:`
  - `fact:`
- Observed evidence metadata via opt-in input, with no classification upgrade.
- First property identity joins for Razor bindings, form targets, endpoint
  alignment, and model-binding facts.

Remaining useful work:

- Reuse route-flow rows when available to add endpoint-centered downstream
  context.
- Add property-specific service/data/dependency terminal context only when
  existing facts expose a trail.
- Harden consumer/export compatibility for richer property-flow rows.
- Make ambiguity and missing-schema states more explicit.

## Proposed Slices

### Slice 1: Route-Flow And Endpoint Downstream Context

Add report-layer composition from selected UI/property roots into route-flow
context when the combined index exposes route-flow tables or equivalent
documented route-flow rows.

Inputs:

- selected property-flow roots;
- existing UI/template/Razor/model-binding facts;
- endpoint-alignment facts;
- `combined_route_flow_edges` or a successor schema;
- route-flow source identity, gaps, and classification metadata.

Output:

- additive path nodes/edges or supporting context rows that cite route-flow
  evidence;
- `RouteFlowUnavailable`, `RouteFlowNoPropertyContext`,
  `RouteFlowReducedCoverage`, or equivalent gaps when route-flow cannot be
  used safely;
- no route traversal recomputation beyond existing shared helpers.

Classification:

- `StrongStaticLineage` only when root, endpoint, and route-flow evidence are
  rule-backed, full-coverage, and property-specific.
- `ProbableStaticLineage` when structural/static route and model evidence are
  strong but not fully semantic.
  This requires at least Tier2Structural evidence linking the selected property
  to the route or model-binding target; controller/action name matches,
  endpoint reachability, or source proximity alone do not qualify.
- `NeedsReviewLineage` for same-name, alias-only, convention-only, or
  cross-source review-tier bridges.
- `UnknownAnalysisGap` for reduced coverage or missing required schema.

### Slice 2: Service/Data/Dependency Terminal Context

Add terminal context rows only when existing combined evidence exposes a
property-specific trail. The implementation must not attach every service or
query reachable from the endpoint unless the selected property participates in
that trail.

Eligible evidence families:

- argument/value-origin facts;
- parameter-forwarding facts;
- object-shape/payload fields;
- assignment/mapping/projection facts;
- validation/read/write facts;
- query-pattern or SQL-shape facts;
- legacy data metadata and data surfaces;
- package/dependency surfaces;
- event/message surfaces;
- path/reverse rows as supporting context only.

Edge rules:

- Use existing source rule IDs for source facts.
- Use existing `property-flow.*.v1` derived rules for report-layer joins where
  possible.
- Add a new derived rule only if an existing rule cannot describe the row and
  the rule catalog is updated with limitations first.

### Slice 3: Consumer Compatibility

Property-flow rows already appear in generated artifacts such as docs-export,
vault export, evidence packets, and static explorer flows. This slice verifies
that downstream consumers either render new additive rows safely or emit a
documented gap.

Consumers to inspect when implementation changes row shape:

- `tracemap docs-export`
- `tracemap vault`
- `tracemap evidence-pack`
- `tracemap explorer generate`

The implementation should prefer backward-compatible additive fields. A report
version bump is required only when consumers cannot safely ignore new fields or
row meanings.

## Data Model Guidance

Prefer additive metadata on existing report records:

- `PropertyFlowNode`
- `PropertyFlowEdge`
- `PropertyFlowPath`
- `PropertyFlowGap`
- `PropertyFlowInventory`

Suggested safe metadata keys:

- `contextKind`
- `terminalKind`
- `sourceFamily`
- `bridgeKind`
- `schemaSignal`
- `propertyTrailKind`
- `routeFlowEvidenceId`
- `supportingPathId`
- `supportingReverseId`
- `surfaceKind`
- `surfaceSubtype`
- `coverageLabel`

Do not store:

- raw snippets;
- raw SQL;
- raw config values;
- connection strings;
- raw URLs with host/query data;
- raw remotes;
- local absolute paths;
- secrets or credentials;
- private sample names.

## Rule ID Plan

Reuse existing rule IDs where possible:

- `property-flow.root.v1`
- `property-flow.path.v1`
- `property-flow.selector.v1`
- `property-flow.schema.v1`
- `property-flow.coverage.v1`
- `property-flow.truncation.v1`
- `property-flow.observed-evidence.v1`
- `typescript.angular.*.v1`
- `csharp.razor.*.v1`
- route-flow, endpoint, path, reverse, query, data, dependency, event, and
  package rules already emitted by their source facts.

Add new rules only if implementation introduces a distinct derived conclusion.
Candidate derived rule names, if needed:

- `property-flow.route-context.v1`
- `property-flow.terminal-context.v1`
- `property-flow.consumer-compat.v1`

Each new rule must be added to `rules/rule-catalog.yml` before output uses it,
with limitations covering static evidence, reduced coverage, ambiguity,
runtime non-proof, and redaction.

Gap names such as `RouteFlowReducedCoverage` and
`RouteFlowNoPropertyContext` are planned gap classifications unless already
present in code at implementation time. If an implementation emits them as new
gap kinds, the implementation must map them to an existing catalogued rule or
add a new catalogued rule before output uses them.

The term "equivalent" in requirements means a catalogued, rule-backed fact
family that carries selected-property identity, symbol/fact identity, or
explicit safe alias metadata. Same-name-only matches, broad endpoint
reachability, or unscoped source proximity do not qualify as equivalent bridges.

## Ambiguity Handling

The report must not pick hidden winners.

Ambiguous cases include:

- multiple components expose the same property/control name;
- multiple Razor actions or handlers match a form target;
- multiple DTO/model types share a simple name;
- same property name appears in model and DTO families;
- route-flow edges exist for an endpoint but not for the selected property;
- alias evidence matches but no exact symbol/fact bridge exists;
- custom Angular components or directives hide the actual control binding;
- partial views or editor templates hide Razor property context;
- generated code or stale designer files create incomplete model evidence.

Represent these as:

- multiple capped candidates;
- `NeedsReviewLineage` paths;
- `UnknownAnalysisGap` when coverage prevents a credible conclusion;
- explicit gaps such as `PropertyIdentityUnavailable`,
  `EndpointAlignmentUnavailable`, `RouteFlowNoPropertyContext`,
  `MapperContextUnavailable`, or `TerminalContextUnavailable`.

## Browser And Computer-Use Boundary

Browser/computer-use evidence is allowed only as local hidden/manual validation
context in this spec. It can help a human compare rendered fields to checked-in
templates, but it must not be scanner proof, default report input, or public
claim evidence.

Existing observed evidence support remains metadata only and must not upgrade
static classifications.

Future browser-assisted workflows must have a separate spec before becoming a
tool feature.

## Validation Plan

Spec-only PR:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- Kiro Opus spec review
- Kiro Sonnet spec review

Implementation PRs:

- focused property-flow tests;
- focused C# Razor/model-binding tests when .NET extraction changes;
- `npm run check --prefix src/typescript` when TypeScript extraction changes;
- route-flow tests when route-flow schema consumption changes;
- docs/vault/export/explorer tests when consumer row shape changes;
- `dotnet build src/dotnet/TraceMap.sln`;
- `dotnet test src/dotnet/TraceMap.sln`;
- `./scripts/check-private-paths.sh`;
- `git diff --check`.

## Open Decisions For Implementation

1. Whether the first implementation slice should reuse only existing
   route-flow JSON/report rows or read route-flow tables directly.
2. Whether terminal service/data/dependency context should be rendered inside
   existing `Lineage Paths` or a new additive `Downstream Context` subsection.
3. Whether row additions remain `version: 1.0` compatible or require a `1.1`
   report version.
4. Which existing consumer, if any, must be patched in the same PR as new row
   kinds.
