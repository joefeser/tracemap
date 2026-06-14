# API and DTO Contract Diff Requirements

## Introduction

TraceMap can scan repositories, combine indexes, compare dependency evidence, and run contract-delta impact. It does not yet have a focused workflow for comparing API and DTO contract surfaces between two indexed snapshots.

API and DTO Contract Diff answers: between two TraceMap indexes, which endpoint routes, endpoint handler identities, request/response DTO shapes, type/member contracts, and route metadata changed according to static evidence?

This is static evidence comparison. It is not OpenAPI generation, binary compatibility analysis, runtime traffic analysis, deployment reachability, serializer runtime mapping proof, or source-code diffing.

## Scope

In scope:

- Compare API and DTO contract evidence between two single-language indexes or two combined indexes.
- Compare endpoint facts, route metadata, handler symbols, request/response type facts, DTO/type/property facts, schema-like field facts, and endpoint alignment facts where available.
- Validate source identity, commit SHA, scan coverage, analysis level, and extractor versions before claiming added/removed/changed contract evidence.
- Emit deterministic Markdown and JSON reports with rule IDs, evidence tiers, file spans, commit SHAs, extractor versions, supporting fact IDs, source labels, and limitations.
- Preserve reduced-coverage, syntax-only, source-identity, duplicate-identity, and schema-gap caveats.
- Provide stable identities for endpoints, DTOs, properties, method signatures, and route shapes.
- Integrate cleanly with future Contract Delta Impact V2 and Release Review Report workflows.

Out of scope:

- No OpenAPI or Swagger generation claims unless future evidence explicitly supports them.
- No binary compatibility guarantees.
- No runtime request sampling or traffic inference.
- No deployment/base-path/proxy/auth reachability inference.
- No serializer runtime alias, reflection, dynamic dispatch, runtime dependency injection, collection contents, mutation, or branch feasibility inference.
- No source text diffing or AST diffing outside indexed facts.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Requirements

### Requirement 1: Command and Inputs

**User Story:** As a release reviewer, I want a command that compares API and DTO contract evidence between two TraceMap indexes.

#### Acceptance Criteria

1. WHEN the user runs `tracemap contract-diff --before <index.sqlite> --after <index.sqlite> --out <path>` THEN TraceMap SHALL compare API and DTO contract evidence and emit a deterministic report.
2. WHEN both inputs are single-language indexes from the same repository identity THEN TraceMap SHALL compare them in `ApiDtoContractDiffSingleV1` mode.
3. WHEN both inputs are combined indexes THEN TraceMap SHALL compare them in `ApiDtoContractDiffCombinedV1` mode.
4. WHEN one input is combined and the other is single-language THEN TraceMap SHALL fail with a clear message; mixed-mode comparison is deferred.
5. WHEN either input is not a valid TraceMap index THEN the command SHALL fail with a clear schema error naming the side and missing object.
6. WHEN the command runs THEN it SHALL open both indexes read-only and SHALL NOT mutate either input.
7. WHEN output path is a directory or has no extension THEN TraceMap SHALL write `contract-diff-report.md` and `contract-diff-report.json`.
8. WHEN output path is a file and no format is provided THEN TraceMap SHALL write Markdown.
9. WHEN `--format json` is provided with a file output THEN TraceMap SHALL write deterministic JSON to that file even if the extension is not `.json`.
10. WHEN `--exit-code` is provided THEN TraceMap SHALL return `1` only when `Added`, `Removed`, or `ChangedEvidence` rows exist; `NeedsReviewDiff`, coverage-relative rows, no-evidence rows, and gap-only reports SHALL return `0`.

### Requirement 2: Source Identity and Coverage

**User Story:** As a maintainer, I want TraceMap to prove that snapshots are comparable before it reports contract changes.

#### Acceptance Criteria

1. WHEN comparing single-language indexes THEN TraceMap SHALL compare repo identity, language, project root hash where available, scan ID, commit SHA, build status, analysis level, and extractor versions.
2. WHEN comparing combined indexes THEN TraceMap SHALL pair sources by source label and validate source identity per source before comparing evidence.
3. WHEN source identity is missing or unverified THEN contract rows for that source SHALL be classified no stronger than review-tier and SHALL include `SourceIdentityUnverified`.
4. WHEN paired sources conflict on repository identity, language, or source label ownership THEN TraceMap SHALL emit `SourceIdentityConflict` and SHALL NOT compare rows for that source unless a future explicit override is specified.
5. WHEN commit SHA is missing on either side THEN TraceMap SHALL emit `UnknownCommitSha` and SHALL avoid claims that depend on complete history.
6. WHEN coverage is reduced on the before side THEN newly observed after evidence SHALL be classified as coverage-relative, not definite addition.
7. WHEN coverage is reduced on the after side THEN missing after evidence SHALL be classified as coverage-relative, not definite removal.
8. WHEN optional precision tables are absent THEN TraceMap SHALL emit schema gaps and downgrade conclusions that require those tables.
9. WHEN semantic analysis failed or syntax fallback was used for relevant evidence THEN TraceMap SHALL label syntax-only and reduced-coverage caveats near affected rows.

### Requirement 3: Endpoint Contract Evidence

**User Story:** As an API reviewer, I want endpoint route and handler changes compared using stable static evidence.

#### Acceptance Criteria

1. WHEN endpoint evidence exists THEN TraceMap SHALL compare endpoint kind, HTTP method, normalized path key, route template, route parameters, handler symbol, containing type, source label, and rule-backed evidence where available.
2. WHEN endpoint method and normalized path key are both available THEN they SHALL be the primary endpoint identity components.
3. WHEN only a path or display route is available THEN endpoint identity SHALL be review-tier and SHALL not be used for strong removed/added claims.
4. WHEN handler symbol identity changes for the same method/path identity THEN TraceMap SHALL classify the endpoint as `ChangedEvidence` or review-tier equivalent, not as a removed plus added route unless the route identity also changes.
5. WHEN route parameter names or count change under the same route identity THEN TraceMap SHALL classify the route as changed and include safe parameter metadata.
6. WHEN endpoint evidence comes from syntax-only route extraction THEN TraceMap SHALL classify no stronger than review-tier unless supporting structural or semantic evidence exists.
7. WHEN endpoint display strings contain unsafe values, raw URLs, local absolute paths, or snippets THEN TraceMap SHALL hash or omit unsafe values.

### Requirement 4: DTO Type and Property Evidence

**User Story:** As a contract reviewer, I want DTO type and member shape changes compared without requiring generated OpenAPI.

#### Acceptance Criteria

1. WHEN DTO/type evidence exists THEN TraceMap SHALL compare fully qualified type name, namespace, assembly/package/module, language, symbol ID, source label, and safe display name where available.
2. WHEN property/member evidence exists THEN TraceMap SHALL compare containing type identity, property/member name, declared type, nullability/required metadata where available, JSON/schema alias when explicitly indexed, and evidence tier.
3. WHEN a property changes declared type, requiredness, nullability, JSON/schema alias, or containing type identity THEN TraceMap SHALL classify it as changed.
4. WHEN a property is added or removed under full comparable coverage THEN TraceMap SHALL classify it as added or removed.
5. WHEN a type/member is visible only through syntax or text evidence THEN TraceMap SHALL classify no stronger than review-tier.
6. WHEN a property name is generic or high fan-out, such as `id`, `type`, `name`, or `status`, and lacks containing type identity THEN TraceMap SHALL classify no stronger than review-tier.
7. WHEN serializer-specific aliases are not indexed THEN TraceMap SHALL emit `SerializerMappingUnavailable` rather than infer aliases from naming conventions.
8. WHEN generated code or external contract artifacts are not scanned THEN TraceMap SHALL emit coverage limitations rather than infer absence.
9. WHEN a language adapter lacks semantic DTO member declared-type, nullability, requiredness, or alias metadata THEN TraceMap SHALL compare only the metadata actually indexed and SHALL classify affected rows no stronger than review-tier.

### Requirement 5: Request and Response Shape Evidence

**User Story:** As an API maintainer, I want TraceMap to compare the request/response DTOs attached to endpoints when evidence exists.

#### Acceptance Criteria

1. WHEN endpoint request type evidence exists THEN TraceMap SHALL compare endpoint-to-request DTO attachments by stable endpoint identity and stable DTO identity.
2. WHEN endpoint response type evidence exists THEN TraceMap SHALL compare endpoint-to-response DTO attachments by stable endpoint identity and stable DTO identity.
3. WHEN an endpoint changes request or response type under stable endpoint identity THEN TraceMap SHALL emit `ChangedEvidence`.
4. WHEN request/response shape evidence is inferred only from syntax or framework conventions THEN TraceMap SHALL label it review-tier.
5. WHEN multiple response types or status-code-specific responses are indexed THEN TraceMap SHALL preserve status code and response kind in stable identity.
6. WHEN status-code-specific response evidence is unavailable THEN TraceMap SHALL not infer response status behavior.
7. WHEN endpoint-to-DTO attachment evidence is absent because no adapter emits that attachment fact for the source THEN TraceMap SHALL emit `AttachmentEvidenceUnavailable` or equivalent gap and SHALL NOT claim request/response attachment changes.
8. WHEN endpoint-to-DTO attachment evidence is absent due to reduced coverage THEN TraceMap SHALL emit `UnknownAnalysisGap` or coverage-relative no-evidence, not proof of no request/response contract.
9. WHEN implementation inventory finds no credible request/response attachment evidence for v1 THEN attachment comparison SHALL remain a gap/report section only and SHALL NOT block endpoint or DTO type/property diff implementation.

### Requirement 6: Stable Identity and Diff Semantics

**User Story:** As a reviewer, I want stable identities that avoid row churn and avoid collapsing distinct contracts.

#### Acceptance Criteria

1. WHEN endpoint rows are compared THEN stable identity SHALL include source identity, endpoint kind, HTTP method, and normalized path key when available; handler symbol SHALL be compared as metadata under that route identity, not included in the primary endpoint identity.
2. WHEN DTO type rows are compared THEN stable identity SHALL include source identity, language, fully qualified type name or symbol ID, and assembly/package/module where available.
3. WHEN DTO property rows are compared THEN stable identity SHALL include source identity, containing type identity, property/member name, and JSON/schema alias only as supporting metadata unless alias is the contract identity.
4. WHEN method signature rows are compared THEN stable identity SHALL include source identity, containing type, method name, arity, fully qualified parameter types where available, and return type where available.
5. WHEN route shape rows are compared THEN stable identity SHALL include source identity, method, normalized path key, and route parameter signature.
6. WHEN a stable identity cannot be built without volatile row IDs or unsafe source text THEN TraceMap SHALL use a deterministic hash over safe metadata and classify the row no stronger than review-tier.
7. WHEN duplicate stable identities appear within one snapshot THEN TraceMap SHALL emit `DuplicateContractIdentity`, preserve duplicate provenance in JSON, and downgrade affected rows.
8. WHEN same display name but different source label, language, assembly, or package identity appears THEN TraceMap SHALL keep those rows distinct.
9. WHEN only volatile database IDs differ THEN TraceMap SHALL NOT classify the row as changed.

### Requirement 7: Classifications

**User Story:** As a reviewer, I want classifications that separate strong static changes from uncertain evidence.

#### Acceptance Criteria

1. WHEN comparable evidence exists only after and coverage is credible on both sides THEN classify as `Added`.
2. WHEN comparable evidence exists only before and coverage is credible on both sides THEN classify as `Removed`.
3. WHEN comparable evidence exists on both sides with changed safe metadata THEN classify as `ChangedEvidence`.
4. WHEN evidence appears after but before coverage was reduced or relevant gaps exist THEN classify as `AddedWithBeforeGap`.
5. WHEN evidence disappears after but after coverage was reduced or relevant gaps exist THEN classify as `RemovedWithAfterGap`.
6. WHEN evidence is syntax-only, name-only, ambiguous, duplicated, high fan-out, or identity-unverified THEN classify as `NeedsReviewDiff`.
7. WHEN credible full coverage shows no comparable evidence for selected scope THEN classify report-level result as `NoDiffEvidence`.
8. WHEN selectors match no evidence in either snapshot THEN emit `SelectorNoMatch`.
9. WHEN analysis gaps prevent a credible conclusion THEN classify as `UnknownAnalysisGap`.
10. WHEN output includes confidence THEN confidence SHALL be fixed from classification and documented.
11. WHEN multiple evidence tiers support one row THEN strongest allowed classification wins, but lower-tier evidence remains available as supporting evidence.

### Requirement 8: Selectors and Scopes

**User Story:** As an investigator, I want to narrow the diff to a contract area without changing the meaning of classifications.

#### Acceptance Criteria

1. WHEN `--scope` is omitted THEN TraceMap SHALL compare `endpoints`, `dto-types`, `dto-properties`, `methods`, `request-response`, and `route-shapes` where evidence exists.
2. WHEN `--scope` is provided THEN it SHALL accept a comma-separated list of `all`, `endpoints`, `dto-types`, `dto-properties`, `methods`, `request-response`, and `route-shapes`.
3. WHEN `--source <label>` is provided with a combined index THEN comparison SHALL be limited to the source label on both sides.
4. WHEN `--endpoint "<METHOD> <PATH_KEY>"` is provided THEN TraceMap SHALL parse exactly one ASCII space between the uppercase method token and non-empty normalized path key; normalized path keys SHALL NOT contain unescaped whitespace, and invalid selectors SHALL fail clearly.
5. WHEN `--type <fully-qualified-or-display-name>` is provided THEN TraceMap SHALL filter DTO/type rows by safe exact identity or display name matching.
6. WHEN `--property <name>` is provided THEN TraceMap SHALL filter property rows by exact property/member name and SHALL label generic property-only filters as review-tier.
7. WHEN `--change-kind <kind>` is provided THEN TraceMap SHALL filter rows by contract row kind using the closed values `endpoint`, `dto-type`, `dto-property`, `method`, `request-response`, and `route-shape`.
8. WHEN `--change-kind` receives an unknown value THEN TraceMap SHALL fail clearly with a sanitized selector error.
9. WHEN selectors are ignored because their scopes are disabled THEN query metadata SHALL record ignored selector/scope combinations.
10. WHEN selectors match only one side THEN TraceMap SHALL include one-sided rows rather than requiring both snapshots to match.
11. WHEN caps `--max-diff-rows`, `--max-evidence-rows`, or `--max-gaps` are provided THEN TraceMap SHALL apply deterministic ordering, emit `TruncatedByLimit` when rows are omitted, and preserve enough summary counts to show truncation occurred.

### Requirement 9: Markdown and JSON Output

**User Story:** As a human or automation author, I want safe deterministic reports.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL appear in this order: Summary, Compared Snapshots, Sources and Coverage, Endpoint Contract Diffs, DTO Type Diffs, DTO Property Diffs, Method Signature Diffs, Request/Response Attachment Diffs, Route Shape Diffs, Gaps, Limitations.
2. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `query`, `beforeSnapshot`, `afterSnapshot`, `summary`, `sourcePairs`, `endpointDiffs`, `dtoTypeDiffs`, `dtoPropertyDiffs`, `methodDiffs`, `requestResponseDiffs`, `routeShapeDiffs`, `gaps`, `coverageWarnings`, and `limitations`.
3. WHEN a diff row is emitted THEN it SHALL include stable ID, row kind, classification, confidence, rule ID, before evidence, after evidence, source label, commit SHAs, evidence tiers, fact rule IDs, file spans, extractor versions, supporting fact IDs, and safe display metadata.
4. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.
5. WHEN arrays or metadata maps are emitted THEN ordering SHALL be deterministic and metadata keys SHALL be sorted.
6. WHEN identical inputs and options are run twice THEN Markdown and JSON SHALL be byte-stable.
7. WHEN values may contain raw snippets, raw SQL, config values, connection strings, raw URLs, local absolute paths, or unsafe Markdown characters THEN output SHALL omit, hash, or escape them using shared safe helpers.
8. WHEN row caps are hit THEN Markdown and JSON SHALL emit `TruncatedByLimit` gaps and preserve deterministic ordering.

### Requirement 10: Rules and Limitations

**User Story:** As a maintainer, I want every contract-diff conclusion backed by documented rules.

#### Acceptance Criteria

1. WHEN a diff row is emitted THEN it SHALL include a contract-diff rule ID.
2. WHEN supporting evidence is emitted THEN it SHALL preserve source fact rule IDs and evidence tiers.
3. WHEN endpoint diff rows are emitted THEN they SHALL cite `api.dto.contract.diff.endpoint.v1` or the implementation's documented equivalent.
4. WHEN DTO type/property rows are emitted THEN they SHALL cite `api.dto.contract.diff.dto.v1` or the implementation's documented equivalent.
5. WHEN request/response attachment rows are emitted THEN they SHALL cite `api.dto.contract.diff.attachment.v1` or the implementation's documented equivalent.
6. WHEN gaps are emitted THEN each gap SHALL include rule ID, evidence tier, and limitation.
7. WHEN implementation adds new rule IDs THEN `rules/rule-catalog.yml` SHALL document them before implementation merges.
8. WHEN limitations are rendered THEN they SHALL state that static API/DTO contract evidence is not OpenAPI completeness, runtime traffic, serializer runtime mapping, binary compatibility, deployment reachability, or auth behavior proof.
9. WHEN identity gaps are emitted THEN they SHALL cite `api.dto.contract.diff.identity.v1` or the implementation's documented equivalent.
10. WHEN coverage gaps are emitted THEN they SHALL cite `api.dto.contract.diff.coverage.v1` or the implementation's documented equivalent.
11. WHEN selector gaps are emitted THEN they SHALL cite `api.dto.contract.diff.selector.v1` or the implementation's documented equivalent.
12. WHEN truncation gaps are emitted THEN they SHALL cite `api.dto.contract.diff.truncation.v1` or the implementation's documented equivalent.
13. WHEN the first implementation PR emits any `api.dto.contract.diff.*` row or gap THEN the same PR or an earlier merged PR SHALL include rule catalog entries and limitations for every emitted rule ID.

### Requirement 11: Validation and Tests

**User Story:** As a maintainer, I want tests that prevent false certainty and output churn.

#### Acceptance Criteria

1. Tests SHALL cover endpoint added, removed, changed route metadata, and changed handler evidence.
2. Tests SHALL cover DTO type added/removed and DTO property added/removed/changed declared type.
3. Tests SHALL cover `AttachmentEvidenceUnavailable` gaps when no adapter emits stable endpoint-to-DTO attachment evidence; tests for changed request/response attachment rows are deferred until an adapter indexes credible attachment evidence.
4. Tests SHALL cover same route display name across different source labels staying distinct.
5. Tests SHALL cover generic property names downgrading when containing type identity is missing.
6. Tests SHALL cover reduced before coverage producing `AddedWithBeforeGap`.
7. Tests SHALL cover reduced after coverage producing `RemovedWithAfterGap`.
8. Tests SHALL cover source identity conflict producing gaps rather than strong diff rows.
9. Tests SHALL cover syntax-only evidence classifying no stronger than review-tier.
10. Tests SHALL prove unsafe values do not render in Markdown or JSON.
11. Tests SHALL prove JSON and Markdown byte-stability for identical inputs.
12. Tests SHALL prove indexes are opened read-only and not mutated.
13. Tests SHALL prove selector parsing and selector-no-match behavior.
14. Tests SHALL cover `--change-kind` filtering and invalid `--change-kind` values.
15. Tests SHALL cover comma-separated `--scope` behavior and default scope behavior.
16. Tests SHALL cover duplicate stable identity downgrade through `DuplicateContractIdentity`.
17. Tests SHALL cover route parameter rename/count change under stable route identity.
18. Tests SHALL cover `TruncatedByLimit` cap behavior with deterministic ordering.
19. Tests SHALL cover status-code/response-kind metadata preservation when indexed and no status inference when absent.
20. Tests SHALL cover `SerializerMappingUnavailable` instead of alias inference from naming conventions.
21. Tests SHALL cover handler-symbol change under stable route identity as `ChangedEvidence`, not removed plus added.
22. Tests SHALL include implementation validation with `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check`.
