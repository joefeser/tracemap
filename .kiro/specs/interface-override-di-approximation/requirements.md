# Interface, Override, and DI Approximation Requirements

## Introduction

TraceMap already records static call edges, object creation, symbol
relationships, combined dependency paths, route-flow rows, reverse query rows,
and evidence exports. Real C# applications often call through interfaces,
abstract or virtual members, inherited members, and dependency-injection
registrations. The current graph can stop too early or rely on review-only
relationship rows without a dedicated contract for how those facts should be
extracted, combined, and consumed.

This spec defines a deterministic approximation layer for interface,
override, inheritance, and statically visible dependency-injection evidence. It
does not prove runtime dispatch, runtime dependency-injection bindings,
environment-specific registrations, dynamic proxy targets, branch feasibility,
or production usage. Every edge or gap must carry a rule ID, evidence tier,
source provenance, limitations, and coverage state.

This is a spec-only phase for issue 35. It must not implement scanner,
reducer, reporting, export, or site code on this branch.

## Current State

- C# semantic symbol identity and direct symbol relationships are already
  documented by `csharp.semantic.symbolidentity.v1` and
  `csharp.semantic.symbolrelationship.v1`.
- Statically visible DI registration and dynamic receiver candidate facts are
  already documented by `csharp.semantic.runtimeevidence.v1` as
  `DependencyRegistered` and `DynamicDispatchCandidate`. This spec formalizes
  and extends how that existing evidence is audited, combined, and consumed; it
  does not treat those fact types as greenfield.
- The combined index imports `combined_symbol_relationships`,
  `combined_call_edges`, `combined_object_creations`,
  `combined_argument_flows`, `combined_parameter_forward_edges`, and
  `combined_dependency_edges`.
- `tracemap paths`, `tracemap reverse`, and `tracemap route-flow` already use
  conservative classifications and limitations around dynamic dispatch,
  dependency injection, and runtime behavior.
- `combined.route-flow.interface-bridge.v1` already defines candidate
  interface bridge rows, but scanner/index evidence and shared graph semantics
  are not yet specified enough to support broad implementation work.

## MVP Scope Decisions

- The first implementation slice SHALL focus on C# semantic extraction and
  combined-index consumption.
- Syntax fallback MAY emit weaker relationship candidates only when a rule
  documents the pattern, limitations, and downgrade behavior.
- Dependency-injection evidence SHALL be limited to statically visible
  registration calls using known generic or `typeof(...)` shapes.
- DI evidence SHALL identify registration candidates and registration gaps, not
  selected runtime targets.
- Dispatch expansion SHALL be conservative: possible targets are candidates,
  not proof that the target executes.
- The implementation SHALL integrate with route-flow, paths, reverse, combined
  report, impact/include-paths, and export by reusing shared graph/read models
  where practical.
- The implementation SHALL remain deterministic, bounded, schema-additive, and
  public-artifact safe.

## Requirements

### Requirement 1: Symbol Relationship Fact Contract

**User Story:** As a maintainer, I want inheritance, interface, override, and
member relationship facts to be explicit so that downstream graph consumers do
not guess from names.

#### Acceptance Criteria

1. WHEN C# semantic analysis resolves a type declaration with direct base type
   or interface declarations THEN TraceMap SHALL emit direct relationship
   evidence with rule ID, evidence tier, source symbol ID, target symbol ID,
   relationship kind, file span, extractor name, and extractor version.
2. WHEN C# semantic analysis resolves an override member THEN TraceMap SHALL
   emit an override relationship from overriding member to directly overridden
   member.
3. WHEN C# semantic analysis resolves an interface member implementation THEN
   TraceMap SHALL emit an interface-member-implementation relationship from
   implementation member to interface member.
4. WHEN explicit interface implementation is resolved THEN TraceMap SHALL
   preserve both the implementation member and interface member identities
   without relying on display-name equality.
5. WHEN relationships are transitive, inherited through multiple hops, generic
   substitutions, default-interface-method-related, or partial-type-dependent
   THEN TraceMap SHALL not emit them as direct source facts unless the compiler
   exposes that exact direct relationship; graph consumers MAY traverse direct
   relationships later with a derived rule.
6. WHEN semantic analysis is unavailable or reduced THEN TraceMap SHALL emit an
   `AnalysisGap` for relationship extraction coverage and MAY emit Tier3
   syntax-only relationship candidates only under a documented syntax rule.
7. WHEN syntax-only relationship candidates are emitted THEN they SHALL be
   clearly distinguished from `csharp.semantic.symbolrelationship.v1` and SHALL
   never upgrade dispatch, path, route-flow, reverse, or reducer conclusions
   above review-tier.

### Requirement 2: Statically Visible DI Registration Evidence

**User Story:** As a reviewer, I want TraceMap to record deterministic DI
registration evidence without pretending to know the runtime container state.

#### Acceptance Criteria

1. WHEN C# semantic analysis sees a statically visible registration call using
   known method shapes such as service/implementation generic arguments or
   direct `typeof(...)` service and implementation arguments THEN TraceMap SHALL
   continue to emit or extend the existing `DependencyRegistered` fact contract
   with service symbol, implementation symbol when present, registration method
   family, lifetime when statically visible, registration shape, file span,
   rule ID, evidence tier, extractor name, and extractor version.
2. WHEN a registration maps a service type to itself, an implementation-only
   registration, or an instance registration with static type evidence THEN the
   fact SHALL record the known service/implementation identity and the missing
   side as `null` or a closed-set placeholder such as `self` or `unknown`
   rather than guessing. WHEN an instance registration's static type cannot be
   resolved THEN TraceMap SHALL emit `UnsupportedRegistrationShape` rather than
   a partial instance registration row.
3. WHEN registrations use factories, lambdas, open generics, assembly scanning,
   module methods, extension methods with unresolved symbols, configuration,
   environment-specific branches, reflection, service locators, keyed/named
   services, decorators, conditional registration, or custom container APIs
   outside the supported shape THEN TraceMap SHALL emit a registration gap or
   `csharp.semantic.flowboundary.v1` flow-boundary fact instead of expanding
   runtime targets.
4. WHEN registration evidence is syntax-only or method-name-only THEN it SHALL
   be Tier3 and SHALL not imply a resolved service-to-implementation binding.
5. WHEN a DI registration references generic type parameters or open generic
   type definitions THEN TraceMap SHALL label the registration as open-generic
   review evidence and SHALL not close generic type arguments from call sites in
   v1.
6. WHEN multiple registrations exist for the same service symbol THEN TraceMap
   SHALL preserve all candidates deterministically and mark fan-out or
   ambiguity for graph consumers.
7. WHEN registration evidence is rendered or exported THEN raw config values,
   raw URLs, source snippets, hostnames, raw remotes, local absolute paths, and
   secrets SHALL be omitted or hashed.

### Requirement 3: Dispatch Candidate Graph Edges

**User Story:** As an investigator, I want static graph traversal to continue
through plausible interface and virtual dispatch targets when evidence exists,
while staying honest about uncertainty.

#### Acceptance Criteria

1. WHEN a call edge targets an interface member and relationship evidence links
   one or more implementation members to that interface member THEN graph
   consumers MAY add derived dispatch candidate edges from the call site or
   called interface member to each implementation member.
2. WHEN a call edge targets a virtual or abstract member and override
   relationship evidence links one or more overriding members THEN graph
   consumers MAY add derived override dispatch candidate edges.
3. WHEN a call edge targets a base member and the selected graph context has
   inheritance evidence for derived types THEN graph consumers MAY add
   inherited dispatch candidate edges only under a documented derived rule and
   bounded traversal; the traversal depth and fan-out bounds SHALL be
   deterministic constants documented by that rule or consumer.
4. WHEN candidate edges are emitted THEN each edge SHALL carry a derived rule
   ID, evidence tier, supporting relationship fact IDs, supporting call edge
   IDs, source and target symbol IDs, file spans where available, and a
   limitation that the edge is not runtime dispatch proof.
5. WHEN candidate derivation depends on syntax-only relationships, name-only
   symbol joins, fallback call edges, high fan-out, or ambiguous symbols THEN
   the edge SHALL be review-tier or gap evidence and SHALL not strengthen
   downstream classifications.
6. WHEN no relationship evidence exists for an interface or virtual call THEN
   graph consumers SHALL stop at the unresolved boundary and emit a gap such as
   `ImplementationCandidateUnavailable`, `OverrideCandidateUnavailable`, or
   `DispatchTargetUnavailable`.
7. WHEN dynamic receivers, reflection, delegates, expression trees, runtime
   proxies, generated code, dynamic language features, or missing references
   prevent deterministic candidate expansion THEN TraceMap SHALL emit a
   boundary or analysis-gap fact rather than inventing targets.

### Requirement 4: DI-Aware Candidate Association

**User Story:** As a reviewer, I want DI registration evidence to narrow or
annotate possible candidates without turning static registrations into runtime
certainty.

#### Acceptance Criteria

1. WHEN a service interface call has both implementation relationship evidence
   and statically visible DI registration evidence for the same service symbol
   THEN graph consumers MAY annotate candidate implementation edges with
   registration support.
2. WHEN a registration maps service symbol `S` to implementation symbol `I` and
   relationship evidence proves `I` implements or inherits from `S` THEN the
   candidate edge MAY be labeled `registration-supported-candidate`.
3. WHEN registration evidence exists but relationship evidence does not prove
   service compatibility THEN graph consumers SHALL emit a
   `RegistrationCompatibilityUnproven` gap and SHALL NOT create a strong
   dispatch edge from registration alone.
4. WHEN multiple registrations support the same service or implementation THEN
   graph consumers SHALL keep all candidates, record candidate counts, and cap
   classification at review-tier or weaker according to documented fan-out
   rules.
5. WHEN a registration is environment-specific, branch-dependent,
   order-dependent, keyed/named, factory-based, open-generic, module-scanned,
   decorator-based, or custom-container-specific THEN graph consumers SHALL
   preserve it as context or a gap, not as a selected runtime target.
6. WHEN DI-supported candidate edges appear in route-flow, paths, reverse,
   impact, report, or export output THEN wording SHALL say "candidate" or
   "registration evidence" and SHALL not say "the implementation", "runtime
   target", "will call", "is injected", or equivalent certainty.

### Requirement 5: Combined Index and Schema Contract

**User Story:** As an automation author, I want combined indexes to preserve
relationship and DI evidence so route-flow, paths, reverse, and export can
consume the same facts.

#### Acceptance Criteria

1. WHEN single-language indexes contain relationship facts THEN `tracemap
   combine` SHALL preserve them in combined fact rows and precise relationship
   tables with source index identity, language, rule ID, evidence tier, source
   symbol ID, target symbol ID, relationship kind, file span, and supporting
   fact ID, which implementations MAY supply by joining on
   `relationship_id = fact_id` when the backing fact row is sufficient.
   Extractor metadata MAY be supplied by the same deterministic join;
   implementations SHALL add nullable columns only if that join is
   insufficient.
2. WHEN single-language indexes contain `DependencyRegistered` facts THEN
   `tracemap combine` SHALL preserve service/implementation symbols, lifetime,
   registration shape, evidence tier, rule ID, file span, extractor metadata,
   and safe metadata in combined facts and any additive precise table defined
   by implementation.
3. WHEN schema additions are required THEN they SHALL be additive, versioned,
   tolerant of older indexes, and covered by migration or schema-compatibility
   tests.
4. WHEN precise relationship or registration tables are missing from older
   indexes but source facts exist THEN graph consumers MAY fall back to safe
   fact properties with reduced coverage and SHALL emit a schema gap.
5. WHEN source symbols cross language adapters or source indexes THEN symbol
   identity SHALL stay namespaced by source and language; raw symbol ID equality
   across sources SHALL not imply identity.
6. WHEN combined outputs include candidate or gap rows THEN fact IDs, edge IDs,
   supporting rule IDs, source labels, commit SHAs, and coverage labels SHALL be
   sorted deterministically.

### Requirement 6: Consumer Integration

**User Story:** As a TraceMap user, I want existing query and reporting
commands to consume the new evidence consistently.

#### Acceptance Criteria

1. WHEN `tracemap paths` traverses interface, override, inheritance, or
   DI-supported candidate edges THEN path classifications SHALL be capped at
   `NeedsReviewPath` unless a future deterministic rule explicitly permits a
   stronger classification.
2. WHEN `tracemap route-flow` consumes candidate edges THEN
   `combined.route-flow.interface-bridge.v1` or a successor rule SHALL preserve
   candidate evidence and cap route-flow classification at
   `NeedsReviewStaticRouteFlow` or weaker.
3. WHEN `tracemap reverse` consumes candidate edges THEN reverse
   classifications SHALL be capped at `NeedsReviewReversePath` or weaker and
   SHALL include runtime-dispatch and DI-binding limitations.
4. WHEN `tracemap impact --include-paths` or reducer-adjacent path context uses
   candidate edges THEN it SHALL label the output as static review context and
   SHALL not use candidate edges alone to say a changed contract is definitely
   impacted.
5. WHEN combined report or portfolio report summarizes relationship and DI
   evidence THEN it SHALL report counts, candidate fan-out, gaps, and
   limitations without claiming runtime binding.
6. WHEN evidence graph or vault export renders candidate edges THEN exported
   nodes and edges SHALL include safe labels, rule IDs, tiers, limitations, and
   supporting IDs, and SHALL omit unsafe values.
7. WHEN downstream consumers encounter high fan-out, reduced coverage, missing
   project references, missing extractor versions, unknown commit SHA, or
   schema gaps THEN they SHALL downgrade classifications or emit
   `UnknownAnalysisGap` rather than presenting clean absence or strong
   reachability.

### Requirement 7: Classifications, Coverage, and Gaps

**User Story:** As a reviewer, I want conservative labels so I know when
TraceMap found static evidence versus an analysis boundary.

#### Acceptance Criteria

1. WHEN a candidate edge is derived entirely from Tier1 semantic call and
   relationship evidence THEN the strongest candidate classification SHALL
   still be review-tier unless a future runtime-binding proof rule exists.
2. WHEN DI registration evidence supports a candidate THEN the candidate MAY be
   ordered or annotated ahead of unsupported candidates, but SHALL remain
   review-tier unless future rules prove stronger static binding.
3. WHEN fan-out exceeds the documented threshold for a consumer THEN the
   consumer SHALL emit an ambiguity gap and cap classifications.
4. WHEN a consumer derives candidate edges or inherited dispatch candidates
   THEN fan-out thresholds, traversal depth, and result caps SHALL be
   deterministic constants documented in the rule catalog, consumer design, or
   CLI help before the behavior ships.
5. WHEN semantic project load fails, references are missing, generated source
   is unavailable, relationship extraction is disabled, or registration
   extraction is unavailable THEN TraceMap SHALL emit `AnalysisGap` facts and
   mark coverage reduced.
6. WHEN source identity or commit SHA is missing THEN candidate absence and
   no-path conclusions SHALL become `UnknownAnalysisGap`.
7. WHEN candidates are unavailable under full coverage THEN output MAY use the
   canonical clean static absence label `NoDispatchCandidateEvidence`, but
   SHALL still avoid runtime absence wording.
8. WHEN candidates are unavailable under reduced coverage THEN output SHALL use
   `UnknownAnalysisGap`, not clean absence.
9. WHEN a consumer decides whether full dispatch-candidate coverage exists THEN
   full coverage SHALL require a known commit SHA, verified source identity,
   `Level1SemanticAnalysis` or equivalent full semantic relationship coverage
   for contributing C# sources, no relationship-extractor or registration
   extractor availability gaps for those sources, and compatible schema for the
   required relationship/call/registration evidence.

### Requirement 8: Public Artifact Safety and Wording

**User Story:** As a maintainer, I want generated outputs to be safe to share
and hard to overread.

#### Acceptance Criteria

1. WHEN Markdown, JSON, NDJSON, SQLite export, logs, or review artifacts include
   relationship, dispatch, or registration evidence THEN they SHALL include
   rule IDs, evidence tiers, file paths, line spans, commit SHA, extractor
   versions, and limitations where available.
2. WHEN output contains candidate edges THEN wording SHALL use "candidate",
   "possible static target", "registration evidence", "relationship evidence",
   or "analysis gap" and SHALL not imply runtime certainty.
3. WHEN raw source snippets, raw SQL, config values, connection strings, URLs,
   hostnames, private labels, local absolute paths, raw remotes, or secrets are
   present in source fact properties THEN generated artifacts SHALL omit or hash
   them.
4. WHEN diagnostic messages mention unsupported patterns THEN they SHALL use
   closed-set reason codes or safe summaries, not raw expressions or values.
5. WHEN rule limitations are rendered THEN they SHALL explicitly state that
   relationship and DI evidence do not prove runtime dispatch, container
   selection, branch feasibility, proxy expansion, generated-code freshness, or
   production usage.

### Requirement 9: Tests and Validation

**User Story:** As a maintainer, I want focused tests proving this approximation
stays deterministic, useful, and bounded.

#### Acceptance Criteria

1. WHEN implementation lands THEN tests SHALL cover direct interface
   implementation, explicit interface implementation, interface inheritance,
   class inheritance, abstract overrides, virtual overrides, generic
   interfaces, default interface methods, partial types, and missing references.
2. WHEN DI registration extraction lands THEN tests SHALL cover generic
   service/implementation registrations, `typeof(...)` registrations,
   implementation-only registrations, instance registrations, multiple
   registrations, open generics, opaque instance registrations with
   unresolvable static type, factories, scanning, conditional branches, and
   unsupported custom containers.
3. WHEN candidate graph derivation lands THEN tests SHALL cover interface call
   expansion, override expansion, unavailable candidates, ambiguous symbols,
   high fan-out, reduced coverage, and deterministic ordering.
4. WHEN DI-aware candidate association lands THEN tests SHALL cover
   `registration-supported-candidate` labeling, ordering registration-supported
   candidates before relationship-only candidates, and
   `RegistrationCompatibilityUnproven` gaps when registration evidence exists
   without relationship compatibility proof.
5. WHEN consumers are updated THEN tests SHALL prove paths, route-flow,
   reverse, impact/include-paths, combined report, and export preserve
   candidate limitations and do not strengthen classifications incorrectly.
6. WHEN consumers render DI-supported candidate edges THEN tests SHALL assert
   forbidden wording is absent, including runtime-target, will-call,
   is-injected, container-resolves, and definite-impact claims from candidates
   alone.
7. WHEN schema additions preserve supporting fact IDs for relationship or
   registration rows THEN tests SHALL cover old schema fallback, additive
   column presence, and deterministic supporting ID ordering.
8. WHEN scanner-level `DynamicDispatchCandidate` facts and derived combined
   dispatch candidate edges both exist THEN tests SHALL prove they are not
   conflated in facts, graph edges, report rows, or exports.
9. WHEN relationship extraction lands THEN tests SHALL prove emitted
   relationship kinds stay within the documented closed set, including the
   interface-extends-interface case.
10. WHEN DI registration extraction lands THEN tests SHALL prove
   `DependencyRegistered` rows and combined registration evidence are sorted
   deterministically and byte-stable.
11. WHEN unsafe source metadata appears THEN tests SHALL prove Markdown, JSON,
   logs, and exports omit or hash unsafe values.
12. WHEN outputs are generated twice from identical inputs THEN tests SHALL
   prove byte-stable facts, SQLite rows, reports, and exports.
13. WHEN validation runs locally THEN `dotnet test`, a CLI scan against at least
   one public-safe sample, relevant pinned smoke checks from `docs/VALIDATION.md`,
   `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass or be
   explicitly deferred with rationale.

## Non-Goals

- Runtime container execution.
- Environment-specific registration certainty.
- Dynamic proxy expansion in v1.
- Runtime route, request, database, queue, or service execution proof.
- Branch feasibility, auth, middleware, deployment, serializer runtime mapping,
  or production usage inference.
- LLM calls, embeddings, vector databases, or prompt-based classification.
- Source-snippet storage by default.
- Cross-language runtime binding or source-identity merging by name.
