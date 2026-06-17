# Route-Centered Static Flow Report Requirements

## Introduction

TraceMap can already index many pieces of an endpoint flow: HTTP route
bindings, TypeScript HTTP client calls, endpoint alignment, call edges, symbol
relationships, query patterns, dependency/data surfaces, object/projection
shapes, combined dependency paths, reverse paths, and evidence graph/vault
exports. The missing product view is a single route-centered report that starts
from one HTTP route or client call and explains the static code path TraceMap can
evidence through files, line spans, symbols, intermediate calls,
transformations, business logic, dependency surfaces, and analysis gaps.

This feature is a deterministic report/query layer over existing evidence. It
does not execute code, call live HTTP endpoints, connect to databases, prove
runtime dependency-injection targets, infer branch feasibility, or classify
business impact with AI.

Public claim level: available only through checked-in public-safe fixtures and
generated reports that pass TraceMap redaction guards.

## Requirements

### Requirement 1: Route-Centered Query Surface

**User Story:** As a maintainer, I want to query a combined TraceMap snapshot by
route or client call so that I can review the static path evidence centered on
one API interaction.

#### Acceptance Criteria

1. WHEN the user runs `tracemap route-flow --index <combined.sqlite> --route
   "<METHOD> <PATH>" --out <path>` THEN TraceMap SHALL read the combined index
   read-only and emit a route-centered flow report.
2. WHEN `--client-call "<METHOD> <PATH>"` is provided instead of `--route` THEN
   TraceMap SHALL select matching `HttpCallDetected` client-call evidence as the
   query root.
3. WHEN `--from-endpoint`, `--from-symbol`, or `--from-source` are provided THEN
   TraceMap SHALL reuse the existing `tracemap paths` selector grammar where the
   selector names overlap; any deviation SHALL be documented in the route-flow
   rule limitations and design before implementation. Route and client-call
   selectors SHALL remain first-class aliases in the report query metadata.
4. WHEN the input is not a combined index THEN the command SHALL fail clearly
   and SHALL NOT silently treat one single-language scan as complete route
   coverage.
5. WHEN both a server route and an aligned client call are available THEN the
   report SHALL include both sides as roots or entry evidence, preserving
   endpoint-alignment rule IDs and source labels.
6. WHEN only one side is available THEN the report SHALL still emit a valid
   partial report with coverage labels and selector gaps for the missing side.
7. WHEN no selector matches the combined index THEN the report SHALL emit a
   valid empty report with a `SelectorNoMatch` gap, not a malformed file or a
   clean no-impact conclusion.
8. WHEN route templates include parameters, optional segments, or normalized
   path keys THEN selection SHALL use the existing endpoint-normalization
   contract and record the match mode in query metadata.
9. WHEN `--exit-code` is requested THEN TraceMap SHALL return `0` for
   `StrongStaticRouteFlow` or `ProbableStaticRouteFlow` results with no
   blocking gaps, SHALL return non-zero for `NeedsReviewStaticRouteFlow`,
   `NoRouteFlowEvidence`, or `UnknownAnalysisGap`, and SHALL let validation,
   argument, file, schema, and system errors take precedence.
10. WHEN `--classification <value>` is provided THEN TraceMap SHALL filter the
    report to rows matching that classification and SHALL recompute the overall
    summary classification from the remaining rows. If the filter removes every
    row, TraceMap SHALL emit a `SelectorNoMatch` gap and set the overall
    classification to `UnknownAnalysisGap`.

### Requirement 2: Evidence Row Provenance

**User Story:** As a reviewer, I want every row in the route flow report tied to
documented static evidence so that conclusions are auditable.

#### Acceptance Criteria

1. WHEN any root, edge, logic row, dependency surface, terminal surface, or gap
   is emitted THEN it SHALL include a rule ID and evidence tier.
2. WHEN a row is backed by source evidence THEN it SHALL include source label,
   commit SHA, repo-relative file path, start line, end line, extractor name, and
   extractor version where available.
3. WHEN supporting fact IDs, edge IDs, symbol IDs, scan IDs, or combined source
   IDs are available THEN the report SHALL preserve them in deterministic order.
4. WHEN a row is derived from multiple evidence families THEN the row SHALL cite
   the derived route-flow rule ID and the supporting source rule IDs.
5. WHEN source evidence lacks a file span or extractor version THEN the row
   SHALL use explicit `null`, `unknown`, or `unavailable` placeholders and emit a
   coverage note rather than dropping the row.
6. WHEN commit SHA is unknown or source identity is unverified THEN the report
   SHALL mark provenance as reduced and SHALL NOT present no-path findings as
   clean absence.
7. WHEN evidence is syntax-only, textual, fallback, ambiguous, high fan-out, or
   reduced coverage THEN the row SHALL retain that weaker tier and SHALL NOT be
   upgraded by path composition.

### Requirement 3: Evidence Families To Compose

**User Story:** As a maintainer, I want route flow reports to reuse TraceMap's
existing fact families instead of creating a second scanner.

#### Acceptance Criteria

1. WHEN endpoint alignment rows exist THEN route-flow selection SHALL use them
   to relate `HttpCallDetected` client calls to `HttpRouteBinding` server routes
   where method and normalized route evidence supports the match.
2. WHEN route-binding facts exist THEN the report SHALL include controller,
   action, handler symbol, method, normalized path template/key, parameters, and
   route limitations where available.
3. WHEN TypeScript HTTP client call facts exist THEN the report SHALL include
   client file span, containing class/function, method, normalized path evidence,
   dynamic URL reason, response/body shape metadata where available, and
   endpoint alignment status.
4. WHEN combined call edge, object creation, symbol relationship, or
   parameter-forward evidence exists in `combined_dependency_edges` or its
   backing `combined_*` tables THEN route-flow traversal SHALL reuse it as path
   edges with original supporting fact/edge provenance.
5. WHEN `combined_argument_flows` or `combined_fact_symbols` exist THEN
   route-flow SHALL read them through explicit route-flow reader code rather
   than assuming the existing path graph inventory exposes them.
6. WHEN `combined_symbol_relationships` or fact-symbol attachments exist THEN the
   report MAY use them to connect endpoint, method, interface, implementation,
   override, or type nodes within the conservative limits of Requirement 4.
7. WHEN query-pattern, SQL-shape, persistence, package/config, HTTP-client,
   WCF/service, legacy-data, or dependency-surface facts are reached THEN the
   report SHALL include them as dependency/data/business surfaces.
8. WHEN object shape, projection shape, schema/DTO, serializer, validation,
   branching, async boundary, or flow-boundary facts exist on or near the path
   THEN the report SHALL include them as business-logic rows without claiming
   runtime execution.
9. WHEN combined `paths`, `reverse`, evidence graph, or vault export models
   already expose reusable graph inventory or safe rendering helpers THEN the
   implementation SHALL reuse or extract those helpers rather than creating a
   divergent graph model.

### Requirement 4: Conservative Interface-To-Implementation Bridges

**User Story:** As a reviewer, I want interface calls linked to possible
implementation candidates when TraceMap has compiler evidence, but I do not want
the report to imply runtime DI certainty.

#### Acceptance Criteria

1. WHEN a call edge targets an interface member and
   `combined_symbol_relationships` contains compiler-resolved implementation
   evidence for one or more concrete members THEN route-flow MAY add
   `interface-implementation-candidate` bridge rows.
2. WHEN a bridge row is emitted THEN it SHALL cite the relationship fact or
   edge, `csharp.semantic.symbolrelationship.v1` or successor rule, source and
   target symbol IDs, evidence tier, file spans where available, and a limitation
   saying the bridge is not runtime DI target proof.
3. WHEN a path requires any `interface-implementation-candidate` bridge, even a
   single compiler-resolved candidate, THEN the path classification SHALL be
   capped at `NeedsReviewStaticRouteFlow` unless a future deterministic
   runtime-binding rule supplies stronger evidence.
4. WHEN no implementation relationship exists THEN the report SHALL stop at the
   interface call and emit an `ImplementationCandidateUnavailable` gap rather
   than guessing from names or project structure.
5. WHEN implementation evidence comes from syntax-only, name-only, fallback, or
   cross-source string matching THEN the bridge SHALL be review-tier and SHALL
   NOT produce strong classifications.
6. WHEN dependency-injection registrations, reflection, dynamic dispatch,
   factory methods, service locators, or configuration-driven bindings would be
   required to identify the runtime target THEN route-flow SHALL record a
   limitation or gap instead of choosing a target.

### Requirement 5: Business Logic And Data Surface Rows

**User Story:** As an engineer, I want the route report to highlight logic and
data surfaces on the static path so I can review more than just method names.

#### Acceptance Criteria

1. WHEN projection or object-shape evidence is attached to a method, call-site,
   return expression, DTO mapping, serializer member, or route-adjacent symbol
   on the path THEN the report SHALL emit a `projection-or-object-shape` row.
2. WHEN query/filter/sort/select/include/mutation evidence is reached THEN the
   report SHALL emit `query-shape` or `query-builder` rows using safe derived
   field/table/operation metadata only.
3. WHEN branch, validation, guard, exception, null-check, authorization
   attribute, middleware marker, async scheduling, await boundary, callback, or
   flow-boundary evidence exists on the path THEN the report SHALL emit a
   `business-logic-boundary` row with static evidence limitations.
4. WHEN repository, DbSet-like property, ORM model, SQL-shape, package/config,
   HTTP client, queue/event, storage, or WCF/service evidence is reached THEN
   the report SHALL emit a `dependency-or-data-surface` row.
5. WHEN business logic rows are adjacent to but not directly traversed by a path
   edge THEN the report SHALL label them as `path-context` instead of path edges.
6. WHEN logic rows are emitted THEN they SHALL never be described as executed,
   taken, authorized, persisted, queried at runtime, or business-impact proof.
7. WHEN raw values, literals, raw SQL, source snippets, URLs, connection strings,
   or secrets appear in source fact properties THEN the report SHALL omit or hash
   them before rendering.

### Requirement 6: Classifications, Coverage, And Gaps

**User Story:** As a TraceMap user, I want route-flow results labeled with
conservative coverage and gap states so I do not overread partial evidence.

#### Acceptance Criteria

1. WHEN a selected route/client call, traversal edges, and terminal surfaces are
   connected by Tier1 semantic or strong Tier2 structural evidence under full
   route-flow coverage THEN the report MAY classify the result as
   `StrongStaticRouteFlow`. Full route-flow coverage requires known commit SHA,
   full or credible source coverage for contributing sources, route/client-call
   extractor availability for selected entry evidence, and non-gap-only graph
   evidence from the edge and symbol-relationship families required by the path.
2. WHEN credible structural evidence connects the path but one or more semantic
   links are unavailable THEN the report SHALL classify the result no stronger
   than `ProbableStaticRouteFlow`.
3. WHEN the path uses syntax-only, textual, name-only, ambiguous,
   implementation-candidate, generated-code uncertain, high fan-out, dynamic
   URL, or fallback evidence THEN the result SHALL classify as
   `NeedsReviewStaticRouteFlow` or weaker.
4. WHEN no downstream path or surface is found under full route-flow coverage
   THEN the report MAY emit `NoRouteFlowEvidence`.
5. WHEN no downstream path or surface is found under reduced coverage, missing
   extractors, missing schema, unknown commit SHA, or selector ambiguity THEN the
   report SHALL emit `UnknownAnalysisGap`, not clean absence.
6. WHEN traversal hits depth, path, frontier, root, business-row, or gap caps
   THEN the report SHALL mark the report partial and emit `TruncatedByLimit`.
7. WHEN any contributing source has reduced coverage THEN route-flow
   classifications and no-evidence findings SHALL carry coverage caveats.
8. WHEN gaps are emitted THEN each gap SHALL include a gap kind, rule ID,
   evidence tier, source label where available, affected selector or row ID, and
   limitation.

### Requirement 7: Markdown And JSON Outputs

**User Story:** As a human and automation consumer, I want deterministic
route-flow artifacts that are readable and machine-checkable.

#### Acceptance Criteria

1. WHEN `--out` is a directory or extensionless path THEN TraceMap SHALL write
   `route-flow-report.md` and `route-flow-report.json`.
2. WHEN `--out` is a file path THEN TraceMap SHALL write Markdown by default or
   JSON when `--format json` is provided.
3. WHEN Markdown is emitted THEN sections SHALL include Summary, Query, Snapshot
   Sources, Entry Evidence, Static Flow, Business/Data Logic, Dependency
   Surfaces, Gaps, and Limitations.
4. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`,
   `reportCoverage`, `coverageWarnings`, `query`, `snapshot`, `summary`,
   `entryEvidence`, `flowRows`, `logicRows`, `dependencySurfaces`, `gaps`, and
   `limitations`.
5. WHEN the same input and options are run twice THEN Markdown and JSON SHALL be
   byte-stable.
6. WHEN arrays or metadata maps are emitted THEN ordering SHALL be
   deterministic.
7. WHEN evidence rows are rendered THEN Markdown SHALL use static-evidence
   wording such as "static path evidence", "candidate implementation", and
   "coverage-relative"; it SHALL NOT say "executed", "impacted", "called at
   runtime", "uses in production", or "authorized".
8. WHEN JSON contains optional unavailable values THEN it SHALL use `null`, empty
   arrays, or closed-set placeholder strings consistently.

### Requirement 8: Public And Private Artifact Safety

**User Story:** As a maintainer, I want generated route-flow artifacts safe for
public sharing after review.

#### Acceptance Criteria

1. Generated Markdown and JSON SHALL NOT include local absolute paths, raw
   repository remotes, private repository names, private sample labels, source
   snippets, raw SQL, raw URLs, endpoint addresses, connection strings, config
   values, secrets, credential-like values, or raw analyzer diagnostics by
   default.
2. File paths SHALL be repo-relative or neutral labels.
3. Source labels SHALL be user-provided safe labels or sanitized labels from the
   combined index; unsafe labels SHALL be hashed or replaced with neutral labels.
4. Route selectors and display paths SHALL use normalized method/path evidence,
   never raw full URLs or hostnames.
5. Raw source snippets SHALL remain unavailable unless a future explicit option
   and redaction rule are approved; this spec does not add that option.
6. Logs SHALL avoid echoing unsafe selector values when they look like local
   paths, remotes, raw URLs, raw SQL, connection strings, or secrets.
7. Public-safe fixtures SHALL use neutral names and synthetic domains only where
   necessary, and SHALL NOT include private sample names or paths.

### Requirement 9: Tests And Validation

**User Story:** As a maintainer, I want tests and smoke checks that prove the
route-flow report is deterministic, conservative, and safe.

#### Acceptance Criteria

1. WHEN the feature is implemented THEN tests SHALL cover a public-safe sample
   fixture with a client HTTP call aligned to an ASP.NET route, controller to
   service interface call, interface implementation candidate, repository call,
   DbSet-like data access, query/projection evidence, and at least one
   business-logic boundary.
2. Tests SHALL cover route-only, client-call-only, aligned client/server,
   selector no-match, dynamic URL, missing TypeScript HTTP facts, missing route
   facts, combined index with only one language adapter present, missing
   `combined_call_edges`, missing `combined_argument_flows`, missing
   `combined_fact_symbols`, missing `combined_symbol_relationships`, reduced
   coverage, unknown commit SHA, duplicate route identity across sources,
   ambiguous implementation candidates, high-fan-out surfaces, percent-encoded
   route selectors, truncation, non-combined input rejection, no-mutation
   read-only behavior, filter-option behavior, and old combined schemas.
3. Tests SHALL prove that interface implementation bridges do not claim runtime
   DI certainty and cannot produce `StrongStaticRouteFlow` when multiple
   candidates exist.
4. Tests SHALL prove syntax-only, name-only, dynamic, fallback, ambiguous, or
   reduced-coverage evidence cannot produce `StrongStaticRouteFlow`.
5. Tests SHALL prove Markdown and JSON omit unsafe values including local
   absolute paths, raw remotes, raw SQL, raw URLs, snippets, connection strings,
   private labels, unsafe `SafeMetadata` values, and secret-like values.
6. Tests SHALL prove generated JSON and Markdown are byte-stable across repeated
   runs and input-row permutations.
7. Tests SHALL prove generated Markdown and JSON do not contain forbidden
   runtime or impact wording such as `executed`, `impacted`, `called at
   runtime`, `authorized`, `used in production`, or `query runs`.
8. Tests SHALL prove `--format json` file output and `--exit-code` behavior.
9. Tests SHALL prove `--max-logic-rows` emits a truncation gap.
10. Tests SHALL prove `--classification`, `--from-endpoint`, `--from-symbol`,
    and `--from-source` behavior, including entry evidence kinds for symbol and
    source roots.
11. Tests SHALL prove empty combined snapshots and `index_sources` with zero rows
    produce `UnknownAnalysisGap`.
12. Tests SHALL prove no timestamps or generation-time values appear in route
    flow Markdown or JSON.
13. Validation SHALL include `dotnet build src/dotnet/TraceMap.sln`, `dotnet test
   src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, and `git diff
   --check`.
14. IF route-flow implementation modifies combined path traversal, endpoint
   alignment, language adapters, or shared report helpers THEN validation SHALL
   include the relevant pinned smoke checks from `docs/VALIDATION.md`, including
   the public combined-path smoke when applicable.
15. IF a required local tool is missing THEN the implementer SHALL follow the
   repository tool-discovery guidance before stopping or changing the workflow.

## Non-Goals

- No runtime execution proof.
- No live HTTP calls or browser traffic capture.
- No database connection, schema introspection, query execution, or query-plan
  inference.
- No branch feasibility, auth proof, permission proof, middleware proof,
  deployment proof, or production-usage proof.
- No runtime DI certainty, reflection target certainty, dynamic dispatch
  certainty, serializer runtime mapping proof, or generated-code freshness proof.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  TraceMap core.
- No source snippets, raw SQL, raw URLs, connection strings, secrets, local
  absolute paths, raw remotes, or private sample names in default outputs.
