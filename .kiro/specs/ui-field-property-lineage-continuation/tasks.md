# UI Field Property Lineage Continuation Tasks

Status: partial-implementation-in-progress
Readiness: implementation-partial

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-continuation/`.
- [x] Inspect existing `ui-field-property-lineage` and
  `ui-field-property-lineage-next-slice` specs before drafting.
- [x] Inspect current property-flow implementation and tests enough to avoid
  duplicating completed work.
- [x] Capture GitHub issue #165 and related route-flow context without private
  sample names, private paths, raw URLs, raw SQL, hostnames, remotes, or source
  snippets.
- [x] Run Kiro spec review with Opus.
- [x] Run Kiro spec review with Sonnet.
- [x] Patch all Medium+ merge-readiness findings; patch Low findings only when
  narrow and safe.
- [x] Run at most two re-review cycles.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Commit, push, open a ready PR into `dev`, and run the PR loop.

## Recommended Implementation Slices

### PR 1: Route-Flow And Endpoint Downstream Context

- [ ] 1. Verify current property-flow and route-flow schema contracts.
  Requirements: 1, 2, 4.
  - [x] Inventory current `PropertyFlowReporter` route-flow checks and gaps.
  - [x] Inventory current route-flow output/tables available in combined
    indexes.
  - [x] Decide whether new rows remain report version `1.0` compatible or
    require `1.1`.
  - [x] Record schema compatibility decisions in implementation state.

- [ ] 2. Reuse route-flow evidence where property-specific context exists.
  Requirements: 2, 3, 4.
  - [x] Add or confirm catalogued rule IDs before emitting any new route-flow
    context or gap classifications.
  - [ ] Inspect docs-export, vault, evidence-pack, and static explorer behavior
    when this slice adds new route-flow context row kinds, even if those
    consumers are not otherwise edited.
  - [ ] Connect property-flow roots to route-flow context only through
    rule-backed endpoint/model/value-origin evidence.
  - [ ] Add route-flow context nodes/edges or supporting rows with supporting
    fact/edge IDs and source labels.
  - [x] Emit `RouteFlowNoPropertyContext` or equivalent gap when route-flow
    exists but is not property-specific.
  - [x] Preserve reduced-coverage downgrades and no-hidden-winner behavior.

- [ ] 3. Add endpoint composition tests.
  Requirements: 2, 4, 7.
  - [ ] Test Angular event/control/payload/HTTP/endpoint/route-flow context.
  - [ ] Test Razor form/binding/action-or-handler/model-binding/route-flow
    context.
  - [x] Test route-flow missing schema, empty schema, reduced coverage, and
    no-property-context gaps.
  - [ ] Test deterministic ordering and byte-stable Markdown/JSON.

### PR 2: Service/Data/Dependency Terminal Context

- [ ] 4. Add terminal context composition from existing facts.
  Requirements: 3, 4.
  - [ ] Add or confirm catalogued rule IDs before emitting any new terminal
    context rows or gap classifications.
  - [ ] Inspect docs-export, vault, evidence-pack, and static explorer behavior
    when this slice adds new terminal context row kinds, even if those consumers
    are not otherwise edited.
  - [ ] Include validation/read/write, mapping, projection, manual assignment,
    service/repository, query, data-surface, dependency-surface, event, or
    message terminal rows only when the selected property-specific trail
    supports them.
  - [ ] Treat path/reverse rows as supporting context, not recomputed
    semantics.
  - [ ] Keep same-name, alias-only, convention-only, and broad endpoint
    reachability context as `NeedsReviewLineage` or explicit gaps.
  - [ ] Do not emit reducer impact or runtime behavior claims.

- [ ] 5. Add terminal context tests.
  Requirements: 3, 4, 7.
  - [ ] Test supported service/data/query/dependency terminal rows.
  - [ ] Test broad endpoint reachability does not attach unrelated terminal
    rows.
  - [ ] Test missing optional schema, ambiguity, fan-out, aliases, and reduced
    coverage.
  - [ ] Test public-safe rendering for surface metadata.

### PR 3: Export And Consumer Compatibility

- [ ] 6. Preserve generated artifact compatibility.
  Requirements: 1, 6.
  - [ ] Inspect docs-export, vault, evidence-pack, and static explorer
    consumers for property-flow row assumptions.
  - [ ] Patch consumers in the same PR or document a report version bump when a
    new field or row kind cannot be safely ignored.
  - [ ] Add safe rendering for new additive row kinds or emit documented gaps.
  - [ ] Ensure generated artifacts keep rule IDs, tiers, coverage labels,
    source labels, commit SHAs, supporting IDs, and limitations where supported.
  - [ ] Reject, hash, or omit unsafe metadata.

- [ ] 7. Add consumer compatibility tests.
  Requirements: 6, 7.
  - [ ] Test docs-export/vault/evidence-pack/static explorer behavior for new
    property-flow rows when touched.
  - [ ] Test deterministic output and private-path guard compatibility.
  - [ ] Test unsupported-schema or unavailable-family gaps where consumers
    cannot safely render new rows.

## Deferred Follow-Ups

- Browser/computer-use capture as a separate local hidden/manual validation
  workflow.
  - [ ] Do not add browser/computer-use product code until a separate reviewed
    spec is opened and approved for that workflow.
- Advanced Angular custom component, directive, pipe, structural directive, and
  reactive-form builder semantics.
- Cross-file Razor/model-binding expansion beyond deterministic symbol or
  syntax evidence.
- Serializer runtime configuration expansion.
- Runtime DI or reflection solving.
- Branch feasibility, symbolic execution, mutation semantics, collection
  contents, or full taint analysis.
- Persisted derived property-flow rows behind an explicit write mode.
- Whole-application property inventory UI.
- LLM classification, embeddings, vector databases, or prompt-based analysis.
