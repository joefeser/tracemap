# Route Flow Endpoint Stitching Tasks

## Spec Delivery Tasks

- [x] 1. Create the Kiro spec packet for issue #201.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-packet.md`.
  - [x] Keep this PR limited to spec files.

- [x] 2. Review the spec before opening the PR.
  - [x] Run Kiro Opus spec review.
  - [x] Run Kiro Sonnet spec review.
  - [x] Patch Medium+ merge-readiness findings or document why none apply.
  - [x] Run at most two re-review cycles.
  - [x] Record review artifacts and final review state in
        `implementation-state.md`.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run any obvious spec/docs validation command.
  - [x] Record validation results in `implementation-state.md`.

## Implementation Tasks

Current state: product implementation has not started for this spec. The tasks
below are intended follow-up implementation slices. Do not check them off in a
spec-only PR.

- [x] 4. Audit live route-flow endpoint stitching behavior. Requirements: 1, 2, 5.
  - [x] Inspect current `CombinedRouteFlowReport` root selection, selector
        trace, summary rollup, and gap behavior.
  - [x] Map endpoint-stitching concepts to existing route-flow gap names before
        adding any new closed-set gap name.
  - [x] Identify existing route-flow rows/gaps that already satisfy this spec.
  - [x] Add fixture tests that lock current behavior before changing traversal
        or output shape.
  - [x] Decide whether endpoint bridge state belongs on existing entry evidence
        rows or a new additive JSON array.

- [ ] 5. Implement endpoint root to method-symbol bridge state. Requirements:
      1, 5, 6.
  - [x] Record bridge state for route/client/endpoint selectors:
        `method-symbol`, `path-node`, `symbol-fallback`, `missing`, or
        `ambiguous`.
  - [x] Record explicit `reduced-coverage` bridge state where coverage, schema,
        extractor, or source identity prevents credible root bridge
        classification.
  - [x] Reuse existing `MissingMethodSymbolBridge` when route evidence cannot
        bridge to a method/root node, unless the audit proves a new closed-set
        root-specific gap is required.
  - [ ] Emit duplicate/ambiguous root gaps for duplicate normalized selectors.
  - [x] Preserve supporting facts, file spans, source labels, commit SHAs,
        extractor identities, and limitations.
  - [ ] Add tests for success, missing bridge, duplicate route roots, dynamic
        route selectors, reduced coverage, and unknown source identity.

- [ ] 6. Implement endpoint method to direct call-edge stitching. Requirements:
      2, 5, 6.
  - [ ] Stitch from endpoint method/root node to source-local call, creation,
        parameter-forward, or path graph edges using symbol or graph identity.
  - [ ] Avoid same-file, directory, text, or short-name-only joins.
  - [ ] Emit `MissingCallEdge` or equivalent gap under full coverage.
  - [ ] Cover both `MissingCallEdge` emitting shapes: traversal dead-end nodes
        with untraversable call-like edges, and zero-path endpoint roots whose
        bridged method has downstream call evidence that could not connect.
  - [ ] Emit reduced/unknown gap under reduced coverage.
  - [ ] Add tests for direct call, no direct call under full coverage, no direct
        call under reduced coverage, cycle safety, and deterministic ordering.

- [ ] 7. Harden interface implementation bridge behavior. Requirements: 3, 5.
  - [ ] Reuse existing `combined_symbol_relationships` evidence and route-flow
        interface bridge rows where already implemented.
  - [ ] Add or extract shared candidate helper only if live code duplicates
        paths/route-flow candidate derivation.
  - [ ] Keep bridge rows capped at `NeedsReviewStaticRouteFlow`.
  - [ ] Emit deterministic no-candidate, multi-candidate, high-fan-out,
        cross-source, cross-language, syntax-only, and runtime-binding gaps.
  - [ ] Add tests proving no runtime DI proof is claimed and strong
        classification is blocked by candidate bridges.

- [ ] 8. Add service/data/dependency attachment precision. Requirements: 4, 5, 6.
  - [ ] Attach service, repository, query, SQL-shape, object/projection,
        legacy-data, package/config, HTTP client, WCF, ASMX/SOAP, remoting,
        event/message, storage, and other dependency surfaces only through
        credible path adjacency.
  - [ ] Emit scoped attachment gaps when candidate surfaces exist but cannot
        stitch to the selected endpoint path.
  - [ ] Add tests for attached surfaces, unattached surfaces, unsafe metadata
        hashing, and deterministic stable IDs.

- [ ] 9. Tighten classification and gap rollup. Requirements: 5.
  - [ ] Require full coverage for `StrongStaticRouteFlow` and clean
        `NoRouteFlowEvidence`.
  - [ ] Ensure `UnknownAnalysisGap` wins over no-evidence conclusions when
        schema, extractor, coverage, or identity blocks credibility.
  - [ ] Add tests for weak evidence, ambiguity, truncation, unknown commit SHA,
        reduced coverage, and missing optional schemas.

- [ ] 10. Validate output compatibility and safety. Requirements: 6, 7.
  - [x] Preserve existing `route-flow-report.json` report type and version
        unless a future breaking-schema spec changes them.
  - [x] Keep new fields additive with deterministic ordering and stable IDs.
  - [ ] Add byte-stable JSON tests.
  - [ ] Add Markdown/JSON/log negative tests for local paths, raw remotes, raw
        URLs, raw SQL, config values, source snippets, private names, secrets,
        and raw selector values.

- [ ] 11. Run implementation validation. Requirements: 7.
  - [x] Run focused route-flow tests.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run relevant pinned smoke checks from `docs/VALIDATION.md` if shared
        graph, route-flow, language-adapter, or reporting behavior changes.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Runtime DI/container analysis.
- Browser/computer-use proof of UI route execution.
- Batch route-flow query files.
- Persisted derived route-flow rows.
- HTML graph visualization beyond existing explorer/vault/report exports.
- Cross-repository runtime binding.
- Fuzzy matching, semantic search, embeddings, vectors, or prompt
  classification.
