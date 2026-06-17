# Legacy ASP.NET Route And Navigation Discovery Implementation State

## Status

- Status: `not-started`
- Spec branch: `codex/spec-legacy-aspnet-route-navigation-discovery`
- Spec path: `.kiro/specs/legacy-aspnet-route-navigation-discovery/`
- Current scope: spec-only branch for requirements, design, implementation
  tasks, and handoff notes.
- Product code status: not implemented in this branch.

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

## Flow Rule Decision

- Current spec default: reuse existing `legacy.flow.*` rules for route/page/
  navigation path and report integration where their models can represent the
  evidence without semantic overload.
- Deferred option: add `legacy.aspnet.flow.v1` and an ASP.NET-specific flow fact
  only if an implementation slice determines existing flow rules cannot express
  the needed route/page/navigation semantics safely. That decision must be
  recorded here before the implementation emits any row that cites the new rule.

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

## Kiro Review State

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
  - added schema/combined-reader compatibility decision coverage for new
    `AspNet*` fact types;
  - added test obligations for superficially safe `location` paths with query
    or fragment components, cross-fact-type navigation hash matches, canonical
    hash format, and role-separated hashes.

## Follow-Up Items

- In implementation slice 1, add rule catalog entries for
  `legacy.aspnet.surface.v1`, `legacy.aspnet.route.v1`, and any emitted route
  gaps before facts or report rows ship.
- If an implementation slice needs route/page/navigation path rows beyond
  existing `legacy.flow.*` semantics, record that decision under
  `Flow Rule Decision` before adding `legacy.aspnet.flow.v1`.
- Add focused fixtures for `Global.asax`, `MapPageRoute`, `web.config`
  handlers/modules/pages, `.ashx`, PageMethods, static navigation, ambiguous
  navigation, and redaction.
- Extend legacy smoke catalog only after checked-in fixtures or reviewed public
  summaries exist.
