# Interface, Override, and DI Approximation Design

## Overview

Add a deterministic approximation layer for C# interface, override,
inheritance, and statically visible DI registration evidence. The work should
strengthen TraceMap's existing static graph without changing its evidence
contract: candidates are evidence-backed possibilities, not runtime binding
truth.

The intended evidence flow is:

```text
C# semantic symbols
  -> direct inheritance/interface/override/member-implementation relationships
  -> statically visible DI registration facts where supported
  -> combined index import
  -> derived candidate dispatch edges with gaps and fan-out limits
  -> paths / route-flow / reverse / report / export consumers
```

This spec is intentionally an approximation. It helps users see where static
evidence can continue through abstraction boundaries, and where TraceMap stops
because runtime information would be required.

## Goals

- Preserve direct compiler-backed relationship evidence for inheritance,
  interface implementation, interface member implementation, and overrides.
- Add or formalize deterministic DI registration facts for supported static
  registration shapes.
- Derive possible dispatch/target edges only from documented evidence.
- Keep candidate edges review-tier and coverage-relative.
- Reuse combined graph, path, route-flow, reverse, report, impact, and export
  infrastructure rather than creating a parallel graph model.
- Emit explicit gaps for unavailable relationship evidence, ambiguous symbols,
  high fan-out, dynamic dispatch, DI uncertainty, and unsupported registration
  patterns.
- Keep JSON, NDJSON, SQLite, Markdown, logs, and exports deterministic and safe.

## Non-Goals

- No runtime DI container execution.
- No runtime target selection or runtime reachability proof.
- No environment, profile, branch, registration-order, decorator, keyed-service,
  or custom-container certainty.
- No dynamic proxy expansion in v1.
- No reflection, delegate, expression-tree, serializer, branch-feasibility, or
  generated-code execution modeling.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic ranking.
- No raw source snippets, raw SQL, raw config values, URLs, hostnames, raw
  remotes, local absolute paths, private labels, or secrets in default
  artifacts.

## Proposed Implementation Slices

### Slice 1: Audit and Fixture Lock

- Inventory current C# relationship extraction, SQLite rows, combine import,
  export, paths, reverse, route-flow, and reducer consumption.
- Add public-safe fixture coverage for the current behavior before changing
  shared graph readers.
- Confirm existing rule catalog entries and identify only additive successor
  rules needed for missing gaps or DI registration shapes.

### Slice 2: Relationship Evidence Hardening

- Ensure direct type and member relationships carry stable source/target symbol
  IDs, relationship kind, evidence tier, rule ID, file span, extractor identity,
  and supporting fact IDs.
- Add syntax-only fallback only if the implementation defines a separate rule
  with Tier3 limitations.
- Verify explicit interface implementation, generic interfaces, default
  interface methods, partial types, and missing references receive either
  compiler-backed evidence or explicit gaps.

### Slice 3: DI Registration Evidence

- Extract statically visible registration calls from semantic C# evidence:
  generic service/implementation, `typeof(...)` service/implementation, self
  registration, implementation-only registration, and instance registration
  where static type evidence exists. Start by auditing and extending the
  existing `DependencyRegistered` emission under
  `csharp.semantic.runtimeevidence.v1`.
- Record unsupported shapes as gaps or flow boundaries: factories, lambdas,
  scanning, open generics, conditional branches, custom containers, keyed/named
  services, decorators, module loading, configuration, reflection, and service
  locator patterns.
- Use safe metadata only. Store expression hashes or closed-set shape names
  instead of source snippets or raw values.

### Slice 4: Combined Graph Candidate Edges

- Add a shared candidate edge builder in the reporting/query layer, probably
  near existing combined path graph inventory.
- Inputs:
  - `combined_call_edges`
  - `combined_symbol_relationships`
  - `combined_facts` for `DependencyRegistered`
  - `combined_fact_symbols` when needed for method/type attachment
  - source coverage and identity metadata from `index_sources`
- Outputs:
  - derived candidate edges in memory
  - candidate/gap rows in reports
  - no persisted derived rows unless a future spec explicitly asks for them

### Slice 5: Consumer Integration

- `tracemap paths`: traverse candidate edges with `NeedsReviewPath` cap and
  candidate limitations.
- `tracemap route-flow`: reuse `combined.route-flow.interface-bridge.v1` or a
  successor for candidate bridge rows.
- `tracemap reverse`: allow reverse traversal over candidate edges with
  `NeedsReviewReversePath` cap.
- `tracemap impact --include-paths`: include candidate path context without
  converting candidates into definite impact.
- `tracemap report` and portfolio reports: summarize relationship and DI
  evidence counts, candidate fan-out, gaps, and limitations.
- `tracemap export`, evidence graph, and vault export: expose safe candidate
  evidence and gaps with rule IDs and tiers.

If `combined.route-flow.interface-bridge.v1` is replaced by a successor, the
implementation PR must define fallback or migration behavior so older combined
indexes and exports carrying the old rule ID remain readable with a schema-gap
or compatibility label.

## Rule and Fact Model

Existing rules to preserve:

| Rule ID | Role |
| --- | --- |
| `csharp.semantic.symbolrelationship.v1` | Direct compiler-resolved relationship facts. |
| `csharp.semantic.runtimeevidence.v1` | Current home for statically visible DI registration evidence. |
| `csharp.semantic.flowboundary.v1` | Runtime-sensitive boundaries and unsupported shapes. |
| `combined.route-flow.interface-bridge.v1` | Route-flow bridge rows for implementation candidates. |
| `combined.route-flow.gap.v1` | Route-flow gaps and runtime-binding uncertainty. |

Likely successor or additive rules:

| Proposed Rule ID | Purpose |
| --- | --- |
| `csharp.syntax.symbolrelationship-candidate.v1` | Optional Tier3 fallback for syntax-only inheritance/interface/override candidates. |
| `csharp.semantic.di-registration.v1` | Only-if-needed split from existing runtime evidence if DI registration scope needs narrower limitations. |
| `combined.dispatch-candidate.v1` | Shared derived reporting/query-layer candidate edges over call and relationship evidence. |
| `combined.dispatch-gap.v1` | Shared graph gaps for missing/ambiguous dispatch and registration evidence. |

The implementation should not add new rules if existing rule IDs can be
documented precisely enough. If a rule changes behavior, update the rule catalog
and tests in the implementation branch.

The proposed rule IDs above are tentative. They become normative only when
`rules/rule-catalog.yml` entries are added in Phase 7. Product code must not
emit a proposed rule ID before its catalog entry exists.

`DynamicDispatchCandidate` and `combined.dispatch-candidate.v1` must remain
distinct concepts. The scanner-level `DynamicDispatchCandidate` fact from
`csharp.semantic.runtimeevidence.v1` records dynamic receiver evidence at a
source location. The proposed combined dispatch candidate rule would be a
derived reporting/query edge over call-edge plus relationship or DI evidence.
Consumers must not merge the two just because the names both contain
"dispatch candidate."

## Relationship Kinds

Use the existing relationship-kind values as the compatibility baseline before
any new normalization is considered:

| Kind | Meaning |
| --- | --- |
| `InheritsFrom` | Type directly inherits a base class. |
| `ImplementsInterface` | Type directly implements an interface. |
| `ExtendsInterface` | Interface directly extends another interface. |
| `Overrides` | Member directly overrides another member. |
| `ImplementsInterfaceMember` | Member implements an interface member. |

Member hiding via `new` is deferred. If a future implementation models it, it
must use review-tier evidence and must not be treated as override dispatch.

If a future implementation introduces lower-case normalized values such as
`inherits`, it must define a compatibility mapping from the live values above,
update all consumers atomically, and add migration, export, reducer, route-flow,
paths, reverse, and legacy-flow compatibility tests.

## DI Registration Shapes

Supported v1 shapes should be intentionally small:

| Shape | Example Pattern | Evidence |
| --- | --- | --- |
| `generic-service-implementation` | `AddScoped<TService, TImplementation>()` | Service and implementation symbols. |
| `generic-self` | `AddSingleton<TService>()` | Service symbol and implementation unknown/self depending on API semantics. |
| `typeof-service-implementation` | `AddTransient(typeof(TService), typeof(TImplementation))` | Service and implementation symbols. |
| `typeof-self` | `AddSingleton(typeof(TService))` | Service symbol and implementation unknown/self depending on API semantics. |
| `instance` | Registration with statically typed instance argument | Service/implementation type when semantic type is available. |

Unsupported or gap shapes:

- factory lambdas or delegates
- assembly scanning and convention registration
- open generics beyond recording open-generic metadata
- custom container APIs not recognized by a documented matcher
- conditional branches or environment/profile-specific paths
- keyed/named services and decorators
- service locator resolution calls
- reflection-built service or implementation types

Unsupported shapes should still be useful review context via flow-boundary or
gap facts, but they must not create selected runtime target edges.

## Candidate Edge Derivation

Candidate edge derivation is a reporting/query-layer operation over already
indexed evidence.

### Interface Member Call

1. Identify a call edge whose target symbol is an interface member, or whose
   target member can be tied to an interface declaration through relationship
   evidence.
2. Find source-local `ImplementsInterfaceMember` relationships targeting that
   interface member.
3. Create one candidate edge per implementation member.
4. Attach registration support if DI evidence maps the interface/type service
   to the implementation/type and relationship evidence proves compatibility.
5. Sort candidates by:
   - stronger evidence tier;
   - registration-supported before relationship-only;
   - source label;
   - containing type display name;
   - member display name;
   - file path;
   - line span;
   - stable symbol ID.

Fan-out thresholds, inherited-dispatch traversal depth, candidate result caps,
and ambiguity thresholds must be deterministic constants documented by the
consumer rule before implementation. A consumer may choose stricter caps than
another consumer, but it must emit the cap and truncation or ambiguity state in
output. A starting review threshold in the 10 to 20 candidate range is a
reasonable first implementation anchor, subject to rule-catalog documentation
and fixture calibration before shipping.

### Virtual or Abstract Member Call

1. Identify a call edge whose target is virtual, abstract, or override-capable
   when semantic metadata exposes that state.
2. Find direct `Overrides` relationships targeting that member.
3. Optionally traverse further override chains with a bounded graph traversal
   and derived rule.
4. Emit review-tier candidate edges with override limitations.

### Base Type Relationship

Type-level `inherits` and `implements` relationships can support candidate
search, but they should not substitute for member-level evidence when the
member relationship is available. Type-level bridging without member evidence
is weaker and should be capped or emitted as a gap.

## Classification Policy

Candidate edges do not become strong runtime facts. Suggested caps:

| Evidence Shape | Strongest Classification |
| --- | --- |
| Tier1 call + Tier1 relationship, no DI evidence | `NeedsReview*` |
| Tier1 call + Tier1 relationship + static DI registration support | `NeedsReview*` |
| Tier1 relationship but no call edge | Context only or gap |
| Tier3 syntax or name-only relationship | `NeedsReview*` or `UnknownAnalysisGap` |
| Multiple candidates or high fan-out | `NeedsReview*` with ambiguity gap |
| Reduced coverage, unknown commit, missing schema | `UnknownAnalysisGap` for absence/no-path conclusions |

The `*` suffix maps to the consumer's vocabulary:
`NeedsReviewPath`, `NeedsReviewStaticRouteFlow`,
`NeedsReviewReversePath`, or equivalent report/export classification.

Full coverage for `NoDispatchCandidateEvidence` requires a known commit SHA,
verified source identity, full semantic relationship coverage for contributing
C# sources, no relationship-extractor or registration-extractor availability
gaps for those sources, and compatible schema for the relationship, call, and
registration evidence required by the consumer.

## Gaps

Use existing gap vocabularies when possible. Add successor gap names only when a
consumer needs a stable closed-set value:

| Gap | When |
| --- | --- |
| `ImplementationCandidateUnavailable` | Interface relationship evidence is unavailable. |
| `OverrideCandidateUnavailable` | Override relationship evidence is unavailable. |
| `DispatchTargetUnavailable` | Dynamic or unresolved call target cannot be expanded. |
| `RuntimeBindingNotProven` | A candidate exists but runtime dispatch/DI cannot be proven. |
| `AmbiguousImplementationCandidates` | Multiple possible implementation candidates exist. |
| `RegistrationCompatibilityUnproven` | DI registration and relationship evidence do not prove compatible symbols. |
| `UnsupportedRegistrationShape` | Registration-like syntax uses a shape outside v1. |
| `RelationshipExtractorUnavailable` | Semantic relationship extraction did not run or schema is missing. |
| `RegistrationExtractorUnavailable` | DI registration extraction did not run or schema is missing. |
| `NoDispatchCandidateEvidence` | Full coverage exists and no static candidate evidence was found. |

Every gap must include a rule ID, evidence tier, source identity where known,
file span where applicable, supporting IDs, and a limitation.

## Combined Schema Direction

Prefer using current tables before adding schema:

- `facts` / `combined_facts` remain the canonical fact store.
- `symbol_relationships` / `combined_symbol_relationships` remain the precise
  relationship store.
- `call_edges` / `combined_call_edges` remain the call evidence store.
- `fact_symbols` / `combined_fact_symbols` can attach registration or boundary
  facts to service/implementation symbols.

For relationship rows, extractor metadata, rule IDs, evidence tiers, file
spans, and supporting fact IDs may be satisfied by joining the relationship row
to its backing `SymbolRelationship` fact. In the current schema,
`relationship_id` is the fact ID; that is acceptable if tests prove the join is
stable in single-language and combined indexes. Add nullable relationship-table
columns only if the backing fact join is insufficient.

If DI needs a precise table, define it additively with stable nullable columns:

```text
dependency_registrations
  registration_id
  fact_id
  service_symbol_id
  implementation_symbol_id
  lifetime
  registration_shape
  registration_family
  evidence_tier
  rule_id
  extractor_name
  extractor_version
  file_path
  start_line
  end_line
```

Combined indexes would mirror this as `combined_dependency_registrations` with
the same columns plus `source_index_id`, `source_label`, and `language`.
Extractor metadata MAY also be supplied by joining
`combined_dependency_registrations.fact_id` to `combined_facts` when that join
is sufficient, consistent with the relationship-row strategy above. Older
schemas must remain readable through fact fallback and schema gaps.

If relationship rows do not currently expose a supporting fact ID in every
needed table, adding that ID is an additive schema change. The implementation
must keep older indexes readable and preserve source facts as fallback
provenance.

## Output Safety

Generated artifacts must use safe metadata:

- repo-relative file paths only;
- no source snippets;
- no raw SQL or config values;
- no URLs, hostnames, connection strings, raw remotes, or local absolute paths;
- source labels only when already accepted by existing safe-label rules;
- expression hashes and closed-set shape names instead of raw expressions.

Forbidden wording in generated reports:

- "runtime target"
- "will call"
- "is injected"
- "container resolves"
- "definitely impacted" from candidate edges alone
- "no implementation exists" under reduced coverage

Preferred wording:

- "candidate static target"
- "registration evidence"
- "relationship evidence"
- "runtime binding not proven"
- "coverage-relative gap"

## Test Strategy

Add tests in implementation slices, not this spec branch:

- C# semantic fixture tests for direct and explicit interface implementation,
  inheritance, overrides, generics, default interface methods, and partial
  types.
- C# DI fixture tests for supported registration shapes and unsupported gaps.
- SQLite writer and exporter tests for relationship and registration rows.
- Combine tests proving source namespacing and deterministic ordering.
- Paths, route-flow, reverse, impact/include-paths, combined report, and export
  tests proving candidate classifications stay review-tier.
- DI-aware candidate tests for registration-supported ordering,
  `RegistrationCompatibilityUnproven`, and forbidden runtime-certainty wording.
- Tests proving scanner-level `DynamicDispatchCandidate` facts are not confused
  with derived combined candidate edges.
- Safety tests for Markdown, JSON, logs, exports, and redaction.
- Byte-stability tests for facts, SQLite query order, and generated reports.

## Validation

Future implementation PRs should run:

```bash
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If shared graph, language adapter, report, or export behavior changes, follow
the relevant pinned smoke checks in `docs/VALIDATION.md`. A CLI scan against a
public-safe sample should be included once scanner behavior changes land.
