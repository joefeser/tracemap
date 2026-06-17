# UI Field and Property Lineage Implementation State

## Current Branch

`codex/spec-ui-field-property-lineage`

## Status

`not-started`

## Current Slice

This branch is spec-only. It adds the Kiro spec for UI field/property lineage
under `.kiro/specs/ui-field-property-lineage/` and does not implement scanner,
reporting, CLI, rule-catalog, fixture, or product-code changes.

## Source Material

- Issue #165: https://github.com/joefeser/tracemap/issues/165
- Related issue #159: https://github.com/joefeser/tracemap/issues/159

Issue #165 asks TraceMap to answer a user-facing static evidence question that
starts from a visible UI field or bound property and follows evidence through
template/control binding, component/view-model property, client service payload,
server DTO/model property, controller/action usage, service/repository calls,
and data/entity surfaces where available.

Issue #159 is the related route-centered static flow proposal. This spec treats
route-centered flow as a composable downstream evidence family: UI
field/property lineage can reuse route/client-call flow after a property reaches
HTTP call or endpoint evidence, but property-flow must remain useful and honest
when route-flow evidence is unavailable.

## Scope Decisions

- Proposed CLI shape is `tracemap property-flow --index <combined.sqlite> --property <selector> --out <path>`.
- The command is modeled as a combined-index report/query layer, not a new monolithic scanner.
- Angular and Razor/cshtml are the first UI evidence families described.
- Browser/computer-use evidence is only an optional follow-up demo/validation layer. It is not required for core deterministic claims and cannot replace rule-backed static facts.
- Every evidence row must preserve rule ID, evidence tier, source label, file span, commit SHA, and extractor ID/version where available.
- Reports must include coverage labels, analysis gaps, limitations, and public/private safety rules.
- Endpoint alignment inside a combined index should reuse current combined endpoint matching behavior over `combined_facts` and `index_sources`; persisted `endpoint_matches` rows are not required for v1.
- Route-flow from issue #159 has no concrete schema in this branch, so the spec requires a `RouteFlowUnavailable` gap until #159 defines a machine-checkable route-flow table or equivalent metadata.
- Until issue #159 supplies a route-flow schema signal, implementation must treat route-flow as unavailable and must not invent a fallback route-flow signal.
- `fact:<combinedFactId>` selectors refer to `combined_facts.combined_fact_id`, not a new property-flow-specific ID format.
- No product code, site files, generated outputs, or rule catalog entries are changed in this spec-only slice.
- Rule catalog requirements in the spec are gates for future implementation slices that emit source facts or derived property-flow rows, not a merge gate for this spec-only PR.

## Validation Plan For This Spec-Only Slice

- `git diff --check`
- `./scripts/check-private-paths.sh` if available
- Kiro review through `scripts/kiro-review.mjs` if local Kiro review tooling is available

Kiro CLI Opus and Sonnet reviews should be attempted if locally available. If
the review commands are unavailable, record the exact blocker and use
self-review.

## Implementation Validation Plan For Future Slices

- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- TypeScript adapter tests and build when Angular extraction changes.
- Razor/.NET adapter tests when Razor extraction changes.
- Combined report/path/reverse tests when graph composition changes.
- Relevant pinned smoke checks from `docs/VALIDATION.md` for changed adapters.
- `./scripts/check-private-paths.sh`
- `git diff --check`

## Open Questions For Implementation

- Whether Razor/cshtml extraction should live in an existing .NET scanner project or a focused Razor extractor helper.
- Whether Angular template parsing should use Angular compiler APIs, a lightweight template parser, or a conservative syntax parser in the first slice.
- Whether property-flow should share the existing path graph builder directly or use a thin adapter that projects UI/property roots into path-compatible nodes.
- Which route-flow rule IDs from issue #159 become the stable integration point once that feature lands.
- Whether AutoMapper/projection evidence already has enough stable property-to-property metadata for strong lineage, or should start as review-tier only.

## Blockers

None for the spec-only slice.
