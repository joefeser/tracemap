# Route Flow Endpoint Composition Implementation State

Status: spec-ready

Readiness: ready-for-implementation

Branch: `codex/spec-route-flow-endpoint-composition`

Base branch: `dev`

Issue: `#201`

## Scope Decisions

- This branch is spec-only. It adds a future implementation plan for endpoint
  route-flow composition and does not change runtime scanner, reducer,
  route-flow, paths, reporting, or static-site code.
- The current code already has the route-flow report envelope, classifications,
  bounded path rows, implementation candidate rows, argument/fact-symbol
  projection rows, and public gap codes such as `SelectorNoMatch`,
  `SchemaMissing`, `ReducedCoverage`, `ExtractorUnavailable`, and
  `TruncatedByLimit`. This spec extends that baseline; it does not replace or
  rename shipped JSON values.
- The motivating observation is recorded generically: a private/local sample
  showed route root evidence and raw call evidence, but the route-flow report did
  not stitch them into a composed static route trace.
- The spec extends existing route-flow concepts instead of adding a second
  route-flow command, classification vocabulary, report type, or rule namespace.
- The intended implementation starts at route-binding evidence, bridges to the
  endpoint method symbol, follows static call edges, treats interface
  implementation relationships as review-tier candidates, continues through
  implementation method bodies, attaches business/data logic rows, and emits
  reachable dependency/data surfaces.
- Missing route roots, missing method-symbol bridges, missing call edges,
  missing implementation bridges, reduced coverage, identity gaps, schema gaps,
  extractor gaps, and traversal bounds are represented as rule-backed gaps.
- Interface member to implementation member relationships are static candidate
  bridges only. They do not prove runtime dependency injection selection,
  dynamic dispatch target selection, service locator binding, host activation,
  traffic, or production execution.
- Reduced coverage, unknown commit SHA, missing schema, missing extractor
  metadata, syntax-only evidence, name-only evidence, ambiguity, high fan-out,
  and traversal caps downgrade classifications and prevent clean absence
  conclusions.
- Public artifacts must remain generic and safe: no private local paths, private
  repo names, exact private routes, raw source snippets, raw SQL/config values,
  secrets, or raw remotes.

## Relationship To Nearby Specs

- `route-centered-static-flow-report` defines the route-flow report, command,
  classifications, and core rule family.
- `route-flow-service-data-composition` covers service/data projection work and
  records follow-up tasks for downstream traversal, interface candidates, and
  data-surface attachment.
- This spec narrows issue `#201` into a concrete endpoint-composition slice so a
  future branch can implement route root to method-symbol bridges, method call
  traversal, implementation candidate bridges, downstream method traversal, and
  better gap reasons without reopening the broader route-flow product design.

## Privacy Notes

The spec intentionally avoids private sample names, exact private route strings,
raw source snippets, raw SQL, raw config values, secrets, local absolute paths,
private repository names, endpoint URLs, and raw remotes. Any future private
smoke output should remain ignored and local-only unless separately reviewed and
redacted.

## Review Notes

- Kiro Opus spec review: completed with full coverage. Blocking findings
  patched: keep existing gap names such as `SchemaMissing`, state additive-only
  JSON evolution, and record the current route-flow baseline so implementation
  extends rather than rebuilds existing behavior.
- Kiro Sonnet spec review: completed with full coverage. Blocking findings
  patched: align task readiness with completed review, make rule-catalog
  prerequisites explicit, and tighten syntax fallback wording to source-local
  matching without global short-name stitching.
- Kiro re-review completed once after patching. Opus completed with full
  coverage and found no remaining design blockers after the final clarity
  patch. Sonnet re-review completed with reduced coverage because Kiro denied a
  shell tool request; its process-gate findings were resolved by validation and
  task-state updates, and its clarity findings were patched.
- Self-review: completed after model review patches. Checked evidence
  boundaries, no-AI/non-runtime constraints, classification downgrades, gap
  compatibility, traversal defaults, current-baseline scoping, and privacy
  wording.
- PR review-loop Codex review found one actionable compatibility issue:
  `MissingRouteRoot` wording could be read as replacing `SelectorNoMatch` for
  plain selector misses. Patched the requirements, design, and tasks so
  `SelectorNoMatch` remains the existing selector-miss code and
  `MissingRouteRoot` is only an additive narrower endpoint route-root gap.

## Validation Notes

- `git diff --check`: passed before staging.
- `./scripts/check-private-paths.sh`: passed.
- Spec-only scope check: passed. The only changed files are the four new files
  under `.kiro/specs/route-flow-endpoint-composition/`; no runtime code,
  generated artifacts, or static-site files were changed.
- Focused public-safety scan for URL/path/secret-like patterns found only the
  intentional prohibited-value wording in the spec.
- Post-review selector-gap compatibility patch validation: `git diff --check`
  passed and `./scripts/check-private-paths.sh` passed.

## Follow-Up For Implementation

- Confirm or extend `combined.route-flow.gap.v1` before emitting the new gap
  codes named by this spec.
- Implement route-binding to endpoint method-symbol bridge first; without that
  bridge, downstream call evidence cannot be attributed to a route root.
- Keep interface implementation candidate rows review-tier until a separate
  deterministic runtime-binding evidence model exists.
- Add deterministic output and privacy tests before using private/local smoke
  results to validate the implementation.
