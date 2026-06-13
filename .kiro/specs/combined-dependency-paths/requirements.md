# Combined Dependency Paths Requirements

## Introduction

TraceMap can combine multiple language indexes and produce a combined dependency report. The next layer is a deterministic path query command over the combined database.

The goal is to answer review questions like:

- Which discovered client calls can be connected to backend route evidence?
- Once a backend route is found, what static call, creation, parameter-forwarding, SQL, config, package, or external dependency evidence sits behind it?
- Which dependency surfaces are reachable from a known symbol or endpoint according to static evidence?
- Where does the evidence stop because a language adapter, framework pattern, dynamic dispatch, or reduced scan coverage prevents a credible path?

This is still static analysis. A path means TraceMap found connected static evidence rows. It does not prove runtime traffic, runtime dependency injection bindings, dynamic dispatch targets, reflection targets, branch feasibility, auth behavior, deployment routing, database schema existence, SQL execution, or actual value flow.

## Current State

- `tracemap combine` writes combined source, fact, symbol, relationship, call, object creation, argument flow, alias, parameter-forwarding, and dependency-edge data.
- `tracemap report` summarizes endpoint alignment, dependency surfaces, dependency edges, known gaps, and limitations over a combined index.
- There is not yet a command that traverses the combined evidence graph from an endpoint, symbol, source, or surface to downstream dependencies.
- `combined_dependency_edges` gives a useful summary view, but endpoint-to-symbol and symbol-to-surface joins need deterministic linking rules.

## MVP Scope Decisions

- Add a new command: `tracemap paths --index <combined.sqlite> --out <path>`.
- MVP input is a combined SQLite database produced by `tracemap combine`.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP is read-only. It does not write derived path rows back to the combined database.
- MVP builds an in-memory graph from existing combined tables and facts.
- MVP supports bounded forward path search from endpoints, symbols, sources, and terminal dependency surfaces.
- MVP uses one shared endpoint matcher with `tracemap report`; it must not add a third endpoint alignment implementation. The existing `endpoint_matches` table is reserved and remains unused/read-only in this slice.
- MVP does not support reverse traversal, `--to-endpoint`, or `--to-source`.
- MVP treats `EndpointMatch` as the only cross-source graph edge. Symbol-name joins are source-local only.
- MVP favors high-confidence paths and explicitly emits `PathGap` rows when evidence is missing.
- MVP does not require scanner changes, but it may add small helper APIs to reuse combined report readers/matchers.
- MVP does not implement a UI, HTML graph viewer, graph database, LLM ranking, embeddings, runtime tracing, or whole-program taint analysis.

## Example Workflow

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index /tmp/client/index.sqlite --label client \
  --index /tmp/api/index.sqlite --label api \
  --out /tmp/combined.sqlite

dotnet run --project src/dotnet/TraceMap.Cli -- paths \
  --index /tmp/combined.sqlite \
  --from-endpoint "GET /api/orders/{}" \
  --out /tmp/order-paths
```

Expected directory artifacts:

```text
paths-report.md
paths-report.json
```

## Requirements

### Requirement 1: Path Query Command

**User Story:** As a reviewer, I want a `tracemap paths` command so that I can follow static dependency evidence across combined indexes without writing graph SQL by hand.

#### Acceptance Criteria

1. WHEN the user runs `tracemap paths --index <combined.sqlite> --out <path>` THEN TraceMap SHALL read the combined index and emit a path report.
2. WHEN `--format json` is provided with a file output THEN TraceMap SHALL emit a machine-readable JSON path report.
3. WHEN `--out` is an existing directory or a path without an extension THEN TraceMap SHALL emit both `paths-report.md` and `paths-report.json`.
4. WHEN `--out` is a file path THEN TraceMap SHALL emit only the requested format, defaulting to Markdown.
5. WHEN the input is not a combined index THEN the command SHALL fail with a clear message and SHALL NOT silently treat a single-language index as path-queryable.
6. WHEN required combined tables or views are missing THEN the command SHALL fail with a schema error naming the missing table or view.
7. WHEN no selector is provided THEN the command SHALL run a conservative default query that starts from discovered endpoint matches and shows paths to dependency surfaces.
8. WHEN a selector is provided THEN the command SHALL constrain traversal to the requested starting evidence and terminal surface filters.
9. WHEN the command completes THEN the CLI SHALL print output path, source count, graph node count, graph edge count, path count, gap count, and report coverage.
10. WHEN the command runs THEN it SHALL open the combined database read-only and SHALL NOT mutate source indexes or derived tables.

### Requirement 2: Query Selectors

**User Story:** As an investigator, I want simple selectors so that I can ask targeted questions without knowing internal IDs.

#### Acceptance Criteria

1. WHEN `--from-endpoint "<METHOD> <PATH_KEY>"` is provided THEN TraceMap SHALL start from matching HTTP client calls and server route facts using normalized path keys.
2. WHEN `--from-symbol <symbol>` is provided THEN TraceMap SHALL start from matching symbol IDs, display names, fully qualified names, source symbols, or target symbols using the source-local symbol key rules.
3. WHEN `--from-source <label>` is provided THEN TraceMap SHALL start from facts and edges belonging to matching source labels.
4. WHEN `--to-surface <kind>` is provided THEN TraceMap SHALL find paths ending at terminal dependency surfaces of that kind, limited to `sql-query`, `http-route`, `http-client`, `package-config`, or `external`.
5. WHEN `--surface-name <text>` is provided with `--to-surface` THEN TraceMap SHALL filter surface display names, package names, config keys, table names, or normalized path keys with case-insensitive exact matching by default; `*` MAY be used as a leading/trailing wildcard for prefix, suffix, or contains matching.
6. WHEN `--source-pair <client>:<server>` is provided THEN TraceMap SHALL constrain endpoint alignment starts to that source pair; the parser SHALL split on the last colon so labels can contain colons.
7. WHEN `--to-endpoint`, `--to-source`, or `--to-symbol` is requested in v1 THEN the command SHALL fail with a clear unsupported-selector message rather than attempting reverse traversal.
8. WHEN `call`, `create`, `relationship`, or `parameter-forward` terms are requested as `--to-surface` values THEN the command SHALL reject them as edge kinds, not terminal surfaces.
9. WHEN symbol selectors match by display name THEN matching SHALL be scoped to a single source index unless the user also provides an explicit `--from-source` filter.
10. WHEN multiple selectors are provided THEN TraceMap SHALL combine them as filters, not as separate independent reports, unless a future `--batch` mode is added.
11. WHEN a selector matches multiple evidence nodes THEN TraceMap SHALL include deterministic top-N results and state how many candidates were matched.
12. WHEN a selector matches nothing THEN TraceMap SHALL emit a valid report with zero paths and a `SelectorNoMatch` gap.

### Requirement 3: Evidence Graph Model

**User Story:** As a maintainer, I want path search to use a clear graph model so that results are deterministic and explainable.

#### Acceptance Criteria

1. WHEN the graph is built THEN every graph node SHALL have a stable node ID, node kind, source index ID, source label, optional symbol ID, optional combined fact ID, optional edge ID, display name, evidence tier, rule ID, file path, and line span where available.
2. WHEN endpoint facts are loaded THEN client calls and server routes SHALL become `Endpoint` nodes.
3. WHEN endpoint alignment finds matches THEN matched client and server endpoint nodes SHALL be connected by derived `EndpointMatch` edges with classification and static match quality.
4. WHEN symbol nodes are created THEN TraceMap SHALL use a deterministic source-local symbol key: `sourceIndexId + normalizedDisplayName`, with `combined_symbol_id` retained as provenance and preferred for node identity when available.
5. WHEN a raw edge table references symbols by display string THEN TraceMap SHALL join it only to symbol nodes in the same `sourceIndexId`; if more than one symbol in the source has the same normalized display name, the graph SHALL keep all provenance and classify resulting paths as `NeedsReviewPath`.
6. WHEN `combined_dependency_edges` rows are loaded THEN each row SHALL become an edge between source-local symbol nodes where deterministic symbol keys can be resolved.
7. WHEN `combined_call_edges`, `combined_object_creations`, `combined_symbol_relationships`, and `combined_parameter_forward_edges` provide more precise source/target IDs than the summary view THEN the graph builder SHOULD prefer the precise table while keeping the summary view as fallback.
8. WHEN the graph crosses from one source index to another THEN it SHALL do so only through a derived `EndpointMatch` edge in MVP.
9. WHEN dependency surface facts are loaded THEN SQL, config, package, HTTP, and external dependency evidence SHALL become terminal `Surface` nodes.
10. WHEN a fact can be attached to a symbol by `combined_fact_symbols`, source/target symbol fields, method symbol fields, or containing type metadata THEN the graph SHALL connect the symbol node to the surface node with a rule-backed `SurfaceEvidence` edge.
11. WHEN no credible symbol-to-surface link exists THEN the surface SHALL remain discoverable by source and file path, but path reports SHALL mark the missing link as a gap rather than inventing a path.
12. WHEN multiple facts describe the same logical node THEN the graph MAY deduplicate by stable ID only if all provenance is retained in evidence rows.
13. WHEN a path uses a derived edge THEN the edge SHALL name the derived rule or algorithm ID and all supporting source fact IDs.

### Requirement 4: Path Search Semantics

**User Story:** As a reviewer, I want paths to be useful but bounded so that reports stay readable and reproducible.

#### Acceptance Criteria

1. WHEN path search runs THEN it SHALL use deterministic breadth-first search or another documented deterministic algorithm.
2. WHEN multiple paths are available THEN TraceMap SHALL sort by confidence, path length, source label, display name, file path, line, and stable ID.
3. WHEN `--max-depth <n>` is omitted THEN the command SHALL default to a conservative depth that can connect endpoint-to-controller-to-service-to-repository-to-surface paths.
4. WHEN `--max-paths <n>` is omitted THEN the command SHALL default to a bounded count such as 100 paths.
5. WHEN a graph has cycles THEN the search SHALL avoid infinite traversal and SHALL record whether paths were truncated by cycle, depth, path, or frontier-size limits.
6. WHEN a route endpoint is matched to a server method symbol THEN traversal SHALL continue through static call edges, object creations, parameter-forwarding edges, and symbol relationships where evidence exists.
7. WHEN a client endpoint is matched to a server route through endpoint alignment THEN traversal MAY cross source indexes only through that derived endpoint edge.
8. WHEN no endpoint alignment exists and any source contributing nodes to the queried segment has reduced coverage, known gaps, failed/partial build status, unknown commit SHA, or analysis gaps THEN the command SHALL emit `UnknownAnalysisGap` rather than `NoPathFound`.
9. WHEN selectors match but no path exists and every source contributing nodes to the queried segment has full credible coverage THEN the command SHALL emit `NoPathFound`.
10. WHEN reduced coverage exists THEN path absence SHALL be labeled coverage-relative and SHALL NOT be described as proof that no dependency exists.
11. WHEN a path crosses languages or source indexes THEN every crossing SHALL include source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, and file spans.
12. WHEN path search reaches a terminal surface THEN the path SHALL stop unless the user opts into deeper expansion in a future command.

### Requirement 5: Path Classifications

**User Story:** As a reviewer, I want path results classified so that I can separate strong evidence from review items.

#### Acceptance Criteria

1. WHEN every non-endpoint hop is Tier1 semantic and any endpoint hop is an exact static method/path match THEN the path SHALL be classified as `StrongStaticPath`.
2. WHEN a path includes strong structural evidence such as route syntax, SQL shape, package/config facts, or object creation without full semantic resolution THEN the path SHALL be classified as `ProbableStaticPath`.
3. WHEN a path depends on syntax/textual symbols, name-only matching, unresolved receivers, or fallback symbol links THEN the path SHALL be classified as `NeedsReviewPath`.
4. WHEN analysis gaps prevent a credible conclusion THEN the path SHALL be classified as `UnknownAnalysisGap`.
5. WHEN selectors match but no path exists under full coverage THEN the result SHALL be classified as `NoPathFound`.
6. WHEN selectors match nothing THEN the result SHALL be classified as `SelectorNoMatch`.
7. WHEN path search stops because of depth, row, cycle, or result caps THEN the report SHALL include `TruncatedByLimit` gaps.
8. WHEN dynamic URL, reflection, dynamic dispatch, DI, serializer, or runtime route gaps are visible THEN the report SHALL include `NeedsReview` or `UnknownAnalysisGap` rows tied to the supporting facts.

### Requirement 6: Markdown Report

**User Story:** As a human reviewer, I want a readable path report so that I can understand the evidence trail quickly.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN it SHALL include sections in this order: Summary, Query, Sources, Paths, Path Gaps, Evidence Inventory, Limitations.
2. WHEN paths exist THEN each path SHALL render as a numbered evidence trail with one row per hop.
3. WHEN a hop is rendered THEN it SHALL show node kind, display name, source label, evidence tier, rule ID, file span, and edge kind.
4. WHEN a path crosses a source boundary THEN Markdown SHALL visibly mark the source transition.
5. WHEN SQL or config surfaces are rendered THEN Markdown SHALL show hash/key/shape metadata only and SHALL NOT show raw SQL, raw config values, raw snippets, or local absolute paths.
6. WHEN no paths exist THEN Markdown SHALL explain whether the outcome is `NoPathFound`, `SelectorNoMatch`, or `UnknownAnalysisGap`.
7. WHEN more than 100 paths or more than 200 inventory rows exist THEN Markdown SHALL cap rows deterministically and emit truncation notices.
8. WHEN coverage is reduced THEN Markdown SHALL place coverage caveats near the summary and near no-path conclusions.

### Requirement 7: JSON Report Contract

**User Story:** As an automation author, I want a stable JSON path report so that scripts can consume path evidence.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include top-level `version`, `reportCoverage`, `coverageWarnings`, `query`, `sources`, `summary`, `paths`, `gaps`, `inventory`, and `limitations`.
2. WHEN query metadata is emitted THEN it SHALL include normalized selectors, max depth, max paths, and algorithm/version identifiers.
3. WHEN a path is emitted THEN it SHALL include `pathId`, `classification`, `confidence`, `length`, `startNodeId`, `endNodeId`, `nodes`, `edges`, `supportingFactIds`, `supportingEdgeIds`, and structured `notes`.
4. WHEN a node is emitted THEN it SHALL include `nodeId`, `nodeKind`, `displayName`, `sourceIndexId`, `sourceLabel`, `scanId`, `commitSha`, `symbolId`, `combinedFactId`, `ruleId`, `evidenceTier`, `filePath`, `startLine`, `endLine`, and surface metadata where applicable.
5. WHEN an edge is emitted THEN it SHALL include `edgeId`, `edgeKind`, `fromNodeId`, `toNodeId`, `classification`, `ruleId`, `evidenceTier`, `supportingFactIds`, `supportingCombinedEdgeIds`, `filePath`, `startLine`, and `endLine`.
6. WHEN data is missing THEN JSON SHALL use `null` or empty arrays consistently rather than omitting required fields.
7. WHEN Markdown caps rows THEN JSON SHALL still include all paths up to `--max-paths` and all gaps unless a JSON cap is explicitly added.
8. WHEN inventory is emitted THEN it SHALL include deterministic counts by node kind, edge kind, source label, surface kind, and gap kind, plus only nodes/edges participating in returned paths and gap evidence.
9. WHEN the JSON shape changes in a future version THEN the top-level `version` SHALL change.

### Requirement 8: Evidence Boundaries and Safety

**User Story:** As a maintainer, I want the command to stay honest and safe so that public reports do not overclaim or leak private data.

#### Acceptance Criteria

1. WHEN paths are reported THEN the report SHALL say paths are static evidence trails and not runtime execution traces.
2. WHEN endpoint hops are reported THEN the report SHALL say endpoint matches do not prove runtime traffic, auth, proxies, deployment base paths, CORS, or reachability.
3. WHEN call/object/relationship hops are reported THEN the report SHALL say they do not prove dynamic dispatch, runtime DI, reflection, branch feasibility, collection contents, or serializer contract behavior.
4. WHEN parameter-forwarding hops are reported THEN the report SHALL say they are direct static argument evidence, not full taint analysis or mutation tracking.
5. WHEN SQL/query hops are reported THEN the report SHALL say they do not prove runtime execution, database schema existence, generated SQL equivalence, dialect validity, or branch feasibility.
6. WHEN raw source snippets, raw SQL, raw URLs, config values, connection strings, or local absolute paths are present in input properties THEN the command SHALL not render them by default.
7. WHEN dynamic reasons are rendered THEN they SHALL be closed-set reason codes or hashes, not raw source expressions.
8. WHEN reduced coverage exists THEN the report SHALL avoid claiming complete dependency coverage.
9. WHEN file paths are rendered THEN the command SHALL use a shared safe path helper that rejects or hashes local absolute paths.
10. WHEN Markdown cells are rendered THEN the command SHALL escape pipe characters, line endings, and Markdown link delimiters that could create accidental links.

### Requirement 9: Tests and Validation

**User Story:** As a maintainer, I want focused tests for path search so that future language adapters can use the same combined dependency contract.

#### Acceptance Criteria

1. WHEN a combined index contains a client call, matched server route, call edge, and SQL surface THEN tests SHALL prove a path from endpoint to SQL surface appears.
2. WHEN a combined index contains package/config surfaces attached to symbols THEN tests SHALL prove paths can end at those surfaces.
3. WHEN a combined index contains multiple possible paths THEN tests SHALL prove deterministic ordering.
4. WHEN a graph contains cycles THEN tests SHALL prove search terminates and records cycle/depth limits.
5. WHEN selectors match nothing THEN tests SHALL prove a `SelectorNoMatch` gap appears.
6. WHEN selectors match but no path exists under full coverage THEN tests SHALL prove `NoPathFound`.
7. WHEN reduced coverage or analysis gaps exist THEN tests SHALL prove no-path conclusions are coverage-relative.
8. WHEN dynamic URL or unresolved symbol evidence appears THEN tests SHALL prove `NeedsReviewPath` or `UnknownAnalysisGap` rows include evidence.
9. WHEN Markdown output exceeds row caps THEN tests SHALL prove deterministic truncation notices and full JSON path rows up to `--max-paths`.
10. WHEN JSON is emitted THEN tests SHALL prove required top-level arrays and nullable fields are present.
11. WHEN output is generated twice from the same input THEN tests SHALL prove byte-stable Markdown and JSON.
12. WHEN endpoint matches are computed THEN tests SHALL prove `tracemap report` and `tracemap paths` use identical shared endpoint-match results.
13. WHEN two sources contain the same symbol display name THEN tests SHALL prove symbol nodes are not merged across sources and ordering is deterministic.
14. WHEN a graph contains symbol-name edges across sources THEN tests SHALL prove those edges are not joined unless bridged by `EndpointMatch`.
15. WHEN the command runs THEN tests SHALL prove the combined DB file is byte-unchanged.
16. WHEN precise edge tables are missing optional rows but the dependency view has fallback rows THEN tests SHALL prove documented fallback behavior.
17. WHEN `--surface-name` uses exact and wildcard matching THEN tests SHALL prove the documented matching semantics.
18. WHEN a large graph exceeds the frontier cap THEN tests SHALL prove the command terminates and emits `TruncatedByLimit`.
19. WHEN files are checked in THEN `dotnet build src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass.

## Future Work

- HTML graph viewer with collapsible paths.
- Persisted derived path rows behind an explicit `--write-derived` mode.
- Populating or reading persisted `endpoint_matches` after the schema and ownership model are finalized.
- Reverse traversal and `--to-endpoint`, `--to-source`, or `--to-symbol` selectors.
- Same-file or line-proximity surface attachment.
- Snapshot path diffing between two combined indexes.
- More advanced SQL parsing and symbol-to-SQL attachment.
- Framework-specific route-to-handler and DI binding evidence.
- Batch query files for CI dependency audits.
- Query language for arbitrary graph filters.
