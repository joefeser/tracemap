# Legacy ASP.NET Route And Navigation Discovery Design

## Overview

Add deterministic classic ASP.NET surface, route, handler, PageMethod, and
navigation evidence as an additive legacy extractor and reporting input. The
feature should sit beside the existing WebForms event-flow, ASMX/SOAP, WCF/SVC,
Remoting, HTTP/API, SQL/query, legacy data metadata, combined paths, and
evidence graph export work.

Intended evidence chain:

```text
ASP.NET page/control/master/handler/route/config/navigation evidence
  -> linked code-behind partial class or handler/page method symbol
  -> existing call/object/service-client/static dependency evidence
  -> ASMX/WCF/Remoting/HTTP/SQL/legacy data terminal evidence where available
  -> combined reports, paths, reverse queries, and graph/vault export
```

Every edge remains static evidence. The implementation must not host IIS, run
ASP.NET, crawl pages, execute scripts, call HTTP endpoints, load production
configuration, simulate route matching, simulate master page/control trees,
resolve auth/roles, or claim runtime reachability.

## Goals

- Inventory classic ASP.NET application surfaces from checked-in files.
- Extract static route registration evidence from `Global.asax` and linked code.
- Extract checked-in config evidence for handlers, modules, pages, controls, and
  URL mappings without leaking raw values.
- Identify `.ashx`, handler interfaces, PageMethods, ScriptMethods, and
  script-service method surfaces.
- Emit deterministic navigation reference facts and conservative static edges
  only when target evidence is credible.
- Connect new evidence to existing WebForms event flow, ASMX/WCF/Remoting,
  dependency paths, reports, reverse queries, and evidence graph/vault export.
- Preserve rule IDs, evidence tiers, file spans, commit SHA, extractor versions,
  coverage labels, supporting fact/edge IDs, and limitations.

## Non-Goals

- No product-code implementation in this spec branch.
- No ASP.NET runtime hosting, IIS probing, browser crawling, JavaScript
  execution, app execution, live HTTP calls, or route simulation.
- No proof that a route/page/handler is deployed, reachable, authorized, or
  selected by the ASP.NET runtime.
- No full master page, nested user-control, ViewState, postback, control tree,
  URL rewriting, session, auth, role, or config-transform simulation.
- No inference from database-backed navigation, localization resources, dynamic
  menus, reflection, custom route providers, or runtime DI.
- No LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact analysis in TraceMap core.
- No raw source snippets, config values, endpoint addresses, URLs, hostnames,
  local absolute paths, raw remotes, credentials, or private labels in outputs.

## Relationship To Existing Legacy Work

| Existing area | Relationship |
| --- | --- |
| WebForms event flow | Existing `WebFormsPageDeclared`, `WebFormsEventBindingDeclared`, and `WebFormsHandlerResolved` facts remain the event-entry evidence. This spec adds route/navigation surfaces that can point to those pages and handlers. |
| HTTP/API attribute routes | Existing `csharp.syntax.aspnetroute.v1` emits `HttpRouteBinding` facts for MVC/Web API controller-style attribute route syntax. This spec covers classic ASP.NET `Global.asax`/`MapPageRoute`/handler/page/navigation evidence; the two route families must not be merged or double-emitted, and consumers may join them only through rule-backed supporting fact IDs or existing HTTP/flow models. |
| ASMX/SOAP | PageMethods and ScriptService evidence can resemble ASMX attributes, but ASMX host/operation facts remain ASMX-owned unless an `.asmx` host or ASMX service rule independently supports that classification. |
| WCF/SVC | WCF service hosts and service-reference metadata remain WCF-owned. Routes or navigation edges may point to code that calls WCF clients through existing mapping evidence. |
| Remoting | Remoting facts remain a separate legacy service-boundary family. Route/page paths may terminate at Remoting evidence only through existing static call or mapping facts. |
| Combined paths/reverse/release review | These consumers may use new surface roots and navigation edges where selector/output models can represent them safely, or emit availability gaps. |
| Evidence graph/vault export | New route/page/navigation nodes and edges should be exported only with safe identities, source fact links, rule IDs, and limitations. |
| Legacy smoke catalog | Add neutral route/navigation expectations only after implementation fixtures or reviewed public summaries exist. |

## Proposed Fact Types

Suggested additive fact types:

- `AspNetSurfaceDeclared`
- `AspNetRouteDeclared`
- `AspNetConfigSurfaceDeclared`
- `AspNetHandlerDeclared`
- `AspNetPageMethodDeclared`
- `AspNetNavigationReferenceDeclared`
- `AspNetNavigationEdgeDeclared`

Existing `WebFormsPageDeclared`, `WebFormsControlDeclared`,
`WebFormsEventBindingDeclared`, `WebFormsHandlerResolved`, and
`WebFormsEventFlowProjected` should be reused where their semantics already
match. Do not duplicate page inventory solely to rename existing WebForms facts;
emit `AspNetSurfaceDeclared` when the evidence is broader than WebForms event
inventory or represents route/config/handler-specific surface context.

Use existing `AnalysisGap` for malformed directives, unsupported config,
dynamic routes, ambiguous navigation targets, JavaScript-generated links,
runtime-only providers, encrypted config, missing generated files, and older
index/extractor availability gaps.

## Proposed Rule IDs

Implementation slices that first emit facts or rows should add rule catalog
entries such as:

- `legacy.aspnet.surface.v1`
  - Inventories classic ASP.NET page/control/master/handler/application
    surfaces from checked-in files and directives.
  - Designer file orphan gaps, where no matching page/control evidence exists,
    are emitted under this rule.
  - Limitations: static files only; no page activation, runtime compilation,
    IIS deployment, auth, or reachability proof.
- `legacy.aspnet.route.v1`
  - Extracts static route registration evidence from `Global.asax` and linked
    code.
  - Limitations: no route table execution, dynamic route construction, runtime
    config, URL rewriting, auth, deployment, or request matching proof.
- `legacy.aspnet.config.v1`
  - Extracts checked-in handler/module/pages/urlMappings evidence from config.
  - Limitations: checked-in config only; transforms, includes, encrypted
    sections, machine.config, runtime mutations, and custom sections are gaps.
- `legacy.aspnet.handler.v1`
  - Detects `.ashx`, `IHttpHandler`, `IHttpAsyncHandler`, handler factories, and
    config-backed handler declarations.
  - Limitations: static declaration only; no request execution, factory result,
    pipeline ordering, auth, or deployment proof.
- `legacy.aspnet.page-method.v1`
  - Detects PageMethods, ScriptMethods, ScriptService methods, and safe page
    method links.
  - Limitations: static attributes and symbols only; no AJAX call execution,
    serialization behavior, auth, or script reachability proof.
- `legacy.aspnet.navigation.v1`
  - Extracts deterministic static navigation references and optional target
    edges.
  - Limitations: no browser behavior, JavaScript execution, data-bound menus,
    role trimming, master-page/control-tree simulation, or user reachability
    proof.

Default flow/report integration should reuse the existing `legacy.flow.*` rules
where those models can represent route/page/navigation roots and surfaces
without changing semantics. `legacy.aspnet.flow.v1` and
`AspNetSurfaceFlowProjected` are deferred unless an implementation slice
determines existing flow rules cannot represent route/page/navigation roots
without semantic overload. That determination must be recorded in
`implementation-state.md` under `Flow Rule Decision`, and
`legacy.aspnet.flow.v1` must be added to `rules/rule-catalog.yml` before any
fact or report row cites it.

## Evidence Tiers

| Evidence | Tier |
| --- | --- |
| Compiler-resolved ASP.NET framework route methods, handler interfaces, framework attributes, page method symbols, linked code-behind symbols | `Tier1Semantic` |
| Parseable markup/handler directives, `web.config` handler/module/pages/urlMappings structures, sitemap structures, deterministic file-to-page/config relationships | `Tier2Structural` |
| Syntax-only route calls, unresolved PageMethod/WebMethod/ScriptMethod attributes, safe static navigation strings, name-only page or handler matches scoped by file/type evidence | `Tier3SyntaxOrTextual` |
| Malformed files, dynamic route construction, ambiguous targets, generated JavaScript navigation, data-bound menus, encrypted config, transforms/includes, custom providers, reflection, missing extractor capability | `Tier4Unknown` through `AnalysisGap` |

Semantic evidence improves identity and de-duplication but must not be required
for syntax, markup, config, and navigation fallback.

## File And Artifact Inventory

Inspect:

- `.aspx`, `.ascx`, `.master`
- `.ashx`, `Global.asax`
- `.aspx.cs`, `.ascx.cs`, `.master.cs`, `.ashx.cs`, `Global.asax.cs`
- `.designer.cs` files that support WebForms/page identity
- checked-in `web.config` files
- checked-in `.sitemap` files when they are safe XML structures

Inventory uses repository-relative paths only. Source snippets and raw local
paths are never stored.

The MVP should avoid a second full page inventory when existing
`WebFormsPageDeclared` facts already provide page/control/master identity. New
facts should add:

- route registration context,
- config handler/module/pages declarations,
- handler/page-method surfaces,
- navigation references and target edges,
- availability gaps for older indexes that lack this extractor.

## Markup And Directive Parsing

Use tolerant directive parsing because WebForms and `.ashx` files often contain
ASP.NET directive syntax rather than strict XML.

Safe directive fields:

- `directiveKind`
- `language`
- `inheritsTypeName`
- `handlerTypeName`
- `codeBehindFile`
- `codeFile`
- `masterPageFileName`
- `autoEventWireup` when already safe and explicit
- `surfaceKind`
- `directiveHash` when unsupported attributes exist

Path-like directive values should be reduced to safe filenames or safe
repo-relative descriptors only when clearly safe. Values containing path
separators outside expected repo-relative forms, URLs, environment markers,
expressions, config substitutions, or unsafe content are omitted or hashed.

Malformed directives emit `AnalysisGap` with the relevant
`legacy.aspnet.*.v1` rule.

## Route Extraction

Candidate sources:

- `Global.asax` directive and linked code-behind;
- `Application_Start` and static registration methods reachable by direct calls
  from `Application_Start`;
- Roslyn syntax nodes for route registration calls;
- semantic symbols for `System.Web.Routing.RouteTable`,
  `System.Web.Routing.RouteCollection`, `MapPageRoute`, and static `Route`
  construction when available.

MVP supported shapes:

- `RouteTable.Routes.MapPageRoute(name, routeUrl, physicalFile, ...)`;
- `routes.MapPageRoute(name, routeUrl, physicalFile, ...)` where `routes` is
  semantically or structurally scoped as a route collection;
- `RouteTable.Routes.Add(name, new Route(...))` only when the pattern and
  handler/page target are simple static arguments.

Tiering for these shapes:

- compiler-resolved framework calls and mapped page symbols emit
  `Tier1Semantic`;
- parseable `Global.asax` directive plus statically scoped route-collection
  structure emits `Tier2Structural`;
- syntax-only route calls with safe simple arguments but no resolved route
  collection symbol emit `Tier3SyntaxOrTextual`;
- ambiguous route collection aliases, dynamic values, custom route classes, or
  unresolved target construction emit `Tier4Unknown` gaps or review-tier syntax
  evidence rather than concrete routes.

Safe route properties:

- safe route name descriptor or hash;
- route pattern descriptor or hash;
- mapped page file name or hash;
- registration method symbol when available;
- support IDs for `Global.asax`, code-behind, route call, and mapped page facts;
- route value/constraint/data-token presence flags rather than raw values.

Dynamic string building, loops over unknown data, config-backed patterns,
custom route classes, custom `IRouteHandler`, URL rewriting, and runtime
registration modules produce review-tier evidence or `AnalysisGap` rows.

## Config Extraction

Parse checked-in XML config with safe, bounded settings.

Candidate sections:

- `system.web/httpHandlers`
- `system.webServer/handlers`
- `system.web/httpModules`
- `system.webServer/modules`
- `system.web/pages`
- `system.web/pages/controls`
- `system.web/pages/namespaces`
- `system.web/urlMappings`
- `system.web/compilation`
- relevant location-scoped config blocks when the path value is safe or hashed

Safe properties:

- section kind;
- safe extension or wildcard descriptor;
- safe verb descriptor;
- handler/module type name when safe;
- page/control tag prefix and namespace when safe;
- mapped page descriptor or hash;
- config scope descriptor or hash;
- presence flags for transforms, includes, encrypted sections, and custom
  sections.

Raw values are never stored for credentials, connection strings, app settings,
machine keys, endpoint addresses, hostnames, physical paths, or environment
values. `location` path values are URL-path-like rather than filesystem-path-like
and use a stricter path-safety rule than directive `CodeFile`, `CodeBehind`, and
`MasterPageFile` values: only page-relative descriptors with no scheme, host,
query string, fragment, parent traversal, environment marker, expression, or
physical path marker may be rendered. Other `location` path values are hashed or
omitted. Matching hashes alone cannot prove client/server reachability.

## Handler And PageMethod Extraction

Handler evidence:

- `.ashx` directives with safe class/type metadata;
- `IHttpHandler` and `IHttpAsyncHandler` implementations;
- `IHttpHandlerFactory` or `IHttpAsyncHandlerFactory` only as review-tier
  factory evidence unless static target type is compiler-resolved;
- config-backed handler declarations from safe XML structures.

PageMethod and script evidence:

- static methods on WebForms page classes with `[WebMethod]`;
- `[ScriptMethod]` metadata attached to supported page or service methods;
- `[ScriptService]` classes as script-callable surface evidence;
- safe links to existing page, code-behind, ASMX, or service facts when
  identity is scoped and unambiguous.

Syntax fallback uses Roslyn syntax nodes only. Comments, strings, XML docs,
inactive preprocessor regions, and arbitrary text are not evidence.

## Navigation Extraction

Candidate deterministic navigation references:

- markup attributes such as static `NavigateUrl`, `PostBackUrl`, `Action`, and
  safe menu/tree item URLs;
- C# calls such as `Response.Redirect("...")`, `Server.Transfer("...")`, and
  direct assignments to known navigation properties with simple literal values;
- checked-in `.sitemap` nodes with safe paths and titles reduced to descriptors
  or hashes.

Navigation target values use a stricter version of directive path safety. Bare
repo-resolvable page filenames or route names may be rendered as safe
descriptors. Values with schemes, hosts, query strings, fragments, expressions,
string concatenation, environment markers, physical paths, config substitutions,
or script fragments are hashed or omitted; credential-like values are omitted.

Emit `AspNetNavigationReferenceDeclared` for a supported reference. Emit
`AspNetNavigationEdgeDeclared` only when the target links to a checked-in page,
route fact, config mapping, or handler with credible static evidence. PageMethod
and ScriptMethod facts are AJAX callback surfaces; they can connect through
script/service-call evidence when separately supported, but they are not direct
targets of standard page-to-page navigation references.

Do not emit concrete edges for:

- JavaScript-generated navigation;
- string concatenation with variables;
- data-bound controls;
- database-driven menus;
- resource/localization-driven URLs;
- role-trimmed sitemap behavior;
- master-page or nested-user-control runtime composition;
- custom URL rewriting;
- auth/role gating.

Those cases should be `AnalysisGap` evidence or review-tier facts, depending on
how much static structure is visible. `NeedsReview` is a report/path
classification cap, not a fact evidence tier; the underlying fact remains
`Tier3SyntaxOrTextual` or `Tier4Unknown`.

## Flow And Report Integration

Minimum implementation behavior:

- `tracemap scan` reports counts and limitations for ASP.NET route/navigation
  facts and gaps.
- `facts.ndjson` and `index.sqlite` preserve new fact types and source
  provenance.
- Combined import/export preserves facts and gaps without dropping rule IDs or
  evidence tiers.
- Legacy flow/path readers treat route/page/handler/page-method facts as
  possible roots or intermediate surfaces only when selector models can express
  them safely.
- Evidence graph/vault export includes nodes and edges with redacted identities
  and source fact links.

Preferred path behavior:

- A WebForms event path may include a navigation source or route/page surface
  before reaching a handler.
- A route/page/page-method root may traverse existing call/object/service-client
  evidence to WCF, ASMX, Remoting, HTTP, SQL/query, or legacy data terminals.
- A navigation edge may connect one static page surface to another, then to
  existing handler or PageMethod evidence.

Path classifications are capped by the weakest route/navigation leg. For
example, a `Tier3SyntaxOrTextual` navigation reference cannot produce a
`StrongStaticPath`, even if the downstream handler has semantic call edges.

## Output Safety

Allowed display values:

- repo-relative paths;
- line spans;
- rule IDs and evidence tiers;
- extractor versions and commit SHA;
- safe identifiers such as type names, method names, page filenames, extension
  descriptors, and route labels when they pass existing safety rules;
- context-separated hash prefixes for unsafe non-secret values.

Hashing should reuse existing TraceMap redaction/hash helpers, including the
scanner-side `TraceMap.Core.FactFactory.Hash` helper and reporting-side
`TraceMap.Reporting.CombinedReportHelpers.Hash` helper. Context separation must
prefix the hashed value with at least the rule or extractor family plus the
property role, so the same unsafe value in a route pattern, config value, and
navigation target does not become an implicit cross-context join key. Hash-only
cross-context joins are prohibited unless a future rule explicitly allows that
join with additional non-hash supporting evidence. If these helpers cannot
support the needed context separation cleanly, adding that shared helper support
is a prerequisite for Task 1 and must be recorded in `implementation-state.md`
before any route/navigation facts are emitted.

Canonical ASP.NET route/navigation value hashes should use a 32-character
lowercase hex prefix. The input shape is
`legacy.aspnet.<rule-family>:<propertyRole>:<normalizedValue>`, where
`rule-family` is one of `surface`, `route`, `config`, `handler`,
`page-method`, or `navigation`, and `propertyRole` is a stable closed token such
as `route-pattern`, `config-location-path`, or `navigation-target`. Report and
export code should carry scanner-emitted hashes forward; when it must recompute
an equivalent display hash, it must use the same context prefix and length.

Omit credential-like values rather than hashing them. Unsafe route patterns,
URLs, hostnames, config values, physical paths, source snippets, local absolute
paths, raw remotes, and private sample names must not appear in facts, reports,
logs, review summaries, SQLite display fields, or export artifacts.

## MVP Slices

1. Surface inventory compatibility and rule catalog scaffolding for ASP.NET
   route/navigation evidence.
2. `Global.asax` route registration extraction with static `MapPageRoute`
   shapes and dynamic-route gaps.
3. Config handler/module/pages/urlMappings extraction with strict redaction.
4. `.ashx`, `IHttpHandler`, PageMethod, ScriptMethod, and ScriptService
   extraction with semantic and syntax fallback.
5. Static navigation reference and edge extraction for checked-in pages/routes.
6. Combined report, paths/reverse, evidence graph/vault export, and legacy smoke
   catalog integration.

## Deferred Follow-Ups

- Full ASP.NET route table simulation.
- Runtime URL rewriting, auth/role, or sitemap role-trimming modeling.
- Browser crawling, HTTP probing, or JavaScript execution.
- Data-bound, database-driven, or localization-resource navigation resolution.
- Deep master-page/control-tree composition.
- Classic ASP.NET MVC convention-based routing such as `routes.MapRoute(...)`
  in `RouteConfig.cs` or `Global.asax.cs`; this MVP focuses on WebForms-era
  `MapPageRoute` and handler/page/navigation evidence.
- Cross-application route alignment beyond existing combined evidence models.
- ASP.NET-specific flow facts such as `AspNetSurfaceFlowProjected` and
  `legacy.aspnet.flow.v1` unless existing `legacy.flow.*` rules prove
  insufficient during an implementation slice.
- Public site or demo updates.
