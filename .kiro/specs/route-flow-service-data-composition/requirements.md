# Route Flow Service/Data Composition Requirements

## Introduction

TraceMap route-flow reporting can identify a route entry for a normalized API
route, but current output may stop at the entry even when the combined index
contains static facts that can support more useful downstream evidence. This
spec defines a conservative extension to the existing `tracemap route-flow`
report that projects existing combined evidence from route/controller entries
through service, repository-like, object-shape, query-shape, and data-surface
rows.

The feature is a deterministic reporting/query improvement over existing index
data. It is not runtime tracing, live request execution, runtime dependency
injection resolution, dynamic dispatch proof, database introspection, AI impact
analysis, or prompt-based classification.

Public claim level: static evidence only. Any private validation must be
described with generic wording such as "private legacy ASP.NET smoke sample" and
"normalized API route".

## Requirements

### Requirement 1: Route Flow Inputs

**User Story:** As a maintainer, I want route-flow output to use already
combined evidence rows so that route entries can be explained without rerunning
scanners.

#### Acceptance Criteria

1. WHEN a combined index contains route entry evidence, controller or handler
   symbols, `combined_call_edges`, `combined_object_creations`,
   `combined_argument_flows`, `combined_parameter_forward_edges`,
   `combined_fact_symbols`, `combined_symbol_relationships`,
   `combined_dependency_edges`, `FactTypes.ObjectShapeInferred`,
   `FactTypes.QueryPatternDetected`, dependency/data facts represented in
   `combined_facts`, coverage metadata, or `AnalysisGap` facts THEN the
   route-flow composer SHALL read them as optional inputs.
2. WHEN `combined_argument_flows` exist THEN the composer SHALL project usable
   direct argument evidence into route-flow detail rows without claiming full
   taint analysis, mutation tracking, branch feasibility, or runtime value
   contents.
3. WHEN `combined_fact_symbols` exist THEN the composer SHALL project credible
   fact-to-symbol attachments from `combined_facts` into route-flow detail rows,
   preserving supporting fact IDs, symbol IDs, rule IDs, evidence tiers, file
   paths, line spans, commit SHA, and extractor versions.
4. WHEN expected input tables or fact families are missing, empty, disabled, or
   from an older schema THEN the composer SHALL emit an availability gap rather
   than failing or treating the evidence as clean absence.
5. WHEN an index has reduced semantic coverage, failed project load, syntax-only
   fallback, unknown commit SHA, missing generated code, or known framework
   gaps THEN route-flow output SHALL carry reduced-coverage labels or
   `UnknownAnalysisGap`.
6. WHEN input rows include unsafe display values THEN projection SHALL use safe
   labels, hashes, descriptors, or omitted values according to the redaction
   rules in this spec.

### Requirement 2: Route Entry To Downstream Static Evidence

**User Story:** As a reviewer, I want a route-flow report to show downstream
method, service, repository, and data evidence when static facts support the
connections.

#### Acceptance Criteria

1. WHEN route entry evidence is statically attached to a controller or handler
   method symbol THEN the composer SHALL use that symbol as a route-flow root.
2. WHEN a route-flow root has call, creation, argument-flow, parameter-forward,
   or fact-symbol evidence to a downstream method THEN the composer SHALL emit a
   downstream method detail row with the supporting evidence IDs and tiers.
3. WHEN a downstream method has `FactTypes.ObjectShapeInferred`, DTO/type, or
   member-shape facts attached by credible symbol evidence THEN the composer
   SHALL emit object-shape detail rows capped by the weakest supporting evidence
   tier.
4. WHEN a downstream method, repository-like method, or data-access symbol has
   `FactTypes.QueryPatternDetected` or dependency/data evidence attached by
   credible facts THEN the composer SHALL emit query-shape or data-surface rows
   using only safe descriptors and hashes.
5. WHEN evidence can connect the route root to service/repository-like/data facts
   by static method calls and symbol attachments THEN the report MAY classify the
   result as a static downstream route-flow row.
6. WHEN the composer sees plausible downstream facts in the same source but no
   credible static bridge from the route root THEN it SHALL emit a narrower
   bridge gap rather than inventing a flow.
7. WHEN no downstream facts are found and relevant coverage is complete THEN the
   report MAY emit `NoRouteFlowEvidence`; under reduced or missing coverage it
   SHALL emit reduced-coverage labels or `UnknownAnalysisGap` instead.

### Requirement 3: Interface And Implementation Candidates

**User Story:** As a maintainer, I want interface calls to expose static
implementation candidates without pretending TraceMap knows the runtime binding.

#### Acceptance Criteria

1. WHEN a route-flow path reaches an interface method call and static
   `implements`, `inherits`, override, or symbol relationship facts identify
   candidate implementation methods THEN the composer SHALL emit candidate rows
   labeled as static implementation candidates.
2. Candidate rows SHALL include the interface method, candidate implementation
   symbol, relationship evidence, source label, rule IDs, evidence tiers,
   supporting fact or edge IDs, file paths, and line spans where available.
3. Candidate rows SHALL NOT claim runtime dependency injection resolution,
   service locator binding, configuration binding, container registration,
   dynamic dispatch target selection, or production execution.
4. WHEN exactly one static candidate is found through interface relationship
   evidence, the candidate row SHALL be capped at `NeedsReviewStaticRouteFlow`
   or lower and SHALL use wording that says "candidate"; a stronger row requires
   separate direct non-interface call evidence to the implementation method.
5. WHEN multiple candidates are found, candidates SHALL be sorted
   deterministically and capped at `NeedsReviewStaticRouteFlow` or lower.
6. WHEN candidate relationships are name-only, syntax-only, ambiguous, high
   fan-out, or reduced-coverage THEN the composer SHALL emit needs-review or
   unknown-gap labels rather than stronger conclusions.
7. WHEN no implementation candidate can be found, the composer SHALL preserve
   the interface call evidence and emit an `ImplementationCandidateUnavailable`
   gap.

### Requirement 4: Classifications, Rule IDs, And Limitations

**User Story:** As a TraceMap user, I want every route-flow conclusion to be
bounded by rule-backed evidence and documented limitations.

#### Acceptance Criteria

1. Every route-flow result, detail row, and gap SHALL include at least one rule
   ID and SHALL preserve source evidence tiers.
2. Existing `combined.route-flow.*` rule IDs SHALL be reused wherever they
   already cover the behavior; new rule IDs SHALL be added only for new
   projection behavior before implementation emits those rows, with documented
   limitations for each new rule.
3. Required rule handling:
   - reuse `combined.route-flow.selector.v1`;
   - reuse `combined.route-flow.entry.v1`;
   - reuse `combined.route-flow.path.v1`;
   - reuse `combined.route-flow.interface-bridge.v1`;
   - reuse `combined.route-flow.logic-surface.v1`;
   - reuse `combined.route-flow.dependency-surface.v1`;
   - reuse `combined.route-flow.classification.v1`;
   - extend `combined.route-flow.gap.v1` only for newly introduced gap codes;
   - reuse `combined.route-flow.redaction.v1`;
   - reuse `combined.route-flow.report.v1`;
   - add `combined.route-flow.argument-projection.v1`;
   - add `combined.route-flow.fact-symbol-projection.v1` if existing
     `combined.route-flow.logic-surface.v1` cannot carry the row provenance
     without ambiguity.
4. Classifications SHALL use the existing `RouteFlowClassifications` vocabulary:
   `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
   `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
   `UnknownAnalysisGap`. Reduced coverage is a report coverage label and gap
   reason, not a summary classification.
5. Semantic compiler-resolved evidence MAY support stronger classification than
   structural or syntax-only evidence, but composed rows SHALL never upgrade the
   original evidence tier merely because another row connects.
6. Syntax-only, textual, name-only, ambiguous, high fan-out, interface-candidate,
   reduced-coverage, or missing-extractor evidence SHALL cap results at
   `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`, with reduced coverage
   recorded as coverage metadata or a gap reason.
7. Limitations SHALL state that route-flow rows are static evidence, not runtime
   request execution proof, runtime route reachability, runtime dependency
   injection binding proof, branch feasibility proof, query execution proof, or
   database schema proof.

### Requirement 5: Deterministic Output Contract

**User Story:** As an automation author, I want route-flow output to be stable
and machine-readable.

#### Acceptance Criteria

1. WHEN route-flow composition runs against identical input rows THEN Markdown
   and JSON output SHALL be byte-stable, excluding intentionally external build
   metadata already present in the index.
2. Results SHALL sort deterministically by classification rank, route identity
   safe key, source label, path length, detail row kind, downstream safe display
   label, file path, start line, stable symbol or fact ID, and stable row ID.
3. Stable row IDs SHALL be derived from ordered safe source labels, route-flow
   root identity, supporting fact IDs, supporting edge IDs, symbol IDs, file
   paths, and line spans.
4. JSON output SHALL extend the existing `route-flow-report.json` contract
   emitted by `CombinedRouteFlowReporter`, preserving `reportType = "route-flow"`
   and the existing version unless a backward-incompatible change is explicitly
   specified in a future spec.
5. Markdown output SHALL align with the existing `route-flow-report.md` renderer
   while adding route entry, downstream methods, object shapes, query shapes,
   data surfaces, candidate implementations, gaps, and limitations in a
   consistent order.
6. Output SHALL include source labels, scan IDs, commit SHAs, extractor
   identities, rule IDs, evidence tiers, file paths, and line spans when those
   fields are available and safe to display.
7. Missing values SHALL use explicit `null`, empty arrays, or closed-set gap
   codes consistently; output SHALL NOT rely on omitted fields to communicate
   uncertainty.

### Requirement 6: Safe Rendering And Redaction

**User Story:** As a maintainer, I want committed specs and generated reports to
be safe for public review.

#### Acceptance Criteria

1. The implementation SHALL NOT render raw source snippets, raw SQL, raw config
   values, connection strings, secrets, raw local absolute paths, private sample
   names, private repository names, private route strings, raw endpoint URLs, or
   raw remote URLs in route-flow Markdown, JSON, logs, or committed fixtures.
2. File paths in public artifacts SHALL be repo-relative, synthetic fixture
   paths, or neutral labels.
3. Route values SHALL be normalized route keys or safe labels; private route
   strings SHALL be omitted or hashed.
4. SQL and data evidence SHALL use shape descriptors, stable hashes, table or
   object safe labels where allowed by source facts, and redaction notes rather
   than raw query text.
5. Config and secret-like values SHALL be omitted, hashed, or represented by
   closed-set descriptors.
6. Logs SHALL avoid echoing raw selector values when those values look like
   paths, remotes, URLs, connection strings, raw SQL, secrets, or private route
   strings.
7. Redacted rows SHALL include the redaction rule ID and preserve enough
   provenance for review without leaking the unsafe value.

### Requirement 7: Validation And Acceptance Smoke

**User Story:** As a reviewer, I want focused validation that proves the feature
is conservative, useful, and safe.

#### Acceptance Criteria

1. Tests SHALL cover route entry to direct downstream method rows using call
   evidence.
2. Tests SHALL cover route entry to service interface calls with one candidate,
   multiple candidates, and no candidate.
3. Tests SHALL cover route entry to repository/data-access methods and
   data-surface rows where static facts support the path.
4. Tests SHALL cover `combined_argument_flows` projection and
   `combined_fact_symbols` projection.
5. Tests SHALL cover object-shape, query-shape, and data-surface detail rows.
6. Tests SHALL prove that interface candidates, syntax-only evidence, name-only
   evidence, ambiguity, high fan-out, missing extractors, and reduced coverage
   cannot produce the strongest classification.
7. Tests SHALL prove deterministic ordering and byte-stable JSON for identical
   input.
8. Tests SHALL prove unsafe values do not appear in Markdown, JSON, logs, or
   SQLite-derived display fields.
9. Tests SHALL include route-flow non-regression coverage for existing rule IDs,
   classification values, JSON version/report type, CLI wiring, and Markdown
   section compatibility.
10. Tests SHALL prove every emitted rule ID resolves to a rule catalog entry and
    no duplicate/conflicting route-flow rule family is introduced.
11. Tests SHALL cover negative joins where `combined_argument_flows` or
    `combined_fact_symbols` rows exist but cannot be credibly connected to the
    route-flow path.
12. Tests SHALL cover `AmbiguousImplementationCandidates`,
    `ControllerToServiceBridgeMissing`, reduced-coverage byte-stable JSON, and
    redaction of argument-flow display fields.
13. WHEN argument-flow or fact-symbol projection emits rows for a selected
    route/table THEN tests SHALL prove the old present-but-unprojected
    `ExtractorUnavailable` gap is not emitted for that route/table; WHEN rows
    exist but cannot be credibly joined THEN tests SHALL prove the narrower
    projection-unavailable gap is emitted.
14. Tests SHALL cover `combined_parameter_forward_edges` as a route-flow bridge,
    or implementation state SHALL explicitly defer that coverage with rationale.
15. Validation SHALL include `git diff --check`, the private path guard if
    available, relevant spec lint/check scripts if present, `dotnet test` for the
    implementation slice, and any relevant pinned smoke checks from
    `docs/VALIDATION.md` or an explicit deferral in implementation state.
16. Acceptance smoke direction SHALL use generic wording only: a private legacy
    ASP.NET smoke sample with a normalized API route should produce either static
   downstream service/data rows with rule IDs, evidence tiers, file spans, and
   supporting fact IDs, or narrower evidence-backed gaps explaining which bridge
   or projection is missing.
