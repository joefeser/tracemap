# UI Field Property Lineage Continuation Requirements

## Introduction

TraceMap already has a deterministic `tracemap property-flow` command with
Angular template/form roots, Razor binding roots, Razor model-binding target
facts, model/property identity joins, and optional observed evidence metadata.
The next useful slice is deeper downstream static composition: when a UI field
or bound property is selected, TraceMap should show the rule-backed trail from
the UI evidence into existing route-flow, endpoint, value-origin, service,
query, data-surface, dependency-surface, and export evidence where the current
combined index can prove those hops.

This is static evidence only. It must not claim runtime DOM visibility,
user interaction, authentication state, feature-flag behavior, production
traffic, branch feasibility, serializer behavior, DI resolution, or database
execution.

## Source Material

- GitHub issue #165: Trace UI field bindings to model and backend properties.
- Existing spec: `.kiro/specs/ui-field-property-lineage`.
- Existing spec: `.kiro/specs/ui-field-property-lineage-next-slice`.
- Related flow spec: `.kiro/specs/route-centered-static-flow-report`.
- Related endpoint spec: `.kiro/specs/cross-app-endpoint-alignment`.
- Validation guidance: `docs/VALIDATION.md` property-flow section.

## Requirement 1: Preserve Existing Property-Flow Contract

**User Story:** As a reviewer, I want the continuation to extend the existing
property-flow report without breaking current selectors, output files, or
safety guarantees.

### Acceptance Criteria

1. WHEN this continuation is implemented THEN existing `tracemap property-flow`
   selectors SHALL remain compatible: `field:`, `control:`, `binding:`,
   `model:`, `dto:`, `symbol:`, and `fact:`.
2. WHEN new rows or metadata are added THEN the implementation SHALL keep the
   JSON top-level shape backward compatible or explicitly bump and document the
   report version.
3. WHEN Markdown is generated THEN the existing section order SHALL be
   preserved unless a documented versioned report change is introduced.
4. WHEN optional schema needed for downstream composition is missing THEN the
   report SHALL emit rule-backed gaps rather than fail or omit the limitation.
5. WHEN output is generated THEN it SHALL remain deterministic for identical
   inputs, including roots, paths, nodes, edges, gaps, inventory, limitations,
   and metadata ordering.

## Requirement 2: Compose UI Roots Through Existing Route And Endpoint Evidence

**User Story:** As a reviewer, I want a UI field to show which endpoint route it
appears to reach when static TypeScript/Razor, HTTP, and endpoint alignment
evidence supports the connection.

### Acceptance Criteria

1. WHEN an Angular binding or form-control root has rule-backed payload and
   HTTP-call evidence THEN the report SHALL connect the root to the HTTP call
   as `NeedsReviewLineage` unless exact symbol/value-origin evidence supports a
   stronger classification.
2. WHEN endpoint alignment evidence connects the HTTP call to a server endpoint
   THEN the report SHALL include the endpoint node and supporting endpoint
   evidence IDs.
3. WHEN a Razor form target statically identifies action/page/handler and HTTP
   method metadata THEN the report SHALL connect the form target to matching
   action/handler/model-binding evidence by joining already-extracted
   `RazorFormTarget` facts and existing endpoint/model-binding facts, not by
   adding a new Razor root extraction pass.
4. WHEN route-flow evidence is available through `combined_route_flow_edges` or
   a documented successor schema THEN the property-flow report SHALL reuse that
   evidence as downstream context rather than reimplementing route traversal.
   The implementation SHALL NOT recompute route traversal independent of the
   shared route-flow helper or documented route-flow table contract.
5. WHEN route-flow evidence is reachable from the same endpoint but is not tied
   to the selected property by rule-backed endpoint, model-binding,
   value-origin, payload, fact-symbol, or equivalent static evidence THEN the
   report SHALL emit `RouteFlowNoPropertyContext` or an equivalent gap rather
   than attaching route-flow rows as property lineage.
6. WHEN route-flow evidence is unavailable, empty, reduced, or incompatible
   THEN the report SHALL emit `RouteFlowUnavailable`,
   `RouteFlowNoPropertyContext`, or equivalent rule-backed gaps.

## Requirement 3: Compose Through Service, Mapping, Query, Data, And Dependency Evidence

**User Story:** As a reviewer, I want the report to show where a property
touches business/data logic when static facts already expose a property-specific
trail.

### Acceptance Criteria

1. WHEN existing combined facts expose validation, mapping, projection, manual
   assignment, object-shape, or value-origin evidence tied to the selected
   property THEN the report SHALL include those nodes and edges with rule IDs,
   tiers, source labels, spans, supporting fact IDs, and limitations.
2. WHEN existing route-flow/path/reverse evidence exposes service or repository
   call context tied to the selected property trail THEN the report SHALL
   include it as supporting static context.
   Supporting path/reverse context is still static evidence only; it does not
   prove runtime connectivity, request execution, authorization, or production
   traffic.
   The connection SHALL be established through rule-backed value-origin,
   parameter-forwarding, assignment, mapping, payload, model-binding,
   fact-symbol, or equivalent property-specific static evidence. Broad
   endpoint reachability alone does not qualify. Equivalent property-specific
   evidence is limited to catalogued rule-backed facts that carry the selected
   property identity, symbol/fact identity, or explicit safe alias metadata.
3. WHEN query-pattern, SQL-shape, data-surface, package, dependency, event, or
   message surface evidence is terminal context for the property-specific trail
   THEN the report SHALL include the surface with safe metadata only.
4. WHEN evidence is only same-name, alias-only, convention-only, or
   cross-source without a documented bridge THEN the report SHALL classify the
   hop as `NeedsReviewLineage` or emit an ambiguity gap.
   Equivalent bridges are limited to catalogued rule-backed facts that carry the
   selected property identity, symbol/fact identity, or explicit safe alias
   metadata. Same-name-only matches are not equivalent bridges.
   If same-name or alias-only evidence co-occurs with reduced coverage or
   missing schema, the reduced-coverage or missing-schema downgrade wins and
   the result SHALL be no stronger than `UnknownAnalysisGap`.
5. WHEN evidence does not expose a property-specific trail THEN the report
   SHALL not attach broad service/data/dependency rows merely because they are
   reachable from the same endpoint.

## Requirement 4: Bound Ambiguity And Coverage Downgrades

**User Story:** As a reviewer, I want unclear lineage to be visible without
TraceMap choosing hidden winners.

### Acceptance Criteria

1. WHEN multiple candidate DTOs, models, properties, endpoints, handlers,
   components, aliases, or surfaces match a selector THEN the report SHALL keep
   all candidates within configured caps or emit a truncation gap.
2. WHEN a generic property name such as `id`, `name`, `type`, or `status`
   produces high fan-out THEN the report SHALL downgrade to
   `NeedsReviewLineage` or `UnknownAnalysisGap` as appropriate.
   Full-coverage high fan-out SHALL use `NeedsReviewLineage`; reduced coverage
   or missing schema that prevents a credible candidate set SHALL use
   `UnknownAnalysisGap`.
   The v1 high fan-out threshold remains 10 or more candidate property roots, as
   documented in `.kiro/specs/ui-field-property-lineage-next-slice/design.md`;
   any threshold change SHALL be documented and tested.
3. WHEN scan coverage is reduced for a source used by the selected trail THEN
   no absence-of-lineage conclusion SHALL be stronger than
   `UnknownAnalysisGap`.
4. WHEN optional combined schema tables are absent THEN the report SHALL
   distinguish missing schema from no evidence.
5. WHEN route, endpoint, model-binding, mapper, query, data, or dependency
   evidence is incomplete THEN the report SHALL emit gaps that cite rule IDs and
   documented limitations.

## Requirement 5: Keep Browser/Computer-Use Evidence Hidden And Non-Upgrading

**User Story:** As a demo author, I may want local observed UI context, but I do
not want it to become scanner proof or public output by accident.

### Acceptance Criteria

1. WHEN optional browser/computer-use evidence is mentioned in this spec THEN it
   SHALL be framed as local hidden/manual validation context only.
2. WHEN observed evidence is accepted by existing property-flow options THEN it
   SHALL not upgrade root, edge, path, summary, absence, or impact
   classifications.
3. WHEN observed evidence contains unsafe keys, secrets, raw URLs, local
   absolute paths, private sample labels, credentials, raw snippets, raw SQL, or
   config values THEN the implementation SHALL reject or hash it using existing
   safety rules.
4. WHEN public/demo output is generated THEN observed browser/computer-use
   context SHALL be excluded unless an explicit reviewed public-safe workflow is
   added in a future spec.

## Requirement 6: Public-Safe Reports And Export Consumers

**User Story:** As a user preparing evidence docs, vault notes, or explorer
pages, I want property-flow continuation data to be safe for local hidden use
and bounded public summaries.

### Acceptance Criteria

1. WHEN property-flow output is consumed by vault export, docs-export, static
   explorer, or evidence-pack tooling THEN consumers SHALL either render safe
   additive metadata or emit rule-backed unsupported/gap rows.
2. WHEN new row kinds or metadata are added THEN consumer compatibility SHALL be
   tested for deterministic output, safety redaction, and graceful fallback.
3. WHEN a new field or row kind cannot be safely ignored by an existing
   consumer, including when an unknown field could be silently forwarded into
   public output or when additive metadata on an existing row type could pass
   through into generated HTML/Markdown/JSON, THEN the same implementation PR
   SHALL either patch that consumer or introduce a documented report version
   bump and compatibility gap behavior.
4. WHEN a generated artifact is public/demo-safe THEN it SHALL contain rule IDs,
   evidence tiers, coverage labels, commit SHA, extractor versions, source
   labels, supporting IDs, and limitations where the consumer supports them.
   When a consumer cannot support one of those fields, it SHALL omit that field
   safely or emit a documented compatibility gap rather than inventing or
   leaking data.
5. WHEN data is unsafe or hidden-only THEN public/demo outputs SHALL omit,
   redact, hash, or reject it rather than leaking it.

## Requirement 7: Validation Coverage

**User Story:** As a maintainer, I want enough focused tests to prevent future
route/property/data composition regressions.

### Acceptance Criteria

1. Tests SHALL cover Angular control/event/payload/HTTP/endpoint composition.
2. Tests SHALL cover Razor form/binding/action-or-handler/model-binding
   composition.
3. Tests SHALL cover route-flow available, route-flow empty, route-flow missing
   schema, and route-flow reduced coverage.
4. Tests SHALL cover service/repository/query/data/dependency terminal context
   only when property-specific evidence exists.
5. Tests SHALL cover ambiguity, fan-out, generic property names,
   same-name-only matches, alias-only matches, cross-source boundaries, and
   missing optional schema.
6. Tests SHALL cover deterministic Markdown/JSON and safe generated artifact
   behavior.
7. Validation SHALL run focused `PropertyFlowTests`, relevant .NET/TypeScript
   adapter tests if extraction changes, `dotnet build`, `dotnet test`,
   `./scripts/check-private-paths.sh`, and `git diff --check`.
8. Validation for consumer-compatibility slices SHALL run focused docs-export,
   vault, evidence-pack, or static explorer tests for every consumer touched or
   affected by new property-flow row shapes, plus
   `./scripts/check-private-paths.sh`.

## Non-Goals

- Runtime DOM proof.
- User interaction proof.
- Authentication, role, permission, feature-flag, interceptor, proxy, or
  deployment proof.
- Runtime DI or reflection solving.
- Serializer runtime configuration expansion.
- Branch feasibility, symbolic execution, or full taint analysis.
- Live browser-required scanner behavior.
- Production login, credentials, private data capture, or live HTTP capture.
- Raw source snippets by default.
- Raw SQL, connection strings, raw config values, local absolute paths, private
  sample labels, raw remotes, or secrets in output.
- LLM calls, embeddings, vector databases, or prompt-based classification.
