# UI Field Property Lineage Composition Requirements

## Introduction

TraceMap already has deterministic `tracemap property-flow` support for UI
field/property roots, Angular template/form evidence, Razor binding/form-target
evidence, Razor model-binding target facts, first-hop property identity joins,
safe Markdown/JSON output, and limited route-flow context gaps.

This spec defines the completion slice for issue #165: statically trace UI
field bindings and form-ish controls from Angular templates/components and
`.cshtml`/Razor views to backend model, DTO, and property evidence, then
compose that property-specific trail with route-flow, service, query, data, and
dependency evidence only when existing rule-backed facts support the hop.

The output is an evidence packet, not runtime proof. It must not claim browser
visibility, user interaction, live HTTP execution, authorization behavior,
feature-flag state, production traffic, dependency-injection runtime target
selection, serializer runtime behavior, database execution, branch feasibility,
business impact, or release safety.

## Source Material

- GitHub issue #165: Trace UI field bindings to model and backend properties.
- `.kiro/specs/ui-field-property-lineage/`
- `.kiro/specs/ui-field-property-lineage-next-slice/`
- `.kiro/specs/ui-field-property-lineage-continuation/`
- `.kiro/specs/route-flow-service-data-composition-next/`
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `docs/VALIDATION.md` and public-safe property-flow/reporting validation
  guidance.

## Existing Baseline

This spec assumes the current baseline already includes:

- `tracemap property-flow` over a combined index.
- Selectors for `field:`, `control:`, `binding:`, `model:`, `dto:`,
  `symbol:`, and `fact:`.
- Angular facts for template bindings, form bindings, event bindings, template
  variables, and binding gaps.
- Razor facts for `asp-for`, `Html.*For`, form targets, model-binding targets,
  and binding gaps.
- First-hop property identity joins for selected Angular/Razor/model-binding
  evidence where static facts support them.
- Safe Markdown/JSON reports with stable IDs, rule IDs, evidence tiers, line
  spans, commit SHAs, extractor versions, coverage labels, gaps, limitations,
  and deterministic ordering.
- Route-flow report context groups, touched files, touched symbols, logic rows,
  dependency surfaces, argument projection, fact-symbol projection, and
  route-flow gaps.

Implementation must reuse these contracts unless a narrow versioned adjustment
is explicitly required.

## Requirement 1: Conservative UI Field And Property Roots

**User Story:** As a reviewer, I want TraceMap to start from Angular and Razor
UI evidence only when static source facts identify a field, control, binding,
or model property.

### Acceptance Criteria

1. WHEN `field:`, `control:`, or `binding:` selectors are used THEN candidates
   SHALL come only from documented UI root families such as Angular template
   bindings, Angular form bindings, Angular event bindings, Angular template
   variables, Razor bindings, Razor form targets, and Razor binding gaps.
2. WHEN Angular roots use interpolation, property binding, event binding,
   two-way binding, reactive forms, template-driven forms, `formControlName`,
   `formGroup`, `formArrayName`, `ngModel`, or static template variables THEN
   property-flow SHALL preserve the backing rule ID, tier, file path, line span,
   source label, commit SHA, extractor ID/version, and safe metadata.
3. WHEN Razor roots use `asp-for`, `Html.*For`, input/select/textarea-like
   controls, labels, validation helpers, form target attributes, page handlers,
   action/controller metadata, or static model expressions THEN property-flow
   SHALL preserve the backing rule ID, tier, file path, line span, source
   label, commit SHA, extractor ID/version, and safe metadata.
4. WHEN a UI expression depends on dynamic Angular expressions, custom
   directives, pipes, bracket notation, unresolved external templates,
   template-variable ambiguity, `ViewBag`, `ViewData`, dynamic Razor models,
   partial/editor-template ambiguity, generated Razor uncertainty, or custom tag
   helpers without static model identity THEN TraceMap SHALL emit a gap or cap
   the hop at `NeedsReviewLineage`.
5. WHEN server-only model-binding facts exist without a supporting UI binding
   or form-target root THEN they SHALL NOT satisfy `field:`, `control:`, or
   `binding:` selectors.
6. WHEN selectors use generic names such as `id`, `name`, `type`, `value`,
   `state`, or `status` THEN selected roots and downstream paths SHALL remain
   no stronger than `NeedsReviewLineage` at the documented v1 fan-out threshold
   of 10 or more candidate roots unless an exact `fact:`, `symbol:`,
   `model:<type>.<property>`, `dto:<type>.<property>`, source filter, or
   equivalent strong identity narrows the match.
7. WHEN `model:<type>.<property>` or `dto:<type>.<property>` selectors are used
   THEN candidates SHALL be limited to catalogued model-family, view-model,
   DTO, serializer contract, page model, action/handler parameter, or
   model-binding target facts. Server-only model-binding facts MAY satisfy
   `model:` or `dto:` selectors according to their family metadata, but they
   SHALL NOT be presented as UI roots unless a separate UI binding or form
   target bridge connects them to UI evidence.

## Requirement 2: UI-To-Backend Property Identity

**User Story:** As an investigator, I want a UI field or form control to map to
backend model/DTO properties only when static evidence supports that identity.

### Acceptance Criteria

1. WHEN an Angular template binding maps to a component member, form control,
   event handler, or payload field THEN the hop SHALL require direct symbol,
   value-origin, assignment, alias, argument-flow, parameter-forwarding,
   object-shape, or equivalent catalogued evidence.
2. WHEN an Angular event handler writes a UI value into an HTTP body, query, or
   route parameter THEN property-flow SHALL preserve the handler, assignment,
   payload field, and HTTP-call supporting IDs; same-name-only joins SHALL be
   review-tier.
3. WHEN a Razor binding references a model property THEN the hop SHALL require
   static `@model`, page model, view model, action/handler parameter,
   model-binding target, exact fact/symbol identity, or documented syntax
   fallback evidence.
4. WHEN a Razor form target identifies an MVC action or Razor Page handler THEN
   TraceMap SHALL join it to endpoint and model-binding evidence by normalized
   action, controller, page, handler, and HTTP method metadata, not by raw form
   text or short-name proximity alone.
5. WHEN payload fields, form fields, route parameters, query parameters, JSON
   aliases, bind aliases, constructor parameters, or mapper aliases connect to
   backend properties THEN reports SHALL show the alias evidence as supporting
   metadata and SHALL NOT hide ambiguity or promote same-name-only evidence
   above `NeedsReviewLineage`.
6. WHEN alias evidence supports a hop THEN the alias SHALL itself be a
   catalogued static fact with a rule ID and documented limitation. Alias-like
   names without catalogued rule backing SHALL be treated as same-name-only or
   convention-only evidence.
7. WHEN a property fact lacks containing type identity, family identity
   (`model`, `view-model`, `dto`, `unknown`), exact symbol/fact identity, or
   static alias evidence THEN the hop SHALL be `NeedsReviewLineage` or an
   explicit property-identity gap.
8. WHEN multiple candidate DTOs, models, properties, handlers, endpoints, or
   aliases match within caps THEN reports SHALL keep deterministic candidates
   and emit ambiguity gaps rather than choosing a hidden winner.

## Requirement 3: Route-Flow And Endpoint Composition

**User Story:** As a platform engineer, I want property-flow to reuse existing
route-flow and endpoint evidence only when it is tied to the selected property.

### Acceptance Criteria

1. WHEN endpoint alignment connects a client HTTP call or Razor form target to
   a server endpoint through existing combined evidence THEN property-flow MAY
   compose across the source boundary only through the documented endpoint
   bridge and SHALL preserve endpoint-alignment supporting IDs.
2. WHEN `combined_route_flow_edges`, route-flow report rows, or a documented
   successor schema expose route-flow evidence for the same endpoint THEN
   property-flow SHALL attach route-flow context only if the selected property
   reaches that endpoint through rule-backed payload, model-binding,
   assignment, mapping, value-origin, fact-symbol, or explicit safe alias
   evidence.
   A documented successor schema means a table, view, or route-flow JSON field
   named in the implementation PR's `implementation-state.md`, guarded by a
   catalogued schema/report rule, and covered by a public-safe compatibility
   test before property-flow consumes it.
3. WHEN route-flow evidence is endpoint-reachable but not property-specific
   THEN property-flow SHALL emit `RouteFlowNoPropertyContext` or an equivalent
   gap under a catalogued rule and SHALL NOT attach broad endpoint rows as
   property lineage.
4. WHEN route-flow schema is missing, empty, incompatible, reduced, or older
   than the expected contract THEN property-flow SHALL emit
   `RouteFlowUnavailable`, `MissingOptionalSchema`,
   `UnsupportedRouteFlowSchema`, or equivalent gaps with rule IDs and coverage
   labels.
5. WHEN existing route-flow context groups, touched files, touched symbols,
   logic rows, dependency surfaces, argument projections, or fact-symbol
   projections are reused THEN property-flow SHALL include them as supporting
   context with their route-flow rule IDs; it SHALL NOT recompute an independent
   route traversal engine.
6. WHEN cross-source traversal lacks endpoint alignment, route-flow, combined
   path, exact fact/symbol identity, or another documented bridge THEN
   property-flow SHALL stop at the source boundary and emit a gap.

## Requirement 4: Backend Service, Mapping, Query, Data, And Dependency Context

**User Story:** As a maintainer, I want TraceMap to show downstream backend
context only when the selected property participates in the static trail.

### Acceptance Criteria

1. WHEN existing facts expose validation, guard, read/write, assignment,
   manual mapping, projection, object initializer, constructor mapping, mapper
   configuration, parameter-forwarding, argument-flow, or value-origin evidence
   tied to the selected property THEN property-flow SHALL include those hops
   with supporting fact/edge IDs.
2. WHEN existing route-flow/path/reverse evidence exposes service,
   repository-like, query, data-surface, dependency-surface, package/config,
   HTTP client, event/message, storage, WCF, ASMX, remoting, legacy-data, SQL,
   or persistence context tied to the selected property trail THEN property-flow
   SHALL render it as terminal static context.
3. WHEN context is merely reachable from the same endpoint, same containing
   method, same file, same class, or same property name without a
   property-specific bridge THEN property-flow SHALL not attach it as lineage.
4. WHEN evidence is same-name-only, alias-only, convention-only, syntax-only,
   high fan-out, generated-code uncertain, source-local only, or ambiguous THEN
   the affected hop SHALL be capped at `NeedsReviewLineage` or represented as a
   gap.
5. WHEN coverage is reduced, commit SHA is unknown, extractor identity is
   unavailable, optional schema is missing, or traversal is truncated THEN
   absence and terminal conclusions SHALL be no stronger than
   `UnknownAnalysisGap` for affected trails.
6. WHEN no property-specific downstream context exists under credible full
   coverage THEN property-flow MAY emit `NoLineageEvidence` with limitations;
   under reduced coverage it SHALL emit `UnknownAnalysisGap` or a narrower gap.

## Requirement 5: Evidence Model, Rule IDs, And Classifications

**User Story:** As an automation author, I want every emitted node, edge, path,
gap, and report row to be evidence-backed and machine-readable.

### Acceptance Criteria

1. Every selected root, node, edge, path, context row, inventory row, coverage
   warning, and gap SHALL include a rule ID, evidence tier, coverage label,
   supporting fact IDs or edge IDs where available, source label, source index
   ID where available, scan ID where available, commit SHA, extractor
   ID/version, file path, and line span where available.
2. Derived property-flow conclusions SHALL reuse existing `property-flow.*.v1`
   rule IDs where they fit. New rule IDs SHALL be added to
   `rules/rule-catalog.yml` with documented limitations before any output uses
   them.
3. Source evidence SHALL preserve source rule IDs such as
   `typescript.angular.*.v1`, `csharp.razor.*.v1`, `combined.route-flow.*.v1`,
   endpoint-alignment, value-origin, mapping, query, data, dependency, path, and
   reverse rules.
4. Classification SHALL remain in the documented property-flow vocabulary:
   `StrongStaticLineage`, `ProbableStaticLineage`, `NeedsReviewLineage`,
   `UnknownAnalysisGap`, `NoLineageEvidence`, `SelectorNoMatch`, and
   `TruncatedByLimit`, unless a future versioned spec changes it.
5. `StrongStaticLineage` SHALL require a complete static trail with semantic or
   equivalent strong evidence, property-specific bridges, known source identity,
   known commit SHA, compatible schema, no blocking gaps, and full relevant
   coverage.
6. `ProbableStaticLineage` SHALL require at least Tier2 structural
   property-specific evidence for each non-semantic hop; route/controller/action
   name proximity, broad endpoint reachability, source proximity, or same-name
   matching alone SHALL NOT qualify.
7. Gap codes emitted by this feature SHALL come from a closed set documented in
   the implementation PR before output changes. The initial closed set for this
   spec is: `SelectorNoMatch`, `AmbiguousSelector`,
   `PropertyIdentityUnavailable`, `SameNameOnlyPropertyMatch`,
   `GenericPropertyFanOut`, `EndpointAlignmentUnavailable`,
   `RouteFlowUnavailable`, `RouteFlowNoPropertyContext`,
   `UnsupportedRouteFlowSchema`, `MapperEvidenceUnavailable`,
   `TerminalContextUnavailable`, `MissingOptionalSchema`, `ReducedCoverage`,
   `UnknownCommitSha`, `ExtractorIdentityUnavailable`, `UnsafeInputRejected`,
   `RedactedUnsafeValue`, and `TruncatedByLimit`.
8. WHEN property-specific evidence is weak but present, such as syntax-only,
   same-name-only, alias-only with catalogued alias evidence, convention-only,
   high fan-out, or review-tier endpoint/model-binding evidence, THEN the hop
   SHALL be represented as `NeedsReviewLineage` with supporting IDs and may
   also emit a gap. WHEN property identity, endpoint alignment, route-flow
   context, mapper evidence, terminal context, schema, or coverage is absent,
   dynamic-only, incompatible, or unsafe to inspect, THEN TraceMap SHALL emit a
   gap and SHALL NOT create a lineage hop for that boundary.

## Requirement 6: Safe Output, Redaction, And Export Consumers

**User Story:** As a reviewer preparing reports, vault notes, RAG import
chunks, docs-export artifacts, or static explorer pages, I want lineage output
to remain deterministic and safe by default.

### Acceptance Criteria

1. Markdown, JSON, vault export, RAG/docs-export chunks, evidence graph export,
   static explorer output, evidence-pack output, diagnostics, and logs SHALL
   omit, hash, or safely describe raw source snippets, raw HTML, raw SQL, raw
   config values, connection strings, secrets, credentials, tokens, raw URLs
   with host/query data, hostnames, raw remotes, local absolute paths, private
   sample labels, form values, submitted values, anti-forgery tokens, and
   unreviewed generated artifact paths.
2. Default reports SHALL store safe property paths, names, closed-set kinds,
   line spans, hashes, rule IDs, tiers, supporting IDs, and limitations; raw
   snippets SHALL remain unavailable unless a separate explicit raw-snippet
   option is reviewed and implemented.
3. WHEN new property-flow rows or metadata are added THEN report consumers
   SHALL either render safe additive fields deterministically or emit a
   documented compatibility gap. Consumers SHALL NOT silently forward unknown
   unsafe metadata into public/demo output.
4. WHEN a row or metadata value is hidden-only or unsafe THEN public/demo-safe
   exports SHALL omit, redact, hash, or reject it and cite the appropriate
   redaction rule.
5. Identical inputs SHALL produce byte-stable Markdown, JSON, vault/RAG export
   chunks, and generated artifact output for touched consumers.
6. Stable IDs SHALL derive only from safe deterministic inputs such as source
   label, source index ID, commit SHA, repo-relative file path, line span,
   closed-set kind, supporting fact IDs, supporting edge IDs, symbol IDs, and
   safe hashes.

## Requirement 7: Runtime-Assisted Ideas Remain Out Of Core Scope

**User Story:** As a maintainer, I want optional browser or runtime-assisted
ideas to stay separate from deterministic scanner claims.

### Acceptance Criteria

1. Core scanner, reducer, property-flow, route-flow, report, export, vault,
   RAG/docs-export, and explorer behavior SHALL NOT require browser execution,
   computer-use automation, live HTTP requests, production login, credential
   use, traffic capture, telemetry, DOM observation, or runtime app execution.
2. IF runtime-assisted or browser-observed ideas are mentioned THEN they SHALL
   be marked future/out of scope for this spec and SHALL NOT upgrade static
   classifications, absence conclusions, route-flow composition, or impact
   findings.
3. Observed evidence metadata, if consumed by existing options, SHALL remain
   local/demo context only and SHALL preserve existing non-upgrading behavior.
4. The implementation SHALL NOT add LLM calls, embeddings, vector databases, or
   prompt-based classification to the core scanner, reducer, report, or export
   logic.

## Requirement 8: Public-Safe Validation

**User Story:** As a reviewer, I want validation fixtures and commands that
prove the composition rules without private data.

### Acceptance Criteria

1. Tests SHALL use synthetic or public-safe fixtures for Angular component and
   template bindings, reactive forms, template-driven controls, event handlers,
   payload construction, HTTP calls, endpoint alignment, Razor form targets,
   MVC actions, Razor Page handlers, model-binding targets, DTO/model
   properties, mapping, validation, query, service, data, and dependency
   context.
2. Tests SHALL cover successful property-specific composition and negative
   cases for same-name-only matches, generic fan-out, ambiguity, route-flow
   unavailable, route-flow no property context, endpoint unavailable, missing
   model-binding target, missing mapper evidence, missing terminal context,
   reduced coverage, missing optional schema, unknown commit SHA, and
   truncation.
3. Tests SHALL cover safe Markdown/JSON rendering, vault/RAG/docs-export
   compatibility when touched, static explorer/evidence-pack compatibility when
   touched, deterministic ordering, byte stability, rule-catalog resolution,
   and private-path guard compatibility.
4. Validation SHALL run focused `PropertyFlowTests`, relevant route-flow tests,
   relevant Razor/.NET and TypeScript adapter tests when extraction changes,
   relevant export/consumer tests when row shapes change, `dotnet build
   src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`,
   `./scripts/check-private-paths.sh`, and `git diff --check`, or record an
   explicit public-safe deferral in implementation state.
5. For language-adapter changes, validation SHALL follow `docs/VALIDATION.md`
   and run or explicitly defer the relevant pinned smoke checks with rationale,
   including the TypeScript/Angular adapter checks when Angular extraction or
   fixtures change.
