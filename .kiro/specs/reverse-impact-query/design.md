# Reverse Impact Query Design

## Overview

Add a deterministic reverse dependency query layer over one combined TraceMap snapshot.

The workflow becomes:

```bash
tracemap reverse \
  --index combined.sqlite \
  --surface sql-query \
  --surface-name runners \
  --to endpoints \
  --out reverse-report
```

The command reads a combined database, selects dependency surfaces, walks the existing static dependency graph backward toward upstream roots, and emits Markdown plus JSON. It is an investigation layer over existing static evidence. It does not run applications, scan repositories, infer business importance, or call AI services.

## Goals

- Answer "what can reach this dependency surface?" from a combined index.
- Reuse `tracemap paths` graph evidence and endpoint matching semantics so forward and reverse queries agree.
- Group reverse evidence by endpoint, symbol, or source root.
- Preserve source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, fact IDs, edge IDs, and limitations.
- Be deterministic, bounded, testable, and safe for public reports.

## Non-Goals

- No runtime usage proof.
- No production telemetry, tracing, auth, CORS, routing middleware, deployment, proxy, or customer-usage inference.
- No runtime dependency injection, reflection, serializer, dynamic dispatch, collection contents, mutation semantics, or branch feasibility resolution.
- No SQL execution, SQL equivalence, database schema validation, or query-plan inference.
- No full taint/dataflow engine.
- No source-code text diffing.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No web UI in this slice.
- No mutation of combined indexes.

## Command Shape

```text
tracemap reverse --index <combined.sqlite> --out <path> [options]
```

Options:

```text
--format <markdown|json>
--source <label>
--surface <sql-query|http-route|http-client|package-config>
--surface-name <text>
--to <endpoints|symbols|sources|all>
--max-depth <n>
--max-frontier <n>
--max-surfaces <n>
--max-roots <n>
--max-paths-per-root <n>
--max-gaps <n>
--exit-code
```

Output behavior should match existing report/path/diff/impact conventions:

- File output defaults to Markdown.
- `--format json` with file output writes JSON.
- Directory output writes `reverse-report.md` and `reverse-report.json`.
- A non-existing output path with no extension is treated as a directory.
- Input is opened read-only.
- Non-combined indexes are rejected with a clear error.

`--exit-code` returns `1` only when requested and reverse roots or paths are present. Gaps, selector misses, and no-evidence rows do not force non-zero exit unless a future option explicitly asks for strict gaps.

Default caps:

| Option | Default |
| --- | --- |
| `--to` | `endpoints` |
| `--max-depth` | `8` |
| `--max-frontier` | `10000` |
| `--max-surfaces` | `200` |
| `--max-roots` | `100` |
| `--max-paths-per-root` | `5` |
| `--max-gaps` | `1000` |

Reverse traversal can fan out faster than forward traversal when a widely used surface is selected. `--max-surfaces` bounds the selected start set, and `--max-roots` is the primary bound for high fan-in surfaces. Surface selection is sorted deterministically before capping, and hitting either cap emits `combined.reverse.truncation.v1`.

## Relationship to Existing Commands

`tracemap report` answers "what dependency evidence exists in this combined snapshot?"

`tracemap paths` answers "what static evidence trail connects this upstream selector to terminal dependency surfaces?"

`tracemap impact` answers "what changed between two combined snapshots, and optionally what before/after path context surrounds those changes?"

`tracemap reverse` answers "starting from this dependency surface in one combined snapshot, which upstream endpoint, symbol, or source roots can reach it?" The key inversion is that `paths` starts from callers and finds surfaces, while `reverse` starts from a surface and finds callers. It should reuse the same graph inventory as `paths` and render paths in root-to-surface order for readability, even though traversal may be computed from terminal surfaces backward.

## Product Semantics

Preferred wording:

- "reverse static evidence"
- "reachable static root"
- "selected dependency surface"
- "coverage-relative"
- "needs review"
- "analysis gap"

Avoid:

- "this endpoint uses the database at runtime"
- "called in production"
- "no callers" when coverage is reduced
- "database/table exists" from SQL evidence alone
- "root cause" or "business impact"

## High-Level Flow

1. Validate options.
2. Open the combined index read-only and validate combined schema.
3. Build or reuse the same combined path graph inventory used by `tracemap paths`.
4. Select terminal dependency surfaces from `combined_facts` and graph terminal nodes.
5. Build reverse adjacency from the path graph without changing stored data.
6. Run bounded reverse traversal from selected surfaces toward requested root targets.
7. Reconstruct root-to-surface path order for output.
8. Apply coverage, schema, duplicate identity, selector, and truncation caveats.
9. Classify selected surfaces, reverse roots, paths, and gaps.
10. Sort and cap roots, paths, surfaces, and gaps deterministically.
11. Render deterministic Markdown and JSON.

## Code Placement

Suggested layout:

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedReverseQuery.cs
    CombinedReverseQueryModels.cs
    CombinedReverseTraversal.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedReverseQueryTests.cs
```

`TraceMap.Reporting` remains the right home because the command is a report/query layer over combined evidence.

If the existing path reporter is too write-oriented, first extract internal reusable APIs without behavior changes:

- `CombinedDependencyPathReporter.BuildReportAsync(...)` or an equivalent path graph inventory builder.
- A graph inventory object containing endpoint roots, symbol nodes, source nodes, dependency surfaces, edges, evidence tiers, rule IDs, file spans, and source provenance.
- Shared output writing, safe rendering, hashing, and format normalization helpers.

The refactor must preserve current `report`, `paths`, `diff`, and `impact` output behavior before reverse behavior lands.

## Data Sources

The MVP should not read raw source files. It should read only combined snapshot data and existing derived projections:

| Source | Purpose |
| --- | --- |
| `index_sources` | source labels, scan IDs, commit SHAs, coverage, identity |
| `combined_facts` | endpoint and dependency surface evidence |
| `combined_symbols` | symbol identities where available |
| `combined_fact_symbols` | fact-to-symbol attachment |
| `combined_dependency_edges` | dependency edge summary |
| precise edge tables | call/create/relationship/argument/parameter-forward details when available |
| path graph inventory | bounded traversal graph shared with `tracemap paths` |

`endpoint_matches` should remain reserved unless a separate ownership spec defines persisted derived endpoint rows. This command should reuse the in-memory endpoint matching used by reports and paths.

## Report Model

Suggested models:

```csharp
public sealed record CombinedReverseReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedReverseQuery Query,
    CombinedReverseSnapshotInfo Snapshot,
    CombinedReverseSummary Summary,
    IReadOnlyList<CombinedReverseSurface> SelectedSurfaces,
    IReadOnlyList<CombinedReverseRoot> ReverseRoots,
    IReadOnlyList<CombinedReversePath> Paths,
    IReadOnlyList<CombinedReverseGap> Gaps,
    IReadOnlyList<string> Limitations);
```

```csharp
public sealed record CombinedReverseSurface(
    string SurfaceId,
    string SurfaceKind,
    string StableKey,
    string SourceLabel,
    string DisplayName,
    string Classification,
    string Confidence,
    string RuleId,
    string EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats);
```

```csharp
public sealed record CombinedReverseRoot(
    string RootId,
    string RootKind,
    string StableKey,
    string SourceLabel,
    string DisplayName,
    string Classification,
    string Confidence,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> PathIds,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats);
```

```csharp
public sealed record CombinedReversePath(
    string PathId,
    string RootId,
    string SurfaceId,
    string Classification,
    string Confidence,
    int Depth,
    IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);
```

```csharp
public sealed record CombinedReverseGap(
    string GapId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    IReadOnlyDictionary<string, string> Metadata);
```

The implementation can reuse existing path node/edge models if their JSON shape is already safe and stable. New reverse-specific models should avoid leaking local absolute paths, raw SQL, raw URLs, snippets, or config values.

## Stable Identity

Stable IDs must be derived from safe, deterministic fields:

- source labels
- scan IDs and commit SHAs where available
- normalized surface kind
- safe surface identity hash
- endpoint method and normalized path key
- symbol stable identity
- sorted node and edge evidence keys

Stable IDs must not depend on SQLite row IDs, combined fact IDs alone, local absolute paths, raw display names, raw SQL, raw URLs, output path, enumeration order, timestamps, or machine-specific separators.

Combined fact IDs and edge IDs can appear as supporting evidence IDs, sorted deterministically, but they should not be the only semantic identity for a root, selected surface, or path.

Endpoint roots use source label plus endpoint method and normalized path key. Symbol roots use source index identity plus the same source-local symbol stable key used by `tracemap paths` symbol reconciliation. Source roots use source label plus scan ID and commit SHA when available, with missing identity called out as an identity-unverified caveat rather than replaced by a volatile fallback.

## Traversal Strategy

The implementation should build a graph with the same semantics as `tracemap paths`:

- endpoint roots and endpoint matches
- symbol nodes
- source nodes
- dependency surface terminal nodes
- call/create/relationship/argument/parameter-forward edges where available
- fallback dependency edges where precise edges are absent
- fact-to-symbol attachments

Reverse traversal should:

1. Resolve selected terminal surfaces.
2. Build reverse adjacency from existing directed graph edges.
3. Traverse from each selected terminal surface toward requested root kinds.
4. Stop at surface, depth, frontier, root, and per-root path caps.
5. Deduplicate paths by stable path signature.
6. Render every path in root-to-surface order.

`--source <label>` narrows selected terminal surfaces and requested roots. It does not prune mid-path edges solely because a path crosses another source label; cross-source transitions are part of the evidence the command is meant to expose.

Contributing sources for no-path credibility are the sources of selected surfaces plus every source traversed while walking reverse adjacency toward roots within `--max-depth` and `--max-frontier`. Reduced coverage in any contributing source downgrades no-path conclusions to `UnknownAnalysisGap`. If traversal caps are also hit, emit both `TruncatedByLimit` and `UnknownAnalysisGap`.

### Reverse Adjacency Ordering

Reverse traversal must reuse the `tracemap paths` edge rank and deterministic node ordering when building the graph inventory. Reverse adjacency is sorted by:

1. path edge rank from the shared path graph inventory,
2. source label,
3. edge kind,
4. from-node stable key,
5. to-node stable key,
6. file path after safe path normalization,
7. start line,
8. supporting edge ID,
9. supporting fact ID.

Selected surfaces are sorted by classification rank, source label, surface kind, safe display name, file path, start line, and stable surface ID before `--max-surfaces` is applied.

Reverse roots are sorted by classification rank, root kind, source label, safe display name, shortest path length, path count descending, and stable root ID.

Reverse paths are sorted by classification rank, path length, root source label, root display name, terminal surface kind, terminal safe display name, file path, start line, and stable path ID.

Root kinds:

| `--to` | Root behavior |
| --- | --- |
| `endpoints` | Stop at endpoint evidence roots. |
| `symbols` | Stop at nearest stable symbol roots. |
| `sources` | Summarize source labels with representative bounded paths. |
| `all` | Include endpoint, symbol, and source roots with deduplicated path evidence. |

When endpoint roots are unavailable but symbol roots exist, `--to endpoints` should emit `UnknownAnalysisGap` or `NoReversePathEvidence` depending on coverage. It should not silently return symbols as endpoints.

## Classifications

### Surface Classifications

| Classification | Meaning |
| --- | --- |
| `SelectedSurfaceEvidence` | Surface selector matched safe dependency surface evidence. |
| `NeedsReviewSurfaceEvidence` | Surface match is name-only, syntax/textual, duplicate identity, or otherwise review-tier. |
| `UnknownAnalysisGap` | Surface selection exists but gaps prevent credible interpretation. |

### Reverse Path and Root Classifications

| Classification | Meaning |
| --- | --- |
| `StrongStaticReversePath` | Tier1 semantic path evidence with credible full coverage. |
| `ProbableStaticReversePath` | Strong structural evidence but incomplete semantic chain. |
| `NeedsReviewReversePath` | Syntax/textual, name-only, ambiguous, fallback, or duplicate-identity evidence. |
| `NoReversePathEvidence` | Selected surface had no reverse path under credible full coverage. |
| `UnknownAnalysisGap` | Gaps or reduced coverage prevent a credible conclusion. |
| `SelectorNoMatch` | Selectors matched no eligible evidence. |
| `TruncatedByLimit` | Depth, frontier, root, path, or gap caps were hit. |

### Confidence Mapping

| Classification | Confidence |
| --- | --- |
| `StrongStaticReversePath` | `High` |
| `ProbableStaticReversePath` | `Medium` |
| `SelectedSurfaceEvidence` | `Medium` |
| `NeedsReviewSurfaceEvidence` | `Low` |
| `NeedsReviewReversePath` | `Low` |
| `NoReversePathEvidence` | `Low` |
| `UnknownAnalysisGap` | `Low` |
| `SelectorNoMatch` | `Low` |
| `TruncatedByLimit` | `Low` |

## Rule IDs

New rule IDs:

| Rule ID | Purpose |
| --- | --- |
| `combined.reverse.surface.v1` | Selected dependency surface evidence and surface selector gaps. |
| `combined.reverse.root.v1` | Upstream endpoint, symbol, and source root grouping. |
| `combined.reverse.path.v1` | Reverse static path evidence from roots to selected surfaces. |
| `combined.reverse.selector.v1` | Selector no-match and unsupported selector gaps. |
| `combined.reverse.truncation.v1` | Depth, frontier, root, path, and gap cap gaps. |
| `combined.reverse.identity.v1` | Duplicate or unverified stable identity caveats specific to reverse grouping. |

Propagated supporting rule IDs:

| Rule ID | Use |
| --- | --- |
| `combined.paths.endpoint-match.v1` | Endpoint root matching and endpoint-root caveats. |
| `combined.paths.fact-attached-to-symbol.v1` | Fact-to-symbol graph attachments. |
| `combined.paths.surface-evidence.v1` | Terminal surface evidence. |
| `combined.paths.symbol-reconciliation.v1` | Symbol node reconciliation across sources. |
| `combined.paths.query-gap.v1` | Missing graph evidence or no-path query gaps. |
| `combined.paths.truncation-gap.v1` | Existing path traversal truncation semantics. |
| `combined.diff.identity.v1` | Propagated identity caveats from combined index provenance where duplication or unverified identity predates reverse grouping. |

Every new `combined.reverse.*.v1` rule must be added to `rules/rule-catalog.yml` with limitations before implementation is complete.

## Output Safety

Use existing safe helpers wherever possible:

- safe path rendering
- Markdown table escaping
- deterministic hash helper
- sorted metadata writer
- unsafe value omission/hash rules

Reports must not render:

- raw SQL text
- raw HTTP URLs
- config values
- connection strings
- source snippets
- local absolute paths
- raw repository remotes

Normalized table names, route templates, method names, package names, source labels, commit SHAs, and evidence tiers are renderable when they are already part of safe structured metadata.

## Error Handling

- Missing `--index` or `--out`: clear CLI validation error.
- Non-combined index: clear combined-index error.
- Bad `--surface`: list allowed values.
- Bad `--to`: list allowed values.
- Invalid numeric cap: clear positive-integer error.
- `--surface-name` with unsafe or empty value: clear validation error or safe no-match gap, depending on existing selector behavior.
- Missing optional precision schema: emit schema/query gap and continue with reduced precision if fallback graph evidence exists.

## Testing Strategy

Unit tests should cover:

- output-path handling for file/directory/JSON
- non-combined input rejection
- read-only database behavior
- selector no-match
- SQL, HTTP client, HTTP route, and package/config surface selection
- endpoint, symbol, source, and all target grouping
- strong/probable/review/unknown/no-evidence classifications
- reduced-coverage downgrade for no-path conclusions
- duplicate identity downgrade
- traversal caps and gap caps
- stable path/root/surface IDs independent of row order
- byte-stable Markdown and JSON
- unsafe value redaction
- `--exit-code`

Validation should include:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If traversal internals shared with `tracemap paths` change, also run the pinned combined-path smoke checks from `docs/VALIDATION.md`.

## Implementation Slices

Recommended first implementation PR:

1. Extract a reusable no-write path graph inventory from `tracemap paths` and prove existing `paths` output is byte-stable.
2. Add reverse models, CLI parsing, selector resolution, safe selected-surface output, Markdown, JSON, rule catalog entries, and tests.
3. Add reverse traversal to endpoint roots only.

Recommended follow-up PR:

1. Add `--to symbols`, `--to sources`, and `--to all`.
2. Add richer duplicate identity and schema-gap propagation.
3. Add public sample smoke coverage for a scanned combined client/server fixture.

The spec defines the full MVP, but the first PR boundary should be kept reviewable if implementation risk is high.
