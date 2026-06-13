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
--allow-identity-mismatch
--source <label>
--endpoint "<METHOD> <PATH_KEY>"
--surface <kind>
--surface-name <text>
--max-depth <n>
--max-paths <n>
--max-frontier <n>
--max-diff-rows <n>
--max-gaps <n>
--exit-code
```

Output behavior:

- File output defaults to Markdown.
- `--format json` with a file writes JSON.
- Directory output writes both `diff-report.md` and `diff-report.json`, even when `--format json` is provided.
- A non-existing `--out` path with no extension is treated as a directory.
- Output path resolution follows the existing combined report/path writer convention: an existing directory is a directory output; a non-existing leaf with no extension is a directory output; a file extension selects file output.
- The command opens both SQLite files read-only.
- The command rejects non-combined indexes using the same combined index detection rules as `tracemap report` and `tracemap paths`.
- Exit code is `0` by default when diffs are found. `--exit-code` makes diff rows produce exit code `1` for CI gates. Invalid input and schema errors always return non-zero.

Default query:

- Compare source inventory.
- Compare coverage and analysis gap summaries.
- Compare endpoint evidence.
- Compare dependency surfaces.
- Compare dependency edges.
- Do not compare paths unless `--include-paths` is provided.
- `--scope all` without `--include-paths` runs all non-path scopes and records `Path comparison: not requested`.
- `--scope paths` without `--include-paths` is an error.

Path query behavior:

- `--include-paths` runs the existing bounded path query over both snapshots.
- If `--include-paths` is provided with no endpoint, source, or surface selector, the start set is the same conservative default used by `tracemap paths`: matched endpoint pairs only.
- Path diffing uses stable path signatures, not path row ordering.
- Default path limits apply per snapshot and should match `tracemap paths`: `maxDepth = 8`, `maxPaths = 100`, and `maxFrontier = 10000`.
- Path signatures are deduplicated before diffing.
- Diff output is capped at `maxPaths` added, `maxPaths` removed, and `maxPaths` changed path rows by default.
- Path comparison must report when limits prevented complete comparison and must not classify capped-out unseen paths as added, removed, or unchanged.

Surface selector vocabulary:

| CLI value | Surface diff mapping | Path terminal mapping |
| --- | --- | --- |
| `sql-query` | SQL/query facts and SQL dependency surfaces | `sql-query` terminal surfaces |
| `http-route` | server route endpoint/surface evidence | `http-route` terminal surfaces |
| `http-client` | outbound HTTP client evidence | `http-client` terminal surfaces |
| `package-config` | package, dependency, config, environment variable, and framework config surfaces | `package-config` terminal surfaces |

`--surface-name` is exact case-insensitive matching in MVP. Wildcards are deferred.

Endpoint selector parsing:

- Split the value on the first whitespace run.
- The first token is the HTTP method and is normalized using existing endpoint method normalization.
- The remaining non-empty text is the normalized path key.
- A selector with only a method, only a path, or an empty path key is a validation error.
- Path keys are not expected to contain unescaped leading method tokens; the parser does not support method-less endpoint matching in MVP.

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

`TraceMap.Reporting` remains the home because this is a reporting/query layer over combined evidence. Diff code must live in the same reporting assembly unless shared report/path APIs are deliberately made public. If combined report/path internals are not reusable enough, the first implementation should extract shared readers/projectors in a behavior-preserving way before adding diff-specific logic. The refactor must include a reusable path query/projection API that returns a path inventory without writing reports, plus shared safe path, hashing, Markdown escaping, output writing, format normalization, path signature hashing, endpoint stable key, property parsing, and deterministic sort helpers.

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

`endpoint_matches` is intentionally not a data source for this command. Existing combined indexes may contain the table, but ownership, lifecycle, one-sided findings, dynamic findings, and refresh behavior are not defined for diff use.

Required schema objects:

| Required object | Reason |
| --- | --- |
| `index_sources` | source pairing, scan metadata, coverage |
| `combined_facts` | endpoint and surface evidence |
| `combined_symbols` | symbol identities where available |
| `combined_dependency_edges` | dependency edge summary |

Precision schema objects that must be checked individually:

| Object | Behavior when absent |
| --- | --- |
| `combined_fact_symbols` | compare fact-only evidence and down-rank symbol-specific identity |
| `combined_call_edges` | use `combined_dependency_edges` fallback |
| `combined_object_creations` | use `combined_dependency_edges` fallback |
| `combined_symbol_relationships` | use `combined_dependency_edges` fallback |
| `combined_argument_flows` | omit argument-flow-specific edge comparison |
| `combined_parameter_forward_edges` | omit parameter-forward-specific edge comparison |

If one of these objects is absent, the diff report must record a schema or coverage gap before using a weaker fallback. The validator must check the underlying tables individually, not only the `combined_dependency_edges` view, so older or partial combined indexes are labeled correctly.

Malformed metadata handling:

- Malformed `combined_facts.properties_json` must not crash the diff command.
- The row should be retained with an empty safe metadata set where possible, a `MalformedPropertiesJson` gap, and classification no stronger than `UnknownAnalysisGap` for conclusions that depend on the malformed metadata.
- Missing or empty `manifest_json` should emit `MissingManifestJson`; source identity, coverage, and commit-SHA-dependent conclusions for that source are classified no stronger than `UnknownAnalysisGap`.
- Supporting IDs read from facts, edges, paths, or gaps must be sorted ordinally before output.

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

If labels match but repo identity, language, or root identity conflicts, fail by default with `SourceIdentityChanged`. Do not attempt fuzzy pairing.

Source pairing validation:

- Exact label match is required.
- A pairing is trusted only when language matches and at least one repository identity signal matches, such as repository URL/name, repo identity hash, or root identity hash.
- If language, repository URL/name, or repository identity hash is present on both sides and conflicts, the command fails by default with `SourceIdentityChanged`.
- `--allow-identity-mismatch` lets the command continue, but all evidence diffs for that source are classified no stronger than `NeedsReviewDiff` and carry an identity caveat.
- Root path hash mismatch alone is a warning when repository identity and language match, because monorepo subdirectory layout can move.
- If identity signals are missing on either side, emit `SourceIdentityUnverified` and classify evidence diffs for that source no stronger than `NeedsReviewDiff`.
- Commit SHAs are compared as case-insensitive full strings with no prefix matching.
- If commit SHA is the only changed source metadata and comparable evidence is otherwise unchanged, emit one source-level `ChangedEvidence` row and do not report endpoint, surface, edge, or path churn.

### Endpoints

Endpoint key fields:

- source label;
- endpoint kind, such as client or route;
- HTTP method;
- normalized path key;
- handler or symbol stable identity where available;
- evidence rule family where necessary to avoid collisions.

Method and path should be normalized using the same endpoint normalizer as combined reporting. Dynamic or unresolved endpoints should retain a hashed identity and be classified no stronger than `NeedsReviewDiff`.

Endpoint key rules:

- Single-side endpoint key: source label, endpoint kind, method, normalized path key, and handler/symbol identity when available.
- Matched endpoint key: client single-side key plus server single-side key plus match classification.
- If an endpoint changes from unmatched to matched while the single-side endpoint evidence remains stable, emit `ChangedEvidence` for match metadata rather than unrelated added/removed endpoint rows.
- Combined fact IDs may be carried as provenance but must not be the only stable identity for endpoint comparison.

### Dependency Surfaces

Surface key fields:

- source label;
- surface kind;
- normalized surface identity;
- structured metadata hash.

Surface-specific identity rules:

- SQL/query: operation, normalized table names where available, query shape hash, parameter count, call-site symbol where available, file path plus line span hash when symbol identity is unavailable.
- Package/config: package name, config key, ecosystem, version range where available.
- HTTP dependency: method plus normalized path key when available; otherwise method plus host hash; otherwise safe file path plus line span hash with `NeedsReviewDiff`.
- File/config surface: safe relative file identity or hashed unsafe path.

Raw SQL, raw URLs, config values, connection strings, source snippets, and volatile row IDs must not be rendered and must not be part of cleartext stable keys.

Normalized table names are considered renderable schema identifiers, not raw SQL. They must still be Markdown-escaped, deterministically sorted, and excluded if the extractor marks them as unsafe or derived from raw text without parsing.

### Dependency Edges

Edge key fields:

- source label;
- edge kind;
- normalized source symbol or evidence identity;
- normalized target symbol or surface identity;
- rule family;
- metadata hash where needed.

Volatile row IDs, insertion ordering, and generated local database IDs must not drive change detection.

Rule family is derived from `ruleId` by removing a trailing version token matching `.v<digits>` when present. The full `ruleId` remains evidence metadata. A rule version change within the same family should produce `ChangedEvidence`, not unrelated added/removed edges.

### Paths

Path signature fields:

- ordered node descriptor sequence;
- ordered edge descriptor sequence;
- start selector identity;
- terminal surface identity;
- source transition descriptors;
- classification and evidence-tier summary as metadata, not as the only key.

Use a deterministic hash for `pathSignature`. Preserve a debug-safe descriptor string only if it contains no unsafe source values.

Node descriptors should use stable combined fact IDs, stable combined symbol IDs, source fact IDs, source symbol IDs, or fully qualified signatures where available. Display names are fallback identity only; any path signature that depends on display-name fallback is classified no stronger than `NeedsReviewDiff`.

Hashes:

- Stable keys, metadata hashes, fallback identity hashes, and path signatures use SHA-256.
- Encoding is UTF-8.
- Output format is lowercase hexadecimal.
- Inputs are canonical JSON with deterministic property ordering or `\n`-joined canonical field values.

Selector semantics:

- Selectors define comparison scope.
- Selectors are applied symmetrically to before and after snapshots.
- If a selector matches before but not after, include before evidence as `Removed`, `RemovedWithAfterGap`, or `UnknownAnalysisGap` based on coverage.
- If a selector matches after but not before, include after evidence as `Added`, `AddedWithBeforeGap`, or `UnknownAnalysisGap` based on coverage.
- If a selector matches neither snapshot, emit `SelectorNoMatch`.
- If a selector is only meaningful for disabled scopes, record it in query metadata as ignored for those scopes rather than applying it globally.

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

`ChangeType` is a coarser machine bucket separate from `Classification`.

Allowed `ChangeType` values:

| ChangeType | Classifications |
| --- | --- |
| `Added` | `Added`, `AddedWithBeforeGap` |
| `Removed` | `Removed`, `RemovedWithAfterGap` |
| `Changed` | `ChangedEvidence`, `NeedsReviewDiff` |
| `Gap` | `UnknownAnalysisGap`, `SelectorNoMatch`, `NoPathEvidence` |
| `None` | `NoDiffEvidence` |

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

- Both before and after coverage credible for the evidence kind: `Added` or `Removed` can be used.
- Before credible and after reduced for the evidence kind: missing after evidence is `RemovedWithAfterGap`.
- Before reduced and after credible for the evidence kind: new after evidence is `AddedWithBeforeGap`.
- Both reduced for the evidence kind: one-sided evidence is `UnknownAnalysisGap` unless evidence-tier and rule IDs prove the evidence kind was credibly covered in both snapshots.
- Unknown commit SHA: source-wide conclusions are `UnknownAnalysisGap` unless the diff is purely schema/source inventory.
- Source identity conflict allowed with `--allow-identity-mismatch`: evidence diffs are no stronger than `NeedsReviewDiff`.
- If duplicate stable identities exist in either snapshot, classify affected rows no stronger than `NeedsReviewDiff`.

Evidence-kind credibility:

- MVP does not require per-evidence-kind scanner coverage tables to exist.
- Use a shared `SourceHasCredibilityGap` predicate for coarse source credibility. It should include reduced analysis level, failed or partial build status, unknown commit SHA, and `AnalysisGap` facts.
- A relevant gap is a gap in the same source label and same comparable evidence kind when the gap exposes kind metadata.
- If gap kind is unknown, treat it as relevant to all evidence kinds in that source.
- Gaps in unrelated source labels do not downgrade a row unless the row is a cross-source endpoint or path diff involving that source.
- When kind-level credibility is unknown, prefer gap-aware classification over strong `Added` or `Removed`.

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

Each rule catalog entry must include limitations text. At minimum, limitations must state that diff rules compare static evidence, depend on stable identity keys, do not prove runtime behavior, and are coverage-relative when scan coverage is reduced.

## Report Model

Suggested public model:

```csharp
public sealed record CombinedDependencyDiffReport(
    string ReportType,
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

`DiffId` derivation:

```text
diffId = "diff:" + sha256(lowercase(changeType) + "\n" + classification + "\n" + stableKey + "\n" + diffRuleId)
```

If duplicate instances share the same stable key, append a deterministic duplicate ordinal to the hash input.

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
    IReadOnlyList<KeyValuePair<string, string>> SafeMetadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);
```

`SafeMetadata` is serialized as a sorted key-value array, ordered by ordinal key then ordinal value.

Safe metadata allowlist:

| Surface family | Allowed keys |
| --- | --- |
| SQL/query | `operation`, `operationName`, `tableName`, `tableNames`, `columnNames`, `fieldNames`, `sqlSourceKind`, `queryShapeHash`, `patternHash`, `textHash`, `textLength` |
| Package/dependency | `packageName`, `dependencyName`, `moduleName`, `groupId`, `artifactId`, `version`, `dependencyKind`, `ecosystem` |
| Config | `configKey`, `keyPath`, `environmentVariableName`, `connectionStringName` |
| HTTP | `httpMethod`, `normalizedPathKey`, `normalizedPathTemplate`, `urlKind`, `hostHash`, `dynamicReason` |
| Evidence | `evidenceTier`, `ruleId`, `sourceLabel`, `language`, `scanId`, `commitSha`, `filePath`, `startLine`, `endLine` |

Excluded keys include `sqlText`, `rawSql`, `url`, `rawUrl`, `configValue`, `connectionString`, `snippet`, `literalValue`, and any unrecognized property unless it is hashed under a closed-set hash key.

Suggested path diff model:

```csharp
public sealed record CombinedPathDiffRow(
    string DiffId,
    string ChangeType,
    string Classification,
    string Confidence,
    string PathSignature,
    string DiffRuleId,
    CombinedPathEvidence? Before,
    CombinedPathEvidence? After,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedDiffNote> Notes);

public sealed record CombinedPathEvidence(
    string PathId,
    string PathClassification,
    string StartIdentity,
    string EndIdentity,
    IReadOnlyList<string> SourceTransitions,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<KeyValuePair<string, string>> TerminalSurfaceMetadata);
```

Required JSON fields should be emitted with `null` or empty arrays when data is absent.

Suggested snapshot info model:

```csharp
public sealed record CombinedDiffSnapshotInfo(
    string Side,
    IReadOnlyList<CombinedDiffSourceInfo> Sources,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<KeyValuePair<string, string>> ExtractorVersions);

public sealed record CombinedDiffSourceInfo(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? RepositoryIdentity,
    string? RootPathHash,
    string Coverage,
    IReadOnlyList<string> GapCodes);
```

Suggested diff gap model:

```csharp
public sealed record CombinedDiffGap(
    string GapId,
    string GapKind,
    string? SourceLabel,
    string? EvidenceKind,
    string RuleId,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);
```

`CombinedDiffGap` is separate from path-specific gap models because source, schema, selector, and metadata gaps may not have path node IDs.

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

The Summary must also include `Path comparison: not requested` when path diffing is omitted.

## JSON Shape

Top-level fields:

```json
{
  "reportType": "combined-dependency-diff",
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

`reportType` is required so consumers can distinguish diff JSON from report/path JSON even when each report type has version `1.0`.

`reportCoverage` values are a closed set:

- `FullEvidenceAvailable`
- `ReducedCoverage`
- `UnknownAnalysisGap`

No `generatedAt`, timestamp, local machine name, or absolute local root should be emitted.

## Algorithm

1. Parse CLI options and normalize selectors.
2. Open both combined indexes read-only.
3. Validate combined schemas and collect schema gaps.
4. Read and pair source inventory by exact label.
5. Project comparable endpoint, surface, edge, and optional path records for before.
6. Project comparable endpoint, surface, edge, and optional path records for after.
7. Detect duplicate stable identities per comparable kind and side.
8. Deduplicate path signatures before comparison.
9. Compare key sets for each scope.
10. For shared keys, compare normalized evidence metadata hashes and provenance summaries.
11. Apply coverage-aware downgrade rules.
12. Apply engine-level row caps and emit truncation gaps for omitted rows.
13. Coalesce duplicate selector and schema gaps across before/after snapshots where the same normalized query failed on both sides.
14. Sort diff rows deterministically by classification rank, kind, source label, stable key, file path, line, and diff ID.
15. Sort gaps by gap kind, source label, evidence kind, rule ID, message, and gap ID.
16. Use a constant ordered limitations list.
17. Build summary counts and gaps.
18. Write Markdown and/or JSON.

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

- `--max-diff-rows` controls produced rows per non-path diff kind and defaults to 1000;
- `--max-gaps` controls produced gaps and defaults to 1000;
- `--max-paths` controls produced paths per snapshot;
- `--max-frontier` controls queued path states per snapshot;
- path diff output is capped by path added/removed/changed buckets as described above;
- Markdown caps control human report size;
- JSON includes all rows produced by configured caps.

Opening a SQLite database in read-only mode should not change the main database file. Tests should hash the main database file before and after. Sidecar `-wal`/`-shm` files may exist if inputs were produced in WAL mode; tests should avoid flakiness by creating fixture databases without active writers or by explicitly scoping the byte-stability assertion to the main file.

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
