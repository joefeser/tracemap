# UI Field Property Lineage Terminal Context Tasks

Status: ready-for-implementation
Readiness: validated-spec-only

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-terminal-context/`.
- [x] Fetch `origin/dev` and verify the current `dev` SHA.
- [x] Inspect predecessor UI lineage and route-flow composition specs.
- [x] Inspect `PropertyFlowReport.cs`, `PropertyFlowTests.cs`, and
  `rules/rule-catalog.yml` enough to avoid duplicate or stale scope.
- [x] Include current-context notes for `main`/`dev` alignment at
  `4b5844ff` and PR #376 route-schema gap work.
- [x] Keep this PR limited to the assigned spec folder.
- [x] Run Kiro spec review with Opus, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Patch Medium+ actionable findings; patch Low findings only when narrow
  and safe.
- [x] Run one bounded re-review if feasible and record it.
- [x] Update `implementation-state.md` status/readiness after review fixes.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm diff is limited to this spec folder.
- [x] Commit the spec branch.
- [x] Push the branch and open a PR to `dev`.
- [x] Wait 3 minutes, then run ACK PR loop.
- [x] Follow ACK-authorized actions only; do not manually tag review bots, do
  not merge, do not force-push, and never squash.

## PR 1: Terminal Context Gate And One Narrow Context Family

- [x] 1. Audit live property-flow and route-flow contracts.
  Requirements: 1, 2, 3, 4.
  - [x] Confirm `property-flow` report version and row shapes.
  - [x] Confirm current route-flow/path/reverse/query/data/dependency rows that
    can be consumed without scanner changes.
  - [x] Identify the smallest terminal context family that can be attached
    through existing selected-property facts.
  - [x] Record which existing rules are reused and why
    `property-flow.terminal-context.v1` is or is not needed before product
    code changes.
  - [x] If the generic-name set changes, update the live
    `PropertyFlowReporter` generic-name set, docs/spec references, and tests in
    the same implementation PR so names such as `result` and `response` do not
    drift from the documented downgrade behavior. Not applicable for PR 1:
    generic-name set was intentionally unchanged.
  - [x] Record the chosen context family and unsupported alternatives in this
    spec's `implementation-state.md` before product edits.

- [x] 2. Define catalog-first terminal context behavior.
  Requirements: 3, 4, 5.
  - [x] Reuse existing catalogued rules wherever possible.
  - [x] Add `property-flow.terminal-context.v1` or another catalogued rule only
    if existing rules are insufficient. Not needed for PR 1 because existing
    path/surface rules remain the evidence carrier.
  - [x] Document new emitted artifacts, gap codes, evidence tiers, and
    limitations before emitting them. No new artifacts or gap codes are emitted.
  - [x] Add tests asserting emitted terminal-context rule IDs resolve to
    `rules/rule-catalog.yml`. Not applicable for PR 1 because no new
    terminal-context rule ID is emitted; tests assert existing
    `combined.paths.surface-evidence.v1` carries the surface edge.

- [x] 3. Implement one property-trail-gated terminal context family.
  Requirements: 1, 2, 4, 5.
  - [x] Attach selected terminal context only when existing facts expose a
    selected-property bridge.
  - [x] Preserve supporting fact/edge IDs, source labels, commit SHAs, file
    spans, extractor IDs/versions, rule IDs, tiers, and coverage labels.
  - [x] Emit an explicit gap or omit context when the selected-property bridge
    is absent.
  - [x] Keep wording to static terminal context, not runtime proof or impact.
  - [x] Keep report version `1.0` only if the metadata is additive and safely
    ignorable, and add the consumer compatibility test required by
    Requirement 4 acceptance criterion 6. PR 1 keeps additive path notes and
    node safe metadata only.

- [x] 4. Add required negative attachment tests.
  Requirements: 1, 5, 6.
  - [x] Broad endpoint reachability alone does not attach validation,
    read-write, mapping, service, query, data, or dependency terminal context.
  - [x] Route reachability alone does not attach terminal context.
  - [x] Same method proximity alone does not attach terminal context.
  - [x] Same class proximity alone does not attach terminal context.
  - [x] Same file proximity alone does not attach terminal context.
  - [x] Same property name alone does not attach terminal context.
  - [x] Same short symbol name alone does not attach terminal context.
  - [x] A broad dependency edge from the endpoint alone does not attach
    terminal context.
  - [x] The same selector repeated across unrelated properties, including
    high-fan-out non-generic names, remains review-tier or gapped and cannot
    attach context by hidden selection. Existing family/generic tests remain in
    `PropertyFlowTests`.
  - [x] Same-name/generic high-fan-out evidence remains `NeedsReviewLineage` or
    a gap and cannot upgrade terminal context.
  - [x] Missing or insufficient property bridges do not silently upgrade any
    path or edge classification.

- [x] 5. Add positive and gap tests.
  Requirements: 1, 3, 4, 6.
  - [x] Add one public-safe fixture where selected-property evidence attaches
    the chosen terminal context family.
  - [x] Add one public-safe fixture where weak-but-present catalogued
    property-specific evidence attaches context as `NeedsReviewLineage` and
    renders the weaker-evidence explanation, including the narrowing criterion
    that qualified any generic name. Covered by the syntax-tier property
    fixture and static terminal-context note; no generic-name narrowing changed.
  - [x] Add one fixture where nearby terminal facts exist but the selected
    property trail is absent and a gap or omission results.
  - [x] Assert emitted terminal-context gap codes map to catalogued rules. Not
    applicable for PR 1 because no new terminal-context gap codes are emitted.
  - [x] Assert deterministic ordering for roots, paths, context rows, gaps, and
    inventory rows. Existing property-flow deterministic ordering is reused; no
    new context row collection is added.
  - [x] Assert unsafe terminal metadata is omitted, hashed, or category-labeled.
  - [x] Assert terminal context hashes are stable, salt-free for report output,
    machine-independent, and byte-stable for identical inputs. No new terminal
    context hash is emitted in PR 1; existing shape/text hashes are reused.
  - [x] If report version `1.0` is preserved, assert at least one existing
    property-flow consumer, such as docs-export or another touched
    evidence-export path, safely ignores or renders additive metadata. PR 1
    leaves the top-level report contract unchanged and verifies additive note
    and safe metadata behavior.

- [x] 6. Validate PR 1.
  Requirements: 7.
  - [x] Run focused `PropertyFlowTests`.
  - [x] Run any touched route-flow/path/reverse/export tests. No route-flow,
    path, reverse, or export code was touched; full solution validation covered
    integration risk.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run additional `docs/VALIDATION.md` adapter checks if scanner or
    language adapter behavior changes.
    Not applicable for PR 1: no scanner or language adapter behavior changed.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Update this spec's `implementation-state.md` with validation evidence,
    deferred checks, and readiness.

## Deferred Follow-Ups

- Add remaining terminal context families after PR 1 proves the gate.
- Expand Angular/Razor property bridges that require new scanner evidence.
- Add persisted property-flow terminal rows behind an explicit write mode.
- Update vault, docs-export, evidence-pack, explorer, or evidence graph
  consumers only when report shape changes require it.
- Add advanced serializer, DI, branch feasibility, mutation, or taint modeling
  only through future deterministic specs.
- Add runtime/browser/live HTTP/demo validation only as non-upgrading metadata
  in a separate spec.
- Add public site copy or public claims only in a separate public-copy spec.
- AI/LLM classification, embeddings, vector databases, and prompt-based
  analysis remain out of scope.
