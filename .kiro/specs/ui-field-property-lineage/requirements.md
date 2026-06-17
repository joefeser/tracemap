# UI Field and Property Lineage Requirements

## Introduction

TraceMap should be able to answer a static evidence question that starts from a
visible UI field, form control, template binding, Razor helper, or bound model
property and follows where that field or property is read, written, validated,
mapped, sent, bound, and used downstream.

This extends the route-centered static flow work proposed in issue #159. The
route-centered flow starts from an HTTP route or client call. UI field/property
lineage starts one layer earlier: a field, binding, form control, component or
view-model property, DTO/model property, or static selector for that property.

The feature is deterministic static analysis. It does not prove that a field is
visible at runtime, visible to every user, reachable under every app state,
authorized for a role, enabled by a feature flag, submitted over a live network,
or executed in production.

## Scope

In scope:

- Angular template binding evidence, including interpolation, property binding,
  event binding, two-way binding, reactive forms, template-driven forms,
  `formControlName`, form groups, and template variables where static.
- Angular/TypeScript component or service property evidence, HTTP client call
  evidence, payload object shapes, request body/query/route parameter evidence,
  and static validation branches where available.
- Razor/MVC/Pages binding evidence, including `asp-for`, `Html.*For`, tag
  helpers, form actions, handlers/actions, and model-binding targets where
  static.
- Server DTO/model parameters and properties, controller/action usage, mapper or
  projection evidence, service/repository calls, query patterns, entity/data
  surfaces, and dependency surfaces where existing facts support a path.
- Composition with existing and planned evidence families: Angular/TypeScript
  facts, Razor/cshtml facts, HTTP client calls, endpoint alignment,
  route-centered flow from issue #159, `call_edges`, `symbol_relationships`,
  parameter/value-origin facts, object/projection shapes, query patterns,
  DTO/model/property facts, data/dependency surfaces, combined paths, reverse
  paths, and vault/evidence graph export.
- Markdown and JSON reports with rule IDs, evidence tiers, file spans, source
  labels, commit SHAs, extractor IDs/versions, supporting IDs, coverage labels,
  gaps, and limitations.

Out of scope:

- No runtime visibility proof.
- No proof that a field is visible to every user or app state.
- No auth, role, or feature-flag proof without static evidence, and static
  evidence remains a limitation rather than runtime proof.
- No branch feasibility, runtime dependency injection certainty, serializer
  runtime certainty, live browser requirement, live HTTP requirement, production
  login, or credential use.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  core scanner, composition, or reporting logic.
- No raw source snippets, raw SQL, raw remotes, connection strings, secrets,
  local absolute paths, captured credentials, private data, or unsafe literal
  values in default reports.

## Requirements

### Requirement 1: Property Lineage Command

**User Story:** As a reviewer, I want a command that starts from a UI field or
bound property selector so I can inspect the static evidence TraceMap has for
where that property is used downstream.

#### Acceptance Criteria

1. WHEN the user runs `tracemap property-flow --index <combined.sqlite> --property <selector> --out <path> [--max-roots <n>] [--max-depth <n>] [--max-paths <n>] [--max-frontier <n>] [--max-inventory <n>] [--max-gaps <n>]` THEN TraceMap SHALL read a combined index and emit a property-lineage report.
2. WHEN `--format json` is provided with a file output THEN TraceMap SHALL emit machine-readable JSON.
3. WHEN the output path is an existing directory or a path without an extension THEN TraceMap SHALL write `property-flow-report.md` and `property-flow-report.json`.
4. WHEN the input is not a combined index THEN TraceMap SHALL fail with a clear schema error and SHALL NOT silently treat a single-language index as fully lineage-queryable.
5. WHEN required combined tables are missing THEN the command SHALL emit schema gaps or fail with a sanitized missing-table error according to whether the table is optional or required.
6. WHEN the command runs THEN it SHALL open the input database read-only and SHALL NOT mutate source indexes, source repositories, or derived tables.
7. WHEN the command completes THEN the CLI SHALL print output path, selected root count, path count, gap count, truncation state, and report coverage.
8. WHEN no selector is provided THEN the command SHALL fail with a clear usage error; there is no default whole-application property query in v1.
9. WHEN the output path is an explicit `.md` or `.json` file THEN TraceMap SHALL write only that format unless `--format` selects a compatible format; a file path without `--format` SHALL default to Markdown unless the extension is `.json`.
10. WHEN route-centered flow from issue #159 exists in the same combined index through a documented schema signal THEN property-flow MAY reuse route-flow edges after the property reaches an HTTP call or endpoint; it SHALL preserve the route-flow rule IDs as supporting evidence.
11. WHEN route-centered flow is unavailable THEN property-flow SHALL still report local UI, payload, endpoint, DTO/model, and surface evidence where present, plus an explicit route-flow-unavailable gap where downstream traversal would otherwise depend on #159.
12. WHEN endpoint alignment is needed inside a combined index THEN property-flow SHALL reuse the existing combined endpoint matching behavior over `combined_facts` and `index_sources` or a documented successor; it SHALL NOT require persisted `endpoint_matches` rows unless a future schema explicitly makes them authoritative.

### Requirement 2: Selectors

**User Story:** As an investigator, I want selector forms that can target UI
fields, template bindings, controls, and model properties without exposing raw
private source text.

#### Acceptance Criteria

1. WHEN `--property <selector>` is provided THEN TraceMap SHALL parse it as one of the documented selector kinds: `field:<name>`, `control:<name>`, `binding:<name>`, `model:<type>.<property>`, `dto:<type>.<property>`, `symbol:<id-or-display>`, or `fact:<combinedFactId>`.
2. WHEN `--source <label>` is provided THEN selector matching SHALL be limited to matching source labels using deterministic case-insensitive exact matching.
3. WHEN `--framework angular`, `--framework razor`, or `--framework any` is provided THEN selector matching SHALL constrain UI root evidence accordingly; default SHALL be `any`.
4. WHEN a selector is a generic property name such as `id`, `name`, `type`, `value`, `state`, or `status` without source/type context THEN TraceMap SHALL allow the query but classify roots no stronger than `NeedsReviewLineage`.
5. WHEN selectors match multiple roots THEN TraceMap SHALL report deterministic top-N roots, total candidate count, and ambiguity gaps rather than picking a hidden winner.
6. WHEN selectors match nothing THEN TraceMap SHALL emit a valid report with `SelectorNoMatch`.
7. WHEN a selector references a raw local absolute path, raw URL, secret-like value, or source snippet THEN TraceMap SHALL reject it with a sanitized diagnostic and SHALL NOT run a best-effort hashed query.
8. WHEN `fact:<combinedFactId>` is used THEN TraceMap SHALL resolve the value against `combined_facts.combined_fact_id` and SHALL preserve the selected fact's source label, scan ID, commit SHA, rule ID, evidence tier, extractor ID/version, file span, and safe metadata.
9. WHEN `model:<type>.<property>` is used THEN TraceMap SHALL match model/view-model property facts only; WHEN `dto:<type>.<property>` is used THEN TraceMap SHALL match DTO property facts only; evidence that belongs to both families SHALL be reported as an ambiguity rather than silently preferred.

### Requirement 3: Angular Template and Form Evidence

**User Story:** As a maintainer, I want Angular UI evidence represented as rule-backed facts so property lineage can start from templates and forms.

#### Acceptance Criteria

1. WHEN an Angular template contains interpolation such as `{{ user.name }}` THEN the scanner SHALL emit a template binding fact for the property path when static.
2. WHEN a template contains property binding such as `[value]`, `[disabled]`, `[ngModel]`, or `[formGroup]` THEN the scanner SHALL emit static binding evidence with binding kind, target name, expression hash/kind, property path when static, rule ID, tier, file span, commit SHA, and extractor version.
3. WHEN a template contains event binding such as `(change)`, `(input)`, `(click)`, or `(ngSubmit)` THEN the scanner SHALL emit event binding evidence that connects the event to a handler expression when static.
4. WHEN two-way binding such as `[(ngModel)]` is present THEN the scanner SHALL represent both read and write directions as lineage-capable evidence while preserving that this is template binding evidence, not runtime mutation proof.
5. WHEN reactive forms use `formControlName`, `formGroup`, `formArrayName`, or `FormControl`/`FormGroup` construction THEN the scanner SHALL emit control-name and group-shape evidence where static.
6. WHEN template-driven forms use `name`, `ngModel`, or template references such as `#email="ngModel"` THEN the scanner SHALL emit control identity evidence where static.
7. WHEN template variables, local refs, or structural directives make a property path ambiguous THEN TraceMap SHALL emit `NeedsReview` or `AnalysisGap` evidence rather than inventing a property path.
8. WHEN external templates are referenced from a TypeScript component THEN TraceMap SHALL connect the template file to the component class only when the static `templateUrl` or inline template evidence is available.
9. WHEN template expressions call arbitrary functions, pipes, bracket notation, safe-navigation chains, dynamic property names, or custom directive inputs that cannot be resolved statically THEN TraceMap SHALL preserve available syntax evidence but downgrade lineage classification.
10. WHEN raw template snippets would be persisted THEN the scanner SHALL store binding kind, names, spans, expression hashes, and safe property paths only; raw snippets remain behind a future explicit raw-snippet option.

### Requirement 4: TypeScript Class, Payload, and HTTP Evidence

**User Story:** As a reviewer, I want property roots in Angular or TypeScript to connect to component members, service payloads, and HTTP calls when static evidence supports it.

#### Acceptance Criteria

1. WHEN a template property path maps to a TypeScript class member on the owning component THEN TraceMap SHALL connect the template fact to the member symbol with a rule-backed edge.
2. WHEN a template event handler calls a component method THEN TraceMap SHALL connect the event binding to the method symbol where compiler or syntax evidence supports the handler expression.
3. WHEN a component or service constructs an object literal used as an HTTP body THEN TraceMap SHALL emit object-shape evidence for visible property names, shape hash, source symbol/containing method, and safe field metadata.
4. WHEN a UI property is assigned into a payload field with matching or mapped property names THEN TraceMap SHALL require direct assignment, alias, or value-origin evidence before reporting a lineage hop.
5. WHEN an HTTP client call includes body, query, route, or header-like parameters THEN TraceMap SHALL preserve existing `HttpCallDetected`, route normalization, body field names, query names, parameter/value-origin evidence, and gaps.
6. WHEN property names are connected only by same-name matching across object shapes THEN TraceMap SHALL classify the hop as `NeedsReviewLineage` unless a stronger rule proves the mapping.
7. WHEN TypeScript compiler symbols are unavailable THEN syntax fallback MAY emit useful Tier3 evidence and SHALL mark source coverage reduced.
8. WHEN a property path crosses callbacks, observables, promises, pipes, subscriptions, destructuring, spread, or dynamic indexing THEN TraceMap SHALL stop or downgrade with a specific boundary/gap code unless a dedicated deterministic rule supports the hop.

### Requirement 5: Razor, MVC, and Pages Evidence

**User Story:** As a maintainer, I want Razor and MVC field evidence to seed property lineage without requiring runtime rendering.

#### Acceptance Criteria

1. WHEN a `.cshtml` file contains `asp-for` on input, select, textarea, label, validation, or custom tag-helper-like elements THEN the scanner SHALL emit Razor binding evidence with model/property path, element/control kind, rule ID, evidence tier, file span, source label, commit SHA, and extractor version.
2. WHEN Razor uses `Html.TextBoxFor`, `Html.EditorFor`, `Html.DisplayFor`, `Html.LabelFor`, `Html.ValidationMessageFor`, or related `Html.*For` helpers THEN the scanner SHALL emit model expression evidence where static.
3. WHEN a Razor form has `asp-action`, `asp-controller`, `asp-page`, `asp-page-handler`, `method`, or equivalent static form-action metadata THEN TraceMap SHALL emit form target evidence and connect it to endpoint/handler facts where static.
4. WHEN Razor Pages handlers such as `OnPost`, `OnPostSave`, or handler attributes are statically visible THEN TraceMap SHALL connect form handler evidence to handler/action symbols where supported.
5. WHEN MVC model-binding targets are visible from action parameters, `[FromBody]`, `[FromForm]`, `[BindProperty]`, page models, or view models THEN TraceMap SHALL emit model-binding target evidence with safe type/property metadata.
6. WHEN `ViewBag`, `ViewData`, dynamic model usage, partials, editor templates, display templates, custom tag helpers, or runtime-generated forms prevent static model-property identity THEN TraceMap SHALL emit review-tier evidence or gaps.
7. WHEN Razor syntax or generated C# cannot be semantically resolved THEN syntax fallback SHALL still emit safe Tier3 evidence where possible and mark coverage reduced.
8. WHEN raw HTML, source snippets, form values, route values, anti-forgery tokens, or private URLs would be stored THEN reports SHALL omit or hash unsafe values by default.

### Requirement 6: Server DTO, Model, Mapping, and Validation Evidence

**User Story:** As an API reviewer, I want UI property evidence to connect to server-side model properties, validation, mapping, and downstream use only when facts support the chain.

#### Acceptance Criteria

1. WHEN combined endpoint matching connects an HTTP client payload to a server action/handler inside the combined index THEN property-flow SHALL attempt to match payload fields to server body/form/route/query model targets using existing endpoint, DTO, model, and parameter evidence.
2. WHEN server DTO or model property facts exist THEN property-flow SHALL preserve containing type identity, property/member name, declared type, nullability/required metadata, alias metadata when indexed, rule ID, tier, file span, commit SHA, and extractor version.
3. WHEN model-binding is inferred only from framework convention or syntax-only evidence THEN property-flow SHALL classify the hop no stronger than `NeedsReviewLineage`.
4. WHEN AutoMapper, projection, manual assignment, object initializer, constructor mapping, or serializer alias evidence maps one property to another THEN property-flow SHALL require rule-backed mapping/object-shape evidence and preserve limitations.
5. WHEN validation attributes, fluent validation, branch checks, guard clauses, or conditional logic read a property THEN TraceMap SHALL emit or consume validation/read evidence as a lineage hop, but SHALL NOT claim branch feasibility or runtime validation outcome.
6. WHEN service/repository calls use a DTO/model property as an argument or filter THEN property-flow SHALL connect through call edges and parameter/value-origin evidence where available.
7. WHEN query patterns, data/entity surfaces, persistence calls, SQL shape evidence, or DbSet-like properties are reached THEN the report SHALL show the terminal surface evidence and limitations.
8. WHEN property flow depends on runtime DI, reflection, dynamic dispatch, serializer runtime configuration, generated code, partial classes not loaded, branch feasibility, or mutation semantics THEN TraceMap SHALL emit a gap or downgrade the path.
9. WHEN route-centered flow from issue #159 has not landed or is not present in the input schema THEN server-internal traversal beyond endpoint/model-binding evidence SHALL be labeled as `UnknownAnalysisGap` or `NeedsReviewLineage`; v1 implementation MAY defer Requirements 6.4 through 6.8 to a depends-on-#159 slice while still reporting upstream UI-to-HTTP evidence.

### Requirement 7: Lineage Graph and Composition

**User Story:** As a platform engineer, I want property lineage to reuse existing TraceMap evidence graph concepts so it composes with paths, reverse queries, impact, and vault export.

#### Acceptance Criteria

1. WHEN the property-lineage graph is built THEN every node SHALL include stable node ID, node kind, source label, source index ID, scan ID, commit SHA, optional symbol/fact ID, display name, rule ID, evidence tier, file path, and line span where available.
2. WHEN an edge is built THEN it SHALL include stable edge ID, edge kind, from/to node IDs, rule ID, evidence tier, supporting fact IDs, supporting edge IDs, source label, commit SHA, extractor ID/version where available, file path, and line span.
3. WHEN a path crosses source indexes THEN it SHALL do so only through documented cross-source evidence such as combined endpoint matching, route-flow, combined path edges, or another explicit combined rule.
4. WHEN path search runs THEN it SHALL use deterministic bounded traversal with documented default caps for roots, paths, depth, frontier, inventory rows, and gaps.
5. WHEN no downstream path exists and any contributing source has reduced coverage, unknown commit SHA, schema gaps, or analysis gaps THEN TraceMap SHALL emit `UnknownAnalysisGap`, not proof of no lineage.
6. WHEN all contributing sources have credible full coverage and no path exists THEN TraceMap MAY emit `NoLineageEvidence`, with limitations and rule ID.
7. WHEN existing combined paths or reverse paths already establish part of the trail THEN property-flow SHALL include them as supporting evidence rather than recomputing incompatible semantics.
8. WHEN vault/evidence graph export consumes property-flow reports THEN report JSON SHALL contain stable IDs, safe display metadata, rule IDs, tiers, coverage labels, gaps, and limitations sufficient for export without raw source.

### Requirement 8: Classifications, Coverage, and Gaps

**User Story:** As a reviewer, I want lineage classifications that make evidence strength and uncertainty visible.

#### Acceptance Criteria

1. WHEN every hop is compiler-resolved or equivalent strong static evidence and source coverage is credible THEN TraceMap MAY classify a path as `StrongStaticLineage`.
2. WHEN the path uses structural evidence such as template binding, tag helpers, endpoint alignment, object shapes, or route-flow evidence without Tier3 ambiguity THEN TraceMap SHALL classify it as `ProbableStaticLineage`.
3. WHEN the path uses syntax-only, same-name, name-only, generic property, unresolved template, optional route, ambiguous endpoint, or fallback evidence THEN TraceMap SHALL classify it as `NeedsReviewLineage`.
4. WHEN gaps prevent a credible conclusion THEN TraceMap SHALL classify affected rows or results as `UnknownAnalysisGap`.
5. WHEN selectors match no roots THEN TraceMap SHALL emit `SelectorNoMatch`.
6. WHEN selectors match roots but no path is found under full credible coverage THEN TraceMap MAY emit `NoLineageEvidence`.
7. WHEN traversal caps are hit THEN TraceMap SHALL emit `TruncatedByLimit` and mark the report partial.
8. WHEN source coverage is reduced THEN no-lineage conclusions SHALL be coverage-relative and SHALL NOT be described as proof that the property is unused, unsubmitted, unvalidated, unmapped, or not persisted.
9. WHEN a path includes browser-observed evidence from an optional follow-up layer THEN the classification SHALL remain static-evidence based; observed DOM metadata SHALL be clearly labeled as demo/observed evidence only.
10. WHEN a classification, gap, or no-evidence conclusion is emitted THEN it SHALL carry the derived rule ID mapped in the design's classification-to-rule table, plus the source fact rule IDs where source evidence exists.

### Requirement 9: Markdown Report

**User Story:** As a human reviewer, I want a readable report that explains the property trail without unsafe values.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL appear in this order: Summary, Query, Sources and Coverage, Selected Roots, Lineage Paths, Gaps, Evidence Inventory, Optional Observed Evidence, Limitations.
2. WHEN a selected root is rendered THEN the report SHALL show selector kind, safe display name, source label, rule ID, evidence tier, file span, commit SHA, extractor version, classification, and ambiguity count where applicable.
3. WHEN a lineage path is rendered THEN the report SHALL show a numbered trail with one row per hop, including node kind, edge kind, source label, rule ID, evidence tier, file span, safe display name, and limitation code where applicable.
4. WHEN a path crosses a source boundary THEN Markdown SHALL visibly mark the source transition and cite the cross-source rule.
5. WHEN validation, mapping, query, or data/entity surfaces are rendered THEN Markdown SHALL include safe metadata such as property names, shape hashes, table/key hashes, or normalized path keys, but SHALL NOT show raw SQL, snippets, local absolute paths, raw remotes, secrets, or connection strings.
6. WHEN browser-observed DOM evidence is present THEN it SHALL appear in the optional observed evidence section with a warning that it is not core static proof.
7. WHEN rows are capped THEN Markdown SHALL include deterministic truncation notices with the cap kind.
8. WHEN no paths exist THEN Markdown SHALL explain whether the result is `SelectorNoMatch`, `NoLineageEvidence`, or `UnknownAnalysisGap`.

### Requirement 10: JSON Report Contract

**User Story:** As an automation author, I want stable JSON so property-flow results can feed CI, evidence packets, and vault export.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include top-level `reportType`, `version`, `reportCoverage`, `coverageWarnings`, `query`, `snapshot`, `summary`, `sources`, `selectedRoots`, `lineagePaths`, `gaps`, `inventory`, `observedEvidence`, and `limitations`.
2. WHEN query metadata is emitted THEN it SHALL include selector kind, normalized selector, source filter, framework filter, max roots, max paths, max depth, max frontier, max inventory rows, max gaps, and algorithm/version identifiers.
3. WHEN a selected root is emitted THEN it SHALL include stable root ID, root kind, classification, source label, source index ID, scan ID, commit SHA, combined fact ID, symbol ID, rule ID, evidence tier, file path, start line, end line, extractor ID/version, safe display metadata, supporting fact IDs, and limitations.
4. WHEN a path is emitted THEN it SHALL include `pathId`, `classification`, `confidence`, `length`, `startRootId`, `endNodeId`, `nodes`, `edges`, `supportingFactIds`, `supportingEdgeIds`, and structured notes; confidence SHALL be one of `High`, `Medium`, or `Low` derived from the fixed classification mapping.
5. WHEN a node is emitted THEN it SHALL include `nodeId`, `nodeKind`, `displayName`, `sourceIndexId`, `sourceLabel`, `scanId`, `commitSha`, `symbolId`, `combinedFactId`, `ruleId`, `evidenceTier`, `filePath`, `startLine`, `endLine`, and safe metadata.
6. WHEN an edge is emitted THEN it SHALL include `edgeId`, `edgeKind`, `fromNodeId`, `toNodeId`, `classification`, `ruleId`, `evidenceTier`, `supportingFactIds`, `supportingEdgeIds`, `supportingCombinedEdgeIds`, `filePath`, `startLine`, and `endLine`.
7. WHEN arrays or maps are emitted THEN ordering SHALL be deterministic and metadata keys SHALL be sorted.
8. WHEN data is missing THEN JSON SHALL use `null` or empty arrays consistently rather than omitting required fields.
9. WHEN raw input properties contain unsafe values THEN JSON SHALL omit, hash, or category-label those values and emit safety gaps where needed.
10. WHEN the JSON shape changes in a future version THEN the top-level `version` SHALL change.
11. WHEN `snapshot` is emitted THEN it SHALL include input kind, combined index identity hash when available, source count, source labels, scan IDs, commit SHAs or unknown markers, scanner/extractor version summaries, coverage summaries, and schema compatibility flags without generated timestamps or local absolute paths.

### Requirement 11: Browser/Computer-Use Follow-Up Boundary

**User Story:** As a demo operator, I want optional browser-observed evidence to help validate demos without weakening the deterministic static evidence model.

#### Acceptance Criteria

1. WHEN browser or computer-use capture is implemented THEN it SHALL be optional and SHALL NOT be required for `tracemap property-flow` core claims.
2. WHEN browser-observed DOM fields are captured THEN the output SHALL label them as observed demo/validation metadata, not rule-backed static facts unless a separate static source mapping rule supports the link.
3. WHEN observed DOM evidence is connected to source templates through sourcemaps or checked-in templates THEN the static source mapping SHALL carry rule IDs, tiers, file spans, commit SHA, and extractor/version metadata; the browser observation itself SHALL remain runtime observation metadata.
4. WHEN browser capture requires credentials, production login, live HTTP traffic, private data, or captured secrets THEN TraceMap SHALL reject the workflow for public/default artifacts.
5. WHEN browser observation disagrees with static evidence THEN reports SHALL show a validation discrepancy gap and SHALL NOT replace static facts with runtime observations.
6. WHEN no browser evidence exists THEN property-flow reports SHALL remain complete static reports without warnings beyond normal static limitations.
7. WHEN default CLI commands, public demo scripts, or public-safe validation scripts run THEN they SHALL NOT invoke browser/computer-use capture unless a future command adds an explicit opt-in flag outside the default property-flow path.

### Requirement 12: Safety and Redaction

**User Story:** As a project owner, I want property-lineage reports that are safe to share when produced from public-safe inputs.

#### Acceptance Criteria

1. WHEN reports render file paths THEN they SHALL use repository-relative safe paths or source labels and SHALL reject local absolute paths by default.
2. WHEN source metadata includes raw remotes, hostnames, usernames, branch names, local paths, or private repository names THEN public/default reports SHALL omit or hash unsafe values according to existing redaction policy.
3. WHEN facts contain raw SQL, source snippets, config values, connection strings, tokens, credentials, form values, raw URLs, or private data THEN reports SHALL omit or hash them by default.
4. WHEN source snippets are requested through a future explicit option THEN the option SHALL be local-only, visibly unsafe, and excluded from public-safe validation.
5. WHEN Markdown cells contain pipes, brackets, parentheses, line endings, or link-like text THEN the report SHALL escape them.
6. WHEN generated JSON or Markdown contains unsafe values detected by the private-path guard or equivalent validation THEN validation SHALL fail with sanitized diagnostics.
7. WHEN source labels themselves are unsafe THEN the user SHALL provide safe labels or the report SHALL hash/category-label them.

### Requirement 13: Rule Catalog and Limitations

**User Story:** As a maintainer, I want every lineage conclusion tied to a documented rule with limitations.

#### Acceptance Criteria

1. WHEN new scanner facts are emitted THEN their rule IDs SHALL be documented in `rules/rule-catalog.yml` before implementation merges.
2. WHEN property-flow derives roots, edges, paths, gaps, or classifications THEN derived rule IDs such as `property-flow.root.v1`, `property-flow.edge.v1`, `property-flow.path.v1`, `property-flow.coverage.v1`, `property-flow.selector.v1`, `property-flow.schema.v1`, `property-flow.truncation.v1`, and `property-flow.observed-evidence.v1` or documented equivalents SHALL be cataloged.
3. WHEN existing evidence is reused THEN property-flow SHALL preserve the source rule IDs and evidence tiers rather than replacing them with only derived rule IDs.
4. WHEN a rule is added or changed THEN its limitations SHALL explicitly cover static-only evidence, reduced coverage, dynamic UI/rendering, runtime visibility, auth/role/feature flags, branch feasibility, runtime DI, reflection, serializer behavior, browser observation limits, and safety/redaction as applicable.
5. WHEN no new source facts are added and a report only composes existing facts THEN implementation MAY add only derived report rule IDs, but SHALL document why scanner rule catalog changes are unnecessary.
6. WHEN this spec-only PR lands THEN it SHALL NOT add rule catalog entries; AC 1 through AC 5 are merge gates for future implementation slices that emit or derive those rules.

### Requirement 14: Tests and Validation

**User Story:** As a contributor, I want public-safe fixtures and validation that prove property lineage works without private sample names or paths.

These acceptance criteria are implementation validation requirements for future
product-code slices. The spec-only PR is gated only by the spec validation
criteria in AC 20.

#### Acceptance Criteria

1. Tests SHALL include a public-safe Angular fixture with interpolation, property binding, event binding, two-way binding, reactive forms, template-driven forms, `formControlName`, form groups, template variables, external templates, and an HTTP payload field.
2. Tests SHALL include a public-safe Razor/cshtml fixture with `asp-for`, `Html.*For`, tag helpers, form actions, handlers/actions, page model or MVC action binding, validation output, and model-binding targets.
3. Tests SHALL cover selector parsing for field, control, binding, model property, DTO property, symbol, and fact selectors.
4. Tests SHALL cover generic property-name downgrade for selectors such as `status`.
5. Tests SHALL cover template-to-TypeScript member connection, event handler connection, payload object-shape connection, and HTTP body/query/route parameter evidence.
6. Tests SHALL cover Razor form/property evidence connecting to server handler/action/model-binding targets.
7. Tests SHALL cover DTO/model property mapping through direct assignment, object initializer/projection, AutoMapper-like mapping evidence where supported, validation reads, service/repository calls, query patterns, and data/entity surfaces.
8. Tests SHALL prove dynamic template expressions, custom tag helpers, ViewBag/ViewData, dynamic URLs, reflection, runtime DI, serializer mapping gaps, callbacks, mutation, branch feasibility, and reduced coverage downgrade or gap behavior.
9. Tests SHALL prove no raw SQL, raw snippets, raw remotes, local absolute paths, connection strings, secrets, credentials, raw URLs, or private data appear in Markdown or JSON.
10. Tests SHALL prove JSON and Markdown are byte-stable for identical inputs.
11. Tests SHALL prove the combined index is opened read-only and not mutated.
12. Tests SHALL cover optional browser-observed evidence as observed/demo metadata only, not static proof.
13. Tests SHALL cover older combined indexes missing optional property-flow or route-flow tables and emit schema/coverage gaps rather than crashing.
14. Tests SHALL cover non-combined-index rejection and required-versus-optional combined table behavior.
15. Tests SHALL cover route-flow-unavailable gaps, including local UI-to-HTTP evidence plus an explicit downstream gap.
16. Tests SHALL cover `--source` case-insensitive exact matching, framework constraints, ambiguous selector narrowing, deterministic top-N roots, and ambiguity gaps.
17. Tests SHALL cover `TruncatedByLimit`, cross-source boundary rendering, fixed confidence mapping, and the `NoLineageEvidence` versus `UnknownAnalysisGap` split under full versus reduced coverage.
18. Tests SHALL cover older combined indexes that predate property-flow or route-flow schema signals separately from wrong-input schema errors.
19. Implementation validation SHALL include `dotnet build src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, `git diff --check`, and any language-adapter smoke checks required by `docs/VALIDATION.md` for changed adapters.
20. Spec-only validation SHALL include `git diff --check`, `./scripts/check-private-paths.sh` when available, and Kiro spec review through `scripts/kiro-review.mjs` when local Kiro review tooling is available.
