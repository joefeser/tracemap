# Property Flow Depth v0 Implementation State

Status: spec-drafted-awaiting-review

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
parameter-flow evidence without inventing a second identity model.

The first implementation PR must pin the real ASP.NET Core metadata identities
and record the exact supported signatures before emitting Tier1 facts. No
framework name, controller suffix, handler prefix, or source lookalike is
sufficient by itself.

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

## Validation

Pending exact-head spec review and final validation.

Planned spec-only checks:

- `git diff --check`;
- `./scripts/check-private-paths.sh`;
- verify the committed diff is limited to this spec folder; and
- run a focused baseline test readback for existing Razor/property-flow
  behavior without changing product code.

## Deferred Work

- Product implementation begins only after this specification is reviewed and
  merged.
- No Windows, Access, browser, database, network service, or customer fixture
  is required for this runway.
- No provider/package-specific mapper integration is authorized by this spec.
