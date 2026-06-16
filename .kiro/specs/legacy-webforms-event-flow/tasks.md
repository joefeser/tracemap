# Legacy WebForms Event Flow Tasks

## Implementation Tasks

- [x] 1. Add rule catalog and model constants. Requirements: 1, 2, 3, 4, 5, 6, 7 (rule/fact model only).
  - [x] Add WebForms fact types and rule IDs.
  - [x] Decide explicitly whether each proposed `WebForms*` fact type is new or reuses an existing inventory/edge fact without overloading semantics.
  - [x] Document limitations for markup, designer, handler, event-flow, and logic-signal rules.
  - [x] Bump the relevant extractor version.

- [x] 2. Extend file inventory. Requirements: 1.
  - [x] Identify WebForms markup, code-behind, master, user-control, and designer files.
  - [x] Preserve safe repo-relative paths and line spans.
  - [x] Add malformed/unreadable gap handling.
  - [x] Add inventory tests.

- [x] 3. Extract markup declarations and event bindings. Requirements: 1, 2.
  - [x] Parse page/control/master directives safely.
  - [x] Extract static server controls and supported event attributes.
  - [x] Emit event-binding facts with control ID/type, event name, handler name, file span, rule ID, and evidence tier.
  - [x] Emit needs-review/gap evidence for unsupported dynamic event wiring.
  - [x] Add tests for duplicate controls, deterministic ordering/stable IDs, malformed markup, and unsafe value suppression.

- [x] 4. Resolve code-behind handlers. Requirements: 3.
  - [x] Link markup to code-behind and partial class candidates.
  - [x] Resolve handler methods semantically when possible.
  - [x] Add syntax fallback for failed builds.
  - [x] Emit ambiguity gaps instead of arbitrary matches.
  - [x] Gate auto-event-wireup handlers on explicit `AutoEventWireup` enabled evidence or explicit static event subscription evidence.
  - [x] Add tests for semantic, syntax-only, missing, ambiguous, auto-wireup enabled, auto-wireup false, auto-wireup unknown, and explicit subscription cases.

- [x] 5. Extract and link designer control fields. Requirements: 4.
  - [x] Parse `.designer.cs` partial control fields.
  - [x] Link designer fields to markup controls by page/type identity and control ID.
  - [x] Keep designer evidence supporting-only.
  - [x] Add tests for missing/stale designer files.

- [x] 6. Project event-to-backend flow. Requirements: 5, 7.
  - [x] Connect resolved handlers to existing call/object creation evidence.
  - [x] Include existing WCF/service-reference mapping evidence when handler calls service clients.
  - [x] Include existing SQL/query/dependency surfaces without raw SQL.
  - [x] Preserve supporting fact IDs, edge IDs, rule IDs, evidence tiers, and coverage labels.
  - [x] Add tests for WCF and SQL reachability, reduced coverage, and no-backend-evidence cases.

- [x] 7. Add static logic and UI-boilerplate signals. Requirements: 6.
  - [x] Implement deterministic structural logic signals.
  - [x] Implement lower-strength UI-boilerplate signals.
  - [x] Keep wording bounded; do not claim proven business logic.
  - [x] Add tests for calculation/validation/service-call and UI-only handlers.

- [x] 8. Update reports, validation, and docs. Requirements: 7, 8.
  - [x] Add scan report counts and known-gap summaries.
  - [x] Reconcile precise `WebForms*` evidence with existing `legacy.validation.ui-events.v1` summaries so the legacy validation harness does not report divergent UI-event counts.
  - [x] Update legacy validation summary counts.
  - [x] Update acceptance or adapter-contract docs for new facts.
  - [x] Keep public claim level hidden until redacted evidence is available.
  - [x] Align implementation validation notes with `docs/VALIDATION.md` pinned smoke guidance when scanner behavior changes.

- [x] 9. Validate. Requirements: 8.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `python3 -m unittest scripts.tests.test_legacy_codebase_validation`
  - [x] If Python adapter tests are required by implementation changes, use the temporary virtual environment pattern from `AGENTS.md`.
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Negative privacy test proves generated facts/reports do not contain local absolute paths, private sample identifiers, raw SQL, snippets, config values, raw URLs, remotes, or secrets.
  - [x] Optional ignored local legacy smoke with redacted label/count comparison only.

## Deferred Follow-Ups

- Runtime WebForms/IIS execution.
- Full ASP.NET page lifecycle simulation.
- Event bubbling feasibility.
- Branch feasibility and mutation semantics.
- DI/reflection/dynamic dispatch target proof.
- DBML/EDMX entity/table mapping.
- Rich UI visualization of event-to-backend paths.
