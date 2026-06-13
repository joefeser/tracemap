# Combined Dependency Reporting Requirements

## Introduction

TraceMap can now scan multiple language families and combine their indexes into one provenance-preserving SQLite database. The next missing product layer is a readable dependency report over that combined database.

The goal is to answer review questions like:

- Which apps or packages depend on which other apps, endpoints, symbols, SQL tables, and external packages?
- Which client HTTP calls line up with server endpoints?
- Which endpoints, SQL surfaces, packages, call edges, object creations, and parameter-forwarding paths have evidence?
- Where is evidence reduced, dynamic, ambiguous, or missing?

This is still deterministic static analysis. The report must not claim runtime traffic, runtime dependency injection bindings, branch feasibility, authentication behavior, deployment routing, database schema existence, or dynamic dispatch targets unless a future rule-backed extractor emits that evidence explicitly.

## Current State

- `tracemap combine` imports one or more `index.sqlite` files into a combined database with `index_sources`, `combined_facts`, `combined_symbols`, `combined_symbol_relationships`, `combined_call_edges`, `combined_object_creations`, `combined_argument_flows`, `combined_local_aliases`, `combined_field_aliases`, `combined_parameter_forward_edges`, an empty `endpoint_matches` table, and a `combined_dependency_edges` view.
- `tracemap export` can export a combined index as JSON or Mermaid, but it is graph/data oriented rather than a human dependency assessment.
- `tracemap endpoints` can align exactly one client index and one server index. It does not operate on N-way combined databases.
- The CLI has a `tracemap report --index <path> --out <path>` skeleton.

## MVP Scope Decisions

- Implement the combined dependency report behind the existing `tracemap report` command.
- MVP input is a combined SQLite database produced by `tracemap combine`.
- MVP does not replace per-language scan-time `report.md` files.
- MVP output is Markdown by default and JSON when `--format json` is requested.
- MVP can write both Markdown and JSON when `--out` is a directory.
- MVP computes derived endpoint matches from combined facts in memory and is read-only by default.
- MVP does not persist one-sided, dynamic, or gap endpoint findings into the current `endpoint_matches` table because that table requires both client and server source IDs.
- MVP reads existing combined tables and JSON properties; it does not require language scanner changes.
- MVP may include one small combine compatibility fix: correct `index_sources.language` inference for JVM and Python sources.
- MVP does not implement snapshot diffing, runtime dependency resolution, call-graph path search across arbitrary nodes, or new language extractors.

## Quick Start Workflow

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index /tmp/client/index.sqlite --label client \
  --index /tmp/api/index.sqlite --label api \
  --out /tmp/combined.sqlite

dotnet run --project src/dotnet/TraceMap.Cli -- report \
  --index /tmp/combined.sqlite \
  --out /tmp/dependency-report.md
```

For directory output:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- report \
  --index /tmp/combined.sqlite \
  --out /tmp/tracemap-report
```

Expected directory artifacts:

```text
dependency-report.md
dependency-report.json
```

## Requirements

### Requirement 1: Combined Report Command

**User Story:** As a reviewer, I want `tracemap report` to read a combined index so that I can review cross-repo dependency evidence without writing SQL by hand.

#### Acceptance Criteria

1. WHEN the user runs `tracemap report --index <combined.sqlite> --out <path>` THEN TraceMap SHALL read the combined SQLite database and emit a Markdown dependency report.
2. WHEN `--format json` is provided THEN TraceMap SHALL emit a machine-readable JSON report.
3. WHEN `--out` is a directory THEN TraceMap SHALL emit both `dependency-report.md` and `dependency-report.json`.
4. WHEN `--out` is a file path THEN TraceMap SHALL emit only the requested format, defaulting to Markdown.
5. WHEN the input is not a combined index THEN the command SHALL fail with a clear message and SHALL NOT silently treat a single-language index as a combined dependency report.
6. WHEN required combined tables or views are missing THEN the command SHALL fail with a clear schema error that names the missing table or view.
7. WHEN `--out` names an existing directory or a path with no file extension THEN TraceMap SHALL treat it as a directory and write both Markdown and JSON outputs.
8. WHEN the report completes THEN the CLI SHALL print the output path, source count, fact count, dependency edge count, endpoint finding count, and report coverage.
9. WHEN an output file would be overwritten THEN the command MAY overwrite it, matching existing export/report behavior; it SHALL NOT modify input indexes in the MVP.
10. WHEN combined-index detection runs THEN TraceMap SHALL require `index_sources` with at least one row, `combined_facts`, and `combined_dependency_edges`; missing `endpoint_matches` SHALL be a warning unless derived persistence is explicitly requested in a future slice.

### Requirement 2: Source Inventory and Coverage Summary

**User Story:** As a platform engineer, I want the report to show what was analyzed so that conclusions stay tied to source indexes and commits.

#### Acceptance Criteria

1. WHEN a combined report is generated THEN it SHALL include every row from `index_sources`.
2. WHEN sources are listed THEN each source SHALL include label, language, repo name, scan root relative path when available, branch when available, commit SHA, scanner version, analysis level, and build status.
3. WHEN a source scanner version clearly indicates TypeScript, JVM, Python, or C# THEN the combined source language SHALL be correct for that ecosystem; fixing current JVM/Python mislabeling in `CombinedIndexBuilder.InferLanguage` is in scope.
4. WHEN a pre-existing combined index has a language value that conflicts with scanner version evidence THEN the report SHALL display a corrected language with a coverage warning rather than repeating a known-wrong value as authoritative.
5. WHEN any source has reduced semantic coverage, failed build status, unknown commit SHA, missing or corrected language, or known gaps in its manifest JSON THEN report coverage SHALL be reduced.
6. WHEN report coverage is reduced THEN the Markdown summary and JSON `coverageWarnings` SHALL describe why conclusions are partial.
7. WHEN a source path is useful for provenance THEN the report SHALL use `index_path_hash`, `scanRootRelativePath`, `scanRootPathHash`, and labels; it SHALL NOT display local absolute paths.
8. WHEN the combined database contains multiple sources with the same repo and commit THEN the report SHALL distinguish them by label and scan root metadata.
9. WHEN manifest JSON contains known gaps THEN the report SHALL summarize gap categories without flooding the report with every repeated generated/cache-file gap.

### Requirement 3: Dependency Surface Summary

**User Story:** As a reviewer, I want a compact summary of dependency evidence so that I can quickly understand the app's integration surfaces.

#### Acceptance Criteria

1. WHEN combined facts contain HTTP client calls THEN the report SHALL summarize them by source label, HTTP method, normalized path key when available, and dynamic/unknown URL status.
2. WHEN combined facts contain HTTP route bindings THEN the report SHALL summarize them by source label, HTTP method, normalized path key when available, and route evidence tier.
3. WHEN combined facts contain structured SQL or query-pattern evidence THEN the report SHALL summarize operation, table names, column names, source kind, shape hash, source label, evidence tier, rule ID, and file span where available.
4. WHEN combined facts contain `SqlTextUsed` evidence only THEN the report SHALL render text hash, text length, source kind when available, and `n/a` for table/column fields rather than inventing parsed SQL structure.
5. WHEN combined facts contain package/dependency evidence THEN the report SHALL summarize package/module names by source label and dependency kind where available.
6. WHEN combined dependency edges exist THEN the report SHALL summarize calls, object creations, symbol relationships, and parameter-forwarding edges by source label and edge kind.
7. WHEN facts lack optional normalized fields THEN the report SHALL render `unknown` or `n/a`, not crash and not invent values.
8. WHEN the same dependency surface appears in multiple sources THEN the report SHALL preserve separate source evidence rows rather than deduplicating away provenance.
9. WHEN tables become large THEN Markdown SHALL show the first 200 rows per section using deterministic ordering, emit a truncation notice, and include full rows in JSON unless a JSON cap is explicitly requested.

### Requirement 4: Combined Endpoint Alignment

**User Story:** As a reviewer, I want a combined report to align client calls and server endpoints across N scanned indexes so that I can see cross-app HTTP dependencies.

#### Acceptance Criteria

1. WHEN combined facts include `HttpCallDetected` and `HttpRouteBinding` facts with normalized path evidence THEN the report SHALL compute endpoint alignment per compatible `(client source, server source)` pair.
2. WHEN a client call and server endpoint match by HTTP method and normalized path key THEN the finding SHALL be classified as `MatchedEndpoint`.
3. WHEN a server optional segment produces a compatible path shape THEN the finding SHALL be classified as `OptionalSegmentMatch`.
4. WHEN path keys match but HTTP methods differ THEN the finding SHALL be classified as `MethodMismatch`.
5. WHEN a client call has no matching server endpoint in the combined index THEN the finding SHALL be classified as `ClientCallNoServerEndpoint`.
6. WHEN a server endpoint has no matching client call in the combined index THEN the finding SHALL be classified as `ServerEndpointNoClientMatch`.
7. WHEN a client URL is dynamic or cannot be normalized safely THEN the finding SHALL be classified as `DynamicClientUrlNeedsReview`.
8. WHEN the same client call matches endpoints in two different server sources THEN the report SHALL emit one match per server source and SHALL NOT collapse that fan-out into `AmbiguousMatch`.
9. WHEN multiple server endpoints inside the same server source tie for the same best match THEN the finding SHALL be classified as `AmbiguousMatch`.
10. WHEN a single source contains both HTTP client calls and HTTP route bindings THEN same-source matches SHALL be included and flagged with `sameSource = true`.
11. WHEN analysis gaps prevent credible matching THEN the finding SHALL be classified as `UnknownAnalysisGap` and SHALL attach representative source evidence where available.
12. WHEN no client facts or no server route facts exist THEN the report SHALL say that endpoint alignment was not computable for those sources rather than implying no dependencies.
13. WHEN classifications like client-only or server-only are reported THEN the report SHALL state they are coverage-relative and are not proof of broken calls, unused endpoints, or dead code.
14. WHEN endpoint findings are computed THEN each finding SHALL include source labels, source index IDs, combined fact IDs, original fact IDs, scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, line spans, and static match quality where available.

### Requirement 5: Future Derived Rows in `endpoint_matches`

**User Story:** As a data user, I want endpoint matches to be queryable from the combined database so that reports and external tools can use the same derived evidence.

#### Acceptance Criteria

1. WHEN the MVP `tracemap report` command computes endpoint matches THEN it SHALL keep them in memory and SHALL NOT write to `endpoint_matches`.
2. WHEN a future opt-in write mode is added THEN it SHALL be explicit, such as `--write-derived`, not the default report behavior.
3. WHEN future rows are inserted into the current `endpoint_matches` table THEN only two-sided findings with both client and server source IDs SHALL be persisted unless the schema is changed.
4. WHEN one-sided, dynamic, or gap findings are produced against the current schema THEN they SHALL remain report-only because `client_source_index_id` and `server_source_index_id` are `NOT NULL`.
5. WHEN rows are inserted in a future slice THEN `endpoint_match_id` SHALL be deterministic from source index IDs, combined fact IDs, classification, HTTP method, and normalized path key.
6. WHEN future derived persistence runs repeatedly on the same combined index THEN it SHALL delete rows with the same `derivedBy` algorithm tag before inserting new rows, so changed classifications do not leave orphan rows.
7. WHEN endpoint matches are stored THEN full provenance SHALL live in `evidence_json`, including source labels, source index IDs, combined fact IDs, original fact IDs, scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, line spans, and static match quality.
8. WHEN endpoint matches are stored THEN `evidence_json` SHALL NOT include raw source snippets, raw URLs, raw SQL, literal values, or local absolute paths.
9. WHEN source facts change because the combined database is rebuilt from different indexes THEN old derived rows SHALL NOT be carried over unless recomputed.

### Requirement 6: Dependency Evidence Sections

**User Story:** As a reviewer, I want evidence sections ordered by review value so that the report tells a useful story.

#### Acceptance Criteria

1. WHEN a Markdown report is generated THEN it SHALL include these sections in order: Summary, Sources, Endpoint Alignment, Dependency Surfaces, Dependency Edges, Needs Review, Known Gaps, Limitations.
2. WHEN Endpoint Alignment has dynamic, ambiguous, method mismatch, or unknown-gap findings THEN those findings SHALL be summarized before simple unmatched inventory.
3. WHEN Dependency Surfaces lists SQL evidence THEN it SHALL include the static SQL limitation near the SQL rows.
4. WHEN Dependency Edges lists calls or object creations THEN it SHALL include source symbol, target symbol/type, edge kind, source label, evidence tier, rule ID, and file span.
5. WHEN parameter-forwarding edges are available THEN the report SHALL show source method/parameter and target method/parameter without claiming full taint flow or runtime value flow.
6. WHEN local or field aliases are available THEN MVP MAY summarize counts only; detailed alias paths are a follow-up unless needed to support the report.
7. WHEN known gaps exist THEN the report SHALL group them by source label and gap category.
8. WHEN no data exists for a section THEN the report SHALL say `No evidence found in the combined index.` rather than omitting the section silently.

### Requirement 7: JSON Report Contract

**User Story:** As an automation author, I want a stable JSON report so that downstream scripts can query TraceMap dependency evidence.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include top-level `version`, `generatedAt`, `reportCoverage`, `coverageWarnings`, `sources`, `summary`, `endpointFindings`, `dependencySurfaces`, `dependencyEdges`, `needsReview`, `knownGaps`, and `limitations`.
2. WHEN source rows are emitted THEN they SHALL include source label and provenance fields from `index_sources` without local absolute paths.
3. WHEN endpoint findings are emitted THEN they SHALL include stable classification strings and deterministic evidence objects.
4. WHEN an endpoint finding is emitted THEN it SHALL include `classification`, `httpMethod`, `normalizedPathKey`, `clientSourceIndexId`, `clientSourceLabel`, `serverSourceIndexId`, `serverSourceLabel`, `clientCombinedFactId`, `serverCombinedFactId`, `clientOriginalFactId`, `serverOriginalFactId`, `clientFilePath`, `clientStartLine`, `serverFilePath`, `serverStartLine`, `staticMatchQuality`, `sameSource`, and `notes`, using `null` where one side is absent.
5. WHEN dependency surfaces are emitted THEN each row SHALL include `surfaceKind`, source identity, display name, evidence tier, rule ID, file span, and fact IDs.
6. WHEN dependency edges are emitted THEN each row SHALL include edge kind, source symbol, target symbol, source identity, evidence tier, rule ID, file span, and edge ID.
7. WHEN values are missing THEN JSON SHALL use `null` or empty arrays consistently; it SHALL NOT omit required top-level arrays.
8. WHEN the JSON schema changes in a future version THEN the top-level `version` SHALL change.

### Requirement 8: Evidence Boundaries and Limitations

**User Story:** As a reviewer, I want the report to be honest about what static evidence can and cannot prove.

#### Acceptance Criteria

1. WHEN endpoint matches are reported THEN the report SHALL say they are static method/path evidence and do not prove runtime traffic, reachability, auth, proxy behavior, deployment base paths, or CORS behavior.
2. WHEN SQL/query evidence is reported THEN the report SHALL say it is static shape evidence and does not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
3. WHEN call edges are reported THEN the report SHALL say they do not prove dynamic dispatch target, runtime DI binding, reflection target, branch feasibility, or collection contents.
4. WHEN parameter forwarding is reported THEN the report SHALL say it is direct static argument-to-parameter evidence and not a full taint analysis.
5. WHEN reduced coverage exists THEN the report SHALL avoid words like `complete`, `all`, or `no dependencies` unless scoped explicitly to discovered evidence.
6. WHEN a report section uses derived rows THEN it SHALL identify the source facts and rule IDs that support the derived classification.
7. WHEN dynamic URL findings are rendered THEN Markdown, JSON, and any future `evidence_json` SHALL render only closed-set reason codes and hashes; raw URL fragments SHALL NOT be displayed.

### Requirement 9: Tests and Fixtures

**User Story:** As a maintainer, I want focused tests for combined reporting so that future language work does not break cross-index analysis.

#### Acceptance Criteria

1. WHEN two sample indexes are combined THEN tests SHALL prove `tracemap report` emits Markdown and JSON.
2. WHEN a combined index contains matching HTTP client/server facts THEN tests SHALL prove a `MatchedEndpoint` row appears in Markdown and JSON.
3. WHEN one client call matches routes in two different server sources THEN tests SHALL prove one matched row per server source, not a global `AmbiguousMatch`.
4. WHEN one source contains both client calls and route bindings THEN tests SHALL prove same-source matches are included and flagged.
5. WHEN a combined index contains method mismatch, dynamic URL, client-only, and server-only cases THEN tests SHALL prove those classifications appear.
6. WHEN a combined index contains call edges and object creations THEN tests SHALL prove dependency-edge rows render with source labels and evidence.
7. WHEN a combined index contains SQL/query-pattern facts THEN tests SHALL prove SQL rows render without raw SQL text.
8. WHEN a combined index contains `SqlTextUsed` only THEN tests SHALL prove hash/length evidence renders with `n/a` table/column fields.
9. WHEN a source has reduced coverage, corrected language, or known gaps THEN tests SHALL prove reduced report coverage and warnings.
10. WHEN a combined index contains JVM or Python sources THEN tests SHALL prove source language is rendered correctly.
11. WHEN a dynamic URL finding is emitted THEN tests SHALL prove no raw URL appears in Markdown, JSON, or future persisted evidence JSON.
12. WHEN the input is a single-language index THEN tests SHALL prove `tracemap report` fails clearly.
13. WHEN a Markdown section exceeds 200 rows THEN tests SHALL prove deterministic truncation notice and full JSON rows.
14. WHEN files are checked in THEN `dotnet build src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass.

## Future Work

- Snapshot/diff reporting between two combined databases.
- Cross-source call path exploration using combined dependency edges.
- HTML report output.
- Opt-in endpoint match persistence after deciding whether to change `endpoint_matches` nullability or persist only two-sided findings.
- Rule-backed derived facts for endpoint matches if the rule catalog grows derived fact support.
- Additional SQL normalization and parser-backed table/column extraction.
- Framework-specific package dependency grouping for Maven/Gradle, NuGet, npm, and Python packaging.
