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

- [ ] 1. Add rule catalog entries, schema constants, and compatibility notes in
  the first implementation slice that emits route/navigation facts or report
  rows. Requirements: 1, 2, 3, 4, 5, 6, 7.
  - [ ] Add or reuse fact types for ASP.NET surface, route, config, handler,
    PageMethod, navigation reference, and navigation edge evidence. Add deferred
    flow evidence only if `implementation-state.md` records that existing
    `legacy.flow.*` rules are insufficient.
  - [ ] Add `legacy.aspnet.surface.v1`, `legacy.aspnet.route.v1`,
    `legacy.aspnet.config.v1`, `legacy.aspnet.handler.v1`,
    `legacy.aspnet.page-method.v1`, `legacy.aspnet.navigation.v1`, and any
    required flow/report rule in `rules/rule-catalog.yml` before emitting rows.
  - [ ] Document limitations for static extraction, dynamic route construction,
    master pages, user controls, control trees, reflection, auth/role gating,
    runtime config transforms, JavaScript-generated navigation, deployment, and
    reachability.
  - [ ] Preserve commit SHA, extractor version, repo-relative file path, line
    span, rule ID, evidence tier, coverage label, and supporting fact IDs.

- [ ] 2. Implement classic ASP.NET surface inventory. Requirements: 1, 7, 8.
  Prerequisite: Task 1.
  - [ ] Inventory `.aspx`, `.ascx`, `.master`, `.ashx`, `Global.asax`, linked
    code-behind files, and designer files with safe repository-relative paths.
  - [ ] Parse safe directive attributes for page/control/master/handler
    metadata without storing raw snippets or unsafe values.
  - [ ] Reuse existing `WebForms*` facts where they already express the evidence
    and avoid duplicate page inventory.
  - [ ] Emit `AnalysisGap` facts for malformed, unreadable, generated, or
    unsupported directives and files.
  - [ ] Add tests for safe inventory, malformed directives, stale/missing
    code-behind, and redaction.
  - [ ] Add tests proving designer files support identity/linkage only, do not
    become standalone surfaces, and emit gaps when no matching page/control
    evidence exists.
  - [ ] Add tests proving pages already represented by `WebFormsPageDeclared`
    do not also emit duplicate `AspNetSurfaceDeclared` rows for the same
    identity.

- [ ] 3. Implement `Global.asax` and route registration extraction.
  Requirements: 2, 7, 8.
  Prerequisite: Task 1.
  - [ ] Detect supported `MapPageRoute` and static route-registration shapes in
    `Global.asax` and linked code-behind.
  - [ ] Use semantic symbol resolution where available and syntax fallback where
    build coverage is reduced.
  - [ ] Emit safe route descriptors, route hashes, mapped page links, supporting
    fact IDs, and dynamic-route gaps.
  - [ ] Reject runtime route simulation, custom route-handler inference, URL
    rewriting assumptions, and arbitrary concrete endpoint generation.
  - [ ] Add fixtures for strong static routes, syntax-only routes, dynamic route
    construction, ambiguous targets, and unsafe route values.

- [ ] 4. Implement checked-in ASP.NET config extraction. Requirements: 3, 7, 8.
  Prerequisite: Task 1.
  - [ ] Parse safe structures from `system.web/httpHandlers`,
    `system.webServer/handlers`, modules, pages, controls, namespaces,
    `urlMappings`, and relevant location-scoped config.
  - [ ] Emit config surface facts with safe handler/module/page descriptors and
    supporting fact IDs.
  - [ ] Omit credentials, connection strings, app settings, machine keys, raw
    URLs, hostnames, local paths, endpoint values, and raw config values.
  - [ ] Emit gaps for transforms, external includes, encrypted sections,
    machine.config dependencies, runtime mutations, and unsupported custom
    sections.
  - [ ] Add tests proving supported config sections emit config facts with the
    expected safe section kind, extension descriptor, verb descriptor, type
    identity, scope descriptor, rule ID, and evidence tier.
  - [ ] Add tests proving unsafe `location` path values are hashed or omitted
    rather than rendered raw.
  - [ ] Add tests proving a superficially page-relative `location` path with a
    query string, fragment, scheme, host, parent traversal, or physical path
    marker is hashed or omitted rather than rendered as a safe descriptor.
  - [ ] Add tests proving hash equality alone cannot create reachability or
    navigation edges.

- [ ] 5. Implement `.ashx`, handler interface, handler factory, and PageMethod
  extraction. Requirements: 4, 7, 8.
  Prerequisite: Task 1.
  - [ ] Detect `.ashx` directives and link them to safe handler class/type
    evidence when scoped and unambiguous.
  - [ ] Detect `IHttpHandler`, `IHttpAsyncHandler`, and handler factory shapes
    with semantic resolution and conservative fallback tiers.
  - [ ] Detect `[WebMethod]`, `[ScriptMethod]`, `[ScriptService]`, and static
    page method evidence without conflating it with ASMX host facts.
  - [ ] Emit ambiguity gaps for lookalike attributes, custom handlers,
    factories with unresolved target types, and unsupported dynamic dispatch.
  - [ ] Add tests for semantic, syntax-only, ambiguous, inactive-code,
    comment/string negative, and redaction cases.

- [ ] 6. Implement static navigation reference extraction. Requirements: 5, 7,
  8.
  Prerequisite: Task 1.
  - [ ] Detect deterministic static navigation references in markup, sitemap
    XML, and C# navigation APIs such as `Response.Redirect` and
    `Server.Transfer`.
  - [ ] Emit navigation reference facts with source surface, target descriptor
    or hash, reference kind, line span, rule ID, evidence tier, and limitations.
  - [ ] Emit target edges only when checked-in page, route, config, or handler
    evidence supports the target; PageMethod and ScriptMethod facts require
    separate script/service-call evidence and are not direct standard navigation
    targets.
  - [ ] Emit gaps or facts at `Tier3SyntaxOrTextual` or `Tier4Unknown` for
    JavaScript-generated, data-bound, resource-driven, concatenated,
    database-backed, role-trimmed, or runtime master/control-tree navigation.
  - [ ] Add deterministic ordering and duplicate-link tests.
  - [ ] Add tests proving a navigation reference with no checked-in target
    evidence emits no `AspNetNavigationEdgeDeclared`.
  - [ ] Add tests proving route/config/navigation hash equality alone cannot
    synthesize a target edge.
  - [ ] Add tests proving no `AspNetNavigationEdgeDeclared` is synthesized when
    a navigation target hash equals a hash stored for another fact type, such as
    a config scope hash.

- [ ] 7. Integrate reports, combined indexes, paths, reverse, release review,
  and evidence graph/vault export. Requirements: 6, 7, 8.
  Prerequisite: Task 1.
  - [ ] Add scan report counts and limitations for ASP.NET route/navigation
    facts and gaps.
  - [ ] Ensure combined indexes preserve new facts, supporting IDs, tiers,
    coverage labels, and gaps.
  - [ ] Add route/page/handler/page-method roots or intermediate surfaces to
    path/reverse models only where they can be represented safely.
  - [ ] Decide and document whether route/page/navigation roots require new
    selector/surface kinds or can reuse existing selector types; if new kinds
    are added, document schema/version and older-index availability behavior.
  - [ ] Decide and document index schema version and combined-reader
    compatibility behavior for new `AspNet*` fact types before they first ship.
  - [ ] Export route/navigation nodes and edges through evidence graph/vault
    output with safe identities and source fact links.
  - [ ] Emit availability gaps for older indexes, disabled extractors, missing
    schema, or consumers that cannot yet use the new precision.

- [ ] 8. Add focused validation fixtures and tests. Requirements: 1, 2, 3, 4,
  5, 6, 7, 8.
  - [ ] Cover `.aspx`, `.ascx`, `.master`, `.ashx`, `Global.asax`, code-behind
    partial classes, designer files, and `web.config` structures.
  - [ ] Cover route registration, config handlers/modules/pages, PageMethods,
    ScriptMethods, static navigation, ambiguous navigation, malformed files, and
    reduced build coverage.
  - [ ] Prove WCF/SVC, ASMX/SOAP, Remoting, WebForms event flow, and ASP.NET
    route/navigation facts remain distinct except for rule-backed edges.
  - [ ] Prove `Global.asax` `MapPageRoute` evidence emits ASP.NET route facts
    and is not reclassified as or duplicated into `HttpRouteBinding` /
    `csharp.syntax.aspnetroute.v1` controller-route evidence.
  - [ ] Prove dynamic routes, JavaScript-generated links, master/control-tree
    runtime behavior, auth/role gating, reflection, runtime config transforms,
    and missing generated files cap confidence or emit gaps.
  - [ ] Prove a `Tier3SyntaxOrTextual` navigation leg cannot yield a
    `StrongStaticPath` even when downstream handler or service evidence is
    `Tier1Semantic`.
  - [ ] Prove `[WebMethod]` on a WebForms page emits page-method evidence and
    is not reclassified as ASMX operation evidence without independent ASMX host
    support.
  - [ ] Prove no raw local paths, remotes, snippets, config values, URLs,
    hostnames, credentials, endpoint values, secrets, or private labels appear
    in generated artifacts.
  - [ ] Prove location-scoped config path values with query strings, fragments,
    schemes, hosts, parent traversal, or physical path markers are hashed or
    omitted.
  - [ ] Prove no `AspNetNavigationEdgeDeclared` is synthesized from source and
    target hashes that collide or match across different fact types without
    additional non-hash supporting evidence.
  - [ ] Prove the canonical ASP.NET route/navigation hash format uses the
    agreed context prefix and 32-character lowercase hex length in scanner facts
    and report/export display fields.
  - [ ] Prove the same raw unsafe value in route pattern, config value, and
    navigation target roles produces distinct stored hashes.
  - [ ] Prove deterministic ordering and byte-stable output for fixed inputs.

- [ ] 9. Update operator documentation and legacy smoke catalog guidance when
  implementation lands. Requirements: 6, 7, 8.
  - [ ] Document fact families, rule IDs, evidence tiers, limitations, and
    non-goals.
  - [ ] Add or update `docs/VALIDATION.md` with pinned route/navigation smoke
    guidance if a smoke command is introduced.
  - [ ] Extend the legacy sample smoke catalog only with neutral labels,
    expected rule IDs, tiers, states, sanitized command templates, and reviewed
    public/fixture identity metadata.
  - [ ] Keep local sample paths and private source identities out of committed
    artifacts.

- [ ] 10. Validate each implementation slice. Requirements: 8.
  - [ ] Update this spec's `implementation-state.md` with branch, scope
    decisions, validation, oddities, and follow-up items.
  - [ ] Run focused route/navigation tests.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run or explicitly defer relevant pinned smoke checks from
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
