# UI Field and Property Lineage Implementation State

Status: implementation-slice-in-progress

## Current Branch

`codex/implement-ui-field-property-lineage`

## Current Slice

This slice implements a deterministic v1 of `tracemap property-flow` over
combined indexes, adds Angular template/form source facts in the TypeScript
adapter, adds Razor binding/form-target source facts in the .NET adapter, adds
rule catalog entries and limitations, and updates validation/docs for the new
UI/property evidence families.

The implementation remains static-evidence only. It does not add LLM calls,
embeddings, vector databases, prompt-based classification, live browser
requirements, live HTTP proof, DB connections, credential capture, or runtime
claims.

## Source Material

- Issue #165: UI field/property lineage from visible UI fields, controls,
  template bindings, Razor helpers, model/DTO properties, and supported
  downstream static evidence.
- Related issue #159: route-centered static flow. Current repo state includes a
  route-flow report implementation, but `property-flow` still emits a
  `RouteFlowUnavailable` schema gap unless a concrete route-flow schema signal
  such as `combined_route_flow_edges` is present.

## Evidence Inventory

- TypeScript/Angular before this slice already emitted HTTP client call facts,
  normalized route metadata, object-shape facts, argument/value-origin facts,
  call edges, local aliases, query/config/package facts, and reduced-coverage
  gaps.
- TypeScript/Angular new in this slice emits `UiTemplateBinding`,
  `UiFormControlBinding`, `UiEventBinding`, `UiTemplateVariable`, and
  `UiBindingGap` facts under `typescript.angular.*.v1` rules. It supports
  interpolation, property binding, event binding, two-way binding,
  `formControlName`, `formGroup`, `formArrayName`, template-driven `name` plus
  `ngModel`, template variables, external `templateUrl`, and inline templates.
  Dynamic expressions are stored as gap facts with hashes rather than snippets.
- .NET before this slice had C# syntax/semantic declarations, property access,
  call edges, argument flow, parameter forwarding, object creation,
  ASP.NET route facts, query/data/dependency surfaces, legacy WebForms flow
  evidence, and combined path/route-flow/reverse/report/vault reuse points.
- Razor/cshtml support was new for this slice. It now emits `RazorBinding`,
  `RazorFormTarget`, and `RazorBindingGap` facts for `asp-for`, `Html.*For`,
  static form target attributes, and dynamic Razor model/view-data/partial
  gaps.
- Combined path and route-flow reporters are reused as read-only report-layer
  evidence sources. `property-flow` does not mutate `endpoint_matches`, source
  indexes, source repositories, or derived tables.
- DTO/model/property, mapping/projection, validation/read/write,
  service/repository, query/data/entity, and dependency surfaces are consumed
  where existing combined path graph evidence exposes them. Dedicated
  property-to-property mapper/projection and Razor model-binding target links
  remain follow-up work.

## Scope Decisions

- `tracemap property-flow --index <combined.sqlite> --property <selector>
  --out <path>` is a combined-index report/query command.
- Supported selector prefixes are `field:`, `control:`, `binding:`, `model:`,
  `dto:`, `symbol:`, and `fact:`.
- `--source` is a case-insensitive exact source label filter.
- `--framework` supports `angular`, `razor`, and `any`, defaulting to `any`.
- Directory and extensionless outputs write `property-flow-report.md` and
  `property-flow-report.json`; explicit `.md` or `.json` paths write the
  compatible selected format.
- Generic property names such as `status` are allowed but downgraded to
  `NeedsReviewLineage` unless narrowed by source/type/symbol/fact identity.
- Missing optional schema produces `MissingOptionalSchema` gaps; route-flow
  schema absence produces `RouteFlowUnavailable`.
- Optional browser/computer-use evidence is not implemented in this slice and
  remains outside the core command.

## Oddities

- Route-flow code exists in the current checkout even though the public related
  issue is still open. The property-flow implementation treats route-flow
  schema availability as machine-checkable rather than issue-state-based.
- The TypeScript scanId stability test needed deterministic test commit dates
  so identical synthetic repositories produce identical commit SHAs.
- Razor model-binding target facts (`RazorModelBindingTarget`) have rule catalog
  entries reserved, but action-parameter/handler binding extraction is not
  implemented in this slice.

## Validation

- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests`
- `npm run check --prefix src/typescript`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

Python and JVM adapter smoke checks were deferred as not relevant for this
slice; no Python or JVM adapter code changed. The relevant TypeScript adapter
check and .NET/Razor tests were run.

## Kiro Review

- Initial implementation review completed with reduced coverage because the
  wrapper reported denied tool access for one shell command.
- Actionable findings patched:
  - Deduplicated property-flow start nodes by node ID.
  - Replaced overly broad display-name substring matching with bounded symbol
    matching.
  - Added focused tests for `fact:` selector output, Razor framework filtering,
    and explicit Markdown/JSON file outputs.
- One re-review cycle completed with reduced coverage for the same denied-tool
  condition. It reported no remaining merge-blocking implementation issues for
  the completed slice. Follow-ups remain documented for Razor model-binding
  target extraction and deeper property-specific downstream hops.
- Additional patch after re-review: Razor `@model` type metadata is now captured
  for `model:<type>.<property>` selector precision, with focused tests.

## PR Review Loop

- Initial PR loop returned `actionable_findings` with four unresolved Gemini
  review threads.
- Actionable findings patched:
  - Angular template extraction now supports bracketed
    `[formControlName]`, `[formGroup]`, and `[formArrayName]` bindings when the
    value is a static literal, and emits deterministic gap facts for dynamic
    names.
  - Razor `asp-for` extraction now normalizes before static validation and
    strips both leading `@` and `Model.` from static model property paths.
  - Property-flow bounded display-name matching now uses index scanning instead
    of dynamically constructing regular expressions per node comparison.
- Follow-up Qodo findings patched:
  - Property-flow edge and gap rows now expose structured line-span and
    extractor metadata, plus source/commit attribution where current evidence
    supports it.
  - Coverage warnings are now structured evidence rows with rule ID, evidence
    tier, extractor metadata, supporting source IDs, source labels, and commit
    SHAs rather than bare strings.
- Focused .NET property-flow tests, TypeScript adapter checks, full .NET build,
  full .NET tests, private-path guard, and whitespace checks passed after these
  patches.

## Follow-Ups

- Connect Razor form targets to MVC actions/Razor Page handlers.
- Emit `RazorModelBindingTarget` facts for action parameters,
  `[FromBody]`, `[FromForm]`, `[BindProperty]`, page models, and view models.
- Add stronger event-handler-to-payload and payload-field-to-HTTP property
  hops using direct assignment/value-origin evidence.
- Add DTO/model property mapping through manual assignment, object initializer,
  projection, and AutoMapper-like evidence where rule-backed facts exist.
- Add deeper validation/read/write, service/repository, query/data/entity, and
  dependency surface property hops.
- Add optional observed/browser metadata as demo-only evidence in a future
  opt-in workflow, without upgrading static classifications.

## Blockers

None for the implemented v1 slice. Remaining items above are scoped follow-ups,
not blockers for the current deterministic report and source-fact slice.
