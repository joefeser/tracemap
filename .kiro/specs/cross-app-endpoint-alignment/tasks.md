# Cross-App Endpoint Alignment Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [ ] 0.1 Confirm MVP supports exactly one client index and one server index.
- [ ] 0.2 Confirm N-way endpoint matching waits for future `tracemap combine`.
- [ ] 0.3 Confirm endpoint alignment code lives in a new `TraceMap.EndpointAlignment` .NET project.
- [ ] 0.4 Confirm scan-time extractors stay in `TraceMap.Core` and `src/typescript`.
- [ ] 0.5 Confirm output behavior: file path writes selected format, directory writes both Markdown and JSON, and default file format is Markdown.
- [ ] 0.6 Confirm endpoint matches are derived report rows, not source facts.
- [ ] 0.7 Confirm `tracemap combine` and `tracemap endpoint-diff` are follow-up work.

### Phase 1: Rule Catalog, Models, Provenance, and Test Fixtures

- [ ] 1.1 Add `typescript.integration.angular-httpclient.v1` to `rules/rule-catalog.yml`.
- [ ] 1.2 Add `csharp.syntax.aspnetroute.v1` to `rules/rule-catalog.yml`.
- [ ] 1.3 Add `endpoint.alignment.v1` to `rules/rule-catalog.yml` as a derived-report rule with limitations.
- [ ] 1.4 Add extractor/alignment version constants for the new TypeScript extractor, C# syntax extractor, and endpoint alignment engine.
- [ ] 1.5 Add or document endpoint alignment classifications: `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientMatch`, `AmbiguousMatch`, `DynamicClientUrlNeedsReview`, and `UnknownAnalysisGap`.
- [ ] 1.6 Add endpoint alignment result models with source fact IDs, source scan IDs, repo metadata, commit SHAs, file spans, rule IDs, client/server evidence tiers, and `staticMatchQuality`.
- [ ] 1.7 Add `coverageWarnings` and top-level `reportCoverage` to endpoint report models.
- [ ] 1.8 Add a test-only SQLite fixture builder that can insert scan manifests and code facts with arbitrary properties.
- [ ] 1.9 Add tests that fail if endpoint result JSON omits source fact IDs, source scan metadata, scan-root metadata, or index path hashes.
- [ ] 1.10 Add additive manifest fields for `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash` in .NET scans.
- [ ] 1.11 Add the same additive manifest fields in TypeScript scans.
- [ ] 1.12 Ensure reports can use `--client-label` and `--server-label` when old indexes lack scan-root metadata.

### Phase 2: Shared Route Normalization Contract

- [ ] 2.1 Define golden route-normalization vectors shared by TypeScript and .NET tests before extractor implementation.
- [ ] 2.2 Normalize leading/trailing slashes and duplicate slashes.
- [ ] 2.3 Lowercase literal comparison segments while preserving display templates.
- [ ] 2.4 Convert client template expressions and server route params to placeholders for match keys.
- [ ] 2.5 Preserve parameter names, optional flags, constraints, and catch-all markers as metadata.
- [ ] 2.6 Extract cleartext relative base path suffixes such as `/api` from statically visible environment/base URL values, while hashing host/scheme/full values.
- [ ] 2.7 Strip query strings and fragments from `normalizedPathKey`.
- [ ] 2.8 Extract visible query parameter names in sorted order and set `hasQueryParameters`.
- [ ] 2.9 Add optional server segment expansion rules and tests.
- [ ] 2.10 Add tests for case-insensitive literals, different parameter names, optional server segments, constraints, query strings, fragments, URL-encoded path segments, and dynamic/unsafe paths.
- [ ] 2.11 Add FFP-like golden tests such as `/api/admin/reporting/{clubId}` versus `/api/admin/Reporting/{clubId}/{id?}`.

### Phase 3: TypeScript Scanner Hygiene

- [ ] 3.1 Exclude `.angular` from TypeScript file inventory by default.
- [ ] 3.2 Confirm `node_modules`, `dist`, `build`, coverage, package-manager caches, and TraceMap output paths remain excluded.
- [ ] 3.3 Parse `tsconfig*.json` as JSONC instead of strict JSON to avoid comment-related parse gaps.
- [ ] 3.4 Add tests for `.angular` exclusion and JSONC config parsing.
- [ ] 3.5 Decide whether package-lock/generated-cache JSON config expansion is reduced in MVP or deferred; if MVP, add gating and fact-volume tests.

### Phase 4: Angular `HttpClient` Extraction

- [ ] 4.1 Identify Angular `HttpClient` imports from `@angular/common/http`, including aliased imports.
- [ ] 4.2 Identify constructor-injected `HttpClient` properties.
- [ ] 4.3 Identify simple local aliases assigned from known `HttpClient` properties.
- [ ] 4.4 Detect `HttpClient.get`, `post`, `put`, `patch`, `delete`, `head`, `options`, and `request` calls.
- [ ] 4.5 Extract HTTP method from call name or `request(method, url, ...)`; unknown method expressions become needs-review.
- [ ] 4.6 Extract literal URL paths and no-substitution template literal paths.
- [ ] 4.7 Extract `ts.TemplateExpression` URLs by combining `head.text`, `templateSpans`, `span.expression`, and `span.literal.text`; expression segments become route parameters when safe.
- [ ] 4.8 Recognize `environment.apiUri` and similar imported environment symbols as base URL symbols.
- [ ] 4.9 Extract base path suffixes from statically visible environment values and store them as `basePathPrefix`.
- [ ] 4.10 Emit `normalizedPathTemplate`, `normalizedPathKey`, `baseUrlSymbol`, `basePathPrefix`, `pathParameterNames`, `queryParameterNames`, `hasQueryParameters`, `urlKind`, `urlHash`, and `clientFramework`.
- [ ] 4.11 Extract containing service class and method/function when available.
- [ ] 4.12 Emit dynamic URL facts with closed-set `dynamicReason` values when normalization is unsafe.
- [ ] 4.13 Emit reduced coverage gaps when Angular dependencies are missing and semantic receiver resolution is unavailable.
- [ ] 4.14 Treat destructured methods or indirect receivers that cannot be proven as `DynamicClientUrlNeedsReview` in MVP.
- [ ] 4.15 Defer response generic type and request body field extraction unless the first slice is already stable.
- [ ] 4.16 Add Angular fixture tests covering literal URLs, template URLs, environment base paths, dynamic URLs, query strings, aliased imports, local aliases, indirect receivers, missing dependencies, and reduced-tier behavior.

### Phase 5: ASP.NET Syntax Route Extraction

- [ ] 5.1 Add C# syntax fallback extractor for controller class route attributes.
- [ ] 5.2 Expand `[controller]` from controller class name without `Controller` suffix.
- [ ] 5.3 Expand `[action]` from action method name when present and document casing behavior.
- [ ] 5.4 Detect `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, `[HttpDelete]`, `[HttpHead]`, and `[HttpOptions]`.
- [ ] 5.5 Support inline HTTP verb routes such as `[HttpPost("log-off")]` for literal string arguments only.
- [ ] 5.6 Emit a gap or needs-review fact for non-literal route arguments such as `[HttpGet(Routes.GetAll)]`.
- [ ] 5.7 Support separate method `[Route("...")]` attributes.
- [ ] 5.8 Combine controller and method route templates into one or more endpoint route facts.
- [ ] 5.9 Support absolute method routes using `~/` or leading `/` as controller-prefix overrides.
- [ ] 5.10 Parse route parameters, optional parameters, constraints, and catch-all markers.
- [ ] 5.11 Extract action method name, controller name, parameter names, body parameter type syntax, and visible authorization metadata when straightforward.
- [ ] 5.12 Ensure comments and commented-out attributes do not emit active endpoints.
- [ ] 5.13 Deduplicate semantic and syntax route facts by controller/action/method/normalized route, preferring stronger evidence.
- [ ] 5.14 Document `[Area]`, `AcceptVerbs`, `[ApiController]` implicit routing, conventional routes, minimal APIs, route groups, and endpoint filters as out of MVP unless separately implemented.
- [ ] 5.15 Add tests for semantic failure/syntax fallback, optional segments, route constraints, inline verb templates, absolute route override, non-literal route gaps, duplicate routes, multiple HTTP methods, and comments.

### Phase 6: Endpoint Index Reader

- [ ] 6.1 Add a reader that loads source metadata from `scan_manifest`.
- [ ] 6.2 Load client candidates from `HttpCallDetected` facts with the new endpoint properties.
- [ ] 6.3 Treat old `HttpCallDetected` facts without `normalizedPathKey` as `UnknownAnalysisGap` or `DynamicClientUrlNeedsReview`; do not crash or silently match.
- [ ] 6.4 Load server candidates from `HttpRouteBinding` facts.
- [ ] 6.5 Derive server normalized properties from persisted `routeTemplates` when semantic facts lack normalized fields.
- [ ] 6.6 Treat server facts with neither `normalizedPathKey` nor usable `routeTemplates` as `UnknownAnalysisGap`.
- [ ] 6.7 Preserve source fact JSON, rule IDs, evidence tiers, evidence spans, scan IDs, commit SHAs, branch, scanner version, build status, analysis level, language, labels, scan-root metadata, and index path hash.
- [ ] 6.8 Support source labels supplied by CLI when scan-root metadata is unavailable.
- [ ] 6.9 Add tests against tiny SQLite fixture indexes, old-style facts, missing properties, same-repo different-root indexes, and unknown commit SHA.

### Phase 7: Endpoint Matcher

- [ ] 7.1 Match client and server candidates by segment-level route shape and HTTP method.
- [ ] 7.2 Support optional server segment expansion.
- [ ] 7.3 Report method mismatches when route shape matches but method differs.
- [ ] 7.4 Report dynamic client URLs as needs-review.
- [ ] 7.5 Report ambiguous matches when top candidate scores tie between materially different endpoints.
- [ ] 7.6 Report parameter-name differences as evidence notes, not ambiguity, when route shape is otherwise identical.
- [ ] 7.7 Report `ClientCallNoServerEndpoint` with coverage-relative caveats.
- [ ] 7.8 Report `ServerEndpointNoClientMatch` with dead-code caveats.
- [ ] 7.9 Preserve duplicate client call sites that map to the same server endpoint as separate evidence rows, while summary counts may group by endpoint key.
- [ ] 7.10 Report duplicate server route conflicts when the same method/path appears from multiple actions.
- [ ] 7.11 Add tests for exact, optional, mismatch, ambiguous, dynamic, client-only, server-only, duplicate-call, duplicate-server-route, query-string, fragment, URL-encoded path, and parameter-name-difference scenarios.

### Phase 8: CLI and Reports

- [ ] 8.1 Add `tracemap endpoints --help`.
- [ ] 8.2 Add `tracemap endpoints --client-index <path> --server-index <path> --out <path>`.
- [ ] 8.3 Add `--format markdown|json`.
- [ ] 8.4 Add `--client-label` and `--server-label`.
- [ ] 8.5 Implement file-output behavior: default Markdown file, JSON file with `--format json`, and both Markdown/JSON for directory output.
- [ ] 8.6 Write Markdown report with summary, source indexes, coverage/gaps, needs-review dynamic URLs, needs-review analysis gaps, matches, mismatches, client-only, server-only, and limitations.
- [ ] 8.7 Add inline caveats to client-only and server-only sections.
- [ ] 8.8 Display client and server evidence tiers separately in matched rows.
- [ ] 8.9 Use `staticMatchQuality`, not generic `confidence`, in JSON and Markdown.
- [ ] 8.10 Write stable JSON report with `reportCoverage`, `coverageWarnings`, `sources`, `summary`, and `findings`.
- [ ] 8.11 Add CLI tests for missing args, bad paths, Markdown output, JSON output, directory output, labels, reduced coverage wording, and old index facts.

### Phase 9: Samples and Smoke

- [ ] 9.1 Add a small Angular client sample with `HttpClient` service calls.
- [ ] 9.2 Add a small ASP.NET controller sample with exact, optional, method mismatch, dynamic, and server-only routes.
- [ ] 9.3 Add a sample script or doc command that scans client and server samples and runs endpoint alignment.
- [ ] 9.4 Use internal fixtures for exact-count tests.
- [ ] 9.5 Smoke against the FFP Angular/ASP.NET app and record observed counts, reduced-coverage behavior, and known quirks.
- [ ] 9.6 Ensure smoke docs mention `npm ci` may be needed for target app build, but TraceMap scan itself does not run dependency install automatically.
- [ ] 9.7 Verify FFP endpoint report contains matched, optional-segment, server-only, and coverage-warning sections without asserting exact counts.
- [ ] 9.8 Document known FFP quirks: solution path space-vs-%20 mismatch and Angular CLI successful output with non-clean shell exit.

### Phase 10: Documentation and Acceptance

- [ ] 10.1 Update `README.md` with endpoint alignment command examples, including nested same-repo client/server labels.
- [ ] 10.2 Update `docs/ACCEPTANCE.md` with cross-app endpoint acceptance cases.
- [ ] 10.3 Update `docs/DECISIONS.md` with the decision to align two existing indexes rather than requiring a combined DB in MVP.
- [ ] 10.4 Add a backlog note for `tracemap combine` and endpoint diff.
- [ ] 10.5 Document limitations around runtime routing, auth, proxies, interceptors, reduced coverage, server-only not dead-code proof, and client-only not broken-call proof.

## Follow-Up Backlog

- [ ] `tracemap combine` for N indexes with `index_sources`, namespaced facts, symbols, relationships, and derived endpoint match tables.
- [ ] `tracemap endpoint-diff` for comparing endpoint alignment JSON across two commit SHAs.
- [ ] N-client-to-N-server endpoint alignment.
- [ ] React/Next/Remix client call extraction.
- [ ] OpenAPI/Swagger route import/export.
- [ ] ASP.NET minimal APIs, conventional routes, route groups, endpoint filters, `AcceptVerbs`, `[Area]`, and `[ApiController]` implicit routes.
- [ ] Angular interceptor URL rewrite evidence.
- [ ] Proxy config and deployment base path evidence.
- [ ] Response generic type and request body field extraction if deferred from the first slice.
- [ ] Working tree dirty-state or content snapshot hash.

## Definition of Done

- [ ] `dotnet build src/dotnet/TraceMap.sln` passes.
- [ ] `dotnet test src/dotnet/TraceMap.sln` passes.
- [ ] `cd src/typescript && npm run check` passes.
- [ ] Angular `HttpClient` facts are emitted with route normalization properties.
- [ ] ASP.NET syntax fallback route facts are emitted without semantic framework references.
- [ ] Endpoint alignment command writes Markdown and JSON according to the output rules.
- [ ] Endpoint reports include source scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, line spans, scan-root metadata, labels, and index path hashes.
- [ ] Reduced client/server coverage is labeled in reports and JSON `coverageWarnings`.
- [ ] `ServerEndpointNoClientMatch` and `ClientCallNoServerEndpoint` caveats appear inline.
- [ ] No raw URL/template/body/config values are stored by default; cleartext relative base path suffixes such as `/api` are allowed.
- [ ] Rule catalog entries and limitations are updated.
- [ ] Sample client/server alignment tests cover exact, optional, mismatch, dynamic, client-only, server-only, duplicate routes, duplicate calls, query strings, fragments, and URL-encoded paths.
