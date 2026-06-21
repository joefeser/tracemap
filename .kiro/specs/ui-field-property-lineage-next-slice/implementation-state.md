# UI Field and Property Lineage Next Slice Implementation State

Status: implementation-slice-ready-for-pr

## Current Branch

`codex/implement-ui-field-property-lineage-next-slice`

## Selected Slice

PR 1: Model-Binding And Property Identity Join.

This slice implements deterministic static model-binding target facts and
report-layer property identity joins. It does not implement PR 2 downstream
static composition, optional browser/computer-use evidence, runtime proof,
reflection/DI solving, branch feasibility, LLM calls, embeddings, vector
databases, or prompt-based classification.

## Implemented

- Strengthened syntax `PropertyDeclared` metadata with containing type,
  declared type, qualified target symbol, property name, and model family.
- Added `RazorModelBindingTarget` facts under
  `csharp.razor.model-binding.v1` for:
  - MVC action parameters where parameter DTO/model properties are statically
    visible in the same syntax unit.
  - Razor Page handler parameters where properties are statically visible in
    the same syntax unit.
  - `[BindProperty]`, page model, and view-model property targets.
- Kept server-only `RazorModelBindingTarget` facts out of `field:`,
  `control:`, and `binding:` root selection unless separate UI/form facts are
  present.
- Added report-layer derived paths for:
  - Razor binding to model/view-model property identity.
  - Razor form target to action/handler model-binding target.
  - Angular event/control payload fields through existing object-shape,
    HTTP-call, endpoint route, and model-binding evidence.
- Added review-tier gaps for `SameNameOnlyPropertyMatch`,
  `PropertyIdentityUnavailable`, `EndpointAlignmentUnavailable`, and
  `GenericPropertyFanOut`.
- Added Tier4 `RazorBindingGap` evidence for model-binding parameter types that
  cannot be expanded by same-file syntax fallback.
- Preserved additive report version `1.0`; new rows and metadata stay inside
  existing arrays/objects.

## Scope Decisions And Limitations

- No new TypeScript scanner fact types were added. PR 1 TypeScript fixtures use
  existing Angular event, object-shape, and HTTP-call fact families.
- C# model-binding target extraction is syntax-backed and conservative.
  Cross-file action-parameter-to-property expansion remains a follow-up; syntax
  fallback emits an explicit `cross-file-parameter-type` `RazorBindingGap`
  instead of choosing hidden target winners.
- Same-name-only joins remain `NeedsReviewLineage`; exact type/fact/symbol or
  endpoint/model-binding evidence is required for stronger classification.
- Dynamic model-binding gap expansion and alias-as-supporting-metadata behavior
  are deferred follow-ups. This slice does not use alias evidence to promote
  classifications or choose hidden winners.
- Route-flow-specific downstream traversal remains deferred to PR 2.
- JVM and Python adapter smokes were deferred because this slice did not modify
  JVM or Python adapters.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "PropertyFlowTests|CSharpSyntaxExtractorTests"`: passed, 17 tests.
- `npm install --prefix src/typescript`: completed to install pinned local test dependencies.
- `npm run check --prefix src/typescript`: passed, 7 files / 29 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 568 tests, with the same existing NU1903 warning.
- CLI sample smoke over `samples/endpoint-server-aspnet` and
  `samples/endpoint-client-angular`: scan/combine/property-flow completed,
  produced `property-flow-report.md` and `property-flow-report.json`, and
  reported reduced coverage because the public endpoint client sample has HTTP
  evidence but no Angular template binding root facts.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Review Status

- Kiro implementation review completed with full coverage:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T234639-174Z-implementation-claude-sonnet-4.6.clean.md`.
- Initial review findings were bookkeeping/spec-scope issues. Patched:
  - Moved dynamic model-binding gap expansion from PR 1 required tasks to
    Deferred Follow-Ups.
  - Moved alias-as-supporting-metadata behavior and alias-specific tests to
    Deferred Follow-Ups because this slice does not consume alias evidence.
  - Marked completed parent tasks only where all remaining PR 1 bullets are
    complete.
- Kiro re-review cycle 1 completed with full coverage:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T234926-413Z-re-review-claude-sonnet-4.6.clean.md`.
  Patched remaining spec/test precision issues, including implementation-PR
  framing, fan-out threshold documentation, `propertyType` design metadata, and
  additional selector/family/gap tests.
- Kiro re-review cycle 2 completed with reduced coverage because Kiro reported
  denied tool access:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T235456-592Z-re-review-claude-sonnet-4.6.clean.md`.
  Patched the blocking findings:
  - Aligned production `PropertyDeclared.targetSymbol` with synthetic tests by
    using qualified `Type.Property` symbols when a containing type exists.
  - Added rule-catalog limitations for convention-based ViewModel/InputModel/
    FormModel model-binding facts and same-file syntax fallback boundaries.
  - Emitted `RazorBindingGap` for likely model-binding parameter types whose
    properties cannot be expanded from same-file syntax.
  Also patched the closely related Razor Pages handler route-to-model-binding
  join and added focused coverage.

## PR Status

Prior spec-only PR work was completed before this branch. The current
implementation PR is ready to open after final validation, commit, and push.

## Follow-Ups

- Expand model-binding target extraction across files when semantic or
  deterministic project-wide syntax metadata can support it safely.
- Add deeper mapper/projection, validation/read/write, service/repository,
  query/data/entity, and dependency-surface property hops in later slices.
- Add PR 2 route-flow/path/reverse/data/dependency composition only where
  existing combined evidence exposes a property-specific trail.
