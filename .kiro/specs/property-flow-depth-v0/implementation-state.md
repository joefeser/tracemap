# Property Flow Depth v0 Implementation State

Status: pr1-semantic-razor-producer-implemented

Branch: `codex/property-flow-semantic-razor-v0`

Base: fresh `origin/dev` at
`ac887c857828c86da7a98cdf72b5b235d2c86391`

Issue: [#517](https://github.com/joefeser/tracemap/issues/517)

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
