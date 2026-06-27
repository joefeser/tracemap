# Event Message Flow Composition Design

## Overview

This spec composes existing event/message surface evidence into downstream
review context. The composition layer is a deterministic reader over already
derived TraceMap artifacts. It does not scan source code, execute projects,
connect to brokers, inspect live topology, infer delivery, or classify impact.

The first implementation should be intentionally small: add shared
event/message flow-context vocabulary and wire one report/query consumer path.
The recommended PR 1 consumer is a hidden `messageReviewContext` section in
`tracemap report` because the combined report already reads message surfaces,
candidate edges, source coverage, rule IDs, evidence tiers, file spans, commit
SHA values, and extractor versions.

## Current Live Shape To Verify

Before implementation, verify current `origin/dev` and live code for:

- `.kiro/specs/event-message-surfaces/implementation-state.md`
- `rules/rule-catalog.yml`
- `src/dotnet/TraceMap.Core/Models.cs`
- `src/dotnet/TraceMap.Core/CombinedSurfaceProjection.cs`
- `src/dotnet/TraceMap.Reporting/CombinedDependencyReport.cs`
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`
- `src/dotnet/TraceMap.Reporting/CombinedReverseQuery.cs`
- `src/dotnet/TraceMap.Reduction/ContractDeltaReducer.cs`
- relevant tests such as `MessageSurfaceTests`,
  `CombinedDependencyReportTests`, `CombinedDependencyPathTests`,
  `CombinedReverseQueryTests`, `CombinedRouteFlowTests`, and reducer or
  release-review tests if touched.

The spec authoring pass found `event-message-surfaces` marked
`implemented-v1-with-follow-ups`; existing rule IDs and .NET constants include
the `message.surface.*` family and existing facts include
`MessagePublisherSurface`, `MessageConsumerSurface`,
`MessageBindingDeclared`, and report-level `message-publish-consume` edges.

## Claim Boundaries

Allowed wording:

- static message review context
- static publish/consume candidate
- shared static destination evidence
- one-sided publisher evidence
- one-sided consumer evidence
- binding-only static evidence
- reduced coverage
- needs review
- analysis gap

Forbidden wording:

- delivered message
- subscribed at runtime
- production traffic
- broker route exists
- exactly-once or guaranteed delivery
- consumer is live
- payload compatible
- impacted by this event
- safe to deploy
- AI-detected impact

## Inputs

PR 1 should read existing derived data only:

| Source | Purpose |
| --- | --- |
| `index_sources` | source label, repo identity, commit SHA, coverage, manifest metadata |
| `combined_facts` | existing message surface facts and message gaps |
| `combined_dependency_edges` | existing candidate edges and static dependency edge IDs |
| combined surface projection | message surface rows already normalized for reports |
| existing report/query coverage warnings | reduced/partial coverage labels |

PR 1 should not read raw source files. If a later consumer path needs path or
reverse traversal, it should reuse existing path graph inventory rather than
inventing new graph semantics.

## Proposed Rule IDs

If implementation emits new rule IDs or gap strings, catalog entries must be
added before product code emits them.

Recommended PR 1 additions:

| Rule ID | Tier | Emits | Notes |
| --- | --- | --- | --- |
| `message.flow.context.v1` | `Tier4Unknown` | message review context row | Composition metadata over existing message evidence. It does not prove source behavior beyond supporting rows. |
| `message.flow.gap.v1` | `Tier4Unknown` | message flow context gap | Closed vocabulary for unavailable, unsupported, truncated, ambiguous, unsafe, or reduced message context. |

Existing rules to reuse where possible:

| Rule ID | Use |
| --- | --- |
| `message.surface.combine.v1` | projected message surface rows |
| `message.surface.candidate-edge.v1` | static destination candidate edges |
| `message.surface.paths.v1` | path/reverse selectors and direction filters; deferred for graph context in PR 1 |
| `message.surface.reducer.v1` | later reducer context behavior |
| `message.surface.gap.v1` | source message extraction gaps |

Do not emit a new gap kind under an old rule if that rule catalog entry does
not document the limitation.

Composition gaps under `message.flow.gap.v1` describe why a downstream
report/query layer could not build review context from already-derived rows.
Extraction gaps under `message.surface.gap.v1` describe why a source publisher,
consumer, binding, or destination surface could not be extracted. The
implementation may summarize extraction gaps as supporting evidence, but it
must not merge extraction and composition gap vocabularies without catalog
coverage for both layers.

## Context Model

Suggested JSON shape for PR 1:

```text
messageReviewContext: {
  claimLevel: "hidden",
  status: "not_requested|available|partial|unavailable|selector_no_match|no_compatible_evidence",
  coverageLabel: "FullEvidenceAvailable|ReducedCoverage|PartialCoverage|UnknownCoverage|TruncatedCoverage",
  rows: MessageFlowContextRow[],
  gaps: MessageFlowContextGap[],
  limitations: string[]
}
```

Suggested row:

```text
MessageFlowContextRow
  contextId
  contextKind
  classification
  ruleId
  evidenceTier
  coverageLabel
  sourceLabels[]
  commitShas[]
  extractorVersions[]
  supportingFactIds[]
  supportingEdgeIds[]
  surfaceKinds[]
  operationDirections[]
  destinationIdentityStatus
  safeDestinationDisplay
  caveats[]
```

Suggested gap:

```text
MessageFlowContextGap
  gapId
  gapKind
  classification
  ruleId
  evidenceTier
  coverageLabel
  sourceLabels[]
  commitShas[]
  extractorVersions[]
  supportingFactIds[]
  supportingEdgeIds[]
  operationDirections[]
  message
```

`safeDestinationDisplay` may be a safe normalized destination key, an existing
approved shortened display hash, or `n/a`. It must not contain unsafe raw
destinations, config values, remotes, hostnames, local paths, or snippets.
`message.surface.identity.v1` is the source of truth for approved normalized
destination key and hash formats.

## Closed Status, Coverage, And Classification Values

PR 1 should define local constants, for example
`MessageFlowContextStatuses`, `MessageFlowContextCoverageLabels`, and
`MessageFlowContextClassifications`, following the per-report constant pattern
used elsewhere in the .NET reporting code. Do not rely on ad hoc strings.

Closed statuses:

| Status | Meaning |
| --- | --- |
| `not_requested` | Hidden context is supported but not requested or not enabled by the command mode. |
| `available` | Rows and/or gaps were built under credible coverage. |
| `partial` | Rows and/or gaps were built, but coverage, caps, schema, or source identity make the section partial. |
| `unavailable` | The consumer path cannot build context from the supplied artifact. |
| `selector_no_match` | User-supplied message filters match no compatible evidence. |
| `no_compatible_evidence` | Compatible inputs were read but no message surfaces, candidate edges, or message gaps exist. |

Closed coverage labels:

| Coverage label | Meaning |
| --- | --- |
| `FullEvidenceAvailable` | Compatible inputs have no known coverage warnings for the requested context. |
| `ReducedCoverage` | Existing report/source coverage is reduced or source metadata is incomplete. |
| `PartialCoverage` | Some requested context was built and some was unavailable. |
| `UnknownCoverage` | Coverage cannot be established from the artifact. |
| `TruncatedCoverage` | Caps omitted compatible context rows or gaps. |

Closed classifications:

| Classification | Meaning |
| --- | --- |
| `NeedsReview` | Static context exists but is review-tier only. |
| `UnknownAnalysisGap` | Missing, reduced, ambiguous, unsafe, or unsupported evidence prevents a credible context conclusion. |
| `PartialAnalysis` | Some requested context was available, but coverage is incomplete. |
| `TruncatedByLimit` | Caps omitted compatible rows or gaps. |
| `NoCompatibleMessageEvidence` | No compatible message evidence was found under credible full coverage. |
| `Unavailable` | The requested consumer path cannot build context from the supplied artifact. |
| `NotRequested` | Hidden context was not requested or enabled. |
| `SelectorNoMatch` | User-supplied filters matched no compatible rows. |

If an implementation chooses to reuse an existing command-local classification
set instead, it must document the mapping in `implementation-state.md` before
emitting rows.

## Closed Context Kinds

PR 1 should keep context kinds small:

| Context kind | Meaning |
| --- | --- |
| `message_surface_inventory_context` | One existing publisher, consumer, or binding surface is present and safe enough to summarize as review context. |
| `message_candidate_edge_context` | Existing `message-publish-consume` edge connects publisher and consumer rows through shared safe static destination identity. |
| `message_one_sided_context` | Only publisher-side or consumer-side evidence exists for a destination or selected surface. |
| `message_binding_only_context` | Binding/declaration evidence exists without publisher or consumer evidence. |

Later specs may add route-flow, reducer, release-review, portfolio, or public
export context kinds after catalog entries and tests are defined.

## Closed Gap Kinds

Recommended shared vocabulary:

| Gap kind | When to emit |
| --- | --- |
| `MessageContextNotRequested` | The command supports the section but the user did not request it and default behavior keeps it off. |
| `MessageContextUnavailable` | The consumer path cannot build context from this artifact or schema. |
| `MessageContextUnsupportedSchema` | Required combined tables or fields are missing. |
| `MessageContextNoCompatibleEvidence` | No existing message surfaces, candidate edges, or message gaps are present. |
| `MessageContextSelectorNoMatch` | User-selected message filters match no compatible rows. |
| `MessageContextReducedCoverage` | Source coverage, build status, or manifest metadata makes context partial. |
| `MessageContextDynamicDestination` | Destination identity is dynamic or unresolved. |
| `MessageContextHashedDestination` | Destination is represented only by approved hash metadata. |
| `MessageContextUnsafeValueOmitted` | Unsafe raw values were omitted from context. |
| `MessageContextAmbiguousDestination` | Multiple possible identities prevent a single context row from being stronger than review tier. |
| `MessageContextDuplicateSurfaceIdentity` | Duplicate stable identities prevent unique row interpretation. |
| `MessageContextNoStaticDestinationMatch` | Publisher and consumer rows exist but no safe static destination match exists. |
| `MessageContextNoGraphEvidence` | A later graph consumer found no static edge/path evidence. |
| `MessageContextTruncatedByLimit` | Caps omitted compatible context rows. |
| `MessageContextDirectionUnsupported` | A consumer path cannot honor a requested message direction filter; in PR 1 this is expected only if the chosen report/query path accepts direction filters. |

These are closed strings. Additions require spec/catalog updates.

For the recommended `tracemap report` PR 1 consumer,
`MessageContextDirectionUnsupported` is not expected to fire because
`tracemap report` does not accept message direction filter parameters. During
Task 5, document this as an explicit implementation-state deferral unless the
chosen PR 1 consumer path actually accepts a direction filter and cannot honor
it.

## PR 1 Combined Report Consumer

Recommended implementation steps:

1. Add rule catalog entries for `message.flow.context.v1` and
   `message.flow.gap.v1`, including explicit limitations.
2. Add report models for `messageReviewContext` under the existing combined
   dependency report output.
3. Populate rows from already projected message surfaces and existing
   `message-publish-consume` candidate edges.
4. Populate gaps from existing message `AnalysisGap` rows and consumer-path
   availability/cap conditions.
5. Keep context hidden and static-only in Markdown and JSON.
6. Add deterministic caps and stable sorting.
7. Add tests for empty arrays, one-sided rows, candidate edges, dynamic/hashed
   destination labels, reduced coverage, truncation, and redaction.

Suggested default behavior:

- JSON includes `messageReviewContext` with empty arrays and status even when
  no message evidence exists. The same JSON field set must be present for
  empty, partial, and populated states.
- Markdown emits a short hidden-context status line whenever the section is
  requested or enabled, even when rows and gaps are empty. Detailed row/gap
  tables may still be omitted when empty.
- Default caps: 100 context rows and 100 context gaps. If implementation reuses
  existing combined-report caps instead, document the reused constants in
  `implementation-state.md` and keep truncation tests pinned to those values.
- Row ordering: classification severity, context kind, source labels,
  destination display/status, surface kind, operation direction, rule ID,
  supporting ID.
- Gap ordering: classification severity, gap kind, source labels, rule ID,
  supporting ID.

## Classification Guidance

PR 1 classifications should avoid stronger impact language:

| Evidence | Classification |
| --- | --- |
| Candidate edge from safe static destination match | `NeedsReview` |
| One-sided publisher or consumer evidence | `NeedsReview` |
| Binding-only evidence | `NeedsReview` |
| Dynamic/hashed/unsafe destination | `UnknownAnalysisGap` or `NeedsReview` with explicit caveat |
| Reduced source coverage | `UnknownAnalysisGap` or `PartialAnalysis` |
| Truncated context | `TruncatedByLimit` plus reduced coverage |
| No compatible evidence under credible full coverage | `NoCompatibleMessageEvidence` or existing no-evidence equivalent |

`message.flow.context.v1` stays `Tier4Unknown` because it is composition
metadata. Rows must preserve the weakest supporting message evidence tier in
row metadata rather than upgrading the composition rule's tier.

Do not introduce `DefiniteImpact`, `ProbableImpact`, or delivery language for
message context rows.

## Safety

Context may render:

- safe source labels;
- language and framework family when already present as safe metadata;
- surface kind and operation direction;
- evidence tier and rule ID;
- safe repo-relative file span;
- commit SHA;
- extractor ID/version;
- supporting fact IDs and edge IDs;
- approved normalized destination keys or approved display hashes;
- coverage labels and caveats.

Context must not render:

- source snippets;
- raw payload values;
- raw SQL;
- raw config values;
- secrets;
- connection strings;
- raw remotes;
- local absolute paths;
- raw broker URLs or hostnames;
- raw subscription group IDs or routing keys classified as unsafe;
- raw destination values that the message identity rules hashed or omitted.

## Deferred Follow-Ups

- Reducer integration that uses message context for contract deltas while
  downgrading name-only, syntax-only, dynamic, hashed, ambiguous, generic, and
  high fan-out evidence.
- Release-review integration that imports message context as checklist/review
  context, not release approval.
- Route-flow async message boundary rendering with explicit caveats.
- Path/reverse graph context beyond the already supported message surface
  selectors.
- Roslyn Tier1 message extraction.
- TypeScript, Python, and JVM event/message adapter slices.
- Public-safe site/docs copy after hidden output has been reviewed.

## Test Strategy

Focused tests for PR 1:

- rule catalog contains every emitted new rule ID and limitations;
- JSON shape includes `messageReviewContext`, stable empty arrays, claim level,
  status, rows, gaps, limitations, coverage labels, and supporting IDs;
- Markdown uses static-only wording and escapes message values safely;
- candidate edge context preserves publisher and consumer supporting IDs;
- one-sided publisher and consumer evidence remain visible without invented
  opposite sides;
- binding-only rows remain visible;
- dynamic, hashed, unsafe, ambiguous, duplicate, and reduced-coverage rows emit
  review/unknown gaps;
- truncation emits `MessageContextTruncatedByLimit`;
- direction filter unsupported emits `MessageContextDirectionUnsupported` when
  the chosen consumer path accepts direction filters but cannot honor them;
- no safe publisher/consumer destination match emits
  `MessageContextNoStaticDestinationMatch`;
- duplicate stable identity emits `MessageContextDuplicateSurfaceIdentity`;
- JSON field sets are identical across empty, partial, and populated states;
- `claimLevel` is always `hidden`;
- rendered Markdown/JSON does not contain forbidden wording such as delivered,
  subscribed at runtime, exactly-once, payload compatible, impacted, production
  traffic, or consumer is live;
- candidate-edge context is labeled static destination-match evidence and not a
  call edge, delivery edge, or runtime subscription edge;
- composition gaps use `message.flow.gap.v1` and remain distinct from
  extraction-layer `message.surface.gap.v1` reasons;
- `message.flow.context.v1` rows keep composition `evidenceTier =
  Tier4Unknown` even when all supporting facts are Tier1 or Tier2;
- no-compatible-evidence under credible full coverage is distinct from reduced
  coverage gaps;
- private-output guard does not find raw paths, remotes, connection strings,
  raw snippets, or unsafe values.

Validation:

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "MessageSurfaceTests|CombinedDependencyReportTests"
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If the first implementation chooses a different consumer path, replace the
focused test command with the matching report/query test set and document the
choice in implementation state.
