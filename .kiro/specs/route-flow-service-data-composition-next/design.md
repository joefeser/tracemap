# Route Flow Service/Data Composition Next Design

## Overview

This continuation spec sharpens the next implementation slice for issue #179.
The original `route-flow-service-data-composition` work implemented the broad
foundation: route roots, route-flow graph traversal, argument-flow projection,
fact-symbol projection, interface-candidate boundaries, parameter-forward
bridging, terminal data-surface rows, specific composition gaps, redaction, and
focused tests. The next useful layer is route-flow review ergonomics and
coverage-hardening for service/data evidence that is already present in the
combined index.

The intended report shape is still the existing `tracemap route-flow` output:

```text
route selector
  -> entry evidence
  -> selected static flow rows
  -> grouped method/service context
  -> data/query/dependency/value-origin context
  -> explicit gaps and limitations
```

The implementation should reuse existing route-flow rows and helpers. It should
not create a new command, a second graph engine, a new route-flow report type,
or a competing rule namespace.

## Goals

- Make route-flow output easier to review when service, repository, query,
  dependency, and legacy data context is present.
- Reuse existing `flowRows`, `logicRows`, `dependencySurfaces`,
  `touchedFiles`, `touchedSymbols`, and gaps instead of inventing duplicate
  conclusions.
- Add bounded grouping or presentation metadata only when it can preserve
  supporting evidence IDs and downgrade semantics.
- Strengthen negative cases: unjoinable projection rows, missing optional
  tables, reduced coverage, unknown source identity, stale generated code, and
  truncation.
- Keep public/default output free of raw SQL, config values, snippets, local
  paths, private names, raw remotes, endpoint URLs, and secrets.

## Non-Goals

- No runtime request execution or route probing.
- No browser automation, telemetry import, production traffic inference, or
  service reachability proof.
- No runtime dependency-injection target proof, service-locator proof,
  reflection target proof, dynamic dispatch proof, or branch feasibility proof.
- No database connections, live schema introspection, SQL execution, query-plan
  inference, row counts, mutation semantics, or data-content proof.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No raw source snippets by default.
- No site/public-copy changes unless a later site spec chooses to describe the
  shipped capability.

## Relationship To Existing Specs

This spec is a continuation, not a replacement.

| Existing spec | Relationship |
| --- | --- |
| `route-centered-static-flow-report` | Owns the route-flow command, route-flow report contract, classifications, and broad route-flow concepts. |
| `route-flow-service-data-composition` | Owns the initial #179 projection and composition implementation. This spec consumes its remaining follow-up: broader object-shape, repository-like, and data-surface traversal fixtures. |
| `route-centered-endpoint-trace-completeness` | Owns touched-file/touched-symbol summaries and broad endpoint trace completeness. It originally listed remaining tasks 8-10 for method/service grouping, data/query/dependency rows, and downgrade hardening. This spec owns only the route-flow service/data composition subset: grouped presentation over already-selected route-flow rows, data/query/dependency/value-origin context already joined to selected route-flow evidence, and downgrade tests for those grouped rows. It does not supersede selector trace metadata, touched-file/touched-symbol summaries, or unrelated endpoint-trace completeness backlog. |
| `query-pattern-reporting-v2` and `sql-dependency-surfaces` | Provide query and SQL surface evidence families to render only when joined through route-flow evidence. |
| `legacy-data-model-metadata-extraction` and `legacy-data-model-reporting-integration` | Provide optional legacy-data context; route-flow can render it only as static evidence with no runtime database claim. |
| `ui-field-property-lineage` | Provides adjacent UI/property lineage context but should not be mixed into route-flow unless selected route-flow evidence supports the join. |

If live code has already implemented one of these items, the implementation
should update this spec state and choose the next smallest documented item
rather than redoing it.

## Proposed Slice

The recommended implementation slice is **service/data context grouping and
downgrade hardening** over existing route-flow rows:

1. Build a route-flow view model over existing rows:
   - method/service rows from selected `flowRows`;
   - data/query/dependency rows from selected `logicRows` and
     `dependencySurfaces`;
   - value-origin rows from already-joined argument/parameter-forward context;
   - gap rows from existing route-flow gaps.
2. Add optional grouping labels or sections only if they are backward
   compatible and supported by existing row data.
3. Add focused tests proving grouping does not upgrade evidence tiers or hide
   supporting IDs.
4. Add fixtures for missing/ambiguous/unjoinable service-to-data context.
5. Add safety tests for metadata values that could otherwise leak raw SQL,
   config, endpoint, route, snippet, path, remote, or secret-like data.

## Evidence Model

Each grouped item or new row must preserve:

- `rowId` or equivalent stable ID;
- row kind and safe display label;
- source label and source index ID where available;
- route root safe key;
- classification;
- coverage state;
- evidence tier;
- rule ID and supporting rule IDs;
- supporting fact IDs;
- supporting edge IDs;
- supporting symbol IDs where available;
- repo-relative file path and line span where available;
- commit SHA and extractor identity where available;
- limitations and gap reasons.

Grouping is presentation context, not a new evidence conclusion. A grouped item
inherits the weakest classification, weakest coverage state, and weakest
evidence tier among the rows that support it.

Grouping and section metadata is stamped by the report-envelope rule
`combined.route-flow.report.v1` and inherits supporting row rule IDs. New
grouping fields such as `groupKind`, `matchKind`, and `valueSafety` should be
additive optional fields on existing route-flow row objects or an additive
report-context object that references existing row IDs; older rows may omit
these fields or render them as `null`.

When contributing rows have multiple extractor identities, grouped context
should either emit the sorted set of extractor identities or omit the field and
rely on supporting row references. It must not pick an arbitrary extractor.

## Classification And Downgrade Rules

Use only existing route-flow classifications:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

Strong static rows require full relevant coverage and direct credible static
evidence. Interface candidates, syntax-only evidence, name-only evidence,
ambiguous relationships, generated-code uncertainty, high fan-out, reduced
coverage, missing schema, missing extractor identity, unknown commit SHA,
truncation, or unjoinable projection rows cap the affected item at
`NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.

Clean `NoRouteFlowEvidence` is allowed only when selectors match, relevant
coverage is full, required route-flow evidence tables are available, and no
specific bridge/projection/coverage gap is present. Under reduced coverage, use
gap evidence rather than clean absence.

## Gaps

Reuse `combined.route-flow.gap.v1` where possible. The implementation should
prefer existing closed-set gap codes unless a new code is truly needed and
documented before use.

Expected gap families:

- selector or route-root gaps;
- optional schema/table/column availability gaps using existing
  `SchemaMissing`;
- missing extractor-family evidence using existing `ExtractorUnavailable`;
- argument projection unavailable;
- fact-symbol projection unavailable;
- unsupported attached fact-symbol context;
- missing implementation candidate;
- ambiguous implementation candidates;
- data-surface attachment missing;
- service/repository bridge gaps represented by existing shipped narrower
  codes such as `MissingCallEdge`, `MissingImplementationBridge`,
  `ImplementationCandidateUnavailable`, or `DataSurfaceAttachmentMissing`;
- reduced coverage;
- unknown source identity or commit SHA;
- stale generated code;
- truncation by row, depth, frontier, path, logic-row, or gap caps using
  existing `TruncatedByLimit`.

Do not emit a separate `ControllerToServiceBridgeMissing` gap in this slice;
that provisional name was retired by the shipped v1 route-flow rule catalog in
favor of the narrower codes above.

Gaps must not include unsafe selector values, raw query text, raw config values,
raw endpoint URLs, local absolute paths, raw remotes, private sample names, or
source snippets.

## Safe Metadata

Safe metadata should use closed-set values and stable hashes:

- `groupKind`: `method`, `service`, `interface-candidate`, `repository`,
  `query`, `data-surface`, `dependency`, `legacy-data`, `value-origin`, `gap`.
- `evidenceKind`: public-safe high-level fact family name, not raw internal
  fact type when that would expose unstable implementation detail. V1 allowed
  values should be closed and documented before use; until then,
  `evidenceKind` must not participate in stable ID derivation.
- `matchKind`: `direct-call`, `candidate`, `argument-flow`,
  `parameter-forward`, `fact-symbol`, `dependency-surface`, `gap`.
- `valueSafety`: `safe`, `hashed`, `omitted`, or `unavailable`.
- `coverage`: existing coverage label or `unavailable`.

The implementation must choose one output placement for grouping metadata
(additive fields on existing row objects or an additive report-context object)
and record that decision in this spec's implementation state. It should not
split the same metadata across both shapes without a reviewable compatibility
reason.

Unsafe values must be omitted or hashed with context-separated hashing. Rows
that hash or omit unsafe values should cite `combined.route-flow.redaction.v1`.

## Determinism

The implementation must sort rows and metadata maps deterministically. Stable
IDs may include:

- safe source label;
- source index ID;
- route root safe key;
- row kind;
- supporting fact IDs;
- supporting edge IDs;
- supporting symbol IDs;
- repo-relative file path;
- line span;
- closed-set group kind.

Stable IDs must not include timestamps, temp paths, local absolute paths, raw
remotes, raw URLs, raw SQL, raw config values, raw route strings, private names,
or nondeterministic row order.

## Validation Strategy

Focused implementation tests should extend route-flow tests rather than adding
a parallel test harness. Test data should be synthetic or public-safe.

Minimum coverage:

- direct controller-to-service-to-repository method grouping;
- interface single-candidate, multiple-candidate, and no-candidate grouping;
- data/query/dependency context reached by direct path evidence;
- value-origin context reached by argument/parameter-forward evidence;
- fact-symbol context attached to selected source-local symbols;
- unjoinable adjacent data/query/dependency facts producing gaps;
- old combined indexes with missing optional tables;
- reduced coverage preventing strong/clean conclusions;
- unknown commit or source identity downgrade;
- deterministic JSON and stable IDs;
- redaction of raw SQL, config, URL, route, snippet, path, remote, and
  secret-like values;
- rule catalog resolution for every emitted rule ID.

Pinned smoke checks from `docs/VALIDATION.md` should run when shared route-flow,
combined traversal, report output, or safety behavior changes. Private legacy
ASP.NET validation may be recorded only as local-only evidence and must not
commit private paths, names, routes, or generated outputs.
