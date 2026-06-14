# Combined Dependency Paths Tasks

## Implementation Tasks

- [x] Confirm MVP query shape.
  - [x] Confirm no-selector default runs endpoint-matched-pairs to terminal surfaces.
  - [x] Confirm `--from-endpoint`, `--from-symbol`, `--from-source`, `--to-surface`, `--surface-name`, `--source-pair`, `--max-depth`, `--max-paths`, and `--max-frontier` are the MVP flags.
  - [x] Confirm `--to-endpoint`, `--to-symbol`, `--to-source`, and reverse traversal are deferred.
  - [x] Confirm terminal surfaces are `sql-query`, `http-route`, `http-client`, and `package-config`.
  - [x] Confirm endpoint start nodes are not terminal HTTP surfaces in default queries.
  - [x] Document selector matching rules and ambiguity behavior.
  - [x] Document that `endpoint_matches` is reserved/unused and both report and paths use one shared in-memory matcher.

- [x] Refactor combined report internals without behavior changes.
  - [x] Extract combined index validation from the current report implementation.
  - [x] Extract source inventory and coverage reading.
  - [x] Extract combined endpoint candidate/matching behavior into `TraceMap.Reporting` internal `CombinedEndpointMatcher`.
  - [x] Update `CombinedDependencyReporter` to call `CombinedEndpointMatcher`.
  - [x] Extract dependency surface projection or create a shared internal projector.
  - [x] Preserve existing `tracemap report` behavior and tests while refactoring.
  - [x] Prove report output remains byte-stable before adding path code.
  - [x] Add endpoint-match parity coverage for the shared matcher.

- [x] Add path report models.
  - [x] Define query, summary, node, edge, path, gap, inventory, and limitation models.
  - [x] Define JSON shape version `1.0`.
  - [x] Ensure no generated timestamp is emitted by default.
  - [x] Use structured path notes with `code` and `message`.
  - [x] Pin inventory shape as counts plus path/gap evidence nodes and edges.
  - [x] Pin confidence as a deterministic derivation of classification.
  - [x] Pin classification ordering ranks.
  - [x] Pin nullable fields and required arrays.

- [x] Add graph reader.
  - [x] Read `index_sources`.
  - [x] Read `combined_facts`.
  - [x] Read `combined_symbols`.
  - [x] Read `combined_fact_symbols`.
  - [x] Read `combined_call_edges`.
  - [x] Read `combined_object_creations`.
  - [x] Read `combined_symbol_relationships`.
  - [x] Read `combined_argument_flows`.
  - [x] Read `combined_parameter_forward_edges`.
  - [x] Read `combined_dependency_edges` as summary fallback.
  - [x] Parse `properties_json` defensively.
  - [x] Open SQLite with `Mode=ReadOnly`.
  - [x] Emit schema errors naming missing required tables/views.

- [x] Build evidence graph.
  - [x] Add endpoint client and route nodes.
  - [x] Add endpoint match edges from shared endpoint alignment behavior.
  - [x] Add source-local symbol nodes using deterministic symbol keys.
  - [x] Preserve `combined_symbol_id` as provenance and prefer it for node identity when available.
  - [x] Join raw symbol display strings only within the same source index.
  - [x] Mark duplicate source-local display-name joins as `NeedsReviewPath`.
  - [x] Add call, create, relationship, argument, and parameter-forward edges.
  - [x] Add SQL, config, package, and HTTP dependency surface nodes.
  - [x] Normalize output edge kinds to the canonical lowercase hyphenated vocabulary.
  - [x] Map surface selector keys to node kinds and inventory keys.
  - [x] Attach surfaces to symbols using conservative rule-backed evidence.
  - [x] Emit `UnlinkedSurface` gaps when surfaces cannot be attached.
  - [x] Ensure the only cross-source edge in MVP is `EndpointMatch`.
  - [x] Preserve full provenance on nodes and edges.

- [x] Implement selector resolution.
  - [x] Resolve endpoint selectors by method and normalized path key.
  - [x] Resolve symbol selectors against symbol IDs, display names, fully qualified names, source symbols, and target symbols.
  - [x] Keep symbol selector matches as deterministic per-source candidates unless `--from-source` narrows them.
  - [x] Resolve source selectors by label.
  - [x] Resolve surface selectors by terminal kind and optional exact or wildcard name filter.
  - [x] Resolve source-pair filters by splitting on the first unescaped colon and unescaping `\:` in both labels.
  - [x] Reject unsupported reverse selectors with clear errors.
  - [x] Emit `SelectorNoMatch` gaps for empty selector matches.

- [x] Implement deterministic path search.
  - [x] Use bounded breadth-first search.
  - [x] Support default `maxDepth`, `maxPaths`, and `maxFrontier`.
  - [x] Traverse outbound edges only.
  - [x] Sort outgoing edges within each BFS frontier by the documented traversal edge rank.
  - [x] Prevent infinite traversal on cycles.
  - [x] Record `TruncatedByLimit` gaps for depth/path/cycle/frontier limits.
  - [x] Sort paths deterministically.
  - [x] Classify `StrongStaticPath`, `ProbableStaticPath`, `NeedsReviewPath`, `UnknownAnalysisGap`, and `NoPathFound`.
  - [x] Classify optional endpoint matches and method mismatches below `StrongStaticPath`.
  - [x] Emit `UnknownAnalysisGap` instead of `NoPathFound` when contributing sources have reduced coverage or known gaps.
  - [x] Define contributing sources as resolved start-node sources plus outbound-reachable sources within search bounds.

- [x] Add Markdown writer.
  - [x] Sections: Summary, Query, Sources, Paths, Path Gaps, Evidence Inventory, Limitations.
  - [x] Render one evidence trail per path.
  - [x] Mark source boundary crossings.
  - [x] Render row caps and truncation notices.
  - [x] Escape Markdown table/link delimiters.
  - [x] Use shared safe path rendering for file paths.
  - [x] Avoid raw snippets, raw SQL, raw URLs, config values, connection strings, and local absolute paths.

- [x] Add JSON writer.
  - [x] Emit stable top-level shape.
  - [x] Include query metadata and algorithm/version identifiers.
  - [x] Include paths with full node and edge evidence.
  - [x] Include gaps and inventory.
  - [x] Exclude `generatedAt`, `timestamp`, and raw input property bags.
  - [x] Use `null` and empty arrays consistently.

- [x] Wire CLI.
  - [x] Add `tracemap paths --help`.
  - [x] Parse path query flags.
  - [x] Validate selector combinations.
  - [x] Reject deferred selectors with clear errors.
  - [x] Treat missing-extension output paths as directories.
  - [x] Print useful completion summary.

- [x] Add tests.
  - [x] Non-combined index rejection.
  - [x] Markdown and JSON output.
  - [x] Byte-stable repeated output.
  - [x] Endpoint-to-SQL path.
  - [x] Endpoint-to-config path.
  - [x] Endpoint-to-package path.
  - [x] Symbol-to-surface path.
  - [x] Source pair filter.
  - [x] Source pair labels containing escaped colons split on the first unescaped colon.
  - [x] Report/paths endpoint-match parity.
  - [x] Symbol-key determinism and same-display-name collision behavior.
  - [x] Within-source symbol joins and cross-source boundary enforcement.
  - [x] Read-only DB byte unchanged after a run.
  - [x] Precise-table-vs-view fallback behavior.
  - [x] Schema error naming for missing required tables/views.
  - [x] `--surface-name` exact and wildcard behavior.
  - [x] `--surface-name` exact matches sort before wildcard matches.
  - [x] Edge-kind rejection for `--to-surface calls/creates/inherits/implements/overrides/argument-passed/parameter-forward/fact-attached-to-symbol/surface-evidence`.
  - [x] Canonical edge-kind normalization for schema values and relationship kinds.
  - [x] Endpoint start nodes do not satisfy `http-route` or `http-client` terminal surfaces in default queries.
  - [x] Optional endpoint matches do not classify as `StrongStaticPath`.
  - [x] Multiple paths deterministic ordering.
  - [x] Relationship edge down-ranking through documented BFS frontier edge order.
  - [x] Cycle/frontier handling and `TruncatedByLimit`.
  - [x] Frontier cap gaps identify `frontier` as the truncation reason.
  - [x] `SelectorNoMatch`.
  - [x] `NoPathFound` under full coverage.
  - [x] Reduced coverage no-path caveat.
  - [x] Partial reduced-coverage boundary where unrelated reduced sources do not affect no-path classification.
  - [x] Multi-candidate selector top-N and candidate count.
  - [x] Markdown escaping for pipes, line endings, brackets, and parentheses.
  - [x] Unlinked surface gap.
  - [x] No raw SQL, raw URL, config value, snippet, or local absolute path output.

- [x] Update docs.
  - [x] README quickstart for `combine -> report -> paths`.
  - [x] `docs/ACCEPTANCE.md` path query acceptance.
  - [x] `docs/VALIDATION.md` smoke command.
  - [x] `docs/LANGUAGE_ADAPTER_CONTRACT.md` if path search reveals adapter contract gaps.
  - [x] Rule catalog only if new derived rule IDs are formalized.

- [x] Validate.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

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
