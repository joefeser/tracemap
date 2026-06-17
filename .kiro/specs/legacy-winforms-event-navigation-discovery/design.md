# Legacy WinForms Event Navigation Discovery Design

## Overview

This phase adds a Windows Forms entry-point layer to the existing static
evidence graph. It should reuse the .NET scanner, C# syntax/semantic analysis,
file inventory, call-edge/object-creation extraction, value-flow evidence,
legacy WCF/ASMX/remoting evidence, legacy data metadata, SQL/query facts,
combined reports, dependency paths, evidence graph/vault export, and legacy
validation harness wherever possible.

The intended evidence chain is:

```text
WinForms designer/control/event evidence
  -> resolved handler method
  -> call/object/value-flow evidence
  -> navigation edge or callback boundary
  -> WCF/ASMX/remoting/data/SQL/dependency surface evidence
  -> existing reports, paths, reverse queries, impact, graph export, vault export
```

Every edge remains static evidence. TraceMap must not claim runtime UI
execution, designer execution, event firing, control visibility, navigation
feasibility, thread scheduling, dependency injection resolution, auth/role
permission, service reachability, SQL execution, database existence, deployment,
or production usage.

## Non-Goals

- No WinForms runtime simulation or designer execution.
- No UI automation, screenshotting, accessibility-tree inspection, or app
  execution.
- No full control-tree/layout reconstruction.
- No localization or resource value resolution beyond safe static metadata.
- No proof that an event fires, a form opens, a timer ticks, or a background
  worker runs.
- No runtime DI, reflection target, dynamic dispatch, branch feasibility,
  auth/role gating, thread scheduling, or application state proof.
- No runtime service calls, database calls, WSDL fetches, remoting activation,
  config transform execution, or database introspection.
- No raw source snippets, raw resources, raw SQL, config values, endpoint
  addresses, local absolute paths, raw remotes, private sample identifiers, or
  secrets in committed artifacts.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Proposed Fact Types

Add deterministic fact types only where existing fact types cannot carry the
meaning without overloading semantics:

- `WinFormsSurfaceDeclared`
- `WinFormsControlDeclared`
- `WinFormsEventBindingDeclared`
- `WinFormsHandlerResolved`
- `WinFormsNavigationEdgeDeclared`
- `WinFormsCallbackBoundaryDeclared`
- `WinFormsHandlerFlowProjected`
- `WinFormsResourceMetadataDeclared`

These facts are additive. Existing `CallEdge`, `ObjectCreated`,
`ArgumentPassed`, `ParameterForwardEdge`, `LocalAlias`, `FieldAlias`,
WCF/ASMX/remoting, legacy data, SQL/query, endpoint, dependency-surface, path,
reverse, and graph-export facts remain the source of truth for their domains.

## Proposed Rules

- `legacy.winforms.inventory.v1`
- `legacy.winforms.control.v1`
- `legacy.winforms.event-binding.v1`
- `legacy.winforms.handler-resolution.v1`
- `legacy.winforms.navigation.v1`
- `legacy.winforms.callback-boundary.v1`
- `legacy.winforms.handler-flow.v1`
- `legacy.winforms.resource-metadata.v1`

Rule catalog entries must include limitations before implementation emits these
rule IDs. Every rule should state that evidence is static repository evidence
and does not prove runtime execution, reachability, visibility, scheduling,
authorization, deployment, service calls, SQL execution, database existence, or
production usage.

## Rule To Fact And Tier Mapping

| Rule ID | Emits | Tier ceiling |
| --- | --- | --- |
| `legacy.winforms.inventory.v1` | `WinFormsSurfaceDeclared`, `AnalysisGap` | `Tier1Semantic` for resolved base types; `Tier2Structural` for designer/form structure; `Tier3SyntaxOrTextual` for syntax-only base names; `Tier4Unknown` for gaps |
| `legacy.winforms.control.v1` | `WinFormsControlDeclared`, `AnalysisGap` | `Tier1Semantic` for resolved control/component types; `Tier2Structural` for designer field/object creation structure; `Tier3SyntaxOrTextual` for scoped syntax-only names; `Tier4Unknown` for gaps |
| `legacy.winforms.event-binding.v1` | `WinFormsEventBindingDeclared`, `AnalysisGap` | `Tier1Semantic` for resolved event and handler symbols; `Tier2Structural` for scoped designer subscription evidence; `Tier3SyntaxOrTextual` for syntax-only scoped subscriptions; `Tier4Unknown` for gaps |
| `legacy.winforms.handler-resolution.v1` | `WinFormsHandlerResolved`, `AnalysisGap` | `Tier1Semantic` for resolved handler symbols; `Tier2Structural` for designer/partial/method-signature alignment; `Tier3SyntaxOrTextual` for scoped syntax-name alignment; `Tier4Unknown` for ambiguity, runtime wiring, lambda/delegate, reflection, DI, or reduced coverage gaps |
| `legacy.winforms.navigation.v1` | `WinFormsNavigationEdgeDeclared`, `AnalysisGap` | `Tier1Semantic` for resolved form symbols plus show/run call; `Tier2Structural` for deterministic object creation plus show/run structure; `Tier3SyntaxOrTextual` for scoped syntax-only evidence; `Tier4Unknown` for gaps |
| `legacy.winforms.callback-boundary.v1` | `WinFormsCallbackBoundaryDeclared`, `AnalysisGap` | `Tier1Semantic` for resolved callback component/event symbols; `Tier2Structural` for designer callback structure; `Tier3SyntaxOrTextual` for syntax-only callback names; `Tier4Unknown` for gaps |
| `legacy.winforms.handler-flow.v1` | `WinFormsHandlerFlowProjected`, `AnalysisGap` | Capped by weakest required handler, path, and terminal evidence tier; `Tier4Unknown` for reduced coverage gaps |
| `legacy.winforms.resource-metadata.v1` | `WinFormsResourceMetadataDeclared`, `AnalysisGap` | `Tier2Structural` for safe linked resource metadata; `Tier4Unknown` for parser/security/unsupported resource gaps |

WinForms gaps are emitted as `AnalysisGap` facts with a `classification`
property. `UnknownAnalysisGap` is a classification value, not a fact type.

Fact IDs should follow existing TraceMap stable identity conventions: derive
from deterministic scan identity inputs such as commit SHA, rule ID, fact type,
repo-relative file path, line span, safe symbol/control identity, event name,
and ordered supporting fact or edge IDs. Do not include timestamps, local
absolute paths, raw snippets, raw resource values, raw config values, or other
volatile/private inputs.

## File Inventory

Extend inventory to identify likely WinForms files while keeping them eligible
for normal C# extraction:

- `.cs` files containing `partial class` form/control/component declarations;
- `.Designer.cs` files containing `InitializeComponent`;
- `.resx` files adjacent to form/control partial classes;
- startup files containing `Application.Run` or `ApplicationContext` evidence;
- generated designer files with common headers or `InitializeComponent` shapes.

Inventory should use repo-relative paths only. It should not store local
absolute paths, source snippets, resource values, generated temp paths, private
repository names, raw remotes, or secrets.

Malformed, unreadable, too-large, or parser-rejected files should produce
`AnalysisGap` facts with stable classifications.

## Surface And Control Extraction

### Surface Detection

Semantic pass:

- resolve base types such as `System.Windows.Forms.Form`,
  `System.Windows.Forms.UserControl`, `System.Windows.Forms.Control`,
  `System.ComponentModel.Component`,
  `System.Windows.Forms.ApplicationContext`, and derived custom types when
  possible;
- include symbol identity using existing C# conventions;
- treat generated/designer partials as supporting evidence, not business logic.

Syntax fallback:

- detect base type names scoped to WinForms `using` directives, qualified names,
  or designer structure;
- avoid global short-name matches when file/type context is missing;
- downgrade to `Tier3SyntaxOrTextual` unless form/designer structure supports a
  `Tier2Structural` fact.

### Control And Component Detection

Extract controls/components from:

- designer field declarations;
- `InitializeComponent` object creations;
- field assignment patterns such as `this.button1 = new Button()`;
- menu/toolstrip item declarations and `Items.Add`/`DropDownItems.Add` shapes
  when deterministic;
- timer/background worker component fields;
- `DataGridView`, `ListView`, `TreeView`, `TabControl`, `MenuStrip`,
  `ToolStrip`, `Button`, `CheckBox`, `RadioButton`, `ComboBox`, and common
  WinForms controls.

Do not infer runtime parentage, layout, visibility, enabled state, data binding
result, localization result, or role gating from these facts.

## Event Binding And Handler Resolution

Detect explicit C# event subscriptions, especially in `InitializeComponent`:

- `control.Click += handler`;
- `control.Click += new EventHandler(handler)`;
- `control.Event += this.Handler`;
- `menuItem.Click += ...`;
- `toolStripItem.Click += ...`;
- `dataGridView.CellContentClick += ...`;
- `backgroundWorker.DoWork += ...`;
- `timer.Tick += ...`;
- anonymous delegate or lambda subscriptions as boundary/gap evidence unless
  the target method is deterministically visible.

Supported MVP event families:

| Family | Example events |
| --- | --- |
| Buttons and command controls | `Click`, `CheckedChanged`, `SelectedIndexChanged`, `TextChanged` |
| Menu and toolbar | `Click`, `DropDownItemClicked`, `ItemClicked` |
| Grids and lists | `CellClick`, `CellContentClick`, `CellDoubleClick`, `SelectionChanged`, `RowEnter`, `AfterSelect` |
| Forms | `Load`, `Shown`, `Closing`, `FormClosing`, `Activated` |
| Background workers | `DoWork`, `RunWorkerCompleted`, `ProgressChanged` |
| Timers | `Tick` |

Resolution order:

1. `Tier1Semantic`: event symbol and handler method symbol resolve.
2. `Tier2Structural`: designer subscription, partial class identity, and method
   signature align without full semantic symbols. A designer subscription
   requires the subscription to appear inside an `InitializeComponent` method in
   a `.Designer.cs` file or a file with recognized designer-generated markers.
   Subscriptions in ordinary hand-authored `.cs` files without those markers
   must not be promoted above syntax/textual evidence.
3. `Tier3SyntaxOrTextual`: scoped syntax-only event subscription and handler
   method name align in linked form/control files.
4. `Tier4Unknown`: ambiguity, unsupported subscription shape, missing/stale
   designer file, lambda/delegate boundary, runtime wiring, reflection, DI,
   generated drift, or reduced coverage.

Do not match handler names globally across the repository. If multiple methods
or partial classes remain after form/control scoping, emit ambiguity gaps.

## Navigation Projection

Emit navigation facts for deterministic static patterns:

- `Application.Run(new MainForm())`;
- `Application.Run(context)` when `ApplicationContext` type is visible;
- `form.Show()`;
- `form.ShowDialog()`;
- `new ChildForm().Show()` or deterministic local variable/object creation
  followed by show call;
- `child.MdiParent = this` plus `child.Show()`;
- `IsMdiContainer = true` as form containment support;
- owner/parent arguments passed to `ShowDialog(owner)` where symbols are safe.

Navigation classifications:

| Classification | Meaning |
| --- | --- |
| `StrongStaticNavigation` | source handler/startup, form construction, and show/run target resolve semantically or by strong structure. |
| `ProbableStaticNavigation` | object creation and show/run call align with at least `Tier2Structural` evidence but semantic resolution is incomplete. |
| `NeedsReviewNavigation` | `Tier3SyntaxOrTextual` only, factory-assisted, or partially ambiguous target evidence needs review. |
| `UnknownAnalysisGap` | reduced coverage, dynamic factory, reflection, DI, runtime state, auth/role gating, branch gating, or duplicate candidates prevents a credible edge. |

Navigation edges should cite supporting event, handler, call, object creation,
and surface fact IDs where available. They should not be used to claim runtime
reachability.

## Callback Boundaries

`BackgroundWorker`, timers, and UI marshalling should be visible as boundaries:

- `BackgroundWorker.DoWork`, `RunWorkerCompleted`, and `ProgressChanged`;
- `System.Windows.Forms.Timer.Tick`;
- `System.Threading.Timer` and `System.Timers.Timer` when visible in WinForms
  code;
- `Control.Invoke`, `BeginInvoke`, and delegate callbacks as boundary evidence.

Boundary classifications:

| Classification | Use |
| --- | --- |
| `BackgroundWorkerBoundary` | Static handler evidence exists for background worker events. |
| `TimerCallbackBoundary` | Static timer callback evidence exists. |
| `UiMarshalBoundary` | `Invoke`/`BeginInvoke` or UI-thread marshalling evidence appears on a receiver that resolves to, or is scoped inside, a WinForms `Control`-derived surface. |
| `AsyncDelegateBoundary` | Delegate/lambda/async callback evidence blocks stronger static flow. |
| `UnknownAnalysisGap` | Scheduling/order/runtime target cannot be resolved. |

Callback evidence may support handler-flow projection, but the projection must
state that scheduling, ordering, cancellation, progress semantics, and thread
affinity are not proven.

Calls named `Invoke` or `BeginInvoke` on unknown or non-control receiver types
should not become `UiMarshalBoundary` facts. Emit a
`WinFormsCallbackBoundaryDeclared` fact with `AsyncDelegateBoundary`
classification, or an `AnalysisGap` fact with a specific classification, when
receiver identity is not credible.

## Handler Flow Projection

The MVP should project handler context from scan-local facts rather than
creating a separate graph engine. `WinFormsHandlerFlowProjected` is a scan-time
fact built from facts already available in the same scan/index. Combined
dependency paths, reverse queries, release-review, evidence graph export, and
vault export are report-time enrichments under Requirement 7; they are not
required inputs for emitting scan-time handler-flow facts.

Inputs:

- `WinFormsHandlerResolved`;
- event binding, callback boundary, and navigation facts;
- `CallEdge`, `ObjectCreated`, `ArgumentPassed`, `ParameterForwardEdge`,
  `LocalAlias`, and `FieldAlias`;
- WCF, ASMX, remoting, legacy data metadata, SQL/query, HTTP, config/package,
  and dependency-surface evidence from the same scan/index.

Projection rules:

- preserve supporting fact IDs and edge IDs;
- cap evidence tier by weakest required evidence;
- label reduced coverage when project load, semantic analysis, designer parsing,
  generated code, call edges, value flow, backend metadata, or combined graph
  data is unavailable;
- prefer direct handler-to-call evidence over name-only downstream matches;
- reuse existing dependency path and value-flow semantics instead of creating
  incompatible flow claims;
- when upstream call, value-flow, backend metadata, or optional report-time
  graph data is absent because the extractor did not run, the index was created
  by an older scanner, or the workflow is unavailable, emit
  `WinFormsBackendPathUnavailable` or another specific `AnalysisGap`
  classification with reduced coverage instead of treating absence as a clean
  result;
- do not infer collection contents, branch feasibility, mutation ordering,
  runtime DI targets, reflection targets, dynamic dispatch targets, thread
  scheduling, auth/role outcome, or database/service reachability.

Suggested classifications:

| Classification | Meaning |
| --- | --- |
| `StrongStaticHandlerFlow` | handler and terminal backend evidence are connected by semantic or strong structural call/path evidence. |
| `ProbableStaticHandlerFlow` | handler and terminal evidence are connected by structural evidence with reduced semantic support. |
| `NeedsReviewHandlerFlow` | syntax-only, generated, ambiguous, callback-boundary, or partial evidence requires review. |
| `NoBackendEvidence` | no downstream backend evidence under full coverage. |
| `UnknownAnalysisGap` | reduced coverage or missing evidence prevents a credible conclusion. |

Minimum safe properties for `WinFormsHandlerFlowProjected`:

| Property | Purpose |
| --- | --- |
| `formTypeName` | Safe form/control/component type identity when known. |
| `controlId` | Safe control/component identifier when known. |
| `eventName` | Static event name. |
| `handlerName` | Static handler identifier. |
| `sourceSymbolId` | Shared symbol role for the resolved handler when semantic evidence exists. |
| `flowClassification` | One of the handler-flow classifications. |
| `terminalSurfaceKind` | Safe terminal kind such as `wcf-operation`, `asmx-operation`, `remoting-call`, `legacy-data`, `sql-query`, `http-client`, or `dependency-surface`. |
| `terminalSurfaceNameHash` | Hash of unsafe terminal names when cleartext is not safe. |
| `coverage` | Full/reduced coverage label. |
| `supportingFactIds` | Deterministically ordered supporting fact IDs. |
| `supportingEdgeIds` | Deterministically ordered supporting edge IDs. |
| `ruleIds` | Deterministically ordered contributing rule IDs. |
| `evidenceTiers` | Deterministically ordered contributing evidence tiers. |

Raw SQL, snippets, resource values, config values, endpoint addresses, local
absolute paths, private repository identifiers, and secrets must not appear in
properties.

`sourceSymbolId` is the canonical handler symbol property. Do not add a
WinForms-specific duplicate handler-symbol property unless a future reader
requires an explicit compatibility alias and the alias contract is documented.
Unsafe terminal names should be hashed with the existing TraceMap safe hashing
helper used by scanner facts. Use the same stable lowercase hex digest format
and algorithm metadata conventions already used by comparable WCF, ASMX, SQL,
and legacy data facts so terminal hashes remain comparable across fact types.

## Resource Metadata

`.resx` handling is optional MVP support and should be conservative:

- link resource files to form/control partial classes by safe adjacent filename
  and generated resource manager references;
- emit resource presence, culture suffix, safe resource key hashes, and resource
  kind labels only when safe;
- skip or hash raw string values, binary payloads, image/icon data, file paths,
  URLs, config-looking values, and secret-looking values;
- emit parser/security gaps for DTD/entity expansion, external references,
  malformed XML, unsupported resource kinds, too-large files, and ambiguous
  linkage.

Resource metadata can support form/control identity but must not be used to
reconstruct runtime text, localization, layout, or user-facing behavior.

## Gap Classifications

Use stable classification strings so tests and reports remain deterministic:

| Classification | Use |
| --- | --- |
| `MalformedWinFormsDesigner` | Designer C# cannot be parsed safely. |
| `MissingWinFormsPartialClass` | Designer or resource metadata names a partial class that is absent. |
| `StaleWinFormsDesigner` | Designer evidence conflicts with visible form/control partial evidence. |
| `AmbiguousWinFormsPartialClass` | Multiple partial classes could own the same designer/control evidence. |
| `AmbiguousWinFormsHandler` | Multiple handlers could satisfy the same event binding. |
| `UnsupportedWinFormsEventSubscription` | Event syntax is recognized but outside MVP scope. |
| `RuntimeWinFormsEventSubscription` | Runtime wiring cannot be deterministically scoped. |
| `DynamicWinFormsControlCreation` | Controls are created through factories, loops, collections, or runtime state. |
| `DynamicWinFormsNavigationTarget` | Navigation target is selected dynamically. |
| `WinFormsReflectionBoundary` | Reflection blocks deterministic target resolution. |
| `WinFormsDependencyInjectionBoundary` | Runtime DI/service location blocks deterministic target resolution. |
| `WinFormsAuthRoleBoundary` | Auth/role/user-state gating is visible but not statically resolved. |
| `WinFormsThreadSchedulingBoundary` | Timer/background/async scheduling prevents execution-order claims. |
| `UnsupportedWinFormsResourceMetadata` | `.resx` content or linkage is unsupported or unsafe. |
| `WinFormsResourceParserSecurityRejected` | Resource parser rejects unsafe XML behavior. |
| `WinFormsBackendPathUnavailable` | Existing call/path/value-flow/backend evidence is unavailable or reduced. |

## Reporting And Integration

Minimum implementation:

- `tracemap scan` emits WinForms fact counts and known gaps in `report.md`;
- `facts.ndjson` and `index.sqlite` contain rule IDs, tiers, paths, line spans,
  commit SHA, extractor IDs, extractor versions, properties, and supporting IDs;
- legacy validation summaries include WinForms event/navigation counts and gap
  counts;
- precise `WinForms*` evidence feeds or supersedes only the WinForms-attributed
  portion of the existing coarse `legacy.validation.ui-events.v1` summary where
  available. WebForms-attributed evidence remains covered by precise WebForms
  facts where present or by the existing coarse probe until its own precise
  evidence supersedes it. The legacy validation harness should emit one
  non-divergent combined UI-event count, not separate counts with different
  semantics.

Preferred integration:

- combined reports summarize WinForms entry points, navigation, callbacks, and
  handler-flow availability;
- dependency path/reverse commands can use handlers and form surfaces as roots
  where graph evidence exists;
- contract reducer and release-review can include WinForms context as supporting
  static evidence or explicit unavailable/partial sections;
- evidence graph and vault export include WinForms nodes/edges with rule IDs,
  evidence tiers, commit SHAs, source labels, supporting IDs, and limitations;
- legacy smoke catalog gains a public-safe fixture entry only after checked-in
  or redacted validation exists.

MVP implementation should prioritize scan artifacts, scan report output, legacy
validation reconciliation, and explicit availability gaps in existing combined
and report-time workflows. Rich path/reverse/reducer/release-review graph
behavior may remain deferred unless existing infrastructure can consume the new
facts without changing semantics.

Older indexes without WinForms facts must remain readable. Optional precise
tables should be additive, and older indexes should produce availability gaps
instead of failures.

## Minimal Fixture Guidance

Implementation validation should include at least one deterministic WinForms
fixture. A temporary local fixture is acceptable for first implementation if no
checked-in sample is approved yet, but the test shape should be documented and
redacted summaries only may be committed.

Minimum fixture structure:

- `Program.cs` with static `Application.Run(new MainForm())` startup evidence;
- `MainForm.cs` partial class derived from `Form` with one handler method;
- `MainForm.Designer.cs` partial class with `InitializeComponent`, one
  `Button` or `ToolStripMenuItem`, and one explicit event subscription;
- optional `DetailsForm.cs` target with a deterministic `Show` or `ShowDialog`
  call from the handler;
- optional `.resx` file with safe resource-presence coverage and redaction
  checks.

The fixture does not need to run or compile on modern SDKs for syntax-fallback
tests, but implementation validation should also include semantic coverage when
a modern-compatible test project is practical.

## Safety

Never store or render by default:

- raw source snippets;
- raw `.resx` values, images, icons, binary payloads, or resource strings;
- raw SQL;
- config values;
- connection strings;
- endpoint addresses or URLs;
- usernames, passwords, tokens, keys, or secret-looking values;
- local absolute paths;
- raw repository remotes;
- private repository/sample names.

Use existing safe identifier and hash helpers. When cleartext names are unsafe,
store hashes and explicit unsafe-name classifications.

## Implementation Slices

Recommended MVP slices:

1. Rule catalog, fact constants, extractor version, file inventory, and designer
   parser scaffolding.
2. Surface/control/component extraction with syntax fallback and semantic
   enrichment.
3. Event binding and handler resolution for explicit subscriptions.
4. Navigation and callback boundary facts.
5. Handler-flow projection using existing call/value-flow/backend evidence.
6. Reports, validation, acceptance docs, smoke catalog updates, and privacy
   tests.

Keep each implementation PR reviewable. Defer full WinForms runtime control-tree
simulation, rich layout/resource interpretation, and multi-hop graph expansion
unless a later spec defines deterministic rules.
