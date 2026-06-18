# Route Flow Service/Data Composition Implementation State

Status: implementation-slice-in-progress.

Branch: `codex/implement-route-flow-service-data-composition`

Base branch: `dev`

Issue: `#179`

## Scope Decisions

- This branch implements the first product slice for the already-reviewed spec:
  route-flow rule catalog additions plus read-only projection of
  `combined_argument_flows` and `combined_fact_symbols`.
- The spec targets deterministic route-flow composition over existing combined
  index evidence by extending the existing `tracemap route-flow` command and
  `CombinedRouteFlowReporter`.
- The spec requires projection from `combined_argument_flows` and
  `combined_fact_symbols` into route-flow detail rows when static joins are
  credible.
- Interface calls may expose static implementation candidates, but the wording
  must not claim runtime dependency injection binding or runtime target
  selection.
- Missing bridges, missing optional tables, missing extractors, reduced
  coverage, and ambiguity are represented as rule-backed gaps rather than
  inferred flows.
- Existing `combined.route-flow.*` rule IDs and `RouteFlowClassifications`
  values are reused. This spec does not introduce a parallel `route.flow.*`
  namespace.
- Existing `route-flow-report.json` output is extended backward-compatibly;
  introducing a new incompatible schema is out of scope for this spec.
- Task 4 must land before implementation tasks that emit rows or gaps. The rule
  catalog entries and gap emits list are prerequisites for the projection,
  traversal, rendering, and validation work.
- Public artifacts must stay generic. Private validation is described only as a
  private legacy ASP.NET smoke sample with a normalized API route.

## Privacy Notes

The spec intentionally avoids private local paths, private sample names, private
route strings, raw SQL, raw config values, source snippets, secrets, raw endpoint
URLs, and raw remotes. Any implementation smoke output should remain ignored and
local-only unless reviewed and redacted.

## Kiro Review Notes

- Opus Kiro review completed with full coverage. Blocking findings patched:
  align with the existing route-flow report instead of creating a parallel rule,
  classification, and schema surface; reconcile review/validation state.
- Sonnet Kiro review completed with full coverage. Blocking findings patched:
  reuse `combined.route-flow.*` rules, use existing route-flow classifications,
  suppress present-but-unprojected gaps after projection, and name concrete
  existing fact/table sources.
- Self-review completed after patching: checked evidence boundaries, redaction,
  implementation task scope, and that no implementation code is included.

Review artifacts were saved under the ignored Kiro review artifact directory.

## Validation Notes

- `git diff --cached --check`: passed after staging the spec files.
- `./scripts/check-private-paths.sh`: passed after staging the spec files.
- Kiro review wrapper self-test: passed.
- Repo spec lint/check script: no dedicated spec lint script found. The repo
  spec convention is documented in `.kiro/specs/README.md`; this spec has the
  required state file and completed spec-delivery checklist.
- Residual safety scan for stale route-flow names, local absolute paths, private
  sample fragments, and old speculative rule IDs: passed.

## Implementation Slice: Projection Read Model

2026-06-18 branch `codex/implement-route-flow-service-data-composition`
implements the first suggested PR boundary:

- Added `combined.route-flow.argument-projection.v1` and
  `combined.route-flow.fact-symbol-projection.v1` to `rules/rule-catalog.yml`
  with limitations before emitting those rule IDs.
- Extended `combined.route-flow.gap.v1` with
  `ArgumentProjectionUnavailable` and `FactSymbolProjectionUnavailable`.
- Removed the broad present-but-unprojected `ExtractorUnavailable` gap for
  `combined_argument_flows` and `combined_fact_symbols`.
- Added a read-only route-flow projection reader for `combined_argument_flows`
  and `combined_fact_symbols`.
- After PR review-loop feedback, bounded the projection SQL reads to selected
  route-flow caller/callee pairs and selected source-local symbols, with bounded
  fact-ID probes for projection-unavailable gaps instead of full row buffering.
- Added combined-index lookup indexes for future indexes on fact-symbol
  source/symbol, fact-symbol source/fact, and argument-flow source/caller/callee
  access patterns.
- Projected joined argument-flow rows into existing `LogicRows` as
  `argument-flow` rows only when caller/callee symbols are already connected by
  selected route-flow path evidence.
- Projected joined fact-symbol rows into existing `LogicRows` as object/query/
  data/dependency-style rows by joining `combined_fact_symbols` to
  `combined_facts` and selected source-local symbols.
- Added scoped projection gaps when optional rows exist but cannot be joined to
  the selected route-flow path.
- Added a scoped fact-symbol projection gap when the table exists but this first
  slice does not project the observed fact types directly.
- Unsupported fact-symbol rows attached to the selected route-flow path now emit
  a scoped projection gap even when other fact-symbol rows on that path are
  projected.
- Hashed dependency-surface `tableName`, `columnNames`, and `configKey`
  metadata in route-flow output and added regression assertions for those
  fields.
- Projection rows now cite `combined.route-flow.redaction.v1` in supporting
  rule IDs when unsafe source/target/table/argument values are hashed.
- Kept the route-flow JSON contract backward-compatible: `reportType` remains
  `route-flow` and `version` remains `1.0`.
- Added focused route-flow tests for projection rows, scoped projection gaps,
  old gap suppression, public-safe metadata hashing, deterministic output, and
  rule catalog resolution.

## Current Validation

- `dotnet test src/dotnet/TraceMap.sln --filter CombinedRouteFlowTests`: passed
  after projection implementation and after Kiro review fixes.
- Kiro implementation review with Sonnet (`claude-sonnet-4.6`) completed with
  reduced coverage on first run and found blocking issues. Patched:
  classification-cap helper naming, sensitive config/connection fact-symbol
  projection, fragile logic-row substring heuristics, display-name fallback
  joins, redaction provenance, dependency-surface table/column/config metadata
  hashing, and unsupported fact-symbol gap silence.
- Kiro implementation re-review with Sonnet completed with full coverage and
  identified dependency-surface metadata hashing as the remaining blocking
  issue; patched as noted above.
- Final allowed Kiro implementation re-review round completed with reduced
  coverage and reported additional blocking concerns. Patched concrete current
  slice issues without requesting another re-review: safe entry display symbols,
  sensitive parameter-name hashing, selected-source scoping for projection
  reads, and unbounded projection table reads. The report also listed deferred
  future-scope items for interface candidates, parameter-forward traversal, and
  downstream bridge/data-surface gaps.
- Final local validation after all Kiro-driven patches:
  `dotnet test src/dotnet/TraceMap.sln --filter CombinedRouteFlowTests` passed;
  `dotnet test src/dotnet/TraceMap.sln` passed with 439 tests;
  `git diff --check` passed; `./scripts/check-private-paths.sh` passed.
- PR review-loop remediation patched the actionable Qodo performance finding for
  buffered large projection reads, the optional repeated-sort finding, and the
  Codex inline unsupported fact-symbol gap finding. Validation after that patch:
  `dotnet test src/dotnet/TraceMap.sln --filter CombinedRouteFlowTests` passed;
  `dotnet test src/dotnet/TraceMap.sln` passed with 439 tests;
  `git diff --check` passed after whitespace cleanup;
  `./scripts/check-private-paths.sh` passed.
- Ran the checked-in combined path/reverse smoke via
  `./scripts/smoke-combined-paths.sh <tmp>` after installing local TypeScript
  dependencies with `npm --prefix src/typescript ci`, then ran a direct
  `tracemap route-flow` CLI smoke against that combined index and verified
  `route-flow-report.md`, `route-flow-report.json`, report type/version, summary
  shape, and forbidden runtime wording.
- Private legacy ASP.NET smoke was not run in this PR. The implemented slice is
  covered by synthetic combined-index tests and the checked-in sample smoke;
  private smoke remains a follow-up for downstream traversal/data-surface PRs
  where private legacy route-flow validation is materially broader.

## Remaining Follow-Up For Implementation

- Continue downstream traversal beyond the existing selected combined path graph:
  route root to service/repository/data methods with bridge gaps.
- Add interface implementation candidate expansion tests and classification
  caps for single, multiple, no-candidate, syntax-only, name-only, and high
  fan-out cases.
- Attach broader object-shape, repository-like, data-surface, and
  parameter-forward evidence, or record explicit deferrals with rationale.
- Add focused coverage for `combined_parameter_forward_edges` as a route-flow
  traversal bridge. Deferred to the downstream traversal PR because the first
  slice only enriches already-selected route-flow paths and does not yet own
  route-root bridge expansion.
- Run broader validation, Kiro implementation review, PR review loop, and record
  final results below before merge.
