# UI Field and Property Lineage Tasks

## Spec-Only PR Scope

- [x] Add Kiro spec files under `.kiro/specs/ui-field-property-lineage/`.
- [x] Keep this PR limited to the new spec folder.
- [x] Capture issue #165 and related issue #159 as source material in `implementation-state.md`.
- [ ] Land the spec PR into `dev`.

## Implementation Tasks

- [ ] 1. Confirm current evidence inventory. Requirements: 1, 3, 4, 5, 6, 7.
  - [ ] Inventory existing Angular/TypeScript HTTP call, payload, object-shape, value-origin, call-edge, and coverage-gap facts.
  - [ ] Inventory existing C#/Razor/cshtml support and identify whether Razor binding extraction is new or partially available.
  - [ ] Inventory DTO/model/property facts, mapping/projection evidence, validation/read/write evidence, query patterns, data/entity surfaces, and dependency surfaces.
  - [ ] Inventory combined path, reverse path, route-centered flow, endpoint alignment, and vault export model reuse points.
  - [ ] Document which hops can be supported by existing facts and which need new scanner rules.
  - [ ] Record the inventory findings in `implementation-state.md` under an Evidence Inventory section before closing this task.

- [ ] 2. Define rule catalog entries and limitations. Requirements: 3, 5, 8, 13.
  - [ ] Add or update rule IDs for Angular template/form/event/template-variable facts before emitting them.
  - [ ] Add or update rule IDs for Razor binding/form-target/model-binding facts before emitting them.
  - [ ] Add derived `property-flow.*.v1` or equivalent rule IDs for roots, edges, paths, selector gaps, coverage gaps, schema gaps, truncation, and optional observed evidence.
  - [ ] Document limitations for runtime visibility, auth/role/feature flags, branch feasibility, runtime DI, reflection, serializer behavior, browser observation, dynamic UI, and redaction.

- [ ] 3. Add Angular template and form extraction. Requirements: 3, 4, 14.
  - [ ] Extract interpolation, property binding, event binding, and two-way binding facts.
  - [ ] Extract reactive form evidence for `formControlName`, `formGroup`, `formArrayName`, and static form control construction.
  - [ ] Extract template-driven form evidence for `name`, `ngModel`, and static template variables.
  - [ ] Connect external templates to component classes through static `templateUrl` evidence.
  - [ ] Emit gaps for dynamic expressions, unsupported custom directive inputs, ambiguous template variables, and unresolved external templates.
  - [ ] Add public-safe Angular fixture tests for supported and downgraded patterns.

- [ ] 4. Add Razor/MVC/Pages binding extraction. Requirements: 5, 6, 14.
  - [ ] Extract `asp-for` evidence on input/select/textarea/label/validation elements.
  - [ ] Extract `Html.*For` helper model-expression evidence where static.
  - [ ] Extract static form target metadata from `asp-action`, `asp-controller`, `asp-page`, `asp-page-handler`, and method attributes.
  - [ ] Connect form targets to MVC actions or Razor Page handlers where static evidence supports it.
  - [ ] Extract model-binding targets from action parameters, `[FromBody]`, `[FromForm]`, `[BindProperty]`, page models, and view models where available.
  - [ ] Emit gaps for ViewBag/ViewData, dynamic models, partial/template ambiguity, custom tag helpers, and generated Razor gaps.
  - [ ] Add public-safe Razor/cshtml fixture tests for supported and downgraded patterns.

- [ ] 5. Build property-flow selector and graph models. Requirements: 1, 2, 7, 8, 10.
  - [ ] Implement selector parsing for `field:`, `control:`, `binding:`, `model:`, `dto:`, `symbol:`, and `fact:`.
  - [ ] Add source and framework filters.
  - [ ] Add generic property-name downgrade behavior.
  - [ ] Define root, node, edge, path, gap, inventory, source, snapshot, and limitation models.
  - [ ] Preserve source labels, source index IDs, scan IDs, commit SHAs, rule IDs, tiers, file spans, extractor IDs/versions, supporting fact IDs, and supporting edge IDs.
  - [ ] Reject unsafe selectors and sanitize diagnostics.

- [ ] 6. Compose downstream lineage evidence. Requirements: 4, 6, 7, 8.
  - [ ] Connect template/control roots to TypeScript component members when static evidence supports it.
  - [ ] Connect event bindings to handlers and handlers to payload construction through call/value-origin evidence.
  - [ ] Connect payload fields to HTTP calls and endpoint alignment evidence.
  - [ ] Connect server endpoints to action/handler parameters and DTO/model properties.
  - [ ] Connect DTO/model properties through mapping/projection/manual assignment evidence.
  - [ ] Connect validation reads, service/repository calls, query patterns, and data/entity surfaces.
  - [ ] Reuse route-flow from issue #159 and combined path/reverse helpers where available; before #159 lands, emit `RouteFlowUnavailable` gaps as the default server-internal traversal behavior.
  - [ ] Emit gaps rather than paths for missing route-flow, missing schema, ambiguous endpoint, dynamic dispatch, runtime DI, reflection, serializer mapping, callbacks, mutation, and branch feasibility.

- [ ] 7. Add deterministic traversal and classification. Requirements: 7, 8.
  - [ ] Implement bounded deterministic traversal with documented root, depth, path, frontier, and gap caps.
  - [ ] Sort roots, edges, paths, gaps, inventory rows, and metadata deterministically.
  - [ ] Classify `StrongStaticLineage`, `ProbableStaticLineage`, `NeedsReviewLineage`, `UnknownAnalysisGap`, `NoLineageEvidence`, `SelectorNoMatch`, and `TruncatedByLimit`.
  - [ ] Derive confidence from classification through a fixed mapping.
  - [ ] Ensure reduced coverage downgrades no-lineage conclusions to `UnknownAnalysisGap`.
  - [ ] Ensure cross-source traversal only occurs through documented combined evidence.

- [ ] 8. Add Markdown and JSON output. Requirements: 9, 10, 12.
  - [ ] Emit Markdown sections in the documented order.
  - [ ] Emit JSON top-level shape `property-flow` version `1.0`.
  - [ ] Include selected roots, lineage paths, gaps, inventory, optional observed evidence, and limitations.
  - [ ] Use `null` and empty arrays consistently for missing values.
  - [ ] Escape Markdown table/link characters.
  - [ ] Suppress or hash raw SQL, snippets, raw remotes, local absolute paths, raw URLs, connection strings, secrets, credentials, and private data.
  - [ ] Prove Markdown and JSON byte stability for identical inputs.
  - [ ] Cover non-combined-index rejection, required-versus-optional table behavior, route-flow-unavailable gaps, source/framework filters, top-N ambiguity, truncation, confidence mapping, cross-source boundary rendering, and full-versus-reduced no-lineage classifications.

- [ ] 9. Add optional browser-observed evidence support only after core static reporting. Requirements: 11, 14.
  - [ ] Keep browser/computer-use capture outside the core deterministic scanner/reporting requirement.
  - [ ] Label observed DOM evidence as demo/validation metadata only.
  - [ ] Reject workflows that require production login, credentials, private data capture, or live secret-bearing HTTP.
  - [ ] Add tests proving observed evidence cannot upgrade static classifications by itself.

- [ ] 10. Wire CLI and docs. Requirements: 1, 12, 13, 14.
  - [ ] Add `tracemap property-flow --help`.
  - [ ] Document selector forms, output files, classifications, coverage semantics, safety rules, and limitations.
  - [ ] Update `docs/ACCEPTANCE.md` and `docs/VALIDATION.md`.
  - [ ] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` if new shared UI/property roles are introduced.
  - [ ] Keep public copy bounded to deterministic static evidence claims.

- [ ] 11. Validate implementation. Requirements: 14.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run TypeScript adapter tests and build when Angular extraction changes.
  - [ ] Run Razor/.NET adapter tests when Razor extraction changes.
  - [ ] Run relevant combined path/reverse/report tests when graph composition changes.
  - [ ] Run pinned smoke checks required by `docs/VALIDATION.md` for changed adapters.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Recommended PR Slices

- PR 1: Evidence inventory, rule catalog plan, models, selector parser, and public-safe fixtures with pending expected outputs.
- PR 2: Angular template/form extraction and tests.
- PR 3: Razor/MVC/Pages binding extraction and tests.
- PR 4: Property-flow graph composition, traversal, classifications, and CLI.
- PR 5: Markdown/JSON output hardening, safety validation, byte-stability, and documentation.
- PR 6: Optional browser-observed evidence capture for demos, if still needed after static reporting works.

## Deferred Follow-Ups

- HTML or interactive graph viewer.
- Persisted derived property-flow rows behind an explicit write mode.
- Whole-application default property inventory query.
- Advanced Angular directive, pipe, and structural directive semantics.
- Deep serializer contract expansion.
- Runtime DI container solving.
- Branch feasibility or symbolic execution.
- Full taint analysis.
- Runtime browser or live HTTP proof.
- Production login or credentialed capture.
- LLM classification, embeddings, vector databases, or prompt-based analysis.
