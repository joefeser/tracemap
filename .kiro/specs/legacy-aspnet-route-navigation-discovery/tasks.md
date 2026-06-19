# Legacy ASP.NET Route And Navigation Discovery Tasks

## Spec Authoring Tasks

- [x] 0. Author the Kiro spec artifacts for the next classic ASP.NET
  route/page/navigation discovery slice. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [x] Create `requirements.md`, `design.md`, `tasks.md`, and
    `implementation-state.md`.
  - [x] Align scope with existing WebForms event flow, ASMX/SOAP, WCF,
    Remoting, combined paths, evidence graph/vault export, and legacy smoke
    catalog work.
  - [x] Keep this branch spec-only and avoid product-code, rule-catalog, docs,
    sample, or site changes.

## Implementation Tasks

Task 1 is a prerequisite for Tasks 2 through 7 whenever those tasks emit facts,
gaps, report rows, path rows, or export nodes. Do not emit route/navigation
evidence before the needed rule IDs, schema constants, hash/redaction decisions,
and compatibility notes exist.

- [x] 1. Add rule catalog entries, schema constants, and compatibility notes in
  the first implementation slice that emits route/navigation facts or report
  rows. Requirements: 1, 2, 3, 4, 5, 6, 7.
  - [x] Add or reuse fact types for ASP.NET surface, route, config, handler,
    PageMethod, navigation reference, and navigation edge evidence. Add deferred
    flow evidence only if `implementation-state.md` records that existing
    `legacy.flow.*` rules are insufficient.
  - [x] Add `legacy.aspnet.surface.v1`, `legacy.aspnet.route.v1`,
    `legacy.aspnet.config.v1`, `legacy.aspnet.handler.v1`,
    `legacy.aspnet.page-method.v1`, `legacy.aspnet.navigation.v1`, and any
    required flow/report rule in `rules/rule-catalog.yml` before emitting rows.
  - [x] Document limitations for static extraction, dynamic route construction,
    master pages, user controls, control trees, reflection, auth/role gating,
    runtime config transforms, JavaScript-generated navigation, deployment, and
    reachability.
  - [x] Preserve commit SHA, extractor version, repo-relative file path, line
    span, rule ID, evidence tier, coverage label, and supporting fact IDs.

- [x] 2. Implement classic ASP.NET surface inventory. Requirements: 1, 7, 8.
  Prerequisite: Task 1.
  - [x] Inventory `.aspx`, `.ascx`, `.master`, `.ashx`, `Global.asax`, linked
    code-behind files, and designer files with safe repository-relative paths.
  - [x] Parse safe directive attributes for page/control/master/handler
    metadata without storing raw snippets or unsafe values.
  - [x] Reuse existing `WebForms*` facts where they already express the evidence
    and avoid duplicate page inventory.
  - [x] Emit `AnalysisGap` facts for malformed, unreadable, generated, or
    unsupported directives and files.
  - [x] Add tests for safe inventory, malformed directives, stale/missing
    code-behind, and redaction.
  - [x] Add tests proving designer files support identity/linkage only, do not
    become standalone surfaces, and emit gaps when no matching page/control
    evidence exists.
  - [x] Add tests proving pages already represented by `WebFormsPageDeclared`
    do not also emit duplicate `AspNetSurfaceDeclared` rows for the same
    identity.

- [x] 3. Implement `Global.asax` and route registration extraction.
  Requirements: 2, 7, 8.
  Prerequisite: Task 1.
  - [x] Detect supported `MapPageRoute` and static route-registration shapes in
    `Global.asax` and linked code-behind.
  - [x] Use semantic symbol resolution where available and syntax fallback where
    build coverage is reduced.
  - [x] Emit safe route descriptors, route hashes, mapped page links, supporting
    fact IDs, and dynamic-route gaps.
  - [x] Reject runtime route simulation, custom route-handler inference, URL
    rewriting assumptions, and arbitrary concrete endpoint generation.
  - [x] Add fixtures for strong static routes, syntax-only routes, dynamic route
    construction, ambiguous targets, and unsafe route values.

- [x] 4. Implement checked-in ASP.NET config extraction. Requirements: 3, 7, 8.
  Prerequisite: Task 1.
  - [x] Parse safe structures from `system.web/httpHandlers`,
    `system.webServer/handlers`, modules, pages, controls, namespaces,
    `urlMappings`, and relevant location-scoped config.
  - [x] Emit config surface facts with safe handler/module/page descriptors and
    supporting fact IDs.
  - [x] Omit credentials, connection strings, app settings, machine keys, raw
    URLs, hostnames, local paths, endpoint values, and raw config values.
  - [x] Emit gaps for transforms, external includes, encrypted sections,
    machine.config dependencies, runtime mutations, and unsupported custom
    sections.
  - [x] Add tests proving supported config sections emit config facts with the
    expected safe section kind, extension descriptor, verb descriptor, type
    identity, scope descriptor, rule ID, and evidence tier.
  - [x] Add tests proving unsafe `location` path values are hashed or omitted
    rather than rendered raw.
  - [x] Add tests proving a superficially page-relative `location` path with a
    query string, fragment, scheme, host, parent traversal, or physical path
    marker is hashed or omitted rather than rendered as a safe descriptor.
  - [x] Add tests proving hash equality alone cannot create reachability or
    navigation edges.

- [x] 5. Implement `.ashx`, handler interface, handler factory, and PageMethod
  extraction. Requirements: 4, 7, 8.
  Prerequisite: Task 1.
  - [x] Detect `.ashx` directives and link them to safe handler class/type
    evidence when scoped and unambiguous.
  - [x] Detect `IHttpHandler`, `IHttpAsyncHandler`, and handler factory shapes
    with semantic resolution and conservative fallback tiers.
  - [x] Detect `[WebMethod]`, `[ScriptMethod]`, `[ScriptService]`, and static
    page method evidence without conflating it with ASMX host facts.
  - [x] Emit ambiguity gaps for lookalike attributes, custom handlers,
    factories with unresolved target types, and unsupported dynamic dispatch.
  - [x] Add tests for semantic, syntax-only, ambiguous, inactive-code,
    comment/string negative, and redaction cases.

- [x] 6. Implement static navigation reference extraction. Requirements: 5, 7,
  8.
  Prerequisite: Task 1.
  - [x] Detect deterministic static navigation references in markup, sitemap
    XML, and C# navigation APIs such as `Response.Redirect` and
    `Server.Transfer`.
  - [x] Emit navigation reference facts with source surface, target descriptor
    or hash, reference kind, line span, rule ID, evidence tier, and limitations.
  - [x] Emit target edges only when checked-in page, route, config, or handler
    evidence supports the target; PageMethod and ScriptMethod facts require
    separate script/service-call evidence and are not direct standard navigation
    targets.
  - [x] Emit gaps or facts at `Tier3SyntaxOrTextual` or `Tier4Unknown` for
    JavaScript-generated, data-bound, resource-driven, concatenated,
    database-backed, role-trimmed, or runtime master/control-tree navigation.
  - [x] Add deterministic ordering and duplicate-link tests.
  - [x] Add tests proving a navigation reference with no checked-in target
    evidence emits no `AspNetNavigationEdgeDeclared`.
  - [x] Add tests proving route/config/navigation hash equality alone cannot
    synthesize a target edge.
  - [x] Add tests proving no `AspNetNavigationEdgeDeclared` is synthesized when
    a navigation target hash equals a hash stored for another fact type, such as
    a config scope hash.

- [x] 7. Integrate reports, combined indexes, paths, reverse, release review,
  and evidence graph/vault export. Requirements: 6, 7, 8.
  Prerequisite: Task 1.
  - [x] Add scan report counts and limitations for ASP.NET route/navigation
    facts and gaps.
  - [x] Ensure combined indexes preserve new facts, supporting IDs, tiers,
    coverage labels, and gaps.
  - [x] Add route/page/handler/page-method roots or intermediate surfaces to
    path/reverse models only where they can be represented safely.
  - [x] Decide and document whether route/page/navigation roots require new
    selector/surface kinds or can reuse existing selector types; if new kinds
    are added, document schema/version and older-index availability behavior.
  - [x] Decide and document index schema version and combined-reader
    compatibility behavior for new `AspNet*` fact types before they first ship.
  - [x] Export route/navigation nodes and edges through evidence graph/vault
    output with safe identities and source fact links.
  - [x] Emit availability gaps for older indexes, disabled extractors, missing
    schema, or consumers that cannot yet use the new precision.

- [x] 8. Add focused validation fixtures and tests. Requirements: 1, 2, 3, 4,
  5, 6, 7, 8.
  - [x] Cover `.aspx`, `.ascx`, `.master`, `.ashx`, `Global.asax`, code-behind
    partial classes, designer files, and `web.config` structures.
  - [x] Cover route registration, config handlers/modules/pages, PageMethods,
    ScriptMethods, static navigation, ambiguous navigation, malformed files, and
    reduced build coverage.
  - [x] Prove WCF/SVC, ASMX/SOAP, Remoting, WebForms event flow, and ASP.NET
    route/navigation facts remain distinct except for rule-backed edges.
  - [x] Prove `Global.asax` `MapPageRoute` evidence emits ASP.NET route facts
    and is not reclassified as or duplicated into `HttpRouteBinding` /
    `csharp.syntax.aspnetroute.v1` controller-route evidence.
  - [x] Prove dynamic routes, JavaScript-generated links, master/control-tree
    runtime behavior, auth/role gating, reflection, runtime config transforms,
    and missing generated files cap confidence or emit gaps.
  - [x] Prove a `Tier3SyntaxOrTextual` navigation leg cannot yield a
    `StrongStaticPath` even when downstream handler or service evidence is
    `Tier1Semantic`.
  - [x] Prove `[WebMethod]` on a WebForms page emits page-method evidence and
    is not reclassified as ASMX operation evidence without independent ASMX host
    support.
  - [x] Prove no raw local paths, remotes, snippets, config values, URLs,
    hostnames, credentials, endpoint values, secrets, or private labels appear
    in generated artifacts.
  - [x] Prove location-scoped config path values with query strings, fragments,
    schemes, hosts, parent traversal, or physical path markers are hashed or
    omitted.
  - [x] Prove no `AspNetNavigationEdgeDeclared` is synthesized from source and
    target hashes that collide or match across different fact types without
    additional non-hash supporting evidence.
  - [x] Prove the canonical ASP.NET route/navigation hash format uses the
    agreed context prefix and 32-character lowercase hex length in scanner facts
    and report/export display fields.
  - [x] Prove the same raw unsafe value in route pattern, config value, and
    navigation target roles produces distinct stored hashes.
  - [x] Prove deterministic ordering and byte-stable output for fixed inputs.

- [x] 9. Update operator documentation and legacy smoke catalog guidance when
  implementation lands. Requirements: 6, 7, 8.
  - [x] Document fact families, rule IDs, evidence tiers, limitations, and
    non-goals.
  - [x] Add or update `docs/VALIDATION.md` with pinned route/navigation smoke
    guidance if a smoke command is introduced.
  - [x] Extend the legacy sample smoke catalog only with neutral labels,
    expected rule IDs, tiers, states, sanitized command templates, and reviewed
    public/fixture identity metadata.
  - [x] Keep local sample paths and private source identities out of committed
    artifacts.

- [x] 10. Validate each implementation slice. Requirements: 8.
  - [x] Update this spec's `implementation-state.md` with branch, scope
    decisions, validation, oddities, and follow-up items.
  - [x] Run focused route/navigation tests.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Run or explicitly defer relevant pinned smoke checks from
    `docs/VALIDATION.md`.

## Recommended PR Slices

- Slice 1: surface inventory, rule catalog entries, and route registration
  extraction.
- Slice 2: checked-in config, `.ashx`, handler interface, and PageMethod
  extraction.
- Slice 3: static navigation references and conservative target edges.
- Slice 4: combined reports, paths/reverse/release-review, evidence graph/vault
  export, validation docs, and legacy smoke catalog integration.

## Deferred Follow-Ups

- Full ASP.NET runtime route simulation.
- Browser crawling, JavaScript execution, IIS hosting, or live HTTP calls.
- Runtime URL rewriting, auth/role, sitemap role trimming, and config-transform
  evaluation.
- Database-backed, resource/localization-driven, or data-bound navigation
  resolution.
- Deep master-page and nested-user-control control-tree modeling.
- Public site/demo updates.
