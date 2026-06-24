# Static Dispatch Candidate Bridges Requirements

## Introduction

TraceMap now has a narrow `tracemap paths` slice that can derive conservative
interface and override candidates from existing combined call edges and symbol
relationship evidence. The next practical slice is to turn that into a shared
static candidate bridge contract that route-flow, reverse, impact, combined
report, vault export, and docs/RAG-oriented exports can consume consistently.

This is static analysis only. Candidate bridges are evidence-backed possible
static continuations through interface, override, inheritance, or
statically-visible DI registration evidence. They do not prove runtime dispatch,
runtime dependency-injection binding, selected implementation, service
activation, branch feasibility, production traffic, user reachability, or
business impact.

This is a spec-only phase. It must not implement scanner, reducer, reporting,
export, site, vault, docs-export, or product code on this branch.

## Current State

- `.kiro/specs/interface-override-di-approximation/` defined the broad
  interface, override, inheritance, and DI approximation direction.
- PR #271 implemented only a first `tracemap paths` slice: in-memory candidate
  edges derived from method-level `ImplementsInterfaceMember` and `Overrides`
  relationship evidence, with `NeedsReviewPath` caps and fan-out gaps.
- The broad spec still contains unchecked follow-up work for DI registration
  support, route-flow, reverse, impact, report/portfolio, export/vault, and
  broader scanner hardening.
- Route-flow specs already expect interface bridge rows and candidate wording,
  but route-flow-specific bridge behavior should not drift from the shared path
  graph behavior.
- Reverse, impact, vault export, and docs-export specs already require
  conservative static evidence, provenance, gaps, deterministic caps, and
  public-safe output.

## Scope Decisions

- Reuse current combined candidate rules where practical:
  `combined.dispatch-candidate.v1` for derived candidate edges and
  `combined.dispatch-gap.v1` for candidate derivation gaps.
- Reuse consumer-specific rule IDs for presentation rows, for example
  `combined.route-flow.interface-bridge.v1`, `combined.reverse.path.v1`,
  `combined.impact.*`, `vault-export.*`, and `docs-export.*`, while preserving
  the underlying dispatch candidate rule ID in supporting evidence.
- Do not add a second graph traversal engine. Extract or share the existing
  combined evidence graph/candidate builder where live code supports it.
- Candidate derivation remains in-memory by default. Persisted candidate rows
  are out of scope unless a future schema spec defines lifecycle, migration,
  and compatibility behavior.
- DI registration evidence may annotate or narrow explanations only when
  relationship compatibility is already supported by static evidence. DI
  registration evidence alone must not create a runtime binding conclusion.
- Type-level bridging is weaker than member-level bridging and must be capped
  at review-tier unless a later implementation proves a narrower rule.

## Requirements

### Requirement 1: Candidate State and Consumer Classification Vocabulary

**User Story:** As a reviewer, I want candidate bridge language that separates
static possibilities from runtime certainty.

#### Acceptance Criteria

1. WHEN a derived edge is created from call-edge plus relationship or DI
   evidence THEN TraceMap SHALL assign internal derivation state
   `SymbolBackedCandidate` when it is fully symbol-backed but still not
   runtime-proven.
2. WHEN a relationship-backed candidate depends on syntax-only, Tier3,
   name-only, fallback, ambiguous, high-fan-out, generic, type-level-only,
   reduced-coverage, or DI registration context THEN TraceMap SHALL assign
   internal derivation state `WeakerCandidate`.
3. WHEN candidate derivation cannot be completed because evidence is missing,
   unsupported, schema-incompatible, truncated, or reduced THEN TraceMap SHALL
   assign internal derivation state `CandidateGap` and SHALL emit
   `AnalysisGap` or a narrower closed-set candidate gap.
4. WHEN consumer classifications are emitted THEN both
   `SymbolBackedCandidate` and `WeakerCandidate` SHALL cap route-flow at
   `NeedsReviewStaticRouteFlow`, paths at `NeedsReviewPath`, reverse at
   `NeedsReviewReversePath`, and combined impact at `NeedsReviewImpact` unless
   the consumer has a weaker unknown/gap classification.
5. WHEN wording is rendered in Markdown, JSON, logs, vault notes, docs-export
   chunks, or RAG-oriented artifacts THEN it SHALL use "candidate",
   "static candidate", "registration evidence", "possible static target", or
   equivalent uncertainty wording.
6. WHEN wording is rendered THEN it SHALL NOT say "runtime target", "selected
   implementation", "will call", "is injected", "actual binding",
   "resolved at runtime", "proves impact", or equivalent certainty for these
   edges.
7. WHEN the existing paths implementation emits a note code named
   `StaticDispatchCandidate` THEN implementations SHALL treat it as a
   consumer-specific note for candidate path context, not as a shared emitted
   classification that strengthens any consumer cap.

### Requirement 2: Rule IDs and Limitations

**User Story:** As a maintainer, I want every candidate bridge and gap to cite
documented rules and limitations.

#### Acceptance Criteria

1. WHEN a shared derived candidate edge is emitted or exposed by a consumer THEN
   it SHALL cite `combined.dispatch-candidate.v1` as the underlying rule unless
   a documented successor rule is added before implementation.
2. WHEN shared candidate derivation emits a missing, unsupported, ambiguous,
   fan-out, schema, coverage, registration-compatibility,
   unsupported-registration-shape, generic-candidate,
   member-candidate-unavailable, implementation-candidate-unavailable,
   override-candidate-unavailable, identity-unverified, or truncated-by-limit
   gap THEN it SHALL cite `combined.dispatch-gap.v1` only after
   `rules/rule-catalog.yml` documents the complete closed gap vocabulary, or
   SHALL cite a documented successor rule added before implementation emits
   the gap.
3. WHEN route-flow renders a candidate bridge THEN it SHALL keep
   `combined.route-flow.interface-bridge.v1` or a documented successor as the
   route-flow row rule and include `combined.dispatch-candidate.v1` in
   supporting rule IDs.
4. WHEN reverse, impact, report, vault, or docs-export render candidate context
   THEN they SHALL use their own existing presentation rule IDs while
   preserving the dispatch candidate/gap rule IDs in supporting evidence.
5. WHEN vault export or docs-export/RAG consumers emit candidate edges, gaps,
   or limitations THEN they SHALL use existing `vault-export.*` or
   `docs-export.*` rule IDs if available, or SHALL add new rule IDs to
   `rules/rule-catalog.yml` before product code emits them.
6. WHEN any rule behavior or limitation changes THEN `rules/rule-catalog.yml`
   SHALL be updated before product code emits the changed behavior.
7. WHEN a limitation is emitted THEN it SHALL explicitly state that the edge is
   static candidate evidence and not runtime dispatch or dependency-injection
   binding proof.

### Requirement 3: Evidence Inputs

**User Story:** As an implementation author, I want candidate derivation to use
only deterministic, auditable input evidence.

#### Acceptance Criteria

1. WHEN candidate derivation runs THEN it SHALL use existing combined evidence:
   `combined_call_edges`, `combined_symbol_relationships`, `combined_symbols`,
   `combined_fact_symbols`, `combined_facts`, source coverage metadata, and
   existing DI registration facts where available.
2. WHEN a call edge targets an interface member and
   `ImplementsInterfaceMember` relationships target the same interface member
   THEN TraceMap MAY derive one candidate edge per implementation member.
3. WHEN a call edge targets a virtual, abstract, or override-capable member and
   `Overrides` relationships target that member THEN TraceMap MAY derive one
   candidate edge per overriding member.
4. WHEN explicit interface implementation is present THEN candidate derivation
   SHALL use resolved implementation and interface member symbol IDs rather
   than display-name equality.
5. WHEN type-level `ImplementsInterface`, `InheritsFrom`, or
   `ExtendsInterface` relationships are used because member-level evidence is
   unavailable THEN the candidate SHALL be `WeakerCandidate` and SHALL carry a
   type-level-bridge-only limitation.
6. WHEN a call edge targets an interface or virtual member and only type-level
   `ImplementsInterface`, `InheritsFrom`, or `ExtendsInterface`
   relationships are available without matching member-level
   `ImplementsInterfaceMember` or `Overrides` evidence THEN TraceMap MAY
   derive type-level candidates only as review-tier context or SHALL emit
   `MemberCandidateUnavailable`.
7. WHEN shared candidate derivation distinguishes member-level and type-level
   relationships THEN it SHALL read the original `relationship_kind` from
   `combined_symbol_relationships` or equivalent relationship metadata, not
   only normalized graph edge kinds such as `implements` or `inherits`.
8. WHEN source-local symbol identity cannot be verified THEN candidate
   derivation SHALL emit a gap rather than joining by short name, file
   proximity, namespace text, or display string alone.
9. WHEN semantic coverage is reduced, source identity is unverified, commit SHA
   is unknown, optional candidate tables are missing, or supporting facts lack
   extractor identity THEN candidates SHALL be review-tier or gap evidence.
10. WHEN a source is TypeScript, JVM, Python, or another adapter without
    C#-style member relationship or DI registration facts THEN candidate
    derivation SHALL emit unavailable/reduced-coverage gaps or weaker context
    rather than assuming C# semantics.

### Requirement 4: DI Registration Support

**User Story:** As a reviewer, I want DI evidence to explain static candidates
without pretending to know the runtime container.

#### Acceptance Criteria

1. WHEN a service symbol call has relationship-backed implementation
   candidates and statically visible DI registration evidence for the same
   service symbol THEN TraceMap MAY annotate matching candidates with
   `registration-context-candidate`.
2. WHEN DI registration maps service symbol `S` to implementation symbol `I`
   and relationship evidence proves `I` implements, inherits from, or
   explicitly implements `S` as required by the call member THEN the candidate
   MAY sort before relationship-only candidates.
3. WHEN DI registration evidence exists but relationship compatibility is not
   proven THEN TraceMap SHALL emit `RegistrationCompatibilityUnproven` and
   SHALL NOT create a candidate edge from registration alone.
4. WHEN registrations use factories, lambdas, scanning, module conventions,
   custom containers, keyed/named services, decorators, service locators,
   reflection, configuration, environment-specific branches, registration
   order, or conditional branches THEN TraceMap SHALL emit registration context
   or gaps, not selected runtime targets.
5. WHEN registrations are open-generic, partially closed, generic-parameter
   based, or require call-site type argument closure THEN TraceMap SHALL label
   them as generic review context in v1 and SHALL NOT close generic arguments
   from call sites.
6. WHEN multiple registrations support the same service, implementation, or
   open-generic family THEN TraceMap SHALL preserve all deterministic
   candidates up to caps and emit ambiguity/fan-out metadata.
7. WHEN registration evidence is syntax-only or method-name-only THEN it SHALL
   be Tier3 context and SHALL NOT strengthen the candidate classification.

### Requirement 5: Generics, Open Generics, and Override Caveats

**User Story:** As a maintainer, I want generic and override cases bounded so
candidate derivation remains deterministic.

#### Acceptance Criteria

1. WHEN a candidate relationship involves closed generic symbols with compiler
   identity available THEN TraceMap MAY include the closed symbol IDs in stable
   IDs and supporting metadata.
2. WHEN a candidate relationship involves open generic types or methods THEN
   TraceMap SHALL preserve the open-generic identity as review context and
   SHALL NOT infer a closed runtime target.
3. WHEN variance, constraints, default interface methods, explicit interface
   implementations, partial types, inherited interface members, or generic
   substitutions prevent a direct member candidate THEN TraceMap SHALL emit
   `GenericCandidateNeedsReview`, `MemberCandidateUnavailable`, or another
   documented gap rather than guessing.
4. WHEN override chains extend beyond one level THEN traversal SHALL use a
   deterministic max override depth and SHALL emit `TruncatedByLimit` or a
   candidate gap when the bound is reached.
5. WHEN v1 implements override-chain traversal THEN the max override chain
   depth SHALL be at least 3 and at most 10; the exact value SHALL be
   documented in `rules/rule-catalog.yml` under
   `combined.dispatch-candidate.v1` limitations before implementation emits
   that traversal.
6. WHEN member hiding via `new`, extension methods, dynamic receivers,
   delegates, expression trees, reflection, runtime proxies, generated code,
   or missing references affect dispatch THEN TraceMap SHALL emit boundary or
   gap evidence and SHALL NOT create a stronger candidate.

### Requirement 6: Deterministic Ordering, Caps, and Stable IDs

**User Story:** As an automation author, I want candidate output to be stable
across repeated runs.

#### Acceptance Criteria

1. WHEN candidates are derived THEN stable IDs SHALL be context-separated and
   include safe source identity, call edge ID, target abstraction symbol ID,
   safe source label, source index ID, call edge ID, target abstraction symbol
   ID, candidate member symbol ID, candidate type symbol ID, sorted
   relationship fact IDs, sorted DI registration fact IDs when present, bridge
   kind, rule ID, and a versioned algorithm ID.
2. WHEN stable IDs cannot be constructed without unsafe or volatile values THEN
   TraceMap SHALL hash only verified safe components and assign the row
   `WeakerCandidate` or emit an identity gap.
3. WHEN a symbol ID, fact ID, edge ID, relationship ID, or registration fact ID
   contains volatile values such as temp paths, nondeterministic ordering,
   timestamps, platform-specific identifiers, or raw absolute paths THEN
   TraceMap SHALL emit `DispatchCandidateIdentityUnverified` and SHALL NOT
   create a stable candidate ID from that evidence unless a deterministic safe
   subset can be extracted and verified.
4. WHEN candidates are sorted THEN ordering SHALL be deterministic:
   evidence-tier strength, registration-context before relationship-only,
   member-level before type-level, bridge kind, source label, containing type
   display name, member display name, file path, start line, end line, stable
   symbol ID, and candidate ID.
5. WHEN multiple candidates exist THEN TraceMap SHALL preserve the candidate
   count, omitted count, cap value, and cap reason in JSON and Markdown summary
   metadata where the consumer exposes candidate rows.
6. WHEN a fan-out cap is reached THEN TraceMap SHALL emit a
   `DispatchCandidateFanOut` or consumer-specific truncation gap and SHALL mark
   the affected traversal/report partial.
7. WHEN inherited or override traversal is recursive THEN TraceMap SHALL use
   deterministic cycle detection and depth/frontier caps.
8. WHEN output is repeated over identical inputs and options THEN Markdown,
   JSON, vault graph, docs-export chunks, and generated manifests SHALL be
   byte-stable where those commands already promise byte stability.

### Requirement 7: Conservative Consumer Semantics

**User Story:** As a TraceMap user, I want every command to consume candidate
edges consistently and conservatively.

#### Acceptance Criteria

1. WHEN `tracemap paths` traverses candidate edges THEN the existing
   `NeedsReviewPath` cap SHALL remain and candidate limitations SHALL be
   preserved.
2. WHEN `tracemap route-flow` reaches an interface, virtual, override, or
   DI-context boundary THEN it SHALL render candidate bridge rows without
   choosing a runtime implementation and SHALL cap affected rows at
   `NeedsReviewStaticRouteFlow` or `UnknownAnalysisGap`.
3. WHEN `tracemap reverse` traverses back across candidate edges THEN it SHALL
   cap affected roots and paths at `NeedsReviewReversePath` or weaker and SHALL
   preserve candidate direction in the path evidence.
4. WHEN `tracemap impact --include-paths` or future reverse context includes
   candidate paths THEN candidate context SHALL NOT turn a change into
   `StaticImpactEvidence`; affected items SHALL be `NeedsReviewImpact`,
   `PathContextUnavailable`, `ReverseContextUnavailable`, or
   `UnknownAnalysisGap` according to coverage and selector state.
5. WHEN combined report or portfolio report summarizes candidates THEN it SHALL
   show counts, fan-out, gaps, limitations, and source coverage without
   claiming runtime reachability.
6. WHEN vault export or docs-export/RAG-ready evidence docs include candidate
   evidence THEN they SHALL preserve candidate states, supporting IDs,
   limitations, and safe wording rather than flattening candidates into normal
   call edges.
7. WHEN a consumer cannot read the candidate builder, required schema, or
   supporting rule catalog entries THEN it SHALL emit an explicit unavailable
   or schema gap rather than silently omitting candidates.

### Requirement 8: Output Safety

**User Story:** As a maintainer, I want public artifacts to remain safe even
when candidate evidence crosses many rows.

#### Acceptance Criteria

1. WHEN candidate rows, gaps, summaries, or docs chunks render metadata THEN
   they SHALL omit or hash raw source snippets, raw SQL, raw config values,
   raw URLs, hostnames, raw remotes, local absolute paths, private labels,
   endpoint secrets, connection strings, credentials, and secret-looking values.
2. WHEN file paths are rendered THEN they SHALL be repo-relative or safe-hashed
   according to existing helper behavior.
3. WHEN DI registration evidence includes expressions, factory bodies, config
   keys, or service locator arguments THEN output SHALL use closed-set shape
   names, safe symbol IDs, or hashes only.
4. WHEN Markdown is rendered THEN table cells SHALL escape pipes, line endings,
   and link delimiters.
5. WHEN JSON is rendered THEN optional fields SHALL use `null` or empty arrays
   consistently, and metadata maps SHALL use deterministic key ordering.
6. WHEN logs are emitted THEN they SHALL include counts, gap codes, rule IDs,
   and safe paths only.

### Requirement 9: Tests and Validation

**User Story:** As a maintainer, I want focused tests that prove this bridge
stays bounded and honest.

#### Acceptance Criteria

1. Tests SHALL cover interface member candidate derivation from
   `ImplementsInterfaceMember` relationships.
2. Tests SHALL cover explicit interface implementation without display-name
   equality.
3. Tests SHALL cover override candidate derivation from `Overrides`
   relationships and bounded override-chain traversal.
4. Tests SHALL cover interface member candidate derivation from
   `ImplementsInterfaceMember` relationships under full semantic coverage with
   Tier1 relationship facts, reduced semantic coverage with Tier2/Tier3
   relationship facts, and no-candidate behavior when relationship facts are
   absent or symbol IDs are unavailable.
5. Tests SHALL cover DI registration-context candidates, registration
   compatibility unproven with explicit gap emission and no candidate creation,
   multiple registrations, open generics, factories, scanning, keyed/named
   services, decorators, and service locator gaps.
6. Tests SHALL cover high fan-out caps, deterministic ordering, stable IDs,
   cycle safety, and byte-stable outputs.
7. Tests SHALL prove route-flow, reverse, impact/include-paths, report,
   portfolio, vault export, and docs-export consumers preserve candidate
   wording and downgrade caps.
8. Tests SHALL prove candidate context cannot produce `StrongStaticPath`,
   `StrongStaticRouteFlow`, `StrongStaticReversePath`, `StaticImpactEvidence`,
   release approval, runtime impact, or runtime binding language.
9. Tests SHALL prove reduced semantic coverage and missing optional schemas
   produce gaps rather than clean absence.
10. Tests SHALL prove unsafe values do not render in Markdown, JSON, logs,
    vault graph output, docs-export chunks, or committed fixtures.
11. Tests SHALL prove candidate stable IDs remain byte-identical across
    repeated derivation from identical combined index inputs, sorted
    relationship fact IDs, sorted registration fact IDs, and deterministic
    algorithm ID.
12. Tests SHALL prove route-flow, reverse, impact, report, portfolio, vault,
    and docs-export Markdown and JSON outputs containing candidate evidence do
    not render "runtime target", "selected implementation", "will call",
    "is injected", "actual binding", "resolved at runtime", "proves impact",
    "runtime dispatch proof", or "dependency-injection binding proof"
    language.
13. Validation for implementation PRs SHALL include `dotnet test
    src/dotnet/TraceMap.sln`, at least one public-safe CLI smoke, relevant
    pinned smoke checks from `docs/VALIDATION.md`, `./scripts/check-private-paths.sh`,
    and `git diff --check`, or a recorded deferral with evidence.
