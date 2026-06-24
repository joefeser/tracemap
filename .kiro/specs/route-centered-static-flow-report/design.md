# Route-Centered Static Flow Report Design

## Overview

Add a deterministic route-centered report over one combined TraceMap snapshot.
The report starts from a selected server HTTP route or client HTTP call, gathers
static path evidence through the combined dependency graph, and renders the
files, line spans, symbols, intermediate calls, candidate implementation
bridges, business/data logic, dependency surfaces, coverage labels, gaps, and
limitations that TraceMap can evidence.

The intended evidence shape is:

```text
HttpCallDetected and/or HttpRouteBinding
  -> endpoint alignment and fact-symbol attachment
  -> combined call edges / object creations / argument or parameter-forward edges
  -> optional interface implementation candidate bridge
  -> repository/data/query/projection/business boundary evidence
  -> terminal dependency/data surfaces and explicit gaps
```

Every output row is static evidence. A route-flow report does not prove that a
request executes, that a branch is feasible, that dependency injection selects a
candidate implementation, that a database query runs, that auth succeeds, or
that production traffic exists.

## Goals

- Provide one route-centered answer for "what static code path can TraceMap
  evidence for this HTTP route or client call?"
- Reuse existing endpoint alignment, combined path graph inventory, reverse
  query inventory, query-pattern, dependency-surface, object/projection-shape,
  symbol relationship, and evidence export helpers where they expose the needed
  data. Add explicit route-flow readers for combined fact-symbol and
  argument-flow details that are not currently surfaced by the path inventory.
- Preserve source label, commit SHA, file span, symbol identity, rule ID,
  evidence tier, extractor identity, fact IDs, edge IDs, coverage labels, and
  limitations.
- Bridge interface calls to implementation candidates conservatively when
  compiler-backed relationship evidence exists.
- Highlight business/data logic rows without claiming runtime execution or
  business impact.
- Emit deterministic Markdown and JSON that are safe for public review.

## Non-Goals

- No scanner or language-adapter extraction work in the first implementation
  slice unless a tiny reader-compatibility bug blocks reporting.
- No new source fact schema unless a rule-catalog-backed derived report row
  requires stable output names.
- No mutation of the combined SQLite index.
- No runtime tracing, live HTTP calls, browser automation, service hosting,
  database connections, schema introspection, auth evaluation, or branch
  feasibility analysis.
- No runtime dependency-injection binding proof, reflection target proof,
  dynamic dispatch certainty, serializer runtime mapping proof, or generated
  code freshness proof.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic business-impact ranking.
- No source snippets, raw SQL, raw URLs, connection strings, raw remotes, local
  absolute paths, private labels, or secrets in default outputs.

## Command Shape

Add a focused command:

```text
tracemap route-flow --index <combined.sqlite> --out <path> [selectors] [options]
```

Primary selectors:

```text
--route "<METHOD> <PATH>"
--client-call "<METHOD> <PATH>"
```

Additional selectors, aligned with `tracemap paths` where possible:

```text
--from-endpoint "<METHOD> <PATH>"
--from-webforms-event <page-or-event-selector>
--from-symbol <symbol-selector>
--from-source <label>
--to-surface <sql-query|sql-persistence|http-route|http-client|package-config|legacy-data|wcf-operation|remoting-endpoint|remoting-registration|remoting-channel|remoting-object|remoting-api|dependency-surface>
--surface-name <text>
--classification <StrongStaticRouteFlow|ProbableStaticRouteFlow|NeedsReviewStaticRouteFlow|NoRouteFlowEvidence|UnknownAnalysisGap>
--max-depth <n>
--max-paths <n>
--max-frontier <n>
--max-logic-rows <n>
--max-gaps <n>
--format <markdown|json>
--exit-code
```

Default caps:

| Option | Default |
| --- | --- |
| `--max-depth` | `8` |
| `--max-paths` | `100` |
| `--max-frontier` | `10000` |
| `--max-logic-rows` | `200` |
| `--max-gaps` | `1000` |

Output behavior:

- Directory or extensionless output writes `route-flow-report.md` and
  `route-flow-report.json`.
- File output defaults to Markdown.
- `--format json` with file output writes JSON.
- Input opens read-only.
- Non-combined inputs fail with a clear diagnostic.
- `--exit-code` returns `0` for Strong or Probable route-flow results with no
  blocking gaps, and non-zero for NeedsReview, NoRouteFlowEvidence, or
  UnknownAnalysisGap. Validation, argument, file, schema, and system errors
  still take precedence.

## Why A New Command Instead Of Only `tracemap paths`

`tracemap paths` is a general forward path query. A route-centered report needs
additional entry-evidence grouping, business/data logic rows, interface
candidate bridges, client/server alignment context, and safety wording centered
on one route or client call. The implementation should still reuse
`tracemap paths` graph inventory and selectors where possible. The new command
is a product/reporting facade, not a second traversal engine.

`tracemap route-flow` is the public CLI for this spec. Do not add a public
`tracemap paths --view route-flow` alias in the first implementation slice. A
future compatibility alias may be added by a follow-up only if it delegates to
the same route-flow query engine and does not introduce a divergent traversal or
selector grammar.

## Selector Additions And Deviations From `tracemap paths`

Overlapping selectors (`--from-endpoint`, `--from-webforms-event`,
`--from-symbol`, `--from-source`, `--to-surface`, and `--surface-name`) should
reuse `tracemap paths` grammar and matching semantics. Route-flow adds these
selectors and caps:

- `--route` as a server-route alias over normalized method/path entry evidence;
- `--client-call` as a client-call alias over normalized method/path entry
  evidence;
- `--classification` as a row filter that keeps flow, logic, dependency-surface,
  and non-blocking gap rows matching the requested classification, preserves
  matched entry evidence as query context, preserves blocking coverage/schema/
  identity gaps, and recomputes the overall summary from the remaining rows;
- `--max-logic-rows` for business/data context rows;
- `--max-gaps` for report gap rows.

`combined.route-flow.selector.v1` limitations must document these additions and
state that unsupported or unknown surface kinds produce `SelectorNoMatch` or a
validation error rather than silently returning empty rows. At implementation
time, the allowed `--to-surface` values must be checked against the surface-kind
vocabulary emitted by the shared combined, paths, and reverse query models.

## Proposed Code Placement

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedRouteFlowQuery.cs
    CombinedRouteFlowModels.cs
    CombinedRouteFlowReporter.cs
    CombinedRouteFlowWriter.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    CombinedRouteFlowTests.cs
```

Preferred reuse targets:

- Combined path graph inventory and traversal helpers used by `tracemap paths`.
- Reverse query graph inventory where it already projects terminal surfaces and
  upstream roots.
- Endpoint alignment normalization and matching helpers.
- Combined report source projection helpers for source labels, commit SHA,
  coverage, and safe source metadata.
- Shared safe rendering, hashing, Markdown escaping, JSON ordering, and output
  path helpers.
- Evidence graph/vault export safety checks where they already guard generated
  public artifacts.

Behavior-preserving refactors of shared path/report helpers should land before
route-flow behavior when they are non-trivial.

## Data Sources

The route-flow command reads existing combined-index data and in-memory graph
inventory. It should tolerate missing tables/columns and emit availability gaps.

| Source | Purpose |
| --- | --- |
| `index_sources` | source labels, scan IDs, commit SHA, language, coverage, build status |
| `combined_facts` | route, client call, query, data, dependency, object/projection, boundary evidence |
| `combined_symbols` | source-local and language-specific symbol identity |
| `combined_fact_symbols` | fact-to-symbol attachment edges; requires explicit route-flow reader because the current path inventory does not expose all attachment detail |
| `combined_dependency_edges` | dependency edge summary over calls, object creations, symbol relationships, and parameter-forward edges |
| precise graph tables | `combined_call_edges`, `combined_object_creations`, `combined_symbol_relationships`, and `combined_parameter_forward_edges` backing the dependency-edge view |
| `combined_argument_flows` | argument-flow details; requires explicit route-flow reader because it is not part of `combined_dependency_edges` |
| endpoint alignment logic | client/server method/path matching and dynamic URL gaps |
| paths/reverse graph inventory | bounded static traversal roots, edges, terminal surfaces, gaps |
| evidence graph/vault export helpers | safe node/edge identity and public artifact guards |

Route-flow does not read source files and does not rerun extractors.

## Rule IDs

Add rule catalog entries before implementation emits route-flow output:

- `combined.route-flow.selector.v1`
- `combined.route-flow.entry.v1`
- `combined.route-flow.path.v1`
- `combined.route-flow.interface-bridge.v1`
- `combined.route-flow.logic-surface.v1`
- `combined.route-flow.dependency-surface.v1`
- `combined.route-flow.classification.v1`
- `combined.route-flow.gap.v1`
- `combined.route-flow.redaction.v1`
- `combined.route-flow.report.v1`

Proposed evidence tiers:

| Rule ID | Evidence tier |
| --- | --- |
| `combined.route-flow.selector.v1` | `Tier4Unknown` for selector gaps, otherwise inherits weakest selected evidence |
| `combined.route-flow.entry.v1` | inherits weakest supporting route/client/alignment evidence |
| `combined.route-flow.path.v1` | inherits weakest required path-edge evidence |
| `combined.route-flow.interface-bridge.v1` | inherits relationship evidence, capped at review-tier classification |
| `combined.route-flow.logic-surface.v1` | inherits weakest supporting logic/surface evidence |
| `combined.route-flow.dependency-surface.v1` | inherits weakest supporting dependency/data evidence |
| `combined.route-flow.classification.v1` | derived report rule over supporting evidence |
| `combined.route-flow.gap.v1` | `Tier4Unknown` |
| `combined.route-flow.redaction.v1` | `Tier4Unknown` |
| `combined.route-flow.report.v1` | derived report rule over supporting evidence |

`combined.route-flow.classification.v1` stamps the overall summary
classification and any classification-only gaps. `combined.route-flow.report.v1`
stamps the rendered report envelope and output-format metadata. Evidence rows
should cite their source evidence rule plus the most specific derived route-flow
rule that produced the row.

Rule limitations must document:

- route-flow rows are static evidence, not runtime execution proof;
- endpoint alignment does not prove live traffic, deployment, auth, CORS,
  middleware, proxy, or reachability behavior;
- call edges can miss reflection, dynamic dispatch, delegates, generated code,
  partial methods, dependency injection, and branch feasibility;
- interface bridges identify compiler-known implementation candidates, not
  runtime DI targets;
- query/data rows do not prove SQL execution, database existence, schema
  compatibility, persistence, or production use;
- business-logic rows are static review context, not proof of business intent or
  impact;
- reduced coverage, missing extractors, missing schema, unknown commit SHA,
  truncation, dynamic URLs, and ambiguous candidates cap classifications;
- unsafe paths, remotes, snippets, URLs, SQL/config values, connection strings,
  private labels, and secret-like values are omitted or hashed.

## Report Model

Suggested JSON root:

```csharp
public sealed record RouteFlowReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    RouteFlowQuery Query,
    RouteFlowSnapshot Snapshot,
    RouteFlowSummary Summary,
    IReadOnlyList<RouteFlowEntryEvidence> EntryEvidence,
    IReadOnlyList<RouteFlowRow> FlowRows,
    IReadOnlyList<RouteFlowLogicRow> LogicRows,
    IReadOnlyList<RouteFlowDependencySurface> DependencySurfaces,
    IReadOnlyList<RouteFlowTouchedFile> TouchedFiles,
    IReadOnlyList<RouteFlowTouchedSymbol> TouchedSymbols,
    IReadOnlyList<RouteFlowGap> Gaps,
    IReadOnlyList<string> Limitations);
```

Constants:

```text
reportType = "route-flow"
version = "1.0"
```

`RouteFlowReport.ReportCoverage` and `RouteFlowSummary.ReportCoverage` must be
identical in v1. The top-level field exists for consistency with other combined
reports; the summary field exists for consumers that read only the summary
object.

`TouchedFiles` and `TouchedSymbols` are additive v1 report summaries derived
from cited entry, flow, logic, dependency-surface, and gap rows. They do not add
new scanner evidence and must carry the report-envelope rule plus supporting
row rule IDs, evidence tiers, source labels, commit SHA, file spans, and
limitations where available.

### Query

```csharp
public sealed record RouteFlowQuery(
    string IndexPath,
    string OutputPath,
    string Format,
    string? Route,
    string? ClientCall,
    string? FromEndpoint,
    string? FromWebFormsEvent,
    string? FromSymbol,
    string? FromSource,
    string? ToSurface,
    string? SurfaceName,
    string? Classification,
    string RouteMatchMode,
    int MaxDepth,
    int MaxPaths,
    int MaxFrontier,
    int MaxLogicRows,
    int MaxGaps,
    bool ExitCode);
```

`IndexPath` and `OutputPath` must be populated from sanitized display-path
helpers before the report model is created. They must not store raw CLI argument
values or local absolute paths. Route-flow model construction should go through
a named factory or builder such as `RouteFlowQuery.Create(...)` that accepts raw
CLI paths plus the shared safe-path helper and stores only sanitized display
paths. Tests must assert the serialized query never contains the raw CLI
arguments. `RouteMatchMode` is a closed value: `NormalizedMethodPath`,
`NormalizedPathKey`, `OptionalSegmentCompatible`, `DynamicClientUrl`,
`SymbolSelector`, `SourceSelector`, or `SelectorNoMatch`.

### Summary

```csharp
public sealed record RouteFlowSummary(
    string Classification,
    string ReportCoverage,
    int EntryEvidenceCount,
    int FlowRowCount,
    int LogicRowCount,
    int DependencySurfaceCount,
    int GapCount,
    bool HasBlockingGaps,
    bool Truncated,
    bool ExitCodeWouldBeNonZero,
    IReadOnlyList<string> ClassificationReasons);
```

`Classification` is the report-level rollup used by `--exit-code`. Rollup order
is:

1. `UnknownAnalysisGap` if selector matching, schema, source identity, commit
   SHA, coverage, or required extractor availability prevents a clean
   conclusion.
2. `NeedsReviewStaticRouteFlow` if any remaining row is review-tier or requires
   an implementation candidate, weak evidence, ambiguity, dynamic evidence, high
   fan-out, or truncation caveat.
3. `NoRouteFlowEvidence` if the selector matched under full route-flow coverage
   but no route-flow path or terminal surface remains after filters.
4. `ProbableStaticRouteFlow` for credible structural paths with no stronger
   semantic chain and no blocking gaps.
5. `StrongStaticRouteFlow` only when every required path link satisfies the
   strong criteria and no blocking gaps are present.

A report whose only gap is `SelectorNoMatch` carries
`Classification = "UnknownAnalysisGap"`. `ExitCodeWouldBeNonZero` follows the
CLI mapping: false for Strong or Probable reports with no blocking gaps, true
for NeedsReview, NoRouteFlowEvidence, UnknownAnalysisGap, and validation errors
handled before model construction.

### Snapshot

```csharp
public sealed record RouteFlowSnapshot(
    string IndexKind,
    int SourceCount,
    IReadOnlyList<RouteFlowSource> Sources);
```

```csharp
public sealed record RouteFlowSource(
    string SourceLabel,
    string? SourceIndexId,
    string? ScanId,
    string? CommitSha,
    string Language,
    string AnalysisLevel,
    string BuildStatus,
    bool IdentityVerified,
    IReadOnlyList<string> CoverageWarnings);
```

Route-flow should reuse existing combined source projection helpers instead of
creating a parallel source identity implementation. Use
`CombinedReverseSourceInfo` or the shared identity-derivation helper behind it
for `IdentityVerified`; map `Language`, `AnalysisLevel`, `BuildStatus`, and
coverage warnings from the combined report/source projection field names or
their exact equivalents. Raw repository names, remotes, scan roots, and local
paths must be omitted or hashed according to existing combined-report safety
rules. `IdentityVerified` is derived from the combined source projection and
subsumes the commit SHA check: it is true only when the source has a
non-placeholder commit SHA and verified source metadata. If `CommitSha` is
non-null but `IdentityVerified` is false, the false identity state wins, emits
an identity gap, and caps affected classifications to at most
`NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap` for no-evidence
conclusions.

### Rows

All emitted row types should carry a common evidence envelope:

```csharp
public sealed record RouteFlowEvidenceRef(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorName,
    string? ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingRuleIds,
    IReadOnlyList<string> Limitations);
```

Entry evidence:

```csharp
public sealed record RouteFlowEntryEvidence(
    string EntryId,
    string EntryKind,
    string Method,
    string NormalizedPathTemplate,
    string NormalizedPathKey,
    string? DisplaySymbol,
    string Classification,
    string Coverage,
    RouteFlowEvidenceRef Evidence);
```

Flow rows:

```csharp
public sealed record RouteFlowRow(
    string RowId,
    int Sequence,
    string RowKind,
    string EdgeKind,
    string SourceSymbol,
    string? TargetSymbol,
    string Classification,
    string Coverage,
    string? FromNodeId,
    string? ToNodeId,
    RouteFlowEvidenceRef Evidence);
```

Recommended `RowKind` values:

- `entry`
- `client-server-alignment`
- `call-edge`
- `object-creation`
- `argument-flow`
- `parameter-forward`
- `interface-implementation-candidate`
- `symbol-relationship`
- `path-context`
- `terminal-surface`
- `gap`

`EdgeKind` is a closed value for the relation represented by the row:
`none`, `client-server-alignment`, `direct-call`, `object-creation`,
`argument-flow`, `parameter-forward`, `interface-implementation-candidate`,
`symbol-relationship`, `fact-symbol-attachment`, `path-context`,
`terminal-surface`, or `unknown`.

Entry evidence uses this closed `EntryKind` set:

- `route-root` for `--route`;
- `client-call-root` for `--client-call`;
- `endpoint-root` for `--from-endpoint`;
- `webforms-event-root` for `--from-webforms-event`;
- `symbol-root` for `--from-symbol`;
- `source-root` for `--from-source`;
- `aligned-route-pair` when the selected route and client call are both present
  and endpoint alignment supports the pair.

When the selector is `--from-symbol` or `--from-source`, `Method`,
`NormalizedPathTemplate`, and `NormalizedPathKey` are empty strings, and the
report emits a coverage gap noting that route/client-call entry evidence was not
selected.

Logic rows:

```csharp
public sealed record RouteFlowLogicRow(
    string LogicRowId,
    string LogicKind,
    string DisplayName,
    string AttachmentKind,
    string? AttachedFlowRowId,
    string Classification,
    string Coverage,
    IReadOnlyDictionary<string, string> SafeMetadata,
    RouteFlowEvidenceRef Evidence);
```

Recommended `LogicKind` values:

- `projection-or-object-shape`
- `query-filter-sort-selection`
- `validation-or-guard`
- `branch-or-condition`
- `async-boundary`
- `authorization-marker`
- `flow-boundary`
- `serializer-or-contract-shape`

Dependency surfaces:

```csharp
public sealed record RouteFlowDependencySurface(
    string SurfaceId,
    string SurfaceKind,
    string DisplayName,
    string StableKey,
    string Classification,
    string Coverage,
    IReadOnlyDictionary<string, string> SafeMetadata,
    RouteFlowEvidenceRef Evidence);
```

`StableKey` is derived from safe, source-scoped static identity:

```text
surface-kind | source-label | normalized route key or package/config/query/data stable key | safe display identity
```

If no existing combined/paths/reverse stable key exists for a surface, compute a
deterministic hash over the safe fields above. Do not include scan-specific fact
IDs, edge IDs, row IDs, raw SQL, raw URLs, config values, local paths, snippets,
or unsanitized metadata in the key input. Supporting fact and edge IDs remain in
the evidence envelope, not in the surface stable key. Duplicate stable keys emit
identity gaps instead of being disambiguated with volatile IDs.

Gaps:

```csharp
public sealed record RouteFlowGap(
    string GapId,
    string GapKind,
    string Message,
    string RuleId,
    string EvidenceTier,
    string Coverage,
    string? SourceLabel,
    string? AffectedRowId,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);
```

Recommended gap kinds:

- `SelectorNoMatch`
- `SchemaMissing`
- `ExtractorUnavailable`
- `ImplementationCandidateUnavailable`
- `RuntimeBindingNotProven`
- `DynamicDispatchBoundary`
- `DynamicClientUrlNeedsReview`
- `ReducedCoverage`
- `UnknownCommitSha`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`
- `TruncatedByLimit`
- `UnsafeValueOmitted`

These gap kinds are the v1 closed set. New gap kinds require a rule-catalog
update and JSON compatibility review.

`UnknownAnalysisGap` and `NoRouteFlowEvidence` appear both as summary
classifications and as gap kinds. JSON consumers must use
`summary.classification` for the overall result and `gaps[].gapKind` for
individual evidence gaps. A `--classification UnknownAnalysisGap` filter keeps
gap rows whose gap kind or row classification maps to `UnknownAnalysisGap`; if
no rows remain, it emits `SelectorNoMatch` and an overall
`UnknownAnalysisGap`.

## Traversal And Composition

1. Validate selectors, caps, and output path.
2. Open combined index read-only and validate combined schema.
3. Load source metadata and coverage warnings.
4. Select entry evidence from `HttpRouteBinding`, `HttpCallDetected`, endpoint
   alignment, fact-symbol attachments, and normalized route keys.
5. Build or reuse the combined path graph inventory for `combined_dependency_edges`.
6. Load additional route-flow-only detail from `combined_argument_flows` and
   `combined_fact_symbols`; if those tables are absent in older combined
   schemas, emit `SchemaMissing` or `ExtractorUnavailable` gaps rather than
   dropping logic rows silently.
7. Traverse forward from selected entry symbols/facts using deterministic
   breadth-first search with depth/path/frontier caps.
8. Attach route-adjacent logic rows from facts whose symbol, file span, or
   source-local fact-symbol attachment is adjacent to a traversed node.
9. Add conservative interface implementation candidate rows only when
   relationship evidence supports them.
10. Detect terminal dependency/data surfaces.
11. Apply classification caps, coverage labels, and gap propagation.
12. Sort rows by selected entry, path signature, sequence, source label, safe
    path, line span, fact ID, and edge ID.
13. Render Markdown and JSON.

`Adjacent` has a narrow v1 meaning for business/data logic rows:

- the logic fact has a `combined_fact_symbols` attachment that shares a
  combined symbol ID with a traversed flow node;
- or the logic fact's file span is within the same method/member span as a
  traversed node in the same source label;
- or the logic fact is attached to the same source fact or edge ID as a
  traversed row;
- or the logic fact shares a source or target fact ID with a
  `combined_argument_flows` row whose source or destination node is traversed.

Same-file evidence without a shared symbol, same-member span, or supporting ID
is not adjacent in v1 and should remain inventory evidence or a gap.

## Interface Bridge Details

Bridge candidates are derived from combined symbol relationship evidence only.
For C#, the initial implementation should consume
`combined_symbol_relationships` rows whose source facts cite
`csharp.semantic.symbolrelationship.v1` or a successor rule, such as interface
member implementation relationships. A bridge row connects:

```text
interface method symbol -> concrete implementation candidate symbol
```

The bridge is allowed when:

- the source call target is an interface member or interface-declared symbol;
- a relationship row records an implementation candidate;
- the source and target symbols are in compatible source-local or combined
  symbol namespaces;
- the bridge can preserve supporting fact/edge IDs and evidence tier.

The bridge is not allowed when:

- it depends only on display-name equality;
- it requires selecting one runtime DI registration;
- the relationship row is missing;
- the candidate crosses languages or sources without explicit rule-backed
  evidence.

Any path that requires an interface implementation candidate bridge is capped at
`NeedsReviewStaticRouteFlow`, even when exactly one compiler-resolved candidate
exists. Multiple candidates may be listed as separate rows; the report must not
choose one as the runtime target.

Cross-source and cross-language implementation bridges are unconditionally
blocked in v1 and emit `RuntimeBindingNotProven` unless a future rule-catalog
entry explicitly defines safe cross-source bridge evidence. Endpoint alignment
may connect client/server entry evidence, but it is not implementation binding
evidence.

## Classification

Classifications:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

Classification rules:

- Strong requires selected entry evidence, credible traversal, and terminal
  surface evidence under full route-flow coverage, with no required weak, ambiguous,
  dynamic, fallback, implementation-candidate, truncation, or identity gaps.
- Probable allows strong structural evidence or mixed semantic/structural
  evidence when no weak ambiguity controls the path.
- NeedsReview is required for Tier3 syntax/textual links, dynamic URLs,
  fallback route facts, name-only reconciliation, interface candidate bridges,
  high fan-out, duplicate identities, generated-code uncertainty, or ambiguous
  terminal surfaces.
- NoRouteFlowEvidence requires full route-flow coverage and relevant extractor
  availability for the selected route/client-call scope.
- UnknownAnalysisGap is required when coverage, schema, extractors, commit SHA,
  selector ambiguity, or caps prevent a clean conclusion.

Full route-flow coverage requires known commit SHA, credible source coverage for
all contributing sources, route/client-call extractor availability for the
selected entry evidence, and non-gap-only edge/symbol relationship evidence for
the path families needed by the selector. A TypeScript-only combined index with
no server route extractor present, or a C# route-only index with no client-call
extractor present for a client selector, is partial route-flow coverage rather
than full coverage.

High fan-out is review-sensitive when a terminal or bridge candidate set has 10
or more candidate rows across distinct source facts, or when a generic terminal
key such as `status`, `id`, `name`, `value`, `result`, or `response` has more
than one candidate. The rule catalog entry must document this v1 threshold and
state that it is conservative and subject to future calibration.

Percent-encoded route selectors are decoded only for safe matching of ordinary
path characters. Encoded slashes such as `%2F`, control characters, path
traversal segments, or decoded URL/host material are not normalized into route
separators; they emit a selector gap or review-tier match and the original raw
selector is not rendered.

## Markdown Shape

Required sections:

1. Summary
2. Query
3. Snapshot Sources
4. Entry Evidence
5. Static Flow
6. Business/Data Logic
7. Dependency Surfaces
8. Gaps
9. Limitations

Preferred wording:

- "static route-flow evidence"
- "candidate implementation"
- "coverage-relative"
- "analysis gap"
- "path context"
- "dependency/data surface evidence"

Avoid:

- "executed"
- "impacted"
- "used in production"
- "called at runtime"
- "authorized"
- "proves DI target"
- "query runs"
- "business impact"

## Safety And Redaction

The writer should run a final string-leaf guard over Markdown and JSON before
writing artifacts. It should reject, hash, or omit:

- local absolute paths;
- raw repository remotes and raw repository names;
- private source labels and private sample identifiers;
- raw URLs, endpoint addresses, WSDL/SOAP addresses, and credential-bearing
  strings;
- raw SQL, connection strings, config values, literal secrets, and token-looking
  values;
- source snippets and raw analyzer diagnostics.

Route display should use normalized method/path evidence, not raw full URLs.
File display should use repo-relative paths or neutral labels. Unsafe source
labels should be converted to stable neutral labels with a redaction note.

## Tests

Focused tests should include:

- public-safe aligned client/server fixture;
- route-only and client-call-only reports;
- selector no-match;
- dynamic client URL gap;
- missing TypeScript HTTP client facts;
- missing route facts;
- missing call-edge tables;
- missing `combined_symbol_relationships` tables;
- interface call with one candidate;
- interface call with multiple candidates capped at NeedsReview;
- no implementation candidate gap;
- repository call and DbSet-like data access surface;
- query filter/sort/select/projection rows;
- validation/guard/branch/async boundary rows where fixture facts exist;
- high fan-out cap;
- reduced coverage and unknown commit SHA gaps;
- truncation gaps;
- old combined schema compatibility;
- non-combined input rejection;
- no-mutation assertion by hashing the combined database before and after;
- filter-option behavior for `--to-surface`, `--surface-name`, and
  `--classification`;
- `--format json` with file output;
- `--exit-code` for strong/probable, needs-review, no-evidence, and gap states;
- `--classification`, `--from-endpoint`, `--from-webforms-event`,
  `--from-symbol`, and `--from-source` semantics;
- `--from-endpoint` against endpoint alignment evidence;
- `--from-webforms-event` against legacy-root path evidence;
- `--classification` filter reducing to zero rows and producing
  `SelectorNoMatch` plus overall `UnknownAnalysisGap`;
- `RouteFlowSummary.ExitCodeWouldBeNonZero` matching the actual process exit
  code when `--exit-code` is used;
- missing `combined_argument_flows` and `combined_fact_symbols` table gaps;
- empty `index_sources` snapshots;
- duplicate normalized route keys across sources with different commit SHAs;
- percent-encoded route selector handling without unsafe decoded output;
- sanitized `RouteFlowQuery.IndexPath` and `RouteFlowQuery.OutputPath`;
- summary contract and overall classification byte stability;
- `--max-logic-rows` truncation;
- `--max-gaps`, `--max-depth`, `--max-paths`, and `--max-frontier` truncation;
- forbidden-wording assertions;
- redaction of `SafeMetadata` on logic rows and dependency surfaces;
- byte-stable Markdown/JSON;
- row-order permutation stability;
- public artifact safety against raw paths, remotes, SQL, URLs, config values,
  snippets, connection strings, private labels, and secrets.

## Validation

Implementation validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run `./scripts/smoke-combined-paths.sh` when implementation changes shared
combined path traversal, endpoint alignment, report helpers, adapter output, or
dependency-surface projection. Follow `docs/VALIDATION.md` for any additional
pinned smoke checks tied to modified language adapters.

No generated timestamp, generated-at field, or wall-clock value should appear in
route-flow Markdown or JSON. No-mutation tests should prefer a logical database
fingerprint, such as schema plus row-count/content hashes with WAL side effects
neutralized, instead of a naive SQLite file hash that can change from journaling
alone.
