# Parameter and Value-Origin Flow Implementation State

## Current Branch

`codex/value-origin-adapter-alignment`

## Implemented Slice

This branch implements recommended PR 5:

- TypeScript semantic `ArgumentPassed` facts now emit shared `argument` and `parameter` role metadata when compiler symbols are available.
- TypeScript SQLite symbol indexing now prefers `{role}SymbolDisplayName` and preserves `{role}SymbolLanguage`.
- Java semantic `ArgumentPassed` facts now emit shared `parameter` role metadata and `argument` role metadata when javac resolves the argument expression to a symbol.
- JVM SQLite symbol indexing now preserves `argument`, `parameter`, `origin`, and `constructor` roles, not only `source` and `target`.
- Python AST `ArgumentPassed`, `LocalAlias`, and `FieldAlias` facts now emit shared role metadata for syntax-visible names.
- Python AST callee parameters remain explicit unresolved ordinal placeholders such as `arg0`; this is Tier3 syntax evidence and not semantic callable-signature resolution.

## Scope Decisions

- No new fact types or rule IDs were added. Existing TypeScript, JVM, and Python value-flow rules remain authoritative.
- No new runtime callback, promise/task scheduling, event firing, closure lifetime, branch feasibility, mutation safety, object identity, collection-content, DI, reflection, or serializer expansion claims were added.
- No TypeScript/JVM/Python interprocedural parameter-forwarding behavior was added beyond existing adapter/storage behavior.
- TypeScript syntax fallback and Java syntax fallback remain ordinal-only lower-tier evidence.
- Python remains reduced-coverage AST evidence and does not resolve callee signatures.

## Validation

- `npm test -- --run` from `src/typescript`: passed, 27 tests.
- `npm run build` from `src/typescript`: passed.
- `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle test` from `src/jvm`: passed.
- `/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests`: passed, 26 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 226 tests.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke`: passed and emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Remaining Follow-Ups

- Add deterministic field/member alias extraction for TypeScript and JVM only if compiler evidence supports direct assignments without property-setter or object-identity claims.
- Add TypeScript/JVM/Python callback/lambda/async boundary facts where deterministic syntax/compiler evidence exists.
- Add syntax/ordinal fallback tests for direct argument-to-parameter extraction beyond the current shared-role checks.
- Add named/optional/rest/varargs/keyword/default parameter mapping tests where adapters can prove mapping deterministically.
- Emit gaps for unresolved or ambiguous argument mapping where an adapter has enough context to identify the ambiguity.
- Add mutation/collection/property/ref/out/destructuring boundary tests where relevant.
- Expand endpoint/request-root value-origin traversal and tests beyond currently supported parameter-forward/surface notes.
- Add diff/impact value-origin downgrade behavior for reduced coverage or unstable identity.
