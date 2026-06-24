# Route Flow Service/Data Composition Final Requirements

## Introduction

TraceMap already has `tracemap route-flow` and several route-centered slices:
the base route-flow report, endpoint trace summaries, service/data composition,
context groups, argument and fact-symbol projection, parameter-forward
bridges, endpoint bridge state, and initial endpoint stitching. The remaining
gap is the final static composition layer for a route-centered trace that can
start at an endpoint/root method and continue through service methods,
implementation candidates, repository/data/query/dependency surfaces,
value-origin rows, coverage labels, gaps, and conservative classifications.

This spec defines the next implementation PR boundary for that remaining work.
It is a deterministic report/query improvement over existing combined-index
evidence. It does not execute routes, host services, call live HTTP endpoints,
resolve runtime dependency injection, prove dynamic dispatch targets, evaluate
branches, connect to databases, execute SQL, infer row counts, prove production
traffic, or use LLMs, embeddings, vector databases, semantic search, or
prompt-based classification.

Public claim level: static evidence only. Committed examples and validation
notes must use synthetic or public-safe selectors such as
`GET /api/admin/users/roles`; private route strings, private sample names, local
absolute paths, raw SQL/config values, source snippets, secrets, raw remotes,
and private labels must not be committed.

## Requirements

### Requirement 1: Continuation Boundary And Ownership

**User Story:** As an implementer, I want the final slice to finish the
remaining route-centered service/data composition work without duplicating
already-shipped route-flow features.

#### Acceptance Criteria

1. WHEN implementation begins THEN it SHALL audit live `dev` route-flow code,
   rule catalog entries, tests, and the related Kiro specs before editing
   product code.
2. The implementation SHALL preserve the existing `tracemap route-flow`
   command, `reportType = "route-flow"`, JSON `version = "1.0"`, route-flow
   classification vocabulary, and `combined.route-flow.*` rule namespace unless
   a future breaking-schema spec explicitly supersedes them.
3. The implementation SHALL extend `CombinedRouteFlowReport` or existing
   route-flow helpers. It SHALL NOT create a second traversal engine, a new
   command, a parallel report type, a parallel rule namespace, or persisted
   derived route-flow rows.
4. This spec owns the final route-centered service/data composition completion
   slice: endpoint/root method to downstream call-edge stitching,
   implementation-candidate traversal hardening, attached service/data/query/
   dependency/value-origin rows, unjoinable-context gaps, and downgrade/safety
   tests for those rows.
5. This spec SHALL NOT reopen completed work from
   `route-flow-service-data-composition-next`,
   `route-centered-endpoint-trace-completeness`,
   `route-centered-static-flow-report`, `route-flow-endpoint-composition`, or
   `route-flow-endpoint-stitching` except to add focused regression coverage or
   patch a live gap found during the audit.
6. If live audit shows a listed task is already complete, the implementer SHALL
   record the evidence in `implementation-state.md`, mark only the corresponding
   task checkbox, and choose the next smallest unchecked task.

### Requirement 2: Endpoint Root To Service Call Stitching

**User Story:** As a reviewer, I want a selected endpoint/root method to stitch
to downstream service and helper calls when existing static evidence supports
the bridge.

#### Acceptance Criteria

1. WHEN selected route/client/endpoint evidence resolves to an endpoint root
   method or existing path root node THEN route-flow SHALL use only source-local
   symbol IDs, graph node IDs, supporting fact IDs, supporting edge IDs, or
   existing combined graph identities to start downstream traversal.
2. WHEN the endpoint/root method has direct static call, object-creation,
   parameter-forward, argument-flow, or dependency-edge evidence to service,
   helper, or repository-like methods THEN route-flow SHALL emit downstream
   flow rows with source label, source index ID, commit SHA where available,
   extractor identity where available, repo-relative file path, line span,
   symbols where available, rule IDs, evidence tiers, supporting fact IDs,
   supporting edge IDs, coverage state, classification, and limitations.
3. WHEN same-file, directory, short-name, display-name, textual, or route-string
   proximity is the only relationship between the root and a call edge THEN
   route-flow SHALL NOT attach the call as selected route evidence.
4. WHEN call-like evidence exists in the source but cannot be credibly stitched
   from a matched endpoint/root under full coverage THEN route-flow SHALL emit
   a scoped `MissingCallEdge` or equivalent shipped gap rather than a generic
   clean no-evidence conclusion.
5. WHEN stitching fails under reduced coverage, missing schema, missing
   extractor-family evidence, unknown commit SHA, or unverified source identity
   THEN route-flow SHALL emit `UnknownAnalysisGap` or a narrower coverage/
   identity/schema/extractor gap and SHALL NOT claim clean absence.
6. WHEN duplicate normalized route roots, ambiguous endpoint roots, cycles,
   depth caps, path caps, frontier caps, logic-row caps, or gap caps affect the
   trace THEN route-flow SHALL emit deterministic gaps and label the trace
   partial where appropriate.

### Requirement 3: Interface And Implementation Candidate Completion

**User Story:** As a maintainer, I want interface calls to expose static
implementation candidates and continue only where evidence allows, without
pretending TraceMap knows runtime binding.

#### Acceptance Criteria

1. WHEN traversal reaches an interface member or interface-declared symbol and
   source-local `combined_symbol_relationships` evidence identifies concrete
   implementation candidates THEN route-flow SHALL render candidate bridge rows
   or grouped context with supporting relationship facts/edges, symbol IDs, file
   spans, rule IDs, evidence tiers, source labels, commit SHAs where available,
   and limitations.
2. WHEN exactly one source-local compiler-backed implementation candidate
   exists THEN route-flow MAY continue through that candidate as review-tier
   static evidence, but any dependent path and summary classification SHALL be
   capped at `NeedsReviewStaticRouteFlow` or weaker.
3. WHEN multiple candidates exist THEN route-flow SHALL render deterministic
   candidate rows or an `AmbiguousImplementationCandidates` gap and SHALL NOT
   select one as the runtime target.
4. WHEN no implementation relationship exists THEN route-flow SHALL preserve
   the interface call row and emit `ImplementationCandidateUnavailable` or an
   existing equivalent gap.
5. WHEN relationship evidence is syntax-only, name-only, high fan-out,
   generated-code uncertain, cross-source, cross-language, stale, or reduced
   coverage THEN affected rows SHALL stay review-tier or unknown.
6. Dependency-injection registrations, container APIs, service locators,
   factories, reflection, configuration bindings, and runtime dispatch shall be
   limitations or gaps only; they SHALL NOT upgrade candidate rows to runtime
   proof.

### Requirement 4: Service/Data/Query/Dependency And Value-Origin Rows

**User Story:** As a reviewer, I want the trace to show the static business,
data, query, dependency, and value-origin evidence attached to selected route
flow rows.

#### Acceptance Criteria

1. WHEN selected route-flow rows connect to object-shape, DTO/projection,
   serializer/contract, validation/guard, branch/condition, exception,
   async/callback, flow-boundary, query-shape, SQL-shape, repository/data
   access, legacy-data, package/config, HTTP client, queue/event, storage, WCF,
   ASMX/SOAP, remoting, or generic dependency facts THEN route-flow SHALL render
   those facts as static context rows or grouped context.
2. WHEN `combined_argument_flows` or `combined_parameter_forward_edges` join to
   selected static route-flow evidence THEN route-flow MAY render value-origin
   context with safe parameter names, ordinals, type descriptors, expression
   kinds, hashes, supporting IDs, rule IDs, evidence tiers, and limitations.
3. WHEN `combined_fact_symbols` attaches data/query/dependency facts to
   selected source-local symbols THEN route-flow SHALL render fact-symbol
   context only when the source-local symbol identity is rule-backed.
4. WHEN data/query/dependency/value-origin facts are present but cannot be
   joined to the selected route path through route-flow path evidence,
   argument-flow evidence, parameter-forward evidence, fact-symbol evidence, or
   selected source-local symbol identity THEN route-flow SHALL emit narrower
   projection or attachment gaps such as `FactSymbolProjectionUnavailable`,
   `ArgumentProjectionUnavailable`, `DataSurfaceAttachmentMissing`, or shipped
   equivalents instead of inferring a flow.
5. Adjacent context SHALL be labeled as static `path-context`, grouped context,
   or gap evidence. It SHALL NOT be rendered as an executed path edge unless a
   selected route-flow edge supports it.
6. Rendered rows SHALL preserve supporting row IDs, supporting fact IDs,
   supporting edge IDs, source labels, source index IDs, commit SHAs,
   extractor identities, file spans, rule IDs, evidence tiers, coverage labels,
   classifications, and limitations where available.

### Requirement 5: Coverage, Gaps, Classifications, And Limitations

**User Story:** As a TraceMap user, I want route-centered output to distinguish
static evidence, review-tier candidates, clean absence, and unknown gaps.

#### Acceptance Criteria

1. Route-flow classifications SHALL remain limited to
   `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
   `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
   `UnknownAnalysisGap`.
2. `StrongStaticRouteFlow` SHALL require full relevant route-flow coverage,
   verified source identity, known commit SHA, unambiguous endpoint/root
   evidence, method/root bridge evidence, at least one stitched downstream row
   backed by Tier1Semantic or Tier2Structural evidence, no required
   interface-candidate bridge on the critical path, and no blocking schema/
   extractor/identity/truncation gaps.
3. `NoRouteFlowEvidence` SHALL require full relevant coverage and no unresolved
   selector, schema, extractor, source identity, route-root, method-symbol,
   call-edge, projection, truncation, or reduced-coverage gap.
4. `UnknownAnalysisGap` SHALL win over clean absence when missing schema,
   missing extractor-family evidence, failed or partial build, reduced
   semantic coverage, unknown commit SHA, unverified source identity, stale
   generated code, unsupported framework shape, or traversal truncation
   prevents a credible conclusion.
5. Syntax-only, textual, name-only, fallback, dynamic, ambiguous,
   high-fan-out, implementation-candidate, generated-code uncertain, unjoined,
   or reduced-coverage evidence SHALL cap affected rows at
   `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.
6. Every emitted gap SHALL include a gap kind, rule ID, evidence tier,
   classification, coverage state, safe scope, source label when available,
   affected row ID when available, supporting fact/edge IDs when available,
   file span when available, and at least one documented limitation.
7. Limitations SHALL explicitly state that the report is static evidence and is
   not proof of runtime request execution, traffic, production call paths,
   runtime DI target selection, dynamic dispatch, branch feasibility,
   authorization behavior, query execution, database state, data contents,
   release safety, outage cause, or business impact.
8. Full relevant route-flow coverage SHALL mean the selected route source has
   verified source identity, known commit SHA, full or credible source coverage
   for the selected evidence families, no selected-source `AnalysisGap` facts
   that affect route roots/call graph/symbol relationships/projection/surface
   evidence, and no missing optional table or extractor-family evidence that is
   required by the selected critical path. If missing optional evidence cannot
   affect the selected path, it MAY remain a non-blocking coverage note with a
   documented limitation.

### Requirement 6: CLI And Report Contract

**User Story:** As an automation author, I want the final route-flow
composition output to be deterministic, additive, and machine-readable.

#### Acceptance Criteria

1. The public command SHALL remain:

   ```text
   tracemap route-flow --index <combined.sqlite> --out <path> [selector] [options]
   ```

   with existing route-flow selectors and caps.
2. Directory or extensionless `--out` SHALL continue writing
   `route-flow-report.md` and `route-flow-report.json`; file output SHALL keep
   existing Markdown/JSON semantics.
3. JSON SHALL preserve required top-level fields already emitted by route-flow:
   `reportType`, `version`, `reportCoverage`, `coverageWarnings`, `query`,
   `snapshot`, `summary`, `entryEvidence`, `flowRows`, `logicRows`,
   `dependencySurfaces`, `touchedFiles`, `touchedSymbols`, `contextGroups`,
   `gaps`, and `limitations`, using empty arrays where a collection has no
   rows.
4. Additive fields for this slice, if needed, SHALL use stable names such as
   `bridgeState`, `groupKind`, `matchKind`, `attachmentKind`, `valueSafety`,
   `pathContextKind`, `supportingRowIds`, `supportingFactIds`,
   `supportingEdgeIds`, and `classificationCap`.
   Fields already emitted by prior route-flow specs SHALL be confirmed during
   the Task 4 live audit before treating this list as new additive work.
   Existing fields SHALL NOT be renamed or redefined in this PR boundary.
5. Markdown SHALL preserve existing route-flow section order and MAY add or
   refine narrow subsections for Endpoint Root, Static Flow, Context Groups,
   Business/Data Logic, Dependency Surfaces, Value Origin, Gaps, and
   Limitations.
6. Identical inputs and options SHALL produce byte-stable JSON and
   deterministic Markdown ordering.
7. Arrays and metadata maps SHALL sort by safe source label, normalized route
   root key, selector kind, row kind, classification rank, path length, safe
   display label, repo-relative file path, start line, end line, stable symbol
   ID, stable fact ID, stable edge ID, and stable row ID as applicable.
8. JSON SHALL use explicit `null`, empty arrays, and closed-set placeholder
   strings or gap codes rather than omitted fields to communicate uncertainty.
9. `--exit-code` behavior SHALL remain unchanged: validation, argument, file,
   schema, and system errors take precedence; route-flow review/unknown/no-
   evidence classifications produce non-zero according to the existing
   route-flow contract.

### Requirement 7: Safety And Redaction

**User Story:** As a maintainer, I want route-flow artifacts and validation
fixtures to be safe for public review.

#### Acceptance Criteria

1. Markdown, JSON, logs, committed fixtures, review packets, and validation
   notes SHALL NOT contain raw SQL, raw config values, connection strings,
   secrets, tokens, raw endpoint URLs, raw query strings, source snippets,
   private route values, private sample names, private repository names, raw
   local absolute paths, raw remotes, hostnames, or private labels.
2. File paths SHALL be repo-relative, synthetic fixture paths, or closed-set
   unavailable placeholders.
3. Route selectors and endpoint names SHALL be synthetic, normalized, hashed, or
   safely described. Private selector text SHALL be omitted or hashed.
4. SQL/query/data metadata SHALL render only safe descriptors, closed-set
   categories, stable hashes, table/column hash fields, or reviewed synthetic
   names.
5. Config, package, URL, connection, and secret-like metadata SHALL be omitted,
   hashed, or represented by closed-set descriptors.
6. Rows that omit or hash unsafe values SHALL cite
   `combined.route-flow.redaction.v1` where the report model allows supporting
   rule IDs.
7. Logs SHALL avoid echoing unsafe selector, SQL, config, path, remote, URL,
   secret-like, or source display values.

### Requirement 8: Validation And Public-Safe Smoke Guidance

**User Story:** As a reviewer, I want implementation validation that proves the
final route-centered trace is useful, conservative, deterministic, and safe.

#### Acceptance Criteria

1. Focused tests SHALL cover endpoint/root method to direct service call
   stitching, missing call-edge gaps under full coverage, and reduced-coverage
   unknown gaps.
2. Focused tests SHALL cover interface single candidate, multiple candidates,
   no candidate, syntax-only candidate, name-only candidate, high-fan-out, and
   cross-source/cross-language rejection behavior where live code supports
   those inputs.
3. Focused tests SHALL cover service-to-repository, repository-to-query,
   repository-to-data-surface, object/projection shape, dependency surface,
   argument-flow, parameter-forward, and fact-symbol rows when joined to
   selected route-flow evidence.
4. Focused tests SHALL cover adjacent-but-unjoinable data/query/dependency/
   value-origin facts and assert narrower projection or attachment gaps.
5. Focused tests SHALL cover duplicate route roots, unknown commit SHA,
   unverified source identity, missing optional schemas, missing extractor
   evidence, stale generated code where representable, traversal caps,
   truncation gaps, deterministic stable IDs, byte-stable JSON, and Markdown
   ordering.
6. Safety tests SHALL scan Markdown, JSON, logs, and fixture-derived metadata
   for raw SQL/config/URL/query-string/secret/snippet/local-path/remote/private-
   label leakage.
7. Tests SHALL prove every emitted rule ID resolves to `rules/rule-catalog.yml`
   and no parallel route-flow rule namespace is introduced.
8. Tests SHALL prove `--exit-code` remains non-zero for review/unknown/no-
   evidence classifications and that validation, argument, file, schema, and
   system errors take precedence over route-flow classification exit behavior.
9. Tests SHALL cover any emitted `classificationCap` field and SHALL prove
   context groups inherit the weakest classification, weakest evidence tier,
   and weakest coverage from contributing rows.
10. Tests SHALL assert that rows with hashed or omitted unsafe values cite
    `combined.route-flow.redaction.v1` where supporting rule IDs are available.
11. Tests SHALL prove a single implementation-candidate continuation cannot
    produce `StrongStaticRouteFlow`, and Tier3-only stitched downstream evidence
    cannot satisfy the strong downstream-row prerequisite.
12. Validation for product changes SHALL run:

   ```bash
   dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests
   dotnet build src/dotnet/TraceMap.sln
   dotnet test src/dotnet/TraceMap.sln
   ./scripts/check-private-paths.sh
   git diff --check
   ```

13. For route-flow/reporting changes, validation SHALL follow `docs/VALIDATION.md`
   and run or explicitly defer the relevant pinned public-safe smoke checks.
14. Public-safe smoke guidance SHALL use synthetic or checked-in fixtures only.
    A valid acceptance result is either static downstream service/data rows
    with rule IDs, evidence tiers, file spans, supporting fact/edge IDs, and
    coverage labels, or narrower evidence-backed gaps explaining which bridge
    or projection is missing.

## Non-Goals

- Runtime request execution, route probing, service hosting, browser automation,
  traffic capture, telemetry import, production proof, auth evaluation, or
  release approval.
- Runtime dependency-injection, service-locator, factory, reflection,
  configuration binding, dynamic dispatch, serializer runtime mapping, branch
  feasibility, symbolic execution, taint analysis, mutation tracking, database
  execution, live schema introspection, query-plan inference, row-count, or data
  content proof.
- Scanner/language-adapter extraction work, except for a tiny compatibility fix
  if the implementation audit proves existing evidence cannot be read safely.
- New public site copy, public marketing claims, generated public outputs, or
  private smoke artifacts.
- LLM calls, embeddings, vector databases, semantic search, fuzzy matching, or
  prompt-based classification in the core scanner/reporter.
