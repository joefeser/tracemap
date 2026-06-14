# Parameter and Value-Origin Flow Requirements

## Introduction

TraceMap already records call edges, object creation, argument-passing facts, local/field alias facts, parameter-forwarding rows, and flow-boundary facts in several adapters. Combined path reports can use parameter-forwarding hops, but the behavior is still adapter uneven and intentionally stops short of full taint analysis.

This spec defines the next static flow layer: deterministic value-origin evidence for method parameters, constructor arguments, simple locals/members, callbacks, and boundaries between application code and dependency surfaces. The goal is to answer questions such as:

- Does a request DTO or parameter have static evidence of being passed into a service method?
- Does a constructor argument get stored into a member and later passed to another call?
- Does a value reach an HTTP client, SQL surface, package surface, or event/message surface with evidence?
- Where does TraceMap stop because mutation, aliasing, collection contents, branch feasibility, DI, reflection, or dynamic dispatch cannot be proven?

This is not a runtime taint engine. It is static, evidence-backed, bounded value-origin context. It must not claim runtime execution, branch feasibility, object identity, collection contents, mutation semantics, or concrete dependency injection state unless a dedicated rule emits deterministic evidence.

## Scope

In scope:

- Normalize existing `ArgumentPassed`, `LocalAlias`, `FieldAlias`, `ParameterForwardEdge`, `CollectionElementFlow`, `MutationSemantics`, `BranchFeasibility`, and `FlowBoundary`-style evidence into a shared value-origin contract.
- Add or align adapter extraction for deterministic direct parameter forwarding and argument-to-parameter edges.
- Add or align constructor argument flow into fields/members only where simple and deterministic.
- Track simple local/member origins for direct assignments and bounded same-method aliases.
- Represent lambda, callback, async, iterator, and closure boundaries as evidence-backed hops or explicit gaps.
- Connect request DTO/value origins to service calls and dependency surfaces where existing facts support the path.
- Preserve rule IDs, evidence tiers, file spans, symbol IDs, commit SHAs, scan IDs, extractor IDs, and extractor versions.
- Make combined report/path/reverse/impact behavior label value-origin evidence conservatively.
- Add tests and validation guidance for each supported flow pattern.

Out of scope:

- No full taint engine.
- No runtime value inference.
- No symbolic execution or branch feasibility beyond existing deterministic `BranchFeasibility` facts.
- No mutation semantics beyond direct rule-backed mutation facts.
- No arbitrary alias analysis, object identity tracking, collection-content tracking, loop iteration modeling, thread/interleaving analysis, or lifetime analysis.
- No runtime dependency injection state, reflection target execution, dynamic dispatch target certainty, serializer mapping expansion, or generated-code inference.
- No source snippets by default.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Requirements

### Requirement 1: Shared Value-Origin Fact Contract

**User Story:** As a TraceMap maintainer, I want value-origin facts to use shared role properties so every language adapter can contribute to combined flow analysis.

#### Acceptance Criteria

1. WHEN an adapter emits value-origin evidence THEN each fact SHALL include rule ID, evidence tier, file span, repo, commit SHA, scan ID, extractor ID, and extractor version through the existing fact model.
2. WHEN the origin, argument, parameter, source, target, or constructor symbol is known THEN the fact SHALL use the shared role properties documented in `docs/LANGUAGE_ADAPTER_CONTRACT.md`: `{role}SymbolId`, `{role}SymbolLanguage`, `{role}SymbolKind`, and `{role}SymbolDisplayName`.
3. WHEN legacy display aliases are required by an existing reader THEN adapters MAY emit those aliases in addition to the shared role properties, but SHALL keep the shared role properties authoritative.
4. WHEN semantic symbol binding is unavailable THEN adapters SHALL emit syntax/textual facts or gaps with reduced evidence tier rather than invent compiler-resolved symbol IDs.
5. WHEN raw expressions are represented THEN adapters SHALL store expression kind, deterministic expression hash, declaration span, or safe display name, not raw source snippets.
6. WHEN a fact participates in SQLite flow tables THEN storage SHALL preserve the originating fact ID, rule ID, evidence tier, source span, and role symbol IDs where available.
7. WHEN flow evidence is absent under reduced coverage THEN reports SHALL label the result as an analysis gap rather than proof that no value path exists.

### Requirement 2: Direct Argument-to-Parameter Flow

**User Story:** As an investigator, I want call-site arguments mapped to callee parameters so I can see when a value is forwarded into another method or constructor.

#### Acceptance Criteria

1. WHEN semantic binding resolves a call target and parameter list THEN the adapter SHALL emit `ArgumentPassed` with caller, callee, argument, and parameter role metadata.
2. WHEN only syntax shape is available THEN the adapter MAY emit `ArgumentPassed` with ordinal parameter placeholders and `Tier3SyntaxOrTextual`, but SHALL label parameter identity as unresolved.
3. WHEN named arguments, optional arguments, spread/rest parameters, params/varargs, keyword arguments, or default parameters are present THEN the adapter SHALL map only deterministically known positions/names and emit gaps for unresolved mapping.
4. WHEN an argument expression is a direct parameter, local, member, or field with stable symbol evidence THEN the fact SHALL include the argument symbol role.
5. WHEN an argument expression is a literal, construction expression, invocation result, interpolated expression, lambda, or dynamic expression THEN the fact SHALL include expression kind/hash and SHALL NOT claim a concrete origin unless a separate rule proves it.
6. WHEN an argument is passed to a constructor THEN constructor and created-type identity SHALL be preserved where available.
7. WHEN call target binding is ambiguous, dynamic, reflective, or dependency-injection mediated THEN the adapter SHALL emit review-tier evidence or a gap, not a strong forwarding edge.

### Requirement 3: Parameter Forwarding Edges

**User Story:** As a reviewer, I want direct parameter-to-parameter forwarding rows so path and impact reports can connect request inputs to downstream calls.

#### Acceptance Criteria

1. WHEN an `ArgumentPassed` fact proves a caller parameter is directly passed to a callee parameter THEN storage SHALL derive or preserve a `ParameterForwardEdge`.
2. WHEN a same-method local alias chain connects a caller parameter to an argument THEN storage MAY derive a `ParameterForwardEdge` if every alias hop is deterministic and the chain length is bounded by a documented constant, default `3` hops.
3. WHEN a field/member has exactly one deterministic constructor assignment from a constructor parameter and is later passed as an argument from an instance method in the same type THEN storage MAY derive a reviewable `ParameterForwardEdge`; "same type" and "constructor uniqueness" mean constructors visible in the current adapter's analyzed declaring-project source scope, with partial/unloaded/generator-hidden constructors treated as ambiguous.
4. WHEN multiple constructors, multiple assignments, object mutation, property setters, or ambiguous field origins exist THEN the edge SHALL be omitted or downgraded with an explicit gap.
5. WHEN a forwarding edge is emitted THEN it SHALL include source method/parameter, target method/parameter, source and target symbol IDs where available, supporting fact IDs, rule ID, evidence tier, file span, and edge kind.
6. WHEN forwarding crosses adapter/source boundaries THEN cross-source edges SHALL require explicit boundary evidence such as endpoint matching, package surface matching, or another documented combined rule.
7. WHEN paths use parameter-forwarding edges THEN reports SHALL include the limitation that they are static argument evidence, not full taint or mutation tracking.

### Requirement 4: Local and Member Origin Tracking

**User Story:** As a maintainer, I want simple local/member origin evidence so direct assignments can improve flow without becoming arbitrary alias analysis.

#### Acceptance Criteria

1. WHEN a local variable is directly assigned from a known parameter, local, field, or member symbol THEN the adapter MAY emit `LocalAlias`.
2. WHEN a field/member is directly assigned from a known parameter, local, field, or member symbol THEN the adapter MAY emit `FieldAlias`.
3. WHEN assignments involve object mutation, collection mutation, property setter side effects, tuple/deconstruction, ref/out effects, destructuring with ambiguous symbols, loop-carried state, or complex control flow THEN the adapter SHALL emit a boundary/gap rather than a strong alias.
4. WHEN alias chains are used for forwarding THEN the chain length SHALL be bounded and deterministic, defaulting to `3` hops unless the implementation documents another fixed value in the rule catalog.
5. WHEN alias evidence is stale after a mutation boundary or ambiguous assignment THEN downstream reports SHALL not continue the origin as strong evidence.
6. WHEN a member origin belongs to an instance created in another method, factory, DI container, serializer, or reflection path THEN TraceMap SHALL not infer object identity without a dedicated rule.
7. WHEN local/member facts are emitted THEN raw assignment expressions SHALL not be stored by default.

### Requirement 5: Constructor Argument and Member Flow

**User Story:** As a reviewer, I want constructor arguments that are stored into members and later used to be visible as static evidence when it is deterministic.

#### Acceptance Criteria

1. WHEN a constructor parameter is assigned directly to a field or simple auto-property in the same containing type THEN the adapter MAY emit a constructor/member origin fact or `FieldAlias`.
2. WHEN exactly one constructor assignment provides the member origin for a type in the analyzed source scope THEN derived forwarding MAY use that member origin.
3. WHEN multiple constructors assign the same member from different parameters, conditional assignments, factory methods, object initializers, records, generated members, or partial-type ambiguity are present THEN TraceMap SHALL downgrade or emit an analysis gap.
4. WHEN a constructor argument is passed through object creation into a member and later into a dependency surface THEN reports SHALL show each supported hop, not collapse the path into one conclusion.
5. WHEN constructor overload resolution is semantic and complete THEN evidence MAY be `Tier1Semantic`; syntax-only constructor-like calls SHALL remain lower tier.
6. WHEN constructor/member flow depends on runtime DI activation, serializer construction, reflection, or framework model binding THEN TraceMap SHALL stop at a boundary unless a dedicated deterministic rule exists.

### Requirement 6: Lambda, Callback, Async, and Closure Boundaries

**User Story:** As an investigator, I want TraceMap to show where values enter callbacks or async continuations without claiming scheduling or runtime execution.

#### Acceptance Criteria

1. WHEN a lambda/callback receives a parameter and passes it directly to another call inside the same syntactic body THEN adapters MAY emit local argument evidence for the callback body.
2. WHEN a lambda captures an outer parameter/local and passes it inside the callback body THEN adapters MAY emit a `CapturedValueFlow` or equivalent review-tier fact if the capture is syntactically direct.
3. WHEN the callback target, delegate binding, event subscription, scheduler, promise/task continuation, or async execution order is not statically proven THEN TraceMap SHALL emit `CallbackBoundary` or `AsyncBoundary` gaps.
4. WHEN async/await preserves a direct method call relationship in source and semantic binding is available THEN normal call and argument evidence MAY be used, but reports SHALL not claim runtime ordering.
5. WHEN iterator/yield/generator flow is detected THEN TraceMap SHALL label it as a boundary unless a later rule explicitly supports it.
6. WHEN closures mutate captured values or capture mutable collections/objects THEN TraceMap SHALL not continue origin evidence as strong flow.
7. WHEN `CallbackBoundary` or `AsyncBoundary` facts are introduced THEN implementation SHALL extend an existing flow-boundary rule or add adapter-local callback/async boundary rule IDs before emitting them; each fact SHALL include `boundaryKind`, source/target or containing symbol role properties when available, evidence tier, file span, and supporting fact IDs when derived from existing evidence.
8. WHEN `CapturedValueFlow` is introduced THEN implementation SHALL use a documented adapter-local callback-flow rule ID and SHALL include captured symbol role properties, callback/lambda containing symbol properties, expression hash/kind, evidence tier, file span, and supporting fact IDs where available.

### Requirement 7: Request and Dependency Surface Flow

**User Story:** As a product user, I want to see request DTOs and values flow into service calls and dependency surfaces when static evidence supports the path.

#### Acceptance Criteria

1. WHEN endpoint facts expose a containing method/function symbol and request/route/body parameter evidence is available THEN TraceMap MAY treat those parameters as value-origin roots.
2. WHEN parameter-forwarding and call edges connect an endpoint root to a service method THEN combined paths/reverse/impact MAY show a static value-origin path with caveats.
3. WHEN a value-origin path reaches SQL, HTTP client, package/config, event/message, or other dependency surfaces THEN reports SHALL show the terminal surface evidence rule ID and evidence tier.
4. WHEN a dependency surface is attached only to a file or unresolved symbol THEN TraceMap SHALL not invent a value-origin path and SHALL emit an unlinked-surface or analysis gap.
5. WHEN request DTO member-to-member flow is not proven THEN TraceMap SHALL not claim individual property values reach a surface; it MAY show DTO/parameter-level evidence.
6. WHEN package or event/message surfaces are not yet implemented in a language adapter THEN the report SHALL label the missing surface support as a gap, not absence of dependency.
7. WHEN cross-repo flow is reported THEN the path SHALL preserve source labels, source index IDs, scan IDs, commit SHAs, and endpoint or surface match rule IDs.

### Requirement 8: Classifications, Evidence Tiers, and Gaps

**User Story:** As a reviewer, I want value-origin findings to be classified by evidence strength and explicit gaps.

#### Acceptance Criteria

1. WHEN every hop in a value-origin path is semantic, compiler-resolved, and full coverage is credible THEN TraceMap MAY emit an additive value-origin classification of `StrongStaticValuePath` while preserving the existing path-level classification field.
2. WHEN a path uses structural endpoint/surface evidence or deterministic lower-tier language evidence THEN TraceMap SHALL emit an additive value-origin classification of `ProbableStaticValuePath` while preserving the existing path-level classification field.
3. WHEN a path uses syntax-only, name-only, callback, unique-constructor-member, or cross-source reconciliation evidence THEN TraceMap SHALL emit an additive value-origin classification of `NeedsReviewValuePath` while preserving the existing path-level classification field.
4. WHEN analysis gaps prevent a credible value-origin conclusion THEN TraceMap SHALL emit `UnknownAnalysisGap` as a gap or value-origin note without changing the canonical path-level classification taxonomy.
5. WHEN no value-origin path is found under full credible coverage THEN TraceMap MAY emit `NoValuePathEvidence` as a value-origin gap/note; existing `NoPathFound` remains the canonical path-level no-path classification.
6. WHEN no value-origin path is found under reduced coverage THEN TraceMap SHALL emit `UnknownAnalysisGap`, not `NoValuePathEvidence`.
7. WHEN mutation, aliasing, collection contents, branch feasibility, dynamic dispatch, runtime DI, serializer construction, reflection, generated code, or async scheduling prevents a stronger conclusion THEN the report SHALL include a specific gap code.
8. WHEN value-flow rows are emitted THEN every row and gap SHALL carry a rule ID and evidence tier.
9. WHEN an adapter cannot produce Tier1 semantic evidence, such as syntax-only Python or Kotlin fallback paths, value-origin classifications SHALL be capped at `ProbableStaticValuePath` or `NeedsReviewValuePath`.

### Requirement 9: Combined Reporting and Query Behavior

**User Story:** As an automation author, I want value-origin evidence to appear deterministically in combined reports, paths, reverse queries, diff, and impact without new graph semantics drifting from existing reports.

#### Acceptance Criteria

1. WHEN `tracemap report` reads a combined index THEN it SHALL summarize value-origin evidence counts and limitations if flow tables exist.
2. WHEN `tracemap paths` uses parameter-forwarding, argument, alias, or constructor/member flow evidence THEN it SHALL include notes describing static-flow limitations.
3. WHEN `tracemap reverse` starts from a dependency surface and reaches an upstream value-origin root THEN it SHALL preserve value-flow edge IDs and supporting fact IDs.
4. WHEN `tracemap diff` or `tracemap impact` compares value-flow evidence THEN it SHALL not overclaim added/removed flow under reduced coverage or unstable identity.
5. WHEN combined indexes are missing optional precise flow tables THEN reports SHALL emit schema/coverage gaps and use fallback evidence only at review tier.
6. WHEN outputs include value-origin metadata THEN arrays and metadata keys SHALL be sorted deterministically.
7. WHEN raw source property bags include snippets, literal values, local absolute paths, raw URLs, connection strings, or config values THEN reporters SHALL omit or hash them before output.
8. WHEN single-index `tracemap flow` is in scope for an implementation slice THEN it SHALL preserve its existing behavior and may add value-origin notes; when it is out of scope, the implementation PR SHALL say so explicitly and leave `tracemap flow` behavior unchanged.

### Requirement 10: CLI, Storage, and Compatibility

**User Story:** As a maintainer, I want this flow layer to evolve without breaking existing scans or combined indexes.

#### Acceptance Criteria

1. WHEN new storage rows are needed THEN schema changes SHALL be additive and compatible with existing combined-index validation.
2. WHEN combined report/path/reverse/diff/impact commands read an older index without optional precise flow tables THEN they SHALL continue to work and emit explicit schema gaps where value-origin precision is unavailable; single-index `tracemap flow` may continue fail-fast behavior until a dedicated compatibility slice changes it.
3. WHEN facts are emitted for new value-origin rules THEN `rules/rule-catalog.yml` SHALL document emitted fact types, required properties, evidence tiers, and limitations.
4. WHEN docs are updated during implementation THEN `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, and `docs/ACCEPTANCE.md` SHALL describe the supported flow subset and non-goals.
5. WHEN deterministic reports include value-origin paths THEN repeated runs over identical inputs SHALL be byte-stable.
6. WHEN validation runs THEN language-adapter tests affected by the implementation SHALL pass or be explicitly deferred with reason.

### Requirement 11: Tests and Validation

**User Story:** As a contributor, I want test coverage that proves value-origin flow is useful without hiding static-analysis limits.

#### Acceptance Criteria

1. Tests SHALL cover direct parameter-to-parameter forwarding.
2. Tests SHALL cover same-method local alias forwarding.
3. Tests SHALL cover unique constructor parameter-to-field-to-call forwarding.
4. Tests SHALL cover multiple-constructor or ambiguous field origin downgrade/gap.
5. Tests SHALL cover lambda/callback direct body forwarding and callback boundary gaps.
6. Tests SHALL cover async direct call evidence without runtime ordering claims.
7. Tests SHALL cover request/endpoint parameter flow into a service method and then a dependency surface where existing facts support the path.
8. Tests SHALL cover mutation, collection, dynamic dispatch, DI, reflection, branch feasibility, and serializer boundary gap emission.
9. Tests SHALL prove no raw source snippets are emitted by default.
10. Tests SHALL prove combined outputs are deterministic and label reduced coverage.
11. Validation SHALL include `git diff --check`, `./scripts/check-private-paths.sh`, and relevant adapter tests for changed languages.
12. Tests SHALL cover older-index compatibility where optional flow tables are absent from combined indexes.
13. Tests SHALL prove syntax-only adapters cannot emit `StrongStaticValuePath`.
14. Tests SHALL prove legacy alias properties do not override shared role properties when both are present.
15. Tests SHALL prove cross-source value paths preserve source labels, scan IDs, commit SHAs, and supporting fact or edge IDs.
