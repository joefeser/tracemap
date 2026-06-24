# UI Field Property Lineage Composition Design

## Overview

This spec completes the issue #165 property-lineage story at the design level:
selected UI roots should compose through backend property evidence and existing
route-flow/service/data context only when the combined index contains
property-specific static evidence. The work remains a deterministic
scanner/report/export improvement, not runtime analysis and not AI impact
analysis.

The expected answer shape is:

```text
selected UI field/control/binding/model property
  -> component/view-model/form model identity
  -> payload/form/query/route parameter or model-binding target
  -> endpoint or handler bridge
  -> route-flow/path/value-origin/mapping/service/query/data/dependency context
  -> explicit gaps, coverage labels, and limitations
```

Every hop is optional and evidence-gated. A missing hop is useful output when it
is labeled as a gap.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `ui-field-property-lineage` | Provides the original broad property-flow command, root facts, selectors, output contract, and safety rules. |
| `ui-field-property-lineage-next-slice` | Implements model-binding/property identity joins and establishes same-name/fan-out downgrade rules. |
| `ui-field-property-lineage-continuation` | Starts downstream route-flow context and no-property-context gaps. This spec completes the composition requirements and consumer/export safety. |
| `route-flow-service-data-composition-next` | Provides route-flow context groups and service/data presentation over selected route-flow rows. Property-flow may reuse those rows only as property-specific supporting context. |
| `route-flow-service-data-composition-final` or successor, if present at implementation time | Parallel or newer route-flow completion work may supersede parts of `route-flow-service-data-composition-next`. Implementation must re-audit the live `dev` route-flow contract and record the target predecessor before product edits. |
| `route-centered-endpoint-trace-completeness` | Provides touched-file/touched-symbol and route-flow completeness metadata. Property-flow may reference that metadata but must not turn broad endpoint context into property lineage. |

This spec should not reopen completed root extraction unless a gap is required
to compose safely. If live code already satisfies a task, implementation should
mark it complete in this spec's state and choose the next smallest unchecked
composition item.

## Contract Sequencing

The implementation PR must start by recording the exact route-flow/property-flow
contract it targets. At minimum it should state:

- whether `ui-field-property-lineage-continuation` or an equivalent property
  identity/route-flow gap slice has merged;
- whether `route-flow-service-data-composition-next`,
  `route-flow-service-data-composition-final`, or another successor owns the
  route-flow context rows being reused;
- which route-flow schema/report signal is consumed, such as
  `combined_route_flow_edges`, route-flow report JSON fields, or a named
  successor table/view/field;
- whether the property-flow report remains version `1.0` compatible.

If the route-flow contract is moving or unavailable, implementation must emit
schema/context gaps rather than guessing.

## Non-Goals

- No browser-required scanner behavior, computer-use workflow, live HTTP,
  production login, credentials, telemetry, DOM observation, or runtime app
  execution.
- No runtime proof of UI visibility, route reachability, authorization,
  feature flags, branch feasibility, serializer behavior, dependency injection,
  database execution, production traffic, release safety, outage cause, or
  business impact.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  core scanner, reducer, report, or export logic.
- No raw source snippets, raw SQL, raw config values, hostnames, raw remotes,
  local absolute paths, connection strings, secrets, form values, anti-forgery
  tokens, or unreviewed private sample labels in default artifacts.
- No new public command unless implementation review proves the existing
  `tracemap property-flow` surface cannot express the composition.

## Proposed Implementation Slices

### Slice 1: Composition Contract Audit And Gaps

Inventory current `property-flow` and `route-flow` report rows and document
which property-specific bridges already exist. Add or harden gaps when route
flow, endpoint alignment, mapper, terminal context, or consumer schema support
is absent.

Expected product behavior:

- `RouteFlowUnavailable` when no usable route-flow schema/report context exists.
- `RouteFlowNoPropertyContext` when route-flow rows exist but are broad endpoint
  context only.
- `PropertyIdentityUnavailable`, `EndpointAlignmentUnavailable`,
  `MapperEvidenceUnavailable`, `TerminalContextUnavailable`,
  `MissingOptionalSchema`, and `TruncatedByLimit` style gaps where appropriate,
  using existing catalogued rules or newly documented rules.

### Slice 2: Angular And Razor Property-Specific Bridges

Strengthen report-layer joins from existing UI roots to payload/form/model
targets:

- Angular event handler -> component method -> assigned payload field -> HTTP
  call body/query/route parameter.
- Angular form control/member -> object-shape/value-origin evidence -> HTTP
  call.
- Razor binding/form target -> action/page handler -> model-binding target.
- Payload/form/query/route field -> DTO/model property through endpoint
  alignment and model-binding facts.

Same-name-only, alias-only, and convention-only bridges remain
`NeedsReviewLineage`. Dynamic UI/model patterns become gaps.

### Slice 3: Backend Terminal Context

Attach backend context only when existing facts expose a selected-property
trail:

- validation/read/write/guard evidence;
- manual assignment, object initializer, projection, constructor mapping, or
  mapper configuration evidence;
- service/repository calls through selected call/value-origin edges;
- query pattern, SQL shape, data surface, dependency surface, package/config,
  event/message, storage, WCF, ASMX, remoting, and legacy-data terminal context;
- existing route-flow context groups, logic rows, dependency surfaces, touched
  files, touched symbols, path, reverse, argument projection, and fact-symbol
  projection rows as supporting context.

Broad endpoint reachability alone is never enough.

### Slice 4: Export And Consumer Compatibility

Verify generated consumers whenever report row shapes or metadata change:

- `tracemap vault`
- `tracemap docs-export` / RAG import chunks
- `tracemap evidence-pack`
- `tracemap explorer generate`
- any evidence graph export path that consumes property-flow JSON

Consumers should safely render additive public-safe metadata or emit a
compatibility gap. A report version bump is required when a consumer cannot
safely ignore new rows or when an existing row's meaning changes.

## Evidence And Data Model

Prefer additive metadata on existing property-flow models:

- selected roots;
- nodes;
- edges;
- paths;
- gaps;
- inventory rows;
- coverage warnings;
- limitations;
- optional context rows or supporting context references.

Suggested closed metadata keys:

- `uiFramework`
- `uiRootKind`
- `bindingKind`
- `controlKind`
- `modelFamily`
- `propertyTrailKind`
- `bridgeKind`
- `contextKind`
- `terminalKind`
- `routeFlowContextKind`
- `schemaSignal`
- `redactionState`
- `coverageLabel`

Suggested edge kinds:

- `ui-binding-to-component-member`
- `event-calls-handler`
- `control-value-assigned`
- `member-assigned-to-payload`
- `payload-field-sent-by-http`
- `form-field-bound-to-model`
- `form-target-to-handler`
- `endpoint-to-model-binding`
- `model-property-mapped`
- `property-read-by-validation`
- `property-forwarded-to-service`
- `property-used-by-query`
- `property-reaches-terminal-surface`
- `route-flow-context-supported`

These keys and edge kinds are optional additive metadata on existing
property-flow row arrays for report version `1.0` only if existing consumers can
ignore them safely. If a row meaning changes or a consumer cannot safely ignore
the metadata, implementation must bump the report version or emit a documented
compatibility gap.

Stable IDs should derive only from safe deterministic inputs. Do not include raw
selector values, raw snippets, raw SQL, raw URLs, hostnames, local absolute
paths, or private names in IDs or metadata.

## Rule ID Plan

Reuse existing property-flow rules where possible:

- `property-flow.root.v1`
- `property-flow.edge.v1`
- `property-flow.path.v1`
- `property-flow.selector.v1`
- `property-flow.coverage.v1`
- `property-flow.schema.v1`
- `property-flow.truncation.v1`
- `property-flow.observed-evidence.v1` for non-upgrading observed metadata
  annotation only; it SHALL NOT justify, attach, or upgrade a hop in this
  spec's scope

Preserve source rules where they justify a hop:

- `typescript.angular.template-binding.v1`
- `typescript.angular.form-binding.v1`
- `typescript.angular.event-binding.v1`
- `typescript.angular.template-variable.v1`
- `typescript.angular.binding-gap.v1`
- `csharp.razor.binding.v1`
- `csharp.razor.form-target.v1`
- `csharp.razor.model-binding.v1`
- `csharp.razor.binding-gap.v1`
- `combined.route-flow.*.v1`
- endpoint, value-origin, mapping, query, data, dependency, path, reverse, and
  export rules already present in the catalog.

Candidate new derived rules, only if existing rules are insufficient:

- `property-flow.terminal-context.v1`
- `property-flow.consumer-compat.v1`
- `property-flow.redaction.v1`

These candidate IDs SHALL NOT be emitted until `rules/rule-catalog.yml`
includes the rule and its limitations in the same implementation PR.
Implementation should default to existing property-flow, route-flow, export, and
redaction rules. Existing `property-flow.edge.v1` already documents route-flow
no-property-context behavior; implementation should prefer it before minting a
new route-context rule.

## Classification Guidance

`StrongStaticLineage` requires a complete static chain with semantic or
equivalent strong property-specific evidence, full relevant coverage, known
commit SHA, compatible schema, known extractor identity, and no blocking gaps.

`ProbableStaticLineage` requires at least Tier2 structural property-specific
evidence for each non-semantic hop. It cannot be based only on controller/action
name proximity, same endpoint reachability, same file/class, source proximity,
or same-name matching.

`NeedsReviewLineage` covers same-name-only, alias-only, convention-only,
syntax-only, high-fan-out, ambiguous, candidate-based, generated-code
uncertain, partial-property, or fallback evidence under otherwise usable
coverage.

`UnknownAnalysisGap` covers missing schema, reduced coverage, unknown commit
SHA, missing extractor identity, incompatible route-flow context, dynamic
runtime-only boundaries, truncation that blocks a conclusion, or unsafe data
that prevents a credible conclusion.

`NoLineageEvidence` is allowed only when selector roots are credible, relevant
schema is compatible, relevant coverage is full, and no blocking gaps exist.

## Gap Codes

The implementation should keep gap codes closed and stable. Initial codes for
this spec are:

- `SelectorNoMatch`
- `AmbiguousSelector`
- `PropertyIdentityUnavailable`
- `SameNameOnlyPropertyMatch`
- `GenericPropertyFanOut`
- `EndpointAlignmentUnavailable`
- `RouteFlowUnavailable`
- `RouteFlowNoPropertyContext`
- `UnsupportedRouteFlowSchema`
- `MapperEvidenceUnavailable`
- `TerminalContextUnavailable`
- `MissingOptionalSchema`
- `ReducedCoverage`
- `UnknownCommitSha`
- `ExtractorIdentityUnavailable`
- `UnsafeInputRejected`
- `RedactedUnsafeValue`
- `TruncatedByLimit`

Weak-but-present property-specific evidence produces a `NeedsReviewLineage`
row, optionally with a gap. Missing, dynamic-only, incompatible, unsafe, or
unavailable evidence produces a gap and no hop across that boundary.

## Ambiguity And Boundary Rules

- Multiple candidate roots, properties, endpoints, handlers, aliases, mapper
  targets, route-flow contexts, or terminal surfaces must remain visible within
  caps.
- Generic names use the current property-flow v1 closed set:
  `id`, `name`, `type`, `value`, `state`, and `status`. Fan-out at the
  documented v1 threshold of 10 or more candidate property roots remains
  review-tier unless a later spec changes and tests the threshold.
- Same-name-only evidence is not equivalent to property identity.
- Alias evidence can support a hop only when the alias is itself a catalogued
  static fact and the report preserves the alias rule ID and limitations.
- Cross-source joins require endpoint alignment, route-flow, combined path,
  exact fact/symbol identity, or another documented bridge.
- Terminal context must be downstream of the selected property trail; broad
  route reachability is context at most and becomes a gap if not
  property-specific.

## Safe Output And Export Design

Default output may include:

- repo-relative file paths;
- line spans;
- safe type/property/control names;
- safe route keys when already normalized by existing route-flow rules;
- safe hashes for expressions, snippets, SQL shapes, table names, column sets,
  URLs, config values, and other sensitive values;
- rule IDs, evidence tiers, coverage labels, supporting IDs, extractor
  versions, commit SHAs, limitations, and gap codes.

Default output must not include raw snippets, raw SQL, raw config values, raw
HTML, raw URL hosts or query strings, connection strings, credentials, tokens,
private sample labels, local absolute paths, raw remotes, form values, submitted
values, or anti-forgery tokens.

Vault/RAG/docs-export consumers should treat property-flow rows as evidence
chunks, not conclusions beyond their classifications. Generated public/demo
artifacts must omit or hash unsafe values and must preserve limitations such as
"static evidence only" and "coverage-relative."

## Validation Plan

Public-safe fixtures should cover:

- Angular interpolation, property binding, event binding, two-way binding,
  reactive forms, template-driven forms, payload construction, and HTTP calls.
- Razor `asp-for`, `Html.*For`, static form target metadata, MVC actions,
  Razor Page handlers, model-binding targets, and gaps for dynamic Razor.
- Endpoint alignment from client/form evidence to backend endpoint evidence.
- Route-flow available, missing, empty, incompatible, reduced, and
  no-property-context states.
- Validation, mapping, service/repository, query, data-surface, and
  dependency-surface context only when property-specific evidence exists.
- Negative cases for generic names, high fan-out, same-name-only, alias-only,
  ambiguous type family, missing mapper evidence, missing terminal context,
  cross-source boundary without bridge, reduced coverage, unknown commit SHA,
  and truncation.
- Safe Markdown/JSON/export rendering and byte-stable output.

Recommended commands for implementation PRs:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlow
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlow
npm run check --prefix src/typescript
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run `docs/VALIDATION.md` pinned smoke checks for changed adapters/reporters or
record an explicit deferral with rationale in implementation state.
