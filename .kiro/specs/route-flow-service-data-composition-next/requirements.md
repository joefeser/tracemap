# Route Flow Service/Data Composition Next Requirements

## Introduction

TraceMap already ships `tracemap route-flow` and several service/data
composition slices: route roots, static call traversal, interface candidate
boundaries, argument-flow projection, fact-symbol projection, parameter-forward
bridges, terminal data-surface rows, and narrow bridge gaps. The remaining gap
for issue #179 is not another broad route-flow rewrite. It is the next
continuation slice for making route-flow reports more complete and reviewable
when static evidence reaches service, repository-like, query-shape,
object-shape, dependency, and legacy data facts.

This spec defines the next implementation packet. It is a deterministic
reporting/query improvement over already-combined evidence. It does not execute
routes, call services, resolve runtime dependency injection, evaluate branches,
connect to databases, execute SQL, infer row counts, prove production traffic,
or use LLMs, embeddings, vector databases, or prompt-based classification.

Public claim level: hidden until implemented. Any private validation must be
described generically and must not record private route strings, private sample
names, local absolute paths, raw SQL, config values, source snippets, secrets,
or raw remotes.

## Requirements

### Requirement 1: Continuation Boundary

**User Story:** As an implementer, I want this follow-up to build on shipped
route-flow composition instead of duplicating completed work.

#### Acceptance Criteria

1. WHEN implementation begins THEN it SHALL verify the current route-flow code
   and existing specs before editing product code.
2. WHEN an existing route-flow behavior is already implemented THEN the
   implementation SHALL reuse it and SHALL NOT create a parallel traversal
   engine, rule namespace, command, or JSON report type.
3. WHEN the existing `route-flow-service-data-composition` spec already covers a
   behavior completely THEN this continuation SHALL reference it as prior art
   rather than restating it as new scope.
4. WHEN a task remains ambiguous after repository inspection THEN the
   implementation SHALL stop or narrow the task rather than guessing.
5. The continuation SHALL preserve `reportType = "route-flow"` and the existing
   route-flow JSON version unless a future breaking-schema spec explicitly
   changes it.
6. BEFORE implementing method/service grouping, data/query/dependency context,
   or downgrade-hardening behavior that overlaps
   `.kiro/specs/route-centered-endpoint-trace-completeness` tasks 8-10, the
   implementer SHALL record the exact sub-scope owned by this continuation
   slice. This spec owns only route-flow service/data grouping over already
   selected route-flow rows and downgrade tests for those grouped rows. It does
   not own touched-file or touched-symbol summaries, selector trace metadata,
   broad endpoint-trace completeness, or unrelated route-flow presentation
   backlog. The implementation SHALL NOT let both specs independently claim the
   same product work.

### Requirement 2: Method, Service, And Data Row Grouping

**User Story:** As a reviewer, I want route-flow output to group static service
and data evidence clearly without hiding the underlying evidence rows.

#### Acceptance Criteria

1. WHEN `flowRows`, `logicRows`, and `dependencySurfaces` contain selected
   source-local evidence for the same route-flow query THEN the report SHALL
   group rows into method/service, data/query/dependency, and gap contexts using
   safe labels.
2. Grouping SHALL reuse existing rows and supporting IDs. It SHALL NOT duplicate
   path graph rows as separate conclusions.
3. Group labels SHALL distinguish direct concrete method calls, interface calls,
   static implementation candidates, repository-like methods, query-shape
   facts, dependency/data surfaces, and bridge gaps when the existing evidence
   supports that distinction.
4. Interface and implementation candidate grouping SHALL keep candidate wording
   and SHALL cap classification at `NeedsReviewStaticRouteFlow` or weaker
   unless direct concrete call evidence exists.
5. WHEN grouping evidence is incomplete, ambiguous, high fan-out, syntax-only,
   name-only, generated-code uncertain, or reduced coverage THEN the grouped
   context SHALL remain review-tier or gap-labeled.
6. Grouped rows SHALL preserve rule IDs, evidence tiers, file paths, line spans,
   source labels, source index IDs, commit SHA where available, extractor
   identity where available, supporting fact IDs, supporting edge IDs, coverage
   state, and limitations.

### Requirement 3: Data, Query, Dependency, And Value-Origin Context

**User Story:** As a maintainer, I want route-flow reports to show the relevant
data/query/dependency context near the route path when static evidence supports
that relationship.

#### Acceptance Criteria

1. WHEN route-flow already has selected rows for object-shape, DTO/projection,
   validation/guard,
   branch/condition, async/callback, serializer/contract, query-shape,
   repository/data-access, package/config, HTTP client, queue/event, storage,
   WCF, ASMX, Remoting, legacy-data, SQL/persistence, or generic dependency
   evidence through selected static route-flow rows THEN the report SHALL
   include that context as static review evidence. This slice groups and
   renders existing route-flow evidence; it SHALL NOT add scanners, language
   extractors, or new source fact families.
2. WHEN `combined_argument_flows` or `combined_parameter_forward_edges` join to
   selected route-flow rows THEN value-origin context MAY be shown as bounded
   review context. It SHALL NOT claim full taint, mutation tracking, branch
   feasibility, serializer behavior, repository translation, runtime values, or
   production data contents.
3. WHEN `combined_fact_symbols` attaches data/query/dependency facts to a
   selected source-local symbol THEN the report SHALL show fact-symbol context
   using existing route-flow rule IDs and safe metadata.
4. WHEN data/query/dependency evidence is adjacent but not directly connected by
   route-flow path evidence, argument-flow evidence, parameter-forward evidence,
   or source-local fact-symbol evidence THEN the report SHALL emit a narrower
   gap instead of inferring a flow.
5. WHEN existing facts include unsafe SQL, config, connection string, URL,
   route, source snippet, secret-looking, local-path, remote, or private-name
   values THEN output SHALL omit, hash, or replace those values with safe
   descriptors and SHALL cite `combined.route-flow.redaction.v1`.

### Requirement 4: Coverage, Gaps, And Downgrades

**User Story:** As a TraceMap user, I want route-flow composition to explain
why evidence is missing or weak instead of overclaiming.

#### Acceptance Criteria

1. Route-flow classification SHALL use only existing
   `RouteFlowClassifications`: `StrongStaticRouteFlow`,
   `ProbableStaticRouteFlow`, `NeedsReviewStaticRouteFlow`,
   `NoRouteFlowEvidence`, and `UnknownAnalysisGap`.
2. Strong or clean no-evidence conclusions SHALL require full relevant
   route-flow coverage.
3. Reduced coverage, missing optional tables, missing schema columns, unknown
   commit SHA, missing extractor identity, stale generated code, unsupported
   route/data shapes, unjoinable projection rows, ambiguous service/data
   matches, high fan-out, truncation, and private-smoke-only evidence SHALL
   downgrade classifications or emit gaps.
4. WHEN no downstream rows are found under reduced coverage THEN the report
   SHALL NOT emit a clean `NoRouteFlowEvidence` conclusion.
5. WHEN optional route-flow tables or columns are absent in older combined
   indexes THEN the report SHALL emit availability gaps and remain
   backward-compatible. Missing optional tables or columns SHALL use existing
   `SchemaMissing` gaps; missing extractor-family evidence SHALL use existing
   `ExtractorUnavailable` gaps unless a new rule-catalog entry documents a
   narrower code before implementation.
6. WHEN rows are truncated by caps THEN the report SHALL emit deterministic
   `TruncatedByLimit` gaps and SHALL NOT imply the omitted rows are absent.
7. Every emitted gap SHALL include a rule ID, classification, safe scope,
   source label when available, supporting IDs when available, and documented
   limitations.

### Requirement 5: Deterministic Safe Output

**User Story:** As an automation author, I want route-flow composition output to
be stable, safe, and machine-readable.

#### Acceptance Criteria

1. Identical inputs SHALL produce byte-stable JSON and deterministic Markdown
   ordering.
2. New arrays and metadata maps SHALL be sorted deterministically by safe source
   label, route root key, row kind, classification rank, path length, safe
   display label, repo-relative file path, start line, stable symbol ID, stable
   fact ID, stable edge ID, and stable row ID as applicable.
3. Stable IDs SHALL be derived only from safe source labels, route root keys,
   supporting fact IDs, supporting edge IDs, symbol IDs, repo-relative file
   paths, line spans, and closed-set row kinds.
4. JSON SHALL use explicit `null`, empty arrays, and closed-set gap codes rather
   than omitted fields to communicate uncertainty.
5. Markdown and JSON SHALL NOT contain raw local absolute paths, raw remotes,
   private sample names, private route strings, raw SQL, raw config values,
   endpoint URLs, connection strings, source snippets, secrets, or unreviewed
   generated artifact paths.
6. Logs SHALL avoid echoing unsafe selector or display values.

### Requirement 6: Validation

**User Story:** As a reviewer, I want focused tests proving the continuation is
useful without widening TraceMap's claims.

#### Acceptance Criteria

1. Tests SHALL cover method/service grouping for direct calls, interface single
   candidate, interface multiple candidates, no candidate, syntax-only
   candidate, and high-fan-out cases where applicable to the selected slice.
2. Tests SHALL cover data/query/dependency context rows from existing
   `logicRows`, `dependencySurfaces`, `combined_argument_flows`,
   `combined_parameter_forward_edges`, and `combined_fact_symbols` where those
   inputs exist.
3. Tests SHALL cover unjoinable projection rows, unsupported attached context,
   missing optional schema, old combined indexes, reduced coverage, unknown
   commit SHA, and clean full-coverage no-evidence behavior.
4. Tests SHALL cover deterministic output and stable IDs for row grouping,
   data/query/dependency rows, gaps, and truncation.
5. Tests SHALL cover raw SQL/config/URL/route/snippet/path/secret redaction in
   Markdown, JSON, logs, and safe metadata.
6. Tests SHALL prove every emitted rule ID resolves to the rule catalog.
7. Validation SHALL run focused route-flow tests, solution build/tests when
   product code changes, `git diff --check`, `./scripts/check-private-paths.sh`,
   and relevant pinned smokes from `docs/VALIDATION.md` or record an explicit
   deferral.
8. Tests SHALL cover closed-set grouping metadata, including `groupKind`,
   `matchKind`, and `valueSafety`, for direct-call, candidate, argument-flow,
   data/query/dependency, and gap rows touched by the selected implementation
   slice.
