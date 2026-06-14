# Multi-Index Portfolio Dependency Report Design

## Overview

Add a deterministic portfolio report layer over many TraceMap indexes.

```text
N single-language indexes and/or N combined indexes
  -> portfolio source identity and coverage validation
  -> dependency surface inventory
  -> cross-source endpoint and shared-surface grouping
  -> optional before/after portfolio diff and impact context
  -> optional bounded path/reverse context
  -> portfolio-report.md + portfolio-report.json
```

The portfolio report is a composition and reporting layer over existing TraceMap evidence. It does not scan source code, mutate indexes, infer runtime topology, or introduce AI-based classification.

## Goals

- Make multi-index TraceMap evidence reviewable without hand-written SQL or one-off scripts.
- Preserve source labels, repo identity, commit SHAs, scan IDs, extractor versions, rule IDs, evidence tiers, file spans, fact IDs, edge IDs, and limitations.
- Reuse existing combined report, dependency surfaces, endpoint alignment, diff, impact, paths, and reverse semantics.
- Provide a safe portfolio-wide dependency inventory and cross-source summary.
- Support reproducible manifest-driven portfolio snapshots.
- Keep Markdown and JSON deterministic and safe for public review.

## Non-Goals

- No repository scanning.
- No index combining side effects or portfolio database persistence in v1.
- No runtime topology, traffic, deployment, ownership, auth, CORS, proxy, DI binding, dynamic dispatch, reflection, branch-feasibility, SQL execution, database schema existence, package compatibility, vulnerability, license, or business impact inference.
- No release approval, CI gate, or merge recommendation.
- No LLM calls, embeddings, vector databases, prompt-based classification, generated summaries, or hidden risk scores.
- No raw SQL, raw snippets, literal values, config values, connection strings, raw URLs, raw secrets, private paths, or local absolute paths in public output.
- No replacement for existing `combine`, `report`, `diff`, `impact`, `paths`, `reverse`, or future release-review commands.

## Command Shape

Single snapshot:

```text
tracemap portfolio --out <path> [inputs] [options]
```

Inputs:

```text
--index <path> --label <label>
--manifest <portfolio.json>
```

Before/after comparison:

```text
tracemap portfolio \
  --before-manifest <portfolio.json> \
  --after-manifest <portfolio.json> \
  --out <path>
```

Options:

```text
--format <markdown|json>
--source <label>
--group <tag>
--surface <http-client|http-route|sql-query|package|config|symbol|edge>
--surface-name <text>
--include-impact
--include-paths
--include-reverse
--max-sources <n>
--max-surface-rows <n>
--max-endpoint-findings <n>
--max-shared-surfaces <n>
--max-edge-rows <n>
--max-paths <n>
--max-roots <n>
--max-gaps <n>
```

Output behavior:

- File output defaults to Markdown.
- `--format json` with file output writes JSON.
- Directory output writes both `portfolio-report.md` and `portfolio-report.json`.
- A non-existing output path with no extension is treated as a directory.
- Input indexes are opened read-only.
- `--exit-code` is not part of v1. Deterministic CI exit policy is deferred to a follow-up spec.

Input constraints:

- `--manifest` and direct `--index` inputs are mutually exclusive in v1.
- A future mixed-input flag, if added, is named `--allow-mixed-inputs`.
- `--before-manifest` and `--after-manifest` are paired and mutually exclusive with single-snapshot inputs.
- Repeated `--index` inputs require paired `--label` values.
- Duplicate labels are rejected.

## Portfolio Manifest

Suggested schema:

```json
{
  "version": "1.0",
  "portfolioId": "payments-platform",
  "snapshotId": "2026-06-14",
  "inputs": [
    {
      "label": "web-client",
      "indexPath": "indexes/web-client/index.sqlite",
      "expectedRepoIdentity": "repohash:...",
      "expectedCommitSha": "abc123...",
      "group": "frontend",
      "roleTags": ["client"]
    }
  ]
}
```

Rules:

- `indexPath` can be absolute or relative to the manifest file for local resolution.
- `indexPath` must not be emitted in public Markdown/JSON except as a safe hash or sanitized basename if needed.
- `label` is the stable portfolio source label.
- `expectedRepoIdentity` and `expectedCommitSha` are optional validation hints, not replacements for index provenance.
- `group` and `roleTags` are reviewer-provided grouping metadata. They do not prove runtime topology or ownership.
- Metadata output should use canonical `metadata: [{ "key": "...", "value": "..." }]` arrays, not arbitrary dictionaries.

## Proposed Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    PortfolioReportEngine.cs
    PortfolioInputReader.cs
    PortfolioManifestModels.cs
    PortfolioIndexReader.cs
    PortfolioReportModels.cs
    PortfolioEndpointAlignment.cs
    PortfolioSurfaceGrouper.cs
    PortfolioMarkdownWriter.cs
    PortfolioJsonWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    PortfolioReportTests.cs
```

`TraceMap.Reporting` remains the right home because portfolio reporting composes existing evidence and report/query workflows.

Before adding portfolio-specific logic, prefer extracting reusable internal services from current combined report/diff/path/reverse implementations when the alternative would duplicate classification or safe-rendering behavior.

## Data Sources

For single-language indexes:

| Source | Purpose |
| --- | --- |
| scan manifest tables/files where available | scan ID, repo, commit SHA, scanner version, coverage |
| facts table or NDJSON-imported fact storage | dependency surface evidence |
| symbol and edge tables where available | symbols, calls, creates, relationships, argument flow |

Single-language indexes are first-class v1 inputs. Portfolio implementation must add a source-index reader that projects `facts`, symbol, and edge tables into portfolio source-scoped rows. It must not pretend combined-only readers support single-language inventory; if a required single-language table is missing, emit `portfolio.schema.v1` gaps for the affected section.

For combined indexes:

| Source | Purpose |
| --- | --- |
| `index_sources` | expanded source identity, labels, coverage, manifest JSON |
| `combined_facts` | endpoint, SQL/query, package/config, and other surface evidence |
| `combined_symbols` | symbol inventory |
| `combined_fact_symbols` | fact-to-symbol attachments |
| `combined_call_edges` | call graph evidence |
| `combined_object_creations` | object creation evidence |
| `combined_symbol_relationships` | inheritance/interface/override evidence |
| `combined_argument_flows` | direct argument evidence |
| `combined_parameter_forward_edges` | parameter-forwarding evidence |
| `combined_dependency_edges` | edge summary fallback |

`endpoint_matches` should not be treated as source of truth in v1. The report should compute endpoint alignment in memory through an extracted internal endpoint matching helper shared with combined reporting. PR 3 must extract or wrap the current combined report matcher before portfolio alignment is implemented; duplicating endpoint matching logic is out of scope.

## High-Level Flow

1. Parse command-line options.
2. Load direct inputs or manifest entries.
3. Resolve input paths locally while preserving only safe input labels for output.
4. Open indexes read-only and classify each as single-language or combined.
5. Expand combined `index_sources` rows into portfolio source records.
6. Validate repo/commit/source identity against manifest hints.
7. Build source coverage and schema gap inventory.
8. Read dependency facts, edges, and safe metadata from each input.
9. Normalize evidence into portfolio source-scoped records without merging provenance.
10. Run endpoint alignment over normalized HTTP candidates.
11. Group shared portfolio surfaces using deterministic, documented grouping rules.
12. If before/after manifests are provided, run portfolio diff context and optional impact composition.
13. If requested, run bounded path/reverse context through existing workflow APIs.
14. Apply caps, truncation gaps, rollup classification, and deterministic ordering.
15. Render Markdown and JSON.

## Report Model

Suggested root:

```csharp
public sealed record PortfolioReport(
    string ReportType,
    string Version,
    string Mode,
    PortfolioQuery Query,
    PortfolioSnapshot? PortfolioSnapshot,
    PortfolioSnapshot? BeforeSnapshot,
    PortfolioSnapshot? AfterSnapshot,
    PortfolioSummary Summary,
    IReadOnlyList<PortfolioInput> Inputs,
    IReadOnlyList<PortfolioSource> Sources,
    PortfolioSection<PortfolioSourceCoverageRow> SourceCoverage,
    PortfolioSection<PortfolioEndpointFinding> EndpointAlignment,
    PortfolioSection<PortfolioSurfaceRow> DependencySurfaces,
    PortfolioSection<PortfolioEdgeRow> DependencyEdges,
    PortfolioSection<PortfolioSharedSurface> SharedSurfaces,
    PortfolioSection<PortfolioPathContextRow> PathContext,
    PortfolioSection<PortfolioReverseContextRow> ReverseContext,
    PortfolioSection<PortfolioDiffRow> PortfolioDiff,
    PortfolioSection<PortfolioImpactRow> PortfolioImpact,
    PortfolioSection<PortfolioReleaseReviewRow> ReleaseReviewContext,
    IReadOnlyList<PortfolioGap> Gaps,
    IReadOnlyList<string> Limitations);
```

`Inputs` are the raw direct-index or manifest entries. `Sources` are expanded portfolio sources: a combined index is one input but may produce many sources from `index_sources`.

Section status vocabulary:

```text
available
not_requested
unavailable
deferred
truncated
```

Coverage values:

```text
FullEvidenceAvailable
ReducedCoverage
UnknownAnalysisGap
```

Rollup values:

```text
ActionableStaticEvidence
ReviewRecommended
NoActionableEvidence
PartialAnalysis
SelectorNoMatch
UnknownAnalysisGap
TruncatedByLimit
```

Rollup precedence:

1. `UnknownAnalysisGap`
2. `TruncatedByLimit`
3. `ActionableStaticEvidence`
4. `ReviewRecommended`
5. `PartialAnalysis`
6. `SelectorNoMatch`
7. `NoActionableEvidence`

The rollup is a report summary label. It must not replace underlying classifications or imply release approval.

A single higher-precedence row can determine a section rollup even when other rows are review-tier or gap rows. Reports must keep the detail rows visible and limitations must tell reviewers not to rely only on rollup labels.

## Identity and Coverage Logic

Each `PortfolioSource` should carry:

- portfolio source ID;
- user label;
- combined container label when applicable;
- original combined source label when applicable;
- language;
- repo name or repo identity hash;
- commit SHA;
- scan ID;
- scanner and extractor versions;
- analysis level;
- build status;
- manifest gap categories;
- safe scan-root relative path or hash when available.

Coverage is reduced when:

- commit SHA is missing or `unknown`;
- repo identity is missing or conflicts with manifest expectations;
- build status is failed/reduced/unknown;
- analysis level is syntax-only, partial, failed, or unknown;
- language is missing or corrected from scanner metadata;
- manifest known gaps are present;
- required schema or optional precision tables are missing for requested sections;
- caps truncate evidence.

Do not merge sources just because labels, repo names, or package names match. Grouping is an additional view over evidence, not a provenance rewrite.

Duplicate source identity handling:

- If the same scan ID appears through more than one input, emit `DuplicateSourceIdentity`.
- If scan ID is unavailable but repo identity plus commit SHA matches across inputs, emit `DuplicateSourceIdentity`.
- Duplicate copies are excluded from cross-source endpoint alignment and shared-surface grouping so a combined index plus one of its constituent single-language indexes cannot fabricate cross-source coupling.
- Duplicate evidence may still appear as source inventory rows with identity caveats.

## Dependency Surface Normalization

Normalize evidence into `PortfolioSurfaceRow` with:

- `surfaceId`;
- `surfaceKind`;
- `safeDisplayName`;
- `sourceId`;
- `sourceLabel`;
- `containerLabel`;
- `commitSha`;
- `ruleId`;
- `evidenceTier`;
- `filePath`;
- `startLine`;
- `endLine`;
- `factId`;
- `originalFactId`;
- `metadata`.

Surface kind mapping:

| Surface kind | Evidence examples |
| --- | --- |
| `http-client` | `HttpCallDetected` |
| `http-route` | `HttpRouteBinding` |
| `sql-query` | `QueryPatternDetected`, `SqlTextUsed`, `DapperCallDetected`, `SqlCommandDetected` |
| `package` | package declarations, project references, imports, lockfile rows |
| `config` | config keys, environment variable names, connection string names |
| `symbol` | public symbols and framework-specific symbols |
| `edge` | calls, creates, relationships, argument flows, parameter forwarding |

Unsafe property values must be omitted, hashed, or represented as closed-set reason codes.

## Endpoint Alignment

Portfolio endpoint alignment should reuse combined report matching semantics:

- client candidates are HTTP call facts;
- server candidates are HTTP route binding facts;
- same-source candidates are allowed when facts differ;
- exact method and normalized path key gives `MatchedEndpoint`;
- optional-compatible path shape gives `OptionalSegmentMatch`;
- path match with method mismatch gives `MethodMismatch`;
- dynamic or unsafe path evidence gives `DynamicClientUrlNeedsReview`;
- source-local multiple equivalent server candidates gives `AmbiguousMatch`;
- one-sided inventory rows use `ClientCallNoServerEndpoint` and `ServerEndpointNoClientMatch`;
- missing evidence or known gaps use `UnknownAnalysisGap`.

Endpoint finding IDs should be deterministic from:

```text
portfolio version
client source ID
server source ID or null
client fact ID or null
server fact ID or null
classification
HTTP method
normalized path key hash
```

Raw URL fragments must never appear in findings, notes, Markdown, JSON, or stderr.

## Shared Portfolio Surfaces

Shared surfaces are grouped by deterministic safe keys, for example:

- `http-route:<method>:<normalizedPathKey>`;
- `http-client:<method>:<normalizedPathKey>`;
- `sql-table:<normalizedTableName>`;
- `sql-column:<normalizedTableName>:<normalizedColumnName>`;
- `package:<ecosystem>:<packageName>`;
- `config-key:<safeKeyNameOrHash>`;
- `symbol:<language>:<stableSymbolId>`.

Grouping limitations:

- Shared surface grouping is static name/shape evidence only.
- It does not prove runtime coupling, ownership, deployment topology, schema existence, package compatibility, or production usage.
- Groups must list supporting source evidence rows and rule IDs.
- Groups must expose `allSourcesSame` so same-source-only evidence is not mistaken for cross-source evidence.
- Groups based on Tier3 or hash-only evidence must be review-tier.

## Before/After Portfolio Comparison

`--before-manifest` and `--after-manifest` create `PortfolioComparisonV1` mode.

Comparison steps:

1. Load before and after manifests.
2. Validate source labels, repo identity, expected commit SHA, and schema.
3. Pair sources by manifest label plus repo identity when available.
4. If labels match but extracted repo identity differs and no manifest `expectedRepoIdentity` resolves the pair, emit `IdentityAmbiguous`, downgrade affected comparisons to `ReviewRecommended`, and continue without a strong same-source claim.
5. Treat unmatched sources as added/removed/unpaired source rows.
6. Project surfaces and edges into stable safe identities.
7. Compare projected evidence with coverage-relative downgrade rules.
8. Reuse combined diff and impact engines when inputs are compatible combined snapshots.
9. Render unavailable/deferred sections when reuse is not possible.

Do not compare local file paths or raw snippets. Do not promote evidence from reduced coverage into strong added/removed claims.

## Optional Context

### Path Context

`--include-paths` is off by default. When requested:

- reuse existing bounded path query APIs;
- run only against compatible combined graph evidence;
- preserve path classifications, rule IDs, evidence tiers, supporting facts, edge IDs, and limitations;
- cap depth, frontier, paths, gaps, and selected sources;
- emit unavailable/deferred gaps for incompatible single-index-only evidence.

### Reverse Context

`--include-reverse` is off by default. When requested:

- reuse existing bounded reverse query APIs;
- preserve reverse roots, paths, rule IDs, evidence tiers, supporting IDs, and limitations;
- cap roots, paths, frontier, depth, gaps, and selected surfaces;
- emit unavailable/deferred gaps for incompatible inputs.

### Release Review Context

Release-review packet import is deferred until a release-review report workflow exists in the .NET codebase. In v1, `releaseReviewContext` remains present for JSON shape stability with `status: "not_requested"` or `status: "deferred"` and a `portfolio.optional-context.v1` gap if a future caller requests unsupported release-review import.

## Markdown Report

Section order:

1. Summary
2. Portfolio Inputs
3. Source Identity and Coverage
4. Cross-Source Endpoint Alignment
5. Dependency Surfaces
6. Dependency Edges
7. Shared Portfolio Surfaces
8. Optional Path and Reverse Context
9. Portfolio Diff and Impact
10. Release Review Context
11. Gaps
12. Limitations

Markdown rendering rules:

- escape table delimiters and Markdown syntax in user-controlled fields;
- render safe relative file paths and line spans only;
- show rule ID and evidence tier for every finding/group/gap;
- show partial/truncated status near Summary and affected sections;
- use `not_requested`, `unavailable`, `deferred`, or `truncated` explicitly;
- avoid `safe`, `clean`, `approved`, `ready`, `no dependencies`, or runtime certainty language.

## JSON Contract

Top-level fields:

```text
reportType
version
mode
query
portfolioSnapshot
beforeSnapshot
afterSnapshot
summary
inputs
sources
sourceCoverage
endpointAlignment
dependencySurfaces
dependencyEdges
sharedSurfaces
pathContext
reverseContext
portfolioDiff
portfolioImpact
releaseReviewContext
gaps
limitations
```

Rules:

- `reportType` is `multi-index-portfolio-report`.
- `version` starts at `1.0`.
- all arrays are sorted deterministically;
- empty lists are `[]`;
- absent optional objects are `null`;
- arbitrary metadata is represented as sorted `[{ "key": "...", "value": "..." }]`;
- generated timestamps are omitted unless a future version explicitly accepts non-byte-stable output;
- `PortfolioSnapshot` and nested objects must not include generated timestamps, wall-clock dates, process IDs, stored scan timestamps, or stored import timestamps;
- identical inputs and options must produce byte-identical JSON and Markdown.

## Rule IDs

Implementation must document portfolio rules in `rules/rule-catalog.yml` before emitting findings:

| Rule ID | Purpose |
| --- | --- |
| `portfolio.identity.v1` | source identity, duplicate label, repo/commit mismatch, unknown SHA gaps |
| `portfolio.coverage.v1` | reduced coverage and partial analysis rollups |
| `portfolio.schema.v1` | missing table/view/precision schema gaps |
| `portfolio.endpoint.alignment.v1` | cross-source endpoint alignment derived findings |
| `portfolio.surface.inventory.v1` | source-scoped surface inventory rows |
| `portfolio.surface.group.v1` | shared-surface grouping rows |
| `portfolio.edge.inventory.v1` | dependency edge inventory rows |
| `portfolio.diff.v1` | portfolio before/after projected diff rows |
| `portfolio.impact.context.v1` | reused or unavailable impact context rows |
| `portfolio.optional-context.v1` | path/reverse requested/not-requested/unavailable rows and v1 release-review deferred rows |
| `portfolio.selector.v1` | selector no-match or ignored selector gaps |
| `portfolio.truncation.v1` | deterministic caps and omitted counts |
| `portfolio.redaction.v1` | unsafe value omission/hash/redaction findings or gaps |

Each rule catalog entry must include emitted row/gap types and limitations. Portfolio-specific rules should cite underlying TraceMap rule IDs where they compose existing evidence. `portfolio.redaction.v1` must document at least `RedactedValue` and `UnsafePropertyOmitted` gaps and must state that redaction cannot prove arbitrary user-provided names are non-sensitive; it only applies the configured safe-rendering policy.

## Safety and Redaction

Use shared safe rendering helpers wherever available.

Never emit:

- raw source snippets;
- raw SQL;
- raw URLs;
- literal values;
- config values;
- connection strings;
- secrets, tokens, credentials, or secret-looking strings;
- private checkout paths;
- local absolute paths;
- unchecked fact property bags.

Manifest string fields are user-controlled too. `portfolioId`, `snapshotId`, `label`, `group`, and `roleTags` must be escaped, omitted, or rendered through safe helpers before Markdown, JSON display metadata, or stderr output.

Allowed safe output:

- repo-relative paths;
- line spans;
- commit SHA;
- rule IDs;
- evidence tiers;
- scanner/extractor versions;
- hashes of unsafe values;
- normalized path keys;
- SQL shape hashes and text hashes;
- package names and versions when not secret-like;
- config key names or hashes.

Errors must be sanitized too. Input labels can be shown; raw paths should be omitted or replaced with safe basenames/hashes.

## Testing Strategy

Unit tests:

- manifest parsing and duplicate label rejection;
- single and combined index detection;
- combined source expansion;
- identity and coverage gap classification;
- surface normalization;
- endpoint alignment classifications;
- shared surface grouping;
- deterministic sorting and stable IDs;
- Markdown escaping and JSON metadata ordering;
- redaction of unsafe values;
- cap/truncation behavior.

Integration tests:

- repeated `--index --label` command output;
- manifest command output;
- before/after manifest comparison;
- mixed single/combined portfolio input;
- optional path/reverse requested and not requested states;
- release-review context v1 deferred/not-requested behavior;
- read-only input mutation check;
- byte-stable repeated output.
- absence of generated and stored scan/import timestamps from output.

Validation for implementation PRs:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When implementation touches language adapters, combined indexes, paths, reverse, report, diff, or impact behavior, follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks.

## Suggested PR Slices

1. Command shell, manifest parsing, read-only index detection, source identity/coverage model, rule catalog entries, JSON/Markdown skeleton, unavailable optional sections.
2. Surface inventory over single and combined inputs with safe rendering.
3. Edge inventory over single and combined inputs with safe rendering.
4. Cross-source endpoint alignment after extracting the shared endpoint matcher, plus shared-surface grouping.
5. Before/after manifest comparison and optional impact composition.
6. Optional path/reverse context and public sample workflow.

## Open Assumptions

- The current combined report endpoint matching logic can be extracted into a shared internal helper without changing combined report output; if not, PR 3 must stop and update this spec.
- Single-language indexes expose enough manifest/fact metadata to support v1 identity, surface, and edge inventory through a new reader; missing required tables become `portfolio.schema.v1` gaps for affected sections.
- Portfolio comparison can start with manifest-based before/after input to avoid ambiguous direct command-line source pairing.
- Existing safe rendering helpers can be shared across portfolio Markdown, JSON, and stderr.
