# Route-Centered Endpoint Trace Completeness Design

## Overview

This spec completes the existing `tracemap route-flow` view so a reviewer can
ask, "for this normalized route or client-call selector, what static evidence is
touched?" The feature extends the report layer over a combined index. It does
not add scanner-side inference in the first slice and does not create a new
runtime or AI analysis path.

The design builds on these current pieces:

- `CombinedRouteFlowReporter` and the existing `route-flow-report.md` /
  `route-flow-report.json` contract.
- `CombinedDependencyPathReporter.BuildGraphInventoryAsync` and shared combined
  path/reverse inventory helpers where they already expose graph rows safely.
- Existing route-flow rule IDs in `rules/rule-catalog.yml`, especially
  `combined.route-flow.selector.v1`, `entry.v1`, `path.v1`,
  `interface-bridge.v1`, `logic-surface.v1`, `dependency-surface.v1`,
  `argument-projection.v1`, `fact-symbol-projection.v1`,
  `classification.v1`, `gap.v1`, `redaction.v1`, and `report.v1`.
- Existing shared rendering and redaction helpers such as safe-path and
  deterministic hash utilities.

## Non-Goals

- No product-code implementation in the spec PR.
- No new public command in the first implementation slice.
- No runtime execution, live HTTP calls, traffic capture, production call-path
  proof, or dependency-injection runtime target proof.
- No database connection, raw SQL rendering, or live schema introspection.
- No outage-cause, release-safety, business-impact, or "impacted" claims.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No committed private repo names, local private paths, raw private routes, raw
  remotes, hostnames, secrets, snippets, raw SQL, config values, or private
  sample labels.

## Command Surface

Keep `tracemap route-flow` as the public command:

```bash
tracemap route-flow --index combined.sqlite --route "GET /api/items/{id}" --out out/
tracemap route-flow --index combined.sqlite --client-call "GET /api/items/{id}" --out out/
tracemap route-flow --index combined.sqlite --from-symbol "Synthetic.Api.ItemsController.Get" --out out/
```

The examples are synthetic. Implementation should continue to require exactly
one root selector from the existing selector set. Selector values are normalized
before storage. Unsafe raw selector material is omitted, hashed, or replaced
with a safe descriptor.

The first implementation slice should avoid adding new options unless an
implementation review finds an existing cap or selector is insufficient. Any
later option must be documented in the rule catalog before it appears in output.

## Data Flow

1. Validate the input is a combined index and open SQLite read-only.
2. Normalize the selector with the existing route-flow selector logic.
3. Build or reuse graph inventory from combined path/report helpers.
4. Select route/client entry evidence from `HttpRouteBinding`,
   `HttpCallDetected`, endpoint alignment, WebForms, symbol, or source roots.
5. Compose selected static flow rows from combined call, dependency,
   object-creation, parameter-forward, argument-flow, fact-symbol, and
   symbol-relationship evidence.
6. Attach method/service/data/query/dependency context only when source-local
   evidence connects it to selected rows.
7. Build deterministic touched-file and touched-symbol summaries from selected
   rows.
8. Add coverage labels, gaps, limitations, and classification downgrades.
9. Render Markdown and JSON through existing safe output paths and redaction
   rules.

## Report Model Additions

Preserve `RouteFlowReport` `reportType = "route-flow"` and `version = "1.0"`.
Additive fields may be introduced after implementation review. Suggested model
names are illustrative and may be adjusted to fit existing style:

```csharp
public sealed record RouteFlowTouchedFile(
    string FileId,
    string SourceLabel,
    string? CommitSha,
    string FilePath,
    int? FirstStartLine,
    int? LastEndLine,
    string Classification,
    string Coverage,
    IReadOnlyList<string> SupportingRowIds,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowTouchedSymbol(
    string SymbolId,
    string SourceLabel,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string DisplayName,
    string SymbolKind,
    string Classification,
    string Coverage,
    IReadOnlyList<string> SupportingRowIds,
    RouteFlowEvidenceRef Evidence);
```

If these concepts can be represented by existing `RouteFlowLogicRow` and
`RouteFlowDependencySurface` rows without ambiguity, implementation should
prefer compatible reuse and add only summaries. Argument and value-origin
evidence should follow the established `combined.route-flow.argument-projection.v1`
pattern by projecting into logic rows unless implementation review proves a
separate additive shape is necessary.

Touched-file and touched-symbol summaries are report-envelope aggregations under
`combined.route-flow.report.v1`. They inherit the supporting row rule IDs,
evidence tiers, classifications, coverage labels, and limitations instead of
creating standalone conclusions.

## Selector And Entry Evidence

Selector normalization should keep these fields where safe:

- selector kind: `route`, `client-call`, `from-endpoint`, `from-symbol`,
  `from-source`, or `from-webforms-event`;
- HTTP method when applicable;
- normalized path key/template when safe;
- match mode: exact normalized key, method/path, aligned endpoint, symbol,
  source, or no match;
- redaction state and supporting redaction rule when input was unsafe.

Entry evidence comes from existing route-flow selection. Rows must preserve
source labels, commit SHAs, extractor identity, file spans, supporting IDs,
route/client method and normalized path keys, and the weakest supporting
evidence tier. Source scan IDs remain available through `RouteFlowSnapshot`
source entries; entry rows should not invent a duplicate `scanId` field unless a
future schema change adds it explicitly.

## Static Trace Composition

The implementation should reuse existing route-flow traversal before adding new
graph code. Rows can be grouped for readability as method rows, service rows,
interface candidate rows, data/query rows, dependency rows, and value-origin
rows, but the underlying provenance remains row-level route-flow evidence.

Composition rules:

- Direct semantic call evidence may support stronger static classifications.
- Structural evidence can support probable static rows when no weaker required
  evidence is present.
- Structural or syntax fallback route-root bridges are stricter than ordinary
  downstream structural edges and cap the affected trace at
  `NeedsReviewStaticRouteFlow` or weaker.
- Syntax-only, textual, name-only, ambiguous, high-fan-out, fallback,
  generated-code uncertain, dynamic, reduced, or truncated evidence is
  review-tier or unknown.
- Same-file or same-name proximity alone is not enough to create a path edge.
- Cross-source joins require explicit alignment or combined evidence, not
  global short-name stitching.

## Interface, Override, And DI Boundaries

Interface and override handling stays conservative:

- Candidate implementation rows require deterministic symbol relationship or
  equivalent combined evidence.
- Candidate rows must say "candidate" and include a limitation that runtime DI
  target selection is not proven.
- Any path that depends on an interface candidate is capped at
  `NeedsReviewStaticRouteFlow` or weaker.
- Direct concrete call evidence can be rendered separately and classified by
  that stronger direct evidence.
- Missing or ambiguous implementation evidence emits gaps such as
  `ImplementationCandidateUnavailable`, `MissingImplementationBridge`, or
  `AmbiguousImplementationCandidates`.

Dependency injection registrations, factories, service locators,
configuration-driven bindings, reflection, and dynamic dispatch are limitations
unless a future deterministic rule specifically covers them.

## Data, Query, Dependency, And Value-Origin Rows

Rows may include:

- object/projection/DTO/serializer/validation rows from selected symbols;
- query-shape rows from safe operation/table/field descriptors and hashes;
- dependency/data rows for HTTP, package/config, storage, queue/event, WCF,
  remoting, legacy data, repository, ORM, or generic dependency surfaces;
- argument/value-origin rows from `combined_argument_flows` when the caller and
  callee are already selected by route-flow evidence;
- fact-symbol projection rows from `combined_fact_symbols` when the attached
  fact and symbol are source-local and selected.

Raw values must not be rendered. Safe metadata should prefer closed-set kinds,
type descriptors, ordinals, operation names, redaction hashes, and stable
source-scoped keys.

## Coverage And Classification

The report should preserve the existing classification vocabulary:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

`FullEvidenceAvailable` and `ReducedCoverage` remain coverage labels, not
additional summary classifications.

`UnknownAnalysisGap` may appear as the top-level route-flow summary
classification when coverage, schema, source identity, or selector state
prevents any stronger or cleaner classification.

Strong and clean no-evidence conclusions require full route-flow coverage,
known commit SHA, verified source identity, relevant extractor availability,
compatible schema, no blocking gaps, and no required weak links. Reduced
coverage, unknown commit SHA, selector ambiguity, missing optional tables,
fallback evidence, caps, unsafe-value omission, or unavailable extractors
downgrade the relevant rows and summary.

## Output Rendering

Markdown should preserve existing sections and may add:

- Touched Files
- Touched Symbols
- Value-Origin Evidence
- Coverage Notes

JSON should add collections only as backward-compatible fields. Missing values
must be explicit. Stable IDs should be derived from safe ordered source labels,
selector kind, normalized key, supporting fact IDs, edge IDs, symbol IDs, file
paths, line spans, gap kinds, and rule IDs.

Deterministic ordering:

1. source label;
2. selector kind and normalized key;
3. classification rank;
4. path length and sequence;
5. row kind;
6. safe display label;
7. repo-relative file path;
8. start line and end line;
9. symbol ID, fact ID, edge ID;
10. stable row ID.

## Privacy And Redaction

Apply `combined.route-flow.redaction.v1` to selector metadata, Markdown, JSON,
logs, safe metadata, source labels where needed, and generated fixtures.

Forbidden public output includes private repo names, private local paths, raw
private routes, raw remotes, hostnames, raw URLs, raw SQL, raw config values,
source snippets, connection strings, secrets, private sample labels, and
diagnostic strings that include any of those values.

## Validation Strategy

Use public-safe synthetic fixtures. Required implementation validation:

- focused route-flow unit tests for selector safety, entries, touched files,
  symbols, method/service rows, interface candidates, value-origin rows, query
  rows, dependency rows, gaps, and classification downgrades;
- byte-stability tests for Markdown and JSON;
- privacy tests for Markdown, JSON, logs, and SQLite-derived display fields;
- catalog tests proving every emitted rule ID exists;
- `dotnet test`;
- public-safe CLI smoke over a checked-in or generated synthetic combined
  fixture;
- relevant `docs/VALIDATION.md` route-flow/reporting checks;
- `git diff --check`;
- `./scripts/check-private-paths.sh`.

## First Implementation Slice

The smallest useful first PR should:

1. add additive touched-file and touched-symbol summaries from existing
   `entryEvidence`, `flowRows`, `logicRows`, `dependencySurfaces`, and `gaps`;
2. preserve JSON version/report type and existing route-flow sections;
3. add deterministic sorting and byte-stability tests;
4. add privacy tests for selector and summary rendering;
5. avoid new scanner extraction and avoid new public command options.

This first slice gives users a file/symbol map for existing route-flow evidence
while leaving deeper row-type expansions for later PRs.
