# Combined Change Impact Design

## Overview

Add a deterministic change-impact query layer over two combined TraceMap snapshots.

The workflow becomes:

```bash
tracemap impact \
  --before before-combined.sqlite \
  --after after-combined.sqlite \
  --out impact-report
```

The command reads both combined databases, runs the same stable evidence comparison as `tracemap diff`, optionally gathers bounded before/after path context, and emits Markdown plus JSON. It is an explanation layer over existing static evidence. It does not run applications, scan repositories, infer business importance, or call AI services.

## Goals

- Turn combined snapshot diffs into reviewable static change context.
- Reuse `tracemap diff` and `tracemap paths` semantics so classifications do not drift.
- Explain changed endpoints, dependency surfaces, edges, coverage, and optional paths with evidence rows.
- Show before/after static reachability context when it is bounded and credible.
- Preserve source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, fact IDs, edge IDs, and limitations.
- Be deterministic, testable, and safe for public reports.

## Non-Goals

- No runtime impact proof.
- No production telemetry, tracing, auth, CORS, routing middleware, deployment, proxy, or customer-usage inference.
- No runtime dependency injection, reflection, serializer, dynamic dispatch, or branch feasibility resolution.
- No SQL execution or schema validation.
- No source-code text diffing.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No web UI in this slice.
- No mutation of combined indexes.

## Command Shape

```text
tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path> [options]
```

Options:

```text
--format <markdown|json>
--scope <all|sources|coverage|endpoints|surfaces|edges|paths>[,...]
--source <label>
--endpoint "<METHOD> <PATH_KEY>"
--surface <sql-query|http-route|http-client|package-config>
--surface-name <text>
--include-paths
--max-impact-items <n>
--max-paths-per-item <n>
--max-depth <n>
--max-frontier <n>
--max-gaps <n>
--allow-identity-mismatch
--exit-code
```

Output behavior should match existing report/path/diff conventions:

- File output defaults to Markdown.
- `--format json` with file output writes JSON.
- Directory output writes `impact-report.md` and `impact-report.json`.
- A non-existing output path with no extension is treated as a directory.
- Inputs are opened read-only.
- Non-combined indexes are rejected with a clear error.

`--exit-code` returns `1` only when requested and impact items are present. Gaps without impact items do not force non-zero exit unless a future option explicitly asks for strict gaps.

## Product Semantics

The report should avoid bare "impacted" language where it could be read as runtime proof. Preferred wording:

- "static impact evidence"
- "changed evidence"
- "reachable static context"
- "path context changed"
- "coverage-relative"
- "needs review"
- "analysis gap"

Avoid:

- "this endpoint is impacted" without qualification
- "no impact" when coverage is reduced
- "calls at runtime"
- "database/table changed" from SQL evidence alone

## High-Level Flow

1. Validate options.
2. Normalize scopes and selectors.
3. Open both combined indexes read-only.
4. Run the diff projector/comparer over selected scopes.
5. Convert eligible diff rows into impact item candidates.
6. Apply source identity, coverage, duplicate identity, selector, and truncation caveats.
7. For selected impact items, gather bounded before/after path context.
8. Classify each impact item.
9. Sort and cap impact items and gaps.
10. Render deterministic Markdown and JSON.

## Code Placement

Suggested layout:

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedChangeImpact.cs
    CombinedChangeImpactModels.cs
    CombinedImpactPathContext.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedChangeImpactTests.cs
```

`TraceMap.Reporting` remains the right home because the command is a report/query layer over combined evidence.

If existing diff/path services are too write-oriented, first extract internal reusable APIs:

- `CombinedDependencyDiffer.BuildReportAsync(...)` or equivalent report-returning method without writing files.
- `CombinedDependencyPathReporter.BuildReportAsync(...)` or a path-query inventory method without writing files.
- Shared output writing, safe rendering, hashing, and format normalization helpers.

The refactor must preserve current `report`, `paths`, and `diff` output behavior before `impact` behavior lands.

## Data Sources

The MVP should not read raw source files. It should read only combined snapshot data and existing derived projections:

| Source | Purpose |
| --- | --- |
| `index_sources` | source labels, scan IDs, commit SHAs, coverage, identity |
| `combined_facts` | endpoint and dependency surface evidence |
| `combined_symbols` | symbol identities where available |
| `combined_fact_symbols` | fact-to-symbol attachment |
| `combined_dependency_edges` | dependency edge summary |
| precise edge tables | call/create/relationship/argument/parameter-forward details when path context uses them |
| diff projector | stable changed evidence rows |
| path query engine | bounded before/after static reachability context |

`endpoint_matches` should remain reserved unless a separate ownership spec defines persisted derived endpoint rows. This command should reuse the in-memory endpoint matching used by reports/paths.

## Report Model

Suggested models:

```csharp
public sealed record CombinedChangeImpactReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedImpactQuery Query,
    CombinedDiffSnapshotInfo BeforeSnapshot,
    CombinedDiffSnapshotInfo AfterSnapshot,
    CombinedImpactSummary Summary,
    IReadOnlyList<CombinedImpactItem> ImpactItems,
    IReadOnlyList<CombinedImpactGap> Gaps,
    IReadOnlyList<string> Limitations);
```

```csharp
public sealed record CombinedImpactItem(
    string ImpactId,
    string ChangeType,
    string Classification,
    string Confidence,
    string EvidenceKind,
    string SourceLabel,
    string StableKey,
    string DiffRuleId,
    string ImpactRuleId,
    CombinedDiffEvidence? Before,
    CombinedDiffEvidence? After,
    CombinedImpactPathContext PathContext,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedImpactNote> Notes);
```

```csharp
public sealed record CombinedImpactPathContext(
    string Classification,
    IReadOnlyList<CombinedImpactPathSummary> BeforePaths,
    IReadOnlyList<CombinedImpactPathSummary> AfterPaths,
    IReadOnlyList<CombinedImpactGap> Gaps);
```

Impact output should keep full path details out of Markdown by default when there are many paths, but JSON should preserve path IDs and enough path evidence to audit the conclusion. The command may include full path node/edge arrays for the top capped paths when safe and deterministic.

## Stable IDs

Impact IDs should be deterministic and independent of row order:

```text
impact:<hash(changeType + classification + evidenceKind + stableKey + diffRuleId + impactRuleId)>
```

Path-context IDs should hash:

```text
before/after + impactId + pathSignature + pathClassification
```

Do not use:

- local absolute paths
- raw repository URLs
- raw SQL
- SQLite row IDs alone
- current timestamp
- arbitrary iteration order

## Classification Rules

Suggested rule IDs:

| Rule ID | Purpose |
| --- | --- |
| `combined.impact.source.v1` | Source and coverage impact rows. |
| `combined.impact.endpoint.v1` | Endpoint change impact rows. |
| `combined.impact.surface.v1` | Dependency surface change impact rows. |
| `combined.impact.edge.v1` | Dependency edge change impact rows. |
| `combined.impact.path.v1` | Path signature and path context impact rows. |
| `combined.impact.selector.v1` | Selector no-match and ignored selector gaps. |
| `combined.impact.truncation.v1` | Impact item and path-context truncation gaps. |

Suggested classifications:

| Classification | Meaning |
| --- | --- |
| `StaticImpactEvidence` | Changed evidence has credible static support and bounded path context. |
| `ProbableStaticImpact` | Changed evidence is strong structural evidence, but path context is absent or partial. |
| `NeedsReviewImpact` | Changed evidence depends on syntax/textual, ambiguous, duplicate, or name-only evidence. |
| `ReachabilityChanged` | Before/after path context changed under credible source identity and coverage. |
| `ReachabilityEvidenceChanged` | Comparable before/after paths exist but path evidence changed. |
| `NoImpactEvidence` | Comparable changes exist but no static impact context is found under full credible coverage. |
| `SelectorNoMatch` | User selectors matched no comparable evidence. |
| `UnknownAnalysisGap` | Gaps prevent a credible conclusion. |
| `TruncatedByLimit` | Output or path search was capped. |

Confidence mapping:

| Classification | Confidence |
| --- | --- |
| `StaticImpactEvidence` | High |
| `ReachabilityChanged` | Medium |
| `ReachabilityEvidenceChanged` | Medium |
| `ProbableStaticImpact` | Medium |
| everything else | Low |

Classifications must be downgraded when:

- source identity is conflicted or unverified
- commit SHA is unknown
- the opposite snapshot has reduced coverage for added/removed evidence
- evidence is Tier3 syntax/textual
- duplicate stable identities exist
- path context is truncated before it can support the item

## Path Context Strategy

Path context is expensive, so it must be bounded.

Default behavior:

- `impact` may include direct context from diff rows without running path search.
- `--include-paths` enables path context gathering.
- `--max-impact-items` defaults to 100.
- `--max-paths-per-item` defaults to 5.
- `--max-depth` and `--max-frontier` should reuse `tracemap paths` defaults unless specified.

Selector mapping:

| Changed evidence | Before/after path query |
| --- | --- |
| endpoint | `--from-endpoint "<METHOD> <PATH_KEY>"` |
| surface | `--to-surface <kind> --surface-name <safe identity>` when supported |
| edge | `--from-symbol <source symbol>` when available |
| source/coverage | no path context by default; summarize coverage/identity only |
| path diff | summarize path signature and supporting path evidence |

If a query cannot be represented by the current path selector vocabulary, emit `PathContextUnavailable` instead of inventing a weaker query.

## Markdown Output

Sections:

1. Summary
2. Query
3. Snapshot Sources
4. Impact Items
5. Path Context
6. Gaps
7. Limitations

Markdown should:

- show partial coverage near the top
- group items by classification, then evidence kind
- render before/after evidence side-by-side where useful
- render path context as capped before/after lists
- escape Markdown table/link delimiters
- use safe path rendering
- never show raw SQL, raw snippets, config values, connection strings, raw URLs, or local absolute paths

## JSON Output

Top-level JSON:

```json
{
  "reportType": "combined-change-impact",
  "version": "1.0",
  "reportCoverage": "ReducedCoverage",
  "coverageWarnings": [],
  "query": {},
  "beforeSnapshot": {},
  "afterSnapshot": {},
  "summary": {},
  "impactItems": [],
  "gaps": [],
  "limitations": []
}
```

Determinism rules:

- no timestamps
- sorted arrays
- sorted metadata pairs
- stable hashes
- deterministic truncation rows
- explicit empty arrays
- safe/hashing policy for sensitive fields

## Error Handling

Fail fast for:

- missing `--before`, `--after`, or `--out`
- non-combined input
- invalid scope value
- invalid endpoint selector format
- invalid surface kind
- incompatible output format
- impossible numeric caps

Emit gaps rather than fail for:

- selector no match
- optional path context unavailable
- reduced coverage
- duplicate stable identities
- source identity mismatch when explicitly allowed
- truncation
- missing optional precision tables when fallback comparison remains possible

## Validation

Required before merge:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When implementation touches path context, also run the public combined-path smoke:

```bash
./scripts/smoke-combined-paths.sh
```

## Open Questions

- Should path context default to off for all users, or should a small direct endpoint/surface context be on by default?
- Should impact reports support a `--diff-json <path>` input to reuse a previously generated diff report?
- Should `NoImpactEvidence` be emitted per changed item, or only as a report-level gap when no items exist?
- Should full path node/edge details be included in Markdown, or only in JSON?
- Should `impact` eventually subsume some `diff --include-paths` behavior, or should both commands stay distinct?

