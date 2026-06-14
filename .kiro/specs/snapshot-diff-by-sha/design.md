# Snapshot Diff By Commit SHA Design

## Overview

Snapshot diff is a stricter release-review layer over TraceMap's existing index and combined-diff capabilities.

```text
before index.sqlite / before combined.sqlite
after index.sqlite  / after combined.sqlite
        |
        v
tracemap snapshot-diff --before ... --after ... --out ...
        |
        v
snapshot-diff-report.md / snapshot-diff-report.json
```

The command compares static indexed evidence from two artifact snapshots that claim to represent the same repository identity at different commit SHAs. It does not fetch Git history, checkout commits, scan source, parse patches, execute user code, or call AI systems.

## Relationship To Existing Commands

`tracemap diff` compares two combined indexes and is useful for broad multi-repo evidence changes. `tracemap snapshot-diff` adds stricter snapshot semantics:

- source identity and commit SHA are first-class inputs to validation;
- single-language indexes are supported as one-source snapshots;
- combined indexes must pair sources by exact labels and repository identity;
- extractor-version and coverage changes are explicit report sections;
- output is shaped for future release review consumption.

Where possible, combined-index snapshot diff should delegate projection and comparison to `CombinedDependencyDiffer` or shared combined diff readers. Snapshot diff should not copy combined endpoint/surface/edge/path comparison logic unless a shared projector first makes reuse impossible.

Most combined-index behavior is therefore not net-new diff logic. The new value is the stricter same-repository snapshot contract, commit-SHA validation, single-index projection, extractor-version caveats, and release-review-shaped report sections.

`tracemap impact` remains the command for static change context around changed evidence. Future release review reports may consume snapshot diff and impact reports together.

## Command Shape

```bash
tracemap snapshot-diff \
  --before <index.sqlite|combined.sqlite> \
  --after <index.sqlite|combined.sqlite> \
  --out <path> \
  [options]
```

Options:

```text
--format <markdown|json>
--source <label>
--scope <all|sources|coverage|endpoints|surfaces|graph|paths|gaps|extractors|contract-shapes>
--endpoint "<METHOD> <PATH_KEY>"
--surface <kind>
--surface-name <text>
--include-paths
--allow-identity-mismatch
--max-depth <n>
--max-paths <n>
--max-frontier <n>
--max-diff-rows <n>
--max-gaps <n>
--exit-code
```

`--scope` is the coarse selector. A future `--kind` filter can be added only if it is distinct from scope. V1 should not expose both names for the same behavior.

Scope mapping:

| Snapshot scope | Combined delegation scope | Notes |
| --- | --- | --- |
| `all` | `all` | include every supported delegated section plus snapshot-specific rows |
| `sources` | `sources` | includes source inventory changes |
| `coverage` | `sources` then filter | combined diff emits coverage rows under source comparison support |
| `endpoints` | `endpoints` | maps directly |
| `surfaces` | `surfaces` | maps directly |
| `graph` | `edges` | combined `EdgeDiffs` render as snapshot `graphDiffs` |
| `paths` | `paths` | requires `--include-paths` |
| `gaps` | none | snapshot-specific gap projection; delegated combined `Gaps` still flow to top-level `gaps` |
| `extractors` | none | snapshot-specific extractor-version projection |
| `contract-shapes` | none | snapshot-specific projector output when facts are available |

Snapshot scope tokens must be validated and translated before any shared combined diff helper is called. `coverage`, `graph`, `gaps`, `extractors`, and `contract-shapes` must never be forwarded to existing combined scope validators. `coverage` may internally query the combined `sources` scope because coverage rows are produced from source comparison support, but the user-visible snapshot report should filter to coverage rows. Unknown scope tokens fail closed.

Output behavior matches existing report/diff conventions:

- file output defaults to Markdown;
- `--format json` with a file writes JSON;
- directory output writes both `snapshot-diff-report.md` and `snapshot-diff-report.json`;
- read inputs using SQLite read-only mode;
- do not write derived rows into either input database.

## Input Detection

The command must detect input shape before comparison:

| Input kind | Detection | V1 behavior |
| --- | --- | --- |
| Single-language index | has `scan_manifest` / source fact tables and lacks combined `index_sources` | compare as one-source snapshot |
| Combined index | has `index_sources` and combined fact tables | compare source labels and delegate to combined diff where possible |
| Mixed single/combined | one input single, one input combined | validate both shapes, fail non-zero before comparison, and write no output files |
| Missing commit SHA | manifest or source commit SHA is null, empty, or `unknown` | emit `UnknownAnalysisGap` for history-dependent conclusions and continue only where static evidence comparison is still credible |
| Unknown schema | missing required single/combined markers | fail with sanitized schema error |

Single-index support is important because "same repo, two commit SHAs" is often a single repository workflow. Combined-index support is important because multi-language or multi-service repos may already be represented through `tracemap combine`.

## Snapshot Model

Suggested internal model:

```csharp
public sealed record SnapshotDiffSnapshot(
    string Side,
    string InputKind,
    IReadOnlyList<SnapshotDiffSource> Sources,
    IReadOnlyList<SnapshotComparableRecord> Records,
    IReadOnlyList<SnapshotDiffGap> Gaps);
```

`Side` is `before` or `after`. `InputKind` is `single` or `combined`.

`SnapshotDiffSource` should include:

- source label;
- source index ID when combined;
- language;
- scan ID;
- repo identity hash or equivalent stable identity signal;
- commit SHA;
- scan root hash or safe scan-root metadata where available;
- analysis level;
- build status;
- extractor versions;
- gap summaries.

## Source Identity Validation

Source identity is strict by default.

For single indexes:

1. Read both manifests.
2. Parse manifest JSON for repository identity inputs.
3. Derive repository identity as `repo-hash:{Hash(RemoteUrl ?? RepoName)}` when either value exists.
4. Derive root identity as `ScanRootPathHash ?? GitRootHash` when available and store it as a root caveat, not as a replacement for repository identity.
5. Compare repository identity and commit SHA.
6. Treat language as optional metadata because current single-index manifests do not guarantee a first-class language column.
7. Fail on repository identity conflict.
8. Emit `SourceIdentityUnverified` when identity signals are incomplete.

For combined indexes:

1. Pair sources by exact source label.
2. Compare language and repository identity signals from `index_sources`.
3. Treat missing labels as source added/removed.
4. Fail on conflicts unless `--allow-identity-mismatch` is set.

`--allow-identity-mismatch` should not make strong claims. It permits report generation, but affected rows are review-tier, confidence is capped at `review`, and both before/after snapshot metadata include identity caveats. Repository identity values in JSON remain hashed forms only.

Commit SHAs are compared as exact case-insensitive strings. Do not prefix-match short and long SHAs. Unknown SHAs make history-dependent conclusions `UnknownAnalysisGap`. Commit SHAs included in stable metadata or output should be lowercased.

Reports and errors must never render raw `RemoteUrl`, `RepoName`, local scan roots, or private source paths. Use source labels, hashed identity strings, and sanitized caveats only.

## Coverage Validation

Coverage is not a decoration; it controls classifications.

Inputs to coverage judgment:

- analysis level;
- build status;
- `AnalysisGap` facts;
- schema gaps;
- missing precision tables;
- extractor version changes;
- missing or malformed manifests.

Reduced coverage downgrades absence/presence claims. If before coverage is reduced, a new row in after may have existed before but not been extracted. If after coverage is reduced, a missing row may still exist but not be extracted.

## Comparable Evidence Records

`SnapshotComparableRecord` should normalize each evidence kind into one comparison shape:

```csharp
public sealed record SnapshotComparableRecord(
    string EvidenceKind,
    string SourceLabel,
    string StableKey,
    string DisplayName,
    string RuleId,
    string EvidenceTier,
    SnapshotEvidenceSide Evidence,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<SnapshotDiffCaveat> Caveats);
```

Evidence kinds:

| Evidence kind | Inputs |
| --- | --- |
| `source` | source inventory and manifest metadata |
| `coverage` | analysis/build/gap summaries |
| `endpoint` | HTTP route/client facts and endpoint projection |
| `contract-shape` | type/property/method/DTO/serializer/member facts |
| `surface` | SQL, package, HTTP, config, storage, event/message surfaces |
| `graph` | call edges, object creation, symbol relationships, argument flow, parameter forwarding |
| `gap` | `AnalysisGap` and schema/metadata gaps |
| `extractor` | extractor name/version changes |
| `path` | optional bounded path signatures |

## Stable Key Rules

Use the strongest safe identity available and avoid row IDs:

- source: exact source label plus repository identity hash;
- endpoint: source label, endpoint kind, method, normalized path key, handler/symbol identity where available;
- contract-shape: source label, symbol/documentation identity, member name, containing type, arity/signature where available;
- SQL surface: source label, surface kind, operation, safe table/column metadata, query shape hash, source kind, text hash only as review-tier fallback;
- package surface: source label, ecosystem, package name, version/range, manifest/lockfile path hash;
- event/message surface: source label, surface kind, safe topic/queue/event identifier, publisher/consumer role;
- graph evidence: source label, edge kind, source symbol identity, target symbol identity, rule family, safe metadata hash;
- fallback: source label, evidence kind, rule ID, safe relative file path or path hash, line span, safe metadata hash.

If only fallback identity is available, classify no stronger than `NeedsReviewDiff`.

Single-index endpoint keys must not reuse the combined client/server endpoint key format. Use:

```text
endpoint:{sourceLabel}:{endpointKind}:{normalizedMethod}:{normalizedPathKey}:{handlerIdentityOrNone}
```

Combined endpoint keys continue to use the existing combined endpoint identity model.

## Diff Engine

Algorithm:

1. Detect input kind and schema.
2. Project before and after snapshots.
3. Validate source identity and coverage.
4. Apply selectors.
5. Group records by stable key.
6. Emit added, removed, and changed rows using coverage-aware classification.
7. Emit source/coverage/extractor/gap rows.
8. Optionally delegate path comparison to combined diff/path logic.
9. Apply deterministic caps and truncation gaps.
10. Serialize deterministic Markdown/JSON.

For combined indexes, use existing combined diff models where possible. The preferred implementation shape is to reuse or expose no-write combined read/comparison helpers, then section-map their output. For single indexes, project into the same one-source comparison model used by combined diff where practical rather than building an unrelated diff engine.

Combined section mapping:

| Existing combined output | Snapshot output |
| --- | --- |
| `SourceDiffs` | `sourceDiffs` |
| `CoverageDiffs` | `coverageDiffs` |
| `EndpointDiffs` | `endpointDiffs` |
| `SurfaceDiffs` | `surfaceDiffs` |
| `EdgeDiffs` | `graphDiffs` |
| `PathDiffs` | `pathDiffs` |
| `Gaps` | `gaps` |

Snapshot-specific arrays:

| Snapshot array | Source |
| --- | --- |
| `contractShapeDiffs` | single-index projector in v1; combined indexes require an explicit future projector |
| `gapDiffs` | single-index `AnalysisGap` fact comparison in v1; combined indexes require an explicit future projector |
| `extractorVersionDiffs` | manifest/source extractor-version comparison |

If a requested snapshot-specific array cannot be populated from the input schema or delegated engine, keep the array empty and emit an explicit availability gap such as `EvidenceKindUnavailableForSnapshotDiffV1`. Do not treat an unavailable array as no changed evidence.

For combined indexes in v1, `contractShapeDiffs` and `gapDiffs` are unavailable unless a combined projector is explicitly added. `contractShapeDiffs` for single indexes should use declared type/member facts such as `TypeDeclared`, `PropertyDeclared`, and `MethodDeclared` with stable keys based on source label, containing type, member name, signature or arity when available, and rule ID. `gapDiffs` for single indexes should use `AnalysisGap` facts with stable keys based on source label, gap code or fact type, safe path hash or relative path, line span, and rule ID.

## Classifications

Suggested closed classification set:

| Classification | Meaning |
| --- | --- |
| `Added` | evidence exists only after and coverage supports the conclusion |
| `Removed` | evidence exists only before and coverage supports the conclusion |
| `ChangedEvidence` | same stable identity but safe metadata/provenance changed |
| `AddedWithBeforeGap` | appears after, but before coverage/gaps prevent a strong addition claim |
| `RemovedWithAfterGap` | missing after, but after coverage/gaps prevent a strong removal claim |
| `NeedsReviewDiff` | syntax/name/hash/fallback identity requires review |
| `NoSnapshotDiffEvidence` | no comparable changes under credible coverage |
| `SelectorNoMatch` | selectors match neither snapshot |
| `TruncatedByLimit` | output or traversal caps omitted rows |
| `UnknownAnalysisGap` | identity/schema/coverage/metadata gaps prevent a credible conclusion |

When combined delegation produces an existing `NoDiffEvidence` gap, snapshot diff should render the report-level result as `NoSnapshotDiffEvidence` and preserve the combined rule ID as supporting evidence. Same-SHA divergent evidence is not a separate row classification; affected `ChangedEvidence` rows carry a `SameCommitShaDivergentEvidence` note with rule ID `snapshot.diff.identity.v1`.

Confidence mapping is part of the output contract:

| Classification | Confidence |
| --- | --- |
| `Added`, `Removed`, `ChangedEvidence` with Tier1/Tier2 evidence and credible coverage | `high` |
| `Added`, `Removed`, `ChangedEvidence` with Tier3 evidence | `review` |
| `AddedWithBeforeGap`, `RemovedWithAfterGap` | `medium` |
| `NeedsReviewDiff` | `review` |
| `NoSnapshotDiffEvidence`, `SelectorNoMatch`, `TruncatedByLimit`, `UnknownAnalysisGap` | `unknown` |

## Path Comparison

Path comparison is opt-in.

For combined indexes, `--include-paths` reuses existing `tracemap diff --include-paths` method-level semantics and limits. It must not shell out to the CLI:

- default `maxDepth = 8`;
- default `maxPaths = 100`;
- default `maxFrontier = 10000`;
- stable path signatures;
- truncation gaps when limits are hit.

For single indexes, v1 rejects `--include-paths` because the current traversal layer is combined-index based.

## Safety And Redaction

Reports must use shared redaction helpers where available:

- no raw SQL;
- no raw config values;
- no connection strings;
- no raw URLs;
- no source snippets;
- no local absolute paths;
- no private repository names in rendered report metadata;
- Markdown escape user-controlled cells.

Raw properties can be used only to derive safe hashes or safe structured metadata. Unsafe input values should be omitted or hashed.

## JSON Shape

Top-level JSON:

```json
{
  "reportType": "snapshot-diff-by-sha",
  "version": "1.0",
  "reportCoverage": "Full|Partial|Unknown",
  "query": {},
  "beforeSnapshot": {},
  "afterSnapshot": {},
  "summary": {},
  "sourceDiffs": [],
  "coverageDiffs": [],
  "endpointDiffs": [],
  "contractShapeDiffs": [],
  "surfaceDiffs": [],
  "graphDiffs": [],
  "gapDiffs": [],
  "extractorVersionDiffs": [],
  "pathDiffs": [],
  "gaps": [],
  "limitations": []
}
```

All arrays and metadata entries must be sorted. No timestamps or machine-local paths should be emitted.

For combined inputs, this JSON shape is a stable snapshot wrapper around the delegated combined diff result. `graphDiffs` are mapped from combined edge diffs. Empty snapshot-specific arrays are allowed only when paired with an availability gap or limitation explaining why that evidence kind was unavailable.

## Markdown Shape

Section order:

1. Summary
2. Query
3. Snapshot Identity
4. Source And Coverage Changes
5. Endpoint Changes
6. Contract Shape Changes
7. Surface Changes
8. Graph Changes
9. Analysis Gap Changes
10. Extractor Version Changes
11. Path Changes
12. Gaps
13. Limitations

Rows should be capped deterministically with visible truncation notices.

## Rules

Expected rule catalog entries for implementation:

- `snapshot.diff.source.v1`
- `snapshot.diff.coverage.v1`
- `snapshot.diff.evidence.v1`
- `snapshot.diff.identity.v1`
- `snapshot.diff.schema.v1`

When combined path/diff evidence is delegated, preserve existing `combined.diff.*` and `combined.paths.*` rule IDs as supporting rules.

## Tests

Implementation should include focused tests for:

- single-index endpoint/symbol fact change;
- combined-index delegation;
- source identity conflict and `--allow-identity-mismatch`;
- unknown commit SHA;
- same SHA but changed evidence warning;
- reduced coverage downgrade;
- row ID churn avoidance;
- extractor-version changes;
- malformed metadata gaps;
- unsafe value redaction;
- path comparison opt-in;
- byte-stable Markdown/JSON;
- read-only input database behavior.
- manifest JSON identity derivation matching combined hashed identity rendering;
- single-index comparison with missing language metadata;
- mixed single/combined rejection;
- combined edge-to-graph mapping;
- unavailable `contractShapeDiffs`, `gapDiffs`, or `extractorVersionDiffs` producing gaps;
- `--scope` mapping for coverage and graph;
- identity conflict reports redacting raw URLs and repository names;
- deterministic confidence mapping.

## PR Slices

Recommended implementation sequence:

1. Spec and model tests for source/commit validation.
2. Single-index projector and basic Markdown/JSON output.
3. Combined-index delegation to existing diff.
4. Graph/surface/contract-shape evidence expansion.
5. Path opt-in integration.
6. Release-review integration follow-up.
