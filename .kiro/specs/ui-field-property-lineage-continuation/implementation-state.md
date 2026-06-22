# UI Field Property Lineage Continuation Implementation State

Status: partial-implementation-in-progress
Readiness: implementation-partial
Branch: codex/implement-ui-field-property-lineage-continuation
Target PR base: dev
Public claim level: hidden

## Scope

This implementation branch starts the next continuation packet for issue #165
after the implemented `property-flow` v1 and
`ui-field-property-lineage-next-slice` model-binding/property identity join.

Selected slice: PR 1 route-flow and endpoint downstream context, narrowed to
route-flow schema contract hardening and explicit no-property-context evidence.
The product change is additive and keeps `property-flow` report version `1.0`:

- `PropertyFlowReporter` now emits `RouteFlowNoPropertyContext` when
  `combined_route_flow_edges` has rows but no selected property trail has
  rule-backed route-flow-specific context.
- route-flow-specific path edges, when present, receive a static route-flow
  context note in JSON/Markdown without upgrading the path classification.
- `property-flow.edge.v1` documents the new gap in the rule catalog.

This PR does not add new browser/computer-use evidence, does not persist
derived property-flow rows, and does not attach broad endpoint reachability as
property lineage.

## Source Material

- GitHub issue #165: Trace UI field bindings to model and backend properties.
- `.kiro/specs/ui-field-property-lineage/`
- `.kiro/specs/ui-field-property-lineage-next-slice/`
- `.kiro/specs/cross-app-endpoint-alignment/`
- `.kiro/specs/route-centered-static-flow-report/`
- `docs/NEXT_EXECUTION_REPORT.md`
- `docs/VALIDATION.md`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`

## Current Repository State Observed

- `tracemap property-flow` exists and is listed in
  `docs/NEXT_EXECUTION_REPORT.md`.
- Existing lineage state is `implemented-v1-with-follow-ups`.
- Existing next-slice state is `implemented` for PR 1: Model-Binding And
  Property Identity Join.
- Existing code contains `PropertyFlowReporter`, `PropertyFlowClassifications`,
  route-flow-unavailable gaps, observed-evidence metadata, Angular/Razor test
  fixtures, and downstream first-hop model-binding tests.
- Remaining next-slice tasks identify PR 2 downstream static composition and PR
  3 report-consumer compatibility as the next useful areas.

## Scope Decisions

- This spec focuses on downstream static composition, not another root
  extractor.
- Browser/computer-use remains optional local hidden/manual validation context
  only; it is not scanner proof, report proof, or public claim evidence.
- Route-flow reuse must be evidence-backed and property-specific.
- Service/data/dependency terminal context must not be attached simply because
  it is reachable from the same endpoint.
- Existing `property-flow` report shape should remain backward compatible
  unless an implementation slice explicitly bumps the report version.
- This slice is report version `1.0` compatible because it only adds an
  optional gap kind and optional path notes; no existing required fields or row
  meanings changed.
- Current combined indexes do not persist route-flow rows into
  `combined_route_flow_edges` by default. The reporter treats that table/view as
  an optional contract signal and emits a gap when rows exist but cannot be tied
  to the selected property trail.
- No runtime DOM, user interaction, auth/session, production telemetry,
  browser-required scanner logic, LLM/vector/prompt classification, or runtime
  database proof belongs in scope.

## Review State

Implementation review:

- Sonnet implementation review ran with full coverage. Medium findings were
  patched by closing the PR 1 route-flow consumption decision to
  `combined_route_flow_edges` as an optional table/view contract signal and by
  tightening consumer compatibility language for additive metadata pass-through
  on existing row types.
- Low findings patched where narrow: path/reverse context is called out as
  static-only in requirements, terminal context must be downstream of already
  joined identity roots, browser/computer-use product code requires a separate
  reviewed spec, and Razor form target composition explicitly joins
  already-extracted `RazorFormTarget` facts instead of adding another root
  extraction pass.
- Sonnet re-review ran with full coverage and reported no Medium or High
  findings. Remaining notes were process/clarification-level only.

Opus spec review ran with reduced coverage because Kiro reported denied shell
tool access after reading the spec and selected source files. Medium findings
were patched:

- removed the inaccurate `property-flow.gap.v1` reuse entry and listed only
  catalogued existing property-flow rules;
- made route-flow property-specificity an explicit acceptance criterion before
  route-flow rows can be attached as property lineage.

Low-risk findings also patched:

- consumer compatibility now requires same-PR consumer patches or a documented
  version bump when new fields cannot be safely ignored;
- planned route-flow gap names are identified as new unless already present at
  implementation time;
- implementation tasks now require catalog-first rule handling before new
  context/gap rows are emitted.

Sonnet spec review ran with full coverage. The Medium finding was patched by
defining "tied to the selected property trail" as rule-backed value-origin,
parameter-forwarding, assignment, mapping, payload, model-binding, fact-symbol,
or equivalent property-specific static evidence, and by stating that broad
endpoint reachability alone does not qualify. Low findings patched by defining
"equivalent" bridges, setting the high-fanout `NeedsReviewLineage` versus
`UnknownAnalysisGap` boundary, tightening "safely ignored" for public-output
consumers, and adding consumer-specific validation for compatibility slices.

Sonnet re-review cycle 1 ran with full coverage. The remaining Medium finding
was patched by making `requirements.md` explicitly forbid independent
route-traversal recomputation outside the shared route-flow helper or documented
route-flow table contract. Low findings were patched by defining equivalent
property-specific evidence inline, tying high fan-out downgrade to the
documented candidate cap unless separately documented and tested, specifying
consumer fallback behavior, and requiring `check-private-paths.sh` for consumer
compatibility slices.

Sonnet re-review cycle 2 ran with full coverage. The remaining Medium finding
was patched by adding a minimum Tier2Structural evidence bar for
`ProbableStaticLineage` route/model context; controller/action name matches,
endpoint reachability, or source proximity alone do not qualify. Low findings
were patched by documenting reduced-coverage precedence over same-name/alias
matches, citing the existing v1 high-fanout threshold of 10 or more candidate
property roots, and requiring consumer inspection in PR 1 and PR 2 when new row
kinds are introduced. No third review cycle was run because this workflow caps
re-review at two cycles.

PR-loop review patch: added the already-catalogued `property-flow.edge.v1` rule
to the continuation spec's reuse list so implementers do not mint a duplicate
derived edge rule.

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-continuation --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-continuation --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

## Validation State

Passed:

- `dotnet test src/dotnet/TraceMap.sln --filter PropertyFlow`: 21 passed
- `dotnet build src/dotnet/TraceMap.sln`: passed with existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings
- `dotnet test src/dotnet/TraceMap.sln`: 606 passed with existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings
- `git diff --check`
- `./scripts/check-private-paths.sh`
- Kiro Sonnet implementation review and one re-review: full coverage; no
  Medium or High findings remaining

## PR State

PR: not opened yet

PR-loop result: not run yet.

## Follow-Ups For Implementation

1. Complete route-flow endpoint downstream context by attaching real
   property-specific route-flow rows once a shared helper or documented table
   contract exposes them.
2. Service/data/dependency terminal context.
3. Export and consumer compatibility.
