# Parameter and Value-Origin Flow Implementation State

## Current Branch

`codex/value-origin-callback-boundaries`

## Implemented Slice

Earlier implementation already shipped the recommended PR 2 constructor/member hardening, including:

- unique constructor parameter-to-field-to-call forwarding,
- ambiguous constructor/member origin omission,
- reassigned constructor field omission beyond the alias bound,
- repeated constructor assignment omission.

This branch reconciles that stale task state and implements the recommended PR 3 initial .NET slice:

- explicit `CallbackBoundary` fact type under `csharp.semantic.flowboundary.v1`,
- explicit `AsyncBoundary` fact type under `csharp.semantic.flowboundary.v1`,
- callback boundaries for syntactically visible lambdas, anonymous methods, delegate arguments, delegate creation, captured outer parameters/locals, and event subscriptions,
- async boundaries for `await`, task scheduling/continuation calls, thread-pool queueing calls, and iterator `yield`,
- focused storage-level tests proving direct calls inside lambda bodies still emit normal `ArgumentPassed` evidence,
- focused tests proving captured-value, event, await, and task scheduling boundary evidence remains review-tier context.

## Scope Decisions

- No new public `tracemap flow`, combined path, reverse, diff, impact, or report output contract changes in this slice.
- No new `combined.flow.*` rule IDs were added because no new combined/public report row was introduced.
- No dedicated `CapturedValueFlow` fact/rule was added. Captured outer parameters/locals are represented as `CallbackBoundary` review context until a future slice promotes captured-value evidence beyond boundary labeling.
- Existing rules `csharp.semantic.valueflow.v1`, `csharp.semantic.localalias.v1`, `csharp.semantic.fieldalias.v1`, and `csharp.semantic.parameterforwarding.v1` remain authoritative for forwarding.
- `csharp.semantic.flowboundary.v1` is extended to emit `CallbackBoundary` and `AsyncBoundary`; its limitations now explicitly exclude callback invocation, event firing, runtime scheduling, execution ordering, task completion, closure lifetime, and mutation safety.
- Value-origin classifications remain future additive metadata/notes for combined report/path/reverse layers; they do not replace existing path classifications.
- TypeScript, JVM, and Python alignment remain follow-up slices.
- TypeScript/JVM/Python tests were not run because this branch only changes the .NET semantic extractor, .NET tests, shared docs, and the rule catalog.

## Validation

- `dotnet test src/dotnet/TraceMap.sln --filter SqliteIndexWriterTests` passed: 11 focused tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 221 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Remaining Follow-Ups

- Add TypeScript/JVM/Python callback/lambda/async boundary alignment where deterministic evidence exists.
- Add combined path/reverse/report value-origin notes now that .NET callback/async boundaries are available.
- Add a dedicated `CapturedValueFlow` rule only if a future slice needs stronger captured-value evidence than review-tier boundary facts.
- Keep callback/async reports from claiming runtime scheduling, ordering, callback invocation, event firing, closure lifetime, or task completion.
