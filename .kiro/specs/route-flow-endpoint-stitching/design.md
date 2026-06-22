# Route Flow Endpoint Stitching Design

## Overview

This spec is a continuation above the shipped `tracemap route-flow` slices. The
current implementation already has a route-flow report model, CLI, JSON/Markdown
writers, route/client selectors, combined path traversal, argument/fact-symbol
projection, interface-candidate rows, dependency-surface rows, and safety
guards. The missing product behavior from issue #201 is a clearer composition
layer that can prove each bridge from route selector to endpoint method, from
endpoint method to call graph, from interface call to implementation candidates,
and from downstream method to data/dependency surfaces.

The design reuses existing combined index evidence and route-flow output. It
does not introduce a second traversal engine. It should either extend the
current `CombinedRouteFlowReport` builder or extract small reusable helpers from
existing route-flow/path code when that reduces duplication without changing
other commands.

## Current-State Constraints

The implementation must start by auditing current `dev` behavior. Recent slices
already implemented many pieces that this spec depends on:

- `combined.route-flow.*` rule catalog entries.
- `RouteFlowClassifications` and summary rollup behavior.
- `combined_fact_symbols` and `combined_argument_flows` projection readers.
- Conservative route-flow interface bridge rows.
- Parameter-forward and object-creation route-flow fixtures.
- `DataSurfaceAttachmentMissing` and scoped projection gaps.
- Selector redaction and reduced-coverage hardening.

The implementation should not duplicate those features. It should add missing
bridge clarity, tests, or small helper extraction only where live inspection
shows a real gap.

## Data Sources

### Required Sources

- Combined source metadata: source label, language, scan ID, commit SHA, git
  root identity, analysis level, build status, coverage, extractor versions.
- Endpoint/route facts: HTTP route binding, endpoint alignment, client call
  evidence, WebForms/legacy roots where supported by current route-flow.
- Combined dependency/call edges: call, creates, forwards, dependency,
  parameter-forward, and path graph rows already used by `tracemap paths`.
- `combined_symbol_relationships`: implements/interface/override candidate
  evidence.
- `combined_fact_symbols`: fact-to-symbol attachment evidence.
- `combined_argument_flows`: argument/parameter forwarding context.
- Combined facts: query pattern, SQL shape, object/projection shape,
  repository/data access, package/config, service, legacy-data, message/event,
  WCF/ASMX/remoting, and other dependency/data evidence families.
- Analysis gaps from scans and combine.

### Optional Sources

Older combined indexes may lack optional route-flow precision tables. Missing
tables must not be treated as proof of no flow. Emit `SchemaMissing` or
`ExtractorUnavailable` style gaps under existing route-flow gap rules and
continue with lower precision evidence where safe.

## Stitching Pipeline

### 1. Selector Normalization

Normalize selector input using the existing route-flow selector grammar. Store
only safe selector traces:

- method family or selector kind;
- normalized route key hash or safe normalized route key when already public
  and non-sensitive;
- source label hash or safe label;
- matched root count;
- omitted unsafe field markers with redaction rule support.

Do not echo raw user selectors, raw URL/query strings, local paths, or private
tokens into output or logs.

### 2. Endpoint Root Resolution

Resolve route/client/endpoint evidence to endpoint root candidates. A root is
usable for downstream traversal only when it has a credible method symbol bridge
or an existing path graph node that represents the endpoint method.

Candidate root fields:

- source label and source identity state;
- route/client/endpoint fact IDs;
- endpoint method symbol ID/display name when safe;
- file span;
- rule ID and evidence tier;
- bridge state: `method-symbol`, `path-node`, `symbol-fallback`,
  `missing`, `ambiguous`, or `reduced-coverage`.

Missing or ambiguous root bridges emit closed-set gaps before traversal.

### 3. Endpoint Method to Call Edge

For each resolved root, traverse source-local direct call/creation/forwarding
edges already available to route-flow. Joins must be based on safe symbol IDs,
path graph nodes, supporting fact/edge IDs, or existing combined graph helper
identity. Same-file or short-name-only joins are not enough.

When downstream edges exist in the source index but no edge can be connected to
the selected endpoint root, emit a bridge-specific gap. This prevents a
mysterious generic no-evidence result.

### 4. Interface Candidate Bridge

When traversal reaches an interface member target, look for source-local
implementation relationships in `combined_symbol_relationships`. Candidate rows
must carry:

- relationship kind;
- source and target symbol identities;
- supporting relationship/fact IDs;
- evidence tier and file span;
- candidate reason;
- limitation text saying this is not runtime DI proof.

Classification cap rules:

- any bridge through implementation candidates is at most
  `NeedsReviewStaticRouteFlow`;
- syntax-only/name-only candidate evidence is review-tier;
- multiple candidates, high fan-out, cross-source, cross-language, missing
  identity, or reduced coverage stays review-tier or unknown;
- no arbitrary winner selection.

### 5. Downstream Surface Attachment

Attach data/dependency/business rows only through credible path adjacency:

- traversed source/target method symbol;
- selected path edge/fact IDs;
- `combined_fact_symbols` source-local symbol attachment;
- `combined_argument_flows` or parameter-forward rows already joined to the
  selected path;
- existing dependency surface projection rows already reachable from the path.

Do not attach by same file, directory, type name, or textual proximity alone.

If candidate surfaces exist but cannot join to the selected route path, emit a
scoped attachment gap with counts and safe categories.

## Proposed Models

Keep the existing route-flow report root. Additive internal/output models may
include:

```text
EndpointRootStitch
  id
  selectorKind
  rootKind
  sourceLabel
  sourceIndexId
  methodSymbolId
  methodDisplayName
  bridgeState
  classification
  evidence
  gaps[]

EndpointCallStitch
  id
  sourceEndpointRootId
  edgeKind
  callerSymbolId
  calleeSymbolId
  callBridgeState
  classification
  evidence
  limitations[]

ImplementationBridgeStitch
  id
  interfaceSymbolId
  candidateSymbolId
  relationshipKind
  candidateCount
  bridgeState
  classificationCap
  evidence
  gaps[]
```

These may be rendered as existing flow rows, bridge rows, or logic rows rather
than new top-level arrays if that preserves backward compatibility. If new
arrays are added, they must be additive and deterministic.

## Gap Codes

Reuse the shipped route-flow gap vocabulary before adding new names. The first
implementation slice must audit the live `CombinedRouteFlowReport` gap constants
and map concepts to existing emits before changing output.

Known concept-to-vocabulary mapping:

- endpoint method bridge missing: reuse existing `MissingMethodSymbolBridge`
  unless live audit proves it cannot express the root bridge state.
- dependency or data surface attachment missing: reuse existing
  `DataSurfaceAttachmentMissing` for terminal data/dependency surfaces that
  cannot stitch to the selected route path.
- unknown source identity, placeholder commit SHA, or unverified combined
  source identity: reuse existing `IdentityGap`.
- traversal or output cap reached: reuse existing `TruncatedByLimit` and
  `TraversalBounds` according to current route-flow behavior.
- missing optional schema or extractor precision: reuse existing `SchemaMissing`
  or `ExtractorUnavailable`.
- implementation bridge absence or ambiguity: reuse existing
  `ImplementationCandidateUnavailable` and
  `AmbiguousImplementationCandidates` where present.

Genuinely new closed-set gap names are allowed only for states the shipped
vocabulary cannot express. The likely first candidate is a duplicate or
ambiguous endpoint root state, because route-flow currently has selector and
method-symbol gaps but may not distinguish multiple normalized endpoint roots.

All emitted gaps must cite `combined.route-flow.gap.v1` or a documented
successor rule before implementation emits them. Do not add parallel aliases
such as `EndpointMethodBridgeMissing`, `UnknownSourceIdentity`, or
`DependencySurfaceAttachmentMissing` when the existing names above are
sufficient.

## Rule IDs

Use existing route-flow rules when possible:

- `combined.route-flow.selector.v1`
- `combined.route-flow.entry.v1`
- `combined.route-flow.path.v1`
- `combined.route-flow.interface-bridge.v1`
- `combined.route-flow.logic-surface.v1`
- `combined.route-flow.dependency-surface.v1`
- `combined.route-flow.argument-projection.v1`
- `combined.route-flow.fact-symbol-projection.v1`
- `combined.route-flow.classification.v1`
- `combined.route-flow.gap.v1`
- `combined.route-flow.redaction.v1`
- `combined.route-flow.report.v1`

If implementation adds a new rule ID, update `rules/rule-catalog.yml` before
emitting the rule. No evidence without a rule ID.

## Classification Rules

`StrongStaticRouteFlow` requires:

- full route-flow coverage;
- verified source identity and known commit SHA;
- unambiguous endpoint root;
- endpoint method bridge;
- source-local downstream call evidence;
- no required interface candidate bridge on the critical path;
- no blocking schema/extractor/truncation gaps.

`ProbableStaticRouteFlow` applies when evidence is strong but structural,
source-local, and complete enough to be credible without runtime proof.

`NeedsReviewStaticRouteFlow` applies when interface bridges, syntax fallback,
name-only evidence, dynamic routes, ambiguity, high fan-out, or weak
projection/attachment evidence is required.

`NoRouteFlowEvidence` requires full coverage and no unresolved selector,
schema, identity, extractor, or reduced-coverage gaps.

`UnknownAnalysisGap` wins when coverage, identity, schema, or extractor gaps
prevent a credible conclusion.

## Safety

All output rendering must reuse existing safe rendering helpers where
practical. Unsafe values include local paths, raw remotes, raw URLs, hosts,
query strings, config values, raw SQL, source snippets, private sample names,
secrets, and raw diagnostics. Unsafe data must be omitted, hashed, or replaced
with closed-set placeholders. Rows that omit or hash unsafe values should cite
`combined.route-flow.redaction.v1` in supporting rules where the existing report
model allows it.

## Tests

Focused tests should be synthetic and public-safe. The minimum useful test
matrix:

- route selector to endpoint method symbol bridge;
- missing endpoint method bridge gap;
- endpoint method to direct call edge;
- missing direct call edge gap under full and reduced coverage;
- single interface implementation candidate with review-tier cap;
- multiple implementation candidates with deterministic ambiguity;
- no implementation candidate gap;
- source-local dependency/data surface attachment;
- dependency/data surface present but unattached scoped gap;
- missing optional schemas produce schema gaps, not no-evidence;
- duplicate normalized routes do not choose arbitrary roots;
- dynamic or unsafe selectors are redacted;
- byte-stable JSON output and deterministic row ordering.

Run full `CombinedRouteFlowTests` if route-flow code changes, then full .NET
build/test before PR.

## Implementation Slices

Preferred first slice:

1. Audit current route-flow endpoint-root bridge behavior.
2. Add missing endpoint method bridge state/gaps and tests.
3. Add one direct call-edge stitch regression.
4. Add classification/gap rollup assertions.

Second slice:

1. Harden interface implementation candidate bridge with shared candidate helper
   reuse where possible.
2. Add ambiguity and no-candidate tests.

Third slice:

1. Add service/repository/data-surface attachment gap precision.
2. Add output/safety tests for business/data rows.

## Open Questions

- Whether endpoint root bridge state should render as a separate JSON array or
  as fields on existing entry evidence rows. Prefer additive fields on existing
  rows unless a separate array is clearer and backward-compatible.
- Whether a shared candidate builder should move from paths into reporting
  helpers before route-flow consumes it. Prefer no refactor unless tests prove
  duplication or drift.
- Whether high fan-out thresholds should reuse the current route-flow threshold
  exactly. Prefer reuse unless implementation finds an already documented
  threshold mismatch.
