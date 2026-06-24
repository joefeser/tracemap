# Static Dispatch Candidate Bridges Design

## Overview

Create a shared static dispatch candidate bridge layer over existing combined
TraceMap evidence. The layer derives conservative candidate edges from
source-local call edges, symbol relationships, and optional DI registration
evidence, then lets route-flow, reverse, impact, report, vault export, and
docs-export/RAG-oriented artifacts consume the same candidate semantics.

The evidence flow is:

```text
combined call edge
  + source-local symbol relationships
  + optional DI registration evidence
  + source coverage and schema metadata
  -> shared in-memory candidate edge / gap model
  -> paths, route-flow, reverse, impact, report, vault, docs-export consumers
```

The design deliberately avoids runtime proof language. A candidate edge means
TraceMap found a documented static reason to continue review through an
abstraction boundary. It does not mean the target is selected at runtime.

## Goals

- Refine the broad interface/override/DI approximation spec into a smaller
  implementation slice.
- Share candidate derivation across consumers instead of letting route-flow,
  paths, reverse, and impact each invent different bridge behavior.
- Preserve rule IDs, evidence tiers, supporting fact/edge IDs, file spans,
  commit SHAs, extractor versions, coverage labels, and limitations.
- Add DI registration-context explanation where statically visible
  registration evidence and relationship compatibility agree.
- Keep candidate output deterministic, capped, review-tier, and safe for
  public artifacts.
- Emit explicit gaps for missing, ambiguous, unsupported, generic, high
  fan-out, schema-missing, and reduced-coverage states.

## Non-Goals

- No runtime DI container execution, service provider inspection, or runtime
  binding selection.
- No runtime dispatch proof, dynamic proxy expansion, reflection execution, or
  branch feasibility inference.
- No source scanning changes in the first bridge-consumer slice unless live
  audit finds a missing rule-catalog gap required to consume existing evidence.
- No persisted candidate edge table in the combined database.
- No UI, site, telemetry, graph database, LLM calls, embeddings, vector
  database, prompt-based classification, or probabilistic ranking.
- No raw snippets, raw SQL, raw config, raw URLs, hostnames, raw remotes, local
  absolute paths, private labels, or secrets in default artifacts.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `interface-override-di-approximation` | Broad parent. This spec consumes the follow-up work after the paths-only slice. |
| `route-flow-endpoint-stitching` | Route-flow bridge clarity must use the shared candidate builder and keep interface bridge rows review-tier. |
| `route-flow-service-data-composition-next` | Service/data grouping may display candidate context, but grouping must inherit candidate downgrades. |
| `combined-dependency-paths` | Existing path graph candidate behavior is the starting point for shared builder extraction. |
| `reverse-impact-query` | Reverse traversal should reuse the same candidate edges and cap at `NeedsReviewReversePath`. |
| `contract-delta-impact-v2` and combined impact specs | Impact context may cite candidate paths, but candidates do not become strong static impact. |
| Vault/docs-export/RAG evidence specs | Exports preserve candidate evidence as citation-rich review context, not normal call edges. |

## Proposed Implementation Slices

### Slice 1: Audit and Extract Shared Model

- Audit live `CombinedDependencyPath` candidate derivation from PR #271.
- Identify the smallest shared internal model that can represent candidate
  edges, candidate support, and candidate gaps without changing output.
- Move or wrap path-specific derivation into a reporting/query-layer service
  that accepts combined graph/read evidence and returns candidate edges/gaps.
- Preserve existing path tests and output first.

### Slice 2: DI Registration Context Candidate Annotation

- Audit existing `DependencyRegistered` facts under
  `csharp.semantic.runtimeevidence.v1` and any fact-symbol attachments that can
  identify service and implementation symbols.
- Add registration context only as an annotation on relationship-backed
  candidates.
- Emit gaps for unsupported DI shapes:
  factories, lambdas, assembly scanning, module registration, keyed/named
  services, decorators, service locators, reflection, config, dynamic branches,
  registration order, custom containers, and open generics.
- Keep all registration-context candidates at review-tier.

### Slice 3: Route-Flow Consumer

- Replace route-flow-specific interface bridge derivation, where present, with
  the shared candidate builder or an adapter over it.
- Render candidate bridge rows with `combined.route-flow.interface-bridge.v1`
  and supporting `combined.dispatch-candidate.v1`.
- Ensure affected route-flow classifications remain at most
  `NeedsReviewStaticRouteFlow`.
- Add tests for direct call, single candidate, multiple candidate, no
  candidate, high fan-out, DI-context candidate, and reduced coverage.

### Slice 4: Reverse and Impact Context

- Let reverse traversal use shared candidate edges while preserving reverse
  path direction and candidate limitations.
- Ensure candidate-containing reverse paths cap at `NeedsReviewReversePath`.
- Ensure `tracemap impact --include-paths` and future reverse context classify
  candidate-dependent context as `NeedsReviewImpact`,
  `PathContextUnavailable`, `ReverseContextUnavailable`, or
  `UnknownAnalysisGap`, not `StaticImpactEvidence`.

### Slice 5: Report, Portfolio, Vault, and Docs Export

- Add candidate inventory summaries to combined report and portfolio report:
  candidate counts, registration-context counts, fan-out caps, unsupported
  DI shapes, and gaps.
- Export candidate edges/gaps into vault graph as review edges, not normal call
  edges.
- Include candidate evidence in docs-export/RAG-ready chunks with rule IDs,
  tiers, supporting IDs, coverage labels, and limitations.
- Add safety and forbidden-wording tests across all rendered artifacts.

## Rule Model

Use existing rules before adding successors:

| Rule ID | Use |
| --- | --- |
| `combined.dispatch-candidate.v1` | Shared derived candidate edge over call and relationship/DI evidence. |
| `combined.dispatch-gap.v1` | Shared missing, ambiguous, unsupported, fan-out, schema, and coverage candidate gaps. |
| `combined.route-flow.interface-bridge.v1` | Route-flow presentation row for implementation/interface bridge candidates. |
| `combined.route-flow.gap.v1` | Route-flow gap presentation. |
| `combined.reverse.path.v1` | Reverse path presentation that may include candidate edges as supporting evidence. |
| `contract.delta.context.v2` or existing combined impact rules | Impact path/reverse context gaps and review-tier findings. |
| `vault-export.*` | Vault graph nodes, edges, gaps, validation, and redaction. |
| `docs-export.*` | Docs/RAG-oriented chunks, gaps, limitations, validation, and redaction. |

If a successor rule is needed, implementation must update
`rules/rule-catalog.yml` before emitting it and add catalog-resolution tests.

Before shared builder implementation emits new gap kinds, audit the live
`combined.dispatch-gap.v1` catalog entry. If it only documents
`DispatchCandidateFanOut`, expand that entry or add a successor rule for the
complete closed vocabulary before product code emits registration, generic,
schema, identity, or missing-candidate gaps.

Vault and docs-export consumers must audit the existing `vault-export.*` and
`docs-export.*` rule families. If no existing presentation rule can honestly
wrap candidate edges, add new candidate edge/gap rules before emitting vault or
docs-export candidate artifacts.

## Classification Model

Shared candidate derivation states are internal model fields, not emitted
consumer classifications:

| State | Meaning |
| --- | --- |
| `SymbolBackedCandidate` | Fully symbol-backed static candidate from call plus relationship evidence. Consumer output still caps at review-tier. |
| `WeakerCandidate` | Depends on weaker evidence, ambiguity, high fan-out, type-level bridging, generic caveats, reduced coverage, or registration-only context. |
| `CandidateGap` | Derivation cannot prove or disprove a candidate because evidence is unavailable, unsupported, truncated, or reduced. |

The existing paths note code `StaticDispatchCandidate` is a consumer-specific
path note for candidate context. It is intentionally non-strengthening and does
not create a shared emitted classification.

Consumer caps:

| Consumer | Candidate cap |
| --- | --- |
| Paths | `NeedsReviewPath` |
| Route-flow | `NeedsReviewStaticRouteFlow` |
| Reverse | `NeedsReviewReversePath` |
| Impact | `NeedsReviewImpact`, `PathContextUnavailable`, `ReverseContextUnavailable`, or `UnknownAnalysisGap` |
| Report/portfolio | Review/partial inventory context only |
| Vault/docs-export/RAG | Review context chunks/edges only |

No consumer may use candidate evidence to produce a strong/definite/runtime
classification by itself.

## Candidate Edge Model

Suggested internal model:

```text
DispatchCandidateEdge
  candidateId
  algorithmId
  classification
  sourceIndexId
  sourceLabel
  callEdgeId
  abstractionSymbolId
  candidateSymbolId
  candidateMemberSymbolId
  candidateTypeSymbolId
  relationshipKind
  bridgeKind
  evidenceTier
  ruleId
  supportingFactIds[]
  supportingEdgeIds[]
  supportingRelationshipIds[]
  supportingRegistrationFactIds[]
  registrationContext
  filePath
  startLine
  endLine
  limitations[]
  gaps[]
```

`bridgeKind` values:

- `interface-member`
- `explicit-interface-member`
- `override-member`
- `type-level-interface`
- `type-level-inheritance`
- `di-registration-context`
- `type-level-interface-only`
- `type-level-inheritance-only`
- `generic-review`

`registrationContext` values:

- `none`
- `registration-context-candidate`
- `registration-observation-only`
- `registration-compatibility-unproven`
- `unsupported-registration-shape`

## Candidate Gap Model

Suggested closed gap kinds:

- `ImplementationCandidateUnavailable`
- `OverrideCandidateUnavailable`
- `MemberCandidateUnavailable`
- `RegistrationCompatibilityUnproven`
- `UnsupportedRegistrationShape`
- `GenericCandidateNeedsReview`
- `AmbiguousImplementationCandidates`
- `DispatchCandidateFanOut`
- `DispatchCandidateSchemaMissing`
- `DispatchCandidateReducedCoverage`
- `DispatchCandidateIdentityUnverified`
- `DispatchCandidateTruncatedByLimit`
- `RuntimeBindingNotProven`
- `DynamicDispatchBoundary`

Consumers may map these to existing gap names if their output vocabulary already
contains equivalent names. They must preserve the original gap kind in
supporting metadata when possible.

Implementation should reuse existing gap codes where the semantics already
match, such as `RuntimeBindingNotProven` for DI registration context that does
not prove runtime binding, rather than creating parallel aliases. New gap kinds
should be added only when the existing vocabulary cannot express the state
without losing evidence or limitation detail.

Gap reconciliation:

| Shared concept | Existing code to reuse when applicable |
| --- | --- |
| DI registration observed but runtime binding not proven | `RuntimeBindingNotProven` |
| Dynamic receiver, proxy, reflection, or similar boundary | `DynamicDispatchBoundary` |
| Registration service/implementation relationship unproven | `RegistrationCompatibilityUnproven` plus `RuntimeBindingNotProven` limitation |
| Factory, scanning, keyed, decorator, or service-locator shape | `UnsupportedRegistrationShape` plus consumer gap rule |
| Candidate cap exceeded | `DispatchCandidateFanOut` |

## Derivation Rules

### Interface Member Calls

1. Start with a source-local call edge whose target is an interface member or
   whose target can be tied to an interface declaration through rule-backed
   relationship evidence.
2. Find `ImplementsInterfaceMember` relationships targeting that interface
   member in the same source index by reading original `relationship_kind`
   metadata, not only normalized graph edge kinds.
3. Create one candidate per implementation member.
4. If the implementation is explicit, use symbol IDs and relationship evidence,
   not display names.
5. Attach DI registration context only when service and implementation symbols
   are compatible through relationship evidence.

Explicit interface implementations: when a type explicitly implements a member
such as `IFoo.Bar` through a private implementation member, relationship
evidence must use the compiler's resolved symbol IDs for both the interface
member and implementing member. Display name matching is insufficient because
names may differ. If semantic analysis does not provide explicit implementation
relationship facts, emit `MemberCandidateUnavailable` and do not create a
candidate from name similarity alone.

### Override Calls

1. Start with a call edge whose target is virtual, abstract, or override-capable
   according to available semantic metadata or relationship evidence.
2. Find direct `Overrides` relationships targeting that member.
3. Walk deeper override chains only with documented max depth, cycle detection,
   and fan-out caps.
4. Emit candidate gaps when missing references, generated code, or generic
   substitutions prevent a credible member candidate.

### Type-Level Fallback

Type-level `ImplementsInterface`, `InheritsFrom`, and `ExtendsInterface`
relationships are useful context but weaker than member-level relationships.
When used to explain a possible bridge, the candidate remains
`WeakerCandidate`, and consumer output must clearly say member-level evidence
was unavailable. Type-level fallback is new behavior for the shared builder and
must not be introduced while claiming byte-for-byte preservation of the
existing paths output; Slice 1 preserves member-level paths behavior first, and
Task 6 owns any additive type-level review context.

### DI Registration Annotation

Supported registration evidence should remain intentionally small:

- generic service/implementation registrations;
- `typeof(service), typeof(implementation)` registrations;
- self registrations with explicit supported semantics;
- instance registrations only when static type evidence is available;
- implementation-only registrations as context, not binding proof.

Unsupported shapes should produce gaps or context only:

- factories and lambdas;
- assembly scanning and convention registration;
- keyed/named services;
- decorators;
- service locator calls;
- reflection-built types;
- config or environment driven registrations;
- order-dependent or branch-dependent registrations;
- custom container APIs without documented matchers;
- open generics that require call-site closure.

## Ordering and Caps

Default implementation constants should be documented in the rule catalog before
shipping. The first shared-builder slice should preserve the live paths fan-out
cap where possible, currently 10 candidates per abstraction boundary, unless
reviewed calibration changes it.

Recommended v1 override traversal depth: 5 levels. This covers typical
framework base to abstract to concrete chains without unbounded inheritance tree
walking. The shipped value must be at least 3, at most 10, and documented in
the rule catalog before override-chain traversal emits product behavior.

Sort candidates by:

1. evidence tier strength;
2. registration-context before relationship-only;
3. member-level before type-level;
4. bridge kind;
5. source label;
6. containing type display name;
7. member display name;
8. file path;
9. start line;
10. end line;
11. stable symbol ID;
12. candidate ID.

Caps to document:

- candidates per abstraction boundary;
- override traversal depth;
- candidate frontier;
- candidate gaps rendered in Markdown;
- candidate rows rendered in Markdown;
- candidate rows preserved in JSON/export.

## Stable IDs

Candidate IDs should use a context-separated SHA-256 over:

- algorithm ID, for example `static-dispatch-candidate-bridges.v1`;
- source index ID and safe source label;
- call edge ID;
- abstraction symbol ID;
- candidate member and type symbol IDs;
- sorted relationship fact IDs;
- sorted registration fact IDs when present;
- bridge kind;
- rule ID.

Do not include timestamps, temp paths, absolute paths, raw remotes, raw URLs,
raw SQL, raw config, source snippets, private labels, nondeterministic row
order, or raw display names that have not passed safety review.

Volatility filtering: before hashing, inspect candidate ID inputs for platform
temp paths, macOS cache paths, Windows temp paths, nondeterministic GUID
fragments, timestamps, and unsorted collection ordering. If detected, emit
`DispatchCandidateIdentityUnverified` and skip candidate creation unless a
deterministic safe subset can be extracted and verified.

## Consumer Integration

### Paths

Paths already has the first implementation slice. Refactoring must preserve
existing behavior and tests before route-flow/reverse/impact consumers change.

### Route-Flow

Route-flow candidate rows should contain:

- route root/selector context;
- interface or override abstraction symbol;
- candidate implementation symbol;
- candidate count and omitted count;
- supporting dispatch candidate ID;
- supporting relationship and call edge IDs;
- optional DI registration context;
- gaps and limitations.

Route-flow candidate bridge row kind:

```text
rowKind: interface-implementation-candidate
ruleId: combined.route-flow.interface-bridge.v1
abstractionSymbolId
candidateSymbolId
candidateCount
omittedCount
supportingDispatchCandidateId
supportingCallEdgeIds[]
supportingRelationshipIds[]
registrationContext
gaps[]
limitations[]
```

The implementation must preserve existing route-flow row kinds such as
`method`, `http-client`, and `sql-query`. The
`interface-implementation-candidate` row is an additive candidate presentation
row over shared dispatch evidence and preserves the existing route-flow
candidate vocabulary.

### Reverse

Reverse traversal should represent candidate edges in the emitted root-to-surface
path order, even when the graph walk was computed backward. Candidate evidence
caps the whole affected path/root at `NeedsReviewReversePath`.

### Impact

Impact reports may include candidate path/reverse context as review evidence.
Candidate context must not be used to convert a changed surface into
`StaticImpactEvidence` or single-index `DefiniteImpact`.

### Report and Portfolio

Reports should summarize:

- candidate edge count by source and bridge kind;
- registration-context candidate count;
- weaker candidate count;
- candidate gap count by kind;
- fan-out/truncation state;
- reduced coverage and source identity caveats.

### Vault and Docs Export/RAG

Candidate edges should be exported as review-context graph edges or chunks with
rule IDs, tiers, supporting IDs, coverage labels, limitations, and safe wording.
They must not be flattened into ordinary call edges because that would erase the
static-candidate boundary.

## Safety

Use existing safe path, metadata hashing, Markdown escaping, and generated-file
guards. Candidate output must never render:

- raw source snippets;
- raw SQL or raw query text;
- raw config values or connection strings;
- raw URLs, hostnames, endpoint secrets, query strings, or fragments;
- raw remotes;
- local absolute paths;
- private labels;
- credentials or secret-looking values.

DI registration context should use closed-set shape names and safe symbol IDs.
Factory bodies, lambdas, config expressions, and reflection arguments are not
rendered by default.

## Validation Strategy

Focused tests should be added near existing path, route-flow, reverse, impact,
report, vault, and docs-export tests rather than creating a parallel harness.

Minimum implementation validation:

```bash
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

For implementation PRs touching language adapters, graph traversal, route-flow,
reverse, impact, export, or public artifacts, also follow `docs/VALIDATION.md`
and run or explicitly defer relevant pinned smoke checks with evidence.

Spec-only validation for this branch:

```bash
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-opus-4.8 --fresh --save-review-text
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-sonnet-4.5 --fresh --save-review-text
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind re-review --model claude-sonnet-4.5 --fresh --save-review-text --timeout-ms 900000
./scripts/check-private-paths.sh
git diff --check
```

Record review artifacts, findings, patches, and validation results in
`implementation-state.md`.
