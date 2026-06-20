# Route-Centered Endpoint Trace Completeness Requirements

## Introduction

TraceMap already has `tracemap route-flow` plus endpoint-composition and
service/data-composition slices. The next gap is completeness of the
route-centered answer: given a safe selector such as a normalized API route,
TraceMap should tell a reviewer what files, line spans, symbols, method calls,
service calls, data/query/dependency evidence, route-flow rows, gaps, and
limitations the selected static evidence touches.

This feature is a deterministic report/query improvement over existing combined
index evidence. It does not execute code, observe traffic, prove runtime
dependency-injection targets, infer production call paths, classify outage
cause, assess release safety, use AI/LLM impact analysis, use embeddings, use a
vector database, or perform prompt-based classification.

Public claim level: static evidence only. Examples, fixtures, review notes, and
committed artifacts must be synthetic or public-safe.

## Requirements

### Requirement 1: Route Selector Normalization And Safety

**User Story:** As a reviewer, I want route-centered trace queries to accept
safe selectors and record how they matched so that the report is reproducible
without leaking private route values.

#### Acceptance Criteria

1. WHEN the user runs `tracemap route-flow --index <combined.sqlite> --route
   "<METHOD> <PATH>" --out <path>` THEN TraceMap SHALL normalize the selector
   using the existing route-flow selector contract and store only safe selector
   metadata in the report.
2. WHEN `--client-call`, `--from-endpoint`, `--from-webforms-event`,
   `--from-symbol`, or `--from-source` is used THEN the trace-completeness view
   SHALL preserve the existing route-flow selector grammar and SHALL NOT add a
   second public command in the first implementation slice.
3. WHEN selector text contains host material, raw remotes, absolute local paths,
   encoded slashes, traversal-like material, connection strings, raw SQL,
   secret-like values, or unsafe private identifiers THEN output SHALL omit,
   hash, or replace the unsafe value with a deterministic safe descriptor and
   cite `combined.route-flow.redaction.v1`.
4. WHEN a selector can be matched by method and normalized path key THEN the
   report SHALL record the match mode, normalized key, method, and whether the
   selected side is route evidence, client-call evidence, or aligned evidence.
5. WHEN no selector matches, including when the combined index contains no
   endpoint route evidence at all, THEN the report SHALL emit a
   `SelectorNoMatch` gap with `combined.route-flow.selector.v1`, not a clean
   absence conclusion.
6. WHEN selector matching depends on reduced coverage, dynamic client URL
   evidence, syntax fallback, unknown commit SHA, or missing schema THEN the
   result SHALL be coverage-relative and SHALL NOT emit `NoRouteFlowEvidence`.

### Requirement 2: Entry Evidence And Touched Files

**User Story:** As a maintainer, I want the report to identify the route or
client entry evidence and the source files it touches so that I can begin a
review from evidence-backed locations.

#### Acceptance Criteria

1. WHEN route-binding facts match the selector THEN the report SHALL include
   route entry rows with rule IDs, evidence tiers, source labels, commit SHAs,
   repo-relative file paths, line spans, extractor names, extractor versions,
   supporting fact IDs, and handler/controller/action symbols where available.
2. WHEN HTTP client-call facts match the selector THEN the report SHALL include
   client entry rows with the same provenance fields plus dynamic URL reasons,
   request/response shape descriptors, and endpoint-alignment status where
   available.
3. WHEN client and server evidence are aligned by deterministic endpoint
   evidence THEN the report SHALL include both sides and preserve supporting
   alignment rule IDs and source labels.
4. WHEN only one side exists THEN the report SHALL remain valid, mark coverage
   appropriately, and include a gap for the missing side only when the evidence
   family was expected and unavailable.
5. WHEN a route entry bridges to a handler or method symbol THEN the touched
   files list SHALL include the entry file span and the method-symbol span when
   available.
6. WHEN the bridge to a source-local method symbol is unavailable or ambiguous
   THEN the report SHALL emit `MissingMethodSymbolBridge`,
   `MissingRouteRoot`, or a narrower existing gap, and SHALL NOT guess from a
   short name. `MissingRouteRoot` SHALL describe matched selector context that
   cannot produce route-root evidence for composition; it SHALL NOT replace the
   plain selector-miss case.

### Requirement 3: Static Flow Rows And Symbols Touched

**User Story:** As an engineer, I want route-centered output to show the method,
service, interface, implementation, and dependency rows touched by selected
static evidence.

#### Acceptance Criteria

1. WHEN selected route-flow traversal reaches static call, object-creation,
   parameter-forward, argument-flow, fact-symbol, symbol-relationship, or
   dependency edges THEN the report SHALL include method/service flow rows with
   supporting edge IDs, fact IDs, symbols, file spans, rule IDs, evidence tiers,
   and limitations.
2. WHEN a row touches a symbol THEN the report SHALL include the safest
   available symbol identity: stable symbol ID, containing type/member display
   name, source label, file path, and line span. Missing fields SHALL use
   explicit `null`, empty arrays, or documented unavailable placeholders.
3. WHEN multiple rows touch the same file or symbol THEN the report SHALL expose
   both row-level evidence and a deterministic touched-files/touched-symbols
   summary grouped by safe source label and repo-relative file path.
4. WHEN traversal reaches an interface member THEN the report MAY emit
   interface-to-implementation candidate rows only from deterministic
   relationship evidence and SHALL keep candidate-dependent rows at
   `NeedsReviewStaticRouteFlow` or weaker.
5. WHEN overrides, inheritance, partial classes, generated code, reflection,
   factories, service locators, configuration-driven bindings, or dynamic
   dispatch affect target selection THEN the report SHALL emit gaps or
   limitations rather than choosing a runtime target.
6. WHEN traversal caps are reached THEN the report SHALL emit
   `TruncatedByLimit` or `TraversalBounds`, label the trace partial, and keep
   all retained rows in deterministic order.

### Requirement 4: Data, Query, Dependency, And Value-Origin Evidence

**User Story:** As a reviewer, I want the endpoint trace to show static
business/data context without exposing unsafe source values or pretending to
execute queries.

#### Acceptance Criteria

1. WHEN object-shape, DTO/type, projection, serializer, validation, guard,
   branch, exception, async, callback, or flow-boundary facts attach to selected
   route-flow symbols THEN the report SHALL include logic rows as static
   context.
2. WHEN query-pattern, SQL-shape, ORM, repository, package/config, HTTP client,
   queue/event, storage, WCF/service, remoting, legacy-data, or generic
   dependency facts are reached THEN the report SHALL include dependency/data
   rows with safe descriptors and provenance.
3. WHEN `combined_argument_flows` can be joined to selected static call/path
   evidence THEN the report SHALL include argument/value-origin rows with safe
   parameter names, ordinals, type descriptors, expression kinds, hashes, rule
   IDs, and limitations where available.
4. WHEN argument/value-origin rows are unavailable, unjoinable, cross-source by
   guess, or only name-matched THEN the report SHALL emit
   `ArgumentProjectionUnavailable` or a narrower gap and SHALL NOT infer a data
   flow.
5. WHEN `combined_fact_symbols` attach query, object-shape, config, package, or
   data facts to selected symbols THEN the report SHALL include fact-symbol
   projection rows only when the symbol identity is source-local and
   rule-backed.
6. WHEN raw SQL, raw config values, raw URLs, connection strings, source
   snippets, secrets, private route strings, private sample labels, hostnames,
   raw remotes, or local absolute paths appear in input evidence THEN Markdown,
   JSON, logs, and committed fixtures SHALL omit, hash, or safely describe
   those values.
7. WHEN data/query/dependency rows are adjacent but not directly connected by
   selected static evidence THEN they SHALL be labeled as `path-context` or
   represented as gaps, never as executed path edges.

### Requirement 5: Coverage Labels, Gaps, Classifications, And Limitations

**User Story:** As a TraceMap user, I want endpoint trace reports to state what
is known, what is partial, and why confidence was downgraded.

#### Acceptance Criteria

1. WHEN every required selector, entry, path, symbol, and terminal-surface link
   has full static coverage, known commit SHA, verified source identity, and no
   blocking gaps THEN the summary MAY be `StrongStaticRouteFlow` or
   `ProbableStaticRouteFlow` according to existing route-flow rules.
2. WHEN evidence is syntax-only, textual, name-only, dynamic, ambiguous,
   high-fan-out, generated-code uncertain, interface-candidate,
   fallback-derived, unverified, reduced, or truncated THEN affected rows SHALL
   be capped at `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.
3. WHEN no downstream evidence is found under full route-flow coverage THEN the
   report MAY emit `NoRouteFlowEvidence`; under reduced coverage it SHALL emit
   `UnknownAnalysisGap` or a narrower gap.
4. WHEN any gap is emitted THEN it SHALL include gap kind, rule ID, evidence
   tier, coverage, source label where available, affected row ID where
   available, supporting IDs where available, file span where available, and a
   limitation.
5. WHEN limitations are rendered THEN they SHALL explicitly state that the
   report is static evidence and not proof of runtime request execution,
   traffic, production call paths, runtime DI target selection, dynamic dispatch
   selection, branch feasibility, authorization behavior, query execution,
   database state, release safety, outage cause, or business impact.
6. WHEN the report includes touched-files or touched-symbols summaries THEN
   summaries SHALL inherit the weakest classification and coverage limitations
   from the rows they summarize.

### Requirement 6: Markdown And JSON Output Contract

**User Story:** As an automation author, I want route-centered trace output to
be stable, machine-readable, and reviewable.

#### Acceptance Criteria

1. JSON output SHALL preserve the existing `reportType = "route-flow"` and
   `version = "1.0"` unless a future breaking-schema spec explicitly changes
   the contract.
2. JSON output SHALL include the existing route-flow fields and SHALL add any
   trace-completeness fields additively, such as `touchedFiles`,
   `touchedSymbols`, `methodRows`, `serviceRows`, `dataRows`, `queryRows`,
   `dependencyRows`, `argumentRows`, `coverageNotes`, or similarly named
   backward-compatible collections.
3. Markdown output SHALL keep the existing Summary, Query, Snapshot Sources,
   Entry Evidence, Static Flow, Business/Data Logic, Dependency Surfaces, Gaps,
   and Limitations sections, and MAY add narrowly named sections for touched
   files, touched symbols, and value-origin evidence.
4. WHEN identical input rows and options are used twice THEN Markdown and JSON
   SHALL be byte-stable.
5. Arrays and maps SHALL sort deterministically by source label, normalized
   selector key, classification rank, path length, row kind, safe display
   label, file path, start line, symbol ID, fact ID, edge ID, and stable row ID.
6. Output wording SHALL use phrases such as "static evidence",
   "candidate implementation", "coverage-relative", and "touched by selected
   evidence"; it SHALL NOT say "impacted", "executed", "called at runtime",
   "uses in production", "caused", "safe to release", or equivalent runtime or
   business-impact claims.

### Requirement 7: Privacy, Fixtures, And Validation

**User Story:** As a maintainer, I want implementation validation to be
public-safe and specific enough to prevent regressions.

#### Acceptance Criteria

1. Tests SHALL use public-safe or synthetic fixtures with generic labels and
   synthetic route values such as `GET /api/items/{id}`.
2. Tests SHALL cover safe selector normalization, selector miss behavior,
   route/client entry evidence, route-to-method symbol bridging, touched files,
   touched symbols, direct service calls, interface candidates, no candidates,
   multiple candidates, data/query/dependency rows, argument/value-origin rows,
   and unjoinable projection gaps.
3. Tests SHALL cover reduced coverage, unknown commit SHA, missing schema,
   missing extractor, traversal bounds, high fan-out, dynamic URL, and
   syntax-only downgrade behavior.
4. Tests SHALL prove raw SQL, raw config values, raw URLs, hostnames,
   connection strings, secrets, raw remotes, source snippets, local absolute
   paths, private route values, and private sample labels do not appear in
   Markdown, JSON, logs, or committed fixtures.
5. Tests SHALL prove every emitted route-flow rule ID resolves in
   `rules/rule-catalog.yml` and no parallel route-flow rule namespace is
   introduced.
6. Validation SHALL include `dotnet test`, a public-safe CLI smoke over a
   synthetic or checked-in fixture, `git diff --check`, the private-path guard,
   and relevant `docs/VALIDATION.md` route-flow/reporting smoke checks unless a
   deferral is recorded with rationale.
