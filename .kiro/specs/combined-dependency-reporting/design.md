# Combined Dependency Reporting Design

## Overview

Add a deterministic report layer over combined TraceMap indexes.

The workflow becomes:

```bash
tracemap combine \
  --index client/index.sqlite --label client \
  --index api/index.sqlite --label api \
  --out combined.sqlite

tracemap report --index combined.sqlite --out dependency-report.md
```

The command reads combined tables, derives cross-source endpoint alignment, summarizes dependency surfaces, and writes Markdown plus optional JSON. It is a reporting and correlation layer over existing evidence. It does not add LLMs, runtime tracing, or new scan-time inference.

## Goals

- Make combined indexes useful without hand-written SQL.
- Convert `tracemap report` from a skeleton into a combined dependency report command.
- Show source inventory and coverage honestly.
- Derive N-way endpoint alignment from combined HTTP client and route facts.
- Summarize HTTP, SQL/query, package, call, object-creation, relationship, and parameter-forwarding evidence.
- Preserve source labels, source index IDs, scan IDs, commit SHAs, original fact IDs, combined fact IDs, rule IDs, evidence tiers, and file spans.
- Keep output suitable for public demos without local absolute paths or raw source snippets.

## Non-Goals

- No single-index report rewrite in this slice.
- No runtime traffic capture.
- No target app restore/build.
- No full interprocedural path search.
- No runtime dependency injection, reflection, dynamic dispatch, serializer contract, branch feasibility, or collection-content inference.
- No snapshot diff command.
- No scanner changes unless review finds a small schema compatibility bug.

## Command Shape

Use the existing CLI skeleton:

```bash
tracemap report --index <combined.sqlite> --out <path> [--format <markdown|json>] [--no-write-derived]
```

Behavior:

- File output defaults to Markdown.
- `--format json` with a file writes JSON.
- Directory output writes both `dependency-report.md` and `dependency-report.json` unless a future flag narrows it.
- The command rejects non-combined indexes with a message like `tracemap report currently requires a combined index produced by tracemap combine`.
- The command validates the presence of `index_sources`, `combined_facts`, `combined_dependency_edges`, and `endpoint_matches`.

## Proposed Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedDependencyReportEngine.cs
    CombinedDependencyIndexReader.cs
    CombinedDependencyModels.cs
    CombinedEndpointMatcher.cs
    CombinedDependencyMarkdownWriter.cs
    CombinedDependencyJsonWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedDependencyReportTests.cs
```

`TraceMap.Reporting` already exists, so this slice should extend it rather than creating a new project.

## Data Sources

Read from combined database tables:

| Table/View | Purpose |
| --- | --- |
| `index_sources` | source labels, languages, commit SHAs, scan roots, manifest JSON, coverage |
| `combined_facts` | endpoint, SQL/query, package/config/integration facts |
| `combined_dependency_edges` | calls, creates, relationships, parameter-forwarding summary view |
| `combined_call_edges` | call edge details when needed |
| `combined_object_creations` | object creation details when needed |
| `combined_argument_flows` | direct argument evidence when needed |
| `combined_parameter_forward_edges` | parameter-forwarding evidence |
| `endpoint_matches` | optional derived row storage |

The reader should treat `properties_json` as the source for language-specific optional fields.

## Report Model

Suggested public model:

```csharp
public sealed record CombinedDependencyReport(
    string Version,
    DateTimeOffset GeneratedAt,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<CombinedReportSource> Sources,
    CombinedReportSummary Summary,
    IReadOnlyList<EndpointFinding> EndpointFindings,
    IReadOnlyList<DependencySurfaceRow> DependencySurfaces,
    IReadOnlyList<DependencyEdgeRow> DependencyEdges,
    IReadOnlyList<NeedsReviewRow> NeedsReview,
    IReadOnlyList<KnownGapRow> KnownGaps,
    IReadOnlyList<string> Limitations);
```

Coverage values:

- `FullEvidenceAvailable`: all sources have full semantic coverage and clean build/provenance signals.
- `ReducedCoverage`: at least one source has reduced semantic coverage, build failure, known gaps, unknown language, or unknown commit SHA.
- `UnknownAnalysisGap`: the combined schema or source manifests are insufficient to support a credible conclusion.

This is report coverage, not proof of complete runtime dependency knowledge.

## Source Coverage Logic

For each source:

- Parse `manifest_json` with the existing `ScanManifest` model when possible.
- Flag reduced coverage when:
  - `analysis_level` is not full semantic for that adapter.
  - `build_status` is failed/reduced/unknown.
  - `commit_sha` is `unknown` or empty.
  - `language` is empty.
  - manifest known gaps are non-empty.
- Group known gaps by stable category. A simple MVP category can use the prefix before `:` or the first sentence fragment.

Do not display local absolute paths. `index_path_hash`, scan root metadata, repo name, labels, and commit SHA are enough.

## Dependency Surfaces

Build rows from `combined_facts`.

### HTTP Client Calls

Fact type: `HttpCallDetected`.

Fields:

- source label/source index ID
- HTTP method from `httpMethod`, `methodName`, or `targetSymbol`
- normalized path key/template from properties
- `urlKind`, `dynamicReason`
- evidence tier, rule ID, file span

Dynamic calls should also create `NeedsReviewRow` entries.

### HTTP Route Bindings

Fact type: `HttpRouteBinding`.

Fields:

- source label/source index ID
- HTTP method from `httpMethod`, `httpMethods`, or `methodName`
- normalized path key/template from properties
- controller/action when available
- evidence tier, rule ID, file span

### SQL and Query Patterns

Fact types:

- `QueryPatternDetected`
- `SqlTextUsed`
- `DatabaseColumnMapping`

SQL-shape rows should prefer:

- `operationName`
- `tableName` or `tableNames`
- `columnNames` or `fieldNames`
- `sqlSourceKind`
- `queryShapeHash`

Query-builder rows should prefer:

- `filterFields`
- `sortFields`
- `selectFields`
- `includeFields`
- `mutationFields`
- `patternHash`

`SqlTextUsed` rows should display only hashes/lengths/source kind. They must not display raw SQL.

### Packages and Config

Package/dependency facts differ by adapter. MVP should read obvious fact types and properties without overfitting:

- fact types containing `Package`, `Dependency`, `ProjectReference`, or equivalent existing package facts
- properties like `packageName`, `dependencyName`, `moduleName`, `groupId`, `artifactId`, `version`, `dependencyKind`

If no stable package facts are present, show `No evidence found in the combined index.`

## Endpoint Alignment

The combined matcher generalizes the existing two-index endpoint concept.

Inputs:

- Client candidates: `HttpCallDetected` facts.
- Server candidates: `HttpRouteBinding` facts.
- Candidate role hints:
  - A fact with `clientFramework`, `urlKind`, or call-oriented properties is a client candidate.
  - A fact with `controllerName`, `actionName`, `routeTemplates`, or route binding properties is a server candidate.
  - Source language is not enough by itself; TypeScript can expose server routes and C# can emit HTTP calls.

Matching:

1. Exclude self-pairs where client and server facts come from the same combined fact.
2. Normalize or read `normalizedPathKey`.
3. If a client has `urlKind = dynamic` or no safe path key, emit `DynamicClientUrlNeedsReview`.
4. Match exact method + path key as `MatchedEndpoint`.
5. Match compatible optional route shapes as `OptionalSegmentMatch` when optional metadata is available.
6. Match path key but method mismatch as `MethodMismatch`.
7. If multiple server candidates tie with the same highest score, emit `AmbiguousMatch`.
8. Emit client-only and server-only inventory after matched/mismatch/ambiguous findings.
9. Emit `UnknownAnalysisGap` for source pairs where missing route/call facts plus known gaps make a clean statement unreliable.

Static match quality:

- `High`: method and normalized path match exactly.
- `Medium`: optional segment or parameter-name differences.
- `Low`: dynamic/incomplete evidence.

Endpoint findings are derived evidence, not source facts.

## `endpoint_matches` Persistence

MVP should write derived endpoint rows unless `--no-write-derived` is passed.

Persistence strategy:

- Compute deterministic `endpoint_match_id` from:
  - client source index ID
  - server source index ID
  - client combined fact ID or `none`
  - server combined fact ID or `none`
  - classification
  - HTTP method or `unknown`
  - normalized path key or `unknown`
- Delete rows whose `evidence_json` identifies the same algorithm/version before re-inserting, or use `insert or replace`.
- Include `derivedBy: tracemap-combined-dependency-report/1` in `evidence_json`.
- Keep raw snippets, raw URLs, raw SQL, and absolute paths out of `evidence_json`.

If this mutation makes review uncomfortable, implement `--no-write-derived` first and make DB writes a second task. The spec still prefers persistence because the table already exists as a placeholder.

## Markdown Output

Sections:

1. `# TraceMap Dependency Report`
2. `## Summary`
3. `## Sources`
4. `## Endpoint Alignment`
5. `## Dependency Surfaces`
6. `## Dependency Edges`
7. `## Needs Review`
8. `## Known Gaps`
9. `## Limitations`

Keep rows compact and deterministic. Suggested sorting:

- Sources by label.
- Endpoint findings by classification priority, method, path key, client label, server label, file path, line.
- Surfaces by surface kind, source label, display name, file path, line.
- Edges by edge kind, source label, source symbol, target symbol, file path, line.

Classification priority:

1. `UnknownAnalysisGap`
2. `DynamicClientUrlNeedsReview`
3. `AmbiguousMatch`
4. `MethodMismatch`
5. `MatchedEndpoint`
6. `OptionalSegmentMatch`
7. `ClientCallNoServerEndpoint`
8. `ServerEndpointNoClientMatch`

Caps:

- Markdown sections may cap long tables at 200 rows per section by default.
- If capped, include a line like `Showing first 200 of 1,245 rows. JSON contains all rows.`
- JSON should include all rows in MVP.

## JSON Output

Stable top-level shape:

```json
{
  "version": "1.0",
  "generatedAt": "2026-06-13T00:00:00Z",
  "reportCoverage": "ReducedCoverage",
  "coverageWarnings": [],
  "sources": [],
  "summary": {},
  "endpointFindings": [],
  "dependencySurfaces": [],
  "dependencyEdges": [],
  "needsReview": [],
  "knownGaps": [],
  "limitations": []
}
```

Required row provenance:

- `sourceIndexId`
- `sourceLabel`
- `scanId`
- `commitSha`
- `combinedFactId` or `edgeId`
- `originalFactId` when available
- `ruleId`
- `evidenceTier`
- `filePath`
- `startLine`
- `endLine`

## Evidence Boundaries

Limitations to include in every report:

- Endpoint alignment is static method/path evidence. It does not prove runtime traffic, runtime reachability, auth behavior, proxy behavior, deployment base paths, CORS behavior, or user exercise.
- SQL/query rows are static shape or hash evidence. They do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
- Call and creation edges are static code evidence. They do not prove dynamic dispatch targets, runtime DI registrations, reflection targets, branch feasibility, or collection contents.
- Parameter-forwarding rows are direct static argument-to-parameter evidence, not full taint analysis.
- Reduced coverage means absence of evidence is not evidence of absence.

## Tests

Test shape:

- Use small temporary C# scans to produce call/object edges, then combine.
- Use synthetic SQLite rows where necessary for cross-language endpoint combinations that are expensive to scan in a unit test.
- Prefer end-to-end CLI tests for `combine -> report`.
- Add a focused reader/matcher unit test for endpoint classifications.

Core test cases:

1. Combined report rejects single-language index.
2. Combined report writes Markdown and JSON for a combined index.
3. Sources and coverage warnings render from `index_sources`.
4. Matched endpoint appears in Markdown/JSON and `endpoint_matches`.
5. Method mismatch, dynamic URL, client-only, and server-only classifications are deterministic.
6. `--no-write-derived` does not mutate `endpoint_matches`.
7. Dependency edges include source label and evidence.
8. SQL/query rows do not include raw SQL text.
9. Markdown row caps include truncation notice when triggered.

## Validation

Run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If TypeScript, JVM, or Python files are changed during implementation, also run the relevant checks from `docs/VALIDATION.md`.
