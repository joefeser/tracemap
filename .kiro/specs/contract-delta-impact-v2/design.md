# Contract Delta Impact V2 Design

## Overview

Contract Delta Impact V2 extends the existing `tracemap reduce` workflow from deterministic single-index name matching into a richer evidence-backed impact query over single or combined TraceMap indexes.

The command remains a reducer: it consumes a contract delta and an already-built index. It does not scan repositories, diff source files, execute code, connect to databases, call package registries, or use AI classification.

```text
contract-delta-v2.json + index.sqlite
  -> contract reference normalization
  -> evidence matching against facts/symbols/surfaces
  -> optional path/reverse context for combined indexes
  -> Markdown + JSON impact report
```

The main product shift is from "does this changed name appear in facts?" to "what structured contract reference changed, what indexed evidence supports review, and where does coverage stop us from making a stronger statement?"

## Goals

- Preserve existing `tracemap reduce` compatibility.
- Add a structured v2 contract-delta model.
- Support single and combined indexes.
- Match contract references to facts, symbols, endpoints, DTOs, dependency surfaces, SQL/package evidence, dependency edges, paths, and reverse roots where available.
- Emit deterministic, safe Markdown and JSON.
- Keep all conclusions rule-backed, evidence-tiered, span-backed, commit-aware, and coverage-relative.

## Non-Goals

- No runtime impact proof.
- No source-code diffing.
- No runtime dependency-injection binding certainty.
- No reflection/dynamic dispatch certainty.
- No SQL execution or schema validation.
- No package compatibility, vulnerability, or license assessment.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No hidden risk score or business criticality inference.

## Current State

| Area | Current behavior | V2 gap |
| --- | --- | --- |
| Basic reducer | Parses legacy contract delta shape and emits Markdown findings from one index. | Needs structured v2 input, JSON output, richer evidence model, and combined-index support. |
| Single-language indexes | Store scan manifest, facts, evidence tiers, spans, commit SHA, extractor versions, and reducer-compatible names. | Need kind-specific matching for methods, endpoints, packages, SQL, and dependency surfaces. |
| Combined indexes | Preserve source provenance, dependency surfaces, path/reverse/diff/impact evidence. | Need contract-delta matching that can query combined sources and optional path/reverse context. |
| Rules | `contract.delta.reduce.v1` documents basic reducer behavior. | Need `contract.delta.impact.v2` and supporting limitations before implementation. |

## Command Shape

Keep the existing command and add v2 behavior by input detection and optional flags:

```text
tracemap reduce --index <index.sqlite> --contract-delta <delta.json> --out <path> [options]
```

Potential options for v2 implementation:

```text
--format <markdown|json>
--scope <all|type|property|method|endpoint|package|schema|sql-table|sql-column|sql-query|dependency-surface>[,...]
--source <label>
--change-id <id>
--kind <type|property|method|endpoint|package|schema|sql-table|sql-column|sql-query|dependency-surface>
--surface <kind>
--endpoint "<METHOD> <PATH_KEY>"
--include-paths
--include-reverse
--max-findings <n>
--max-evidence-rows <n>
--max-paths-per-change <n>
--max-context-queries <n>
--max-gaps <n>
--exit-code
```

Output behavior should follow existing report/diff/impact conventions:

- File output defaults to Markdown.
- Directory output writes `impact-report.md` and `impact-report.json`.
- `--format json` with file output writes JSON.
- Inputs are opened read-only.
- No generated timestamps appear in deterministic report payloads.
- `--scope` filters by v2 change kind. It does not filter by impact classification group.
- `--include-paths` and `--include-reverse` require a combined index in v2 and are rejected for single-language indexes with a clear message.
- `--format markdown` with directory output still writes both `impact-report.md` and `impact-report.json`; Markdown is the primary artifact.
- `--format json` with file output writes JSON to the given file even if the extension is not `.json`.

Default caps:

| Option | Default |
| --- | --- |
| `--max-findings` | `100` |
| `--max-evidence-rows` | `500` |
| `--max-paths-per-change` | `5` |
| `--max-context-queries` | `50` total path/reverse queries |
| `--max-gaps` | `1000` |

## V2 Contract Delta Model

Suggested JSON shape:

```json
{
  "version": "contract-delta-v2",
  "contract": "CustomerProfileApi",
  "source": {
    "kind": "openapi",
    "name": "customer-profile",
    "commitSha": "0123456789abcdef0123456789abcdef01234567"
  },
  "changes": [
    {
      "id": "change-001",
      "kind": "property",
      "changeType": "removed",
      "reference": {
        "typeName": "CustomerProfileResponse",
        "propertyName": "primaryEmail",
        "fullyQualifiedName": "Contracts.CustomerProfileResponse.primaryEmail"
      },
      "old": { "type": "string", "required": true },
      "new": null,
      "severity": "breaking"
    }
  ]
}
```

Reference models by kind:

| Kind | Key fields |
| --- | --- |
| `type` | `typeName`, `fullyQualifiedName`, `namespace`, `assembly`, `packageName` |
| `property` | `typeName`, `propertyName`, `fullyQualifiedName`, `jsonName`, `schemaName` |
| `method` | `typeName`, `methodName`, `signature`, `parameterTypes`, `returnType` |
| `endpoint` | `method`, `path`, `normalizedPathKey`, `operationId`, `routeName` |
| `package` | `ecosystem`, `packageName`, `oldVersion`, `newVersion`, `versionRange` |
| `schema` | `schemaName`, `typeName`, `fieldName`, `jsonPointer` |
| `sql-table` | `tableName`, `schemaName`, `databaseName`, `sourceKind` |
| `sql-column` | `tableName`, `columnName`, `schemaName`, `sourceKind` |
| `sql-query` | `queryShapeHash`, `operationName`, `tableName`, `columnNames`, `sqlSourceKind` |
| `dependency-surface` | `surfaceKind`, `surfaceName`, `stableKey`, `sourceKind` |

The reducer should normalize references into safe matcher tokens. It must prefer structured fields over display strings. Legacy deltas can be converted into v2-like `type` or `property` references with `inputCompatibility=LegacyContractDeltaV1`.

### Legacy V1 Compatibility Adapter

Legacy v1 input has `contract`, `source`, and `changes[]` with a flat `element` string. The adapter maps legacy fields into v2-like internal references without pretending the input was fully structured:

| V1 input | V2 internal reference |
| --- | --- |
| `contract` | Input contract name. |
| `source` | Input source hint. |
| `changes[n].changeType` | Same change type when recognized. |
| `changes[n].element = Type.member` | `kind=property` by default, `reference.typeName=Type`, `reference.propertyName=member`, `reference.fullyQualifiedName=Type.member`, `inputCompatibility=LegacyContractDeltaV1`. |
| `changes[n].element = Name` | Ambiguous name-only legacy reference; preserve v1 matching behavior but mark specificity as `NameOnlyLegacy`. |
| method-like syntax or explicit method metadata if later added | `kind=method`. |

Legacy two-part `Type.member` values remain property-like because the current reducer was built primarily around DTO/property/type deltas. If method intent is ambiguous, the adapter may add a method candidate internally, but the finding must report that legacy input was ambiguous. Compatibility rows preserve `contract.delta.reduce.v1` in the report header or finding metadata.

V2-native inputs are stricter than the legacy adapter. Ambiguous or missing structured references emit input gaps rather than free-text matches.

### Minimum Required Reference Fields

| Kind | Minimum required fields |
| --- | --- |
| `type` | one of `fullyQualifiedName`, `typeName` |
| `property` | `propertyName` plus one of `typeName`, `fullyQualifiedName`, `schemaName`, or `jsonName`; name-only property references are review-tier |
| `method` | `methodName` plus optional `typeName` or `signature`; method-name-only is review-tier |
| `endpoint` | `normalizedPathKey` or `path`; `method` is required for strong endpoint matches and path-only matches are review-tier |
| `package` | `ecosystem` and `packageName` for strong matches; `packageName` alone is review-tier |
| `schema` | `schemaName` or `jsonPointer` |
| `sql-table` | `tableName` |
| `sql-column` | `columnName` plus `tableName` for strong matches; column-only is review-tier |
| `sql-query` | one of `queryShapeHash`, `textHash`, or `tableName`; hash-based matches are stronger than table-only matches |
| `dependency-surface` | `surfaceKind` plus one of `stableKey`, `surfaceName`, `queryShapeHash`, `textHash`, or package/endpoint-specific identity |

## Matching Pipeline

1. Parse and validate contract delta.
2. Detect index kind: single index or combined index.
3. Load scan/source coverage and known gaps.
4. Normalize each change reference into matcher keys.
5. Query candidate evidence by kind-specific matchers.
6. Deduplicate evidence by stable fact/symbol/surface identity.
7. Attach coverage and identity caveats.
8. Optionally gather path/reverse context for combined indexes.
9. Classify findings.
10. Render Markdown and JSON.

Generic-name and fan-out downgrade rules must be documented constants. Existing v1 names such as `status`, `type`, and `id` should not silently become strong v2 impact. New kind families need their own heuristics, for example generic endpoint paths like `/status`, generic SQL table names like `users`, and package names without ecosystem identity. The v1 compatibility adapter preserves existing warn-only behavior unless implementation explicitly changes it with regression tests and rule-catalog limitations.

### Single-Index Matchers

Single-index matching reads existing scanner artifacts:

| Evidence source | Usage |
| --- | --- |
| `scan_manifest` | coverage, commit SHA, extractor versions, analysis gaps |
| `facts` | fact type, rule ID, evidence tier, path/span, properties |
| symbol tables when present | stable symbol identities and fully qualified names |
| language-specific reducer keys | `typeName`, `propertyName`, `memberName`, `methodName`, `keyPath`, `targetSymbol`, `containingType` |

Endpoint matching uses exact `normalizedPathKey` where available plus HTTP method when provided. A path-only match without method evidence is review-tier. A method mismatch is not a strong endpoint match and must be reported as a caveat or `NeedsReview`.

### Combined-Index Matchers

Combined-index matching reads combined projections and source provenance:

| Evidence source | Usage |
| --- | --- |
| `index_sources` | source labels, source index IDs, scan IDs, repo identity hashes, commit SHAs, coverage |
| `combined_facts` | endpoint, DTO, package, SQL, HTTP, config, integration facts |
| `combined_symbols` | stable symbol evidence |
| `combined_fact_symbols` | fact-to-symbol attachment |
| `combined_dependency_edges` | SQL view containing package/API/SQL/config edge summaries and fallback edges |
| precise edge/path tables | optional path/reverse context |

Dependency surfaces are computed by the combined report/impact readers rather than read from a persisted surface table. `kind=dependency-surface` must reuse existing combined surface readers instead of introducing a second surface projection. SQL query references map `queryShapeHash` to combined surface `ShapeHash`, `textHash` to `TextHash`, `tableName` to `TableName`, and `columnNames` to `ColumnNames`.

The implementation should reuse existing combined report/path/reverse readers and safe rendering helpers where possible.

## Evidence Model

Suggested internal report model:

```csharp
public sealed record ContractDeltaImpactReport(
    string ReportType,
    string Version,
    ContractDeltaInputSummary Input,
    ContractDeltaImpactQuery Query,
    ContractDeltaIndexSummary Index,
    ContractDeltaImpactSummary Summary,
    IReadOnlyList<ContractDeltaImpactFinding> Findings,
    IReadOnlyList<ContractDeltaImpactGap> Gaps,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<string> Limitations);
```

```csharp
public sealed record ContractDeltaImpactFinding(
    string FindingId,
    string ChangeId,
    string ChangeKind,
    string ChangeType,
    string Classification,
    string Confidence,
    string RuleId,
    string EvidenceTier,
    string? SourceLabel,
    string? SourceIndexId,
    string? CommitSha,
    string DisplayName,
    IReadOnlyList<ContractDeltaEvidenceRow> Evidence,
    IReadOnlyList<ContractDeltaContextPath> Paths,
    IReadOnlyList<ContractDeltaContextRoot> ReverseRoots,
    IReadOnlyList<ContractDeltaImpactGap> Gaps,
    IReadOnlyList<string> Limitations);
```

```csharp
public sealed record ContractDeltaEvidenceRow(
    string EvidenceId,
    string EvidenceKind,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorVersion,
    string? SourceLabel,
    string? CommitSha,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingSymbolIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyDictionary<string, string> SafeMetadata);
```

All IDs are deterministic hashes over stable inputs. Do not use timestamps, SQLite row IDs alone, local absolute paths, raw URLs, raw SQL, snippets, or property bag iteration order.

`FindingId` should be a documented SHA-256 truncation over stable values such as:

```text
changeId | reportType | sourceLabel | classification | evidenceKind | matchedFactType | safeFilePath | startLine
```

The implementation may choose a comparable stable composition, but it must document exact inputs before merge and tests must assert determinism.

## Classification

Classification vocabulary is selected by report type.

`ContractDeltaImpactSingleV2` uses the v1-compatible reducer vocabulary:

| Classification | Meaning |
| --- | --- |
| `DefiniteImpact` | Tier1 semantic evidence directly matches the changed contract reference. |
| `ProbableImpact` | Strong Tier2 structural evidence matches, such as DTO, endpoint, SQL/package/config, or framework evidence. |
| `NeedsReview` | Tier3 syntax/textual, name-only, ambiguous, generic, duplicate, or high-fan-out evidence matches. |
| `NoEvidenceFullCoverage` | No match and full semantic coverage is credible. |
| `NoEvidenceReducedCoverage` | No match and coverage is reduced or partial. |
| `UnknownAnalysisGap` | Relevant gaps prevent credible conclusion. |

`ContractDeltaImpactCombinedV2` uses the combined-impact vocabulary already established by `tracemap impact`:

| Classification | Meaning |
| --- | --- |
| `StaticImpactEvidence` | Strong static evidence with credible coverage and optional path/reverse support. |
| `ProbableStaticImpact` | Strong structural evidence without enough path/reverse context for stronger language. |
| `NeedsReviewImpact` | Syntax/textual, ambiguous, duplicate, name-only, hash-only, or identity-limited evidence. |
| `UnknownAnalysisGap` | Gaps prevent credible conclusion. |
| `NoImpactEvidence` | No comparable combined evidence was found under credible coverage. |
| `SelectorNoMatch` | User selectors matched no comparable evidence. |
| `TruncatedByLimit` | Findings or context were capped. |
| `PathContextUnavailable` | No safe path selector can be derived or path context was not supported. |
| `ReverseContextUnavailable` | No safe reverse selector can be derived or reverse context was not supported. |

Confidence mapping:

| Classification | Confidence |
| --- | --- |
| `DefiniteImpact`, `StaticImpactEvidence` | High |
| `ProbableImpact`, `ProbableStaticImpact` | Medium |
| everything else | Low |

Do not expose both vocabularies as peer classifications in one finding. If compatibility information is useful, include it as metadata such as `legacyClassification`, not as a second authoritative classification.

## Rule IDs

Expected rule behavior:

| Rule ID | Purpose |
| --- | --- |
| `contract.delta.reduce.v1` | Existing legacy reducer compatibility. |
| `contract.delta.impact.v2` | V2 contract-delta parsing, matching, finding classification, and report rows. |
| `contract.delta.input.v2` | Input validation and compatibility gaps when separated from primary impact rows. |
| `contract.delta.context.v2` | Path/reverse context rows and `PathContextUnavailable`/`ReverseContextUnavailable` gaps when separated from primary findings. |

Supporting evidence rows preserve original fact rule IDs such as endpoint, SQL, package, DTO, symbol, call-edge, and language adapter rules. Combined context preserves existing `combined.paths.*`, `combined.reverse.*`, `combined.impact.*`, and `combined.diff.*` rule IDs where reused.

`contract.delta.impact.v2` is required for the first implementation PR and should be the first implementation commit. `contract.delta.input.v2` and `contract.delta.context.v2` are required only if the implementation emits separate input/context rows rather than using `contract.delta.impact.v2` for all v2 rows. Every new or changed rule must document limitations in `rules/rule-catalog.yml` before implementation code merges.

## Coverage And Identity Caveats

A finding can be no stronger than review-tier when:

- source identity is missing, unverified, duplicated, or conflicting;
- scan coverage is reduced and absence of evidence matters;
- semantic analysis failed and only syntax fallback exists;
- optional combined precision tables are missing;
- evidence is hash-only;
- a selector derives from a generic display name;
- a match has high fan-out;
- path/reverse context is capped or unavailable.

`NoEvidenceFullCoverage` requires all of the following:

- successful full semantic coverage for the relevant scan scope;
- known commit SHA;
- no relevant `AnalysisGap` facts;
- no source identity warnings for combined sources;
- no missing optional precision data needed for the selected kind.

## Path And Reverse Context

Path and reverse context are optional because they can be expensive and are not always derivable from a contract reference. They are available only for combined indexes in v2.

Path selector derivation examples:

| Change kind | Path context selector |
| --- | --- |
| `endpoint` | endpoint method/path key as path root |
| `type`/`method` | matched symbol as root or intermediate node |
| `property` | matched member/DTO/serializer fact attachment |
| `sql-table`/`sql-column`/`sql-query` | terminal `sql-query` or `sql-persistence` surface |
| `package` | package/config/dependency surface |
| `dependency-surface` | selected terminal surface |

Reverse selector derivation examples:

| Change kind | Reverse context selector |
| --- | --- |
| `sql-table`, `sql-column`, `sql-query` | reverse from SQL surface to endpoints/symbols/sources |
| `package` | reverse from package surface to sources/symbols |
| `endpoint` | reverse from route/client surface where supported |

When no safe selector can be derived, emit `PathContextUnavailable` or `ReverseContextUnavailable` rather than attempting fuzzy traversal.

Only symbol-identity-backed matches or stable-key dependency surface matches may seed path/reverse traversal. Name-only, syntax-only, ambiguous, generic, or high-fan-out matches cannot seed traversal even when they produce a finding.

## Markdown Report

Suggested sections:

1. Summary
2. Input Delta
3. Index Coverage
4. Findings By Change
5. Evidence Rows
6. Path Context
7. Reverse Context
8. Gaps
9. Limitations

Markdown must be safe to publish. It must not display raw SQL, source snippets, literal values, config values, connection strings, raw repository URLs, or local absolute paths.

When neither `--include-paths` nor `--include-reverse` was requested, Path Context and Reverse Context may be replaced by one line stating that reachability context was not requested.

## JSON Report

The JSON report is the automation contract. It should be byte-stable and include enough evidence to audit every finding.

Required top-level fields:

```text
reportType
version
input
query
index
summary
findings
gaps
coverageWarnings
limitations
```

Sort all arrays by stable IDs and all metadata keys ordinally. Use empty arrays for repeated fields with no values and `null` for missing scalar values.

Required ordering:

- findings: `changeId`, `sourceLabel`, classification ordinal, `findingId`;
- evidence rows: evidence-tier strength, safe file path, start line, `evidenceId`;
- path/reverse context rows: stable path/root ID;
- gaps: rule ID, classification, stable gap ID.

## Safety

- Never store or render raw SQL text by default.
- Never render source snippets by default.
- Never render connection strings, config values, secrets, raw URLs, or local absolute paths.
- Hash unsafe identifiers only when the hash is useful for deterministic comparison; otherwise omit.
- Preserve file spans and snippet hashes rather than snippet bodies.
- Keep all report limitations explicit.
- `./scripts/check-private-paths.sh` must pass for implementation PRs, but it is not a substitute for redaction tests. Path-adjacent fields such as input paths, output paths, source index paths, repository roots, and report display paths must use existing safe path/hash helpers.

## Recommended Implementation Slices

1. Rule catalog skeleton first, then v2 input parser, compatibility adapter, JSON/Markdown report skeleton, and v1 byte-stability guard.
2. Single-index kind-specific matchers for type/property/method first, then endpoint/package/SQL references.
3. Combined-index source provenance, dependency-surface matching, identity/coverage caveats, and deterministic grouping.
4. Optional path/reverse context integration with caps and gaps after single-index and combined-index matching land.
5. Byte-stability, safety, sample deltas, and validation docs.
