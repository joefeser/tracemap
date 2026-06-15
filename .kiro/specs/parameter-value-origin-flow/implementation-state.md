# Parameter and Value-Origin Flow Implementation State

## Current Branch

`codex/value-origin-combined-notes`

## Implemented Slice

Earlier implementation already shipped recommended PRs 2 and 3:

- unique constructor parameter-to-field-to-call forwarding and ambiguous constructor/member origin omission,
- `CallbackBoundary` and `AsyncBoundary` fact types under `csharp.semantic.flowboundary.v1`,
- callback/lambda/async boundary limitations in rules and docs.

This branch implements recommended PR 4:

- combined report summaries now include deterministic value-origin evidence counts for `combined_argument_flows`, `combined_local_aliases`, `combined_field_aliases`, `combined_parameter_forward_edges`, `CallbackBoundary`, and `AsyncBoundary` evidence when present,
- combined report needs-review rows now surface callback/async boundary facts as value-origin review context without runtime scheduling, callback invocation, event firing, closure lifetime, or task completion claims,
- combined paths now add deterministic `ValueOriginClassification` notes for paths containing `parameter-forward` or `argument-passed` edges while preserving the canonical `Classification` field,
- combined reverse paths now carry the same additive notes and continue to preserve supporting fact IDs and combined edge IDs,
- Markdown renderers now show path/reverse notes, and JSON remains deterministic.

## Scope Decisions

- No `tracemap flow`, diff, or impact behavior changed in this slice.
- No new `combined.flow.*` rule IDs were added because the slice adds report/path/reverse notes derived from existing rule-backed facts and edges rather than emitting new evidence rows.
- Existing rules `csharp.semantic.valueflow.v1`, `csharp.semantic.localalias.v1`, `csharp.semantic.fieldalias.v1`, and `csharp.semantic.parameterforwarding.v1` remain authoritative for forwarding.
- Existing `csharp.semantic.flowboundary.v1` remains authoritative for callback/async boundary facts.
- Value-origin classifications are additive notes only; they do not replace existing path/reverse classifications.
- This slice does not infer endpoint/request DTO roots, runtime DI state, callback invocation, event firing, runtime scheduling, task completion, closure lifetime, mutation safety, object identity, or collection contents.
- TypeScript, JVM, and Python alignment remain follow-up slices.
- TypeScript/JVM/Python tests are not required unless those adapters change.

## Validation

- Focused combined output validation passed: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~CombinedDependencyPathTests|FullyQualifiedName~CombinedReverseQueryTests|FullyQualifiedName~CombinedDependencyReportTests"` passed, 39 tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 225 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Remaining Follow-Ups

- Add TypeScript/JVM/Python callback/lambda/async boundary alignment where deterministic evidence exists.
- Add a dedicated `CapturedValueFlow` rule only if a future slice needs stronger captured-value evidence than review-tier boundary facts.
- Expand endpoint/request-root value-origin traversal and tests beyond currently supported parameter-forward/surface notes.
- Add diff/impact value-origin downgrade behavior for reduced coverage or unstable identity.
- Keep future callback/async reports from claiming runtime scheduling, ordering, callback invocation, event firing, closure lifetime, or task completion.

## Review Fixes

- Qodo: expression-tree lambdas now emit `ExpressionTreeBoundary` metadata with `convertedExpressionType` and `underlyingDelegateType`, not misleading `convertedDelegateType` callback metadata.
- Qodo: captured-value scanning skips nested anonymous function bodies so outer callbacks do not inherit inner-only captures.
- Qodo: markdown `Flow Boundaries` now includes `CallbackBoundary` and `AsyncBoundary`.
- Qodo: iterator boundaries now include assembly metadata keys consistently with other async boundaries.
- Qodo: task scheduling detection now uses symbol-based type checks for `Task`, `Task<TResult>`, `TaskFactory`, `TaskFactory<TResult>`, and `ThreadPool` instead of display-name substring checks.
- Codex: delegate arguments passed to object creation, such as `new Thread(DoWork)`, now emit `DelegateArgumentBoundary`.
- Codex: `await foreach` and `await using` statements now emit `AsyncBoundary` facts.
