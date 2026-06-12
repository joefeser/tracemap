# Cross-App Endpoint Alignment Tasks

## Implementation Plan

### Phase 0: Scope and Review

- [ ] 0.1 Review `requirements.md` for evidence-tier wording and overclaiming.
- [ ] 0.2 Review `design.md` for route normalization, matching semantics, and multi-index design seam.
- [ ] 0.3 Decide whether endpoint alignment gets a new .NET project or lives in an existing project for MVP.
- [ ] 0.4 Decide whether endpoint command emits Markdown only by default or Markdown plus JSON.
- [ ] 0.5 Confirm MVP supports exactly one client index and one server index.
- [ ] 0.6 Confirm `tracemap combine` and endpoint diff are follow-up work, not part of MVP.

### Phase 1: Rule Catalog and Models

- [ ] 1.1 Add `typescript.integration.angular-httpclient.v1` to `rules/rule-catalog.yml`.
- [ ] 1.2 Add `csharp.syntax.aspnetroute.v1` to `rules/rule-catalog.yml`.
- [ ] 1.3 Add `endpoint.alignment.v1` to `rules/rule-catalog.yml` as a derived-report rule with limitations.
- [ ] 1.4 Add or document endpoint alignment classifications: `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientCall`, `AmbiguousMatch`, `DynamicClientUrlNeedsReview`, and `UnknownAnalysisGap`.
- [ ] 1.5 Add endpoint alignment result models with source fact IDs, source scan IDs, repo metadata, commit SHAs, file spans, rule IDs, evidence tiers, and confidence.
- [ ] 1.6 Add tests that fail if endpoint result JSON omits source fact IDs or source scan metadata.

### Phase 2: TypeScript Scanner Hygiene

- [ ] 2.1 Exclude `.angular` from TypeScript file inventory by default.
- [ ] 2.2 Confirm `node_modules`, `dist`, `build`, coverage, package-manager caches, and TraceMap output paths remain excluded.
- [ ] 2.3 Parse `tsconfig*.json` as JSONC instead of strict JSON to avoid comment-related parse gaps.
- [ ] 2.4 Reduce or gate package-lock/generated-cache JSON config expansion so scans do not emit excessive config facts by default.
- [ ] 2.5 Add tests for `.angular` exclusion, JSONC config parsing, and lockfile/config fact volume behavior.

### Phase 3: Angular `HttpClient` Extraction

- [ ] 3.1 Identify Angular `HttpClient` imports from `@angular/common/http`.
- [ ] 3.2 Identify constructor-injected `HttpClient` properties and common local aliases.
- [ ] 3.3 Detect `HttpClient.get`, `post`, `put`, `patch`, `delete`, `head`, `options`, and `request` calls.
- [ ] 3.4 Extract HTTP method from call name or `request(method, url, ...)`.
- [ ] 3.5 Extract literal URL paths and no-substitution template literal paths.
- [ ] 3.6 Extract template literal paths with expression segments represented as route parameters.
- [ ] 3.7 Recognize `environment.apiUri` and similar imported environment symbols as base URL symbols.
- [ ] 3.8 Emit `normalizedPathTemplate`, `normalizedPathKey`, `baseUrlSymbol`, `pathParameterNames`, `urlKind`, `urlHash`, and `clientFramework`.
- [ ] 3.9 Extract containing service class and method/function when available.
- [ ] 3.10 Extract response generic type syntax and request body object shape fields when visible.
- [ ] 3.11 Emit dynamic URL facts with `DynamicClientUrlNeedsReview`-ready properties when normalization is unsafe.
- [ ] 3.12 Add Angular fixture tests covering literal URLs, template URLs, environment base URLs, dynamic URLs, body fields, response generics, and missing dependencies.

### Phase 4: ASP.NET Syntax Route Extraction

- [ ] 4.1 Add C# syntax fallback extractor for controller class route attributes.
- [ ] 4.2 Expand `[controller]` from controller class name without `Controller` suffix.
- [ ] 4.3 Expand `[action]` from action method name when present.
- [ ] 4.4 Detect `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, `[HttpDelete]`, `[HttpHead]`, and `[HttpOptions]`.
- [ ] 4.5 Support inline HTTP verb routes such as `[HttpPost("log-off")]`.
- [ ] 4.6 Support separate method `[Route("...")]` attributes.
- [ ] 4.7 Combine controller and method route templates into one or more endpoint route facts.
- [ ] 4.8 Parse route parameters, optional parameters, constraints, and catch-all markers.
- [ ] 4.9 Extract action method name, controller name, parameter names, body parameter type syntax, and visible authorization metadata.
- [ ] 4.10 Ensure comments and commented-out attributes do not emit active endpoints.
- [ ] 4.11 Add tests for semantic failure/syntax fallback, optional segments, route constraints, inline verb templates, duplicate routes, and multiple HTTP methods.

### Phase 5: Route Normalization Library

- [ ] 5.1 Implement normalization for client templates and server route templates.
- [ ] 5.2 Normalize leading/trailing slashes and duplicate slashes.
- [ ] 5.3 Lowercase literal comparison segments while preserving display templates.
- [ ] 5.4 Convert client template expressions and server route params to placeholders for match keys.
- [ ] 5.5 Preserve parameter names, optional flags, constraints, and catch-all markers as metadata.
- [ ] 5.6 Strip or separate known base URL prefixes.
- [ ] 5.7 Add tests for case-insensitive literals, different parameter names, optional server segments, constraints, query strings, and dynamic/unsafe paths.
- [ ] 5.8 Add golden tests using FFP-like paths such as `/api/admin/reporting/{clubId}` versus `/api/admin/Reporting/{clubId}/{id?}`.

### Phase 6: Endpoint Index Reader

- [ ] 6.1 Add a reader that loads source metadata from `scan_manifest`.
- [ ] 6.2 Load client candidates from `HttpCallDetected` facts with endpoint properties.
- [ ] 6.3 Load server candidates from `HttpRouteBinding` facts with endpoint properties.
- [ ] 6.4 Preserve source fact JSON, rule IDs, evidence tiers, evidence spans, scan IDs, and commit SHAs.
- [ ] 6.5 Support source labels supplied by CLI when scan-root metadata is unavailable.
- [ ] 6.6 Treat missing endpoint properties as `UnknownAnalysisGap` candidates rather than crashing.
- [ ] 6.7 Add tests against tiny SQLite fixture indexes.

### Phase 7: Endpoint Matcher

- [ ] 7.1 Match client and server candidates by normalized route shape and HTTP method.
- [ ] 7.2 Support optional server segment matching.
- [ ] 7.3 Report method mismatches when route shape matches but method differs.
- [ ] 7.4 Report dynamic client URLs as needs-review.
- [ ] 7.5 Report ambiguous matches when top candidate scores tie.
- [ ] 7.6 Report client-only calls.
- [ ] 7.7 Report server-only endpoints.
- [ ] 7.8 Preserve duplicate client call sites that map to the same server endpoint as separate evidence rows, while summary counts may group by endpoint key.
- [ ] 7.9 Add tests for exact, optional, mismatch, ambiguous, dynamic, client-only, server-only, and duplicate-call scenarios.

### Phase 8: CLI and Reports

- [ ] 8.1 Add `tracemap endpoints --help`.
- [ ] 8.2 Add `tracemap endpoints --client-index <path> --server-index <path> --out <path>`.
- [ ] 8.3 Add `--format markdown|json`.
- [ ] 8.4 Add `--client-label` and `--server-label`.
- [ ] 8.5 Write Markdown report with summary, source indexes, coverage/gaps, needs-review, matches, mismatches, client-only, server-only, and limitations.
- [ ] 8.6 Write stable JSON report.
- [ ] 8.7 Add CLI tests for missing args, bad paths, Markdown output, JSON output, labels, and reduced coverage wording.

### Phase 9: Scan Root and Provenance Hooks

- [ ] 9.1 Add additive manifest fields for `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash` in .NET scans.
- [ ] 9.2 Add the same additive manifest fields in TypeScript scans.
- [ ] 9.3 Add optional local absolute root output only behind an explicit future option or document why it remains out of MVP.
- [ ] 9.4 Ensure endpoint reports include source labels and scan-root metadata when available.
- [ ] 9.5 Add tests for same-repo different-root reports.

### Phase 10: Samples and Smoke

- [ ] 10.1 Add a small Angular client sample with `HttpClient` service calls.
- [ ] 10.2 Add a small ASP.NET controller sample with exact, optional, method mismatch, dynamic, and server-only routes.
- [ ] 10.3 Add a sample script or doc command that scans client and server samples and runs endpoint alignment.
- [ ] 10.4 Smoke against the FFP Angular/ASP.NET app and record expected reduced-coverage behavior.
- [ ] 10.5 Ensure smoke docs mention `npm ci` may be needed for target app build, but TraceMap scan itself does not run dependency install automatically.
- [ ] 10.6 Verify FFP endpoint report can line up the known Angular service calls with ASP.NET controllers.

### Phase 11: Documentation and Acceptance

- [ ] 11.1 Update `README.md` with endpoint alignment command examples.
- [ ] 11.2 Update `docs/ACCEPTANCE.md` with cross-app endpoint acceptance cases.
- [ ] 11.3 Update `docs/DECISIONS.md` with the decision to align two existing indexes rather than requiring a combined DB in MVP.
- [ ] 11.4 Add a backlog note for `tracemap combine` and endpoint diff.
- [ ] 11.5 Document limitations around runtime routing, auth, proxies, interceptors, and reduced coverage.

## Follow-Up Backlog

- [ ] `tracemap combine` for N indexes with `index_sources`, namespaced facts, symbols, relationships, and derived endpoint match tables.
- [ ] `tracemap endpoint-diff` for comparing endpoint alignment JSON across two commit SHAs.
- [ ] N-client-to-N-server endpoint alignment.
- [ ] React/Next/Remix client call extraction.
- [ ] OpenAPI/Swagger route import/export.
- [ ] ASP.NET minimal APIs, route groups, endpoint filters, and conventional route extraction.
- [ ] Angular interceptor URL rewrite evidence.
- [ ] Proxy config and deployment base path evidence.
- [ ] Working tree dirty-state or content snapshot hash.

## Definition of Done

- [ ] `dotnet build src/dotnet/TraceMap.sln` passes.
- [ ] `dotnet test src/dotnet/TraceMap.sln` passes.
- [ ] `cd src/typescript && npm run check` passes.
- [ ] Angular `HttpClient` facts are emitted with route normalization properties.
- [ ] ASP.NET syntax fallback route facts are emitted without semantic framework references.
- [ ] Endpoint alignment command writes Markdown and JSON.
- [ ] Endpoint reports include source scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, and line spans.
- [ ] Reduced client/server coverage is labeled in reports.
- [ ] No raw URL/template/body/config values are stored by default.
- [ ] Rule catalog entries and limitations are updated.
- [ ] Sample client/server alignment tests cover exact, optional, mismatch, dynamic, client-only, and server-only scenarios.
