# Implementation State

Status: implemented
Readiness: implemented
Last updated: 2026-06-27
Branch: codex/site-property-flow-schema-gap
Base: dev at 4b5844ff07199969eacd040e9383037d0b266d49
Target base: dev
Public claim level: concept

## Summary

This site slice adds a concept-level public proof-path page for the
property-flow route-flow schema compatibility gap that landed after PR #376.
It does not change scanner, reducer, property-flow, route-flow, runtime,
production, release, or impact behavior.

Selected placement: `/proof-paths/property-flow-schema/`.

Rejected placement alternatives:

- `/use-cases/property-flow-schema/`: rejected because the page explains one
  evidence stop condition, not a broad user workflow.
- `/property-flow-schema/`: rejected because concept-level compatibility
  guidance should not become a first-class product route.
- Section on `/proof-paths/`: rejected because the explanation needs its own
  rule, tier, schema-status, and validation context.
- Section on `/evidence/gaps/`: rejected because the route needs both
  property-flow and route-flow context, while the gap register stays generic.

## Verified Current-Branch Evidence

Reviewed before writing public copy:

- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs` records route-flow
  schema status as unavailable, unsupported, empty, or available.
- `PropertyFlowReport.cs` emits `UnsupportedRouteFlowSchema` when a present
  `combined_route_flow_edges` table/view lacks a compatible normalized route
  key column.
- The unsupported gap uses `property-flow.schema.v1`,
  `Tier4Unknown`, and `UnknownAnalysisGap`; it carries selected supporting
  fact IDs, source IDs, commit SHA evidence, file path/line span where a
  selected anchor exists, and observed schema-column context.
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs` contains focused
  coverage for `Property_flow_marks_incompatible_route_flow_schema_as_unsupported`.
- `rules/rule-catalog.yml` documents `property-flow.schema.v1`, including the
  unsupported route-flow schema gap and its limitation that unsupported schema
  does not prove route-flow evidence is absent.
- `docs/ACCEPTANCE.md` records property-flow schema-gap acceptance criteria.

## Scope Decisions

- Public route remains concept-level, with narrow verified source-behavior
  language only.
- The page is not in primary navigation.
- The page links to `/proof-paths/`, `/proof-paths/route-flow/`,
  `/evidence/gaps/`, `/evidence/`, `/limitations/`, `/static-vs-runtime/`,
  and `/review-claim-checklist/`.
- `/proof-paths/` links back to `/proof-paths/property-flow-schema/`.
- Public copy names checked-in source files and rule IDs, but does not publish
  raw snippets, raw generated artifacts, local paths, private route values, or
  private sample names.

## Validation Results

Completed on 2026-06-27:

- `cd site && npm test`: passed, 609 tests after ACK-authorized validator
  hardening for metadata claim scans, private route metadata scans, and
  boundary-section claim scans.
- `cd site && npm run validate`: passed; built static site and validated
  78 HTML files, 2736 internal references, and 77 sitemap URLs.
- `cd site && npm run build`: passed.
- desktop browser sanity: passed at 1440x1100 against
  `/proof-paths/property-flow-schema/`; title and H1 rendered, no horizontal
  overflow, no broken images, and expected main links were present.
- mobile browser sanity: passed at 390x844 against
  `/proof-paths/property-flow-schema/`; title and H1 rendered, no horizontal
  overflow, no broken images, and expected hero links were present.
- Browser sanity was not repeated after the ACK-authorized validator-only
  patch because no page source, CSS, layout, or metadata content changed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed; private path guard passed.

## Follow-Up Items

- None currently planned. Any future stronger public claim should cite a
  generated public-safe demo artifact or repository evidence row and preserve
  the same rule/tier/limitation boundary.
