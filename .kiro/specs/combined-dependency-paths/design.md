# Combined Dependency Paths Design

## Overview

Add a deterministic path query layer over combined TraceMap indexes.

The workflow becomes:

```bash
tracemap combine \
  --index client/index.sqlite --label client \
  --index api/index.sqlite --label api \
  --out combined.sqlite

tracemap paths \
  --index combined.sqlite \
  --from-endpoint "GET /api/orders/{}" \
  --to-surface sql-query \
  --out paths-report
```

The command reads the combined database, builds an in-memory evidence graph, traverses bounded paths, and writes Markdown plus optional JSON. It is a query and explanation layer over existing facts, edges, and endpoint alignment. It does not add runtime tracing, LLM ranking, embeddings, or new scan-time inference.

## Goals

- Make combined indexes investigable, not just inspectable.
- Reuse the combined report's source inventory, coverage, endpoint candidate, and dependency surface concepts where practical.
- Find evidence-backed paths from endpoints, symbols, sources, or surfaces to downstream dependency surfaces.
- Preserve source labels, source index IDs, scan IDs, commit SHAs, fact IDs, edge IDs, rule IDs, evidence tiers, and file spans.
- Emit useful gaps instead of inventing links.
- Keep output deterministic and public-demo safe.

## Non-Goals

- No runtime call tracing.
- No graph database.
- No HTML viewer in this slice.
- No scanner rewrites.
- No full taint analysis or mutation analysis.
- No runtime dependency injection binding resolution.
- No dynamic dispatch, reflection, serializer, middleware, auth, CORS, proxy, deployment, or branch feasibility inference.
- No path persistence in the combined database by default.

## Command Shape

```bash
tracemap paths --index <combined.sqlite> --out <path> [options]
```

Options:

```text
--format <markdown|json>
--from-endpoint "<METHOD> <PATH_KEY>"
--to-endpoint "<METHOD> <PATH_KEY>"
--from-symbol <symbol>
--to-symbol <symbol>
--from-source <label>
--to-source <label>
--to-surface <kind>
--surface-name <text>
--source-pair <fromLabel>:<toLabel>
--max-depth <n>
--max-paths <n>
```

Output behavior:

- File output defaults to Markdown.
- `--format json` with a file writes JSON.
- Directory output writes both `paths-report.md` and `paths-report.json`.
- A non-existing `--out` path with no extension is treated as a directory.
- The command opens SQLite read-only.
- The command rejects non-combined indexes using the same combined index detection rules as `tracemap report`.

Default query:

- If no selectors are provided, start from matched endpoint pairs and search for paths to dependency surfaces.
- Cap default output tightly enough for review.
- Emit a summary if no starting endpoint evidence exists.

## Proposed Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedDependencyPathModels.cs
    CombinedDependencyPathReader.cs
    CombinedEvidenceGraph.cs
    CombinedPathQueryEngine.cs
    CombinedPathMarkdownWriter.cs
    CombinedPathJsonWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedDependencyPathTests.cs
```

`TraceMap.Reporting` remains the home because paths are a reporting/query layer over combined evidence. If the current combined report implementation remains a single file, this slice may first extract reusable source/report reader pieces so path search and report generation do not drift.

## Data Sources

Read from combined tables and views:

| Table/View | Purpose |
| --- | --- |
| `index_sources` | source labels, scan metadata, manifest JSON, coverage |
| `combined_facts` | endpoint, SQL/query, package/config, and general fact nodes |
| `combined_symbols` | stable symbol nodes |
| `combined_fact_symbols` | fact-to-symbol attachment |
| `combined_call_edges` | precise call graph edges |
| `combined_object_creations` | creation edges |
| `combined_symbol_relationships` | inheritance/interface/override edges |
| `combined_argument_flows` | direct argument evidence |
| `combined_parameter_forward_edges` | derived parameter-forwarding edges |
| `combined_dependency_edges` | summary fallback for graph edges |

The graph builder should prefer precise tables when present and use `combined_dependency_edges` as fallback or inventory evidence.

## Report Model

Suggested public model:

```csharp
public sealed record CombinedDependencyPathReport(
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedPathQuery Query,
    IReadOnlyList<CombinedReportSource> Sources,
    CombinedPathSummary Summary,
    IReadOnlyList<CombinedPath> Paths,
    IReadOnlyList<CombinedPathGap> Gaps,
    CombinedPathInventory Inventory,
    IReadOnlyList<string> Limitations);
```

No generated timestamp should be emitted by default. Outputs must be byte-stable for identical inputs.

Suggested path model:

```csharp
public sealed record CombinedPath(
    string PathId,
    string Classification,
    string Confidence,
    int Length,
    string StartNodeId,
    string EndNodeId,
    IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Notes);
```

Coverage values:

- `FullEvidenceAvailable`
- `ReducedCoverage`
- `UnknownAnalysisGap`

Path classifications:

- `DefiniteStaticPath`
- `ProbableStaticPath`
- `NeedsReviewPath`
- `UnknownAnalysisGap`
- `NoPathFound`
- `SelectorNoMatch`

Gap kinds:

- `SelectorNoMatch`
- `NoPathFound`
- `UnknownAnalysisGap`
- `TruncatedByLimit`
- `UnlinkedSurface`
- `DynamicBoundary`
- `SchemaMissing`

## Evidence Graph

### Node Kinds

Suggested node kinds:

- `EndpointClient`
- `EndpointRoute`
- `Symbol`
- `Method`
- `Type`
- `DependencySurface`
- `SqlSurface`
- `ConfigSurface`
- `PackageSurface`
- `ExternalSurface`
- `AnalysisGap`

Each node should carry:

- `nodeId`
- `nodeKind`
- `displayName`
- `sourceIndexId`
- `sourceLabel`
- `scanId`
- `commitSha`
- `symbolId`
- `combinedFactId`
- `ruleId`
- `evidenceTier`
- `filePath`
- `startLine`
- `endLine`
- optional surface metadata

Node IDs should be deterministic from source index ID plus the strongest available stable identifier:

1. symbol ID
2. combined fact ID
3. combined edge ID
4. source label, file path, line span, node kind, and display name hash

### Edge Kinds

Suggested edge kinds:

- `EndpointMatch`
- `Calls`
- `Creates`
- `Inherits`
- `Implements`
- `Overrides`
- `ArgumentPassed`
- `ParameterForwarded`
- `FactAttachedToSymbol`
- `SurfaceEvidence`
- `SameFileFallback`

Each edge should carry:

- `edgeId`
- `edgeKind`
- `fromNodeId`
- `toNodeId`
- `classification`
- `ruleId`
- `evidenceTier`
- `supportingFactIds`
- `supportingCombinedEdgeIds`
- `filePath`
- `startLine`
- `endLine`

Derived edge IDs should include an algorithm tag such as `tracemap.paths.endpoint-match.v1`.

## Graph Construction

1. Validate the combined index.
2. Read source rows and coverage metadata.
3. Read facts needed for endpoints, surfaces, symbols, and gaps.
4. Read symbols and symbol attachments.
5. Read precise edge tables.
6. Read `combined_dependency_edges` as fallback/inventory.
7. Build endpoint nodes and endpoint match edges using shared endpoint matching behavior from combined reporting.
8. Build symbol nodes from `combined_symbols` and source/target symbol fields.
9. Build dependency surface nodes from the same fact-type/property rules used by `tracemap report`.
10. Attach facts to symbols using `combined_fact_symbols`, explicit symbol properties, target/source symbols, containing type metadata, or file/line proximity only when the fallback is documented and classified as needs review.
11. Add graph gaps where a surface, endpoint, or edge cannot be credibly linked.

The graph should retain all provenance even when display nodes are deduplicated.

## Path Search Algorithm

Use deterministic breadth-first search for MVP.

Defaults:

- `maxDepth`: 8
- `maxPaths`: 100
- Markdown path cap: 100
- Markdown inventory cap: 200

Traversal:

- Start node set comes from selectors.
- End node set comes from selectors, `--to-surface`, or default terminal surfaces.
- Traverse outbound edges first.
- Optionally include reverse traversal only when the query asks for `--to-*` without a `--from-*`; this must be documented in query metadata.
- Avoid revisiting nodes already in the current path.
- Record cycle/depth truncation gaps.
- Stop at terminal surfaces unless future options allow expansion.

Ordering:

1. classification confidence
2. path length
3. start source label
4. end source label
5. display names
6. file paths
7. line numbers
8. stable node/edge IDs

Confidence:

- `High`: Tier1 semantic hops plus exact endpoint match.
- `Medium`: Tier2 structural hops or optional endpoint match.
- `Low`: Tier3 syntax/textual hops, fallback symbol links, dynamic or incomplete evidence.

Classification:

- `DefiniteStaticPath`: all hops are Tier1 semantic or exact endpoint derived.
- `ProbableStaticPath`: at least one Tier2 structural hop and no Tier3/fallback gaps.
- `NeedsReviewPath`: Tier3/fallback/name-only evidence is required.
- `UnknownAnalysisGap`: visible gaps prevent credible conclusion.

## Surface Attachment Rules

Attach surfaces conservatively:

- Use `combined_fact_symbols` when available.
- Use explicit `sourceSymbol`, `targetSymbol`, `methodSymbol`, `containingType`, `targetContainingType`, or equivalent properties when present.
- Use source label plus exact file path and line-span containment only as `NeedsReviewPath` evidence.
- Do not attach raw SQL text to table symbols by string matching.
- Do not infer repository/service ownership from folder names unless a fact or source label supports it.
- Do not infer runtime object identity from constructor arguments.

Unattached surfaces should still appear in inventory and may produce `UnlinkedSurface` gaps.

## Markdown Output

Sections:

1. Summary
2. Query
3. Sources
4. Paths
5. Path Gaps
6. Evidence Inventory
7. Limitations

Path rendering example:

```text
1. ProbableStaticPath, Medium, length 5
   client EndpointClient GET /api/orders/{} src/orders.ts:12
   -> EndpointMatch MatchedEndpoint High
   api EndpointRoute GET /api/orders/{} OrdersController.cs:18
   -> Calls Tier1Semantic csharp.semantic.call.v1
   api Method OrderService.GetOrders OrderService.cs:42
   -> Calls Tier1Semantic csharp.semantic.call.v1
   api Method OrderRepository.QueryOrders OrderRepository.cs:77
   -> SurfaceEvidence Tier2Structural csharp.syntax.querypattern.v1
   api SqlSurface SELECT orders columns id;status OrderRepository.cs:79
```

Markdown must not render raw snippets, raw SQL, raw URLs, local absolute paths, config values, or connection strings.

## JSON Output

Top-level shape:

```json
{
  "version": "1.0",
  "reportCoverage": "ReducedCoverage",
  "coverageWarnings": [],
  "query": {},
  "sources": [],
  "summary": {},
  "paths": [],
  "gaps": [],
  "inventory": {},
  "limitations": []
}
```

Use camelCase through existing JSON serializer options. Required arrays should be present even when empty. Missing values should be `null`, not omitted.

## Reuse and Refactoring

This spec should avoid copy-pasting the combined report internals.

Preferred implementation path:

1. Extract combined index validation/source reading into reusable internal helpers.
2. Extract dependency surface row construction into a reusable service or keep a shared internal projector.
3. Extract endpoint candidate/matcher behavior so `tracemap report` and `tracemap paths` agree on method lists, optional routes, dynamic reasons, same-source matches, and fan-out.
4. Keep output model types separate so report JSON and paths JSON can evolve independently.

## Limitations Text

Path reports must include these limitations:

- Paths are static evidence trails, not runtime execution traces.
- Endpoint matches do not prove traffic, reachability, auth, proxy behavior, deployment base paths, CORS, or middleware behavior.
- Call/object/relationship edges do not prove dynamic dispatch targets, runtime DI registrations, reflection targets, branch feasibility, or collection contents.
- Parameter-forwarding edges are direct static argument evidence, not full taint analysis, mutation tracking, or serializer contract mapping.
- SQL/query surfaces do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
- Reduced coverage means path absence is not evidence of absence.

## Open Questions for Review

- Should `paths` default to endpoint-to-surface queries, or require an explicit selector for the first implementation?
- Should reverse traversal be supported in MVP for `--to-symbol` and `--to-surface` queries?
- Should path reports reuse `CombinedDependencyReportDocument` source rows directly or introduce a narrower source row?
- Should `SameFileFallback` be included in MVP or deferred to avoid noisy paths?
- Should path JSON include an inventory of all graph nodes/edges, or only nodes/edges participating in returned paths plus gap evidence?
