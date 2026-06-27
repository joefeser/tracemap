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
- [ ] Commit the spec branch.
- [ ] Push the branch and open a PR to `dev`.
- [ ] Wait 3 minutes, then run ACK PR loop.
- [ ] Follow ACK-authorized actions only; do not manually tag review bots, do
  not merge, do not force-push, and never squash.

## PR 1: Terminal Context Gate And One Narrow Context Family

- [ ] 1. Audit live property-flow and route-flow contracts.
  Requirements: 1, 2, 3, 4.
  - [ ] Confirm `property-flow` report version and row shapes.
  - [ ] Confirm current route-flow/path/reverse/query/data/dependency rows that
    can be consumed without scanner changes.
  - [ ] Identify the smallest terminal context family that can be attached
    through existing selected-property facts.
  - [ ] Record which existing rules are reused and why
    `property-flow.terminal-context.v1` is or is not needed before product
    code changes.
  - [ ] Record the chosen context family and unsupported alternatives in this
    spec's `implementation-state.md` before product edits.

- [ ] 2. Define catalog-first terminal context behavior.
  Requirements: 3, 4, 5.
  - [ ] Reuse existing catalogued rules wherever possible.
  - [ ] Add `property-flow.terminal-context.v1` or another catalogued rule only
    if existing rules are insufficient.
  - [ ] Document new emitted artifacts, gap codes, evidence tiers, and
    limitations before emitting them.
  - [ ] Add tests asserting emitted terminal-context rule IDs resolve to
    `rules/rule-catalog.yml`.

- [ ] 3. Implement one property-trail-gated terminal context family.
  Requirements: 1, 2, 4, 5.
  - [ ] Attach selected terminal context only when existing facts expose a
    selected-property bridge.
  - [ ] Preserve supporting fact/edge IDs, source labels, commit SHAs, file
    spans, extractor IDs/versions, rule IDs, tiers, and coverage labels.
  - [ ] Emit an explicit gap or omit context when the selected-property bridge
    is absent.
  - [ ] Keep wording to static terminal context, not runtime proof or impact.
  - [ ] Keep report version `1.0` only if the metadata is additive and safely
    ignorable, and add the consumer compatibility test required by
    Requirement 4 acceptance criterion 6.

- [ ] 4. Add required negative attachment tests.
  Requirements: 1, 5, 6.
  - [ ] Broad endpoint reachability alone does not attach validation,
    read-write, mapping, service, query, data, or dependency terminal context.
  - [ ] Route reachability alone does not attach terminal context.
  - [ ] Same method proximity alone does not attach terminal context.
  - [ ] Same class proximity alone does not attach terminal context.
  - [ ] Same file proximity alone does not attach terminal context.
  - [ ] Same property name alone does not attach terminal context.
  - [ ] Same short symbol name alone does not attach terminal context.
  - [ ] A broad dependency edge from the endpoint alone does not attach
    terminal context.
  - [ ] The same selector repeated across unrelated properties, including
    high-fan-out non-generic names, remains review-tier or gapped and cannot
    attach context by hidden selection.
  - [ ] Same-name/generic high-fan-out evidence remains `NeedsReviewLineage` or
    a gap and cannot upgrade terminal context.
  - [ ] Missing or insufficient property bridges do not silently upgrade any
    path or edge classification.

- [ ] 5. Add positive and gap tests.
  Requirements: 1, 3, 4, 6.
  - [ ] Add one public-safe fixture where selected-property evidence attaches
    the chosen terminal context family.
  - [ ] Add one public-safe fixture where weak-but-present catalogued
    property-specific evidence attaches context as `NeedsReviewLineage` and
    renders the weaker-evidence explanation, including the narrowing criterion
    that qualified any generic name.
  - [ ] Add one fixture where nearby terminal facts exist but the selected
    property trail is absent and a gap or omission results.
  - [ ] Assert emitted terminal-context gap codes map to catalogued rules.
  - [ ] Assert deterministic ordering for roots, paths, context rows, gaps, and
    inventory rows.
  - [ ] Assert unsafe terminal metadata is omitted, hashed, or category-labeled.
  - [ ] Assert terminal context hashes are stable, salt-free for report output,
    machine-independent, and byte-stable for identical inputs.
  - [ ] If report version `1.0` is preserved, assert at least one existing
    property-flow consumer, such as docs-export or another touched
    evidence-export path, safely ignores or renders additive metadata.

- [ ] 6. Validate PR 1.
  Requirements: 7.
  - [ ] Run focused `PropertyFlowTests`.
  - [ ] Run any touched route-flow/path/reverse/export tests.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run additional `docs/VALIDATION.md` adapter checks if scanner or
    language adapter behavior changes.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Update this spec's `implementation-state.md` with validation evidence,
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
