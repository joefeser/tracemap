# Parameter and Value-Origin Flow Tasks

## Spec-Only PR Scope

- [x] Add Kiro spec files under `.kiro/specs/parameter-value-origin-flow/`.
- [x] Keep this PR limited to the new spec folder.

## Implementation Tasks

- [x] 1. Confirm current flow evidence behavior. Requirements: 1, 2, 3, 9.
  - [x] Inspect .NET `ArgumentPassed`, `LocalAlias`, `FieldAlias`, `ParameterForwardEdge`, flow-boundary, runtime-evidence, and symbol-identity facts.
  - Inspect TypeScript call/argument/value-flow facts and storage rows.
  - Inspect JVM call/argument/constructor evidence.
  - Inspect Python `ArgumentPassed`, `FieldAlias`, and derived parameter-forwarding rows.
  - [x] Inventory combined report/path/reverse/diff/impact handling of value-origin evidence.

- [x] 2. Define shared value-origin models. Requirements: 1, 8, 9, 10.
  - [x] Decide whether new public report models are required or existing path/reverse models can carry value-origin notes.
  - [x] Define shared safe properties for argument, parameter, origin, constructor, and boundary roles.
  - [x] Define value-origin classifications as additive metadata/notes while preserving existing path-level classification strings.
  - [x] Document compatibility behavior for older indexes without precise flow tables.
  - [x] Audit combined flow table/view definitions before adding schema or changing views.

- 3. Harden direct argument-to-parameter extraction. Requirements: 2, 8, 11.
  - [x] Add or update tests for semantic direct forwarding.
  - Add or update tests for syntax/ordinal fallback.
  - Add or update tests for named/optional/rest/varargs/keyword/default parameter mapping.
  - Emit gaps for unresolved or ambiguous argument mapping.
  - Confirm syntax-only adapters use ordinal placeholders or gaps where named mapping is unavailable.

- 4. Harden local and member origin evidence. Requirements: 4, 5, 8, 11.
  - [x] Add same-method local alias tests.
  - [x] Pin alias chain bound, default `3` hops, in tests and rule limitations.
  - [x] Add deterministic field/member alias tests.
  - [x] Add unique constructor parameter-to-member-to-call tests.
  - [x] Add ambiguous multiple-constructor/multiple-assignment downgrade tests.
  - Add mutation/collection/property/ref/out/destructuring boundary tests where relevant.

- 5. Add callback, lambda, async, and closure boundaries. Requirements: 6, 8, 11.
  - Add direct lambda/callback body forwarding tests for supported adapters.
  - Add captured-value review-tier tests where deterministic.
  - Add callback/delegate/event/promise/task/async boundary gap tests.
  - Add or update rule catalog entries before emitting `CallbackBoundary`, `AsyncBoundary`, or `CapturedValueFlow`.
  - Ensure reports do not claim runtime scheduling or ordering.

- 6. Connect value origins to endpoints and dependency surfaces. Requirements: 7, 9, 11. Depends on task 3 and same-adapter task 5 decisions.
  - Add endpoint/request parameter root tests.
  - Add service-call forwarding path tests.
  - Add terminal SQL/HTTP/package/config surface tests where existing surfaces support them.
  - Ensure unlinked surfaces remain gaps.
  - Preserve source labels, scan IDs, commit SHAs, and supporting fact/edge IDs in combined output.

- 7. Update combined reporting and query layers. Requirements: 8, 9, 10.
  - Add value-origin counts/limitations to combined report if useful.
  - Add path notes for value-origin edges.
  - Preserve existing canonical path `Classification` values; add value-origin classification as notes or additive fields only.
  - Preserve value-origin support in reverse query.
  - Ensure diff/impact downgrade added/removed flow under reduced coverage.
  - Add byte-stability tests for changed outputs.

- 8. Update rule catalog and docs. Requirements: 1, 8, 10.
  - [x] Reuse existing rule IDs where behavior is unchanged.
  - Add new `combined.flow.*.v1` or adapter-local rules only for new evidence behavior.
  - [x] Document limitations for every changed rule.
  - [x] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, and `docs/ACCEPTANCE.md` during implementation.
  - [x] Review new safe metadata keys against private-path guard expectations.

- [x] 9. Validate. Requirements: 11.
  - [x] Run affected adapter test suites.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - Run TypeScript/JVM/Python tests if those adapters change.
  - Run relevant smoke scripts if combined path/reverse behavior changes.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Recommended PR Slices

- [x] PR 1: Current-state audit + shared value-origin model + focused .NET tests for direct forwarding and alias boundaries.
- PR 2: Constructor/member origin hardening + ambiguous constructor downgrade tests.
- PR 3: Callback/lambda/async boundary evidence across supported adapters.
- PR 4: Combined path/reverse/report value-origin notes and deterministic output tests after task 3 boundary semantics are available.
- PR 5: TypeScript/JVM/Python adapter alignment where current behavior differs from the shared contract.

## Deferred Follow-Ups

- Full taint analysis.
- Symbolic execution.
- Runtime telemetry.
- Collection-content and object-identity tracking.
- Framework-specific model-binding expansion.
- DI container execution or registration graph solving.
- Serializer contract expansion beyond deterministic rule-backed facts.
