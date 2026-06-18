# Route Flow Service/Data Composition Design

## Overview

Add a conservative extension to the existing `tracemap route-flow` report over
combined TraceMap evidence. The extension starts from route entry evidence that
is already attached to a controller or handler method, projects usable details
from `combined_argument_flows` and `combined_fact_symbols`, and emits downstream
static evidence rows for methods, object shapes, query shapes,
repository-like/data-access evidence, and data surfaces when existing facts
support the bridge.

Intended evidence chain:

```text
route entry evidence
  -> controller or handler method symbol
  -> static call, creation, argument-flow, parameter-forward, or symbol edge
  -> service, repository, or data-access method symbol
  -> object-shape, query-shape, dependency-surface, or data-surface fact
  -> route-flow report rows with rule IDs, evidence tiers, coverage, and gaps
```

Every row is a static evidence statement. The report must not claim that a
request executed, a route is deployed or reachable, a dependency injection
container selected an implementation, a branch is feasible, SQL executed, data
exists, or a database schema is present.

## Goals

- Make route-flow output useful when route entry evidence exists but downstream
  details are currently unavailable.
- Project detail from `combined_argument_flows` and `combined_fact_symbols` into
  route-flow rows.
- Compose route-to-controller-to-service-to-repository/data evidence using
  deterministic static facts.
- Expose interface implementation candidates without claiming runtime binding.
- Preserve rule IDs, evidence tiers, supporting fact/edge IDs, file paths, line
  spans, commit SHA, extractor versions, and coverage labels.
- Emit narrower gaps when a specific bridge or projection is unavailable.
- Keep Markdown, JSON, and logs public-safe by default.

## Non-Goals

- No scanner rewrite in this spec.
- No runtime request execution, hosting, route probing, browser automation, or
  API calls.
- No runtime dependency injection, service locator, container registration, or
  configuration binding resolution.
- No dynamic dispatch, reflection, serializer, branch feasibility, auth,
  middleware, deployment, or database existence proof.
- No whole-program taint analysis or mutation tracking.
- No raw snippets, raw SQL, config values, connection strings, private routes,
  raw URLs, raw remotes, private sample names, or local absolute paths in public
  outputs.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  the TraceMap core.

## Relationship To Existing Route-Flow Reporting

This spec extends `CombinedRouteFlowReporter` and the existing
`route-centered-static-flow-report` design. It does not replace the route-flow
command, schema, rule namespace, or classification vocabulary.

The public CLI remains:

```text
tracemap route-flow --index <combined.sqlite> --out <path> [selectors] [options]
```

Existing selectors, caps, output behavior, and file names remain unchanged:
directory outputs write `route-flow-report.md` and `route-flow-report.json`;
file outputs default to Markdown unless `--format json` is requested.

The implementation path is:

1. Reuse combined index readers and path graph primitives where they already
   preserve provenance and deterministic ordering.
2. Extend `CombinedRouteFlowReporter` rather than adding a second traversal
   engine or parallel route-flow schema.
3. Preserve the existing `RouteFlowClassifications` values:
   `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
   `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
   `UnknownAnalysisGap`.
4. Preserve the existing `route-flow-report.json` report type and version for
   backward-compatible additions. A new version is required only for a future
   breaking schema change.
5. Keep existing combined path behavior compatible, but route-flow-specific
   output should be owned by `CombinedRouteFlowReporter`.

Route-flow composition must not mutate input SQLite files and should not persist
derived rows unless a later spec defines storage ownership.

## Input Evidence

Read optional evidence from combined SQLite tables and fact properties:

| Input | Role |
| --- | --- |
| Route entry facts | Root evidence for normalized API routes and attached controller/handler methods |
| `combined_symbols` | Source-local method/type symbols and display identities |
| `combined_call_edges` | Direct static method-to-method calls |
| `combined_object_creations` | Constructor/type creation evidence that may bridge controller, service, and repository shapes |
| `combined_argument_flows` | Direct argument evidence projected as detail rows, not full taint |
| `combined_parameter_forward_edges` | Derived parameter forwarding when available |
| `combined_fact_symbols` | Linking rows from facts to symbols; evidence tier, rule ID, file path, and line span come from joining to `combined_facts` |
| `combined_symbol_relationships` | Implements/inherits/overrides relationships for static implementation candidates |
| `combined_facts` with `FactTypes.ObjectShapeInferred` | Object-shape detail evidence |
| `combined_facts` with `FactTypes.QueryPatternDetected` or successor query-shape fact types | Query-shape detail evidence |
| `combined_dependency_edges` and dependency/data facts in `combined_facts` | Terminal dependency/data surface evidence |
| Coverage and gap facts | Coverage caps, availability gaps, and explanation rows |

Readers must tolerate missing tables and columns. A missing optional family
becomes an availability gap. A missing required route root bridge becomes a
specific gap rather than an empty success.

## Graph And Projection Model

The composer builds a bounded in-memory graph from existing combined evidence.
Nodes should include:

- `RouteEntry`
- `ControllerMethod`
- `ServiceMethod`
- `InterfaceMethod`
- `ImplementationCandidate`
- `RepositoryMethod`
- `DataAccessMethod`
- `ObjectShape`
- `QueryShape`
- `DataSurface`
- `Gap`

Edges should include:

- `route-attached-to-symbol`
- `calls`
- `creates`
- `argument-passed`
- `parameter-forward`
- `fact-attached-to-symbol`
- `implements`
- `inherits`
- `overrides`
- `surface-evidence`
- `gap`

Each node and edge carries source label, source index ID, fact ID, edge ID,
symbol ID, rule ID, evidence tier, safe file path, line span, scan ID, commit
SHA, extractor identity, and coverage where available.

`creates` edges connect a method node to a created type or constructor context.
They can support downstream service/repository-like traversal only when a
subsequent call, constructor symbol, method symbol, or fact-symbol attachment
ties the created type back to a concrete symbol. A raw type creation alone is
context evidence, not proof that a downstream method or data surface is reached.

`combined_argument_flows` projection:

- Attach argument-flow rows to the route-flow path when source and target
  symbols are already connected by a credible call or parameter-forward edge.
- Render safe parameter names, positions, type descriptors, or hashes only.
- Do not render literal values or source expressions.
- Do not infer full value flow through mutation, aliasing, branches, dynamic
  dispatch, serialization, or repository translation.
- Suppress the current present-but-unprojected `ExtractorUnavailable` gap for
  `combined_argument_flows` only for the selected route root/table when at
  least one argument-flow projection row is emitted. If rows exist but cannot be
  joined credibly for that route root, preserve a scoped gap such as
  `ArgumentProjectionUnavailable`.

`combined_fact_symbols` projection:

- Attach object-shape, query-shape, dependency-surface, and data-surface facts
  to route-flow methods when the fact-symbol attachment is source-local and
  rule-backed.
- Treat `combined_fact_symbols` as a linking table only. Join
  `combined_fact_symbols.combined_fact_id` to `combined_facts` to recover fact
  type, rule ID, evidence tier, file path, line span, commit/source context, and
  safe display metadata.
- Prefer explicit symbol IDs. Fall back to source-local normalized symbol keys
  only when the existing path/reporting layer already treats that fallback as
  credible and marks ambiguity. A source-local normalized symbol key means the
  same source index plus the same normalized `combined_symbols.display_name` or
  equivalent existing reporter key; ambiguous matches remain review-tier.
- Do not stitch facts across sources by global short names.
- Preserve all supporting fact IDs when deduplicating logical rows.
- Suppress the current present-but-unprojected `ExtractorUnavailable` gap for
  `combined_fact_symbols` only for the selected route root/table when at least
  one fact-symbol projection row is emitted. If rows exist but cannot be joined
  credibly for that route root, preserve a scoped gap such as
  `FactSymbolProjectionUnavailable`.

## Interface Candidate Handling

Interface calls are represented as a stop or candidate expansion, not runtime
binding proof.

When a call edge targets an interface method:

1. Keep the interface call as its own evidence row.
2. Search source-local `implements`, `inherits`, override, and equivalent
   symbol relationship facts for candidate implementation methods.
3. Emit zero, one, or many `ImplementationCandidate` rows.
4. Continue downstream traversal through candidates only as static candidates,
   carrying an interface-candidate cap.
5. If direct call evidence to a concrete implementation also exists, represent
   that as a separate stronger edge and deduplicate shared terminal evidence by
   supporting IDs.

Candidate wording:

- Use "static implementation candidate".
- Do not use "bound implementation", "resolved implementation", "runtime
  target", or similar runtime-binding language.
- If candidates are ambiguous, name-only, syntax-only, or high fan-out, classify
  the row as needs-review or unknown-gap.

## Classification

Use the existing `RouteFlowClassifications` vocabulary:

| Classification | Meaning |
| --- | --- |
| `StrongStaticRouteFlow` | Route root to downstream detail is connected by semantic or equivalently strong static evidence with full route-flow coverage |
| `ProbableStaticRouteFlow` | Credible structural evidence connects the row, but at least one semantic link is unavailable |
| `NeedsReviewStaticRouteFlow` | Syntax-only, name-only, ambiguous, high fan-out, interface candidate, generated-code uncertainty, or partial terminal evidence is involved |
| `NoRouteFlowEvidence` | Selectors matched and relevant coverage is complete, but no route-flow evidence was found |
| `UnknownAnalysisGap` | Missing schema, unavailable extractor, unsupported framework behavior, or an explicit `AnalysisGap` prevents a conclusion |

Classification is capped by the weakest required evidence and by coverage.
Interface candidate expansion, syntax-only fallback, name-only matching,
ambiguous receiver/symbol links, generic high-fan-out terminal names, reduced
coverage, or missing extractor availability cannot produce the strongest
classification.

Reduced coverage is a report coverage label and gap reason, not a separate
summary classification. The v1 high fan-out threshold follows
`combined.route-flow.classification.v1`: 10 or more candidates, or more than one
candidate for generic terminal keys such as `status`, `id`, `name`, `value`,
`result`, or `response`.

## Rule IDs And Limitations

Reuse existing route-flow rules wherever they already cover the behavior:

| Behavior | Rule handling |
| --- | --- |
| selector handling | reuse `combined.route-flow.selector.v1` |
| route entry rows | reuse `combined.route-flow.entry.v1` |
| static path rows | reuse `combined.route-flow.path.v1` |
| interface candidates | reuse `combined.route-flow.interface-bridge.v1` |
| object/query/business/data logic rows | reuse `combined.route-flow.logic-surface.v1` |
| terminal dependency/data surfaces | reuse `combined.route-flow.dependency-surface.v1` |
| summary classification | reuse `combined.route-flow.classification.v1` |
| existing gap codes | reuse `combined.route-flow.gap.v1` |
| redaction | reuse `combined.route-flow.redaction.v1` |
| report envelope | reuse `combined.route-flow.report.v1` |
| argument-flow detail projection | add `combined.route-flow.argument-projection.v1` |
| fact-symbol detail projection | add `combined.route-flow.fact-symbol-projection.v1` |

Do not add a parallel `route.flow.*` rule family. Task 4 decides the catalog
surface before implementation starts; the rule-resolution test must pass whether
a row uses reused route-flow rules or the new projection rules.

Required limitations:

- Static route-flow composition does not prove runtime request execution,
  route deployment, route reachability, traffic, auth behavior, middleware
  behavior, or production use.
- Call edges do not prove dynamic dispatch, runtime dependency injection,
  reflection targets, branch feasibility, collection contents, serializer
  behavior, or generated code behavior.
- Interface candidate rows do not prove runtime binding or selected
  implementation.
- Argument-flow rows are direct static argument evidence, not full taint,
  alias, mutation, or value-origin proof.
- Query/data rows do not prove SQL execution, database existence, schema
  compatibility, permissions, generated SQL equivalence, or data contents.
- Reduced coverage and missing extractors cap confidence.
- Unsafe values are hashed, omitted, or represented by safe descriptors.

## Output Shape

Extend the existing `RouteFlowReport` shape emitted by
`CombinedRouteFlowReporter`. New rows should be represented as compatible
additions to existing `FlowRows`, `LogicRows`, `DependencySurfaces`, or `Gaps`
where those collections can preserve the evidence. Add a new detail collection
only if the existing collections cannot represent the row without losing
provenance; if that is required, keep it optional and backward-compatible.

Detail row kinds:

- `DownstreamMethod`
- `ArgumentFlow`
- `ObjectShape`
- `QueryShape`
- `RepositoryEvidence`
- `DataSurface`
- `ImplementationCandidate`
- `Gap`

Markdown should align with the existing route-flow renderer. New downstream
evidence, candidate implementation, and projection rows may be added to the
nearest existing sections or to narrowly named subsections, but the implementation
must preserve existing section compatibility tested by current route-flow tests.

Every row should include:

- safe display label;
- source label;
- evidence tier;
- rule ID;
- supporting fact IDs;
- supporting edge IDs where applicable;
- safe file path and line span where available;
- coverage label;
- limitation or gap code when relevant.

## Determinism

Traversal should be deterministic breadth-first search or another documented
stable algorithm. Defaults should align with existing path limits where
possible. Suggested sorting:

1. classification rank;
2. route safe key;
3. source label;
4. path length;
5. detail row kind;
6. downstream safe display label;
7. file path;
8. start line;
9. stable symbol, fact, edge, and row IDs.

Stable IDs should be hashes over ordered safe inputs only: source labels, route
root identity, symbol IDs, fact IDs, edge IDs, safe file paths, line spans, and
row kind. Do not include timestamps, local absolute paths, raw route strings,
raw SQL, raw config values, raw URLs, or raw remotes.

Stable ID tuples by new row kind:

| Row kind | Ordered key tuple |
| --- | --- |
| `DownstreamMethod` | source label, route root safe key, method symbol ID, ordered set of all supporting edge IDs for the row, rule ID, file path, start line |
| `ArgumentFlow` | source label, route root safe key, call edge ID, argument-flow row ID when present, otherwise ordered supporting fact IDs, parameter index/name hash, rule ID, file path, start line |
| `ObjectShape` | source label, route root safe key, attached method symbol ID, object-shape fact ID, safe shape key/hash, rule ID, file path, start line |
| `QueryShape` | source label, route root safe key, attached method symbol ID, query fact ID, safe query-shape hash, rule ID, file path, start line |
| `RepositoryEvidence` | source label, route root safe key, repository-like method symbol ID, supporting edge IDs, supporting fact IDs, rule ID, file path, start line |
| `DataSurface` | source label, route root safe key, attached method symbol ID, dependency/data fact ID, safe surface key/hash, rule ID, file path, start line |
| `ImplementationCandidate` | source label, route root safe key, interface symbol ID, candidate symbol ID, relationship fact or edge ID, rule ID, file path, start line |
| `AmbiguousImplementationCandidates` gap | source label, route root safe key, interface symbol ID, sorted candidate symbol IDs, sorted relationship fact or edge IDs, rule ID |

## Gap Semantics

Emit narrow gaps whenever possible:

| Gap | Status | When |
| --- | --- | --- |
| `ArgumentProjectionUnavailable` | new | `combined_argument_flows` are missing or cannot be joined safely |
| `FactSymbolProjectionUnavailable` | new | `combined_fact_symbols` are missing or cannot be joined safely |
| `ControllerToServiceBridgeMissing` | new | Route root exists but no credible service/repository bridge is available |
| `ImplementationCandidateUnavailable` | existing | Interface call exists but static candidates are absent |
| `AmbiguousImplementationCandidates` | new | Multiple candidates or high fan-out prevent stronger classification |
| `DataSurfaceAttachmentMissing` | new | Downstream method exists but no credible data-surface attachment exists |
| `ExtractorUnavailable` | existing | Relevant extractor family is absent or predates the needed facts |
| `SchemaMissing` | existing | Required table or column is absent |
| `ReducedCoverage` | existing | Scan/build/semantic coverage caps conclusions |
| `NoRouteFlowEvidence` | existing | Entry evidence matched but no route-flow path or terminal surface exists under full available coverage |
| `UnknownAnalysisGap` | existing | Gaps prevent a clean route-flow conclusion |
| `UnsafeValueOmitted` | existing | A value was omitted or hashed for public-safe output |

No gap should be emitted without a rule ID and supporting context. Extend the
`combined.route-flow.gap.v1` `emits` list for new gap codes before code emits
them.

## Privacy And Redaction

Render safe fields only:

- repo-relative or synthetic fixture paths;
- normalized route keys or route hashes;
- safe symbol display names when they are not private sample identifiers;
- query shape descriptors and hashes, not raw SQL;
- config key categories or hashes, not values;
- source labels that have been neutralized or reviewed;
- closed-set reason codes for dynamic or unknown behavior.

Reject or redact:

- raw source snippets;
- raw SQL;
- raw config values;
- connection strings;
- secret-looking tokens;
- local absolute paths;
- private sample names;
- private repository names;
- private route strings;
- raw endpoint URLs;
- raw remote URLs.

## Validation Strategy

Implementation validation should include:

- focused unit tests for route root to downstream method composition;
- interface candidate tests for zero, one, and many candidates;
- projection tests for `combined_argument_flows` and `combined_fact_symbols`;
- object-shape, query-shape, repository, and data-surface attachment tests;
- reduced coverage and missing schema/extractor tests;
- deterministic ordering and byte-stable JSON tests;
- redaction tests for Markdown, JSON, logs, and display fields;
- `dotnet test` for the implementation slice;
- `git diff --check`;
- private path guard if available;
- relevant spec lint/check script if present;
- relevant pinned smoke checks from `docs/VALIDATION.md`, or explicit deferral
  with rationale.

Acceptance smoke should remain generic in committed docs: run a private legacy
ASP.NET smoke sample with a normalized API route and verify that output contains
either static downstream service/data rows with evidence or narrower
rule-backed gaps for the missing bridge/projection. Do not commit private sample
paths, names, route strings, raw SQL, config values, snippets, or raw remotes.
