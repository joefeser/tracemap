# Route Flow Endpoint Composition Requirements

## Introduction

TraceMap can extract endpoint route bindings and raw downstream call evidence,
but route-flow and path reports can still fail to stitch those facts into an
end-to-end static route trace. A private/local sample showed route root
evidence and raw call evidence, but the report did not compose the route root,
method symbol, interface call, implementation relationship, implementation body,
and downstream dependency/data evidence into a bounded route-flow result.

This spec defines the next deterministic route-flow composition slice. It is a
report/query improvement over existing index evidence, not a scanner rewrite and
not runtime tracing. It does not execute code, prove deployment or traffic,
resolve dependency injection at runtime, execute SQL, connect to services, use
LLMs, use embeddings, use vector databases, or perform prompt-based
classification.

Public claim level: static evidence only. Private validation must be described
with generic wording and must not disclose private local paths, private
repository names, exact private routes, raw source snippets, raw SQL/config
values, secrets, or raw remotes.

## Requirements

### Requirement 1: Route Binding To Endpoint Method-Symbol Bridge

**User Story:** As a maintainer, I want route-flow to start from the method
symbol behind an endpoint route so that downstream call evidence can be composed
from the correct root.

#### Acceptance Criteria

1. WHEN endpoint route-binding facts identify a handler, controller action, or
   endpoint method symbol THEN route-flow SHALL use that source-local method
   symbol as the route trace root.
2. WHEN route-binding facts include semantic symbol evidence THEN the
   method-symbol bridge SHALL preserve Tier1 semantic evidence, rule IDs, fact
   IDs, symbol IDs, source labels, scan IDs, commit SHA, extractor identity, file
   path, and line span.
3. WHEN only syntax or structural route evidence is available THEN route-flow
   MAY create a review-tier method-symbol bridge only when a source-local method
   symbol can be matched without global short-name stitching, and the bridge
   SHALL be capped at `NeedsReviewStaticRouteFlow` or weaker.
   A source-local match is ambiguous when two or more method symbols in that
   source share the same short name or fallback key, or when generated, partial,
   overload, or multi-dispatch patterns prevent selecting one unique symbol.
4. WHEN the plain selector-miss case occurs, such as a route selector matching
   no supported route-flow entry evidence, route-flow SHALL preserve the
   existing `SelectorNoMatch` gap. WHEN selector context exists but endpoint
   route-root evidence needed for composition is unavailable THEN route-flow MAY
   emit the additive `MissingRouteRoot` gap instead of an empty clean result.
5. WHEN a route root exists but cannot be tied to a source-local method symbol
   THEN route-flow SHALL emit a `MissingMethodSymbolBridge` gap and SHALL NOT
   start traversal from a guessed method name.
6. WHEN endpoint route evidence comes from reduced coverage, syntax fallback,
   missing extractor metadata, unknown commit SHA, or an older schema THEN the
   report SHALL mark coverage reduced and SHALL NOT classify absence as clean
   `NoRouteFlowEvidence`.
7. WHEN multiple method symbols could match the same route-binding evidence THEN
   route-flow SHALL sort candidates deterministically, emit an ambiguity gap or
   needs-review rows, and SHALL NOT choose a single route root as proven.

### Requirement 2: Endpoint Method To Call Edge Composition

**User Story:** As a reviewer, I want route-flow to connect the endpoint method
to downstream call edges when TraceMap has static evidence for those calls.

#### Acceptance Criteria

1. WHEN a route trace root has outgoing `combined_call_edges`,
   `combined_dependency_edges`, object-creation edges, argument-flow edges, or
   parameter-forwarding edges that are source-local and rule-backed THEN
   route-flow SHALL compose them as bounded static path edges.
2. WHEN a composed edge is emitted THEN the row SHALL preserve supporting edge
   IDs, supporting fact IDs where available, caller and callee symbol IDs,
   source labels, rule IDs, evidence tiers, file paths, line spans, commit SHA,
   extractor identities, and limitations.
3. WHEN raw downstream call evidence exists in the index but cannot be
   source-locally connected from the endpoint method THEN route-flow SHALL emit
   a `MissingCallEdge` or narrower bridge gap rather than inventing a flow.
4. WHEN traversal uses object creation or parameter forwarding as context THEN
   the report SHALL describe that evidence as static context and SHALL NOT claim
   constructor execution order, object lifetime, branch feasibility, or runtime
   receiver identity.
5. WHEN traversal reaches a cap for depth, path count, frontier size, row count,
   or gap count THEN the report SHALL mark the trace partial and emit a
   `TraversalBounds` or `TruncatedByLimit` gap.
6. WHEN call edges are syntax-only, textual, name-only, ambiguous, high fan-out,
   or reduced-coverage THEN route-flow SHALL cap the affected row and summary
   classification at `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.

### Requirement 3: Interface Member Call To Implementation Member Bridge

**User Story:** As an engineer, I want interface calls to show static
implementation candidates without pretending TraceMap knows which runtime
implementation dependency injection selects.

#### Acceptance Criteria

1. WHEN route-flow reaches an interface member call and
   `combined_symbol_relationships`, `combined_dependency_edges`, or equivalent
   symbol relationship facts identify implementation members THEN route-flow MAY
   emit `interface-implementation-candidate` bridge rows.
2. Candidate bridge rows SHALL cite relationship evidence, interface member
   symbol, implementation member symbol, source label, rule IDs, evidence tier,
   supporting fact or edge IDs, file paths, line spans, commit SHA, extractor
   identity, and a limitation.
3. Candidate bridge rows SHALL NOT claim runtime dependency injection
   resolution, service locator binding, configuration binding, factory
   selection, dynamic dispatch target selection, host activation, production
   execution, or traffic.
4. WHEN any path requires an interface-to-implementation candidate bridge THEN
   the path SHALL be capped at `NeedsReviewStaticRouteFlow` or weaker, even when
   a single compiler-resolved candidate exists.
5. WHEN direct non-interface call evidence to a concrete implementation also
   exists THEN route-flow MAY emit that direct edge separately and MAY classify
   it using the stronger direct edge evidence, while keeping the interface
   candidate row review-tier.
6. WHEN no implementation relationship exists THEN route-flow SHALL preserve
   the interface call row and emit `MissingImplementationBridge` or
   `ImplementationCandidateUnavailable`.
7. WHEN implementation relationships are syntax-only, name-only, ambiguous,
   high fan-out, cross-source, or reduced-coverage THEN route-flow SHALL emit
   needs-review or unknown-gap rows rather than stronger conclusions.

### Requirement 4: Implementation Method To Downstream Evidence

**User Story:** As a reviewer, I want implementation methods on a route trace to
connect to further downstream calls and dependency/data surfaces when bounded
static evidence supports the connection.

#### Acceptance Criteria

1. WHEN a concrete implementation method is reached through direct call evidence
   or review-tier implementation-candidate evidence THEN route-flow SHALL search
   only within configured traversal bounds for further static call edges,
   parameter-forwarding edges, fact-symbol attachments, and dependency/data
   surfaces.
2. WHEN implementation method bodies contain direct downstream calls to
   repository-like, data-access, HTTP client, queue/event, storage,
   package/config, service, legacy-data, WCF, remoting, SQL/query, or generic
   dependency surfaces THEN route-flow SHALL emit rows only when those surfaces
   are connected by rule-backed evidence.
3. WHEN business/data logic facts attach to a method on the trace through
   credible symbol evidence THEN route-flow SHALL emit business/data logic rows
   adjacent to the relevant method.
4. WHEN logic facts are near a route trace but not directly connected by the
   composed evidence chain THEN route-flow SHALL label them as `path-context` or
   emit a bridge gap rather than presenting them as path edges.
5. WHEN dependency/data surfaces are reachable only through review-tier
   interface-candidate evidence THEN route-flow SHALL keep the surface row
   review-tier and preserve the implementation bridge limitation.
6. WHEN downstream evidence uses raw SQL, config values, URLs, source snippets,
   secrets, local absolute paths, raw remotes, or private identifiers THEN output
   SHALL omit, hash, or replace those values with safe descriptors.

### Requirement 5: Gap Reasons, Classification, And Downgrades

**User Story:** As a TraceMap user, I want route-flow gaps and classifications
to explain exactly which bridge is missing and why confidence was downgraded.

#### Acceptance Criteria

1. WHEN composition cannot start, continue, or attach a surface THEN route-flow
   SHALL emit a specific gap kind where possible. Existing gap codes such as
   `SelectorNoMatch`, `SchemaMissing`, `ExtractorUnavailable`,
   `ReducedCoverage`, `UnknownCommitSha`, and `TruncatedByLimit` SHALL be
   reused without renaming. New sibling gap codes MAY include
   `MissingRouteRoot`, `MissingMethodSymbolBridge`, `MissingCallEdge`,
   `MissingImplementationBridge`, `ImplementationCandidateUnavailable`,
   `AmbiguousImplementationCandidates`, `IdentityGap`, or `TraversalBounds`.
2. WHEN a gap is emitted THEN it SHALL include rule ID, evidence tier, source
   label where available, affected selector or row ID, supporting evidence IDs
   where available, file span where available, and a limitation.
3. WHEN implementation adds a new gap code THEN the code SHALL be documented in
   `combined.route-flow.gap.v1` with limitations before any report emits it.
4. WHEN multiple gap reasons apply THEN route-flow SHALL preserve all relevant
   rule-backed gap rows rather than collapsing them into a generic
   `UnknownAnalysisGap` unless schema or coverage prevents more precise
   analysis.
5. WHEN coverage is reduced, commit SHA is unknown, semantic project load
   failed, schema is missing, extractor versions are unavailable, or source
   identity is unverified THEN route-flow SHALL downgrade no-evidence and
   path-composition conclusions to `UnknownAnalysisGap` or
   `NeedsReviewStaticRouteFlow` as appropriate.
6. WHEN route-flow emits `NoRouteFlowEvidence` THEN it SHALL do so only after a
   selector matched, route-root and method-symbol bridge checks completed, the
   required evidence families were available, and coverage was sufficient for a
   clean absence conclusion.
7. WHEN a row combines multiple evidence tiers THEN the row classification SHALL
   be capped by the weakest required evidence and SHALL never upgrade original
   evidence tiers merely because the path composed.
8. Classification values SHALL remain the existing route-flow vocabulary:
   `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
   `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
   `UnknownAnalysisGap`.

### Requirement 6: Deterministic Markdown And JSON Output

**User Story:** As an automation author, I want route-flow output to be stable,
machine-readable, and public-safe.

#### Acceptance Criteria

1. WHEN endpoint composition runs against identical input rows and options THEN
   Markdown and JSON output SHALL be byte-stable, excluding external metadata
   already present in the index.
2. JSON output SHALL extend the existing `route-flow-report.json` contract
   backward-compatibly unless a future breaking-schema spec explicitly changes
   the report version. New row kinds and gap kinds SHALL be additive; existing
   gap kinds, classification values, field names, and report version SHALL NOT
   be renamed by this feature.
3. Markdown output SHALL keep the existing route-flow section style while
   clearly showing entry evidence, static flow rows, implementation candidates,
   business/data logic rows, dependency/data surfaces, gaps, limitations, and
   coverage labels.
4. Stable row IDs SHALL be derived from safe source labels, scan IDs where
   available, route root identity, method symbols, edge IDs, fact IDs, file
   paths, line spans, gap kinds, and rule IDs in deterministic order.
5. Rows SHALL sort deterministically by route selector, source label,
   classification rank, path length, row kind, safe display label, file path,
   start line, symbol ID, fact ID, edge ID, and stable row ID.
6. Missing values SHALL use explicit `null`, empty arrays, closed-set gap codes,
   or documented unavailable placeholders. Output SHALL NOT rely on omitted
   fields to communicate uncertainty.
7. Output SHALL include limitations that state route-flow rows are static
   evidence and not proof of runtime request execution, route reachability,
   deployment, traffic, auth behavior, dependency-injection binding, dynamic
   dispatch, branch feasibility, query execution, database schema state, or
   business impact.

### Requirement 7: Validation And Tests

**User Story:** As a maintainer, I want focused tests that prove endpoint
composition is useful, conservative, deterministic, and safe.

#### Acceptance Criteria

1. Tests SHALL cover route-root-to-method-symbol bridging with semantic evidence.
2. Tests SHALL cover route-root-to-method-symbol bridging with syntax fallback
   and classification downgrade.
3. Tests SHALL cover missing route root and missing method-symbol bridge gap
   reasons.
4. Tests SHALL cover endpoint method to direct downstream call edge composition.
5. Tests SHALL cover interface member call to implementation member bridge for
   single candidate, multiple candidates, no candidate, syntax-only candidate,
   and high fan-out cases.
6. Tests SHALL prove interface implementation bridges are review-tier/static and
   do not allow strong route-flow classifications by themselves.
7. Tests SHALL prove syntax/structural route-root to method-symbol bridges cap
   at `NeedsReviewStaticRouteFlow` even though other structural path evidence
   may support `ProbableStaticRouteFlow`.
8. Tests SHALL cover implementation method to downstream call edges and attached
   business/data logic rows.
9. Tests SHALL cover dependency/data surfaces reachable under bounded static
   evidence and unreachable surfaces that produce bridge gaps.
10. Tests SHALL cover reduced coverage, unknown commit SHA, missing schema,
   missing extractor, and traversal-bound downgrades.
11. Tests SHALL prove deterministic ordering and byte-stable JSON output for
    identical input.
12. Tests SHALL prove `NoRouteFlowEvidence` is emitted only when the selector
    matched, route-root and method-symbol bridge checks completed, required
    evidence families were available, and coverage was sufficient.
13. Tests SHALL prove composing mixed-tier edges never produces a row or summary
    classification stronger than the weakest required input evidence.
14. Tests SHALL prove existing route-flow JSON gap kinds and classification
    values remain backward-compatible, including preserving `SchemaMissing`
    rather than renaming it.
15. Tests SHALL prove near-but-unconnected logic is labeled as `path-context` or
    a bridge gap rather than rendered as a path edge.
16. Tests SHALL cover a direct concrete call edge alongside an interface
    candidate bridge, proving the direct edge can keep its own classification
    while the candidate row remains review-tier.
17. Tests SHALL prove multiple precise gaps are preserved when they apply and
    are not collapsed into a generic `UnknownAnalysisGap` unless schema or
    coverage prevents precise attribution.
18. Tests SHALL prove no private local paths, private repo names, exact private
    routes, raw source snippets, raw SQL/config values, raw remotes, URLs,
    connection strings, or secrets leak into Markdown, JSON, logs, or committed
    fixtures.
19. Tests SHALL prove every emitted rule ID resolves to the rule catalog and
    every new or extended rule documents limitations before implementation
    emits rows that cite it.
20. Tests SHALL prove no parallel `route.flow.*` rule namespace is introduced.
