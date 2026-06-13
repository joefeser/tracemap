# Combined Dependency Paths Tasks

## Implementation Tasks

- [ ] Confirm MVP query shape.
  - [ ] Decide whether no-selector default runs endpoint-to-surface search or requires an explicit selector.
  - [ ] Confirm `--from-endpoint`, `--from-symbol`, `--from-source`, `--to-surface`, `--to-symbol`, `--to-source`, `--source-pair`, `--max-depth`, and `--max-paths` are the MVP flags.
  - [ ] Document selector matching rules and ambiguity behavior.

- [ ] Extract reusable combined reporting helpers.
  - [ ] Extract combined index validation from the current report implementation.
  - [ ] Extract source inventory and coverage reading.
  - [ ] Extract endpoint candidate/matching behavior so report and paths agree.
  - [ ] Extract dependency surface projection or create a shared internal projector.
  - [ ] Preserve existing `tracemap report` behavior and tests while refactoring.

- [ ] Add path report models.
  - [ ] Define query, summary, node, edge, path, gap, inventory, and limitation models.
  - [ ] Define JSON shape version `1.0`.
  - [ ] Ensure no generated timestamp is emitted by default.
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

- [ ] Build evidence graph.
  - [ ] Add endpoint client and route nodes.
  - [ ] Add endpoint match edges from shared endpoint alignment behavior.
  - [ ] Add symbol nodes.
  - [ ] Add call, create, relationship, argument, and parameter-forward edges.
  - [ ] Add SQL, config, package, HTTP, and external dependency surface nodes.
  - [ ] Attach surfaces to symbols using conservative rule-backed evidence.
  - [ ] Emit `UnlinkedSurface` gaps when surfaces cannot be attached.
  - [ ] Preserve full provenance on nodes and edges.

- [ ] Implement selector resolution.
  - [ ] Resolve endpoint selectors by method and normalized path key.
  - [ ] Resolve symbol selectors against symbol IDs, display names, fully qualified names, source symbols, and target symbols.
  - [ ] Resolve source selectors by label.
  - [ ] Resolve surface selectors by kind and optional name filter.
  - [ ] Resolve source-pair filters for endpoint alignment starts.
  - [ ] Emit `SelectorNoMatch` gaps for empty selector matches.

- [ ] Implement deterministic path search.
  - [ ] Use bounded breadth-first search.
  - [ ] Support default `maxDepth` and `maxPaths`.
  - [ ] Prevent infinite traversal on cycles.
  - [ ] Record `TruncatedByLimit` gaps for depth/path/cycle limits.
  - [ ] Sort paths deterministically.
  - [ ] Classify `DefiniteStaticPath`, `ProbableStaticPath`, `NeedsReviewPath`, `UnknownAnalysisGap`, and `NoPathFound`.

- [ ] Add Markdown writer.
  - [ ] Sections: Summary, Query, Sources, Paths, Path Gaps, Evidence Inventory, Limitations.
  - [ ] Render one evidence trail per path.
  - [ ] Mark source boundary crossings.
  - [ ] Render row caps and truncation notices.
  - [ ] Avoid raw snippets, raw SQL, raw URLs, config values, connection strings, and local absolute paths.

- [ ] Add JSON writer.
  - [ ] Emit stable top-level shape.
  - [ ] Include query metadata and algorithm/version identifiers.
  - [ ] Include paths with full node and edge evidence.
  - [ ] Include gaps and inventory.
  - [ ] Use `null` and empty arrays consistently.

- [ ] Wire CLI.
  - [ ] Add `tracemap paths --help`.
  - [ ] Parse path query flags.
  - [ ] Validate selector combinations.
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
  - [ ] Multiple paths deterministic ordering.
  - [ ] Cycle handling and `TruncatedByLimit`.
  - [ ] `SelectorNoMatch`.
  - [ ] `NoPathFound` under full coverage.
  - [ ] Reduced coverage no-path caveat.
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
- Snapshot path diffing.
- Batch query files.
- Query language for arbitrary graph filters.
- Runtime DI/framework binding evidence.
- Deeper SQL parser-backed surface attachment.
- Cross-repo/package dependency ownership inference.
