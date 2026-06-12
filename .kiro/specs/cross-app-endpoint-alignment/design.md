# Cross-App Endpoint Alignment Design

## Overview

Add deterministic endpoint alignment across a client TraceMap index and a server TraceMap index.

The MVP workflow is:

```bash
tracemap-ts scan --repo <angular-client-root> --out <client-out>
tracemap scan --repo <dotnet-server-root> --project <api-project> --out <server-out>
tracemap endpoints --client-index <client-out>/index.sqlite --server-index <server-out>/index.sqlite --out <endpoint-report.md>
```

The endpoint command reads facts from both indexes, normalizes client call paths and server route paths, matches them by HTTP method and normalized route shape, and emits Markdown plus optional JSON.

This is a report/correlation layer over evidence. It does not mutate source facts and does not claim runtime traffic or runtime reachability.

## Goals

- Detect Angular `HttpClient` API calls.
- Detect ASP.NET Core controller endpoints even when semantic analysis fails.
- Normalize client and server route templates consistently.
- Correlate client calls to server endpoints across two indexes.
- Preserve source index provenance, commit SHA, scan root identity, rule IDs, evidence tiers, and file spans.
- Keep the design compatible with a future combined multi-index dependency database.

## Non-Goals

- No runtime request tracing.
- No automatic dependency installation or target application build.
- No LLM classification.
- No route reachability proof through middleware, auth, feature flags, reverse proxies, Angular interceptors, or deployment config.
- No monolithic multi-language scanner in MVP.
- No mandatory combined database in MVP.
- No endpoint diff command in MVP.

## Locked Design Decisions

- MVP takes two indexes as inputs, rather than scanning both apps in one command.
- MVP emits endpoint alignment as derived report data, not source facts.
- Source facts remain language-scanner owned.
- Angular `HttpClient` client calls reuse `HttpCallDetected`.
- ASP.NET controller endpoints reuse `HttpRouteBinding`.
- Server syntax route fallback is required because unresolved ASP.NET references can prevent semantic route extraction.
- Combined database work is future-facing, but endpoint result JSON and source metadata must be shaped so a future combine command can reuse it.

## Proposed Package Layout

Dotnet additions:

```text
src/dotnet/
  TraceMap.Core/
    EndpointRouteNormalizer.cs
    AspNetSyntaxRouteExtractor.cs
  TraceMap.EndpointAlignment/
    EndpointAlignmentEngine.cs
    EndpointIndexReader.cs
    EndpointMatcher.cs
    EndpointReportWriter.cs
    EndpointAlignmentModels.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    EndpointAlignmentTests.cs
    AspNetSyntaxRouteExtractorTests.cs
```

TypeScript additions:

```text
src/typescript/src/
  extractors/
    IntegrationExtractor.ts
  endpoints/
    RouteTemplateNormalizer.ts
  tests/
    AngularHttpClientExtractor.test.ts
```

The shared route normalization rules should be specified in tests. The TypeScript scanner may implement a local normalizer for emitted properties, while the .NET endpoint command owns final cross-index matching.

## Fact Model

### Angular Client Calls

Emit `HttpCallDetected` from rule `typescript.integration.angular-httpclient.v1`.

Evidence tier:

- `Tier1Semantic` when the receiver resolves to Angular `HttpClient`.
- `Tier2Structural` when the receiver is an injected constructor property typed or imported as `HttpClient`.
- `Tier3SyntaxOrTextual` when the call shape is `this.http.get/post/...` and semantic evidence is unavailable.

Required properties:

```json
{
  "methodName": "GET",
  "httpMethod": "GET",
  "urlKind": "template",
  "normalizedPathTemplate": "/api/admin/runner/get-by-id/{runnerId}",
  "normalizedPathKey": "/api/admin/runner/get-by-id/{}",
  "baseUrlSymbol": "environment.apiUri",
  "pathParameterNames": "runnerId",
  "queryParameterNames": "",
  "clientFramework": "angular",
  "sourceClass": "RunnerAdminService",
  "sourceMethod": "getById",
  "responseType": "ApiResponse<Runner>",
  "bodyFieldNames": "",
  "urlHash": "<hash>"
}
```

Notes:

- `normalizedPathTemplate` may contain parameter names.
- `normalizedPathKey` should replace all parameter names with `{}` and lowercase literal segments for matching.
- Raw URL/template text is not stored. Store hashes and normalized route evidence.
- For dynamic URLs, set `urlKind = dynamic`, omit `normalizedPathKey` if unsafe, and include a `dynamicReason` property.

### ASP.NET Server Routes

Emit `HttpRouteBinding` from:

- Existing semantic rule where semantic binding works.
- New syntax fallback rule `csharp.syntax.aspnetroute.v1` where semantic binding is unavailable or incomplete.

Syntax fallback properties:

```json
{
  "methodName": "POST",
  "httpMethods": "POST",
  "routeTemplates": "api/admin/[controller],delete-club/{clubId}",
  "normalizedPathTemplate": "/api/admin/club/delete-club/{clubId}",
  "normalizedPathKey": "/api/admin/club/delete-club/{}",
  "routeParameterNames": "clubId",
  "optionalParameterNames": "",
  "routeConstraints": "",
  "controllerName": "Club",
  "actionName": "Delete",
  "bodyParameterNames": "",
  "bodyParameterTypes": "",
  "authorizationKind": "authorize"
}
```

Syntax extraction rules:

- Controller class name `AccountController` becomes `[controller] = Account`.
- Method name may be used for `[action]` if the template contains it.
- `[ApiController]` is useful metadata but not required.
- `[Route]` on controller and method combine.
- `[HttpPost("x")]` acts as both HTTP method and method route.
- `[HttpPost]` without route uses an empty method route.
- Multiple route attributes produce multiple route facts.
- Multiple HTTP method attributes produce multiple method/template combinations.
- Comments should not emit active endpoints.

### Endpoint Alignment Result

Derived result, not a source fact:

```json
{
  "classification": "MatchedEndpoint",
  "method": "POST",
  "clientPathTemplate": "/api/runner/check-in",
  "serverPathTemplate": "/api/Runner/check-in",
  "normalizedPathKey": "/api/runner/check-in",
  "clientFactId": "fact-...",
  "serverFactId": "fact-...",
  "clientScanId": "scan-...",
  "serverScanId": "scan-...",
  "clientCommitSha": "...",
  "serverCommitSha": "...",
  "confidence": "High",
  "evidenceTiers": ["Tier2Structural", "Tier3SyntaxOrTextual"],
  "notes": []
}
```

Classifications:

- `MatchedEndpoint`: method and route key match exactly.
- `OptionalSegmentMatch`: route key matches because one or more server segments are optional.
- `MethodMismatch`: route shape matches but HTTP method does not.
- `ClientCallNoServerEndpoint`: client call has no matching server endpoint.
- `ServerEndpointNoClientCall`: server endpoint has no matching client call.
- `AmbiguousMatch`: more than one candidate has the same best score.
- `DynamicClientUrlNeedsReview`: client URL cannot be normalized safely.
- `UnknownAnalysisGap`: source index coverage gaps prevent credible alignment.

Confidence:

- `High`: both sides have normalized method/path and neither side is dynamic.
- `Medium`: optional segment, parameter-name mismatch, or one side is syntax-only but normalized.
- `Low`: dynamic or incomplete normalization.

## Route Normalization

Normalize into two forms:

- `normalizedPathTemplate`: human-readable path with parameter names.
- `normalizedPathKey`: comparison path with all parameters converted to `{}`.

Examples:

| Input | Template | Key |
| --- | --- | --- |
| `${environment.apiUri}/admin/runner/get-by-id/${runnerId}` | `/api/admin/runner/get-by-id/{runnerId}` | `/api/admin/runner/get-by-id/{}` |
| `api/[controller]` + `get-by-id/{runnerId}` on `RunnerController` | `/api/Runner/get-by-id/{runnerId}` | `/api/runner/get-by-id/{}` |
| `{clubId}/{id?}` | `/{clubId}/{id?}` | `/{}/{?}` or segment metadata with optional flag |
| `{id:guid}` | `/{id}` plus constraint `id:guid` | `/{}`

Normalization steps:

1. Trim whitespace and quotes.
2. Split off scheme/host when statically visible.
3. Preserve a leading slash.
4. Collapse duplicate slashes.
5. Remove trailing slash except root.
6. Lowercase literal segments for comparison.
7. Convert route and template parameters to placeholders.
8. Store parameter names separately.
9. Store optional flags and constraints separately.
10. Hash original source text, but do not store it unless future raw-snippet support is explicitly enabled.

## Matching Algorithm

Input sets:

- Client endpoint candidates from `HttpCallDetected`.
- Server endpoint candidates from `HttpRouteBinding`.

Candidate filters:

1. Skip client calls without a safe normalized route key and report `DynamicClientUrlNeedsReview`.
2. Match server candidates with compatible path shape.
3. Prefer same HTTP method.
4. If path matches but method differs, produce `MethodMismatch`.

Segment matching:

- Literal segments match case-insensitively.
- Parameter segments match by position.
- Optional server parameter segments may be absent from the client.
- Constraints are retained as evidence but do not block MVP matching.
- Catch-all routes are needs-review unless the match is otherwise exact.

Scoring:

| Signal | Score |
| --- | ---: |
| Exact literal segment match | +4 |
| Parameter segment match | +2 |
| Optional segment consumed or skipped | +1 |
| HTTP method exact match | +8 |
| Same parameter name | +1 |
| Constraint compatible | +1 |
| Catch-all involved | -4 |
| Dynamic client URL | no direct match |

If top scores tie, emit `AmbiguousMatch`.

## CLI Design

Add a .NET CLI command:

```bash
tracemap endpoints \
  --client-index <path> \
  --server-index <path> \
  --out <path> \
  [--format markdown|json] \
  [--client-label <label>] \
  [--server-label <label>]
```

Future-friendly options:

- `--out <directory>` can write both `endpoint-report.md` and `endpoint-report.json`.
- `--include-server-only` default true.
- `--include-client-only` default true.
- `--strict-methods` default true.

Help text must say the command performs static evidence alignment and does not prove runtime traffic.

## Report Design

Markdown sections:

1. Summary
2. Source Indexes
3. Coverage and Gaps
4. Needs Review
5. Matched Endpoints
6. Method Mismatches
7. Client Calls Without Server Endpoint
8. Server Endpoints Without Client Call
9. Limitations

Example row:

```text
| POST | /api/runner/check-in | MatchedEndpoint | src/app/services/runner.service.ts:19 | Controllers/RunnerController.cs:49 | Tier2/Tier3 |
```

JSON should be stable and testable:

```json
{
  "summary": {
    "matched": 33,
    "optionalSegmentMatches": 1,
    "methodMismatches": 0,
    "clientOnly": 0,
    "serverOnly": 5,
    "dynamicNeedsReview": 0,
    "ambiguous": 0,
    "unknownGaps": 0
  },
  "sources": [
    {
      "role": "client",
      "scanId": "...",
      "repoName": "ClientApp",
      "remoteUrl": "...",
      "commitSha": "...",
      "scanRootRelativePath": "backend/FFPRunningClub/FFPRunningClub.Api/ClientApp",
      "analysisLevel": "Level1SemanticAnalysisReduced"
    }
  ],
  "findings": []
}
```

## Multi-Index and Combined DB Design Seam

The MVP should not create a combined database, but it should avoid decisions that make one hard later.

Preferred future direction: **combine existing indexes**, not scan all languages into one index initially.

Why:

- Each language scanner can stay independent.
- Failed or reduced coverage remains local to a scan.
- Existing `index.sqlite` artifacts stay useful.
- Combining can be repeated without rescanning source.
- Multi-repo dependency analysis can include repos scanned at different times or commits.

Future command shape:

```bash
tracemap combine \
  --index <client-index.sqlite> --label ffp-client \
  --index <server-index.sqlite> --label ffp-api \
  --out <combined.sqlite>
```

Future combined schema candidates:

```sql
CREATE TABLE index_sources (
  source_index_id TEXT PRIMARY KEY,
  label TEXT NOT NULL,
  index_path_hash TEXT,
  scan_id TEXT NOT NULL,
  repo_name TEXT NOT NULL,
  remote_url TEXT,
  branch TEXT,
  commit_sha TEXT NOT NULL,
  scanner_version TEXT NOT NULL,
  language TEXT,
  scan_root_relative_path TEXT,
  scan_root_path_hash TEXT,
  git_root_hash TEXT,
  analysis_level TEXT NOT NULL,
  build_status TEXT NOT NULL,
  imported_at TEXT NOT NULL
);

CREATE TABLE combined_facts (
  combined_fact_id TEXT PRIMARY KEY,
  source_index_id TEXT NOT NULL,
  original_fact_id TEXT NOT NULL,
  scan_id TEXT NOT NULL,
  fact_type TEXT NOT NULL,
  rule_id TEXT NOT NULL,
  evidence_tier TEXT NOT NULL,
  commit_sha TEXT NOT NULL,
  payload_json TEXT NOT NULL
);

CREATE TABLE endpoint_matches (
  endpoint_match_id TEXT PRIMARY KEY,
  client_source_index_id TEXT NOT NULL,
  server_source_index_id TEXT NOT NULL,
  client_combined_fact_id TEXT,
  server_combined_fact_id TEXT,
  classification TEXT NOT NULL,
  http_method TEXT,
  normalized_path_key TEXT,
  confidence TEXT NOT NULL,
  evidence_json TEXT NOT NULL
);
```

Single-scan manifest additions should be additive:

```json
{
  "scanRootRelativePath": "backend/FFPRunningClub/FFPRunningClub.Api/ClientApp",
  "scanRootPathHash": "<hash>",
  "gitRootHash": "<hash>"
}
```

Raw absolute scan root path should be optional and local-only:

```bash
tracemap scan --repo <path> --out <out> --include-local-paths
```

This avoids making shared artifacts leak machine-specific paths while still allowing local dependency warehouses to answer "which folder did this scan represent?"

## Long-Term Diff Readiness

TraceMap already stores commit SHA in manifests and facts. Endpoint alignment should build on that:

- Endpoint JSON includes both source commit SHAs.
- Endpoint keys are stable for method and normalized route shape.
- Source facts retain evidence file and line spans.
- Future endpoint diff can compare two endpoint JSON files or two combined databases.

Future command shape:

```bash
tracemap endpoint-diff \
  --before <endpoint-report-old.json> \
  --after <endpoint-report-new.json> \
  --out <endpoint-diff.md>
```

Out of MVP:

- Working tree dirty-state hashing.
- Cross-commit symbol move detection.
- Route rename detection beyond normalized key comparison.

## Scanner Hygiene Updates

TypeScript scanner updates needed by this phase:

- Exclude `.angular` by default.
- Keep excluding `node_modules`, `dist`, `build`, coverage, and output path.
- Parse `tsconfig*.json` as JSONC so comments do not emit false config parse gaps.
- Avoid expanding package lockfiles and generated cache JSON into huge config fact sets unless explicitly requested.
- Keep package metadata facts from `package.json`.

C# scanner updates needed by this phase:

- Add ASP.NET syntax route fallback independent of semantic attribute resolution.
- Ensure scoped scan includes project files when `--project` is passed even if `--include` narrows source files.
- Record project-load/build path gaps clearly when solution paths are broken.

## FFP Expected Outcome

After implementation, the FFP fixture should produce roughly:

- Client calls: 34 Angular HTTP call facts.
- Server endpoints: 37 ASP.NET route facts from syntax fallback or semantic extraction.
- Endpoint alignment:
  - 34 client calls matched.
  - At least one optional-segment match for reporting route `{clubId}/{id?}` when client calls only `{clubId}`.
  - Approximately five server-only endpoints:
    - `POST /api/account/log-off`
    - `POST /api/account/change-password`
    - `GET /api/admin/runner/delete`
    - `GET /api/validation/is-club-name-unique`
    - one additional server-only endpoint depending on duplicate client calls and match grouping.

Exact counts should be test-controlled in a smaller fixture, with FFP used as a smoke test because it is an external repo with existing quirks.

## Rule Catalog Entries

Add or update rule catalog entries:

- `typescript.integration.angular-httpclient.v1`
  - Emits `HttpCallDetected`
  - Tier1/Tier2/Tier3 depending on receiver evidence
  - Limitation: static URL extraction only, no interceptor/proxy/runtime proof

- `csharp.syntax.aspnetroute.v1`
  - Emits `HttpRouteBinding`
  - Tier3 syntax fallback
  - Limitation: source attribute shape only, no runtime route table proof

- `endpoint.alignment.v1`
  - Emits derived report findings, not source scan facts
  - Limitation: static cross-index route matching only

## Open Questions for Review

- Should endpoint alignment be a new .NET project (`TraceMap.EndpointAlignment`) or live in `TraceMap.Reporting`/`TraceMap.Storage` for the first slice?
- Should endpoint reports default to Markdown only, or always emit JSON as well?
- Should scan-root metadata be added to both .NET and TypeScript manifests in this phase, or only endpoint report sources?
- Should package-lock config expansion be reduced globally now, or deferred to a scanner hygiene task?
- Should `tracemap endpoints` accept more than one client index or more than one server index in MVP, or should N-way matching wait for `tracemap combine`?
