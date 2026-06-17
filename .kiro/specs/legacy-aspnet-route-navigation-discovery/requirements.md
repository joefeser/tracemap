# Legacy ASP.NET Route And Navigation Discovery Requirements

## Introduction

Classic ASP.NET and WebForms applications expose user-reachable surfaces through
checked-in markup, code-behind partial classes, `Global.asax` route
registration, `web.config` handlers/modules/pages settings, `.ashx` handlers,
PageMethods, ASMX-style WebMethods, and static navigation references. TraceMap
already has WebForms event flow, WCF/SVC, ASMX/SOAP, Remoting, HTTP, SQL/query,
legacy data metadata, combined path, and evidence graph export foundations.

This phase specifies a deterministic legacy ASP.NET surface and navigation
extractor that connects UI/page/route evidence to existing backend service/data
evidence more clearly. The feature remains static-only. It must not host IIS or
ASP.NET, crawl a site, execute pages, issue HTTP calls, simulate the ASP.NET
pipeline, resolve runtime authorization, or claim endpoint reachability.

Public claim level: hidden until validated through checked-in synthetic/public
fixtures or reviewed redacted summaries.

## Requirements

### Requirement 1: Classic ASP.NET Surface Inventory

**User Story:** As a maintainer, I want TraceMap to inventory classic ASP.NET
pages, user controls, master pages, handlers, and application files even when the
legacy solution cannot build.

#### Acceptance Criteria

1. WHEN a repository contains `.aspx`, `.ascx`, `.master`, `.ashx`,
   `Global.asax`, related code-behind files, or designer files THEN TraceMap
   SHALL inventory them with safe repository-relative paths and line spans when
   available. Designer files SHALL be supporting identity/linkage evidence only
   and SHALL NOT be treated as standalone route, page, handler, or navigation
   surfaces.
2. WHEN markup directives contain safe attributes such as `Inherits`,
   `CodeBehind`, `CodeFile`, `MasterPageFile`, `Language`, or handler class
   names THEN TraceMap SHALL emit safe page/control/master/handler metadata
   using identifiers, safe filenames, hashes, or omissions as appropriate.
3. WHEN directive or file inventory values contain local absolute paths, URLs,
   config-like values, expressions, unsafe names, secrets, or source snippets
   THEN TraceMap SHALL omit credential-like values and SHALL hash or omit other
   allowed non-secret unsafe values before writing facts, reports, logs, or
   exported graph nodes.
4. WHEN markup, handler directives, or application files are malformed,
   generated, unreadable, or unsupported by the tolerant parser THEN TraceMap
   SHALL emit `AnalysisGap` evidence rather than claiming no surface exists.
5. Surface inventory SHALL reuse existing WebForms facts where their semantics
   already match, and SHALL add distinct route/navigation/handler facts only
   where the existing `WebForms*`, ASMX, WCF, or Remoting facts would be
   overloaded.

### Requirement 2: Global.asax And Route Registration Evidence

**User Story:** As a reviewer, I want statically registered classic ASP.NET
routes to be visible without running the application.

#### Acceptance Criteria

1. WHEN `Global.asax`, `Global.asax.cs`, startup-like WebForms files, or linked
   code declare deterministic route registration patterns such as
   `RouteTable.Routes.MapPageRoute`, `routes.MapPageRoute`,
   `RouteCollection.MapPageRoute`, or static `Route` additions THEN TraceMap
   SHALL emit route facts with safe route-name descriptor or hash, safe route
   pattern hash or safe pattern descriptor, mapped page identity when safe, rule
   ID, evidence tier, line span, commit SHA, and extractor version.
2. WHEN route registration resolves to framework symbols through Roslyn
   semantic analysis THEN TraceMap SHOULD emit `Tier1Semantic` evidence with
   resolved method/type symbols.
3. WHEN semantic analysis is unavailable but syntax or structural evidence
   identifies known route-registration shapes THEN TraceMap SHALL emit
   `Tier3SyntaxOrTextual` or `Tier2Structural` evidence with alias/lookalike
   limitations.
4. WHEN route names, route patterns, page paths, defaults, constraints, or data
   tokens contain unsafe values THEN raw values SHALL NOT be stored. Safe
   descriptors and context-separated hashes MAY be stored when useful for
   deterministic joins.
5. WHEN route construction depends on dynamic strings, config values,
   reflection, external code, loops over unknown data, runtime transforms, or
   custom route subclasses that cannot be understood statically THEN TraceMap
   SHALL emit route-analysis gaps and SHALL NOT invent concrete endpoints.
6. Route evidence SHALL NOT claim the route is deployed, reachable, authorized,
   selected by ASP.NET at runtime, or compatible with production IIS settings.

### Requirement 3: web.config Handlers, Modules, Pages, And Build Settings

**User Story:** As a maintainer, I want checked-in ASP.NET configuration to
explain static endpoint and page behavior without leaking environment values.

#### Acceptance Criteria

1. WHEN `web.config`, nested `web.config`, or related XML config files contain
   deterministic classic ASP.NET sections such as `system.web/httpHandlers`,
   `system.webServer/handlers`, `httpModules`, `modules`, `pages`,
   `controls`, `namespaces`, `urlMappings`, or `compilation` settings THEN
   TraceMap SHALL emit config facts for safe handler/module/page evidence.
2. WHEN config maps safe extensions, verbs, page paths, handler types, module
   types, or tag-prefix/user-control declarations THEN TraceMap SHALL preserve
   only safe identifiers, safe extension patterns, hashes, and supporting fact
   IDs.
3. WHEN config contains URLs, hostnames, local absolute paths, physical paths,
   credentials, connection strings, app settings, secrets, machine keys, raw
   endpoint values, or environment-specific values THEN raw values SHALL NOT be
   written to `facts.ndjson`, `index.sqlite`, Markdown, JSON, logs, or export
   artifacts.
4. WHEN config transforms, external includes, encrypted sections,
   machine.config inheritance, runtime mutations, custom build providers, or
   unsupported custom sections are required to understand a surface THEN
   TraceMap SHALL emit `AnalysisGap` evidence and mark coverage reduced.
5. Config facts MAY support route/page/handler mapping only when static keys and
   file/type evidence make the relationship credible. Matching hashes alone
   SHALL NOT prove reachability or runtime routing.

### Requirement 4: Handler And Page Method Evidence

**User Story:** As a reviewer, I want static handler and AJAX method surfaces to
be visible from `.ashx`, code-behind, and attribute evidence.

#### Acceptance Criteria

1. WHEN `.ashx` directives, `IHttpHandler` or `IHttpAsyncHandler`
   implementations, config handler declarations, or handler factory patterns
   are statically visible THEN TraceMap SHALL emit handler surface facts with
   safe class/type identity, extension or path shape when safe, line span, rule
   ID, evidence tier, and limitations.
2. WHEN C# code declares `[WebMethod]`, `[ScriptMethod]`, `[ScriptService]`, or
   static page methods on WebForms pages THEN TraceMap SHALL emit page method
   or script-service method facts linked to page/service evidence when static
   identity supports the link.
3. WHEN semantic analysis resolves handler interfaces, framework attributes, or
   containing symbols THEN TraceMap SHOULD emit `Tier1Semantic` evidence.
4. WHEN semantic analysis is unavailable THEN syntax fallback SHALL emit
   review-tier evidence only for supported syntax nodes and SHALL NOT treat
   comments, string literals, XML docs, inactive preprocessor regions, or raw
   text as handler or PageMethod evidence.
5. WHEN attribute names such as `WebMethod` or `ScriptMethod` may refer to
   project-defined lookalikes and semantic analysis cannot disambiguate them
   THEN TraceMap SHALL cap the evidence at review tier or emit ambiguity gaps.
6. Handler and PageMethod evidence SHALL stay distinct from ASMX operation facts
   unless an ASMX host/service rule independently supports that classification.

### Requirement 5: Static Navigation Reference Evidence

**User Story:** As a maintainer, I want static page-to-page and control-to-page
navigation clues to connect UI entry points to other surfaces where evidence is
deterministic.

#### Acceptance Criteria

1. WHEN markup or code contains deterministic navigation references such as
   static `NavigateUrl`, `PostBackUrl`, `Action`, `Response.Redirect`,
   `Server.Transfer`, `HyperLink.NavigateUrl`, `MenuItem.NavigateUrl`,
   `SiteMapPath`, `TreeView`, or safe sitemap nodes THEN TraceMap SHALL emit
   navigation reference facts when the reference value is statically safe or
   reducible to a safe descriptor/hash. When the pattern is recognized but the
   reference cannot be safely reduced, TraceMap SHALL emit an `AnalysisGap`
   rather than silently omitting the evidence.
2. Navigation facts SHALL include source surface identity, target descriptor or
   target hash, reference kind, file span, rule ID, evidence tier, supporting
   fact IDs, and limitations.
3. WHEN navigation references target checked-in pages, route names, static
   route patterns, or config mappings with credible static evidence THEN
   TraceMap MAY emit static navigation edges linking the supporting facts.
4. WHEN navigation references are JavaScript-generated, data-bound,
   resource-driven, computed through string concatenation, stored in config or
   database data, role-filtered, localized, or dependent on runtime master page
   or control tree behavior THEN TraceMap SHALL emit gaps or facts at
   `Tier3SyntaxOrTextual` or `Tier4Unknown` evidence tier, with report
   classification capped at `NeedsReview` when aggregated, instead of concrete
   target edges.
5. Navigation evidence SHALL NOT prove a user can reach the target, an auth
   policy allows access, the page renders, a form posts successfully, a script
   executes, or a backend is impacted.

### Requirement 6: Integration With Existing Flow, Reports, And Graph Export

**User Story:** As a TraceMap user, I want route/page/navigation evidence to
participate in existing static reports and evidence exports instead of forming a
separate analyzer island.

#### Acceptance Criteria

1. WHEN classic ASP.NET route/navigation facts are emitted THEN `tracemap scan`,
   `facts.ndjson`, `index.sqlite`, `scan-manifest.json`, `report.md`, and
   `logs/analyzer.log` SHALL preserve deterministic counts, rule IDs, evidence
   tiers, file spans, commit SHA, extractor versions, coverage labels, and
   limitations.
2. WHEN WebForms event flow, ASMX/SOAP, WCF, Remoting, HTTP/API,
   `HttpRouteBinding` facts from `csharp.syntax.aspnetroute.v1`, SQL/query,
   legacy data metadata, dependency paths, combined reports, reverse queries,
   release-review, or portfolio commands consume the new facts THEN they SHALL
   either integrate them through existing shared models or emit availability
   gaps when precision is unavailable.
3. WHEN a route/page/navigation surface connects to existing WebForms handler,
   PageMethod, service client, ASMX/WCF operation, Remoting, SQL/query, or
   legacy data evidence through static facts and edges THEN path reports MAY
   show a possible static path with supporting fact IDs and weakest-evidence
   confidence caps.
4. WHEN evidence graph or vault export includes these facts THEN exported nodes
   and edges SHALL be redacted, rule-backed, stable, and linked to source facts;
   unsafe route/page/config/navigation values SHALL remain hashed or omitted.
5. Reports SHALL use wording such as "static ASP.NET surface evidence",
   "route candidate", "navigation reference candidate", and "possible static
   path". They SHALL NOT say an endpoint exists at runtime, a page was browsed,
   a request was handled, a user can navigate there, or code is impacted at
   runtime.

### Requirement 7: Rules, Evidence Tiers, Coverage, And Limitations

**User Story:** As a reviewer, I want every route/navigation conclusion backed
by a documented rule and explicit limitations.

#### Acceptance Criteria

1. WHEN route/navigation facts, gaps, report rows, path rows, or export nodes are
   emitted THEN every conclusion SHALL cite a rule ID documented in
   `rules/rule-catalog.yml`. Rule catalog changes are implementation-slice work
   and SHALL land in the same PR that first emits the corresponding facts or
   rows.
2. Rule catalog entries SHALL document limitations for static evidence, dynamic
   route construction, master pages, nested user controls, generated designer
   files, control trees, reflection, runtime config transforms, machine.config,
   URL rewriting, auth/role gating, custom route handlers, JavaScript-generated
   navigation, data-bound navigation, deployment, reachability, and runtime
   execution.
3. Evidence tiers SHALL follow this guidance:
   - `Tier1Semantic` for compiler-resolved ASP.NET framework route calls,
     handler interfaces, attributes, page methods, and type symbols.
   - `Tier2Structural` for parseable markup directives, `.ashx` directives,
     `web.config` handler/module/pages structures, sitemap structures, and
     statically linked page/route/config relationships.
   - `Tier3SyntaxOrTextual` for syntax-only route calls, unresolved attributes,
     safe static navigation strings, and name-only page or handler matches.
   - `Tier4Unknown` for malformed files, dynamic route construction, ambiguous
     targets, unsupported config transforms, encrypted config, custom runtime
     providers, JavaScript-generated navigation, missing extractor capability,
     or older-index availability gaps.
4. Evidence aggregation SHALL never upgrade a path above the weakest required
   supporting evidence and SHALL label partial or reduced analysis explicitly.
5. No conclusion may be emitted without a rule ID, evidence tier, safe file
   path, line span when known, commit SHA, extractor identity, and documented
   limitations.

### Requirement 8: Validation And Smoke Catalog

**User Story:** As a maintainer, I want fixtures and smoke guidance that prove
the extractor is deterministic, conservative, and safe.

#### Acceptance Criteria

1. WHEN this spec is implemented THEN focused fixtures SHALL cover `.aspx`,
   `.ascx`, `.master`, `.ashx`, code-behind partial classes, `Global.asax`
   route registration, `web.config` handlers/modules/pages settings,
   PageMethods/WebMethods/ScriptMethods, static navigation references,
   ambiguous targets, malformed files, reduced build coverage, and redaction.
2. Tests SHALL prove WCF/SVC, ASMX/SOAP, Remoting, WebForms event flow, and new
   route/navigation facts remain distinct unless a rule-backed integration edge
   connects them.
3. Tests SHALL prove dynamic routes, JavaScript-generated links, master-page or
   user-control runtime composition, auth/role gating, runtime config
   transforms, reflection, and unavailable generated files produce gaps or
   review-tier evidence, not strong endpoint claims.
4. Tests SHALL prove deterministic ordering and byte-stable output for fixed
   inputs.
5. Tests SHALL prove raw local absolute paths, raw remotes, source snippets,
   config values, URLs, hostnames, credentials, secrets, endpoint values, and
   private sample identifiers do not appear in generated artifacts.
6. Validation SHALL include `dotnet build`, `dotnet test`, private-path guard,
   `git diff --check`, and relevant pinned legacy smoke checks from
   `docs/VALIDATION.md`, or explicitly defer smoke checks with rationale in the
   implementation state note.
7. The legacy smoke catalog SHALL be extended only with neutral labels, rule
   IDs, expected evidence families, tiers, states, limitations, and sanitized
   command templates. Local sample paths or private source identities SHALL NOT
   be committed.
