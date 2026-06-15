# Requirements

## Introduction

TraceMap now has static evidence for several legacy .NET slices: WebForms event
entry points, WCF and service-reference metadata, service and HTTP surfaces,
call/object creation edges, SQL/query facts, and a queued legacy data metadata
extractor. These facts are useful individually, but older application demos need
one conservative view that explains possible user-action-to-backend/data paths
without implying runtime proof.

This phase adds a deterministic composition and reporting layer over already
extracted static evidence. It should help users answer questions like "what can
this button handler statically reach?" or "which UI actions have evidence near a
service operation or data surface?" while preserving rule IDs, evidence tiers,
supporting fact/edge IDs, coverage labels, limitations, commit SHA, and extractor
identity.

This is not runtime tracing, page lifecycle simulation, API monitoring, database
introspection, AI impact analysis, or probabilistic classification. TraceMap
must not claim that a UI action always reaches a backend, that a service call
executes, or that a SQL/data surface is used in production.

Public claim level: hidden until validated through checked-in public fixtures or
reviewed redacted summaries.

## Requirements

### Requirement 1: Flow Composition Inputs

**User Story:** As a maintainer, I want TraceMap to compose existing legacy
evidence without needing a clean build or a runtime environment.

Acceptance Criteria:

1. WHEN an index contains WebForms event binding, handler resolution, call edge,
   object creation, WCF/service-reference, HTTP/API, SQL/query, legacy data
   metadata, dependency-surface, coverage, or `AnalysisGap` facts THEN the flow
   composer SHALL read them as inputs without rerunning extractors.
2. WHEN one or more expected fact families are absent because the index predates
   an extractor or the extractor was not enabled THEN the composer SHALL emit an
   availability gap rather than failing or treating the evidence as clean
   absence.
3. WHEN semantic evidence is unavailable but syntax or structural evidence is
   present THEN the composer SHALL preserve the original evidence tier and SHALL
   NOT upgrade confidence because a later edge happens to connect.
4. WHEN an index has reduced scan coverage, failed project load, parse gaps,
   missing generated code, or unsupported framework behavior THEN composed flows
   SHALL carry reduced coverage or analysis-gap labels.
5. WHEN inputs include repo paths, source spans, commit SHA, extractor versions,
   fact IDs, edge IDs, rule IDs, or evidence tiers THEN outputs SHALL preserve
   that provenance in deterministic order.

### Requirement 2: Static Flow Roots

**User Story:** As a reviewer, I want flow reports to start from credible static
entry points such as WebForms user actions and HTTP/API surfaces.

Acceptance Criteria:

1. WHEN a `WebFormsHandlerResolved` fact is supported by a static event binding
   or approved auto-wireup evidence as defined by
   `legacy.webforms.event-binding.v1`, `legacy.webforms.handler-resolution.v1`,
   or successor rules THEN the composer SHALL treat it as a possible static
   root with the original page/control/event evidence.
2. WHEN a WebForms handler is a known lifecycle method such as `Page_Load`,
   `Page_Init`, `Page_PreRender`, or `Application_Start` THEN the root SHALL be
   labeled as `webforms-lifecycle` rather than `webforms-event` or
   user-action-root in report output.
3. WHEN HTTP route, API endpoint, service host, or service operation facts exist
   THEN the composer MAY treat them as service/API roots or intermediate
   surfaces, preserving their source evidence and limitations.
4. WHEN a WebForms event binding has no resolved handler under full coverage
   THEN the composer SHALL emit an `AnalysisGap` result with an
   `UnresolvedRoot` gap note, not a backend path.
5. WHEN handler resolution is ambiguous, name-only, syntax-only, or reduced by
   missing code-behind/designer/generated code THEN the root SHALL be capped at
   `NeedsReviewStaticPath`, `ReducedCoverage`, or `AnalysisGap` as appropriate.
6. The composer SHALL NOT create roots from arbitrary method names, global short
   names, raw markup text, or unsupported event-like strings without a rule-backed
   entry-point fact.

### Requirement 3: Static Path Assembly

**User Story:** As a maintainer, I want the report to connect UI/API roots to
backend and data evidence using deterministic static graph rules.

Acceptance Criteria:

1. WHEN a root method has call edges, object creations, parameter-forward edges,
   symbol relationships, WCF/service-reference mappings, HTTP client surfaces,
   SQL/query facts, legacy data metadata links, or dependency surfaces THEN the
   composer SHALL assemble bounded static paths using those existing facts and
   edges.
2. WHEN a path uses generated WCF client evidence and normalized WCF metadata
   mappings THEN it SHALL include the service-reference and operation-normalizing
   rule IDs that support the link.
3. WHEN a path reaches SQL/query or legacy data metadata evidence THEN it SHALL
   report only safe surface names, hashes, shapes, or descriptors already allowed
   by the source facts, never raw SQL or config values.
4. WHEN direct call/object evidence is missing but a structural service or data
   metadata relationship exists THEN the path SHALL be classified no stronger
   than `ProbableStaticPath` or `NeedsReviewStaticPath`, depending on ambiguity
   and coverage.
5. WHEN a traversal would require runtime DI binding, reflection target
   resolution, serializer behavior, branch feasibility, event bubbling,
   ViewState/postback state, service deployment, database existence, or user
   permissions THEN the composer SHALL stop or emit a gap instead of inferring
   the link.
6. Traversal SHALL be deterministic, bounded by configurable depth/path/frontier
   limits, cycle-safe, and stable across repeated runs on identical input.

### Requirement 4: Flow Classifications

**User Story:** As a TraceMap user, I want conservative labels that distinguish
strong static evidence from review-only or missing evidence.

Acceptance Criteria:

1. WHEN a root, traversal edges, and terminal backend/data surface are connected
   by semantic or strongly structural evidence with full coverage THEN the result
   MAY be classified as `StrongStaticPath`.
2. WHEN the path is supported by credible structural evidence but one or more
   semantic links are unavailable THEN the result SHALL be classified as
   `ProbableStaticPath`.
3. WHEN the path depends on syntax-only evidence, name-only evidence, generated
   code uncertainty, high fan-out, ambiguous candidates, or partial terminal
   evidence THEN the result SHALL be classified as `NeedsReviewStaticPath`.
4. WHEN no downstream backend/data evidence is found and relevant extractors have
   full coverage THEN the result MAY be classified as `NoBackendEvidence`.
5. WHEN no downstream backend/data evidence is found under reduced coverage or
   missing extractor availability THEN the result SHALL be classified as
   `ReducedCoverage` or `AnalysisGap`, not clean absence.
6. Every classification SHALL include the rule IDs, evidence tiers, coverage
   labels, supporting fact/edge IDs, and limitations that explain why the label
   was chosen.

### Requirement 5: Reports And Query Surface

**User Story:** As a maintainer, I want machine-readable and human-readable
legacy flow outputs that make old-codebase demos understandable.

Acceptance Criteria:

1. WHEN a user runs `tracemap paths` with legacy roots enabled against a
   supported TraceMap index THEN TraceMap SHALL write deterministic Markdown and
   JSON outputs.
2. WHEN `--include-legacy-roots`, `--from-webforms-event`, `--from-symbol`,
   `--from-endpoint`, `--to-surface`, `--classification`, `--max-depth`, `--max-paths`, or
   `--max-frontier` selectors are provided THEN the command SHALL apply them
   deterministically and report selector gaps for no matches.
3. WHEN no selectors are provided THEN the default report SHALL summarize
   WebForms roots, API/service roots, terminal backend/data surfaces,
   classifications, coverage gaps, and the top bounded static paths.
4. WHEN older indexes do not contain newer fact types or graph tables THEN the
   command SHALL continue and report `SchemaMissing` or `ExtractorUnavailable`
   gaps.
5. Markdown SHALL phrase results as "static evidence", "possible static path",
   or "no backend evidence found under available coverage; absence is not
   proven"; it SHALL NOT say that a UI action executed, caused, always reaches,
   or is proven to impact a backend.
6. JSON output SHALL use a documented versioned schema with stable property
   names and deterministic ordering.

### Requirement 6: Privacy And Public Artifact Safety

**User Story:** As a maintainer, I want flow artifacts safe enough for public
   demos after review and redaction.

Acceptance Criteria:

1. Generated flow outputs SHALL NOT include raw local absolute paths, private
   repository names, raw remotes, secrets, connection strings, config values,
   raw SQL, endpoint addresses, WSDL URLs, service URLs, source snippets, or
   unreviewed sample identifiers.
2. File paths in committed or public artifacts SHALL be repo-relative or neutral
   labels only.
3. Unsafe terminal names and values SHALL be hashed, omitted, or represented by
   safe shape descriptors following the source fact's redaction policy.
4. WHEN a required value is unsafe to display THEN the report SHALL show a safe
   hash/label and include the redaction rule ID rather than leaking the value.
5. Logs SHALL avoid echoing raw selector values when they look like paths,
   remotes, connection strings, raw SQL, URLs, or secrets.

### Requirement 7: Compatibility With Existing Reports

**User Story:** As a TraceMap maintainer, I want this composition layer to reuse
existing reporting code instead of creating an isolated legacy-only graph.

Acceptance Criteria:

1. The implementation SHALL reuse existing index readers, combined/reporting
   models, graph helpers, rule catalog constants, and redaction helpers where
   practical.
2. WHEN existing `tracemap report`, `tracemap paths`, reverse, impact, release
   review, or portfolio commands encounter legacy flow facts or outputs THEN
   they SHALL either consume them through shared models or ignore them safely
   with explicit availability gaps where relevant.
3. The implementation SHALL NOT change scanner fact schemas incompatibly unless
   a migration or backward-compatible reader behavior is defined.
4. The implementation SHALL NOT require the queued legacy data metadata
   extraction to be complete; when those facts are missing it SHALL label the
   corresponding terminal evidence as unavailable.
5. The implementation SHALL keep the core scanner/reducer free of LLM calls,
   embeddings, vector databases, or prompt-based classification.

### Requirement 8: Validation

**User Story:** As a reviewer, I want focused fixtures and safety checks that
prove the flow composer is deterministic, conservative, and redacted.

Acceptance Criteria:

1. WHEN this spec is implemented THEN tests SHALL cover WebForms event roots,
   direct handler-to-service paths, normalized WCF client/service-reference
   paths, HTTP/API surfaces, SQL/query surfaces, legacy data metadata when
   available, reduced coverage, ambiguous paths, missing schema, selector gaps,
   truncation, and privacy suppression.
2. Tests SHALL prove that syntax-only, name-only, ambiguous, high fan-out, or
   reduced-coverage evidence cannot produce `StrongStaticPath`.
3. Tests SHALL prove deterministic ordering and byte-stable JSON for identical
   input.
4. Tests SHALL prove raw SQL, connection strings, URLs, local absolute paths,
   private labels, remotes, config values, and source snippets do not appear in
   Markdown, JSON, logs, or SQLite-derived display fields.
5. Implementation validation SHALL include `dotnet build`, `dotnet test`,
   private-path guard, `git diff --check`, and any relevant pinned smoke checks
   from `docs/VALIDATION.md`, or explicitly defer smoke checks with rationale in
   implementation state.
