# Legacy WinForms Event Navigation Discovery Requirements

## Introduction

TraceMap is being tested against very old .NET applications, including Windows
Forms applications that may not build with modern SDKs. These codebases often
hide backend behavior behind designer-generated `InitializeComponent` event
hookups, form classes, menu and toolbar controls, grid callbacks, MDI windows,
timers, background workers, and service/data calls made from event handlers.

This spec defines a deterministic static extraction slice for WinForms UI
surfaces, event/navigation evidence, callback boundaries, and handler-to-backend
context. The goal is to connect user-action entry points to existing TraceMap
service/data evidence more clearly without runtime claims.

Public claim level: hidden until validated through redacted legacy summaries or
checked-in public fixtures.

## Scope

In scope:

- Inventory `.cs` form/user-control/component classes, `.Designer.cs` files,
  `.resx` metadata when safe, and application startup files that contain
  `Application.Run` evidence.
- Extract static WinForms form, control, component, menu/toolstrip/button/grid,
  timer, background worker, and event-hookup evidence from C# syntax and Roslyn
  semantic analysis when available.
- Resolve event handlers conservatively from `InitializeComponent`, field
  declarations, explicit event subscription syntax, and partial class identity.
- Project static navigation edges for `Form.Show`, `ShowDialog`,
  `Application.Run`, owner/parent assignment, and MDI parent/child patterns when
  deterministic evidence exists.
- Mark callback boundaries for `BackgroundWorker`, `Timer`, async/delegate, and
  UI-thread marshalling patterns without proving scheduling or runtime order.
- Connect resolved handlers to existing method call, object creation, argument
  flow, dependency path, WCF/ASMX/remoting, legacy data metadata, SQL/query, and
  dependency-surface evidence where existing TraceMap facts support the link.
- Emit explicit `AnalysisGap` facts for malformed designer files, missing/stale
  partial classes, ambiguous handlers, dynamic controls, runtime event
  subscription shapes, reflection, dependency injection, unsupported resource
  indirection, and reduced semantic coverage.
- Keep outputs safe: no raw source snippets by default, no raw config values,
  no secrets, no raw URLs, no local absolute paths, and no private sample names.

Out of scope:

- No scanner implementation in this spec branch.
- No site pages, site specs, or public copy changes.
- No app execution, WinForms runtime simulation, designer execution, UI
  automation, screen scraping, accessibility tree inspection, or event firing.
- No full runtime control-tree simulation, layout reconstruction, localization
  resolution, auth/role gating proof, thread scheduling proof, branch
  feasibility proof, runtime service/database calls, or production usage claims.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  fuzzy AI inference in TraceMap core.

## Requirements

### Requirement 1: WinForms File And Surface Inventory

**User Story:** As a maintainer, I want TraceMap to recognize WinForms UI files
even when the legacy solution cannot build.

Acceptance Criteria:

1. WHEN a repository contains `.cs` files declaring types derived from
   `System.Windows.Forms.Form`, `UserControl`, `Control`, `Component`,
   `ApplicationContext`, or common WinForms base types THEN TraceMap SHALL emit
   surface inventory facts with safe repo-relative paths, line spans, commit
   SHA, extractor version, rule ID, and evidence tier.
2. WHEN `.Designer.cs` partial classes contain `InitializeComponent` and control
   field declarations THEN TraceMap SHALL emit designer inventory facts without
   treating generated designer code as hand-authored business logic.
3. WHEN `.resx` files are linked to WinForms classes THEN TraceMap MAY emit safe
   resource metadata facts for form/control identity support, but SHALL NOT
   store raw resource values, binary payloads, images, icons, strings, config
   values, local paths, or secrets.
4. WHEN files are malformed, too large, unreadable, generated in unsupported
   shapes, or cannot be linked to a safe partial class identity THEN TraceMap
   SHALL emit `AnalysisGap` facts instead of claiming absence of WinForms
   evidence.
5. Inventory ordering and fact IDs SHALL be deterministic across repeated scans
   of the same repository commit.

### Requirement 2: Form, Control, And Component Discovery

**User Story:** As a reviewer, I want to see deterministic WinForms forms and
controls that could act as UI entry surfaces.

Acceptance Criteria:

1. WHEN form, user-control, component, or application context classes are
   detected THEN TraceMap SHALL emit facts describing the surface kind, safe type
   identity, partial-class linkage, file span, and evidence tier.
2. WHEN designer fields or `InitializeComponent` assignments create controls or
   components such as `Button`, `MenuStrip`, `ToolStrip`, `ToolStripMenuItem`,
   `DataGridView`, `ListView`, `TreeView`, `TabControl`, `Timer`, or
   `BackgroundWorker` THEN TraceMap SHALL emit control/component facts with safe
   field/control names and control kind.
3. WHEN semantic analysis resolves framework/control types THEN the facts SHOULD
   use fully qualified symbol identities at `Tier1Semantic`; when only syntax is
   available, facts SHALL be labeled `Tier3SyntaxOrTextual` or
   `Tier2Structural` only when file/partial/designer structure supports the
   claim.
4. WHEN dynamic control creation, collection-driven control lookup, custom
   factory methods, or runtime-generated controls are detected but not
   deterministically resolved THEN TraceMap SHALL emit reduced-coverage or
   needs-review gaps.
5. Designer/control facts SHALL NOT claim visibility, enabled state, user access,
   layout position, localization result, runtime parentage, or production usage.

### Requirement 3: Event Hookup And Handler Resolution

**User Story:** As a maintainer, I want TraceMap to connect WinForms user-action
events to handler methods when static evidence is credible.

Acceptance Criteria:

1. WHEN `InitializeComponent` or other C# syntax contains event subscriptions
   such as `control.Click += handler`, `control.Click += new EventHandler(...)`,
   `MenuItem.Click += ...`, `DataGridView.CellContentClick += ...`,
   `BackgroundWorker.DoWork += ...`, or `Timer.Tick += ...` THEN TraceMap SHALL
   emit event-binding facts with control/component identity, event name, handler
   name or symbol, file span, rule ID, and evidence tier.
2. WHEN a handler method resolves within the same form/control/component partial
   class or a deterministic target class THEN TraceMap SHALL emit
   handler-resolution facts preserving supporting fact IDs and evidence tiers.
3. WHEN semantic analysis resolves the handler symbol THEN handler resolution
   SHOULD be `Tier1Semantic`; when only designer/partial structure and method
   signature align, it SHALL be `Tier2Structural`; when only syntax/name
   matching within a scoped file supports it, it SHALL be `Tier3SyntaxOrTextual`.
4. WHEN event subscription uses lambdas, anonymous delegates, method groups on
   other objects, reflection, string-based handler names, custom event brokers,
   dependency injection, or runtime subscription outside deterministic scope
   THEN TraceMap SHALL emit needs-review or `AnalysisGap` evidence rather than
   choosing an arbitrary handler.
5. WHEN multiple handlers, duplicate fields, duplicate partial classes, stale
   designer files, or ambiguous type identities could satisfy an event binding
   THEN TraceMap SHALL preserve all direct evidence and emit ambiguity gaps
   rather than selecting one winner.

### Requirement 4: Navigation Edge Discovery

**User Story:** As a reviewer, I want static evidence of form-to-form navigation
without claims about runtime path feasibility.

Acceptance Criteria:

1. WHEN code calls `Application.Run`, `Form.Show`, `Form.ShowDialog`,
   `ShowDialog(owner)`, or constructs a form immediately before a deterministic
   show call THEN TraceMap SHALL emit navigation edge facts between safe form or
   application-context identities.
2. WHEN MDI parent/child patterns such as `IsMdiContainer`, `MdiParent`
   assignment, owned forms, or parent/owner arguments are visible THEN TraceMap
   SHALL emit static navigation/containment evidence with rule IDs and evidence
   tiers.
3. WHEN navigation is driven by menu/toolstrip/button/grid handlers THEN
   TraceMap SHALL preserve the event binding, handler, call/object creation, and
   navigation edge as separate supporting facts.
4. WHEN navigation target construction is dynamic, dependency-injected,
   reflection-based, factory-selected, branch-dependent, user-role-dependent, or
   controlled by runtime state THEN TraceMap SHALL emit a gap or needs-review
   classification, not a definitive navigation edge.
5. Navigation facts SHALL NOT prove the form opens at runtime, that a user can
   reach it, that auth/role checks allow it, that the window is visible, or that
   the navigation occurs in production.

### Requirement 5: Callback Boundary Discovery

**User Story:** As a maintainer, I want timers, background workers, and callback
boundaries to be visible without overclaiming execution order.

Acceptance Criteria:

1. WHEN static event evidence identifies `BackgroundWorker` callbacks such as
   `DoWork`, `RunWorkerCompleted`, or `ProgressChanged` THEN TraceMap SHALL emit
   callback-boundary facts linked to the component and handler evidence.
2. WHEN static event evidence identifies WinForms `Timer`, threading timer, or
   UI callback events such as `Tick`, `Invoke`, or `BeginInvoke` THEN TraceMap
   SHALL emit callback-boundary or reduced-coverage facts as appropriate.
3. WHEN callback handlers call services, data access, remoting, WCF/ASMX
   clients, SQL/query surfaces, or other dependency surfaces THEN TraceMap MAY
   project static backend context while labeling callback scheduling as partial
   or boundary-limited.
4. WHEN threading, cancellation, progress reporting, UI marshalling, async
   continuations, or runtime ordering determine behavior THEN TraceMap SHALL
   emit explicit limitations and SHALL NOT prove execution order, thread
   affinity, race freedom, or completion.

### Requirement 6: Handler-To-Backend Static Context

**User Story:** As a reviewer, I want a WinForms event handler to explain which
backend evidence it can statically reach.

Acceptance Criteria:

1. WHEN a resolved WinForms handler has call edges, object creations, argument
   flow, WCF/ASMX/remoting mappings, legacy data metadata, SQL/query facts, HTTP
   calls, config/package evidence, or dependency-surface facts THEN TraceMap
   SHALL emit a static handler-flow projection preserving supporting fact IDs,
   edge IDs, rule IDs, evidence tiers, coverage labels, and safe terminal
   surface identities.
2. WHEN the static path depends on existing method call/value-flow or dependency
   path infrastructure THEN TraceMap SHALL reuse those facts/edges and SHALL
   not invent a second graph with incompatible semantics.
3. WHEN a handler calls generated WCF, ASMX, remoting, TableAdapter, repository,
   or service-client code and existing TraceMap evidence maps that call to
   service/data metadata THEN the projection MAY include that mapping as
   supporting static evidence.
4. WHEN path evidence is incomplete because the project does not build,
   semantic analysis is reduced, generated code is missing/stale, call edges are
   absent, dynamic dispatch/reflection/DI is present, or backend metadata is
   partial THEN TraceMap SHALL label the projection partial or emit an
   `AnalysisGap` fact with an `UnknownAnalysisGap` or more specific WinForms gap
   classification.
5. WHEN no backend evidence is found under full coverage THEN TraceMap MAY
   report no static backend evidence; under reduced coverage it SHALL report an
   analysis gap instead.

### Requirement 7: Reports, Indexes, Graph Export, And Validation

**User Story:** As a TraceMap user, I want WinForms evidence to appear in normal
TraceMap artifacts and validation workflows.

Acceptance Criteria:

1. WHEN `tracemap scan` emits WinForms facts THEN `facts.ndjson`,
   `index.sqlite`, `report.md`, `scan-manifest.json`, and
   `logs/analyzer.log` SHALL preserve rule IDs, evidence tiers, file paths,
   line spans, commit SHA, extractor IDs, extractor versions, coverage labels,
   and supporting IDs where available.
2. WHEN combined report, dependency paths, reverse paths, contract reducer,
   release-review, evidence graph, or vault export workflows consume indexes
   with WinForms evidence THEN they SHALL either include those facts where
   supported or emit explicit availability gaps rather than silently ignoring
   them as clean absence.
3. WHEN older indexes lack precise WinForms tables or facts THEN reports SHALL
   remain readable and label missing precision as unavailable when relevant.
4. WHEN the legacy smoke catalog is updated for this slice THEN catalog entries
   SHALL use safe labels, expected rule IDs, evidence tiers, coverage labels,
   limitations, and artifact kinds only; they SHALL NOT include raw scan output
   or private sample identifiers.
5. WHEN precise `WinForms*` facts are available THEN legacy validation SHALL
   feed or supersede only the WinForms-attributed portion of the existing coarse
   `legacy.validation.ui-events.v1` summary with the precise evidence and SHALL
   NOT emit a second divergent UI-event count with different semantics.
   WebForms-attributed evidence SHALL remain covered by precise WebForms facts
   where available or by the existing coarse probe until superseded by its own
   precise evidence.
6. Generated Markdown/JSON/report output SHALL be deterministic and safe for
   public or redacted summaries.

### Requirement 8: Rule Catalog, Limitations, And Safety

**User Story:** As a maintainer, I want every WinForms conclusion to be tied to
documented rules and limitations before implementation lands.

Acceptance Criteria:

1. New rule IDs SHALL be documented in `rules/rule-catalog.yml` with emitted
   fact types, evidence tier expectations, and limitations before scanner code
   emits them.
2. Every emitted WinForms fact or gap SHALL include a rule ID, evidence tier,
   repo-relative file path, line span when available, commit SHA, extractor ID,
   and extractor version.
3. Reports and facts SHALL NOT store raw source snippets by default and SHALL
   NOT include raw resource values, raw config values, connection strings, raw
   SQL, endpoint addresses, secrets, raw remotes, local absolute paths, or
   private sample names.
4. Partial analysis SHALL be explicit when MSBuild project load, Roslyn
   semantics, designer parsing, resource parsing, call graph extraction, value
   flow, or backend metadata is unavailable or reduced.
5. Limitations SHALL explicitly cover dynamic control creation, runtime event
   subscription, reflection, generated designer drift, localization/resource
   indirection, dependency injection, auth/role gating, branch feasibility,
   thread scheduling, runtime state, service/database reachability, and
   production usage.
