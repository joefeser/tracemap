# Deterministic Review Priority Scoring Design

## Overview

Add a deterministic review-priority layer over existing TraceMap report evidence.

```text
existing report rows and gaps
  -> scoring evidence adapter
  -> deterministic component rules
  -> downgrade and unknown rules
  -> row severity hints
  -> report attention summary
  -> Markdown and JSON scoring sections
```

The scoring layer does not scan repositories, read source files, query external services, call AI models, infer business importance, or predict runtime incidents. It organizes static evidence for human review.

The preferred product language is:

- "review priority"
- "severity hint"
- "attention level"
- "score component"
- "downgrade"
- "unknown due to gap"

Avoid:

- "risk probability"
- "incident likelihood"
- "safe to release"
- "runtime impact"
- "security risk"
- "business critical"

## Goals

- Help reviewers sort evidence-heavy reports deterministically.
- Preserve all underlying rule IDs, evidence tiers, classifications, source labels, file spans, commit SHAs, supporting IDs, and limitations.
- Make every priority hint explainable through component rows.
- Treat uncertainty, reduced coverage, missing identity, missing schema, and truncation as first-class scoring inputs.
- Keep output byte-stable and safe for public reports.
- Provide a reusable model that release-review can use first and other workflows can adopt incrementally.

## Non-Goals

- No ML, LLM calls, embeddings, vector databases, prompt-based classification, or generated conclusions.
- No production incident probability, release approval, merge recommendation, vulnerability finding, compliance claim, business-criticality inference, traffic inference, telemetry inference, deployment inference, runtime reachability proof, SQL execution proof, schema-existence proof, package compatibility claim, or license claim.
- No hidden weights or user-specific learned configuration.
- No source scanning or index mutation.
- No rendering of raw SQL, source snippets, literal values, config values, connection strings, raw URLs, hostnames, raw remotes, local absolute paths, private paths, or secrets.

## Product Semantics

Scoring is a prioritization view over static evidence. It answers:

- Which rows should a reviewer inspect first?
- Which report sections have the most review-attention evidence?
- Which scores are limited by gaps, reduced coverage, or truncation?
- Why did a row receive its priority hint?

It does not answer:

- Will this fail in production?
- Is this safe to release?
- Is this a vulnerability?
- How important is this to the business?
- Does this route execute at runtime?

## Naming Note

The spec folder and branch use `deterministic-risk-scoring` because issue `#30` requested that artifact name. Product-facing command help, report fields, Markdown headings, JSON fields, and docs should use "review priority" terminology instead. Do not introduce new public copy that shortens this feature to "risk scoring" without the static evidence boundary.

## Proposed Command and Option Shape

The first implementation slice should prefer release-review integration because release-review already composes diff, impact, path/reverse, package, API/DTO, SQL/schema, gaps, and checklist sections.

Suggested option:

```text
tracemap release-review --before <index.sqlite> --after <index.sqlite> --out <path> --include-priority
```

Follow-up workflows can add the same option:

```text
tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path> --include-priority
tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path> --include-priority
tracemap paths --index <combined.sqlite> --out <path> --include-priority
tracemap reverse --index <combined.sqlite> --out <path> --include-priority
tracemap portfolio --out <path> --include-priority
```

If implementation prefers scoring on by default for a workflow, it must still version the JSON shape and document the default. The safer v1 default is opt-in.

For release-review, the first implementation must make one explicit compatibility decision:

- Opt-in sidecar: when `--include-priority` is absent, Markdown and JSON remain byte-identical to the pre-feature output. When present, JSON includes a top-level `reviewPriority` object and either row annotations or a sidecar `reviewPriorityRows` array.
- Always-present additive section: JSON always includes `reviewPriority.status`, using `not_requested` when the flag is absent. This requires an intentional release-review document `Version` bump or a documented additive-field compatibility policy before merge.

The spec recommends the opt-in sidecar for the first slice because it best protects existing release-review consumers.

If a follow-up workflow receives `--include-priority` before it has a scoring adapter, it should emit `deferred` in the scoring status and include an unavailable-workflow component or gap. `not_supported` is reserved for a future explicit unsupported-scoring status that is added deliberately to the workflow vocabulary.

## Suggested Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    ReviewPriority/
      ReviewPriorityEngine.cs
      ReviewPriorityModels.cs
      ReviewPriorityRules.cs
      ReviewPriorityEvidenceAdapter.cs
      ReviewPriorityMarkdownWriter.cs
      ReviewPriorityJsonProjection.cs
  tests/TraceMap.Tests/
    ReviewPriorityScoringTests.cs
```

The scoring layer belongs in `TraceMap.Reporting` because it adapts report models. It should not live in scanner projects or language adapters.

## Data Sources

Scoring should consume normalized report evidence, not raw database rows, where possible.

| Evidence source | Scoring use |
| --- | --- |
| Underlying report classifications | Base review-priority signal without changing meaning |
| Rule IDs and evidence tiers | Component provenance and strength |
| File paths and line spans | Tie-breaking and evidence location |
| Commit SHAs and scan IDs | Snapshot provenance and missing-identity downgrades |
| Source labels and repo identity hashes | Source comparability and fan-out |
| Coverage/build status | Unknown and downgrade components |
| Analysis gaps | Uncertainty and attention components |
| Changed facts, surfaces, endpoints, edges | Static change components |
| Public-surface evidence | Existing static route, API/DTO, SQL/schema, package, route-flow, reverse, or portfolio surface rows that identify reviewer-visible boundaries without claiming runtime exposure |
| Cross-repo reach evidence | Existing combined or portfolio source labels, source identity hashes, shared surface groups, endpoint alignment rows, path/reverse roots across sources, and manifest comparison rows |
| Path and reverse summaries | Bounded reachability context components |
| Caps and omitted counts | Truncation components |
| Limitations | Explanation and Markdown/JSON safety |

Do not read raw source files for scoring. Do not parse raw SQL/config values for scoring. Do not use raw remotes or hostnames.

For safe rendering and metadata normalization, reuse `CombinedReportHelpers.Cell`, `CombinedReportHelpers.SafePath`, `CombinedReportHelpers.Hash`, and `CombinedReportHelpers.SortedMetadata`, or first extract equivalent shared helpers if visibility needs change. Do not create a third escaping or metadata-sorting implementation inside the scoring package.

## Core Model

Suggested JSON root for a scored report section:

```csharp
public sealed record ReviewPrioritySummary(
    string Status,
    string ModelVersion,
    string AttentionLevel,
    int? PriorityScore,
    bool Complete,
    IReadOnlyList<string> ContributingSections,
    IReadOnlyList<string> LimitedSections,
    IReadOnlyList<ReviewPriorityComponent> Components,
    IReadOnlyList<string> Limitations);
```

Suggested row-level projection:

```csharp
public sealed record ReviewPriorityAnnotation(
    string SeverityHint,
    int? PriorityScore,
    bool Complete,
    IReadOnlyList<ReviewPriorityComponent> Components,
    IReadOnlyList<string> SourceEvidenceIds,
    IReadOnlyList<string> Limitations);
```

Suggested component model:

```csharp
public sealed record ReviewPriorityComponent(
    string ComponentId,
    string ComponentKind,
    int? ComponentValue,
    string Direction,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<ReviewPriorityEvidenceRef> SourceEvidence,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);
```

Suggested evidence reference:

```csharp
public sealed record ReviewPriorityEvidenceRef(
    string EvidenceId,
    string EvidenceKind,
    string RuleId,
    string? EvidenceTier,
    string? SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine);
```

Use `null` and empty arrays consistently. Sort all arrays deterministically. `ComponentValue` is nullable so ordinal-only v1 can emit `null` for components whose meaning is expressed by `severityHint`, `attentionLevel`, `direction`, and rule ID. Numeric implementations must emit integer component values for every numeric component and document how each value contributes to `priorityScore`.

Checklist-derived components should reference the checklist item's finding or gap IDs and inherit evidence tiers from those referenced rows where possible. If multiple source tiers contribute, use the weakest contributing tier for ordering and cite all contributing tier values in limitations or metadata. If the checklist item has no tiered source evidence, emit a metadata-derived component with a documented scoring rule and limitation rather than inventing a source evidence tier.

Report-level `complete` is `false` when requested evidence is unavailable, reduced, or truncated for any scoring section that contributes to report attention. Row-level `complete` is `false` when any component attached to that row is a downgrade or unknown component with an unresolvable gap. Row-level `complete` is `true` when all applicable component rules ran and no open gaps remain for that row.

## Vocabulary

Row-level `severityHint`:

| Value | Meaning |
| --- | --- |
| `critical_review` | Highest static review attention under documented full-coverage conditions |
| `high_review` | Strong static evidence or broad fan-out needing prompt review |
| `medium_review` | Review-tier evidence, downgraded evidence, or meaningful uncertainty |
| `low_review` | Low-priority static evidence under credible coverage |
| `info` | Informational, no-action, or selector metadata under credible coverage |
| `unknown` | Priority cannot be stated due to limiting gaps |

Report-level `attentionLevel`:

| Value | Meaning |
| --- | --- |
| `highest_attention` | One or more critical rows or report-wide blocker gaps |
| `high_attention` | High review rows or broad static evidence concentration |
| `moderate_attention` | Medium review rows, partial analysis, or non-blocking gaps |
| `low_attention` | Low review rows under credible coverage |
| `informational` | No actionable static evidence under requested credible coverage |
| `unknown` | Requested scoring is materially limited by gaps, truncation, or unavailable workflows |

Section status for release-review v1:

```text
available
not_requested
unavailable
deferred
truncated
```

`not_supported` is intentionally excluded from release-review v1 because release-review is the first supported workflow. It can be added later for workflows that accept a scoring request before their scoring adapter exists.

## Scoring Strategy

V1 scoring is ordinal-only. It emits `severityHint`, `attentionLevel`, `complete`, component directions, rule IDs, and limitations; it does not emit numeric weights. Where the JSON schema includes `priorityScore`, v1 emits `null`.

Numeric scoring is deferred to a future scoring model version. If numeric scoring is later introduced, caps must be implemented as a documented function such as `effectiveScore = min(rawScore, capScore)` after positive components are summed, with unknown components setting `priorityScore: null` when the score would imply false completeness.

V1 ordinal row severity is a deterministic function:

1. Start every row at `info`.
2. If any row-level component has direction `unknown` for an unresolvable gap, set the row `severityHint` to `unknown` and `complete` to `false`.
3. Otherwise, apply positive candidate components in precedence order: `critical_review`, `high_review`, `medium_review`, `low_review`, `info`.
4. `critical_review` is allowed only for documented Tier1/Tier2 strong static change evidence on a public surface with credible coverage and source identity, plus either cross-repo reach or bounded path/reverse fan-out evidence.
5. `high_review` is allowed for documented Tier1/Tier2 strong static change evidence on a public surface under credible coverage and identity, or for broad deterministic fan-out under full evidence.
6. `medium_review` is the ceiling for review-tier, Tier3, syntax/textual, hash-only, ambiguous, duplicate, name-only, fallback, coverage-relative, noisy high-fan-out, or optional-schema-limited evidence.
7. `low_review` is used for low-priority static evidence under credible coverage when no stronger component applies.
8. `info` is used for no-actionable-evidence and selector metadata under credible coverage.
9. Apply caps after positive candidates. A cap can only lower a row to its documented ceiling and must remain visible as a component.
10. Sort rows and components deterministically after severity and completeness are assigned.

V1 report attention is also deterministic:

1. If any requested scoring section is materially incomplete because of unavailable workflow, truncation, missing required schema, source identity conflict, or coverage gaps that block section conclusions, report `attentionLevel` is `unknown`.
2. Otherwise, if any row is `critical_review`, report `highest_attention`.
3. Otherwise, if any row is `high_review`, report `high_attention`.
4. Otherwise, if any row is `medium_review` or any non-blocking gap remains, report `moderate_attention`.
5. Otherwise, if any row is `low_review`, report `low_attention`.
6. Otherwise, report `informational`.

Every candidate, cap, downgrade, unknown, and report aggregation rule must be documented in the rule catalog before output is enabled.

## Component Kinds

Initial component kinds:

| Component kind | Direction | Notes |
| --- | --- | --- |
| `static_change_evidence` | `increase` | Changed endpoint, surface, edge, DTO, SQL, package, or source evidence |
| `evidence_tier_strength` | `increase` | Tier1/Tier2 can raise priority under full coverage |
| `public_surface` | `increase` | Existing static public-surface evidence such as routes, API/DTO rows, package surfaces, route-flow roots, reverse selected surfaces, or portfolio shared surfaces |
| `cross_repo_reach` | `increase` | Existing combined/portfolio evidence spanning multiple source labels or source identity hashes |
| `review_tier_evidence` | `cap` | Tier3, syntax/textual, hash-only, or ambiguous evidence caps priority |
| `fan_out` | `increase` | Deterministic affected-source, surface, root, path, or edge counts |
| `path_context` | `increase` | Bounded path evidence, with traversal limits cited |
| `reverse_context` | `increase` | Bounded reverse root/path evidence, with target family cited |
| `coverage_gap` | `unknown` or `cap` | Reduced coverage prevents clean or strong conclusions |
| `identity_gap` | `unknown` or `cap` | Missing, duplicate, unverified, or conflicting identity |
| `commit_gap` | `unknown` or `cap` | Missing commit SHA limits history comparison |
| `schema_gap` | `unknown` or `cap` | Required or optional schema missing |
| `unavailable_workflow` | `unknown` | Requested workflow unavailable or deferred |
| `truncation` | `unknown` | Caps omitted evidence |
| `selector_no_match_credible` | `decrease` | Informational under verified credible coverage |
| `selector_no_match_uncertain` | `unknown` | Selector matched nothing but coverage or identity prevents a clean informational result |
| `no_actionable_evidence` | `decrease` | Only under credible full requested coverage |

The first implementation PR should document exact ordinal candidates, caps, precedence, and unknown behavior in code and rule catalog. Numeric component values and hidden weights are not part of v1.

## Downgrade Rules

Downgrades are first-class components, not silent edits.

- Reduced before coverage caps "added" evidence as coverage-relative.
- Reduced after coverage caps "removed" evidence as coverage-relative.
- Missing or conflicting source identity caps all affected source comparisons.
- Missing commit SHA prevents history-completeness claims.
- Missing required schema makes affected scoring unavailable.
- Missing optional precision schema caps affected evidence to review-tier.
- Syntax-only, textual, hash-only, ambiguous, duplicate, name-only, and fallback evidence caps affected row priority.
- High fan-out from noisy contract names caps reducer-derived rows at review-tier unless another strong non-noisy rule applies.
- Truncation makes report-level scoring incomplete and can force `unknown`.
- Requested but unavailable path/reverse/portfolio/release-review context is an uncertainty component.
- Missing required schema in the first release-review slice means schema already required by release-review to build requested sections. Optional precision schema means section-specific data that release-review can already treat as a gap while continuing.

## Workflow Integration Notes

### Release Review

Release-review is the recommended first adopter. Add a `reviewPriority` section after Summary or before Reviewer Checklist in Markdown, and a top-level `reviewPriority` JSON object. Checklist items can carry row-level annotations but must remain derived from evidence and gaps.

The first implementation should prefer a top-level sidecar object plus `reviewPriorityRows` keyed by stable row IDs. This avoids changing existing row shapes and makes opt-out byte-identity easier to prove. If nested annotations are chosen instead, bump or document the release-review JSON compatibility version.

Future workflow implementation PRs must define that workflow's required schema and optional precision schema before scoring is enabled. Use the release-review rule as the template: required schema is whatever the underlying workflow already requires to build requested sections; optional precision schema is data the workflow can already report as a gap while continuing.

### Combined Diff

Diff scoring should annotate diff rows. It should not rename `Added`, `Removed`, `ChangedEvidence`, `NeedsReviewDiff`, `UnknownAnalysisGap`, or `NoDiffEvidence`.

### Combined Impact

Impact scoring should annotate impact items and path-context rows. It should preserve `DefiniteImpact`, `ProbableImpact`, `NeedsReview`, `PathContextUnavailable`, `UnknownAnalysisGap`, and truncation semantics.

### Route Flow and Paths

Route-flow/path scoring should account for path strength, evidence tier, selector breadth, traversal depth, bounded frontier, root/surface count, and truncation. It must not claim runtime reachability.

### Reverse

Reverse scoring should account for selected surface type, target family, reverse root count, path count, path classification, source coverage, identity gaps, and caps. It must not claim runtime usage.

### Portfolio

Portfolio scoring should account for source count, cross-source shared surfaces, endpoint alignment, source coverage, optional diff/impact/path/reverse context, manifest identity hints, and portfolio caps. It must not infer runtime topology, ownership, business importance, or deployment coupling.

## Rule IDs

Suggested rule IDs:

```text
review.priority.component.v1
review.priority.aggregate.v1
review.priority.downgrade.v1
review.priority.coverage.v1
review.priority.identity.v1
review.priority.schema.v1
review.priority.truncation.v1
review.priority.workflow.v1
review.priority.selector.v1
```

Implementation can split these further. Every emitted component, row annotation, report summary, downgrade, unknown, and limitation must cite a documented rule.

## Markdown Output

Suggested release-review Markdown section:

```text
## Review Priority

Status: available
Model version: review-priority.v1
Attention level: high_attention
Completeness: partial, limited by coverage_gap and truncation

| Priority | Evidence | Components | Limitations |
| --- | --- | --- | --- |
```

Do not render raw values. Use safe identifiers, hashes, source labels, rule IDs, evidence tiers, file spans, and limitations.

## JSON Output

Suggested top-level shape:

```json
{
  "reviewPriority": {
    "status": "available",
    "modelVersion": "review-priority.v1",
    "attentionLevel": "high_attention",
    "priorityScore": null,
    "complete": false,
    "contributingSections": ["topChangedSurfaces", "pathContext"],
    "limitedSections": ["sourceCoverage"],
    "components": [],
    "limitations": []
  }
}
```

The top-level release-review document version or the scoring `modelVersion` must change whenever scoring fields, weights, ordinal precedence, cap behavior, or aggregation behavior changes. Adding `reviewPriority` to release-review JSON must either bump `ReleaseReviewDocument.Version` or explicitly document why the additive field is schema-compatible.

Row annotations should be nested on rows where supported:

```json
{
  "reviewPriority": {
    "severityHint": "medium_review",
    "priorityScore": null,
    "complete": true,
    "components": [],
    "sourceEvidenceIds": ["impact:..."],
    "limitations": []
  }
}
```

If compatibility concerns make row nesting too disruptive, emit a sidecar array keyed by stable row IDs:

```json
"reviewPriorityRows": [
  {
    "rowId": "impact:...",
    "severityHint": "medium_review",
    "priorityScore": 42,
    "components": []
  }
]
```

Prefer the sidecar approach for workflows with stable published schemas.

Sidecar `rowId` values must be derived from the underlying report row's existing stable ID whenever one exists. If a source row lacks a stable ID, the scoring adapter must derive one from deterministic safe fields such as section name, source label, stable key, classification, safe file path, line span, and source evidence IDs, never volatile database row IDs. The row ID derivation must be documented next to the scoring model and covered by byte-stability tests.

## Deterministic Ordering

Sort priority rows by:

1. unknown or incomplete rows before complete scored rows;
2. attention/severity precedence;
3. descending numeric score when present;
4. underlying workflow classification precedence;
5. evidence tier precedence;
6. source label;
7. surface kind or section name;
8. stable key;
9. safe file path;
10. start line;
11. stable row ID.

Sort components by component kind precedence, then direction, rule ID, evidence tier, evidence ID, metadata key, and component ID.

## Safety

Scoring must reuse existing safe renderers and metadata allowlists. It should never serialize raw source snippets, SQL text, config values, literal values, connection strings, raw URLs, hostnames, remotes, local absolute paths, private paths, or secret-looking values.

Sanitized errors should name the side, section, table, rule, or schema object, not unsafe input paths or raw values.

## Testing Strategy

Implementation tests should cover:

- release-review scored output for single and combined indexes;
- row annotation and report-level summary;
- all section status values;
- reduced coverage, missing commit SHA, identity conflict, schema gap, unavailable workflow, truncation, and selector no-match;
- syntax-only, hash-only, duplicate, ambiguous, and high-fan-out downgrade caps;
- deterministic aggregation and tie-breaking;
- byte-stable Markdown and JSON;
- no unsafe values in Markdown or JSON;
- rule catalog coverage for all scoring rule IDs;
- read-only input indexes.
- opt-out release-review output remains byte-identical when `--include-priority` is absent, unless an always-present `not_requested` section is explicitly versioned;
- closed-vocabulary exhaustiveness for emitted statuses, severities, and attention levels;
- Markdown delimiter escaping through the shared helper behavior;
- no-upgrade assertions that review-tier source rows are not promoted beyond documented static-evidence rules.

Spec-only PR validation should run private-path and whitespace checks. Implementation PR validation should add build, test, and relevant smoke checks.
