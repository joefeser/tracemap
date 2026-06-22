# Route Flow Endpoint Stitching Requirements

## Introduction

TraceMap can already produce route-centered static reports from combined
indexes, and recent route-flow slices project argument-flow, fact-symbol,
interface-candidate, and dependency-surface evidence when those facts already
join the selected path. Issue #201 identifies the next gap: a route root can be
detected while the report still fails to stitch that endpoint root to the
method-level call graph and implementation relationship evidence already stored
in the index.

This spec defines a conservative endpoint stitching layer for `tracemap
route-flow`. The goal is to connect static route entry evidence to downstream
method, interface-candidate, service, repository, and data/dependency evidence
when the combined index contains rule-backed facts. It must also explain
precisely why stitching stopped when evidence is missing, ambiguous, reduced, or
unsafe to render.

This is static evidence. It does not prove runtime request execution, runtime
dependency-injection target selection, branch feasibility, authorization,
deployment, traffic, database execution, or production behavior.

## Requirements

### Requirement 1: Endpoint Root Identity and Selector Evidence

**User Story:** As a reviewer, I want a route-flow query to identify the
endpoint root method behind a selected route or aligned client call, so that
downstream call evidence starts from a concrete static symbol rather than a
route string alone.

#### Acceptance Criteria

1. WHEN `tracemap route-flow` receives `--route`, `--client-call`,
   `--from-endpoint`, or an aligned route/client selector THEN the report SHALL
   identify the selected endpoint root evidence with rule ID, evidence tier,
   source label, commit SHA when available, file span when available,
   extractor identity, supporting fact IDs, and limitation text.
2. WHEN multiple route facts normalize to the same route selector in different
   sources or symbols THEN the report SHALL emit deterministic duplicate or
   ambiguity gaps and SHALL NOT choose an arbitrary root.
3. WHEN a route fact has no method symbol bridge THEN the report SHALL reuse
   the shipped `MissingMethodSymbolBridge` gap, or emit a documented successor
   only if live audit proves the shipped gap cannot express the endpoint root
   bridge state.
4. WHEN endpoint evidence comes from syntax fallback, dynamic URL text, reduced
   coverage, unknown commit SHA, or unverified source identity THEN the endpoint
   root SHALL be capped at review-tier or unknown classification as appropriate.
5. WHEN selector values contain local paths, raw URLs, query strings, secrets,
   private names, or unsafe text THEN output SHALL render only safe normalized
   keys, hashes, or omissions with redaction rule evidence.

### Requirement 2: Endpoint Method to Call-Graph Stitching

**User Story:** As a reviewer, I want the endpoint method to stitch into direct
call edges when those edges are already present, so that route-flow can show the
first downstream service or helper call instead of stopping at the controller.

#### Acceptance Criteria

1. WHEN endpoint root symbol evidence and source-local call edges share a
   credible method symbol identity THEN route-flow SHALL traverse from the
   endpoint method to direct callee symbols.
2. WHEN call edges exist in the same file or source but do not share a credible
   method symbol identity THEN route-flow SHALL NOT attach them by same-file,
   text, or short-name heuristics alone.
3. WHEN no direct call edge can be connected from the endpoint root under full
   coverage THEN route-flow SHALL emit a specific `MissingCallEdge` or
   equivalent gap instead of a generic no-evidence conclusion.
4. WHEN no direct call edge can be connected under reduced coverage THEN
   route-flow SHALL emit an unknown or reduced-coverage gap rather than
   claiming no downstream flow.
5. WHEN traversal reaches configured depth, path, frontier, row, or gap caps
   THEN route-flow SHALL emit truncation evidence with deterministic cap names
   and counts.

### Requirement 3: Interface and Implementation Relationship Bridging

**User Story:** As a reviewer, I want route-flow to use existing implementation
relationship evidence as candidate context when a call targets an interface, so
that static reports can continue through likely implementation code without
claiming runtime DI resolution.

#### Acceptance Criteria

1. WHEN a traversed call target is an interface member or interface-declared
   symbol and the combined index has source-local implementation relationship
   evidence THEN route-flow SHALL emit implementation-candidate bridge rows with
   supporting relationship evidence.
2. WHEN a single compiler-backed implementation candidate exists THEN
   route-flow MAY continue traversal through that candidate, but the bridge and
   any dependent path classification SHALL NOT exceed
   `NeedsReviewStaticRouteFlow`.
3. WHEN multiple implementation candidates exist THEN route-flow SHALL render
   deterministic candidate rows or a deterministic ambiguity gap and SHALL NOT
   choose one as the runtime target.
4. WHEN no implementation relationship evidence exists THEN route-flow SHALL
   emit `ImplementationCandidateUnavailable` or equivalent gap evidence.
5. WHEN relationship evidence is syntax-only, name-only, high fan-out,
   cross-source, cross-language, or reduced coverage THEN route-flow SHALL cap
   classification and emit limitations.
6. The implementation SHALL NOT treat DI registration facts, container APIs,
   configuration, factories, reflection, or service-locator calls as runtime
   binding proof.

### Requirement 4: Service, Repository, Data, and Dependency Surface Stitching

**User Story:** As a reviewer, I want the stitched route-flow to include
service, repository, query, legacy-data, and dependency surfaces that are
statically reachable from the endpoint path, so that the report can explain what
files, symbols, and static evidence a route touches.

#### Acceptance Criteria

1. WHEN downstream method symbols connect to query-pattern, SQL-shape,
   repository/data-access, legacy-data, package/config, HTTP client, WCF,
   ASMX/SOAP, remoting, event/message, storage, or other dependency surface
   facts through existing rule-backed relationships THEN route-flow SHALL render
   those surfaces as static review context.
2. WHEN business/data logic rows are adjacent only by same file or textual
   proximity and lack symbol, fact, argument-flow, or edge support THEN
   route-flow SHALL NOT attach them as route evidence.
3. WHEN dependency/data facts exist in the source index but cannot be stitched
   to the selected route path THEN route-flow SHALL emit a scoped
   `DataSurfaceAttachmentMissing` gap, or a documented successor only if live
   audit proves the shipped gap cannot express the attachment state, rather
   than silently dropping context.
4. Rendered surface metadata SHALL omit or hash raw SQL, endpoint addresses,
   query strings, connection strings, config values, local paths, raw remotes,
   source snippets, secrets, and private labels.
5. Surface row stable IDs SHALL be deterministic from safe source-scoped
   identity and metadata, not volatile combined row IDs or local paths.

### Requirement 5: Gap Taxonomy and Classification Safety

**User Story:** As a reviewer, I want route-flow failures to explain the exact
missing bridge, so that a zero-row report is actionable instead of mysterious.

#### Acceptance Criteria

1. Route-flow SHALL distinguish at least these gap families where evidence
   supports the distinction: selector no match, duplicate endpoint root,
   endpoint method bridge missing, missing call edge, implementation candidate
   unavailable, ambiguous implementation candidates, runtime binding not
   proven, dependency/data surface attachment missing, schema missing,
   extractor unavailable, reduced coverage, unknown commit/source identity,
   unsafe value omitted, and traversal truncation.
2. `StrongStaticRouteFlow` SHALL require full coverage, verified source
   identity, rule-backed endpoint root evidence, method-symbol bridge evidence,
   and at least one stitched downstream flow row with no required review-tier
   bridge.
3. Any interface/implementation bridge, syntax-only evidence, name-only
   candidate, dynamic URL, ambiguous root/candidate, high fan-out, reduced
   coverage, unknown commit SHA, or truncation SHALL cap the affected row and
   summary classification to review-tier or unknown according to documented
   rules.
4. Clean `NoRouteFlowEvidence` SHALL require full route-flow coverage and no
   unresolved schema, extractor, identity, selector, or reduced-coverage gaps.
5. `UnknownAnalysisGap` SHALL win over no-evidence conclusions when missing
   schema, reduced coverage, or source identity prevents a credible conclusion.

### Requirement 6: Output Contract and Determinism

**User Story:** As a reviewer or automation, I want route-flow endpoint
stitching to extend existing reports without breaking consumers.

#### Acceptance Criteria

1. The implementation SHALL preserve the existing `route-flow-report.json`
   report type and version unless a future breaking-schema spec explicitly
   changes them.
2. New fields SHALL be additive, deterministic, closed-set where possible, and
   use empty arrays or explicit nulls consistently.
3. Markdown and JSON SHALL include the query, sanitized selector trace,
   snapshot/source identity, endpoint root evidence, stitched flow rows,
   implementation bridge rows, business/data logic rows, dependency surfaces,
   gaps, and limitations where applicable.
4. Arrays SHALL sort deterministically by safe stable keys, source labels, file
   paths, line spans, symbols, rule IDs, and IDs using ordinal ordering.
5. Generated output SHALL contain no timestamps, local absolute paths, raw
   source snippets, raw SQL, raw config values, raw URLs, raw remotes,
   connection strings, secrets, or private sample labels.

### Requirement 7: Tests and Validation

**User Story:** As a maintainer, I want focused fixtures that prove endpoint
stitching is conservative, deterministic, and safe.

#### Acceptance Criteria

1. Tests SHALL cover route root to endpoint method bridge success and missing
   bridge gaps.
2. Tests SHALL cover endpoint method to direct call edge stitching and missing
   call edge gaps.
3. Tests SHALL cover interface implementation candidate success, multiple
   candidates, no candidates, syntax/name-only candidates, and classification
   caps.
4. Tests SHALL cover data/dependency surface attachment success and scoped
   attachment gaps.
5. Tests SHALL cover reduced coverage, missing optional schemas, unknown commit
   SHA or identity, dynamic URLs, duplicate normalized routes, and traversal cap
   behavior.
6. Tests SHALL verify deterministic ordering and byte-stable JSON output.
7. Tests SHALL verify Markdown/JSON/log safety for selectors, surface metadata,
   source labels, and gap details.
8. Validation SHALL run focused route-flow tests, full .NET build/test, private
   path guard, `git diff --check`, and any relevant pinned smoke checks when
   shared graph, route-flow, or language-adapter behavior changes.

## Non-Goals

- Runtime request execution proof.
- Runtime dependency-injection target selection.
- Branch feasibility, symbolic execution, taint analysis, or mutation proof.
- Auth, deployment, traffic, telemetry, production use, or release safety.
- SQL execution, database connectivity, or live schema introspection.
- LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact analysis.
- Fuzzy matching, edit distance, semantic search, or arbitrary winner
  selection for ambiguous candidates.
- Publishing private sample names, private paths, source snippets, raw SQL,
  raw config values, endpoints, secrets, or raw remotes.
