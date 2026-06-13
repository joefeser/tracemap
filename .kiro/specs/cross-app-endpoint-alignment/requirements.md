# Cross-App Endpoint Alignment Requirements

## Introduction

TraceMap needs to line up client-side API calls with server-side API endpoints across separately scanned applications. The motivating fixture is a private Angular client under a legacy ASP.NET Core solution where the frontend lives inside the backend project tree:

- Angular app root: `<private-angular-client-app>`
- ASP.NET app root: `<private-aspnet-server-root>`

The goal is deterministic evidence, not runtime traffic capture. TraceMap should identify what endpoints the Angular code appears to call, what endpoints the ASP.NET code appears to expose, and whether the two can be matched by HTTP method and normalized route template.

This spec does not add LLMs, embeddings, vector databases, or prompt-based classification. It also does not prove runtime reachability, authentication state, deployment routing, proxy behavior, or whether an endpoint is actually exercised by users.

## Discovery Summary

The private fixture probe showed this phase is viable:

- Angular dependencies restored with `npm ci`.
- Angular build printed successful build output and warnings, but the Angular CLI process did not exit cleanly in the shell environment.
- `dotnet build <solution>.sln` failed because the `.sln` references a folder with real spaces while the actual checkout folder uses URL-encoded spaces.
- A direct backend scan using `--project <private-aspnet-server-project>` reached reduced semantic coverage, but existing semantic `HttpRouteBinding` extraction produced no route facts because ASP.NET references were unresolved.
- A throwaway source-text matcher found 34 Angular HTTP calls, 37 ASP.NET endpoints, and 34/34 client calls matched server endpoints after case-insensitive route comparison and optional-segment handling.

The implementation should therefore add Angular `HttpClient` call extraction, ASP.NET route syntax fallback, route normalization, and a cross-index alignment report.

## MVP Scope Decisions

- MVP correlation reads two existing indexes: one client index and one server index.
- MVP does not require a single combined database.
- MVP should leave a clear design seam for a later `tracemap combine` command that merges multiple indexes into one queryable dependency graph.
- MVP does not run `npm install`, `npm ci`, `dotnet restore`, or target application builds automatically.
- MVP may expose explicit restore/build commands in docs or smoke instructions, but scanners must label missing dependencies as reduced coverage.
- MVP route extraction targets Angular `HttpClient` on the client side and ASP.NET Core controller attribute routing on the server side.
- MVP route matching is static, source-based evidence. It does not prove runtime middleware, reverse proxies, CORS, auth policies, route constraints, deployment base paths, or SPA fallback behavior.
- MVP supports exactly one client index and one server index. N-way matching waits for `tracemap combine`.
- MVP writes Markdown when `--out` is a file path, writes both Markdown and JSON when `--out` is a directory, and writes JSON only when `--format json` is provided.
- Endpoint alignment implementation lives in a new .NET project, `TraceMap.EndpointAlignment`, while scan-time extractors remain in the language scanner projects.

## Quick Start Workflow

For a nested Angular/ASP.NET app like the private fixture, the intended workflow is:

```bash
node src/typescript/dist/src/cli.js scan \
  --repo "/path/to/backend/App.Api/ClientApp" \
  --out /tmp/app-client

dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo "/path/to/backend/App" \
  --project App.Api/App.Api.csproj \
  --out /tmp/app-server

dotnet run --project src/dotnet/TraceMap.Cli -- endpoints \
  --client-index /tmp/app-client/index.sqlite \
  --server-index /tmp/app-server/index.sqlite \
  --client-label app-client \
  --server-label app-api \
  --out /tmp/app-endpoints
```

Labels are important when both indexes come from the same Git remote and commit SHA. They are human-readable provenance, not evidence.

## Requirements

### Requirement 1: Angular Client HTTP Call Extraction

**User Story:** As a reviewer, I want TraceMap to extract Angular HTTP calls so that I can see which server endpoints a client appears to depend on.

#### Acceptance Criteria

1. WHEN TypeScript code calls `HttpClient.get`, `post`, `put`, `patch`, `delete`, `head`, `options`, or `request` THEN the TypeScript scanner SHALL emit `HttpCallDetected`.
2. WHEN an Angular `HttpClient` call is emitted THEN the fact SHALL include HTTP method, source file, line span, containing class/function when available, rule ID, extractor version, and evidence tier.
3. WHEN the call URL is a string literal or no-substitution template literal THEN the scanner SHALL store a normalized route template, value hash, length, and URL kind.
4. WHEN the call URL is a template literal containing expressions like `${environment.apiUri}/admin/runner/${runnerId}` THEN the scanner SHALL normalize statically visible literal segments and represent expression segments as route parameters.
5. WHEN the call URL starts with a known environment/base URL symbol such as `environment.apiUri` THEN the scanner SHALL record the base symbol and, when the base value is statically visible, the cleartext path suffix such as `/api`; host, scheme, and full raw URL SHALL be hashed rather than stored.
6. WHEN environment files declare `apiUri` or equivalent base URL values THEN the scanner SHALL emit config facts for the key, hash the raw value, and store only the parsed path suffix as cleartext base-path evidence.
7. WHEN a request URL contains a query string THEN the scanner SHALL exclude the query string from `normalizedPathKey`, extract visible query parameter names in sorted order, and record `hasQueryParameters`.
8. WHEN a request body object literal is visible THEN the scanner SHOULD record body field names and a shape hash, not raw values; body-field extraction is not required for the first endpoint-matching slice.
9. WHEN the call has a response generic type like `HttpClient.get<ApiResponse<User>>()` THEN the scanner SHOULD record the generic return type syntax or resolved symbol; response-type extraction is not required for the first endpoint-matching slice.
10. WHEN the URL is dynamically built from variables, concatenation, helper functions, destructured methods, indirect receivers, or unknown template expressions THEN the scanner SHALL emit `HttpCallDetected` with `urlKind = dynamic`, a closed-set `dynamicReason`, and endpoint alignment SHALL classify it as `DynamicClientUrlNeedsReview`.
11. WHEN `node_modules`, `.angular`, `dist`, build output, or package-manager cache folders exist THEN they SHALL be excluded from source extraction by default.
12. WHEN Angular dependencies are missing THEN the scanner SHALL emit useful syntax/structural HTTP facts where possible and record reduced semantic coverage; it SHALL NOT silently upgrade evidence tiers.

### Requirement 2: ASP.NET Endpoint Extraction With Syntax Fallback

**User Story:** As a reviewer, I want TraceMap to extract ASP.NET endpoints even when the solution or project cannot fully load semantically.

#### Acceptance Criteria

1. WHEN Roslyn semantic analysis resolves ASP.NET controller route attributes THEN the existing semantic route extractor SHALL emit Tier1 or Tier2 `HttpRouteBinding` facts.
2. WHEN semantic analysis is unavailable or reduced THEN syntax fallback SHALL inspect C# controller attributes and emit Tier3 `HttpRouteBinding` facts.
3. WHEN a controller has `[Route("api/[controller]")]` or `[Route("api/admin/[controller]")]` THEN syntax fallback SHALL expand `[controller]` from the class name without the `Controller` suffix.
4. WHEN a method has `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, `[HttpDelete]`, `[HttpHead]`, or `[HttpOptions]` THEN syntax fallback SHALL record the HTTP method.
5. WHEN an HTTP verb attribute includes a route template, such as `[HttpPost("log-off")]`, THEN syntax fallback SHALL use that route as the method-level route template.
6. WHEN a method has separate `[Route("...")]` attributes THEN syntax fallback SHALL combine controller-level and method-level route templates.
7. WHEN route templates contain `{id}`, `{id?}`, `{id:guid}`, `{*path}`, or similar ASP.NET route parameter syntax THEN the fact SHALL record parameter names, optional parameter names, constraints when visible, and normalized placeholders.
8. WHEN a controller action has a likely body parameter THEN the fact SHALL record parameter name and type syntax; semantic type identity SHOULD be added when available.
9. WHEN authorization attributes are visible THEN the fact SHOULD record authorization presence and role/policy hashes without raw sensitive values; detailed authorization extraction is not required for the first endpoint-matching slice.
10. WHEN route extraction cannot confidently combine templates, route arguments are non-literal, or unsupported route attributes are encountered THEN the scanner SHALL emit an `AnalysisGap` or `NeedsReview`-eligible fact rather than guessing.
11. WHEN semantic and syntax extraction both produce the same endpoint THEN the scanner or endpoint reader SHALL deduplicate by controller/action/method/normalized route and prefer the stronger evidence tier.

### Requirement 3: Route Normalization

**User Story:** As a maintainer, I want client and server routes normalized consistently so that matching is deterministic and reviewable.

#### Acceptance Criteria

1. WHEN a route is normalized THEN it SHALL have a leading slash, no duplicate slashes, no trailing slash except root, and stable lowercase comparison form.
2. WHEN a route includes a base URL or configured API base path THEN normalization SHALL separate host/scheme evidence from route path evidence and preserve cleartext relative base paths such as `/api` for matching.
3. WHEN a client template expression segment is normalized THEN it SHALL become a parameter placeholder while preserving a parameter name when obvious.
4. WHEN a server route parameter is normalized THEN route constraints SHALL not prevent template matching, but SHALL be retained as evidence.
5. WHEN a server route parameter is optional THEN matching SHALL expand the server route into compatible candidate shapes with and without the optional segment.
6. WHEN route segments differ only by parameter names, such as `{id}` and `${runnerId}`, THEN matching SHALL compare by segment position and SHALL report parameter-name differences as evidence, not as a miss.
7. WHEN ASP.NET route comparison is performed THEN literal segment matching SHALL be case-insensitive by default.
8. WHEN two or more server endpoints can match a client call THEN the matcher SHALL report `AmbiguousMatch` unless a deterministic score selects a clearly better match.
9. WHEN a client call and server endpoint differ by HTTP method but have the same normalized path THEN the matcher SHALL report `MethodMismatch`.
10. WHEN route normalization loses important information THEN the normalized form SHALL include a match-quality or limitation property.
11. WHEN client facts are emitted THEN client `normalizedPathTemplate` and `normalizedPathKey` SHALL be computed at scan time because raw URL/template text is not persisted.
12. WHEN server facts are read THEN server normalized route properties MAY be derived by the endpoint reader from persisted route templates if older semantic facts do not contain normalized fields.

### Requirement 4: Endpoint Alignment Command

**User Story:** As a reviewer, I want a command that compares a client index and a server index so that I can review matched and unmatched endpoints.

#### Acceptance Criteria

1. WHEN the user runs `tracemap endpoints --client-index <path> --server-index <path> --out <path>` THEN TraceMap SHALL read both indexes and write a Markdown endpoint alignment report.
2. WHEN `--format json` is provided THEN TraceMap SHALL write a machine-readable endpoint alignment result.
3. WHEN `--out` is a directory THEN the CLI SHALL write both `endpoint-report.md` and `endpoint-report.json`; WHEN `--out` is a file path THEN the CLI SHALL write the selected `--format`, defaulting to Markdown.
4. WHEN a client call matches a server endpoint exactly by method and normalized path THEN the report SHALL classify it as `MatchedEndpoint`.
5. WHEN a client call matches because the server has an optional route segment THEN the report SHALL classify it as `OptionalSegmentMatch`.
6. WHEN no server endpoint matches a client call THEN the report SHALL classify it as `ClientCallNoServerEndpoint`.
7. WHEN no client call matches a server endpoint THEN the report SHALL classify it as `ServerEndpointNoClientMatch`.
8. WHEN URL or route evidence is dynamic or incomplete THEN the report SHALL classify it as `DynamicClientUrlNeedsReview` or `UnknownAnalysisGap`.
9. WHEN findings are reported THEN each finding SHALL include client evidence when available, server evidence when available, source index identity, rule IDs, evidence tiers, file paths, line spans, scan IDs, commit SHAs, and extractor versions.
10. WHEN `UnknownAnalysisGap` is reported THEN the finding SHALL attach at least one representative `AnalysisGap` fact as client or server evidence whenever the source indexes contain such a fact.
11. WHEN either index has reduced coverage or unknown commit SHA THEN the report SHALL label conclusions as reduced coverage and SHALL not imply full dependency coverage.
12. WHEN `ClientCallNoServerEndpoint` or `ServerEndpointNoClientMatch` is reported THEN the report SHALL state inline that the classification is coverage-relative and is not proof of a broken call, dead code, or unused endpoint.
13. WHEN report JSON is emitted THEN it SHALL include a top-level `reportCoverage` value and `coverageWarnings` array describing reduced source coverage and its implication.

### Requirement 5: Cross-Index Identity and Provenance

**User Story:** As a platform engineer, I want multi-index reports to preserve where each fact came from so that client/server dependencies can be reviewed across repos, roots, and commits.

#### Acceptance Criteria

1. WHEN an endpoint alignment report reads an index THEN it SHALL preserve that index's `scanId`, repo name, remote URL when available, branch when available, commit SHA, scanner version, analysis level, and build status.
2. WHEN two indexes come from the same Git repository but different scan roots THEN the report SHALL distinguish them using scan root metadata or user-provided labels.
3. WHEN scan root metadata is unavailable THEN the endpoint command SHALL allow `--client-label` and `--server-label`.
4. WHEN scanners are updated for this phase THEN they SHALL add non-breaking manifest fields for scan-root identity, including `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash`.
5. WHEN absolute local paths are useful for a local-only report THEN they MAY be emitted behind an explicit option such as `--include-local-paths`; raw absolute paths SHOULD NOT become required for shareable artifacts.
6. WHEN facts are copied into a derived report or future combined database THEN original `factId`, `scanId`, `repo`, `commitSha`, evidence span, rule ID, and evidence tier SHALL be preserved.
7. WHEN indexes are combined in a future command THEN fact IDs SHALL be namespaced by source index or scan ID to avoid collisions.
8. WHEN a repo is dirty or Git metadata is missing THEN the report SHALL preserve the known metadata and label the snapshot as reduced provenance.

### Requirement 6: Future Combined Dependency Database

**User Story:** As a platform engineer, I want a path toward analyzing multiple indexes together so that I can query all dependencies across clients, services, packages, and languages.

#### Acceptance Criteria

1. WHEN this MVP is implemented THEN it SHALL not require changing single-language scan output into a monolithic scan.
2. WHEN designing endpoint alignment tables or JSON output THEN the design SHALL be compatible with a future `tracemap combine --index <path> --label <label> --out <combined.sqlite>` workflow.
3. WHEN adding the next language family, JVM support SHALL assume `tracemap combine` lands first or alongside it so cross-index dependency analysis has a stable home.
4. WHEN a future combine command imports indexes THEN it SHALL store an `index_sources` table containing source index path hash, label, scan ID, repo identity, scan root identity, language, scanner version, analysis level, build status, commit SHA, and imported-at timestamp.
5. WHEN a future combine command imports facts THEN it SHALL store or expose a `combinedFactId` while keeping the original fact ID unchanged.
6. WHEN a future combine command imports symbols and relationships THEN it SHALL namespace language-specific symbol IDs, carry a `language` discriminator, and retain language/package/assembly identity; raw symbol ID equality across languages SHALL NOT imply identity.
7. WHEN endpoint matches are computed from a combined database THEN they SHALL be represented as derived rows, not source facts, unless a future rule catalog entry defines an evidence-backed derived fact type.
8. WHEN future dependency reports traverse multiple indexes THEN every edge SHALL remain traceable to one or more source facts with rule IDs.
9. WHEN future combined databases are used for long-term analysis THEN commit SHA and scan root metadata SHALL allow comparing snapshots from two different commits.

### Requirement 7: Long-Term Snapshot and Diff Readiness

**User Story:** As a maintainer, I want endpoint and dependency data to be stable across scans so that future work can compare two commit hashes.

#### Acceptance Criteria

1. WHEN the same commit, scan options, and shared route-normalization contract are used twice THEN normalized endpoint keys SHALL be stable.
2. WHEN two scans have different commit SHAs THEN the endpoint report JSON SHALL contain enough metadata for a future diff command to compare added, removed, and changed endpoints.
3. WHEN client calls or server endpoints move files but keep the same normalized method/path THEN future diff logic SHOULD be able to classify that as moved evidence, not necessarily removed dependency.
4. WHEN route templates change by literal segment, method, optionality, or parameter count THEN normalized endpoint keys SHALL change deterministically.
5. WHEN route parameter names change but segment structure remains the same THEN matching MAY report a parameter-name change without treating the endpoint as a different dependency.
6. WHEN a scan has `commitSha = "unknown"` THEN the report SHALL not claim long-term snapshot comparability.

### Requirement 8: Coverage, Gaps, and Safety

**User Story:** As a reviewer, I want endpoint alignment to be honest about missing evidence so that reduced scans do not appear clean.

#### Acceptance Criteria

1. WHEN client semantic analysis is reduced because dependencies are missing THEN Angular HTTP calls found by syntax SHALL remain useful but report coverage SHALL be reduced in the source manifest, endpoint report summary, and endpoint report JSON `coverageWarnings`.
2. WHEN server semantic analysis is reduced because project references or framework references are missing THEN ASP.NET syntax route facts SHALL remain useful but report coverage SHALL be reduced in the source manifest, endpoint report summary, and endpoint report JSON `coverageWarnings`.
3. WHEN solution or project paths are broken, such as URL-encoded folder names versus real spaces, THEN scanners SHALL record the build/project gap and continue with fallback where possible.
4. WHEN the target application build has warnings or does not exit cleanly THEN endpoint alignment SHALL not treat that as a clean build signal.
5. WHEN files exceed byte-size limits or are excluded by default directories THEN gaps SHALL identify the category without flooding reports with generated/cache file noise.
6. WHEN `tsconfig*.json` contains comments THEN the TypeScript scanner SHALL parse it as JSONC or otherwise avoid false JSON parse gaps for normal TypeScript config files.
7. WHEN package-lock or generated cache files are scanned as config THEN the scanner SHOULD avoid emitting excessive config facts unless explicitly requested.
8. WHEN raw snippets, raw tokens, raw URLs, or sensitive config values would be stored THEN the implementation SHALL store hashes, lengths, kinds, and spans instead.
9. WHEN TypeScript scanner `buildStatus` is reported THEN it SHALL reflect TypeScript project semantic diagnostics and scan coverage, not `ng build`, bundler, or target application test status.

### Requirement 9: Reports

**User Story:** As a reviewer, I want endpoint reports that are readable enough to drive review and precise enough to audit evidence.

#### Acceptance Criteria

1. WHEN an endpoint report is generated THEN it SHALL include a summary table with matched, optional, method mismatch, client-only, server-only, dynamic, ambiguous, and unknown-gap counts.
2. WHEN matched endpoints are listed THEN each row SHALL include client method/path, server method/path, client file/line, server file/line, client evidence tier, server evidence tier, and static match quality.
3. WHEN server-only endpoints are listed THEN each row SHALL include server method/path, source action, authorization hints when available, source evidence, and an inline caveat that no client match is not dead-code proof.
4. WHEN client-only calls are listed THEN each row SHALL include client method/path, source service method when available, URL kind, source evidence, and an inline caveat that no server match is not broken-call proof.
5. WHEN dynamic or ambiguous findings exist THEN the report SHALL put them in a needs-review section above simple server-only inventory.
6. WHEN analysis gaps and dynamic URLs both exist THEN the report SHALL split needs-review details into dynamic URL and analysis-gap subsections.
7. WHEN method mismatches are listed THEN the report SHALL note they can indicate client bugs, stale evidence, overloaded routes, or scanner ambiguity and require source review.
8. WHEN report JSON is emitted THEN it SHALL contain stable keys suitable for later automation and tests.

### Requirement 10: Tests and Fixtures

**User Story:** As a maintainer, I want focused fixtures proving client/server alignment so that future language work does not break this capability.

#### Acceptance Criteria

1. WHEN a sample Angular client calls a sample ASP.NET server THEN tests SHALL prove exact endpoint matches.
2. WHEN a server route has an optional segment THEN tests SHALL prove a client path with the segment and a client path without the segment can match.
3. WHEN client and server methods differ on the same path THEN tests SHALL report `MethodMismatch`.
4. WHEN the client URL is dynamic THEN tests SHALL report `DynamicClientUrlNeedsReview`.
5. WHEN the server route can only be extracted by syntax fallback THEN tests SHALL still emit `HttpRouteBinding`.
6. WHEN route constraints are present THEN tests SHALL retain constraints in evidence and match by route shape.
7. WHEN TypeScript dependencies are missing THEN tests SHALL verify reduced coverage with useful syntax/client endpoint facts.
8. WHEN C# framework references are missing THEN tests SHALL verify reduced coverage with useful syntax/server endpoint facts.
9. WHEN combining two indexes is not implemented in MVP THEN tests SHALL still verify that endpoint JSON includes source index metadata needed by a later combine command.
10. WHEN duplicate client call sites map to the same server endpoint THEN tests SHALL preserve both call-site evidence rows.
11. WHEN duplicate server routes expose the same method/path from multiple actions THEN tests SHALL report ambiguity or duplicate-route evidence.
12. WHEN URLs contain query strings, fragments, or URL-encoded path segments THEN tests SHALL verify stable path matching and side evidence.

## Required Property-Key Contract

Client `HttpCallDetected` facts SHALL populate these keys where available:

This property contract is new for endpoint-aware facts. Older `HttpCallDetected` facts may lack these properties; the endpoint reader SHALL treat them as `UnknownAnalysisGap` or `DynamicClientUrlNeedsReview`, not crash and not silently match.

| Property | Meaning |
| --- | --- |
| `methodName` | HTTP method in uppercase |
| `httpMethod` | HTTP method in uppercase |
| `urlKind` | `literal`, `template`, or `dynamic` |
| `normalizedPathTemplate` | normalized route path with relative base path applied when visible, but without host or scheme |
| `normalizedPathKey` | stable comparison key |
| `baseUrlSymbol` | e.g. `environment.apiUri` |
| `basePathPrefix` | cleartext relative base path such as `/api` when statically visible |
| `pathParameterNames` | semicolon-separated path/template parameter names |
| `queryParameterNames` | semicolon-separated query parameter names when visible |
| `hasQueryParameters` | `true` when a visible query string or query options are present |
| `clientFramework` | e.g. `angular` |
| `sourceClass` | containing class when visible |
| `sourceMethod` | containing method/function when visible |
| `responseType` | generic response type syntax or resolved symbol |
| `bodyFieldNames` | semicolon-separated visible request body fields |
| `urlHash` | hash of raw URL/template text |
| `dynamicReason` | one of `TemplateExpressionNotResolvable`, `VariableConcatenation`, `HelperFunctionCall`, `IndirectReceiver`, `ComplexExpression`, or `Unknown` |

Server `HttpRouteBinding` facts SHALL populate these keys where available:

Server normalization fields are new. The endpoint reader MAY derive them from existing semantic `routeTemplates`; if neither normalized fields nor usable route templates are present, the candidate SHALL become `UnknownAnalysisGap`.

| Property | Meaning |
| --- | --- |
| `methodName` | HTTP method or comma-separated HTTP methods |
| `httpMethods` | comma-separated HTTP methods |
| `routeTemplates` | original route template evidence, hash-only if needed |
| `normalizedPathTemplate` | normalized route path |
| `normalizedPathKey` | stable comparison key |
| `routeParameterNames` | semicolon-separated route parameter names |
| `optionalParameterNames` | semicolon-separated optional route parameter names |
| `routeConstraints` | semicolon-separated visible constraints |
| `controllerName` | controller class without suffix |
| `actionName` | action method name |
| `bodyParameterNames` | comma/semicolon-separated body parameter names |
| `bodyParameterTypes` | body parameter type syntax or resolved symbols |
| `authorizationKind` | `none`, `authorize`, `allowAnonymous`, or `mixed` when visible |

Endpoint alignment findings SHALL include source fact IDs and SHALL not become source facts unless a future derived-fact rule is documented.

## Known Limitations

- Static endpoint alignment does not prove runtime reachability, runtime route registration, reverse proxy configuration, deployment base path, CORS behavior, auth state, feature flags, or SPA fallback behavior.
- `ServerEndpointNoClientMatch` is not dead-code proof. It only means no matching call was found in the scanned client index at the reported coverage level.
- `ClientCallNoServerEndpoint` is not broken-call proof. It can reflect dynamic routing, reduced server coverage, unscanned server code, proxies, or framework routes outside MVP extraction.
- Angular interceptors can rewrite URLs; MVP records interceptor presence only if a later rule is added.
- Client helper methods can construct URLs dynamically; MVP labels these as needs-review.
- ASP.NET conventional routes, minimal APIs, endpoint filters, route groups, `[Area]`, `AcceptVerbs`, custom route attributes, `[ApiController]` implicit routing, and middleware-generated endpoints are follow-up unless simple extraction can be added without overclaiming.
- Optional segment matching can create ambiguity; ambiguity must be reported rather than silently resolved when scores are tied.
- Multi-index combine and long-term diff are intentionally future work, but this spec preserves the metadata needed to build them.
