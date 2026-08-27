# Property Flow Depth v0 Implementation State

Status: pr2-direct-property-mapping-producer-implemented

Branch: `codex/property-flow-direct-mapping-v0`

Base: fresh `origin/dev` at
`1d50aa5d70f58e851411b953b1dbccee38625fda` (PR 1 landed as merge a3b9f221 and
is an ancestor of the base).

Issue: [#517](https://github.com/joefeser/tracemap/issues/517)

## PR 2: Direct Property Mapping Producer (this branch)

Scope shipped (tasks 2.1-2.7 only; PR 3/PR 4 remain untouched):

- New fact `PropertyMappingDeclared`, rules `csharp.semantic.propertymapping.v1`
  (Tier1) and `csharp.semantic.propertymapping-gap.v1` (Tier4), producer
  extractor identity `CSharpPropertyMappingExtractor`
  (`csharp-property-mapping/0.1.0`), coverage label
  `bounded-static-property-mapping`.
- Supported v0 shapes: simple assignment `target.Property = source.Property`,
  object-initializer member assignment, and the same initializer rule inside
  lambdas/query clauses labeled `projection`. Unwrapping is limited to
  parentheses, null-forgiving postfix `!`, and same-type casts that preserve
  the resolved source property symbol.
- Fail-closed categorical gaps (`PropertyMappingShapeUnsupported`,
  `PropertyMappingTargetAmbiguous`, `PropertyMappingSemanticUnavailable`,
  `PropertyMappingTruncated`) with closed `shapeState` values
  (`invocation`, `interpolation`, `binary-expression`,
  `conditional-expression`, `switch-expression`, `pattern-expression`,
  `expression-transform`, `conversion`, `type-conversion-required`,
  `indexer-element`, `compound-assignment`, `dynamic`,
  `ambiguous-candidates`, `incomplete-binding`, `unresolved-binding`);
  the aggregate truncation row additionally uses `truncation`;
  no expressions, snippets, constant values, or protected digests are stored.
- Deterministic bounds declared before implementation: 25 facts per method-like
  container, 250 per document, 100 gaps per document; exceedances fold into one
  aggregated truncation gap with occurrence plus suppressed-fact/gap counts.
- Isolation preserved: generic reducer matching excludes both new rule IDs;
  property-flow reader admission excludes them (PR 3 removes this only with
  exact-ID composition); producer-local gaps do not flip build status, analysis
  level, or toolchain-capability classification.

Decisions recorded for reviewers:

- Both endpoints must resolve to ordinary non-indexer properties whose types
  match under `SymbolEqualityComparer.Default`; mismatched property types emit
  `type-conversion-required` rather than guessing through conversions. Only
  nullable-annotation-insensitive equality is required, mirroring PR 3's
  future exact-ID joins.
- Assignments whose resolved endpoints are the identical property symbol are
  skipped as vacuous self-copies and documented in the catalog limitations.
- Plain value copies between a property and a fully resolved local, parameter,
  field, or method group stay silent (value copy-in/out, not member mapping);
  unresolved/ambiguous/transforming counterparts still fail closed. This keeps
  ubiquitous extraction patterns from becoming global noise while preserving
  truthful gap semantics.
- Constructor bodies, field/property initializers, tuple-deconstruction, and
  dictionary-key assignments are outside v0 containers/shapes and documented as
  such; constructor forwarding remains deferred to its own bounded slice.
- Per-method bound keys are the nearest method-like declaration (methods,
  local functions, accessors, operators); anonymous functions are not separate
  containers so lambda-bodied assignments attribute to their enclosing member.
- Exact-head review tightened Tier1 admission: record `with` initializers and
  getter-only, inaccessible, or diagnostically invalid target writes now emit
  categorical gaps; conditional-access and await transforms normalize to the
  documented `expression-transform` state; and the aggregate truncation row
  reserves a slot inside the declared 100-gap document bound.

Validation on this branch:

- focused after exact-head review: PropertyMappingTests plus reducer/reader
  isolation 16/16;
- full solution: 1714/1714 tests, clean build with zero warnings/errors,
  changed-file format verification, private-path guard, and diff check;
- original focused producer validation: PropertyMappingTests 11/11, reducer isolation
  (`Reduce_excludes_semantic_property_mapping...`) and reader isolation
  (`Property_flow_ignores_direct_property_mapping...`) pass;
- synthetic non-compiling fixture scan keeps healthy-file mappings byte-stable
  across repeated scans while the manifest truthfully reports
  `FailedOrPartial`;
- full-solution results recorded in the PR description.

## Review log: independent architecture review response

A read-only independent architecture review of the branch head found the
producer/consumer isolation airtight (every consumption path verified,
including the combined path-graph view) with zero blocking findings and
five important findings. All five were accepted and fixed in one review
response commit:

1. literal-RHS assignments, including object-initializer constant
   defaults, no longer emit per-assignment gaps and stay silent like
   resolved non-property counterparts (design.md Slice B noise rule);
2. compound accumulation into locals, parameters, fields, or literals
   stays silent; compound assignments touching a property on a
   transforming counterpart still fail closed;
3. distinct symbols whose canonical IDs collapse (different generic
   instantiations of one property) fail closed as
   `PropertyMappingTargetAmbiguous`/`canonical-identity-collision`
   instead of emitting identical source/target IDs that would seed false
   exact-ID self-joins in PR 3;
4. the requirement 6.6 reducer regression now proves a genuinely
   matching baseline fact plus eleven mapping facts and one gap produce
   byte-identical reduce output with unchanged `DefiniteImpact`
   classification and no fan-out warning;
5. the property-flow reader isolation test now asserts a lineage path
   exists so its exclusion assertion cannot pass vacuously.

Catalog limitations were extended for same-symbol cross-instance copies,
anonymous-type compilation-order identity indices, resolved chained
member access, and both-side non-property silence. Self-scan note:
tracemap cannot currently self-scan out of the box because the scanner's
semantic load executes the CLI project's source generator and rewrites
its obj state file mid-scan (`SourceSnapshotChangedDuringScan`); the
dogfood noise receipt therefore scans a disposable tree without the
generator-bearing CLI project.

Deferred to later slices in recorded order:

- PR 3 removes the temporary reader exclusion and joins by canonical IDs;
- PR 4 owns the consumer/public-safe audit;
- constructor forwarding, AutoMapper/Mapster profiles, custom resolvers, legacy
  ASP.NET MVC, runtime/reachability claims all remain deferred unchanged.

## Reconciliation

Issue #165 and the broad property-flow v1/composition runway are already
shipped. Current code has structural Razor bindings/form targets, Tier3
syntax/convention model-binding targets, cross-file/ambiguity gaps, and a
property-flow report that can compose first-hop Razor/Angular/model evidence
and bounded route-flow context.

#517 remains valid only as a precision runway. The missing producer seams are:

1. compiler-resolved MVC/Razor Pages action/handler property targets across
   files and partial types; and
2. compiler-resolved direct property-to-property mapping relationships.

The spec does not reopen shipped Angular roots, route-flow traversal, terminal-
context consumers, Access work, SQL evidence, reverse impact, or static
explorer work.

## Scope Decision

Implementation order is:

1. semantic Razor producer;
2. direct property mapping producer;
3. exact-ID property-flow composition; and
4. consumer/public-safe fixture audit.

AutoMapper, Mapster, custom profiles/resolvers, and runtime mapping remain
deferred. Their package presence or method labels cannot establish mapping
semantics. Constructor forwarding is conditional on reusing current exact
parameter-flow evidence without inventing a second identity model and is now
explicitly deferred from v0.

The first implementation PR must pin the real ASP.NET Core metadata identities
and record the exact supported signatures before emitting Tier1 facts. No
framework name, controller suffix, handler prefix, or source lookalike is
sufficient by itself.

The signed-metadata admission predicate is assembly name, Microsoft public-key
token, metadata-only provenance, and exact supported type/member signature.
Runtime/package version is pinned fixture provenance, not an admission key.
Legacy `System.Web.Mvc` remains outside this runway.

The older `ui-field-property-lineage-composition` implementation state now
marks that spec as a historical queue. Its remaining Razor depth and related
compatibility ownership move here so the repository no longer has two active
property-flow implementation specs.

## Repository Evidence Reviewed

- GitHub issue #517.
- `.kiro/specs/ui-field-property-lineage/` and
  `.kiro/specs/ui-field-property-lineage-composition/`.
- `src/dotnet/TraceMap.Core/RazorBindingExtractor.cs`.
- `src/dotnet/TraceMap.Core/CSharpSyntaxExtractor.cs`, especially
  `AddRazorModelBindingTargetFacts`.
- `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs` declaration, property-
  access, argument, alias, assignment, and object-creation seams.
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs` Razor/model join and
  classification logic.
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs` and current Razor/
  syntax/semantic tests.
- `rules/rule-catalog.yml`, `docs/VALIDATION.md`, and consumer references.

## Review Log

A fresh read-only Kiro review ran on exact head
`ca66395620a6b7f4626fa460c7839040dde3de6d` with explicit model
`claude-opus-5`, session `a8a72098-22c2-476d-87c3-dd3b1a8ecb74`. Kiro
reported reduced tool coverage because command execution was denied, but it
read the spec and relevant repository contracts. Local review artifacts are
not committed. Their SHA-256 values are:

- prompt: `50d8fc26e15ae5f80f8218c2686b35e9a1ceedff5e6ec34f74fec78297a5f5f5`;
- raw: `88a15f1f7b07735c53af9f540fd121e6f1805cb533b31ab882f407bea4b4403b`;
- clean: `aff12f20bd46c7c923b62ae191c8a6080c77e3f33a3765da056e27ce50a47613`.

All five blocking findings were accepted and patched:

1. the positive Tier1 fixture now requires real signed ASP.NET Core metadata;
2. framework version is fixture provenance rather than an admission key, with
   a closed candidate assembly allowlist and legacy MVC deferral;
3. both new fact families and gap rules require explicit generic-reducer
   exclusion and regression baselines;
4. row cardinality and the `AnalysisGap`/PascalCase gap contract are closed;
5. the older composition spec no longer claims active queue ownership.

Important findings were also folded into the runway: tier-aware bounded
selection, no automatic v0 classification promotion, constructor-forwarding
deferral, catalog naming/reconciliation, complete consumer inventory, producer
bounds, report metadata allowlists, one limitations key, bounded hashing, and a
review-tier Tier1/Tier3 reconciliation tuple.

Hosted review on PR #647 then found two current-head issues. Both were accepted:

- canonical binding attributes now classify binding source only after
  controller/page-model and action/handler ownership independently qualify;
- the superseded composition spec's task header now matches its historical
  implementation-state status and points to this active queue.

The fresh exact-head Codex follow-up then identified two staging gaps, both
accepted: PageModel property `[BindProperty]` admission is now handler-
independent and cannot synthesize handler ownership, and semantic Razor reducer
isolation moves into PR 1 so that producer can land safely before mapping work.

The next exact-head Codex pass found one remaining independent-landing hazard:
the current property-flow reader would consume semantic
`RazorModelBindingTarget` rows through display-name joins before PR 3. PR 1 now
must exclude its semantic producer rule from that reader and regression-lock
unchanged property-flow output; PR 3 removes the exclusion only with exact-ID
admission and collision coverage.

The next exact-head Codex pass identified two producer-admission gaps, both
accepted. PR 1 now has a conservative public-instance/writable/non-indexed/non-
ref property predicate with explicit negative fixtures. Handler-independent
PageModel property targets record canonical `supportsGet` attribute evidence
without synthesizing handler HTTP methods or inheriting the syntax producer's
POST convention.

## Validation

Initial spec-only validation passed before review:

- focused property-flow/syntax baseline: 38/38 tests passed;
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed.

Final validation after review-driven edits passed:

- focused property-flow/syntax baseline: 38/38 tests passed;
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed;
- changed-file scope is limited to the five new spec files plus the superseded-
  spec implementation-state correction.

PR 1 implementation validation at the fresh implementation base passed:

- focused semantic Razor, property-flow, and reducer baseline: 46/46 tests;
- `dotnet build src/dotnet/TraceMap.sln`: passed with zero warnings/errors;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 1,456/1,456 passed;
- targeted `dotnet format --verify-no-changes`: passed;
- locked TypeScript restore plus `npm run check --prefix src/typescript`: 8/8
  files and 33/33 tests passed (the existing npm audit reported two high-
  severity dependency advisories; this slice did not mutate that dependency
  lane);
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed.

Local signed-fixture provenance was Microsoft.AspNetCore.App `10.0.10` and
Microsoft.NETCore.App `10.0.10`; the test project records that runtime patch,
while product admission remains version-independent.

Hosted review on PR #648 identified five producer-boundary defects. The
follow-up patch makes PageModel type and bound-property identities distinct,
emits the promised forged property-only framework gap, honors canonical
`[NonController]` and `[NonHandler]` exclusions, and walks source base-model
properties deterministically with the most-derived declaration governing
hidden or overridden names. The focused 46-test baseline and full 1,456-test
solution suite remained green after these corrections.

A fresh exact-head Codex review then identified two remaining coverage
boundaries. MVC controller actions now require a public, top-level,
non-abstract, closed controller type before Tier1 emission; rejected controller
types and generic action methods emit categorical owner gaps. Model expansion
also emits a categorical gap when a metadata-only base type can contribute
properties that the source-only traversal cannot inspect. The patch preserves
eligible source-declared properties and does not infer metadata members.

Post-patch validation passed: focused semantic Razor/property-flow/reducer
tests 46/46, build with zero warnings/errors, full solution tests 1,456/1,456,
changed-file formatting, private-path guard, and `git diff --check`. The first
full-suite attempt encountered the unrelated restore-diagnostic test flake;
that exact test passed 1/1 on immediate isolated rerun before the clean full
suite result.

The next fresh Codex pass found two convention edges. Canonical MVC exclusion
attributes are now honored through the relevant base-type or override chain,
and Razor handler parsing records the complete conventional HTTP-method
segment (including custom verbs) instead of treating any `OnPost...` prefix as
POST. Inherited-exclusion and `OnPoster`/`OnTraceAsync` fixtures lock both
behaviors without making runtime endpoint-selection claims.

The following exact-head review closed three additional ownership edges:
canonical `[FromServices]` parameters are excluded from request binding,
handler-independent `[BindProperty]` facts use the attributed property as the
owner exactly as the design requires, and eligible actions declared on an
abstract source base are projected to discoverable concrete source controllers
when no same-name declaration intervenes. Each behavior has a focused negative
or identity regression and remains bounded to compiler-proven source symbols.

The subsequent exact-head pass closed three more declared-contract edges:
projected concrete descendants independently honor canonical `[NonController]`,
source record model types fail closed with a categorical unsupported-type gap,
and trusted HTTP-method attributes are preserved through an override chain.
Regression fixtures cover each case without widening record/constructor support.

The next exact-head review closed canonical `[BindNever]` exclusion, prevented
superseded base-action projection through intermediate abstract overrides, and
deduplicated partial action definition/implementation pairs before fact-bound
accounting. Focused fixtures assert excluded properties, one effective layered
action, and one partial action expansion rather than relying on aggregate counts.

The following exact-head pass closed abstract/non-discoverable PageModel owner
admission for handlers and bound properties, enforced public parameterless
construction for non-body complex model binding, and treated a same-signature
`new` declaration as an inherited-action hiding boundary. Each rejected surface
now emits no Tier1 target and preserves a categorical coverage gap where the
owner or model boundary was otherwise visible.

An exact-head Kiro Opus advisory review then identified three blocking truth
issues. Producer-local Razor gaps no longer change the repository build result
to failed/partial, while they still retain a reduced analysis label and a
specific known-gap message. Resolved scalar/terminal parameters now produce no
false unavailable gaps; unsupported record/collection shapes and metadata-only
complex models remain categorically distinct. Finally, the spec and Tier1
schema now state that existing Tier3 rows are contextual support only because
they lack the canonical identity needed for exact reconciliation; PR 3 owns
actual composition.

The exact-head Kiro re-review confirmed those three fixes and found two further
P2 truth boundaries. Producer-local Razor gaps are now excluded from global
semantic-toolchain reduction and toolchain capability support while remaining
visible as rule-backed known gaps. Expanded-property gaps now carry canonical
owner, endpoint-method, parameter, and ordinal identity so projected concrete
owners cannot collapse into one occurrence. The same pass repaired vacuous
negative assertions, classified arrays with collection gaps, closed the emitted
gap vocabulary, and aligned the Tier1/Tier3 requirement with the staged
contextual-only reconciliation contract. Other request-source attributes,
class-level BindProperties, recursive complex BindProperty expansion, and
custom handler-verb interpretation remain documented v1 limitations rather
than hidden claims.

## Deferred Work

- PR 1 now emits compiler-resolved MVC action parameter, Razor Page handler
  parameter, and handler-independent PageModel `[BindProperty]` targets. The
  producer admits ASP.NET Core only through exact metadata type, assembly name,
  and Microsoft public-key-token checks. The positive fixture targets
  `net10.0`, pins `RuntimeFrameworkVersion` `10.0.10`, and the implementation
  does not use runtime version as a trust predicate.
- Semantic targets carry canonical owner, parameter, model-type, and property
  identities; exact framework-admission provenance; a closed safe property
  schema; a review-tier Tier1/Tier3 reconciliation tuple; bounded sorted HTTP
  methods; coverage; and fixed limitations. Unsupported property/type and
  forged-framework boundaries emit categorical Tier4 `AnalysisGap` facts.
- Syntax and semantic rows intentionally coexist in generic NDJSON/SQLite.
  Until PR 3 implements exact-ID composition, the new producer fact/gap rules
  are explicitly excluded from generic contract reduction and semantic targets
  are excluded from the current name-based property-flow reader. Regression
  tests prove same-visible-name semantic rows cannot become roots, paths, or
  stronger classifications.
- The implementation is covered by real signed framework, partial/cross-file,
  overload/multi-verb, handler-independent property, same-full-name cross-
  assembly, source-lookalike, dynamic/type-parameter/external, closed-schema,
  catalog, storage, determinism, reducer-isolation, and reporter-isolation
  fixtures.
- PR 2 direct property mapping, PR 3 exact-ID composition, and PR 4 consumer/
  public-safe validation remain deferred in the recorded order.
- No Windows, Access, browser, database, network service, or customer fixture
  is required for this runway.
- No provider/package-specific mapper integration is authorized by this spec.
