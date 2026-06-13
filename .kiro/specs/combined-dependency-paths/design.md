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
--from-symbol <symbol>
--from-source <label>
--to-surface <kind>
--surface-name <text>
--source-pair <fromLabel>:<toLabel>
--max-depth <n>
--max-paths <n>
--max-frontier <n>
```

Output behavior:

- File output defaults to Markdown.
- `--format json` with a file writes JSON.
- Directory output writes both `paths-report.md` and `paths-report.json`.
- A non-existing `--out` path with no extension is treated as a directory.
- The command opens SQLite read-only.
- The command rejects non-combined indexes using the same combined index detection rules as `tracemap report`.

Default query:

- If no selectors are provided, start from matched endpoint pairs and search forward to terminal dependency surfaces.
- Terminal surfaces are `sql-query`, `package-config`, `http-client`, `http-route`, and `external`.
- Default query uses `maxDepth = 8`, `maxPaths = 100`, and `maxFrontier = 10000`.
- Unmatched server routes are not default start nodes in MVP.
- Endpoint start nodes are not terminal surfaces. `http-route` and `http-client` terminal surfaces are only satisfied after at least one non-endpoint traversal edge reaches HTTP dependency evidence.
- Emit a summary if no starting endpoint evidence exists.

Unsupported in v1:

- reverse traversal
- `--to-endpoint`
- `--to-symbol`
- `--to-source`
- treating call/create/relationship/parameter-forward edge kinds as terminal surfaces

`--surface-name` matching:

- case-insensitive exact matching by default
- leading/trailing `*` enables prefix, suffix, or contains matching
- exact matches sort before wildcard matches

`--source-pair` parsing splits on the last colon so labels may contain colons.

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

First implementation should be a behavior-preserving refactor of the combined report internals. Existing report tests should pass without changed output before path-specific code lands.

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

`endpoint_matches` ownership:

- The table exists in combined indexes but is intentionally unused by MVP report and paths commands.
- Both commands compute endpoint alignment in memory through one shared matcher.
- Extraction target: promote the combined-report endpoint matching logic into an internal `CombinedEndpointMatcher` service inside `TraceMap.Reporting`; `CombinedDependencyReporter` and `tracemap paths` both call that service. The existing two-index `TraceMap.EndpointAlignment.EndpointMatcher` remains for `tracemap endpoints` until a later compatibility refactor.
- The paths command must not read `endpoint_matches` as source of truth because indexes produced today have no rows there.
- Persisting endpoint matches is a follow-up that must first define ownership, lifecycle, and schema behavior for one-sided/dynamic findings.

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
    IReadOnlyList<CombinedPathNote> Notes);

public sealed record CombinedPathNote(
    string Code,
    string Message);
```

Coverage values:

- `FullEvidenceAvailable`
- `ReducedCoverage`
- `UnknownAnalysisGap`

Path classifications:

- `StrongStaticPath`
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

Inventory shape:

```csharp
public sealed record CombinedPathInventory(
    IReadOnlyDictionary<string, int> NodesByKind,
    IReadOnlyDictionary<string, int> EdgesByKind,
    IReadOnlyDictionary<string, int> NodesBySource,
    IReadOnlyDictionary<string, int> SurfacesByKind,
    IReadOnlyDictionary<string, int> GapsByKind,
    IReadOnlyList<CombinedPathNode> EvidenceNodes,
    IReadOnlyList<CombinedPathEdge> EvidenceEdges);
```

`EvidenceNodes` and `EvidenceEdges` include only nodes/edges participating in returned paths plus gap evidence. They do not dump the entire graph by default.

Surface kind vocabulary:

| Selector / inventory key | Node kind(s) | Notes |
| --- | --- | --- |
| `sql-query` | `SqlSurface` | SQL shape/hash evidence only |
| `package-config` | `PackageSurface`, `ConfigSurface` | One selector groups package, config, connection string, and env evidence |
| `http-client` | `HttpClientSurface` | Terminal outbound HTTP dependency evidence only |
| `http-route` | `HttpRouteSurface` | Terminal route evidence only when reached after non-endpoint traversal |
| `external` | `ExternalSurface` | Other explicit external dependency evidence |

Inventory dictionaries use selector/inventory keys for `SurfacesByKind` and node kinds for `NodesByKind`.

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
- `HttpClientSurface`
- `HttpRouteSurface`
- `ExternalSurface`

`AnalysisGap` is a `CombinedPathGap` record in MVP, not a graph node. It may be promoted to a node kind later if path displays need explicit gap terminators.

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

### Symbol Keys and Node IDs

Symbol joins are source-local in MVP. Cross-language or cross-source symbol stitching is not supported.

Normalize display names for symbol-key matching by trimming whitespace, normalizing line endings to spaces, and using ordinal string comparison after exact display format preservation. The normalized form is the key string. Do not lowercase C# or JVM symbol keys by default because generic/type casing can be significant to display identity. Selector matching may be case-insensitive, but graph joins are ordinal. Multi-line display names are rare in Tier1 semantic symbols; line-ending normalization is intentional because symbol keys are for joins, not display.

Symbol key:

```text
symbol-key-v1:{sourceIndexId}:{normalizedDisplayName}
```

Node IDs should be deterministic from source index ID plus the strongest available stable identifier:

1. symbol ID
2. combined fact ID
3. combined edge ID
4. source label, file path, line span, node kind, and display name hash

When `combined_symbol_id` exists, use it for node identity and retain the source-local symbol key for raw edge-table joins. When raw edge tables reference display strings, join only inside the same `sourceIndexId` using the symbol key. If multiple symbol rows in one source share a normalized display name, keep all candidates, retain provenance, and classify paths using that ambiguous join as `NeedsReviewPath`.

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
8. Build symbol nodes from `combined_symbols` and source/target symbol fields using source-local symbol keys.
9. Build dependency surface nodes from the same fact-type/property rules used by `tracemap report`.
10. Attach facts to symbols using `combined_fact_symbols`, explicit symbol properties, target/source symbols, method symbols, or containing type metadata.
11. Add graph gaps where a surface, endpoint, or edge cannot be credibly linked.

The graph should retain all provenance even when display nodes are deduplicated.

Do not attach surfaces by same-file or line-proximity fallback in MVP. Emit `UnlinkedSurface` instead.

The only cross-source edge in MVP is `EndpointMatch`.

## Path Search Algorithm

Use deterministic breadth-first search for MVP.

Defaults:

- `maxDepth`: 8
- `maxPaths`: 100
- `maxFrontier`: 10000 queued path states
- Markdown path cap: 100
- Markdown inventory cap: 200

Traversal:

- Start node set comes from selectors.
- End node set comes from `--to-surface` and optional `--surface-name`, or default terminal surfaces.
- Traverse outbound edges only.
- Avoid revisiting nodes already in the current path.
- Record cycle/depth/path/frontier truncation gaps.
- Stop at terminal surfaces unless future options allow expansion.
- Down-rank relationship edges relative to call/create/parameter-forward edges so broad inheritance graphs do not dominate early results.

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

- `High`: derived from `StrongStaticPath`.
- `Medium`: derived from `ProbableStaticPath`.
- `Low`: derived from `NeedsReviewPath`, `UnknownAnalysisGap`, `NoPathFound`, or `SelectorNoMatch`.

Classification:

- `StrongStaticPath`: all non-endpoint hops are Tier1 semantic and every endpoint hop, if present, is an exact static method/path match. A pure symbol-to-surface path with all Tier1 hops also qualifies.
- `ProbableStaticPath`: at least one Tier2 structural hop and no Tier3/fallback/ambiguous/gap evidence.
- `NeedsReviewPath`: Tier3/fallback/name-only/ambiguous symbol evidence, optional endpoint matches, method mismatches, dynamic evidence, or unresolved receivers are required.
- `UnknownAnalysisGap`: visible gaps prevent credible conclusion.

Endpoint match quality closed set for path classification:

- Exact endpoint hop: `MatchedEndpoint` with static match quality `High`.
- Non-exact endpoint hop: `OptionalSegmentMatch`, `MethodMismatch`, `AmbiguousMatch`, `DynamicClientUrlNeedsReview`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientMatch`, or `UnknownAnalysisGap`.
- Any non-exact endpoint hop prevents `StrongStaticPath`.

Classification rank:

1. `UnknownAnalysisGap`
2. `NeedsReviewPath`
3. `ProbableStaticPath`
4. `StrongStaticPath`
5. `NoPathFound`
6. `SelectorNoMatch`

No-path rule:

- Contributing sources are the sources of all resolved start nodes plus all sources reachable from those start nodes within `maxDepth` and `maxFrontier` using outbound traversal.
- If selectors match but no path is found and any contributing source has reduced coverage, known gaps, failed/partial build status, unknown commit SHA, or analysis gaps, emit `UnknownAnalysisGap`.
- Emit `NoPathFound` only when every contributing source has credible full coverage.

## Surface Attachment Rules

Attach surfaces conservatively:

- Use `combined_fact_symbols` when available.
- Use explicit `sourceSymbol`, `targetSymbol`, `methodSymbol`, `containingType`, `targetContainingType`, or equivalent properties when present.
- Do not attach raw SQL text to table symbols by string matching.
- Do not infer repository/service ownership from folder names unless a fact or source label supports it.
- Do not infer runtime object identity from constructor arguments.
- Do not attach surfaces by source label plus file path or line proximity in MVP.

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
   api SqlSurface table orders columns id;status shape shape123 OrderRepository.cs:79
```

Markdown must not render raw snippets, raw SQL, raw URLs, local absolute paths, config values, or connection strings.

Markdown path and display helpers should:

- use a shared safe-file-path helper that hashes or rejects local absolute paths
- cap display names at 200 characters
- escape `|`, line endings, `[`, `]`, `(`, and `)` in table cells

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

Path JSON should include structured notes:

```json
{ "code": "ReducedCoverage", "message": "api has known gaps; absence conclusions are coverage-relative." }
```

## Reuse and Refactoring

This spec should avoid copy-pasting the combined report internals.

Preferred implementation path:

1. Extract combined index validation/source reading into reusable internal helpers.
2. Extract endpoint candidate/matcher behavior so `tracemap report` and `tracemap paths` agree on method lists, optional routes, dynamic reasons, same-source matches, and fan-out.
3. Extract dependency surface row construction into a reusable service or keep a shared internal projector.
4. Add report/paths endpoint-match parity tests before implementing BFS.
5. Keep output model types separate so report JSON and paths JSON can evolve independently.

## Limitations Text

Path reports must include these limitations:

- Paths are static evidence trails, not runtime execution traces.
- Endpoint matches do not prove traffic, reachability, auth, proxy behavior, deployment base paths, CORS, or middleware behavior.
- Call/object/relationship edges do not prove dynamic dispatch targets, runtime DI registrations, reflection targets, branch feasibility, or collection contents.
- Parameter-forwarding edges are direct static argument evidence, not full taint analysis, mutation tracking, or serializer contract mapping.
- SQL/query surfaces do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.
- Reduced coverage means path absence is not evidence of absence.

## Review Decisions

- Default query: endpoint-matched pairs to terminal surfaces with `maxDepth = 8`, `maxPaths = 100`, and `maxFrontier = 10000`.
- Reverse traversal: deferred.
- Source rows: use a narrower paths source row or shared source inventory model, not `CombinedDependencyReportDocument`.
- Same-file fallback: deferred.
- JSON inventory: include counts plus nodes/edges participating in returned paths and gap evidence, not the full graph.
