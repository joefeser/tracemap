# Release Review Report Design

## Overview

Release Review Report is a composition layer over TraceMap evidence. It should not become a new scanner, graph traversal engine, contract matcher, or risk oracle. Its job is to gather already-indexed and already-classified evidence into one deterministic packet a human can review before a release.

```text
before index + after index
  -> source identity and coverage validation
  -> existing diff / impact / path / reverse workflows
  -> release-level rollup and checklist
  -> release-review.md + release-review.json
```

The report may reference future workflows as unavailable sections, but implementation should only render evidence that exists.

## Goals

- Provide one release-oriented report over before/after TraceMap indexes.
- Preserve source provenance, commit SHAs, rule IDs, evidence tiers, spans, and limitations from underlying workflows.
- Make gaps and reduced coverage visible in the summary and reviewer checklist.
- Keep Markdown/JSON deterministic and safe to publish.
- Avoid release approval, CI gating, runtime certainty, or generated risk conclusions.

## Non-Goals

- No CI policy engine.
- No merge/release approval decision.
- No production telemetry or deployment analysis.
- No source text diffing outside indexed facts.
- No OpenAPI, SQL schema, package compatibility, or runtime dependency certainty.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Relationship to Existing and Planned Work

Release Review Report should consume these workflows when available. It should prefer existing report builders and classification/downgrade rules over reimplementing them.

| Workflow | Release report use |
| --- | --- |
| `combined-dependency-diff` | Source, coverage, endpoint, surface, edge, and optional path changes |
| `combined-change-impact` | Impact item conversion, coverage-relative downgrade semantics, and impact classifications |
| `contract-delta-impact-v2` | Structured contract delta impact findings |
| `api-dto-contract-diff` | Endpoint, DTO, route, handler, request/response, and method contract changes |
| `sql-schema-change-impact` | SQL/schema/table/column/query-shape impact findings |
| indexed package evidence | Package declaration, lockfile, import, usage, and package-surface diffs already present in indexes |
| future `package-upgrade-impact` | Package upgrade compatibility/usage impact when that workflow is specified and implemented |
| `parameter-value-origin-flow` | Optional value-origin path context when implemented |
| `combined-dependency-paths` | Bounded static path evidence |
| `reverse-impact-query` | Bounded reverse surface-to-root evidence |

Unavailable or unimplemented workflows should be represented as explicit section status, not silent absence.

The distinction from `tracemap diff --include-paths` is packaging and composition: `diff` reports dependency evidence changes, while `release-review` assembles diff, impact, optional path/reverse context, gaps, and checklist items into one release packet. It must not create a stronger conclusion than the underlying workflows provide.

## Command Shape

Proposed command:

```bash
tracemap release-review \
  --before before.sqlite \
  --after after.sqlite \
  --out /tmp/release-review \
  --contract-delta contract-delta.json \
  --sql-schema-delta sql-delta.json \
  --package-delta package-delta.json \
  --include-paths \
  --include-reverse
```

Important defaults:

- `--include-paths` is off by default.
- `--include-reverse` is off by default.
- No delta file is required.
- Directory output writes both `release-review.md` and `release-review.json`.
- File output defaults to Markdown unless `--format json` is specified.
- Directory or extensionless output writes both files even when `--format` is supplied in v1.
- Inputs are opened read-only.
- `--exit-code` is deferred in v1; the command exits 0 on successful report generation regardless of rollup classification and exits non-zero only for command/input errors.

`--package-delta` is accepted as a future package workflow input. Until a package-upgrade workflow exists, package delta sections should render `deferred` or `unavailable` with a gap. Indexed package surfaces can still appear in Top Changed Surfaces or Package Impact when they are present in snapshot diff evidence.

## Data Model

### ReleaseReviewReport

Suggested JSON root:

```text
reportType
version
mode
query
beforeSnapshot
afterSnapshot
summary
sourceCoverage
topChangedSurfaces
contractImpact
apiDtoChanges
sqlSchemaImpact
packageImpact
pathContext
reverseContext
gaps
reviewerChecklist
limitations
```

Every list should be deterministic. Arbitrary finding, gap, and evidence metadata should use one canonical JSON representation:

```json
"metadata": [
  { "key": "example", "value": "safe-value" }
]
```

Metadata entries should be sorted by `key` and then `value` using ordinal comparison. Release-review JSON should not emit arbitrary metadata as unordered dictionaries; fixed report objects such as `summary` and `query` may remain normal JSON objects when their property order is controlled by the writer.

### Section Status

Each optional section should include:

```text
status: available | not_requested | unavailable | deferred | truncated
findings: []
gaps: []
limitations: []
```

This prevents a missing section from reading as a clean result.

Use the status vocabulary exactly:

| Status | Meaning |
| --- | --- |
| `available` | The section was requested or included by default and the required evidence/workflow exists |
| `not_requested` | The section is optional and the user did not request it |
| `unavailable` | The current index mode, schema, or implementation cannot provide this section |
| `deferred` | The section depends on a future specified workflow or future index evidence |
| `truncated` | The section has evidence but caps omitted rows or gaps |

### Rollup Classification

Release-level rollup classification is a summary label, not a replacement for underlying classifications. The report should retain all underlying classifications next to each finding.

Primary rollup selection uses the first matching rule in this fixed precedence order:

| Priority | Rollup | Deterministic trigger |
| --- | --- |
| 1 | `UnknownAnalysisGap` | Identity conflict, schema gap, missing precision table, unknown commit SHA, or coverage gap prevents credible comparison for a requested section |
| 2 | `TruncatedByLimit` | Any requested section hit a cap that omitted findings or gaps |
| 3 | `ActionableStaticEvidence` | At least one strong/probable finding exists under verified source identity and full or comparable coverage |
| 4 | `ReviewRecommended` | Findings are review-tier, syntax-only, hash-only, coverage-relative, identity-unverified, high-fan-out according to an underlying workflow, or ambiguous |
| 5 | `PartialAnalysis` | Requested sections have reduced coverage, unavailable/deferred workflow status, or unknown commit SHA, but comparison is still partially credible and no stronger rollup matched |
| 6 | `SelectorNoMatch` | Selectors matched no evidence and no higher-priority gap/truncation matched |
| 7 | `NoActionableEvidence` | No actionable findings under verified identity and full requested coverage |

Rollup classification must never say `Safe`, `Approved`, `Ready`, or `ReleaseAllowed`.

Strong/probable input classifications include existing values such as `DefiniteImpact`, `ProbableImpact`, `StaticImpactEvidence`, `ProbableStaticImpact`, `Added`, `Removed`, and `ChangedEvidence` only when coverage and source identity permit strong comparison. Coverage-relative forms such as `AddedWithBeforeGap`, `RemovedWithAfterGap`, hash-only evidence, and syntax/textual evidence are review-tier inputs.

## Markdown Report

Markdown section order:

1. Summary
2. Compared Snapshots
3. Source Identity and Coverage
4. Top Changed Surfaces
5. Contract Delta Impact
6. API and DTO Changes
7. SQL and Schema Impact
8. Package Impact
9. Path and Reverse Context
10. Analysis Gaps
11. Reviewer Checklist
12. Limitations

The Summary should include:

- report mode;
- before and after commit SHAs;
- source count;
- actionable finding count;
- review-tier finding count;
- gap count;
- rollup classification;
- path/reverse requested flags;
- top coverage and identity warnings.

The Summary must avoid release approval language.

## Reviewer Checklist

The checklist is deterministic and evidence-derived. It should not be generic advice. Each checklist item should link to one or more finding IDs or gap IDs.

Example checklist item sources:

- source identity conflict;
- reduced after coverage with removed evidence;
- contract delta with static impact evidence;
- SQL/schema impact with hash-only evidence;
- API/DTO changed evidence;
- package change with usage evidence;
- path context truncated;
- reverse context unavailable;
- unsupported workflow requested.

Checklist item severity can use:

```text
must_review
should_review
informational
```

Severity must be derived from classification and gaps using this fixed mapping:

| Severity | Trigger examples |
| --- | --- |
| `must_review` | `UnknownAnalysisGap`, `TruncatedByLimit`, source identity conflict, requested workflow unavailable, reduced coverage affecting removals, or actionable static evidence |
| `should_review` | `ReviewRecommended`, `PartialAnalysis`, coverage-relative findings, hash-only/syntax-only findings, or deferred requested sections |
| `informational` | `SelectorNoMatch`, `NoActionableEvidence`, `not_requested` optional sections, or purely descriptive snapshot metadata |

Gap-only reports with no actionable findings should preserve the table above: blocking gaps such as `UnknownAnalysisGap`, `TruncatedByLimit`, source identity conflicts, requested workflow unavailable, and reduced coverage affecting removals remain `must_review`. Non-blocking or partial-analysis gaps use `should_review`. `informational` applies only when there are no analysis gaps or when the item describes intentionally omitted optional context such as a `not_requested` section.

## Safety and Redaction

Release review must reuse existing safe rendering helpers. It must not render:

- raw SQL;
- source snippets;
- literal values;
- config values;
- connection strings;
- raw URLs;
- local absolute paths;
- private paths;
- secret-looking values.

Markdown table cells must escape user-controlled syntax. JSON should omit or hash unsafe values rather than preserving raw values for downstream tools.

## Source Identity and Coverage

Release review starts with source pairing:

- single indexes compare one before source and one after source;
- combined indexes pair by source label;
- before-only or after-only sources become added/removed source evidence with coverage and identity caveats, not silent omissions;
- identity conflict blocks strong comparison for that source;
- unknown commit SHA or reduced coverage must be visible in summary and checklist;
- missing optional tables become section-specific gaps.

Coverage-relative findings should be carried through from underlying workflows. Release review must not turn coverage-relative rows into definite changes.

## Path and Reverse Context

Path and reverse context are optional because they can be expensive and because absence of paths is not always meaningful under reduced coverage.

Rules:

- no `--include-paths`: path section status `not_requested`;
- no `--include-reverse`: reverse section status `not_requested`;
- combined index only for v1 path/reverse context;
- single-index mode with requested path or reverse context renders `unavailable` or `deferred` section status and a release-review gap rather than running traversal;
- in single-index mode, an explicit `--include-paths` or `--include-reverse` request takes precedence over the default `not_requested` status and renders the requested subsection unavailable/deferred;
- traversal limits produce `TruncatedByLimit`;
- unavailable selectors produce explicit context gaps.

Scope routing should favor existing workflow translation. If release review wraps `combined-change-impact`, `--scope coverage` should be delegated through that workflow's coverage-to-source translation and filtered coverage rows. If release review calls `combined-dependency-diff` directly, it should pass `sources` to the diff layer and filter coverage rows afterward. It should never pass `coverage` unchanged to a workflow that does not accept it.

Cap mapping should be explicit so release-review caps do not double-count or silently override underlying workflows:

| Release-review cap | Underlying mapping |
| --- | --- |
| `--max-findings` | Maximum release findings after deterministic merge/sort |
| `--max-surface-rows` | Top Changed Surfaces rows after deterministic sort |
| `--max-paths` | Path/reverse rows exposed by release review and the underlying path cap where compatible |
| `--max-gaps` | Release-level gaps after deterministic sort |
| `--max-checklist-items` | Evidence-derived checklist rows after deterministic sort |

If a release-review cap is lower than an underlying workflow cap, the release-review cap controls output and records omitted counts. If an underlying workflow truncates before release-review sees all rows, release-review preserves the underlying truncation gap.

## Rule Catalog Expectations

Implementation should add rule catalog entries before emitting new release-review rows or gaps. Suggested rule IDs:

- `release.review.rollup.v1`
- `release.review.checklist.v1`
- `release.review.source.v1`
- `release.review.section.v1`
- `release.review.selector.v1`
- `release.review.truncation.v1`

If an implementation reuses existing rules for a row, it should preserve the original rule ID and use release-review rules only for the rollup/checklist/section wrapper.

## Determinism

Stable IDs should be derived from:

```text
report version + side + section + source label + underlying finding/gap ID + stable metadata hash
```

`side` should be `before`, `after`, `paired`, or `report` depending on whether the item belongs to a snapshot side, a paired comparison, or a report-level rollup/gap. Do not use timestamps, wall-clock runtime, random IDs, local absolute paths, or volatile database row IDs in stable IDs.

## Implementation Slices

Recommended slices:

1. Report model, command shell, source identity/coverage summary, unavailable section statuses.
2. Compose combined dependency diff output and render top changed surfaces.
3. Compose contract delta impact v2 and API/DTO diff sections.
4. Compose SQL/schema and package impact sections.
5. Add optional path/reverse context and reviewer checklist.
6. Add full byte-stability, safety, and sample workflow validation.

The first implementation can ship with unavailable sections for workflows not yet implemented, as long as the report says so explicitly.

## Validation

Implementation PRs should run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If the implementation composes TypeScript/JVM/Python outputs indirectly through combined indexes, use the relevant pinned validation docs and sample smoke scripts when behavior changes.
