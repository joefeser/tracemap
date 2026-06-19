# Route Flow Endpoint Composition Tasks

Status: spec-ready

Readiness: ready-for-implementation

## Spec Delivery Tasks

- [x] 1. Draft public-safe Kiro spec files for issue 201.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Keep the motivating observation generic.
  - [x] Avoid private local paths, private repo names, exact private routes,
        raw source snippets, raw SQL/config values, secrets, and raw remotes.

- [x] 2. Review the spec before opening the PR.
  - [x] Run Opus Kiro spec review when local Kiro tooling is available, or
        document the exact blocker in `implementation-state.md`.
  - [x] Run Sonnet Kiro spec review when local Kiro tooling is available, or
        document the exact blocker in `implementation-state.md`.
  - [x] Patch Medium+ or blocking review findings, or document that none were
        reported.
  - [x] Run a self-review focused on evidence boundaries, redaction,
        classification downgrades, and implementation feasibility.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run the private path guard if available.
  - [x] Confirm the diff is spec-only and does not touch static-site work.
  - [x] Record validation results in `implementation-state.md`.

## Implementation Tasks

Implementation branch status is tracked below. Completed checkboxes correspond
to code and focused tests landed on
`codex/implement-route-flow-endpoint-composition`; unchecked bullets remain
follow-up hardening or broader calibration work.

- [x] 4. Extend or confirm rule catalog support before emitting endpoint
      composition rows. Requirements: 5, 6.
  - [x] Treat current public route-flow gap values as stable. Reuse existing
        codes such as `SelectorNoMatch`, `SchemaMissing`, `ReducedCoverage`,
        `ExtractorUnavailable`, and `TruncatedByLimit`; do not rename them.
  - [x] Confirm `combined.route-flow.entry.v1` covers route-binding to endpoint
        method-symbol bridge rows, or extend its limitations.
  - [x] Confirm `combined.route-flow.path.v1` covers endpoint method to call
        edge rows and implementation method to downstream call rows.
  - [x] Confirm `combined.route-flow.interface-bridge.v1` covers static
        interface implementation candidates as review-tier evidence.
  - [x] Confirm `combined.route-flow.logic-surface.v1` and
        `combined.route-flow.dependency-surface.v1` cover attached
        business/data logic and reachable dependency/data surfaces.
  - [x] Extend `combined.route-flow.gap.v1` with any additive missing gap codes
        such as
        `MissingRouteRoot`, `MissingMethodSymbolBridge`, `MissingCallEdge`,
        `MissingImplementationBridge`, `AmbiguousImplementationCandidates`,
        `IdentityGap`, or `TraversalBounds` before code emits them.
  - [x] Add a catalog assertion proving every emitted endpoint-composition rule
        ID resolves and no parallel `route.flow.*` namespace is introduced.

- [x] 5. Implement route-binding to endpoint method-symbol bridge. Requirements:
      1, 5, 6.
  - [x] Select route roots from rule-backed endpoint route-binding evidence.
  - [x] Prefer semantic route-to-method symbol evidence.
  - [x] Add source-local syntax/structural fallback only when unambiguous and
        review-tier.
  - [x] Preserve `SelectorNoMatch` for plain selector misses; emit
        `MissingRouteRoot` only for the narrower endpoint route-root unavailable
        case, and emit `MissingMethodSymbolBridge` for route roots that cannot
        bridge to a source-local method symbol.
  - [x] Preserve source labels, scan IDs, commit SHAs, extractor identities,
        rule IDs, evidence tiers, fact IDs, symbol IDs, file paths, and line
        spans.
  - [ ] Add semantic, fallback, missing-root, ambiguous-root, and missing-symbol
        tests.
        Semantic, fallback, missing-root, and missing-symbol tests landed;
        ambiguous-root remains follow-up calibration.

- [x] 6. Compose endpoint method to direct downstream call edges. Requirements:
      2, 5, 6.
  - [x] Traverse source-local static call, object-creation context,
        argument-flow, parameter-forward, and dependency edges from endpoint
        method roots.
  - [x] Emit downstream method rows with supporting edge and fact provenance.
  - [x] Emit `MissingCallEdge` gaps when raw call evidence cannot be connected
        from the endpoint root.
  - [x] Add deterministic depth, path, frontier, row, and gap caps.
  - [x] Preserve existing route-flow default caps unless a separate contract
        change updates docs and deterministic output tests.
  - [ ] Add tests for direct downstream calls, unconnected raw call evidence,
        traversal caps, and deterministic ordering.
        Direct downstream, unconnected raw-call, and deterministic output tests
        landed; traversal-cap focused test remains follow-up.

- [x] 7. Bridge interface member calls to implementation member candidates.
      Requirements: 3, 5, 6.
  - [x] Preserve interface method calls as explicit route-flow rows.
  - [x] Read source-local implementation, inheritance, override, or equivalent
        relationship evidence.
  - [x] Emit zero, one, or many static implementation candidate rows.
  - [x] Cap candidate-dependent rows at `NeedsReviewStaticRouteFlow` or weaker.
  - [x] Continue traversal through candidates only as review-tier static
        candidate paths.
  - [x] Emit `MissingImplementationBridge`,
        `ImplementationCandidateUnavailable`, and
        `AmbiguousImplementationCandidates` gaps where appropriate.
  - [ ] Add tests for single candidate, multiple candidates, no candidate,
        syntax-only candidate, name-only candidate, high fan-out, and direct
        concrete call evidence alongside an interface bridge.
        Single-candidate, multiple-candidate, no-candidate, and direct concrete
        edge alongside interface bridge tests landed; syntax-only, name-only,
        and high-fan-out variants remain follow-up calibration.

- [x] 8. Compose implementation method to downstream calls and surfaces.
      Requirements: 4, 5, 6.
  - [x] Continue bounded traversal from concrete implementation methods.
  - [x] Attach direct downstream calls to repository-like, data-access, HTTP,
        queue/event, storage, package/config, service, legacy-data, WCF,
        remoting, SQL/query, or generic dependency surfaces.
  - [x] Attach business/data logic facts to methods on the route trace through
        credible fact-symbol or method-symbol evidence.
  - [x] Label near-but-unconnected logic as `path-context` or emit bridge gaps.
  - [x] Preserve implementation-candidate limitations on downstream rows reached
        through candidate bridges.
  - [ ] Add tests for reachable and unreachable dependency/data surfaces and
        method-attached business/data logic rows.
        Reachable surface and method-attached logic tests landed; unreachable
        surface variants remain follow-up.

- [x] 9. Implement classification, coverage, and gap downgrades. Requirements:
      1, 2, 3, 5.
  - [x] Keep classification values limited to `StrongStaticRouteFlow`,
        `ProbableStaticRouteFlow`, `NeedsReviewStaticRouteFlow`,
        `NoRouteFlowEvidence`, and `UnknownAnalysisGap`.
  - [x] Cap row and summary classification by weakest required evidence.
  - [x] Cap interface-candidate, syntax-only, textual, name-only, ambiguous,
        high-fan-out, generated-code uncertain, missing-extractor,
        reduced-coverage, and traversal-truncated paths.
  - [x] Require sufficient coverage before emitting `NoRouteFlowEvidence`.
  - [x] Emit precise gap rows before falling back to generic unknown gaps.
  - [ ] Add reduced coverage, unknown commit SHA, missing schema, missing
        extractor, identity gap, and traversal-bound tests.
        Reduced coverage, missing schema, missing extractor, and identity/gap
        coverage exist; explicit traversal-bound regression remains follow-up.

- [x] 10. Extend Markdown and JSON output safely. Requirements: 6, 7.
  - [x] Extend existing `route-flow-report.json` backward-compatibly unless a
        future breaking-schema spec changes the version.
  - [x] Render entry bridge, flow, implementation candidate, business/data
        logic, dependency/data surface, gap, coverage, and limitation rows in
        existing route-flow Markdown style.
  - [x] Generate stable row IDs and deterministic sorting.
  - [x] Use explicit `null`, empty arrays, closed-set gap codes, or unavailable
        placeholders for missing values.
  - [x] Add byte-stability tests for JSON and Markdown.

- [x] 11. Enforce privacy and redaction. Requirements: 4, 6, 7.
  - [x] Reuse shared safe path, hashing, display, and redaction helpers.
  - [x] Hash, omit, or safely describe raw SQL, config values, URLs, snippets,
        connection strings, secrets, exact private routes, local paths, private
        repo names, and raw remotes.
  - [x] Ensure logs do not echo unsafe selectors or display values.
  - [x] Cite `combined.route-flow.redaction.v1` when unsafe values are hashed or
        omitted.
  - [x] Add negative privacy tests for Markdown, JSON, logs, and committed
        fixtures.

- [x] 12. Validate implementation. Requirements: 7.
  - [x] Run focused route-flow endpoint composition tests.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln` when practical.
  - [x] Follow `docs/VALIDATION.md` for route-flow/reporting and any affected
        language-adapter checks.
  - [x] Run `git diff --check`.
  - [x] Run the private path guard if available.
  - [x] Run a public-safe CLI smoke against checked-in fixtures.
  - [x] If a private/local smoke is useful, keep outputs ignored and describe
        observations generically in implementation state.
        No private smoke was needed; the public `.tracemap-demo` endpoint-stack
        route-flow smoke passed and outputs stayed under ignored `.tmp/`.

## Suggested PR Boundaries

- PR 1: Rule catalog and route-binding to method-symbol bridge with precise
  start gaps. Apply redaction helpers to every emitted fixture/report value from
  the first row-producing PR; the broader Task 11 hardening can still finish in
  PR 4.
- PR 2: Endpoint method to call edge traversal and deterministic bounds.
- PR 3: Interface implementation candidate bridges and review-tier traversal.
- PR 4: Implementation method downstream traversal, business/data logic rows,
  dependency/data surfaces, output rendering, privacy guards, and smoke
  validation.
