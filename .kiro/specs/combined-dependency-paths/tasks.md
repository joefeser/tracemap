# Combined Dependency Paths Tasks

## Implementation Tasks

- [ ] Confirm MVP query shape.
  - [ ] Confirm no-selector default runs endpoint-matched-pairs to terminal surfaces.
  - [ ] Confirm `--from-endpoint`, `--from-symbol`, `--from-source`, `--to-surface`, `--surface-name`, `--source-pair`, `--max-depth`, `--max-paths`, and `--max-frontier` are the MVP flags.
  - [ ] Confirm `--to-endpoint`, `--to-symbol`, `--to-source`, and reverse traversal are deferred.
  - [ ] Confirm terminal surfaces are `sql-query`, `http-route`, `http-client`, and `package-config`.
  - [ ] Confirm endpoint start nodes are not terminal HTTP surfaces in default queries.
  - [ ] Document selector matching rules and ambiguity behavior.
  - [ ] Document that `endpoint_matches` is reserved/unused and both report and paths use one shared in-memory matcher.

- [ ] Refactor combined report internals without behavior changes.
  - [ ] Extract combined index validation from the current report implementation.
  - [ ] Extract source inventory and coverage reading.
  - [ ] Extract combined endpoint candidate/matching behavior into `TraceMap.Reporting` internal `CombinedEndpointMatcher`.
  - [ ] Update `CombinedDependencyReporter` to call `CombinedEndpointMatcher`.
  - [ ] Extract dependency surface projection or create a shared internal projector.
  - [ ] Preserve existing `tracemap report` behavior and tests while refactoring.
  - [ ] Prove report output remains byte-stable before adding path code.
  - [ ] Add endpoint-match parity coverage for the shared matcher.

- [ ] Add path report models.
  - [ ] Define query, summary, node, edge, path, gap, inventory, and limitation models.
  - [ ] Define JSON shape version `1.0`.
  - [ ] Ensure no generated timestamp is emitted by default.
  - [ ] Use structured path notes with `code` and `message`.
  - [ ] Pin inventory shape as counts plus path/gap evidence nodes and edges.
  - [ ] Pin confidence as a deterministic derivation of classification.
  - [ ] Pin classification ordering ranks.
  - [ ] Pin nullable fields and required arrays.

- [ ] Add graph reader.
  - [ ] Read `index_sources`.
  - [ ] Read `combined_facts`.
  - [ ] Read `combined_symbols`.
  - [ ] Read `combined_fact_symbols`.
  - [ ] Read `combined_call_edges`.
  - [ ] Read `combined_object_creations`.
  - [ ] Read `combined_symbol_relationships`.
  - [ ] Read `combined_argument_flows`.
  - [ ] Read `combined_parameter_forward_edges`.
  - [ ] Read `combined_dependency_edges` as summary fallback.
  - [ ] Parse `properties_json` defensively.
  - [ ] Open SQLite with `Mode=ReadOnly`.
  - [ ] Emit schema errors naming missing required tables/views.

- [ ] Build evidence graph.
  - [ ] Add endpoint client and route nodes.
  - [ ] Add endpoint match edges from shared endpoint alignment behavior.
  - [ ] Add source-local symbol nodes using deterministic symbol keys.
  - [ ] Preserve `combined_symbol_id` as provenance and prefer it for node identity when available.
  - [ ] Join raw symbol display strings only within the same source index.
  - [ ] Mark duplicate source-local display-name joins as `NeedsReviewPath`.
  - [ ] Add call, create, relationship, argument, and parameter-forward edges.
  - [ ] Add SQL, config, package, and HTTP dependency surface nodes.
  - [ ] Normalize output edge kinds to the canonical lowercase hyphenated vocabulary.
  - [ ] Map surface selector keys to node kinds and inventory keys.
  - [ ] Attach surfaces to symbols using conservative rule-backed evidence.
  - [ ] Emit `UnlinkedSurface` gaps when surfaces cannot be attached.
  - [ ] Ensure the only cross-source edge in MVP is `EndpointMatch`.
  - [ ] Preserve full provenance on nodes and edges.

- [ ] Implement selector resolution.
  - [ ] Resolve endpoint selectors by method and normalized path key.
  - [ ] Resolve symbol selectors against symbol IDs, display names, fully qualified names, source symbols, and target symbols.
  - [ ] Keep symbol selector matches as deterministic per-source candidates unless `--from-source` narrows them.
  - [ ] Resolve source selectors by label.
  - [ ] Resolve surface selectors by terminal kind and optional exact or wildcard name filter.
  - [ ] Resolve source-pair filters by splitting on the first unescaped colon and unescaping `\:` in both labels.
  - [ ] Reject unsupported reverse selectors with clear errors.
  - [ ] Emit `SelectorNoMatch` gaps for empty selector matches.

- [ ] Implement deterministic path search.
  - [ ] Use bounded breadth-first search.
  - [ ] Support default `maxDepth`, `maxPaths`, and `maxFrontier`.
  - [ ] Traverse outbound edges only.
  - [ ] Sort outgoing edges within each BFS frontier by the documented traversal edge rank.
  - [ ] Prevent infinite traversal on cycles.
  - [ ] Record `TruncatedByLimit` gaps for depth/path/cycle/frontier limits.
  - [ ] Sort paths deterministically.
  - [ ] Classify `StrongStaticPath`, `ProbableStaticPath`, `NeedsReviewPath`, `UnknownAnalysisGap`, and `NoPathFound`.
  - [ ] Classify optional endpoint matches and method mismatches below `StrongStaticPath`.
  - [ ] Emit `UnknownAnalysisGap` instead of `NoPathFound` when contributing sources have reduced coverage or known gaps.
  - [ ] Define contributing sources as resolved start-node sources plus outbound-reachable sources within search bounds.

- [ ] Add Markdown writer.
  - [ ] Sections: Summary, Query, Sources, Paths, Path Gaps, Evidence Inventory, Limitations.
  - [ ] Render one evidence trail per path.
  - [ ] Mark source boundary crossings.
  - [ ] Render row caps and truncation notices.
  - [ ] Escape Markdown table/link delimiters.
  - [ ] Use shared safe path rendering for file paths.
  - [ ] Avoid raw snippets, raw SQL, raw URLs, config values, connection strings, and local absolute paths.

- [ ] Add JSON writer.
  - [ ] Emit stable top-level shape.
  - [ ] Include query metadata and algorithm/version identifiers.
  - [ ] Include paths with full node and edge evidence.
  - [ ] Include gaps and inventory.
  - [ ] Exclude `generatedAt`, `timestamp`, and raw input property bags.
  - [ ] Use `null` and empty arrays consistently.

- [ ] Wire CLI.
  - [ ] Add `tracemap paths --help`.
  - [ ] Parse path query flags.
  - [ ] Validate selector combinations.
  - [ ] Reject deferred selectors with clear errors.
  - [ ] Treat missing-extension output paths as directories.
  - [ ] Print useful completion summary.

- [ ] Add tests.
  - [ ] Non-combined index rejection.
  - [ ] Markdown and JSON output.
  - [ ] Byte-stable repeated output.
  - [ ] Endpoint-to-SQL path.
  - [ ] Endpoint-to-config path.
  - [ ] Endpoint-to-package path.
  - [ ] Symbol-to-surface path.
  - [ ] Source pair filter.
  - [ ] Source pair labels containing escaped colons split on the first unescaped colon.
  - [ ] Report/paths endpoint-match parity.
  - [ ] Symbol-key determinism and same-display-name collision behavior.
  - [ ] Within-source symbol joins and cross-source boundary enforcement.
  - [ ] Read-only DB byte unchanged after a run.
  - [ ] Precise-table-vs-view fallback behavior.
  - [ ] Schema error naming for missing required tables/views.
  - [ ] `--surface-name` exact and wildcard behavior.
  - [ ] `--surface-name` exact matches sort before wildcard matches.
  - [ ] Edge-kind rejection for `--to-surface calls/creates/inherits/implements/overrides/argument-passed/parameter-forward/fact-attached-to-symbol/surface-evidence`.
  - [ ] Canonical edge-kind normalization for schema values and relationship kinds.
  - [ ] Endpoint start nodes do not satisfy `http-route` or `http-client` terminal surfaces in default queries.
  - [ ] Optional endpoint matches do not classify as `StrongStaticPath`.
  - [ ] Multiple paths deterministic ordering.
  - [ ] Relationship edge down-ranking through documented BFS frontier edge order.
  - [ ] Cycle/frontier handling and `TruncatedByLimit`.
  - [ ] Frontier cap gaps identify `frontier` as the truncation reason.
  - [ ] `SelectorNoMatch`.
  - [ ] `NoPathFound` under full coverage.
  - [ ] Reduced coverage no-path caveat.
  - [ ] Partial reduced-coverage boundary where unrelated reduced sources do not affect no-path classification.
  - [ ] Multi-candidate selector top-N and candidate count.
  - [ ] Markdown escaping for pipes, line endings, brackets, and parentheses.
  - [ ] Unlinked surface gap.
  - [ ] No raw SQL, raw URL, config value, snippet, or local absolute path output.

- [ ] Update docs.
  - [ ] README quickstart for `combine -> report -> paths`.
  - [ ] `docs/ACCEPTANCE.md` path query acceptance.
  - [ ] `docs/VALIDATION.md` smoke command.
  - [ ] `docs/LANGUAGE_ADAPTER_CONTRACT.md` if path search reveals adapter contract gaps.
  - [ ] Rule catalog only if new derived rule IDs are formalized.

- [ ] Validate.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- HTML graph viewer.
- Persisted derived path rows behind explicit `--write-derived`.
- Persisted endpoint match ownership and optional `endpoint_matches` population.
- Snapshot path diffing.
- Batch query files.
- Query language for arbitrary graph filters.
- Reverse traversal and `--to-*` selectors.
- Same-file or line-proximity surface attachment.
- Runtime DI/framework binding evidence.
- Deeper SQL parser-backed surface attachment.
- Cross-repo/package dependency ownership inference.
