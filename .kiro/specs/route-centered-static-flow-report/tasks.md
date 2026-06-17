# Route-Centered Static Flow Report Tasks

## Implementation Tasks

- [ ] 1. Confirm command boundary and rule catalog entries. Requirements: 1, 2, 6, 7, 8.
  - [ ] Confirm final command shape: `tracemap route-flow --index <combined.sqlite> --route "<METHOD> <PATH>" --out <path>`.
  - [ ] Keep `tracemap route-flow` as the public command and do not add a public `tracemap paths --view route-flow` alias in the first implementation slice.
  - [ ] Add `combined.route-flow.selector.v1`.
  - [ ] Add `combined.route-flow.entry.v1`.
  - [ ] Add `combined.route-flow.path.v1`.
  - [ ] Add `combined.route-flow.interface-bridge.v1`.
  - [ ] Add `combined.route-flow.logic-surface.v1`.
  - [ ] Add `combined.route-flow.dependency-surface.v1`.
  - [ ] Add `combined.route-flow.classification.v1`.
  - [ ] Add `combined.route-flow.gap.v1`.
  - [ ] Add `combined.route-flow.redaction.v1`.
  - [ ] Add `combined.route-flow.report.v1`.
  - [ ] Assign evidence tier semantics for each route-flow rule: gap/redaction rules use `Tier4Unknown`, derived report rules inherit weakest supporting evidence where applicable.
  - [ ] Document the v1 high-fan-out threshold before any classification uses it.
  - [ ] Add the high-fan-out threshold to `combined.route-flow.classification.v1` in `rules/rule-catalog.yml`: 10 or more candidates, or more than one candidate for generic terminal keys `status`, `id`, `name`, `value`, `result`, or `response`, with conservative v1 rationale.
  - [ ] Document that `combined.route-flow.classification.v1` stamps the overall summary classification and `combined.route-flow.report.v1` stamps the report envelope.
  - [ ] Document limitations for static evidence, endpoint alignment, traversal, interface bridges, business/data rows, coverage, truncation, and redaction.

- [ ] 2. Reuse or extract combined graph inventory helpers. Requirements: 3, 6, 7.
  - [ ] Inspect `tracemap paths`, `tracemap reverse`, combined report, and evidence export helpers for reusable graph/source/safe-rendering APIs.
  - [ ] Extract behavior-preserving graph inventory APIs if route-flow cannot reuse them directly.
  - [ ] Preserve current `paths`, `reverse`, `impact --include-paths`, and combined report behavior.
  - [ ] Before refactoring shared helpers, add a checked-in fixture test that generates byte-stable combined path and reverse outputs; prove it passes before and after the refactor.

- [ ] 3. Add route-flow models and JSON contract. Requirements: 2, 6, 7, 8.
  - [ ] Define report root with `reportType = "route-flow"` and `version = "1.0"`.
  - [ ] Define query, snapshot, summary, entry evidence, flow row, logic row, dependency surface, gap, and limitation models.
  - [ ] Define `RouteFlowSummary` with overall classification, report coverage, counts, blocking-gap state, truncation state, exit-code state, and classification reasons.
  - [ ] Add a common evidence envelope with rule ID, evidence tier, source label, commit SHA, file span, extractor name/version, supporting fact IDs, edge IDs, and supporting rule IDs.
  - [ ] Populate `RouteFlowQuery.IndexPath` and `RouteFlowQuery.OutputPath` through a factory or builder that sanitizes raw CLI paths before model construction.
  - [ ] Reuse `CombinedReverseSourceInfo` or its shared identity helper for `IdentityVerified`, and reuse existing combined source projections for language, analysis level, build status, and coverage.
  - [ ] Define closed-set `RouteMatchMode`, `GapKind`, `RowKind`, `EdgeKind`, `LogicKind`, and `SurfaceKind` values.
  - [ ] Ensure `RouteFlowReport.ReportCoverage` and `RouteFlowSummary.ReportCoverage` are identical.
  - [ ] Define `RouteFlowDependencySurface.StableKey` from safe source-scoped identity fields, without scan-specific fact IDs or edge IDs.
  - [ ] Use deterministic empty arrays, `null`, and closed-set placeholder values.
  - [ ] Add model tests for JSON shape, summary rollup, sanitized query paths, and stable ordering.

- [ ] 4. Implement selector handling and entry evidence selection. Requirements: 1, 2, 3, 6.
  - [ ] Parse `--route`.
  - [ ] Parse `--client-call`.
  - [ ] Parse `--from-endpoint`, `--from-webforms-event`, `--from-symbol`, `--from-source`, `--to-surface`, `--surface-name`, `--classification`, `--max-depth`, `--max-paths`, `--max-frontier`, `--max-logic-rows`, `--max-gaps`, `--format`, and `--exit-code`.
  - [ ] Reject non-combined indexes with a clear diagnostic.
  - [ ] Reuse the existing `tracemap paths` selector grammar for overlapping selectors or document any rule-backed deviation before implementation.
  - [ ] Treat `--route`, `--client-call`, `--classification`, `--max-logic-rows`, and `--max-gaps` as documented route-flow additions to the paths selector grammar while reusing `--from-webforms-event` from paths legacy-root selectors.
  - [ ] Define closed entry evidence kinds for `--route`, `--client-call`, `--from-endpoint`, `--from-webforms-event`, `--from-symbol`, `--from-source`, and aligned route/client pairs.
  - [ ] Select `HttpRouteBinding` facts by method and normalized route key/template.
  - [ ] Select `HttpCallDetected` facts by method and normalized route key/template.
  - [ ] Include endpoint-alignment context when client and server evidence match.
  - [ ] Emit `SelectorNoMatch` and partial entry gaps without failing the report.
  - [ ] Add selector tests for route-only, client-call-only, aligned client/server, dynamic URL, optional segments, percent-encoded routes, duplicate normalized route keys across sources, and no-match cases.

- [ ] 5. Build deterministic route-flow traversal. Requirements: 3, 6, 7.
  - [ ] Traverse from selected entry evidence using `combined_dependency_edges` and backing `combined_call_edges`, `combined_object_creations`, `combined_symbol_relationships`, and `combined_parameter_forward_edges`.
  - [ ] Preserve supporting fact IDs, edge IDs, source labels, commit SHAs, rule IDs, evidence tiers, file spans, and extractor identities.
  - [ ] Stop at terminal dependency/data surfaces.
  - [ ] Keep traversal breadth-first, cycle-safe, deterministic, and bounded by depth/path/frontier caps.
  - [ ] Emit truncation gaps when caps are reached.
  - [ ] Add tests for direct paths, multi-hop paths, cycles, missing graph tables, old schemas, row permutation stability, and byte-stable output.

- [ ] 6. Add route-flow readers for fact-symbol and argument-flow details. Requirements: 3, 5, 6.
  - [ ] Read `combined_fact_symbols` for route-flow entry attachment, adjacent logic rows, and symbol-based path context.
  - [ ] Read `combined_argument_flows` for argument-flow rows when available.
  - [ ] Emit `SchemaMissing` or `ExtractorUnavailable` gaps when older combined schemas lack these tables.
  - [ ] Keep these readers read-only and deterministic.
  - [ ] Add tests proving missing `combined_fact_symbols` or `combined_argument_flows` does not silently drop logic rows or argument evidence.

- [ ] 7. Implement conservative interface implementation bridges. Requirements: 4, 6.
  - [ ] Detect call targets that are interface members or interface-declared symbols.
  - [ ] Read compiler-backed implementation relationships from `combined_symbol_relationships`.
  - [ ] Emit `interface-implementation-candidate` rows with supporting relationship evidence.
  - [ ] Stop with `ImplementationCandidateUnavailable` when no relationship evidence exists.
  - [ ] Cap any required interface bridge, including a single compiler-resolved candidate, at `NeedsReviewStaticRouteFlow`.
  - [ ] Block cross-source and cross-language implementation bridges until a future rule explicitly defines that evidence; emit `RuntimeBindingNotProven`.
  - [ ] Add tests proving bridges do not claim runtime DI target proof and cannot produce strong classifications.

- [ ] 8. Add business logic and data surface rows. Requirements: 3, 5, 8.
  - [ ] Attach projection/object-shape evidence to traversed symbols or adjacent path context.
  - [ ] Define adjacency as shared combined symbol ID, same method/member span, shared supporting fact/edge ID, or shared source/target fact ID through a traversed `combined_argument_flows` row; do not attach same-file evidence by default.
  - [ ] Attach query/filter/sort/select/include/mutation and SQL-shape evidence using safe derived metadata.
  - [ ] Attach validation/guard, branch/condition, authorization marker, async boundary, serializer/contract shape, and flow-boundary evidence where facts exist.
  - [ ] Attach repository, DbSet-like, ORM, SQL/persistence, HTTP client, package/config, WCF/service, remoting, legacy-data, storage, queue/event, and dependency-surface rows.
  - [ ] Label adjacent evidence as `path-context` rather than path edges.
  - [ ] Add tests for projection, query shape, repository/data access, business boundary, and path-context wording.

- [ ] 9. Add classifications, coverage propagation, and gaps. Requirements: 2, 4, 6.
  - [ ] Implement `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`, `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and `UnknownAnalysisGap`.
  - [ ] Cap classifications by weakest required evidence tier, reduced coverage, dynamic URLs, fallback facts, implementation candidates, ambiguity, high fan-out, unknown commit SHA, and truncation.
  - [ ] Treat `IdentityVerified = false` or placeholder/missing commit SHA as an identity gap that caps affected rows at `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap` for no-evidence conclusions.
  - [ ] Treat `IdentityVerified` as the combined source projection's derived identity state; if it is false, it wins even when a commit SHA string is present.
  - [ ] Map a report whose only gap is `SelectorNoMatch` to overall `UnknownAnalysisGap`.
  - [ ] Require full route-flow coverage before emitting `StrongStaticRouteFlow` or `NoRouteFlowEvidence`.
  - [ ] Emit gaps for schema missing, extractor unavailable, implementation unavailable, runtime binding not proven, dynamic dispatch boundary, reduced coverage, unknown commit SHA, selector no-match, no route-flow evidence, unknown analysis, truncation, and unsafe value omission.
  - [ ] Ensure clean no-evidence rows require full route-flow coverage.
  - [ ] Add tests proving weak, ambiguous, dynamic, fallback, or reduced evidence cannot produce `StrongStaticRouteFlow`.

- [ ] 10. Add Markdown and JSON writers. Requirements: 7, 8.
  - [ ] Emit required Markdown sections: Summary, Query, Snapshot Sources, Entry Evidence, Static Flow, Business/Data Logic, Dependency Surfaces, Gaps, Limitations.
  - [ ] Emit stable JSON with all required top-level fields.
  - [ ] Sort arrays and metadata deterministically.
  - [ ] Use static-evidence wording and forbid runtime/impact wording.
  - [ ] Support directory output and file output semantics.
  - [ ] Add README command documentation.
  - [ ] Add CLI/output tests for Markdown, JSON, directory output, `--format json` file output, invalid selectors, `--exit-code`, and read-only input handling.

- [ ] 11. Enforce public/private safety. Requirements: 5, 7, 8, 9.
  - [ ] Reuse existing safe path, source label, hash, Markdown escaping, and JSON string-leaf guards.
  - [ ] Omit or hash local absolute paths, raw remotes, private labels, raw SQL, raw URLs, endpoint addresses, connection strings, config values, source snippets, raw diagnostics, and secret-looking values.
  - [ ] Ensure logs do not echo unsafe selector values.
  - [ ] Include redaction rule IDs when unsafe values are omitted or hashed.
  - [ ] Add negative safety tests for Markdown, JSON, logs, source labels, route selectors, SQL/query metadata, logic-row `SafeMetadata`, and dependency-surface `SafeMetadata`.
  - [ ] Add forbidden-wording tests for generated Markdown and JSON.

- [ ] 12. Add public-safe fixtures and validation docs. Requirements: 3, 4, 5, 9.
  - [ ] Add or extend a public sample fixture with aligned client/server route evidence, controller-service-repository flow, implementation candidate, data access, query/projection evidence, and at least one business boundary.
  - [ ] Add tests for missing TypeScript HTTP facts and mixed-language reduced coverage.
  - [ ] Add tests for combined indexes with only one relevant language adapter present.
  - [ ] Add no-mutation tests by hashing the combined database before and after route-flow runs.
  - [ ] Use a logical schema/row-count/content fingerprint or WAL-neutralized hash for no-mutation tests rather than a naive SQLite file hash.
  - [ ] Add filter tests for `--to-surface`, including remoting surface kinds, `--surface-name`, and `--classification`.
  - [ ] Add tests for `--from-endpoint` against endpoint alignment evidence.
  - [ ] Add tests for `--from-webforms-event` against legacy-root path evidence.
  - [ ] Add a test where `--classification` filters every row out and yields `SelectorNoMatch` plus overall `UnknownAnalysisGap`.
  - [ ] Add a test proving `RouteFlowSummary.ExitCodeWouldBeNonZero` matches the observed process exit code when `--exit-code` is used.
  - [ ] Add `--max-logic-rows` truncation tests.
  - [ ] Add `--max-gaps`, `--max-depth`, `--max-paths`, and `--max-frontier` truncation tests.
  - [ ] Add tests for empty combined snapshots, sanitized query paths, no timestamps, percent-encoded selectors, and overall classification to exit-code mapping.
  - [ ] Update `docs/VALIDATION.md` with route-flow validation guidance and smoke expectations when implementation lands.
  - [ ] Avoid private sample names, private repo names, local paths, or raw generated outputs in committed files.

- [ ] 13. Validate implementation. Requirements: 9.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] `./scripts/check-private-paths.sh`.
  - [ ] `git diff --check`.
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md` when shared path/report/adapter behavior changes.

## Deferred Follow-Ups

- Route-flow batch query files for many routes.
- HTML or graph visualization output.
- Persisted derived route-flow rows behind an explicit future option.
- Runtime DI registration evidence if a future deterministic rule proves safe
  static candidates without overclaiming.
- Additional framework-specific route/client families beyond what current
  adapters already emit.
