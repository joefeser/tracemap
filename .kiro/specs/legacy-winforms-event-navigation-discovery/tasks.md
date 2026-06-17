# Legacy WinForms Event Navigation Discovery Tasks

## Spec Delivery Tasks

- [x] 0. Author spec package. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [x] Create `requirements.md`, `design.md`, `tasks.md`, and
        `implementation-state.md`.
  - [x] Keep the branch spec-only and avoid product code, site files, generated
        outputs, or rule catalog edits in this PR.
  - [x] Capture public/private safety boundaries and implementation-state notes.
  - [x] Run spec PR validation gates: `git diff --check`,
        `./scripts/check-private-paths.sh` if present, and available spec/docs
        validation scripts.

## Implementation Tasks

- [ ] 1. Add rule catalog and model constants. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [ ] Add `WinForms*` fact types only where existing facts cannot carry the
        meaning without overloading semantics.
  - [ ] Add `legacy.winforms.inventory.v1`.
  - [ ] Add `legacy.winforms.control.v1`.
  - [ ] Add `legacy.winforms.event-binding.v1`.
  - [ ] Add `legacy.winforms.handler-resolution.v1`.
  - [ ] Add `legacy.winforms.navigation.v1`.
  - [ ] Add `legacy.winforms.callback-boundary.v1`.
  - [ ] Add `legacy.winforms.handler-flow.v1`.
  - [ ] Add `legacy.winforms.resource-metadata.v1`.
  - [ ] Document limitations for designer drift, dynamic controls, runtime
        event subscription, reflection, DI, localization/resource indirection,
        auth/role gating, thread scheduling, runtime state, service/database
        reachability, and production usage.
  - [ ] Bump the relevant extractor version before facts are emitted.

- [ ] 2. Extend WinForms inventory. Requirements: 1, 7, 8.
  - [ ] Identify form/control/component `.cs` files, `.Designer.cs`
        `InitializeComponent` files, linked `.resx` metadata, and startup files
        with `Application.Run` evidence.
  - [ ] Preserve safe repo-relative paths and line spans.
  - [ ] Emit `AnalysisGap` facts for malformed, unreadable, too-large, or
        unsupported files.
  - [ ] Add deterministic inventory tests and privacy tests.

- [ ] 3. Extract form, control, and component surfaces. Requirements: 2.
  - [ ] Detect WinForms base types semantically when possible.
  - [ ] Add syntax/structural fallback for old projects that do not build.
  - [ ] Extract designer field declarations and `InitializeComponent` object
        creations for common controls/components.
  - [ ] Emit dynamic-control and generated-designer drift gaps where evidence is
        partial.
  - [ ] Add tests for semantic, syntax fallback, designer-only, stale designer,
        duplicate control, and dynamic control cases.

- [ ] 4. Extract event bindings and resolve handlers. Requirements: 3, 5.
  - [ ] Parse explicit event subscription syntax in designer and hand-authored
        C# files.
  - [ ] Support MVP event families for buttons, menus, toolstrips, grids,
        forms, timers, and background workers.
  - [ ] Resolve handlers semantically when possible and structurally/syntax-only
        when scoped evidence supports it.
  - [ ] Emit ambiguity, runtime subscription, lambda/delegate, reflection, DI,
        and unsupported event-shape gaps rather than arbitrary matches.
  - [ ] Add tests for direct method groups, `new EventHandler`, other delegate
        types, scoped syntax fallback, missing handlers, ambiguous handlers,
        lambdas, and runtime wiring.

- [ ] 5. Extract navigation edges. Requirements: 4.
  - [ ] Detect `Application.Run`, `Form.Show`, `ShowDialog`, owner/parent
        arguments, deterministic object creation before show calls, and
        application-context startup evidence.
  - [ ] Detect MDI parent/child support such as `IsMdiContainer` and
        `MdiParent` assignments.
  - [ ] Preserve supporting event, handler, call, object creation, and surface
        fact IDs.
  - [ ] Emit dynamic target, factory, reflection, DI, auth/role, branch, and
        runtime-state gaps.
  - [ ] Add tests for startup, direct show, dialog, MDI, handler-driven
        navigation, ambiguous targets, dynamic factories, and the
        `Tier2Structural` versus `Tier3SyntaxOrTextual` classification boundary.

- [ ] 6. Extract callback boundaries. Requirements: 5, 6.
  - [ ] Emit callback-boundary facts for `BackgroundWorker` callbacks, timers,
        `Invoke`/`BeginInvoke`, and async/delegate boundary shapes.
  - [ ] Label scheduling, order, cancellation, progress, and thread-affinity
        claims as unsupported.
  - [ ] Connect callback handlers to backend context only as static partial
        evidence.
  - [ ] Add tests for background worker, timer, UI marshal, async/delegate, and
        reduced-coverage cases.

- [ ] 7. Project handler-to-backend static context. Requirements: 6, 7.
  - [ ] Reuse existing call edges, object creations, argument/value-flow facts,
        dependency paths, WCF/ASMX/remoting facts, legacy data metadata, SQL
        facts, HTTP calls, config/package evidence, and dependency surfaces.
  - [ ] Emit `WinFormsHandlerFlowProjected` with supporting fact IDs, edge IDs,
        rule IDs, evidence tiers, terminal surface kind, coverage, and
        classification.
  - [ ] Cap the projected evidence tier by the weakest required supporting
        evidence tier, choose the flow classification from that capped evidence,
        and label reduced coverage explicitly.
  - [ ] Add tests for service-client mapping, SQL/query context, legacy data
        metadata context, remoting context, no-backend-evidence under full
        coverage, tier capping by weakest supporting evidence, and
        `AnalysisGap` classifications under reduced coverage.

- [ ] 8. Add conservative `.resx` metadata support. Requirements: 1, 2, 8.
  - [ ] Link `.resx` files to form/control partial classes only when safe
        deterministic evidence exists.
  - [ ] Store resource presence, culture suffix, safe key hashes, and resource
        kind labels without raw values.
  - [ ] Reject unsafe XML behavior and unsupported/binary/too-large resources
        with explicit gaps.
  - [ ] Add tests proving raw strings, binary payloads, images, icons, paths,
        URLs, config-looking values, and secrets are omitted or hashed.
  - [ ] Include specific `.resx` parser tests for plain string redaction,
        binary payload omission, connection-string-like values, and
        DTD/external-entity rejection.

- [ ] 9. Update reports, indexes, validation, and docs. Requirements: 7, 8.
  - [ ] Ensure `facts.ndjson`, `index.sqlite`, `report.md`,
        `scan-manifest.json`, and `logs/analyzer.log` preserve the required
        metadata.
  - [ ] Add report sections for WinForms surfaces, events, navigation,
        callbacks, handler-flow projections, and known gaps.
  - [ ] Integrate with combined reports, dependency paths, reverse paths,
        reducers, release-review, evidence graph export, and vault export as
        supported or explicit availability gaps.
  - [ ] Reconcile precise `WinForms*` evidence with existing
        `legacy.validation.ui-events.v1` summaries so the legacy validation
        harness does not report divergent UI-event counts.
  - [ ] Add a reconciliation non-divergence test where both precise
        `WinForms*` facts and the coarse `legacy.validation.ui-events.v1` probe
        are present, verifying one combined UI-event count and preserving
        WebForms-attributed evidence semantics.
  - [ ] Add backward-compatibility tests proving older indexes without WinForms
        facts remain readable and emit explicit WinForms availability gaps
        rather than silent clean absence.
  - [ ] Add deterministic Markdown/JSON report tests for WinForms sections and
        redaction.
  - [ ] Add tests proving terminal surface hashes use the same digest format and
        metadata conventions as comparable WCF, ASMX, SQL, and legacy data
        facts.
  - [ ] Update adapter contract, acceptance docs, rule catalog, and validation
        guidance when implementation lands.
  - [ ] Update the legacy smoke catalog only with public-safe or redacted
        metadata after validation exists.

- [ ] 10. Validate implementation. Requirements: 7, 8.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run relevant legacy validation scripts from `docs/VALIDATION.md`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run the CLI against at least one checked-in or temporary WinForms
        fixture and verify `scan-manifest.json`, `facts.ndjson`,
        `index.sqlite`, `report.md`, and `logs/analyzer.log` are produced.
  - [ ] Verify repeated scans of the same WinForms fixture produce stable
        ordering and stable fact IDs.
  - [ ] If ignored local legacy samples are used, commit only redacted
        label/count summaries and keep raw artifacts out of git.

## Deferred Follow-Ups

- Full WinForms runtime control-tree simulation.
- Designer execution, app execution, UI automation, screenshots, or
  accessibility inspection.
- Rich layout reconstruction and localization/resource value rendering.
- Runtime auth/role, DI, reflection, branch, event-order, and thread-scheduling
  proof.
- Multi-hop graph expansion beyond existing deterministic path/value-flow
  infrastructure.
- Public site copy or public claims before redacted validation exists.
