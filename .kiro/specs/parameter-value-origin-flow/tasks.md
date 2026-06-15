# Parameter and Value-Origin Flow Tasks

## Spec-Only PR Scope

- [x] Add Kiro spec files under `.kiro/specs/parameter-value-origin-flow/`.
- [x] Keep this PR limited to the new spec folder.

## Implementation Tasks

- [x] 1. Confirm current flow evidence behavior. Requirements: 1, 2, 3, 9.
  - [x] Inspect .NET `ArgumentPassed`, `LocalAlias`, `FieldAlias`, `ParameterForwardEdge`, flow-boundary, runtime-evidence, and symbol-identity facts.
  - [x] Inventory combined report/path/reverse/diff/impact handling of value-origin evidence.

- [x] 2. Define shared value-origin models. Requirements: 1, 8, 9, 10.
  - [x] Decide whether new public report models are required or existing path/reverse models can carry value-origin notes.
  - [x] Define shared safe properties for argument, parameter, origin, constructor, and boundary roles.
  - [x] Define value-origin classifications as additive metadata/notes while preserving existing path-level classification strings.
  - [x] Document compatibility behavior for older indexes without precise flow tables.
  - [x] Audit combined flow table/view definitions before adding schema or changing views.

- [x] 3. Harden direct argument-to-parameter extraction initial slice. Requirements: 2, 8, 11.
  - [x] Add or update tests for semantic direct forwarding.

- [x] 4. Harden local and member origin evidence initial slice. Requirements: 4, 5, 8, 11.
  - [x] Add same-method local alias tests.
  - [x] Pin alias chain bound, default `3` hops, in tests and rule limitations.
  - [x] Add deterministic field/member alias tests.
  - [x] Add unique constructor parameter-to-member-to-call tests.
  - [x] Add ambiguous multiple-constructor/multiple-assignment downgrade tests.

- [x] 5. Add callback/lambda/async boundary evidence initial .NET slice. Requirements: 6, 8, 11.
  - [x] Preserve normal `ArgumentPassed` evidence for direct calls inside lambda/callback bodies.
  - [x] Emit rule-backed `CallbackBoundary` facts for syntactically visible lambdas, anonymous methods, delegate arguments, delegate creation, captured outer parameters/locals, and event subscriptions.
  - [x] Emit rule-backed `AsyncBoundary` facts for `await`, task scheduling/continuation calls, thread-pool queueing calls, and iterator `yield`.
  - [x] Add direct forwarding/argument evidence tests and review-tier captured-value/boundary tests.
  - [x] Document callback/async boundary limitations in rules and docs.

- [x] 8. Update rule catalog and docs. Requirements: 1, 8, 10.
  - [x] Reuse existing rule IDs where behavior is unchanged.
  - [x] Document limitations for every changed rule.
  - [x] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, and `docs/ACCEPTANCE.md` during implementation.
  - [x] Review new safe metadata keys against private-path guard expectations.

- [x] 9. Validate. Requirements: 11.
  - [x] Run affected adapter test suites.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Recommended PR Slices

- [x] PR 1: Current-state audit + shared value-origin model + focused .NET tests for direct forwarding and alias boundaries.
- [x] PR 2: Constructor/member origin hardening + ambiguous constructor downgrade tests.
- [x] PR 3: Callback/lambda/async boundary evidence initial .NET slice.
- [x] PR 4: Combined path/reverse/report value-origin notes and deterministic output tests after task 3 boundary semantics are available.
- [x] PR 5: TypeScript/JVM/Python adapter alignment where current behavior differs from the shared contract.

## Deferred Follow-Ups

- Add syntax/ordinal fallback tests for direct argument-to-parameter extraction.
- Add named/optional/rest/varargs/keyword/default parameter mapping tests.
- Emit gaps for unresolved or ambiguous argument mapping.
- Confirm syntax-only adapters use ordinal placeholders or gaps where named mapping is unavailable.
- Add mutation/collection/property/ref/out/destructuring boundary tests where relevant.
- Extend callback/lambda/async and closure boundary support to TypeScript/JVM/Python adapters where deterministic evidence exists.
- Add callback/delegate/event/promise/task/async boundary gap tests outside the .NET initial slice.
- Add a dedicated `CapturedValueFlow` rule only if a future slice promotes captured-value evidence beyond boundary review context.
- Ensure future callback/async reports do not claim runtime scheduling or ordering.
- Expand endpoint/request-root value-origin traversal beyond currently supported parameter-forward/surface notes.
- Add endpoint/request parameter root tests.
- Add service-call forwarding path tests.
- Add terminal SQL/HTTP/package/config surface tests where existing surfaces support them.
- Ensure unlinked surfaces remain gaps.
- Preserve source labels, scan IDs, commit SHAs, and supporting fact/edge IDs in combined output.
- Ensure diff/impact downgrade added/removed flow under reduced coverage.
- Add new `combined.flow.*.v1` or adapter-local rules only for new evidence behavior.
- Run TypeScript/JVM/Python tests if those adapters change.
- Run relevant smoke scripts if combined path/reverse behavior changes.
- Full taint analysis.
- Symbolic execution.
- Runtime telemetry.
- Collection-content and object-identity tracking.
- Framework-specific model-binding expansion.
- DI container execution or registration graph solving.
- Serializer contract expansion beyond deterministic rule-backed facts.
