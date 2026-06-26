# Route Flow Service/Data Composition Final Tasks

Status: task-8-coverage-classification-gap-downgrade-ready-for-review

## Spec Delivery Tasks

- [x] 1. Draft the final route-flow service/data composition spec packet.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-prompts.md`.
  - [x] Keep the original spec-delivery PR changed files within this spec
        folder.
  - [x] Keep examples synthetic/public-safe and avoid private paths, private
        labels, raw SQL/config values, source snippets, secrets, raw remotes,
        and private route strings.

- [x] 2. Review and patch the spec.
  - [x] Run Kiro Opus spec review or record the exact unavailable-tool/model
        evidence.
  - [x] Run Kiro Sonnet spec review or record the exact unavailable-tool/model
        evidence.
  - [x] Patch Medium+ actionable findings.
  - [x] Run one bounded Sonnet or Opus re-review after patches.
  - [x] Record review commands, artifacts, coverage, findings, dispositions,
        and final readiness in `implementation-state.md`.

- [x] 3. Validate the original spec change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run any obvious spec/docs validation command if present, or record that
        none exists.
  - [x] Confirm the original spec-delivery diff is limited to
        `.kiro/specs/route-flow-service-data-composition-final/`.
  - [x] Record validation results in `implementation-state.md`.

## Next Implementation PR Tasks

Current state: the initial spec PR merged as PR #311 (`43426b7c`), duplicate
endpoint/root selector ambiguity merged as PR #318 (`1e9c5660`), and
source-local service-call cycle gaps merged as PR #320 (`565d7b64`).
Branch `codex/route-flow-task5-matrix` audited `origin/dev` at
`625e6fef9c9a88539545334c3fcd3e979e7d3244` and closed the remaining Task 5
direct-call/no-call/reduced-coverage/deterministic-ordering matrix.
PR #330 hardened route-flow implementation-candidate continuation and runtime
non-proof behavior. PR #331 added the shared static dispatch candidate builder.
Branch `codex/route-flow-task6-gap-matrix` audited `origin/dev` at
`086ad376e387ea8d87e430175ef2673cbc74c0f1` and closed the remaining Task 6
candidate gap/downgrade matrix by reusing the shared builder for route-flow
candidate derivation and emitting deterministic route-flow fan-out gaps.
Implementation tasks below remain sequenced across the Suggested PR Boundaries
section, not intended as one giant product PR. Checkboxes are marked only where
current `dev` code or merged PR records prove the behavior.

- [x] 4. Audit live route-flow composition state. Requirements: 1.
      Suggested boundary: PR 1 prerequisite.
  - [x] Inspect `CombinedRouteFlowReport`, route-flow tests, route-flow rule
        catalog entries, selector/root handling, context groups, bridge states,
        gap constants, and the requested predecessor specs to confirm ownership
        boundaries.
  - [x] Map each required final-slice behavior to existing code or an unchecked
        implementation task.
  - [x] Confirm which gap codes in the design taxonomy already exist under
        `combined.route-flow.gap.v1` in `rules/rule-catalog.yml` and which
        require a rule-catalog limitation update before first use.
  - [x] Verify emitted top-level JSON field names against the live
        `RouteFlowReport` serializer before treating the field list in this
        spec as confirmed.
  - [x] Record the selected implementation scope in this spec's
        `implementation-state.md`.
  - [x] Confirm no new command, report type, JSON version, traversal engine, or
        rule namespace is needed.

- [x] 5. Complete endpoint/root method to service call stitching. Requirements:
      2, 5, 6.
      Suggested boundary: PR 1.
      Status: complete. Duplicate normalized endpoint roots and source-local
      service-call cycles are implemented on `dev`; branch
      `codex/route-flow-task5-matrix` closed the narrower
      direct-call/no-call/reduced-coverage test matrix, selector-blocker
      suppression, and deterministic direct-call ordering remainder.
  - [x] Stitch endpoint/root methods to direct service/helper/repository calls
        only through source-local symbol, graph node, fact, or edge identity.
  - [x] Avoid same-file, directory, short-name, display-name, textual, or route
        string joins.
  - [x] Emit `MissingCallEdge` or an existing equivalent gap when full-coverage
        call-like evidence cannot stitch from the selected root. Partially
        covered by current route-flow tests; audited as covered by
        `Route_flow_preserves_interface_call_and_emits_candidate_unavailable_gap`,
        `Route_flow_marks_service_call_cycles_as_traversal_bounds`, and
        `Route_flow_emits_data_surface_attachment_gap_when_downstream_calls_have_no_terminal_surface`.
  - [x] Emit reduced/unknown gaps when coverage, schema, extractor, identity,
        or commit evidence prevents a clean conclusion.
  - [x] Add focused duplicate-root coverage proving ambiguous normalized route
        roots emit a deterministic selector gap and downgrade the report.
  - [x] Add tests for direct service calls, no direct call under full coverage,
        no direct call under reduced coverage, duplicate roots, cycles, and
        deterministic ordering.
    - [x] Duplicate roots: covered by PR #318.
    - [x] Add focused cycle coverage proving source-local service-call cycles
          emit a deterministic `TraversalBounds` gap, suppress clean
          no-evidence claims, and downgrade the report.
    - [x] Add focused no-direct-call reduced-coverage coverage proving clean
          `NoRouteFlowEvidence` gaps are suppressed when source coverage gaps
          prevent a clean absence conclusion.
    - [x] Add focused projection/truncation blocker coverage proving clean
          `NoRouteFlowEvidence` gaps are suppressed when projection schema,
          argument projection, data-surface attachment, or traversal bounds
          blockers prevent a clean absence conclusion.
    - [x] Direct/no-call/reduced-coverage/deterministic-ordering remainder:
          closed by audit plus
          `Route_flow_orders_direct_service_call_paths_deterministically` and
          `Route_flow_suppresses_clean_no_evidence_when_no_direct_call_has_full_coverage_selector_blocker`;
          existing tests cover direct service calls, reduced-coverage no-call,
          duplicate roots, cycles, and projection/truncation blocker
          suppression.

- [x] 6. Harden implementation-candidate continuation. Requirements: 3, 5.
      Suggested boundary: PR 2.
  - [x] Reuse source-local `combined_symbol_relationships` evidence for
        implementation candidates.
  - [x] Continue through a single compiler-backed candidate only as review-tier
        static evidence.
  - [x] Emit deterministic multiple-candidate, no-candidate, high-fan-out,
        syntax-only, name-only, cross-source, cross-language, runtime-binding,
        and reduced-coverage gaps.
    - [x] Multiple-candidate ambiguity:
          `Route_flow_marks_multiple_interface_candidates_ambiguous_but_keeps_direct_concrete_edge_stronger`.
    - [x] No-candidate gaps:
          `Route_flow_preserves_interface_call_and_emits_candidate_unavailable_gap`.
    - [x] High-fan-out gap:
          `Route_flow_caps_high_fan_out_interface_candidates_at_needs_review`
          now asserts deterministic `DispatchCandidateFanOut` under
          `combined.route-flow.gap.v1`.
    - [x] Syntax-only/name-only caps:
          `Route_flow_caps_syntax_only_name_only_interface_candidate_at_needs_review`.
    - [x] Cross-source/cross-language/runtime-binding non-proof:
          `RuntimeBindingNotProven` handling from PR #330 plus
          `Route_flow_runtime_binding_gap_preserves_commit_and_extractor_metadata`.
    - [x] Runtime DI/service locator/factory/reflection/dynamic non-proof:
          `Route_flow_does_not_treat_runtime_adjacent_facts_as_implementation_dispatch_proof`.
    - [x] Reduced-coverage gaps:
          `Route_flow_marks_missing_endpoint_bridge_as_reduced_coverage_when_source_is_reduced`,
          `Route_flow_does_not_emit_clean_no_evidence_gap_when_no_direct_call_under_reduced_coverage`,
          and `Route_flow_identity_reduced_coverage_gap_reduces_report_coverage`.
  - [x] Prove candidate bridges cannot produce `StrongStaticRouteFlow`.
  - [x] Add tests proving no runtime DI target, service locator, factory,
        reflection, or dynamic dispatch proof is claimed.

- [x] 7. Complete service/data/query/dependency attachment precision.
      Requirements: 4, 5, 6.
      Suggested boundary: PR 3.
      Status: in progress. Branch `codex/route-flow-task7-attachments`
      audited `origin/dev` at `9bb459587475` and implemented the smallest
      coherent event/message terminal-surface sub-slice: route-flow now accepts
      selected message terminal surface kinds already supported by the combined
      path graph, attaches them only when joined through selected static
      route-flow paths, and preserves `DataSurfaceAttachmentMissing` for
      adjacent unjoined message surface evidence. Branch
      `codex/task7-attachment-precision` audited `origin/dev` at `7ac6e1ac`
      and added the SQL/query dependency-surface attachment precision
      sub-slice. Branch
      `codex/route-flow-task7-value-origin-precision-20260625173937` audited
      `origin/dev` at `7ac6e1ac883998a7c09c87afc416f0c76be225f6` and closed
      the value-origin/projection sub-slice: selected argument-flow projection
      rows and parameter-forward value-origin rows remain attached only through
      selected static route-flow rows, while adjacent unjoined argument-flow
      evidence now preserves a scoped `ArgumentProjectionUnavailable` gap.
      Branch `codex/task7-http-wcf-attachments-20260625-182659` audited
      `origin/dev` at `cb70ba7def4313cce34034421618062b60cb0c01` and selected
      the ASMX/SOAP dependency-surface sub-slice: route-flow now accepts
      selected ASMX/SOAP surface kinds already emitted by the shared surface
      projection, attaches them only through selected static route-flow paths,
      and preserves `DataSurfaceAttachmentMissing` for adjacent unjoined ASMX
      evidence. Branch
      `codex/route-flow-task7-next-attachment-precision-20260625` audited
      `origin/dev` at `122ca28d61a28c5b0e9cadf96ab9191aef39811f` and selected
      the WCF operation dependency-surface sub-slice: WCF service-reference
      mappings now project as `wcf-operation` dependency surfaces and attach
      only through selected static route-flow paths. Branch
      `codex/task7-next-attachment-precision` audited `origin/dev` at
      `edf9c7de8f7bc5eba06bbf6cd9b0a3636aa0c117` and selected the remoting
      endpoint dependency-surface sub-slice: remoting static evidence now
      projects as dependency surfaces and attaches only through selected
      static route-flow paths. Branch
      `codex/task7-package-config-precision-20260626` audited `origin/dev` at
      `3e987d7d46d2bdae7b9c10441bc16ce2c9332010` and selected the
      package/config dependency-surface sub-slice: selected package and config
      fact-symbol rows now project as dependency-surface context only when
      joined to selected static route-flow rows, package/config terminal
      surfaces attach only through selected route-flow paths, and adjacent
      unjoined package/config evidence preserves `DataSurfaceAttachmentMissing`.
      Branch `codex/task7-http-client-precision-20260626` audited
      `origin/dev` at `268b6d082d18004af0980940f458c42e8fa80407` and
      selected the HTTP client dependency-surface sub-slice: existing
      route-flow terminal-surface attachment already joined `HttpCallDetected`
      rows through selected source-local static paths, and the slice added
      focused evidence that adjacent unjoined HTTP client rows preserve
      `DataSurfaceAttachmentMissing`. Branch
      `codex/task7-storage-precision-20260626` audited `origin/dev` at
      `45fc97b50115d6f7c99389d2b8e114352c387cd9` and selected the
      legacy-data/storage dependency-surface sub-slice: route-flow already
      accepted legacy-data terminal surfaces from the deterministic legacy data
      model projection, and the slice added focused evidence that selected
      entity/storage descriptors attach only through selected static
      route-flow paths while adjacent unjoined storage descriptors preserve
      `DataSurfaceAttachmentMissing`. Branch
      `codex/task7-validation-guard-attachments` audited `origin/dev` at
      `825834304185f6934f27eeeb1b07cae06b7d22bd`, rebased onto `origin/dev`
      at `419cfeb1a46f6ffcfac954e8d2b2a7868d39f113`, and selected the
      async/callback flow-boundary sub-slice: selected `CallbackBoundary` and
      `AsyncBoundary` fact-symbol rows now project as review-tier
      `flow-boundary` logic context only when joined to selected static
      route-flow symbols, while adjacent unjoined boundary evidence preserves
      `FactSymbolProjectionUnavailable`.
      Branch `codex/task7-guard-serializer-attachments` audited `origin/dev`
      at `49f90ec90e722afa9fe4afa18898f1d70291c7e1` and selected the
      validation/guard fact-symbol projection sub-slice: selected
      `BranchFeasibility` guard rows now project as review-tier
      `validation-guard` logic context only when joined to selected
      source-local route-flow symbols, while adjacent unjoined guard evidence
      preserves `FactSymbolProjectionUnavailable`.
      Branch `codex/task7-serializer-contract-precision` audited
      `origin/dev` at `b302c0ab5f9284b983cb3210ee6c0bc5f2d0ad27` and selected
      the serializer/contract fact-symbol projection sub-slice: selected
      `SerializerContractMember` rows now project as review-tier
      `serializer-contract` logic context only when joined to selected
      source-local route-flow symbols, while adjacent unjoined serializer
      contract evidence preserves `FactSymbolProjectionUnavailable`. Branch
      `codex/task7-service-projection-breadth-20260626` audited `origin/dev`
      at `36eedb1039eb98d75ed49c3d3c7161cc44e67424` and selected the final
      service/repository/object/projection breadth sub-slice: existing
      source-local service and repository context grouping remains selected by
      route-flow path rows, selected `ObjectShapeInferred` rows now have
      focused object-shape projection coverage and data-surface context
      grouping, and adjacent unjoined object-shape evidence preserves
      `FactSymbolProjectionUnavailable`.
  - [x] Attach service, repository, object/projection, query-shape, SQL-shape,
        legacy-data, package/config, HTTP client, WCF, ASMX/SOAP, remoting,
        event/message, storage, validation/guard, serializer/contract,
        async/callback, and flow-boundary facts only through selected route-flow
        evidence.
    - [x] SQL/query dependency-surface sub-slice covered by
          `Route_flow_attaches_selected_sql_surface_with_path_context_and_stable_ids`
          and
          `Route_flow_does_not_infer_adjacent_sql_surface_without_selected_join`.
          Broader taxonomy remains unchecked until covered explicitly.
    - [x] Event/message terminal-surface attachment sub-slice covered by PR
          #334.
    - [x] ASMX/SOAP dependency-surface sub-slice covered by
          `Route_flow_attaches_asmx_client_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_asmx_client_surface_without_selected_join`.
    - [x] WCF operation dependency-surface sub-slice covered by
          `Route_flow_attaches_wcf_operation_surface_only_from_selected_static_path`,
          `Route_flow_keeps_same_operation_wcf_surfaces_distinct_by_mapping_identity`,
          and
          `Route_flow_does_not_infer_adjacent_wcf_operation_surface_without_selected_join`.
    - [x] Remoting endpoint dependency-surface sub-slice covered by
          `Route_flow_attaches_remoting_endpoint_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_remoting_endpoint_without_selected_join`.
    - [x] Package/config dependency-surface sub-slice covered by
          `Route_flow_attaches_package_config_surfaces_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_package_config_surface_without_selected_join`.
    - [x] HTTP client dependency-surface sub-slice covered by
          `Route_flow_attaches_http_client_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join`.
    - [x] Legacy-data/storage dependency-surface sub-slice covered by
          `Route_flow_attaches_legacy_data_storage_surfaces_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_legacy_data_storage_without_selected_join`.
    - [x] Async/callback flow-boundary fact-symbol projection sub-slice
          covered by
          `Route_flow_attaches_async_callback_boundaries_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_async_callback_boundary_without_selected_join`.
    - [x] Validation/guard branch-feasibility fact-symbol projection
          sub-slice covered by
          `Route_flow_attaches_validation_guard_branches_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_validation_guard_without_selected_join`.
    - [x] Serializer/contract fact-symbol projection sub-slice covered by
          `Route_flow_attaches_serializer_contract_members_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_serializer_contract_without_selected_join`.
    - [x] Service/repository/object/projection breadth sub-slice covered by
          `Route_flow_writes_route_centered_markdown_and_json_without_mutating_combined_index`,
          `Route_flow_attaches_object_shape_projection_only_from_selected_static_path`,
          and
          `Route_flow_does_not_infer_adjacent_object_shape_without_selected_join`.
  - [x] Render argument-flow and parameter-forward value-origin rows only when
        joined to selected static route-flow rows.
  - [x] Render fact-symbol context only for selected source-local symbols.
  - [x] Emit `ArgumentProjectionUnavailable`,
        `FactSymbolProjectionUnavailable`, `DataSurfaceAttachmentMissing`, or
        shipped equivalents for adjacent but unjoinable facts.
  - [x] Add tests for attached surfaces, unjoinable surfaces, attached versus
        path-context labeling, and deterministic stable IDs.
    - [x] Event/message terminal-surface attachment sub-slice covered by
          `Route_flow_attaches_message_surfaces_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_message_surface_without_selected_join`.
    - [x] Argument-flow/parameter-forward value-origin sub-slice covered by
          `Route_flow_attaches_value_origin_rows_only_from_selected_static_path`,
          including joined projection rows, selected parameter-forward rows,
          adjacent unjoined argument-flow gaps, and deterministic repeated
          row/gap IDs.
    - [x] SQL/query surface attachment, adjacent-unjoinable gap behavior,
          path-context labeling, and deterministic stable IDs covered by
          `Route_flow_attaches_selected_sql_surface_with_path_context_and_stable_ids`
          and
          `Route_flow_does_not_infer_adjacent_sql_surface_without_selected_join`.
    - [x] ASMX/SOAP surface attachment, adjacent-unjoinable gap behavior,
          terminal-surface labeling, dependency context grouping, and
          deterministic stable IDs covered by
          `Route_flow_attaches_asmx_client_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_asmx_client_surface_without_selected_join`.
    - [x] WCF operation surface attachment, adjacent-unjoinable gap behavior,
          terminal-surface labeling, dependency context grouping, and
          deterministic stable IDs covered by
          `Route_flow_attaches_wcf_operation_surface_only_from_selected_static_path`,
          `Route_flow_keeps_same_operation_wcf_surfaces_distinct_by_mapping_identity`,
          and
          `Route_flow_does_not_infer_adjacent_wcf_operation_surface_without_selected_join`.
    - [x] Remoting endpoint surface attachment, adjacent-unjoinable gap
          behavior, terminal-surface labeling, dependency context grouping,
          and deterministic stable IDs covered by
          `Route_flow_attaches_remoting_endpoint_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_remoting_endpoint_without_selected_join`.
    - [x] Package/config surface attachment, adjacent-unjoinable gap behavior,
          selected fact-symbol dependency projection, dependency context
          grouping, and deterministic stable IDs covered by
          `Route_flow_attaches_package_config_surfaces_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_package_config_surface_without_selected_join`.
    - [x] HTTP client surface attachment, adjacent-unjoinable gap behavior,
          terminal-surface labeling, dependency context grouping, and
          deterministic stable IDs covered by
          `Route_flow_attaches_http_client_surface_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join`.
    - [x] Legacy-data/storage surface attachment, adjacent-unjoinable gap
          behavior, terminal-surface labeling, legacy-data context grouping,
          subtype preservation, deterministic stable IDs, and public-safe
          descriptor redaction covered by
          `Route_flow_attaches_legacy_data_storage_surfaces_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_legacy_data_storage_without_selected_join`.
    - [x] Async/callback flow-boundary attachment, adjacent-unjoinable
          fact-symbol gap behavior, value-origin context grouping,
          deterministic stable IDs, and review-tier classification cap covered
          by
          `Route_flow_attaches_async_callback_boundaries_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_async_callback_boundary_without_selected_join`.
    - [x] Validation/guard branch-feasibility attachment,
          adjacent-unjoinable fact-symbol gap behavior, method context
          grouping, safe checked-symbol hashing, deterministic stable IDs, and
          review-tier classification cap covered by
          `Route_flow_attaches_validation_guard_branches_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_validation_guard_without_selected_join`.
    - [x] Serializer/contract member attachment, adjacent-unjoinable
          fact-symbol gap behavior, data-surface context grouping,
          safe contract/member hashing, deterministic stable IDs, and
          review-tier classification cap covered by
          `Route_flow_attaches_serializer_contract_members_only_from_selected_static_path`
          and
          `Route_flow_does_not_infer_adjacent_serializer_contract_without_selected_join`.
    - [x] Service/repository context grouping plus selected object-shape
          projection, adjacent-unjoinable fact-symbol gap behavior,
          data-surface context grouping, safe field-name omission,
          deterministic stable IDs, and Tier3 review-tier classification cap
          covered by
          `Route_flow_writes_route_centered_markdown_and_json_without_mutating_combined_index`,
          `Route_flow_attaches_object_shape_projection_only_from_selected_static_path`,
          and
          `Route_flow_does_not_infer_adjacent_object_shape_without_selected_join`.

- [x] 8. Enforce coverage, classification, and gap downgrade behavior.
      Requirements: 5.
      Suggested boundary: PR 1 for rows touched by Task 5; otherwise PR 4.
      Status: complete. Branch `codex/route-flow-task8-coverage` audited
      `origin/dev` at `10bf8a93` and closed the final downgrade/provenance
      slice by emitting explicit gap classifications, sharing the gap
      classifier used by JSON/filter behavior, preserving Unknown for
      truncation blockers, and adding focused coverage over full/reduced
      coverage, selector, identity, projection, no-evidence, Tier3/review-tier,
      and route-flow-specific gap downgrade behavior.
  - [x] Require full relevant route-flow coverage for
        `StrongStaticRouteFlow`.
  - [x] Require full relevant route-flow coverage and no unresolved bridge,
        projection, identity, schema, extractor, selector, reduced-coverage, or
        truncation gaps for `NoRouteFlowEvidence`.
  - [x] Ensure `UnknownAnalysisGap` wins over clean absence when coverage,
        schema, extractor, identity, commit, generated-code, unsupported-shape,
        or truncation evidence blocks a credible conclusion.
  - [x] Cap syntax-only, textual, name-only, dynamic, ambiguous,
        high-fan-out, candidate, generated-code uncertain, unjoined, and
        reduced-coverage rows at review-tier or unknown.
  - [x] Assert Tier3-only stitched downstream rows cannot satisfy
        `StrongStaticRouteFlow`.
  - [x] Ensure every emitted gap includes rule ID, evidence tier,
        classification, coverage, safe scope, supporting IDs where available,
        file span where available, and limitations.

- [ ] 9. Preserve deterministic JSON/Markdown compatibility. Requirements: 6.
      Suggested boundary: PR 1 for changed fields; otherwise PR 4.
  - [ ] Preserve `reportType = "route-flow"` and JSON `version = "1.0"`.
  - [ ] Add only backward-compatible fields or collections.
  - [ ] Use explicit `null`, empty arrays, and closed-set placeholders for
        unavailable values.
  - [ ] Sort rows, context groups, metadata maps, and gaps deterministically.
  - [ ] Add byte-stable JSON and Markdown-ordering tests.
  - [ ] Add `--exit-code` regression tests for review/unknown/no-evidence
        classifications and error-precedence behavior if this slice touches
        summary or classification rollup.
  - [ ] Add tests for any emitted `classificationCap` field and context-group
        weakest classification/tier/coverage rollup.
  - [ ] Add rule-catalog resolution tests for every emitted route-flow rule ID.

- [ ] 10. Enforce safety and public-safe validation. Requirements: 7, 8.
      Suggested boundary: PR 3 for attached rows; otherwise PR 4.
  - [ ] Reuse safe rendering/hash helpers for route selectors, source labels,
        file paths, SQL/query metadata, config/package metadata, URLs, remotes,
        snippets, and secret-like values.
  - [ ] Cite `combined.route-flow.redaction.v1` when unsafe values are hashed
        or omitted.
  - [ ] Assert redacted or hashed rows cite `combined.route-flow.redaction.v1`
        where the report model supports row-level or supporting rule IDs.
  - [ ] Add Markdown, JSON, log, and fixture-metadata negative tests for raw
        SQL, raw config, URLs/query strings, connection strings, secrets,
        snippets, local absolute paths, raw remotes, hostnames, private labels,
        and private route values.
  - [ ] Run focused route-flow tests, full .NET build/test, private-path guard,
        `git diff --check`, and relevant `docs/VALIDATION.md` pinned smokes or
        record explicit deferrals.
  - [ ] Record any private legacy smoke as local-only generic evidence, with no
        private paths, names, routes, SQL/config values, snippets, secrets,
        remotes, or generated private outputs.

## Suggested PR Boundaries

- PR 1: Task 4 plus Task 5, with Task 8/9/10 coverage only for the fields,
  classifications, and safety behavior touched by endpoint/root-to-direct-call
  stitching.
- PR 2: harden implementation-candidate continuation and candidate downgrade
  tests from Task 6, plus any directly affected Task 8/9 checks.
- PR 3: complete attached versus unjoinable service/data/query/dependency/
  value-origin rows from Task 7 and the directly affected Task 10 safety
  matrix.
- PR 4: final downgrade/truncation/schema/extractor/public-smoke coverage if
  PR 1-3 would otherwise grow too large, especially remaining Task 8/9/10
  checks not tied to an earlier product change.

## Deferred Follow-Ups

- Runtime DI/container binding proof.
- Runtime execution, branch feasibility, symbolic execution, taint/mutation
  analysis, SQL execution, row-count, or production traffic proof.
- Batch query files, HTML graph visualization, persisted derived rows, site
  copy, and public marketing claims.
- Scanner/language-adapter expansion unless a future spec explicitly selects
  that scope.
- LLM calls, embeddings, vector databases, semantic search, fuzzy matching, or
  prompt-based classification.
