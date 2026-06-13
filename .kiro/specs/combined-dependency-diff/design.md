# Combined Dependency Diff Design

## Overview

Add a deterministic diff layer over two combined TraceMap indexes.

The workflow becomes:

```bash
tracemap combine \
  --index before-client/index.sqlite --label client \
  --index before-api/index.sqlite --label api \
  --out before-combined.sqlite

tracemap combine \
  --index after-client/index.sqlite --label client \
  --index after-api/index.sqlite --label api \
  --out after-combined.sqlite

tracemap diff \
  --before before-combined.sqlite \
  --after after-combined.sqlite \
  --include-paths \
  --out diff-report
```

The command reads both combined databases in read-only mode, projects static evidence into stable comparison records, compares those records, and writes Markdown plus optional JSON. It is a query and explanation layer over existing facts, reports, and paths. It does not scan repositories, diff source code, execute applications, resolve runtime state, or call LLMs.

## Goals

- Make combined indexes comparable across commits, branches, or release snapshots.
- Explain static dependency changes with evidence, not source snippets.
- Reuse combined report and path query logic where practical.
- Preserve source labels, scan IDs, commit SHAs, extractor versions, rule IDs, evidence tiers, file spans, fact IDs, edge IDs, and path signatures.
- Treat reduced coverage and analysis gaps as first-class caveats.
- Keep output deterministic and public-demo safe.

## Non-Goals

- No source-code semantic diff.
- No runtime traffic, deployment, route, database, or package-lock resolution.
- No runtime dependency injection, reflection, dynamic dispatch, serializer, branch feasibility, mutation, or taint analysis.
- No HTML graph viewer.
- No schema migration of existing combined indexes.
- No persistence of derived diff rows.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Command Shape

```bash
tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path> [options]
```

Options:

```text
--format <markdown|json>
--scope <all|sources|endpoints|surfaces|edges|paths>
--include-paths
--source <label>
--endpoint "<METHOD> <PATH_KEY>"
--surface <kind>
--surface-name <text>
--max-depth <n>
--max-paths <n>
--max-frontier <n>
```

Output behavior:

- File output defaults to Markdown.
- `--format json` with a file writes JSON.
- Directory output writes both `diff-report.md` and `diff-report.json`.
- A non-existing `--out` path with no extension is treated as a directory.
- The command opens both SQLite files read-only.
- The command rejects non-combined indexes using the same combined index detection rules as `tracemap report` and `tracemap paths`.

Default query:

- Compare source inventory.
- Compare coverage and analysis gap summaries.
- Compare endpoint evidence.
- Compare dependency surfaces.
- Compare dependency edges.
- Do not compare paths unless `--include-paths` is provided.

Path query behavior:

- `--include-paths` runs the existing bounded path query over both snapshots.
- Path diffing uses stable path signatures, not path row ordering.
- Default path limits should match `tracemap paths`: `maxDepth = 8`, `maxPaths = 100`, and `maxFrontier = 10000`.
- Path comparison must report when limits prevented complete comparison.

## Proposed Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedDiffModels.cs
    CombinedDiffReader.cs
    CombinedDiffProjector.cs
    CombinedDiffEngine.cs
    CombinedDiffMarkdownWriter.cs
    CombinedDiffJsonWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedDependencyDiffTests.cs
```

`TraceMap.Reporting` remains the home because this is a reporting/query layer over combined evidence. If combined report/path internals are not reusable enough, the first implementation should extract shared readers/projectors in a behavior-preserving way before adding diff-specific logic.

## Data Sources

Read from combined tables and existing reporting services:

| Source | Purpose |
| --- | --- |
| `index_sources` | source labels, languages, scan metadata, manifests, coverage |
| `combined_facts` | endpoint, SQL/query, package/config, HTTP, and general evidence |
| `combined_symbols` | stable symbol evidence where available |
| `combined_fact_symbols` | fact-to-symbol attachment |
| `combined_call_edges` | precise call evidence |
| `combined_object_creations` | object creation evidence |
| `combined_symbol_relationships` | inheritance/interface/override evidence |
| `combined_argument_flows` | direct argument evidence |
| `combined_parameter_forward_edges` | derived parameter-forwarding evidence |
| `combined_dependency_edges` | dependency-edge summary and fallback |
| path query engine | optional bounded path inventory when `--include-paths` is set |

The diff command should not read `endpoint_matches` as source of truth unless a future spec defines ownership and population semantics for that table. It should reuse the same in-memory endpoint matching behavior used by report/path commands.

## Snapshot Model

Suggested internal model:

```csharp
public sealed record CombinedDiffSnapshot(
    string Side,
    IReadOnlyList<CombinedDiffSource> Sources,
    IReadOnlyList<ComparableEndpoint> Endpoints,
    IReadOnlyList<ComparableSurface> Surfaces,
    IReadOnlyList<ComparableEdge> Edges,
    IReadOnlyList<ComparablePath> Paths,
    IReadOnlyList<CombinedDiffGap> Gaps);
```

`Side` is `before` or `after`.

The snapshot projector is responsible for:

- validating combined schema;
- reading source metadata;
- normalizing comparable evidence records;
- computing stable keys;
- preserving provenance;
- detecting duplicate identities;
- applying selector filters;
- running path search only when requested.

No generated timestamp should be emitted. Outputs must be byte-stable for identical inputs.

## Stable Identity Rules

### Sources

Pair sources by exact combined source label.

Source metadata should include:

- source label;
- language;
- scan ID;
- commit SHA;
- repository identity where available;
- root path hash where available;
- extractor versions where available;
- coverage state;
- scan gap summaries.

If labels match but repo identity, language, or root identity conflicts, emit `SourceIdentityChanged`. Do not attempt fuzzy pairing.

### Endpoints

Endpoint key fields:

- source label;
- endpoint kind, such as client or route;
- HTTP method;
- normalized path key;
- handler or symbol stable identity where available;
- evidence rule family where necessary to avoid collisions.

Method and path should be normalized using the same endpoint normalizer as combined reporting. Dynamic or unresolved endpoints should retain a hashed identity and be classified no stronger than `NeedsReviewDiff`.

### Dependency Surfaces

Surface key fields:

- source label;
- surface kind;
- normalized surface identity;
- structured metadata hash.

Surface-specific identity examples:

- SQL/query: operation, normalized table names where available, query shape hash, parameter count, call-site symbol where available.
- Package/config: package name, config key, ecosystem, version range where available.
- HTTP dependency: method, normalized path key or host hash, client library kind where available.
- File/config surface: safe relative file identity or hashed unsafe path.

Raw SQL, raw URLs, config values, connection strings, and source snippets must not be rendered and should not be part of cleartext stable keys.

### Dependency Edges

Edge key fields:

- source label;
- edge kind;
- normalized source symbol or evidence identity;
- normalized target symbol or surface identity;
- rule family;
- metadata hash where needed.

Volatile row IDs, insertion ordering, and generated local database IDs must not drive change detection.

### Paths

Path signature fields:

- ordered node descriptor sequence;
- ordered edge descriptor sequence;
- start selector identity;
- terminal surface identity;
- source transition descriptors;
- classification and evidence-tier summary as metadata, not as the only key.

Use a deterministic hash for `pathSignature`. Preserve a debug-safe descriptor string only if it contains no unsafe source values.

## Diff Classifications

Diff classifications:

| Classification | Meaning |
| --- | --- |
| `Added` | Evidence exists only after, and before coverage was credible for that evidence kind. |
| `Removed` | Evidence exists only before, and after coverage was credible for that evidence kind. |
| `ChangedEvidence` | Stable identity exists in both snapshots, but evidence metadata changed. |
| `AddedWithBeforeGap` | Evidence exists only after, but before coverage/gaps prevent a strong added claim. |
| `RemovedWithAfterGap` | Evidence exists only before, but after coverage/gaps prevent a strong removed claim. |
| `NeedsReviewDiff` | Evidence identity or comparison is name-only, ambiguous, duplicate, or syntax/text based. |
| `UnknownAnalysisGap` | Source identity, commit SHA, schema, coverage, or analysis gaps prevent a credible conclusion. |
| `SelectorNoMatch` | User selectors matched no comparable evidence in either snapshot. |
| `NoPathEvidence` | Path comparison found no path evidence in either snapshot under credible full coverage. |
| `NoDiffEvidence` | Comparable evidence was found, but no diff was detected. |

Confidence mapping:

| Classification | Confidence |
| --- | --- |
| `Added` | High |
| `Removed` | High |
| `ChangedEvidence` | Medium |
| `AddedWithBeforeGap` | Low |
| `RemovedWithAfterGap` | Low |
| `NeedsReviewDiff` | Low |
| `UnknownAnalysisGap` | Low |
| `SelectorNoMatch` | Low |
| `NoPathEvidence` | Low |
| `NoDiffEvidence` | Medium |

Coverage-aware downgrade rules:

- If evidence is missing from `after` and the paired after source has reduced coverage or relevant analysis gaps, prefer `RemovedWithAfterGap` or `UnknownAnalysisGap` over `Removed`.
- If evidence is missing from `before` and the paired before source has reduced coverage or relevant analysis gaps, prefer `AddedWithBeforeGap` or `UnknownAnalysisGap` over `Added`.
- If either source has unknown commit SHA, classify source-wide conclusions no stronger than `UnknownAnalysisGap` unless the diff is purely schema/source inventory.
- If duplicate stable identities exist in either snapshot, classify affected rows no stronger than `NeedsReviewDiff`.

## Rule IDs

Diff-derived rule IDs should be documented in the rule catalog if implementation adds them. Proposed IDs:

| Rule ID | Purpose |
| --- | --- |
| `combined.diff.source.v1` | Source inventory and source metadata comparison. |
| `combined.diff.coverage.v1` | Coverage and analysis gap comparison. |
| `combined.diff.endpoint.v1` | Endpoint evidence comparison. |
| `combined.diff.surface.v1` | Dependency surface comparison. |
| `combined.diff.edge.v1` | Dependency edge comparison. |
| `combined.diff.path.v1` | Dependency path signature comparison. |
| `combined.diff.identity.v1` | Stable identity construction and duplicate identity gaps. |
| `combined.diff.selector.v1` | Selector matching and selector no-match gaps. |

Diff rows must also carry before/after evidence rule IDs where available. A diff rule ID explains the comparison rule; evidence rule IDs explain the source evidence.

## Report Model

Suggested public model:

```csharp
public sealed record CombinedDependencyDiffReport(
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedDiffQuery Query,
    CombinedDiffSnapshotInfo BeforeSnapshot,
    CombinedDiffSnapshotInfo AfterSnapshot,
    CombinedDiffSummary Summary,
    IReadOnlyList<CombinedDiffRow> SourceDiffs,
    IReadOnlyList<CombinedDiffRow> CoverageDiffs,
    IReadOnlyList<CombinedDiffRow> EndpointDiffs,
    IReadOnlyList<CombinedDiffRow> SurfaceDiffs,
    IReadOnlyList<CombinedDiffRow> EdgeDiffs,
    IReadOnlyList<CombinedPathDiffRow> PathDiffs,
    IReadOnlyList<CombinedDiffGap> Gaps,
    IReadOnlyList<string> Limitations);
```

Suggested row model:

```csharp
public sealed record CombinedDiffRow(
    string DiffId,
    string ChangeType,
    string Classification,
    string Confidence,
    string StableKey,
    string DiffRuleId,
    CombinedDiffEvidence? Before,
    CombinedDiffEvidence? After,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedDiffNote> Notes);
```

Suggested evidence model:

```csharp
public sealed record CombinedDiffEvidence(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? EvidenceKind,
    string? DisplayName,
    string? RuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyDictionary<string, string> SafeMetadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);
```

Required JSON fields should be emitted with `null` or empty arrays when data is absent.

## Markdown Shape

Sections:

1. Summary
2. Compared Snapshots
3. Sources
4. Coverage Changes
5. Endpoint Diffs
6. Surface Diffs
7. Edge Diffs
8. Path Diffs
9. Gaps
10. Limitations

Markdown row caps should be deterministic. Suggested defaults:

- 100 endpoint rows;
- 100 surface rows;
- 100 edge rows;
- 50 path rows;
- 200 total evidence rows per detailed section.

When `--include-paths` is omitted, Path Diffs should explicitly say path comparison was not run.

## JSON Shape

Top-level fields:

```json
{
  "version": "1.0",
  "reportCoverage": "FullEvidenceAvailable",
  "coverageWarnings": [],
  "query": {},
  "beforeSnapshot": {},
  "afterSnapshot": {},
  "summary": {},
  "sourceDiffs": [],
  "coverageDiffs": [],
  "endpointDiffs": [],
  "surfaceDiffs": [],
  "edgeDiffs": [],
  "pathDiffs": [],
  "gaps": [],
  "limitations": []
}
```

No `generatedAt`, timestamp, local machine name, or absolute local root should be emitted.

## Algorithm

1. Parse CLI options and normalize selectors.
2. Open both combined indexes read-only.
3. Validate combined schemas and collect schema gaps.
4. Read and pair source inventory by exact label.
5. Project comparable endpoint, surface, edge, and optional path records for before.
6. Project comparable endpoint, surface, edge, and optional path records for after.
7. Detect duplicate stable identities per comparable kind and side.
8. Compare key sets for each scope.
9. For shared keys, compare normalized evidence metadata hashes and provenance summaries.
10. Apply coverage-aware downgrade rules.
11. Sort diff rows deterministically by classification rank, kind, source label, stable key, file path, line, and diff ID.
12. Build summary counts and gaps.
13. Write Markdown and/or JSON.

## Deterministic Sorting

Classification rank:

1. `Removed`
2. `Added`
3. `ChangedEvidence`
4. `RemovedWithAfterGap`
5. `AddedWithBeforeGap`
6. `NeedsReviewDiff`
7. `UnknownAnalysisGap`
8. `SelectorNoMatch`
9. `NoPathEvidence`
10. `NoDiffEvidence`

Within classification:

1. evidence kind;
2. source label;
3. surface or endpoint kind;
4. stable key;
5. safe file path;
6. start line;
7. diff ID.

## Safety

Reuse the shared safe path and safe Markdown helpers. Reports must not render:

- raw SQL;
- raw URLs;
- config values;
- connection strings;
- raw source snippets;
- local absolute paths;
- private repository names introduced by test data;
- unescaped Markdown table control characters.

If a value is needed for identity but unsafe to show, render a stable hash and a closed-set reason code.

## Performance

MVP should project records in memory. This is acceptable because combined indexes used by current smoke tests are modest. Add caps and clear gaps before path diffing can explode:

- `--max-paths` controls produced paths per snapshot;
- `--max-frontier` controls queued path states per snapshot;
- Markdown caps control human report size;
- JSON includes all rows produced by configured caps.

Future work can add streaming comparison or SQL-side projection if large monorepos require it.

## Open Questions

- Should `--scope all` imply `--include-paths` in a later version once path search is faster?
- Should diff reports optionally emit SARIF for CI annotations?
- Should a future command compare single-language indexes directly, or should all comparison go through combined indexes?
- Should `tracemap combine` eventually store source repository identity more explicitly than current manifests provide?

## Deferred Follow-Ups

- HTML graph diff viewer.
- Persisted diff rows.
- SARIF output.
- Batch diff matrix for many commits or branches.
- Single-language index diffing.
- Deeper SQL parser-backed table/column/stored procedure diffing.
- Package lockfile and dependency resolver diffing.
- Runtime trace import and static/runtime comparison.
- Configurable source pairing maps for renamed source labels.
