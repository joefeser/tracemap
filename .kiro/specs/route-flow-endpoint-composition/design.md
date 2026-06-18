# Route Flow Endpoint Composition Design

## Overview

Extend `tracemap route-flow` so selected endpoint routes can compose existing
route-binding, method-symbol, call-edge, symbol-relationship, business/data
logic, and dependency/data evidence into one bounded static route trace.

The intended evidence chain is:

```text
HttpRouteBinding or equivalent endpoint route evidence
  -> endpoint method symbol
  -> direct static call edges from the endpoint method
  -> optional interface implementation candidate bridge
  -> implementation method call edges
  -> business/data logic facts attached to trace methods
  -> dependency/data surfaces reached under bounded static evidence
  -> precise gaps when a bridge or coverage prerequisite is missing
```

Every row is a static evidence statement. A composed route trace does not prove
runtime request execution, route reachability, deployment, traffic, auth
behavior, dependency-injection binding, dynamic dispatch, branch feasibility,
query execution, database schema state, data contents, or business impact.

## Goals

- Start route-flow traversal from route-binding evidence attached to a
  source-local endpoint method symbol.
- Compose endpoint methods to downstream static call edges without global
  short-name stitching.
- Expose interface-to-implementation relationships as review-tier static
  candidate bridges, not runtime DI proof.
- Continue from implementation methods to further static calls and reachable
  dependency/data surfaces within deterministic bounds.
- Attach business/data logic rows to methods along the route trace when
  credible fact-symbol or method-symbol evidence exists.
- Emit specific gap reasons for missing route roots, method-symbol bridges,
  call edges, implementation bridges, reduced coverage, identity gaps, and
  traversal bounds.
- Preserve source labels, scan IDs, commit SHAs, extractor identities, rule IDs,
  evidence tiers, file paths, line spans, supporting fact IDs, supporting edge
  IDs, limitations, and coverage labels.
- Keep Markdown and JSON output deterministic and public-safe.

## Non-Goals

- No runtime code implementation in this spec branch.
- No scanner rewrite as part of the spec.
- No mutation of input SQLite indexes.
- No runtime request execution, API probing, service hosting, browser
  automation, database connections, schema introspection, or telemetry
  ingestion.
- No runtime dependency-injection resolution, service locator evaluation,
  factory execution, reflection target proof, dynamic dispatch certainty,
  serializer runtime mapping proof, generated-code freshness proof, or branch
  feasibility analysis.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  AI impact analysis in scanner, reducer, route-flow, paths, or reporting code.
- No source snippets, raw SQL, raw config values, raw URLs, connection strings,
  raw remotes, local absolute paths, exact private routes, private repository
  names, private sample names, or secrets in committed specs, fixtures, reports,
  PR text, or logs.

## Relationship To Existing Specs And Code

This spec is a follow-on to existing route-flow work:

- `route-centered-static-flow-report` established `tracemap route-flow`, the
  route-flow report envelope, classifications, and core rule family.
- `route-flow-service-data-composition` added projection work for
  `combined_argument_flows` and `combined_fact_symbols` and left route-root
  bridge expansion, interface candidates, and downstream traversal as follow-up
  work.
- This spec focuses on the endpoint-root composition gap from issue `#201`:
  route binding to method symbol, method to call edge, interface call to
  implementation candidate, implementation method to downstream evidence, and
  precise gap semantics.

The implementation should extend the existing `CombinedRouteFlowReporter` and
route-flow model rather than adding a second route-flow command, schema,
classification vocabulary, or rule namespace. The public CLI remains:

```text
tracemap route-flow --index <combined.sqlite> --out <path> [selectors] [options]
```

Any overlap with `tracemap paths --from-endpoint` should delegate to the same
composition semantics or keep route-flow-specific behavior documented as a
route-flow report concern.

## Current Baseline

The current `CombinedRouteFlowReporter` already implements the route-flow report
envelope, route-flow classifications, bounded path rows, interface
implementation candidate rows, projection rows for `combined_argument_flows` and
`combined_fact_symbols`, deterministic output fields, and existing gap codes
such as `SelectorNoMatch`, `SchemaMissing`, `ReducedCoverage`,
`ExtractorUnavailable`, `ImplementationCandidateUnavailable`,
`RuntimeBindingNotProven`, and `TruncatedByLimit`.

This spec extends that baseline. It should not rebuild those features or rename
their public JSON values. New work is limited to endpoint route-root to
method-symbol bridge expansion, direct endpoint method call-edge composition,
clearer missing bridge gaps, implementation-method downstream traversal, and
business/data logic or dependency/data surface attachment where those rows are
not already reachable from the selected route trace.

## Input Evidence

The composer reads existing combined-index evidence read-only and tolerates
missing optional tables or older schemas.

| Input | Role |
| --- | --- |
| Endpoint route facts | Route roots and route-binding metadata |
| `combined_symbols` | Source-local endpoint, interface, implementation, and downstream method symbols |
| `combined_fact_symbols` | Fact-to-symbol attachments for route facts, logic facts, and surface facts |
| `combined_call_edges` | Direct method-to-method static calls |
| `combined_dependency_edges` | Shared path graph edge summary where it preserves provenance; when a dependency edge and precise edge share the same source-local caller/callee pair, prefer the higher-provenance precise edge and cite both supporting IDs rather than double-emitting rows |
| `combined_object_creations` | Constructor/type context, never runtime object lifetime proof |
| `combined_parameter_forward_edges` | Parameter-forwarding bridge evidence where available |
| `combined_argument_flows` | Direct argument details already projected by route-flow rules |
| `combined_symbol_relationships` | Implements, inherits, overrides, and equivalent relationship evidence |
| `combined_facts` | Query, object/projection shape, dependency/data, business-boundary, and gap facts |
| `index_sources` and manifests | Source labels, scan IDs, commit SHA, language, coverage, build status, and extractor identity |
| Existing path/reverse inventory | Reusable graph primitives and surface projections |
| Rule catalog | Rule descriptions and limitations for every emitted row |

The composer does not read source files, run extractors, execute target code, or
connect to external systems.

## Composition Model

### Nodes

- `RouteRoot`
- `EndpointMethod`
- `CallSite`
- `InterfaceMethod`
- `ImplementationCandidate`
- `ImplementationMethod`
- `DownstreamMethod`
- `BusinessDataLogic`
- `DependencyDataSurface`
- `Gap`

These node kinds are labels over one route-flow traversal, not separate graph
engines. `DownstreamMethod` means any non-root concrete method reached by a
composed edge; endpoint, implementation, and downstream labels describe the
method's role in the trace.

### Edges

- `route-bound-to-symbol`
- `calls`
- `creates-context`
- `argument-passed`
- `parameter-forwarded`
- `fact-attached-to-symbol`
- `implements`
- `inherits`
- `overrides`
- `surface-attached`
- `gap`

Each node and edge carries source label, source index ID, scan ID, commit SHA,
extractor name/version, rule ID, evidence tier, supporting fact IDs, supporting
edge IDs, symbol IDs, file path, line span, coverage label, and limitations
where available.

## Composition Algorithm

1. Open the combined index read-only and verify it is a combined index.
2. Resolve the route selector using existing route-flow selector semantics.
3. Load route-binding evidence for matched endpoint roots.
4. Build a source-local route-to-method-symbol bridge from semantic symbol
   evidence first, then documented syntax/structural fallback only when the
   fallback is unambiguous within the same source and does not use global
   short-name stitching.
5. Preserve `SelectorNoMatch` for plain selector misses. Emit additive
   `MissingRouteRoot` only when selector context exists but endpoint route-root
   evidence needed for composition is unavailable. Emit
   `MissingMethodSymbolBridge` when the trace cannot start because the route
   root cannot bridge to a source-local method symbol.
6. Traverse outgoing static call, creation-context, argument-flow,
   parameter-forwarding, and dependency edges from endpoint method symbols,
   within max-depth, max-path, max-frontier, max-row, and max-gap caps.
   Downstream methods are the concrete method nodes reached by these edges;
   they are not a separate traversal mode from endpoint or implementation
   method nodes.
7. When a call targets an interface member, preserve the interface call row and
   search source-local relationship evidence for implementation candidates.
8. Emit implementation candidate rows using
   `combined.route-flow.interface-bridge.v1`; continue traversal through those
   candidates only with a needs-review classification cap.
9. Continue traversal from concrete implementation methods through further
   static call edges and surface attachments.
10. Attach business/data logic rows to trace methods through credible
    source-local fact-symbol or method-symbol evidence.
11. Attach dependency/data surface rows only when the surface is reachable under
    the composed static evidence chain.
12. Emit precise gaps for missing call edges, missing implementation bridges,
    reduced coverage, schema gaps, extractor gaps, identity gaps, and traversal
    bounds.
13. Compute row and summary classifications from the weakest required evidence,
    coverage, gap state, and traversal caps.
14. Render deterministic Markdown and JSON using safe labels, hashes,
    repo-relative paths, explicit unavailable placeholders, and limitations.

## Interface Candidate Handling

Interface calls are explicit stop-or-expand points, not runtime binding proof.

The composer should:

- keep the interface call as a normal static call row;
- find zero, one, or many source-local implementation candidates using
  compiler-backed or documented structural relationship evidence;
- cite the relationship fact or edge on every candidate row;
- continue downstream traversal through candidates only as static candidates;
- cap candidate-dependent rows and surfaces at `NeedsReviewStaticRouteFlow` or
  weaker;
- emit `ImplementationCandidateUnavailable` or `MissingImplementationBridge`
  when no candidate exists;
- emit an ambiguity or high-fan-out gap when candidates exceed useful review
  bounds;
- avoid wording such as "resolved implementation", "runtime target", "bound
  service", or "selected dependency".

If a direct concrete call edge exists independently of the interface bridge,
route-flow may classify that direct edge according to its own evidence while
still keeping the interface candidate row review-tier.

## Gap Taxonomy

Gap rows are emitted through `combined.route-flow.gap.v1`. Existing public gap
codes are reused without renaming. New gap codes are additive only; if the
current rule catalog does not document a gap code before implementation starts,
the first implementation PR must extend that rule entry and limitations before
emitting the code.

| Gap kind | Meaning |
| --- | --- |
| `SelectorNoMatch` | Existing selector miss when the route selector does not match supported entry evidence |
| `MissingRouteRoot` | Additive sibling gap when selector context exists but endpoint route-root evidence is unavailable |
| `MissingMethodSymbolBridge` | Route evidence exists but cannot be tied to a source-local endpoint method symbol |
| `MissingCallEdge` | Downstream call evidence cannot be connected from the current method under static rules |
| `MissingImplementationBridge` | Interface call exists but no static implementation relationship can bridge it |
| `ImplementationCandidateUnavailable` | No candidate implementation is available for a reached interface member |
| `AmbiguousImplementationCandidates` | Candidate set is ambiguous or high fan-out |
| `ReducedCoverage` | Build, semantic, source identity, extractor, or scan coverage prevents clean conclusions |
| `SchemaMissing` | Existing code for required combined schema that is absent or incompatible |
| `ExtractorUnavailable` | Needed extractor family is unavailable or not recorded |
| `IdentityGap` | Source, symbol, route, or surface identity is too weak for the requested join |
| `TraversalBounds` | Traversal stopped before graph exploration finished because max depth, path count, or frontier caps were hit |
| `TruncatedByLimit` | Existing code for output that is partial because rendered rows or gaps were capped |

Gaps should be as specific as available evidence allows. A generic
`UnknownAnalysisGap` summary remains appropriate when coverage or schema state
prevents reliable gap attribution.
Existing route-flow gap codes not listed in this endpoint-composition taxonomy
remain emitted unchanged, including runtime-binding, dynamic-dispatch,
projection, unknown-commit, and unsafe-value gaps.

## Classification Semantics

Use the existing route-flow vocabulary:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

Classification rules:

- Semantic route root, method-symbol bridge, call edge, and terminal surface
  evidence under full route-flow coverage may support
  `StrongStaticRouteFlow`.
- Structural evidence can support no stronger than `ProbableStaticRouteFlow`.
- Structural-only or syntax fallback route-root to method-symbol bridges are a
  stricter case and cap at `NeedsReviewStaticRouteFlow` or weaker because they
  establish the trace root without compiler-resolved symbol evidence.
- Syntax-only, textual, name-only, ambiguous, high-fan-out,
  interface-candidate, generated-code uncertain, missing-extractor, or
  reduced-coverage evidence can support no stronger than
  `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.
- Interface-candidate bridges always cap candidate-dependent rows at
  `NeedsReviewStaticRouteFlow` or weaker.
- `NoRouteFlowEvidence` requires a matched selector, a completed route-root to
  method-symbol bridge check, relevant evidence families available, known source
  identity, and sufficient coverage for clean absence.
- Unknown commit SHA, schema gaps, source identity gaps, failed project load,
  reduced semantic coverage, missing extractors, or traversal truncation prevent
  clean absence conclusions.
- Composed rows never upgrade source evidence tiers.
- `UnknownAnalysisGap` is a classification value on rows or summaries. It is
  distinct from a gap row's specific gap kind; a gap row may carry
  `UnknownAnalysisGap` classification when precise attribution is unavailable.

## Rule IDs And Limitations

Reuse existing rules wherever possible:

| Behavior | Rule handling |
| --- | --- |
| selector handling | `combined.route-flow.selector.v1` |
| route entry and endpoint method bridge | `combined.route-flow.entry.v1` |
| endpoint method and downstream call rows | `combined.route-flow.path.v1` |
| interface implementation candidates | `combined.route-flow.interface-bridge.v1` |
| business/data logic rows | `combined.route-flow.logic-surface.v1` |
| dependency/data surface rows | `combined.route-flow.dependency-surface.v1` |
| argument-flow detail rows | `combined.route-flow.argument-projection.v1` |
| fact-symbol detail rows | `combined.route-flow.fact-symbol-projection.v1` |
| summary and row classification | `combined.route-flow.classification.v1` |
| gap rows | `combined.route-flow.gap.v1` |
| redaction | `combined.route-flow.redaction.v1` |
| Markdown/JSON report envelope | `combined.route-flow.report.v1` |

No implementation may emit a conclusion without a rule ID. No new rule ID may
be emitted before it exists in `rules/rule-catalog.yml` with documented
limitations. This spec does not introduce a parallel `route.flow.*` family.
If a new gap kind is needed, implementation must update
`combined.route-flow.gap.v1` first and keep existing public gap values such as
`SelectorNoMatch` and `SchemaMissing` unchanged.

Required limitations:

- Route-flow rows are static evidence, not runtime execution or reachability
  proof.
- Route evidence does not prove deployment, auth, middleware, proxy, CORS, host
  activation, or production traffic.
- Call edges can miss reflection, dynamic dispatch, delegates, generated code,
  partial methods, dependency injection behavior, and branch feasibility.
- Interface bridge rows are static implementation candidates, not runtime
  dependency-injection target proof.
- Object creation evidence is context, not object lifetime or receiver identity
  proof.
- Query/data rows do not prove SQL execution, database existence, schema
  compatibility, permissions, persistence, generated SQL equivalence, or data
  contents.
- Business/data logic rows are review context, not proof that a branch executes
  or that business impact exists.
- Reduced coverage, missing extractors, schema gaps, unknown commit SHA,
  identity gaps, and traversal caps limit confidence.
- Unsafe values are omitted, hashed, or replaced with safe descriptors.

## Output Contract

Extend the existing route-flow report shape backward-compatibly. The report type
remains `route-flow`; the current report version should remain unchanged unless
a future spec defines a breaking schema migration.

Endpoint composition rows may use existing collections when they preserve
provenance:

- entry evidence for route roots and method-symbol bridges;
- flow rows for endpoint method calls, interface calls, implementation
  candidates, and downstream calls;
- logic rows for business/data logic, argument-flow, fact-symbol, object-shape,
  query-shape, validation, branch, async, and boundary evidence;
- dependency surfaces for terminal dependency/data surfaces;
- gaps for bridge failures, coverage gaps, schema gaps, extractor gaps,
  identity gaps, and traversal caps.

Add a new optional detail collection only if existing collections cannot
represent endpoint-composition provenance without losing rule IDs, evidence
tiers, supporting IDs, file spans, or limitations.

Every row should include:

- stable row ID;
- row kind;
- classification;
- safe display label;
- source label;
- scan ID;
- commit SHA or unavailable placeholder;
- extractor identity where available;
- evidence tier;
- primary rule ID and supporting rule IDs;
- supporting fact IDs;
- supporting edge IDs;
- relevant symbol IDs;
- repo-relative or synthetic file path;
- line span or explicit unavailable placeholder;
- coverage label;
- limitation or gap reason.

Markdown should preserve the existing route-flow sections and add endpoint
composition details under narrowly named sections or subsections such as Static
Flow, Implementation Candidates, Business/Data Logic, Dependency/Data Surfaces,
Gaps, and Limitations.

## Traversal Defaults

Endpoint composition uses the existing route-flow defaults unless a later spec
or implementation task explicitly changes the CLI contract:

| Cap | Default |
| --- | --- |
| `--max-depth` | `8` |
| `--max-paths` | `100` |
| `--max-frontier` | `10000` |
| `--max-logic-rows` | `200` |
| `--max-gaps` | `1000` |

Changing these defaults is a report-contract change and must include
deterministic output regression tests.

## Determinism And Safety

Rows must sort by route selector, source label, classification rank, path length,
row kind, safe display label, file path, start line, symbol ID, fact ID, edge
ID, and stable row ID. Stable row IDs must use only safe, deterministic inputs
such as safe source labels, scan IDs where available, route root identity,
method symbol IDs, edge IDs, fact IDs, file paths, line spans, gap kinds, and
rule IDs.

Outputs must not include:

- private local paths;
- private repository names;
- exact private sample routes;
- raw source snippets;
- raw SQL;
- raw config values;
- raw URLs or endpoint URLs;
- raw remotes;
- connection strings;
- secrets;
- unstable absolute output paths.

When unsafe values are encountered, render safe labels, hashes, descriptors, or
explicit redaction notes and cite `combined.route-flow.redaction.v1`.

## Validation Plan

Implementation should include focused tests for:

- semantic route-root to endpoint method-symbol bridge;
- syntax fallback route-root bridge downgrade;
- structural route-root bridge capped at `NeedsReviewStaticRouteFlow`;
- missing route root;
- missing method-symbol bridge;
- endpoint method to direct downstream call edge;
- interface implementation bridge with single candidate, multiple candidates,
  no candidate, syntax-only candidate, and high fan-out;
- interface bridge classification cap;
- direct concrete call evidence alongside an interface candidate bridge;
- implementation method to downstream call edge;
- business/data logic rows attached to methods on the route trace;
- near-but-unconnected logic labeled as `path-context` or a bridge gap;
- dependency/data surfaces reachable under bounded static evidence;
- unreachable downstream surfaces producing bridge gaps;
- reduced coverage, unknown commit SHA, missing schema, extractor gaps, identity
  gaps, and traversal-bound downgrades;
- `NoRouteFlowEvidence` preconditions under sufficient coverage;
- mixed-tier composition never upgrading the weakest required evidence;
- backward-compatible route-flow JSON gap kind and classification values,
  including existing `SchemaMissing`;
- multiple precise gaps preserved without generic gap collapse when attribution
  is possible;
- deterministic ordering and byte-stable JSON;
- Markdown, JSON, logs, and committed fixture privacy guards;
- rule catalog resolution for every emitted rule ID.
- assertion that no parallel `route.flow.*` rule namespace is introduced.

Repo validation should follow `docs/VALIDATION.md` for language-adapter or
route-flow/reporting changes. At minimum the implementation branch should run
focused route-flow tests, full .NET tests when practical, `git diff --check`,
and the private path guard.
