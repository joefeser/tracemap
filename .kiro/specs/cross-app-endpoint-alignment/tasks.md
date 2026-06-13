# Cross-App Endpoint Alignment Tasks

## Implementation Status

This spec has been implemented as the first endpoint-alignment slice. The checklist below reflects the state after PR review cleanup.

### Completed

- [x] Support exactly one client index and one server index for MVP.
- [x] Keep N-way endpoint matching for combined database follow-up work.
- [x] Implement endpoint alignment in `src/dotnet/TraceMap.EndpointAlignment`.
- [x] Keep scan-time extractors in `TraceMap.Core` and `src/typescript`.
- [x] Write Markdown for default file output, JSON for `--format json`, and both `endpoint-report.md` plus `endpoint-report.json` for directory output.
- [x] Emit endpoint matches as derived report rows, not source scan facts.
- [x] Add `typescript.integration.angular-httpclient.v1`, `csharp.syntax.aspnetroute.v1`, and `endpoint.alignment.v1` rule catalog entries.
- [x] Add endpoint result models with source fact IDs, scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, scan-root metadata, labels, index path hashes, and `staticMatchQuality`.
- [x] Add `reportCoverage` and `coverageWarnings`.
- [x] Add additive manifest fields for `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash` in .NET and TypeScript scans.
- [x] Normalize client and server routes with stable path templates, path keys, parameter names, optional parameters, constraints, query names, and base path prefixes.
- [x] Cap optional route expansion to prevent exponential route-key generation.
- [x] Safely handle malformed percent-encoded route segments.
- [x] Exclude `.angular`, `node_modules`, build output, caches, and TraceMap output paths from TypeScript inventory by default.
- [x] Parse TypeScript configs with JSONC-compatible APIs.
- [x] Detect Angular `HttpClient` calls with literal, template, environment-base, query-string, and dynamic URL handling.
- [x] Emit dynamic client URL findings as `DynamicClientUrlNeedsReview`.
- [x] Add ASP.NET controller syntax route fallback for controller/action route attributes, inline HTTP verb routes, optional parameters, constraints, body parameter names, and comments avoidance.
- [x] Derive normalized server route properties from persisted semantic `routeTemplates` when normalized fields are missing.
- [x] Combine semantic controller/action route templates before endpoint matching.
- [x] Replace `[controller]` in endpoint-reader fallback using containing type metadata when available.
- [x] Deduplicate server candidates by method and normalized path key, preferring stronger evidence.
- [x] Match exact endpoints, optional segments, method mismatches, dynamic URLs, client-only rows, and server-only rows.
- [x] Add coverage-relative caveats for client-only and server-only findings.
- [x] Attach representative `AnalysisGap` evidence to `UnknownAnalysisGap` findings.
- [x] Add `tracemap endpoints --help`, `--client-index`, `--server-index`, `--out`, `--format`, `--client-label`, and `--server-label`.
- [x] Add Angular and ASP.NET endpoint samples.
- [x] Add endpoint alignment unit tests and sample smoke coverage.
- [x] Record private endpoint smoke observations in `docs/ACCEPTANCE.md`.
- [x] Run local ignored endpoint smoke helper for TypeScript + endpoint alignment.

### Intentionally Partial or Deferred

- [x] `tracemap combine` baseline for N indexes with source-index namespacing and source fact/table imports.
- [ ] Derived cross-index rows on top of combined databases.
- [ ] `tracemap endpoint-diff` for comparing endpoint reports across commit SHAs.
- [ ] N-client-to-N-server endpoint alignment.
- [ ] React, Next.js, Remix, Nest, Fastify, Koa, GraphQL, OpenAPI, and other non-Angular/ASP.NET endpoint families.
- [ ] ASP.NET minimal APIs, conventional routes, route groups, endpoint filters, `AcceptVerbs`, `[Area]`, custom route attributes, and `[ApiController]` implicit route conventions.
- [ ] Angular interceptor URL rewrite evidence.
- [ ] Proxy config, deployment base path, CORS, auth, feature-flag, middleware, and runtime route-table evidence.
- [ ] Response generic type and request body field extraction for endpoint matching.
- [ ] Working-tree dirty state or content snapshot hash.

## Definition of Done

- [x] `dotnet build src/dotnet/TraceMap.sln` passes.
- [x] `dotnet test src/dotnet/TraceMap.sln` passes.
- [x] `cd src/typescript && npm run check` passes.
- [x] Angular `HttpClient` facts are emitted with route normalization properties.
- [x] ASP.NET syntax fallback route facts are emitted without semantic framework references.
- [x] Endpoint alignment command writes Markdown and JSON according to output rules.
- [x] Endpoint reports include source scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, line spans, scan-root metadata, labels, and index path hashes.
- [x] Reduced client/server coverage is labeled in reports and JSON `coverageWarnings`.
- [x] `ServerEndpointNoClientMatch` and `ClientCallNoServerEndpoint` caveats appear inline.
- [x] No raw URL/template/body/config values are stored by default; cleartext relative base path suffixes such as `/api` are allowed.
- [x] Rule catalog entries and limitations are updated.
- [x] Sample client/server alignment tests cover exact, optional, mismatch, dynamic, client-only, server-only, query strings, fragments, malformed path escaping, and route-template fallback.
