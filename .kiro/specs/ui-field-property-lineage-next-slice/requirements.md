# UI Field and Property Lineage Next Slice Requirements

## Introduction

The first UI field/property lineage spec established the broad TraceMap
property-flow direction and the initial implementation slices added a
deterministic `tracemap property-flow` command, Angular template/form evidence,
Razor binding/form-target evidence, safe Markdown/JSON output, and basic
composition over existing combined graph evidence.

This continuation spec defines the next clean implementation boundary: connect
existing UI field/control/binding roots to DTO/model properties, then compose
those properties into existing static route, path, reverse, data, dependency,
vault, docs-export, and static HTML explorer evidence where the combined index
already contains rule-backed facts.

This remains deterministic static analysis. It does not prove runtime DOM
visibility, browser execution, live HTTP behavior, production traffic, runtime
authorization, feature flag state, branch feasibility, or runtime serializer/DI
behavior. It does not add LLM calls, embeddings, vector databases, or
prompt-based classification to the core scanner, reducer, report, or export
paths.

## Existing Baseline

The next slice assumes the existing UI lineage work already provides:

- `tracemap property-flow` over a combined index.
- Selector parsing for `field:`, `control:`, `binding:`, `model:`, `dto:`,
  `symbol:`, and `fact:`.
- Source and framework filters.
- Generic property-name downgrade behavior for names such as `id`, `name`,
  `type`, `value`, `state`, and `status`.
- Angular template/form/event/template-variable facts and binding gaps.
- Razor `asp-for`, `Html.*For`, static form target, and binding-gap facts.
- Read-only report composition with stable root/node/edge/path/gap IDs.
- Markdown and JSON report output with rule IDs, evidence tiers, file spans,
  commit SHAs, extractor IDs/versions, safe display metadata, coverage labels,
  and limitations.
- Safe export/documentation integration points for property-flow report data.

The next slice must not reimplement those completed foundations except where a
narrow contract adjustment is required to compose new evidence safely.

## Scope

In scope:

- Static binding-to-property matching from Angular and Razor UI evidence to
  DTO/model/view-model property identity.
- Razor/MVC/Pages form target to action or handler matching where static facts
  support it.
- Model-binding target facts for action parameters, handler parameters,
  `[FromBody]`, `[FromForm]`, `[BindProperty]`, page model properties, and
  view-model properties.
- TypeScript event-handler, value-origin, payload field, and HTTP call
  composition only when direct assignment, argument flow, or existing
  value-origin evidence supports the hop.
- Endpoint-aligned payload field to server DTO/model property matching, with
  ambiguity and fan-out controls.
- DTO/model property mapping through existing or newly documented deterministic
  facts for manual assignment, object initializer, projection, constructor
  argument, and mapper configuration patterns.
- Downstream composition into existing route-flow, paths, reverse, data,
  dependency, vault, docs-export, and static HTML explorer artifacts.
- Public-safe synthetic fixtures and focused tests for the first implementation
  PR boundary.
- Explicit gaps and limitations when route-flow, path, schema, property,
  model-binding, or mapper evidence is absent.

Out of scope:

- New runtime browser/computer-use capture in core property-flow.
- Claims that observed DOM fields prove static lineage.
- Live HTTP requests, production login, credentials, traffic capture, or
  environment-specific route proof.
- Whole-application property inventory by default.
- Full taint analysis, symbolic execution, branch feasibility, runtime DI
  solving, reflection solving, or runtime serializer expansion.
- Raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw remotes, hostnames, private sample labels, or private project routes in
  generated artifacts, tests, or spec examples.

## Requirements

### Requirement 1: Focused Continuation Boundary

**User Story:** As a maintainer, I want the next implementation slice to build
on the existing property-flow baseline without duplicating or destabilizing the
completed UI/root/report work.

#### Acceptance Criteria

1. WHEN implementation begins THEN it SHALL treat the prior
   `ui-field-property-lineage` spec and implementation state as baseline
   context.
2. WHEN existing selector parsing, Angular binding facts, Razor binding facts,
   Markdown/JSON output, or basic combined graph traversal already satisfy a
   requirement THEN the implementation SHALL reuse those contracts instead of
   replacing them.
3. WHEN this slice changes an existing report schema field THEN it SHALL keep
   backward-compatible nullable/default behavior or bump the documented
   property-flow report version.
4. WHEN this slice adds source facts or derived edges THEN each new conclusion
   SHALL carry a rule ID, evidence tier, file path, line span, commit SHA,
   source label, scan ID, extractor ID/version, and documented limitation.
5. WHEN a hop cannot be proven under the next-slice rules THEN TraceMap SHALL
   emit a gap or review-tier classification instead of silently joining by
   display text.

### Requirement 2: Field/Control/Binding Selector Behavior

**User Story:** As a reviewer, I want UI selectors to find the intended static
field or binding roots while making ambiguous generic matches obvious.

#### Acceptance Criteria

1. WHEN `field:<name>`, `control:<name>`, or `binding:<name>` is used THEN root
   matching SHALL consider only documented safe metadata fields from UI binding,
   form control, event binding, template variable, Razor binding, form target,
   and model-binding target facts.
2. WHEN selector values are generic property names THEN selected roots and
   downstream paths SHALL remain no stronger than `NeedsReviewLineage` unless
   a stronger selector context such as `--source`, `fact:`, `symbol:`,
   `model:<type>.<property>`, or `dto:<type>.<property>` narrows identity.
3. WHEN multiple roots match THEN reports SHALL include deterministic top-N
   roots, total candidate count, and `AmbiguousSelector` gaps rather than
   choosing a hidden winner.
4. WHEN a selector is rejected for unsafe input THEN diagnostics SHALL name the
   unsafe category only and SHALL NOT echo the raw value.
5. WHEN source and framework filters are provided THEN matching SHALL remain
   deterministic, case-insensitive for source labels, and limited to closed
   framework labels.

### Requirement 3: Static Binding-To-Property Matching

**User Story:** As an investigator, I want a UI field/control/binding root to
connect to DTO/model properties only when static evidence supports the
identity.

#### Acceptance Criteria

1. WHEN an Angular template property path has a static component member,
   reactive form control, template-driven control, or event handler connection
   THEN property-flow SHALL preserve that source evidence and attempt the next
   hop only through direct symbol, value-origin, or object-shape evidence.
2. WHEN an Angular event handler writes a UI value into a payload field THEN
   the hop SHALL require a rule-backed assignment, argument flow, local alias,
   field alias, or parameter-forwarding fact.
3. WHEN a payload field is sent by an HTTP call THEN the hop SHALL require an
   object-shape, argument-flow, body-field, query-field, route-parameter, or
   equivalent deterministic fact attached to the HTTP call.
4. WHEN Razor `asp-for` or `Html.*For` references a property path THEN
   property-flow SHALL connect it to a view-model/model property only through
   static model metadata, `@model` metadata, page model metadata, action/handler
   parameter metadata, or a documented syntax fallback.
5. WHEN a Razor form target is static THEN TraceMap SHALL attempt to connect it
   to MVC action or Razor Page handler facts using normalized action,
   controller, page, handler, and method metadata.
6. WHEN same-name matching is the only bridge between a UI control and a
   model/DTO property THEN the hop SHALL be `NeedsReviewLineage` and SHALL cite
   a same-name or ambiguity limitation. Same-name-only means the property name
   matches, but containing type identity, symbol ID, exact fact identity,
   rule-backed alias evidence, or a binding/value-origin fact connecting the UI
   root to that specific type is absent. When same-name-only and high fan-out
   rules both apply, reports SHALL emit both gap codes where the gap cap allows,
   and SHALL apply the stricter classification cap.
7. WHEN binding identity crosses dynamic template expressions, custom
   directives, `ViewBag`, `ViewData`, partial/editor template ambiguity,
   reflection, serializer runtime configuration, or branch feasibility THEN the
   result SHALL be a gap or review-tier hop.

### Requirement 4: Model/DTO Property Identity And Ambiguity

**User Story:** As an API reviewer, I want DTO/model property identity to be
precise enough that common names do not create false lineage.

#### Acceptance Criteria

1. WHEN `model:<type>.<property>` is used THEN candidates SHALL be limited to
   model, view-model, Razor model-binding target, or server property facts.
2. WHEN `dto:<type>.<property>` is used THEN candidates SHALL be limited to DTO
   or serializer contract facts, plus parameter facts explicitly classified as
   DTO/body contract evidence.
3. WHEN a property fact lacks containing type identity THEN it SHALL NOT be
   promoted above `NeedsReviewLineage` unless selected by exact `fact:` or
   equivalent strong identity.
4. WHEN a property belongs to multiple families, such as DTO and view-model,
   reports SHALL show the overlap as ambiguity metadata rather than silently
   preferring one family. A fact explicitly classified as both families MAY
   appear for both `model:` and `dto:` selectors, but the overlap SHALL remain
   visible.
5. WHEN serializer aliases, bind aliases, JSON names, form names, constructor
   parameter names, or mapper aliases exist as static facts THEN property-flow
   MAY use them as supporting evidence and SHALL preserve both source and
   derived rule IDs.
6. WHEN high fan-out is detected for generic or common property names THEN
   roots, edges, and paths SHALL downgrade to `NeedsReviewLineage` or emit a
   fan-out gap under a documented threshold.
7. WHEN no DTO/model property evidence exists after a UI-to-HTTP or
   form-to-action hop THEN reports SHALL emit a `PropertyIdentityUnavailable`
   or equivalent gap and SHALL NOT claim downstream property lineage.

### Requirement 5: Downstream Static Composition

**User Story:** As a platform engineer, I want property lineage to compose with
existing static route, path, reverse, data, dependency, and export evidence
without inventing new runtime claims.

#### Acceptance Criteria

1. WHEN endpoint alignment connects a client HTTP call to a server endpoint
   through `combined_facts` and `index_sources`, or through a documented
   `endpoint_matches` successor schema when available, THEN property-flow SHALL
   attempt payload-field to model-binding matching only with existing endpoint,
   parameter, DTO/model, and value-origin evidence.
2. WHEN route-flow evidence is available through the current documented schema
   signal, `combined_route_flow_edges`, or a documented successor THEN
   property-flow MAY include route-flow edges as supporting evidence after the
   property reaches an HTTP call, endpoint, action, or handler.
3. WHEN route-flow evidence is absent THEN property-flow SHALL still show
   local UI-to-payload, form-to-handler, endpoint, model-binding, and property
   evidence where present, plus a route-flow-unavailable gap only for hops that
   genuinely require route-flow-specific semantics.
4. WHEN combined path or reverse evidence already establishes part of the
   downstream trail THEN property-flow SHALL include that evidence as
   supporting context rather than recomputing incompatible semantics.
5. WHEN validation, mapping, service, repository, query, data surface, or
   dependency surface evidence is reached THEN the report SHALL show terminal
   static evidence with safe metadata and limitations.
6. WHEN downstream evidence is missing because optional combined tables are
   absent, empty, or reduced coverage THEN reports SHALL emit schema or
   coverage gaps rather than `NoLineageEvidence`.
7. WHEN all contributing sources have credible full coverage and no path exists
   THEN property-flow MAY emit `NoLineageEvidence`, scoped to the queried
   selector and available static evidence only.

### Requirement 6: Reports, Exports, And Safe Artifact Integration

**User Story:** As an automation author, I want property-flow JSON and Markdown
to remain stable, public-safe, and consumable by existing generated artifact
workflows.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN the existing section order SHALL remain stable
   and SHALL add any new binding, property, mapping, or downstream evidence
   rows without removing existing required sections.
2. WHEN JSON is emitted THEN new fields SHALL be nullable or optional under a
   documented version policy, arrays and maps SHALL be deterministically
   ordered, and metadata keys SHALL be sorted.
3. WHEN vault export, docs-export/RAG-targeted documentation, or static HTML
   explorer consumes property-flow output THEN it SHALL treat reports as static
   evidence artifacts and SHALL NOT infer runtime behavior, reachability,
   production traffic, or impact beyond cited rule-backed evidence.
4. WHEN a generated artifact cannot consume new property-flow fields safely
   THEN it SHALL emit the consumer's existing unsupported-schema or
   unavailable-family gap rules rather than dropping evidence silently.
5. WHEN reports include property, route, data, or dependency metadata THEN they
   SHALL omit or hash unsafe values and SHALL NOT render raw snippets, raw SQL,
   raw URLs, hostnames, raw remotes, local absolute paths, secrets, config
   values, private sample labels, or private routes.
6. WHEN outputs are generated twice from identical inputs THEN Markdown, JSON,
   and downstream generated artifacts SHALL be byte-stable except for explicitly
   documented version changes.

### Requirement 7: Browser/Computer-Use Boundary

**User Story:** As a demo operator, I want optional browser observation to stay
separate from deterministic static claims.

#### Acceptance Criteria

1. WHEN browser or computer-use evidence is considered THEN it SHALL remain
   deferred unless a future opt-in command defines public-safe capture rules.
2. WHEN optional observed evidence exists in a future artifact THEN it SHALL be
   labeled as observed/demo metadata and SHALL NOT upgrade static lineage
   classifications by itself.
3. WHEN browser observation requires credentials, production login, private
   data capture, live secret-bearing HTTP, or private hostnames THEN the public
   workflow SHALL reject it.
4. WHEN browser observation disagrees with static evidence THEN reports SHALL
   show a validation discrepancy gap and SHALL NOT replace static facts with
   runtime observations.
5. WHEN no browser evidence exists THEN the core property-flow report SHALL
   remain complete under static coverage semantics and SHALL NOT warn merely
   because browser capture was not run.

### Requirement 8: First Implementation PR Boundary And Validation

**User Story:** As a reviewer, I want the first product-code PR after this spec
to be small enough to review and validate with public-safe fixtures.

#### Acceptance Criteria

1. The first implementation PR SHALL focus on static model-binding target facts
   and property identity joins for Angular-to-payload-to-HTTP and
   Razor-form-to-handler/model-binding paths.
2. The first implementation PR SHALL include public-safe synthetic Angular and
   Razor/MVC/Pages fixtures with no private names, paths, routes, SQL, config,
   remotes, hostnames, or snippets in expected artifacts.
3. The first implementation PR SHALL include focused tests for direct
   assignment/value-origin hops, same-name review-tier hops, generic
   property-name fan-out downgrade, ambiguous DTO/model family overlap, and
   missing property evidence gaps.
4. The first implementation PR SHALL include focused tests for report JSON and
   Markdown stability, safe rendering, source/framework filters, and older
   combined indexes missing optional downstream schema.
5. The first implementation PR SHALL update rule catalog entries and
   limitations for every new scanner fact and derived report rule it emits.
6. Implementation validation SHALL run `dotnet test` for property-flow and
   changed extractor tests, `dotnet build` or the repository-required narrower
   equivalent, `git diff --check`, `./scripts/check-private-paths.sh`, and any
   adapter smoke checks required by `docs/VALIDATION.md`.
7. This spec-only PR SHALL run Kiro spec review when local tooling is
   available, `git diff --check`, `./scripts/check-private-paths.sh`, and any
   existing spec lint/check if present.
