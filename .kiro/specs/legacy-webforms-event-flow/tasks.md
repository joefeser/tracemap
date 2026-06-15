# Legacy WebForms Event Flow Tasks

## Implementation Tasks

- [ ] 1. Add rule catalog and model constants. Requirements: 1, 2, 3, 4, 5, 6, 7 (rule/fact model only).
  - [ ] Add WebForms fact types and rule IDs.
  - [ ] Decide explicitly whether each proposed `WebForms*` fact type is new or reuses an existing inventory/edge fact without overloading semantics.
  - [ ] Document limitations for markup, designer, handler, event-flow, and logic-signal rules.
  - [ ] Bump the relevant extractor version.

- [ ] 2. Extend file inventory. Requirements: 1.
  - [ ] Identify WebForms markup, code-behind, master, user-control, and designer files.
  - [ ] Preserve safe repo-relative paths and line spans.
  - [ ] Add malformed/unreadable gap handling.
  - [ ] Add inventory tests.

- [ ] 3. Extract markup declarations and event bindings. Requirements: 1, 2.
  - [ ] Parse page/control/master directives safely.
  - [ ] Extract static server controls and supported event attributes.
  - [ ] Emit event-binding facts with control ID/type, event name, handler name, file span, rule ID, and evidence tier.
  - [ ] Emit needs-review/gap evidence for unsupported dynamic event wiring.
  - [ ] Add tests for duplicate controls, deterministic ordering/stable IDs, malformed markup, and unsafe value suppression.

- [ ] 4. Resolve code-behind handlers. Requirements: 3.
  - [ ] Link markup to code-behind and partial class candidates.
  - [ ] Resolve handler methods semantically when possible.
  - [ ] Add syntax fallback for failed builds.
  - [ ] Emit ambiguity gaps instead of arbitrary matches.
  - [ ] Add tests for semantic, syntax-only, missing, ambiguous, and auto-wireup cases.

- [ ] 5. Extract and link designer control fields. Requirements: 4.
  - [ ] Parse `.designer.cs` partial control fields.
  - [ ] Link designer fields to markup controls by page/type identity and control ID.
  - [ ] Keep designer evidence supporting-only.
  - [ ] Add tests for missing/stale designer files.

- [ ] 6. Project event-to-backend flow. Requirements: 5, 7.
  - [ ] Connect resolved handlers to existing call/object creation evidence.
  - [ ] Include existing WCF/service-reference mapping evidence when handler calls service clients.
  - [ ] Include existing SQL/query/dependency surfaces without raw SQL.
  - [ ] Preserve supporting fact IDs, edge IDs, rule IDs, evidence tiers, and coverage labels.
  - [ ] Add tests for WCF and SQL reachability, reduced coverage, and no-backend-evidence cases.

- [ ] 7. Add static logic and UI-boilerplate signals. Requirements: 6.
  - [ ] Implement deterministic structural logic signals.
  - [ ] Implement lower-strength UI-boilerplate signals.
  - [ ] Keep wording bounded; do not claim proven business logic.
  - [ ] Add tests for calculation/validation/service-call and UI-only handlers.

- [ ] 8. Update reports, validation, and docs. Requirements: 7, 8.
  - [ ] Add scan report counts and known-gap summaries.
  - [ ] Reconcile precise `WebForms*` evidence with existing `legacy.validation.ui-events.v1` summaries so the legacy validation harness does not report divergent UI-event counts.
  - [ ] Update legacy validation summary counts.
  - [ ] Update acceptance or adapter-contract docs for new facts.
  - [ ] Keep public claim level hidden until redacted evidence is available.
  - [ ] Align implementation validation notes with `docs/VALIDATION.md` pinned smoke guidance when scanner behavior changes.

- [ ] 9. Validate. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `python3 -m unittest scripts.tests.test_legacy_codebase_validation`
  - [ ] If Python adapter tests are required by implementation changes, use the temporary virtual environment pattern from `AGENTS.md`.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Negative privacy test proves generated facts/reports do not contain local absolute paths, private sample identifiers, raw SQL, snippets, config values, raw URLs, remotes, or secrets.
  - [ ] Optional ignored local legacy smoke with redacted label/count comparison only.

## Deferred Follow-Ups

- Runtime WebForms/IIS execution.
- Full ASP.NET page lifecycle simulation.
- Event bubbling feasibility.
- Branch feasibility and mutation semantics.
- DI/reflection/dynamic dispatch target proof.
- DBML/EDMX entity/table mapping.
- Rich UI visualization of event-to-backend paths.
