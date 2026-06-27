# UI Field Property Lineage Terminal Context Requirements

## Introduction

TraceMap already has `tracemap property-flow` support for selected UI,
model, DTO, symbol, and fact roots; first-hop property identity evidence; and
route-flow schema/gap handling. PR #376 added
`UnsupportedRouteFlowSchema` plus property-flow route-schema gap evidence.

This follow-up is backend-only. It defines how `property-flow` may attach
validation, read/write, mapping, service, query, data, and dependency terminal
context after a selected property has reached backend evidence. Terminal
context is useful only when existing deterministic facts expose a selected
property trail. Broad route reachability, endpoint reachability, same file,
same class, same method, or same property name is not enough.

Public claim level: hidden. The output remains a local deterministic evidence
packet and must not be used as public product copy without a separate reviewed
claim pass.

## Source Material

- PR #376: Harden property-flow route schema gaps.
- `.kiro/specs/ui-field-property-lineage/`
- `.kiro/specs/ui-field-property-lineage-next-slice/`
- `.kiro/specs/ui-field-property-lineage-continuation/`
- `.kiro/specs/ui-field-property-lineage-composition/`
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/route-flow-service-data-composition-next/`
- `.kiro/specs/route-flow-service-data-composition-final/`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`

## Existing Baseline

This spec assumes the current `dev` baseline includes:

- `tracemap property-flow` report version `1.0`.
- Selected roots for `field:`, `control:`, `binding:`, `model:`, `dto:`,
  `symbol:`, and `fact:` selectors.
- Angular/Razor/property evidence roots and first-hop property identity joins
  where existing facts support them.
- Route-flow availability signals for missing, empty, available, and
  unsupported `combined_route_flow_edges`.
- `UnsupportedRouteFlowSchema` emitted under `property-flow.schema.v1`.
- `RouteFlowNoPropertyContext` emitted when route-flow rows match an endpoint
  but no selected-property bridge allows attaching route-flow rows.

## Requirement 1: Backend Terminal Context Is Property-Trail Gated

**User Story:** As a reviewer, I want terminal backend context attached to a
property-flow report only when the selected property statically reaches that
context.

### Acceptance Criteria

1. WHEN existing facts expose validation, guard, read, write, assignment,
   mapping, projection, service, query, data, or dependency evidence tied to
   the selected property trail THEN property-flow MAY attach that evidence as
   backend terminal context.
2. WHEN terminal context is attached THEN each attached row SHALL preserve rule
   ID, evidence tier, source label, source index ID where available, scan ID
   where available, commit SHA, extractor ID/version, file path, line span,
   safe display metadata, and supporting fact or edge IDs.
3. WHEN the selected property reaches an endpoint but terminal context is only
   broadly endpoint-reachable THEN property-flow SHALL NOT attach that terminal
   context.
4. WHEN context is reachable only by same containing method, same class, same
   file, same route, same endpoint, same short symbol name, or same property
   name THEN property-flow SHALL NOT attach it as terminal property context.
5. WHEN weak-but-present property-specific evidence exists, such as same-name
   plus a catalogued alias/value-origin/object-shape/model-binding bridge, THEN
   property-flow MAY emit `NeedsReviewLineage` context and SHALL explain the
   weaker evidence. Generic names such as `id`, `name`, `value`, `state`,
   `status`, `type`, `result`, or `response` SHALL remain insufficient as the
   primary bridge unless narrowed by exact fact/symbol identity, type-qualified
   model/DTO identity, or Tier2-or-stronger structural property identity. If
   the implementation keeps or expands this generic-name set, it SHALL update
   the live `PropertyFlowReporter` generic-name set and tests in the same
   implementation PR so the documented downgrade behavior and code do not
   drift.
6. WHEN no property-specific bridge exists but potentially relevant terminal
   facts are present nearby THEN property-flow SHALL emit a catalogued
   terminal-context gap or omit context, not a lineage edge.

## Requirement 2: Existing Facts Only, No New Runtime Claims

**User Story:** As a maintainer, I want terminal context reuse to remain a
static report-layer composition over existing facts.

### Acceptance Criteria

1. WHEN this feature is implemented THEN it SHALL consume existing combined
   facts, combined graph edges, route-flow report/schema evidence, path/reverse
   evidence, argument/value-origin evidence, fact-symbol evidence, mapping
   evidence, query/data/dependency facts, or documented successors.
2. WHEN existing facts do not expose a selected-property trail THEN the
   implementation SHALL stop or gap rather than infer through runtime behavior.
3. WHEN route-flow context groups, touched files, touched symbols, logic rows,
   dependency surfaces, argument projections, fact-symbol projections, path
   rows, or reverse rows are reused THEN property-flow SHALL treat them as
   supporting terminal context only after a selected-property bridge is proven.
4. WHEN a route-flow/path/reverse row is attached THEN the source route-flow,
   path, reverse, query, data, or dependency rule IDs SHALL be preserved as
   supporting evidence.
5. WHEN the implementation needs a new fact family, extractor, persisted row,
   or scanner output THEN that work SHALL be deferred unless a separate
   implementation PR explicitly updates the owning scanner spec and validation.

## Requirement 3: Rule Catalog First

**User Story:** As an automation author, I want every new terminal-context
conclusion or gap to resolve to a documented rule before it can appear in
output.

### Acceptance Criteria

1. WHEN an implementation emits a new rule ID or gap code THEN
   `rules/rule-catalog.yml` SHALL include the rule, emitted artifact, evidence
   tier, and limitations in the same PR before the output is emitted.
2. WHEN an existing property-flow, route-flow, path, reverse, query, data,
   dependency, mapping, value-origin, or redaction rule already covers the
   behavior THEN the implementation SHALL reuse it instead of minting a
   duplicate rule.
3. WHEN a candidate new terminal-context rule is needed THEN it SHALL use a
   property-flow namespace such as `property-flow.terminal-context.v1` only
   after catalog review documents limitations.
4. WHEN gap codes are added THEN every emitted gap code SHALL map to a
   catalogued rule whose `emits` or limitations document the artifact. This
   spec or `implementation-state.md` MAY record the mapping, but neither
   authorizes emission without a catalogued rule.
5. WHEN a rule-catalog entry changes limitations or emitted artifact meaning
   THEN implementation SHALL update tests and validation notes to cover the new
   behavior.

## Requirement 4: Safe Report Contract

**User Story:** As a report consumer, I want terminal context to be additive,
safe, deterministic, and compatible with property-flow reports.

### Acceptance Criteria

1. WHEN terminal context is added to Markdown or JSON THEN output SHALL remain
   deterministic for identical inputs.
2. WHEN terminal context is added to report version `1.0` THEN the new metadata
   SHALL be additive and safely ignorable by current consumers, or the
   implementation SHALL bump the report version and document the reason. If
   terminal context becomes a required top-level key or changes the meaning of
   existing path, gap, inventory, root, or summary rows, the report version
   SHALL bump.
3. WHEN safe metadata is rendered THEN it MAY include property names, safe
   symbol displays, context kind, terminal kind, source labels, hashes,
   normalized route keys, table/key hashes, and supporting IDs.
4. WHEN unsafe values are encountered THEN reports SHALL omit, hash, or
   category-label them and cite a redaction rule or gap.
5. WHEN terminal context is absent under reduced coverage, unknown commit SHA,
   missing extractor identity, missing optional schema, incompatible schema, or
   traversal caps THEN absence conclusions SHALL be no stronger than
   `UnknownAnalysisGap`.
6. WHEN an implementation keeps report version `1.0` after adding terminal
   context metadata THEN tests SHALL prove at least one existing property-flow
   consumer, such as docs-export or an evidence-export path touched by the PR,
   safely ignores or renders the additive metadata without unsafe forwarding.
7. WHEN hashes are rendered for terminal context or redaction THEN the hash
   algorithm SHALL be stable, salt-free for report output, machine-independent,
   and byte-stable across repeated runs for identical inputs.

## Requirement 5: Explicit Non-Claims

**User Story:** As a reviewer, I want report wording and tests to prevent
terminal context from being mistaken for runtime or impact proof.

### Acceptance Criteria

1. property-flow SHALL NOT claim runtime behavior, production execution,
   browser visibility, user interaction, authorization behavior, feature-flag
   state, dependency-injection runtime target selection, serializer runtime
   behavior, branch feasibility, database execution, persistence outcome,
   traffic, release safety, or business impact.
2. property-flow SHALL NOT say a property is "impacted" unless a separate
   reducer with evidence produces that claim.
3. property-flow SHALL NOT use LLM calls, embeddings, vector databases, or
   prompt-based classification in scanner, reducer, report, export, or terminal
   context composition logic.
4. Markdown and JSON wording SHALL describe attached rows as static evidence
   context or terminal context, not proof of execution or complete coverage.
5. Any public/demo copy touched by implementation SHALL remain bounded to
   deterministic static evidence and hidden-claim readiness unless a separate
   site/public-copy spec changes that posture.

## Requirement 6: Tests For Negative Attachment Cases

**User Story:** As an implementer, I want regression tests that prove terminal
context is not attached from broad or proximity-only evidence.

### Acceptance Criteria

1. Tests SHALL prove broad endpoint reachability alone does not attach
   validation, read-write, mapping, service, query, data, or dependency
   terminal context.
2. Tests SHALL prove route reachability alone does not attach terminal context.
3. Tests SHALL prove same method proximity alone does not attach terminal
   context.
4. Tests SHALL prove same class proximity alone does not attach terminal
   context.
5. Tests SHALL prove same file proximity alone does not attach terminal
   context.
6. Tests SHALL prove same property name alone does not attach terminal context.
7. Tests SHALL prove same short symbol name alone does not attach terminal
   context.
8. Tests SHALL prove a broad dependency edge from the endpoint alone does not
   attach terminal context.
9. Tests SHALL include at least one positive fixture where existing
   property-specific facts do attach terminal context.
10. Tests SHALL include at least one positive fixture where weak-but-present
   catalogued property-specific evidence attaches terminal context as
   `NeedsReviewLineage` and renders the weaker-evidence explanation.
11. Tests SHALL assert every emitted terminal-context rule ID resolves to
   `rules/rule-catalog.yml`.
12. Tests SHALL assert every emitted terminal-context gap code maps to a
   catalogued rule in `rules/rule-catalog.yml`.
13. Tests SHALL assert missing or insufficient terminal-context evidence emits a
   gap or omits context, rather than silently upgrading path classification.

## Requirement 7: Validation

**User Story:** As a maintainer, I want implementation validation to follow the
repository's deterministic evidence and adapter guidance.

### Acceptance Criteria

1. Implementation PRs SHALL run the relevant `docs/VALIDATION.md`
   property-flow checks unless explicitly deferred with reason and risk.
2. Implementation PRs SHALL run `dotnet test` for focused
   `PropertyFlowTests` and any touched route-flow/path/reverse/export test
   groups.
3. Implementation PRs SHALL run `dotnet test src/dotnet/TraceMap.sln` unless
   explicitly deferred with reason and risk.
4. Implementation PRs SHALL run `./scripts/check-private-paths.sh` and
   `git diff --check`.
5. When language adapter behavior changes, implementation SHALL follow
   `docs/VALIDATION.md` for the affected adapter and pinned smoke checks.
6. Failed build or reduced validation SHALL be recorded as partial, not clean.
