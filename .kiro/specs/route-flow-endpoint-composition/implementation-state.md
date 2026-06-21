# Route Flow Endpoint Composition Implementation State

Status: implementation-complete

Readiness: merged

Post-promotion note: PR #206 implemented this slice and PR #247 promoted it to
`main`. Remaining unchecked tasks are follow-up hardening.

Branch: `codex/implement-route-flow-endpoint-composition`

Base branch: `dev`

Issue: `#201`

## Scope Decisions

- This branch implements the endpoint route-flow composition slice in the
  existing .NET route-flow reporter. It does not change scanner extraction,
  reducer behavior, static-site code, or stored combined-index schema.
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
- This spec narrows issue `#201` into a concrete endpoint-composition slice:
  route root to method-symbol bridges, method call traversal, implementation
  candidate bridges, downstream method traversal, and better gap reasons
  without reopening the broader route-flow product design.

## Privacy Notes

The spec intentionally avoids private sample names, exact private route strings,
raw source snippets, raw SQL, raw config values, secrets, local absolute paths,
private repository names, endpoint URLs, and raw remotes. Any future private
smoke output should remain ignored and local-only unless separately reviewed and
redacted.

## Review Notes

### Implementation Review Notes

- Kiro implementation review with Sonnet (`claude-sonnet-4.6`) completed with
  reduced coverage because Kiro denied shell tool access. Blocking findings
  patched: safe surface display fallback, dead-end gap logic without
  name-only interface heuristics, per-root `MissingCallEdge` accounting,
  no-evidence coverage preconditions, and stable flow row IDs for shared
  prefixes.
- Kiro re-review cycle 1 with Sonnet completed with reduced coverage because
  Kiro denied shell tool access. Blocking findings patched: replaced
  interface-name heuristic with combined-symbol kind evidence, tightened
  no-evidence coverage checks, strengthened direct concrete edge and privacy
  assertions.
- Kiro re-review cycle 2 with Sonnet completed with reduced coverage because
  Kiro denied shell tool access. Code blockers were resolved; remaining
  blockers were stale implementation state and validation task status, patched
  in this file and `tasks.md`. Non-blocking follow-ups remain listed below.
- PR review loop found three Gemini null-source handling threads and two Codex
  P2 route-flow compatibility threads. Patched null `source_index_id` reads,
  null source-bucket keys, source compatibility checks, client-call path
  preservation, and `--from-source` scoping for endpoint composition roots.
- PR review loop later found Qodo action-required threads for route-root gap
  evidence, composition gap file/line metadata, unrelated same-source
  `MissingCallEdge` provenance, and generic selector gaps conflicting with
  `MissingRouteRoot`. Patched each path and added focused assertions for the
  evidence/provenance behavior.

### Spec Review Notes

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

- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/TraceMap.sln --filter CombinedRouteFlowTests`:
  passed with 18 focused route-flow tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed with 464 tests.
- Public route-flow CLI smoke against `.tracemap-demo/combined/endpoint-stack.sqlite`
  passed using selector `GET /api/admin/runner/get-by-id/{id}`. Output files
  were written under ignored `.tmp/route-flow-endpoint-composition-smoke/` and
  included report type `route-flow`, version `1.0`, entry evidence, static flow
  rows including `endpoint-method-bridge` / `route-bound-to-symbol`, business/data
  logic rows, dependency surfaces, and reduced-coverage gaps.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Implementation Notes

- Added route-flow-specific endpoint composition over the existing combined path
  graph inventory. The pass selects endpoint route roots, bridges them to
  source-local method symbols, traverses bounded static call/context/surface
  edges, and uses existing route-flow JSON/Markdown output models.
- Added static interface implementation candidate traversal by reversing
  source-local relationship evidence as review-tier candidate edges only. The
  implementation reads combined symbol kind metadata to avoid name-only
  interface gap attribution.
- Added additive gaps through `combined.route-flow.gap.v1`:
  `MissingRouteRoot`, `MissingMethodSymbolBridge`, `MissingCallEdge`,
  `MissingImplementationBridge`, `ImplementationCandidateUnavailable`,
  `AmbiguousImplementationCandidates`, `IdentityGap`, and `TraversalBounds`.
  Existing public gap names such as `SelectorNoMatch`, `SchemaMissing`,
  `ReducedCoverage`, `ExtractorUnavailable`, and `TruncatedByLimit` were
  preserved.
- Candidate-dependent rows and surfaces are capped at
  `NeedsReviewStaticRouteFlow` or weaker. Syntax/textual route-root bridges and
  mixed-tier paths also remain capped by the weakest required evidence.
- Stable flow row IDs now deduplicate shared path prefixes across multiple
  terminal surfaces. Surface display falls back to safe descriptors such as
  `shape:` or `text-hash:` rather than raw snippets, SQL, URLs, or config
  values.
- Route-flow gap JSON now includes optional safe file path and line-span fields
  for composition gaps when a concrete static node anchors the gap. This is an
  additive schema extension.
- Focused tests cover route-bound-to-symbol rows, semantic and syntax-tier route
  bridge behavior, missing route roots, missing method-symbol bridges, direct
  downstream calls, missing call edges, single and multiple interface
  implementation candidates, no-candidate gaps, direct concrete calls alongside
  interface bridges, mixed-tier classification caps, clean no-evidence gap
  preconditions, client-call generic path preservation, source-scoped
  composition roots, deterministic Markdown/JSON output, rule catalog coverage,
  and privacy redaction.

## Deferred Follow-Up

- Add explicit traversal-bound regression coverage for `TraversalBounds`.
- Add syntax-only, name-only, and calibrated high-fan-out implementation
  candidate variants beyond the multiple-candidate ambiguity test.
- Add unreachable dependency/data surface variants and near-but-unconnected
  logic assertions beyond current path-context/projection coverage.
- Add runtime schema-backward-compatibility tests that physically remove optional
  route-flow detail tables and assert `SchemaMissing` remains unchanged.
- Consider tightening `MissingCallEdge` supporting fact IDs to the nearest
  source-local frontier instead of citing same-source raw call evidence.

## Prior Spec-Only Validation Notes

- `git diff --check`: passed before staging.
- `./scripts/check-private-paths.sh`: passed.
- Spec-only scope check: passed. The only changed files are the four new files
  under `.kiro/specs/route-flow-endpoint-composition/`; no runtime code,
  generated artifacts, or static-site files were changed.
- Focused public-safety scan for URL/path/secret-like patterns found only the
  intentional prohibited-value wording in the spec.
- Post-review selector-gap compatibility patch validation: `git diff --check`
  passed and `./scripts/check-private-paths.sh` passed.
