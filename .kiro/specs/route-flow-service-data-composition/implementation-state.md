# Route Flow Service/Data Composition Implementation State

Status: spec-ready; implementation not started.

Branch: `codex/spec-route-flow-service-data-composition`

Base branch: `dev`

Issue: `#179`

## Scope Decisions

- This branch adds a Kiro spec only. It does not change scanner, reducer,
  reporting, CLI, tests, schemas, rule catalog, or generated artifacts.
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

## Follow-Up For Implementation

- Implement projection as a backward-compatible extension of the existing
  `route-flow-report.json` contract.
- Use concrete existing sources for the first implementation slice:
  `combined_facts`, `combined_fact_symbols`, `combined_argument_flows`,
  `combined_dependency_edges`, `FactTypes.ObjectShapeInferred`, and
  `FactTypes.QueryPatternDetected`.
- Treat `combined_fact_symbols` as a linking table; join to `combined_facts` for
  rule ID, evidence tier, file path, and line-span provenance.
- Repository-like rows are derived from method/symbol plus surface evidence;
  there is no dedicated repository table in this spec.
- Keep implementation PRs small and update this file as each slice lands.
