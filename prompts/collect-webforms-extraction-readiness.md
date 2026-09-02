# Collect Web Forms extraction readiness

```text
Perform one private, read-only Web Forms extraction-readiness review using the
existing TraceMap interaction-run artifacts. This is a Web Forms investigation;
do not redirect the review toward Angular, TypeScript, Razor, or WinForms.

Repository instructions:

1. Confirm this checkout is at the exact owner-approved TraceMap revision.
   Do not substitute an experimental branch or a newer revision without owner
   approval.
2. Read `prompts/README.md`, `docs/MIGRATION_EXTRACTION_TRIAGE.md`, and
   `docs/examples/tracemap-migration-extraction-summary.json` completely.
3. Ask the local owner for the exact existing interaction output directory if
   it is not already present in the task context. Do not search unrelated
   directories or drives for it.
4. Review only these existing artifacts beneath that authorized directory:
   - `interaction-run-result.json`
   - `feedback-summary.json`
   - each .NET source's `scan-manifest.json`
   - each .NET source's `facts.ndjson`
5. Do not read source files, configuration contents, SQLite indexes, raw
   reports, analyzer logs, generated documentation, or application data.
6. Do not rescan, build, restore dependencies, or modify any repository.

For each .NET source, sort internally by its private source label and replace
the label with a run-local ordinal: `scope-001`, `scope-002`, and so on. The
ordinal is not an identity and must not be reused as provenance.

Return these fields for each opaque scope:

- `scopeId`
- `analysisLevel`
- `buildStatus`
- fact counts for each exact Web Forms rule:
  - `legacy.webforms.inventory.v1`
  - `legacy.webforms.composition.v1`
  - `legacy.webforms.event-binding.v1`
  - `legacy.webforms.handler-resolution.v1`
  - `legacy.webforms.designer-control.v1`
  - `legacy.webforms.event-flow.v1`
  - `legacy.webforms.logic-signal.v1`
- fact counts for each exact classic ASP.NET rule:
  - `legacy.aspnet.surface.v1`
  - `legacy.aspnet.route.v1`
  - `legacy.aspnet.config.v1`
  - `legacy.aspnet.handler.v1`
  - `legacy.aspnet.page-method.v1`
  - `legacy.aspnet.navigation.v1`
  - `legacy.aspnet.identity-state.v1`
- `AnalysisGap` counts grouped only by those rule IDs
- capability state and coverage effect for these exact capability codes when
  present in `AnalyzerCapabilityDiagnostic` facts:
  - `CSharpSemanticCompilation`
  - `MSBuildProjectLoad`
  - `ReferenceAssemblyResolution`
  - `SyntaxFallbackAvailable`
  - `GeneratedDesignerLinkage`
  - `LegacyWebStackShape`
  - `DownstreamNoEvidenceCoverage`
- count of facts under `legacy.winforms.event-binding.v1`, labeled strictly as
  an adjacent diagnostic that is not Web Forms evidence
- `webFormsReadiness`, using exactly one value:
  - `flow-evidence-available`
  - `handlers-resolved-no-flow-evidence`
  - `bindings-declared-handlers-unresolved`
  - `inventory-only`
  - `webforms-surface-with-reduced-coverage`
  - `no-webforms-evidence-observed`
- `nextPrerequisite`, using exactly one value:
  - `restore-project-load`
  - `restore-reference-assemblies`
  - `restore-semantic-compilation`
  - `review-generated-designer-linkage`
  - `verify-webforms-scope-selection`
  - `rerun-webforms-extraction-after-prerequisites`
  - `review-handler-resolution-gaps`
  - `review-event-flow-input-coverage`
  - `none-observed`

Use fact type as well as rule ID when calculating progress. Analysis gaps do
not count as positive inventory, binding, handler, or flow evidence.

Classification rules:

- `WebFormsEventFlowProjected` positive facts => `flow-evidence-available`,
  reduced if the manifest or relevant capabilities are reduced/unavailable.
- Positive `WebFormsHandlerResolved` with no flow facts =>
  `handlers-resolved-no-flow-evidence`.
- Positive `WebFormsEventBindingDeclared` with no resolved handler =>
  `bindings-declared-handlers-unresolved`.
- Positive page/control/composition/designer facts with no binding facts =>
  `inventory-only`.
- Any Web Forms surface evidence whose trustworthy interpretation is blocked
  by reduced/unavailable capabilities => `webforms-surface-with-reduced-coverage`.
- Zero positive Web Forms and classic ASP.NET surface facts =>
  `no-webforms-evidence-observed`; this is not proof that the source is not a
  Web Forms application.

Choose prerequisites from proven capability state and rule-backed gaps. Do not
infer a missing package, framework version, project type, or source defect from
counts alone.

Return aggregate totals and answer only these questions:

1. How many scopes contain Web Forms page/control inventory?
2. How many contain declared event bindings?
3. How many contain resolved handlers?
4. How many contain event-flow projections?
5. Which capability or rule-backed gap most often blocks the next stage?
6. Did WinForms diagnostics appear in scopes that also contain positive Web
   Forms inventory, and if so, does the evidence prove a classification defect?
7. What is the single smallest repository-side diagnostic or extraction patch
   that would make the next private run more informative?

Privacy boundary:

- Do not return source labels, repository identities, paths, filenames,
  project names, run IDs, commit SHAs, page/control/handler names, routes, URLs,
  symbols, source values, diagnostic messages, hashes derived from private
  values, SQL, configuration values, credentials, connection material, or logs.
- Do not quote raw NDJSON rows.
- Do not infer runtime event firing, navigation, reachability, business
  capability, correctness, migration parity, or completeness.
- Do not commit, push, publish, or open a pull request.

Before responding, search the proposed response for every private source label,
repository name, path fragment, run ID, and commit SHA visible in the reviewed
artifacts. If any match remains, remove it and repeat the check.

Return one JSON object capped at 16 KB and a Markdown summary capped at 450
words. If the authorized artifacts are unavailable or the requested fields
cannot be derived without prohibited material, return only:

webforms-extraction-readiness=boundary-stop;reason=<categorical-reason>
```
