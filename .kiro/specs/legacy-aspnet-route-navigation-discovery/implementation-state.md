# Legacy ASP.NET Route And Navigation Discovery Implementation State

Status: implemented

## Spec Metadata

- Spec branch: `codex/spec-legacy-aspnet-route-navigation-discovery`
- Spec path: `.kiro/specs/legacy-aspnet-route-navigation-discovery/`
- Current scope: spec-only branch for requirements, design, implementation
  tasks, and handoff notes.
- Implementation branch: `codex/implement-legacy-aspnet-route-navigation-discovery`
- Product code status: implemented in this branch.

## Scope Decisions

- This spec covers deterministic static extraction for classic ASP.NET
  route/page/handler/PageMethod/navigation evidence.
- Existing WebForms event-flow facts remain authoritative for markup event
  bindings and handler resolution; this spec adds route, config, handler,
  PageMethod, and navigation evidence around those facts.
- ASMX/SOAP, WCF/SVC, and Remoting fact families remain distinct. Integration is
  through explicit supporting fact IDs and static path/report consumers only.
- The MVP defers full ASP.NET route table simulation, runtime URL rewriting,
  auth/role behavior, sitemap role trimming, browser crawling, IIS hosting, app
  execution, JavaScript execution, and live HTTP calls.
- Safety rules are part of the spec: no raw local absolute paths, raw remotes,
  source snippets, config values, endpoint URLs, hostnames, credentials,
  secrets, or private sample identifiers in committed artifacts.
- Rule catalog edits are intentionally deferred to implementation slices that
  first emit the corresponding facts or report rows.
- Rule catalog entries now exist for `legacy.aspnet.surface.v1`,
  `legacy.aspnet.route.v1`, `legacy.aspnet.config.v1`,
  `legacy.aspnet.handler.v1`, `legacy.aspnet.page-method.v1`, and
  `legacy.aspnet.navigation.v1`.
- Scanner fact constants now include `AspNetSurfaceDeclared`,
  `AspNetRouteDeclared`, `AspNetConfigSurfaceDeclared`,
  `AspNetHandlerDeclared`, `AspNetPageMethodDeclared`,
  `AspNetNavigationReferenceDeclared`, and
  `AspNetNavigationEdgeDeclared`.
- File inventory now recognizes `.ashx`, `Global.asax`/`.asax`, and
  `.sitemap` as ASP.NET legacy inputs.
- Route, config, and navigation unsafe values use the canonical
  `legacy.aspnet.<family>|<propertyRole>|<normalizedValue>` hash input and a
  32-character lowercase hex stored value. Credential-like values are omitted.

## Flow Rule Decision

- Current spec default: reuse existing `legacy.flow.*` rules for route/page/
  navigation path and report integration where their models can represent the
  evidence without semantic overload.
- Deferred option: add `legacy.aspnet.flow.v1` and an ASP.NET-specific flow fact
  only if an implementation slice determines existing flow rules cannot express
  the needed route/page/navigation semantics safely. That decision must be
  recorded here before the implementation emits any row that cites the new rule.
- Implementation decision: no `legacy.aspnet.flow.v1` rule or
  `AspNetSurfaceFlowProjected` fact was added. New `AspNet*` facts are
  preserved in `facts.ndjson`, `index.sqlite`, generic combined facts, report
  counts, and docs/vault-style generic fact export surfaces. Existing path and
  reverse selectors were not given new ASP.NET-specific roots because their
  current surface vocabulary cannot represent route/page/navigation roots
  without implying runtime reachability. The scanner instead emits static
  navigation edges only when non-hash checked-in page/route/config/handler
  evidence supports the link.
- Route tier decision: this slice emits supported route registrations as
  conservative `Tier3SyntaxOrTextual` syntax evidence. `Tier1Semantic`
  compiler-resolved route evidence and `Tier2Structural` route-collection
  scoping are deferred until legacy extractors can consume live semantic model
  route-call resolution without reshaping the scanner pipeline.
- Combined/index compatibility decision: no SQLite schema version bump was
  required because `AspNet*` facts use the existing generic `facts` table and
  JSON property payload. Older indexes that predate `legacy-aspnet/0.1.0` will
  naturally have zero `AspNet*` rows; commands that need ASP.NET-specific roots
  should treat that as unavailable evidence rather than absence until a future
  selector/report slice adds explicit availability gaps.

## Review And Validation Notes

- Initial spec authoring should run Kiro CLI reviews for Opus and Sonnet when
  locally available, then patch Medium+ actionable findings with at most two
  re-review cycles.
- If Kiro CLI review is unavailable, record the exact blocker here and proceed
  with self-review so the spec branch remains inspectable.
- Spec-only validation should include:
  - `git diff --check`
  - `./scripts/check-private-paths.sh`
  - any repository spec validation script if present
- Product implementation validation is deferred until implementation slices and
  should include focused tests, `dotnet build`, `dotnet test`, private-path
  guard, `git diff --check`, and relevant smoke checks from `docs/VALIDATION.md`.

## Current Validation

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- No dedicated spec validation script was present under the repository's script
  paths.
- `node scripts/kiro-review.mjs --self-test` passed.
- Product build/test/smoke validation is intentionally deferred because this
  branch is spec-only and does not implement scanner or reducer product code.
- Focused ASP.NET route/navigation tests passed:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests`.
- Product build/test/private-path/diff validation passed:
  - `dotnet build src/dotnet/TraceMap.sln`
  - `dotnet test src/dotnet/TraceMap.sln` (473 tests)
  - `./scripts/check-private-paths.sh`
  - `git diff --check`
- Local-only CLI smoke passed against a synthetic temporary classic ASP.NET
  fixture. It produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`, and emitted `AspNetRouteDeclared`,
  `AspNetConfigSurfaceDeclared`, `AspNetHandlerDeclared`,
  `AspNetPageMethodDeclared`, and `AspNetNavigationReferenceDeclared` facts.
- Pinned public route/navigation smoke is explicitly deferred because no
  reviewed public route/navigation smoke baseline exists yet; `docs/VALIDATION.md`
  now documents the focused synthetic test command and local-only smoke rules.
- PR-loop follow-up validation passed after patching review findings for
  malformed `.ashx` directives, commented markup navigation, and local C#
  variables named like navigation properties:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests` (12 tests)
  - `dotnet build src/dotnet/TraceMap.sln`
  - `dotnet test src/dotnet/TraceMap.sln` (476 tests)
  - `./scripts/check-private-paths.sh`
  - `git diff --check`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-legacy-aspnet-smoke`
- PR-loop Qodo follow-up validation passed after patching XML I/O failures in
  config/sitemap extraction to emit deterministic `AnalysisGap` facts:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests` (13 tests)
  - `dotnet build src/dotnet/TraceMap.sln`
  - `dotnet test src/dotnet/TraceMap.sln` (477 tests)
  - `./scripts/check-private-paths.sh`
  - `git diff --check`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-legacy-aspnet-smoke`
- Fresh Codex review follow-up validation passed after patching code-behind
  relative navigation target resolution and `urlMappings` descriptor matching
  for navigation edges:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests` (17 tests)
  - `dotnet build src/dotnet/TraceMap.sln`
  - `dotnet test src/dotnet/TraceMap.sln` (481 tests)
  - `./scripts/check-private-paths.sh`
  - `git diff --check`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-legacy-aspnet-smoke`
- Second fresh Codex review follow-up validation passed after scoping config
  candidates to ASP.NET sections and suppressing duplicate PageMethod facts for
  ASMX-owned service methods:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests` (19 tests)
  - `dotnet build src/dotnet/TraceMap.sln`
  - `dotnet test src/dotnet/TraceMap.sln` (483 tests)
  - `./scripts/check-private-paths.sh`
  - `git diff --check`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-legacy-aspnet-smoke`

## Kiro Review State

- Implementation review completed on 2026-06-18:
  - Sonnet `claude-sonnet-4.6`, session
    `bf073695-736c-41e4-b56b-9489458f59d5`, reduced coverage because the
    wrapper reported denied MCP/tool access.
  - Required findings patched:
    - leading-slash `CodeBehind`/navigation paths are now checked before
      normalization so absolute paths are hashed instead of rendered as safe
      descriptors;
    - navigation edge tests now assert weakest-tier capping at
      `Tier3SyntaxOrTextual`;
    - PageMethod tests now assert `[WebMethod]` on a WebForms page is not
      reclassified as `AsmxOperationDeclared` without ASMX host evidence.
  - Non-blocking notes recorded: route semantic tiers are deferred, and generic
    combined/index compatibility is documented above.
- Re-review cycle 1 completed on 2026-06-18:
  - Sonnet `claude-sonnet-4.6`, session
    `bf9230f9-0bcb-45f6-b949-66f6bb7a0bd1`, full coverage.
  - Required findings patched:
    - `location path` values now use stricter config-location safety, hashing
      route-template-shaped values such as `Reports/{year}`;
    - sitemap nodes with no `url` attribute now emit
      `SiteMapNodeMissingUrl` gaps;
    - tests now prove navigation/config hash-only evidence does not synthesize
      navigation edges.
- Re-review cycle 2 completed on 2026-06-18:
  - Sonnet `claude-sonnet-4.6`, session
    `ae7718ab-ed3c-4d3f-b3b3-b015e433b392`, reduced coverage because the
    wrapper reported denied MCP/tool access.
  - Required findings patched after the final allowed Kiro cycle:
    - removed unused `IsAspNetEvidenceFact`;
    - added a test documenting the chosen secret-like navigation behavior:
      emit a navigation-reference fact with `targetOmitted=secret-like`, no
      target path/hash/symbol, no raw secret, and no edge;
    - added class-level `[ScriptService]` evidence for classes without
      per-method PageMethod attributes.
  - No third Kiro review cycle was run because the spec caps implementation
    re-review at two cycles. Post-final-review patches were validated locally
    with focused tests, full solution build/test, private-path guard, diff
    check, and local-only CLI smoke.
- Initial Kiro spec reviews completed with full coverage on 2026-06-17:
  - Opus: `claude-opus-4.8`, session
    `e929e8d6-8b0a-4b78-9326-4fa9adeb77be`
  - Sonnet: `claude-sonnet-4.6`, session
    `0eb6eeed-9272-4650-8755-487102b9118f`
- Medium+ findings patched in this branch:
  - clarified designer files as supporting identity/linkage evidence only;
  - removed duplicate config section wording;
  - closed the navigation-reference zero-output escape hatch;
  - documented route evidence tiering for semantic, structural, syntax, and gap
    cases;
  - anchored hashing to existing TraceMap redaction/hash helper conventions;
  - tightened `location` path and navigation target safety rules;
  - clarified `NeedsReview` as a report/path classification cap, not an
    evidence tier;
  - documented the default flow-rule reuse decision and optional
    `AspNetSurfaceFlowProjected` purpose;
  - added missing positive and negative test expectations for config,
    navigation, selector decisions, non-duplication, weakest-leg capping, and
    ASMX/PageMethod boundaries.
- Re-review cycle 1 completed on 2026-06-17:
  - Sonnet `claude-sonnet-4.6`: full coverage, no blocking findings after the
    first patch.
  - Opus `claude-opus-4.8`: reduced coverage due to denied tool access
    (`ToolDenied`, `kiro.review.wrapper.v1`), but returned actionable text
    findings.
- Re-review cycle 1 findings patched in this branch:
  - moved `legacy.aspnet.flow.v1` and `AspNetSurfaceFlowProjected` to deferred
    unless existing `legacy.flow.*` rules prove insufficient;
  - added `Flow Rule Decision` state guidance;
  - named `TraceMap.Core.FactFactory.Hash` and
    `TraceMap.Reporting.CombinedReportHelpers.Hash` as the existing hash helpers
    to reuse;
  - tightened `location` path semantics;
  - specified designer orphan gaps under `legacy.aspnet.surface.v1`;
  - added prerequisites tying Tasks 2 through 7 to Task 1;
  - documented the boundary with existing `HttpRouteBinding` /
    `csharp.syntax.aspnetroute.v1` evidence and added related test obligations.
- Re-review cycle 2 completed on 2026-06-17:
  - Opus `claude-opus-4.8`: full coverage, no blockers.
  - Sonnet `claude-sonnet-4.6`: full coverage, no blockers.
- Post-cycle-2 self-review patch, without another Kiro cycle because the
  two-cycle cap was reached:
  - pinned the canonical ASP.NET route/navigation hash context prefix shape and
    32-character lowercase hex length;
  - added a task requiring schema/combined-reader compatibility decisions for
    new `AspNet*` fact types before those facts first ship;
  - added test obligations for superficially safe `location` paths with query
    or fragment components, cross-fact-type navigation hash matches, canonical
    hash format, and role-separated hashes.

## Follow-Up Items

- Add a reviewed public-safe route/navigation smoke baseline before promoting
  any public claims beyond checked-in synthetic fixtures.
- Consider a future ASP.NET-specific flow selector/report slice only if path
  and reverse models can represent route/page/navigation roots without implying
  runtime reachability.
- Add deeper semantic-symbol route resolution if the scanner exposes live
  Roslyn semantic models to legacy extractors in a later architecture slice.
