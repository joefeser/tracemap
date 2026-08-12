# Property Flow Depth v0 Implementation State

Status: spec-reviewed-ready-for-pr

Branch: `codex/property-flow-depth-spec`

Base: fresh `origin/dev` at
`8eb8a72d85878f69f1a2d2851089e269d1f26a78`

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

## Deferred Work

- Product implementation begins only after this specification is reviewed and
  merged.
- No Windows, Access, browser, database, network service, or customer fixture
  is required for this runway.
- No provider/package-specific mapper integration is authorized by this spec.
