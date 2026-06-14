# Parameter and Value-Origin Flow Implementation State

## Current Branch

`codex/parameter-value-origin-flow`

## Implemented Slice

This branch implements the first recommended slice:

- current-state audit for .NET flow facts and combined flow tables,
- shared value-origin contract documentation,
- explicit .NET parameter-forward alias bound of 3 hops,
- focused storage-level tests proving direct parameter forwarding,
- same-method local alias forwarding within the 3-hop bound,
- no forwarding beyond the bound,
- unique constructor parameter-to-field-to-call forwarding,
- ambiguous constructor/member origin omission.

## Scope Decisions

- No new public `tracemap flow` output contract changes in this slice.
- No new `combined.flow.*` rule IDs were added because no new emitted fact or public report row was introduced.
- Existing rules `csharp.semantic.valueflow.v1`, `csharp.semantic.localalias.v1`, `csharp.semantic.fieldalias.v1`, and `csharp.semantic.parameterforwarding.v1` remain authoritative.
- Value-origin classifications remain future additive metadata/notes for combined report/path/reverse layers; they do not replace existing path classifications.
- TypeScript, JVM, and Python alignment remain follow-up slices.

## Validation

- `dotnet test src/dotnet/TraceMap.sln --filter SqliteIndexWriterTests` passed: 9 focused tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln --no-build` passed: 161 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- TypeScript/JVM/Python tests were not run because this slice only changes .NET storage derivation, .NET tests, and docs.

## Review Fixes

- Fixed constructor fallback after alias-depth truncation: exhausting the alias bound now omits the derived edge instead of falling back to the original field's constructor origin.
- Preserved constructor assignment multiplicity by removing constructor-origin de-duplication before the exactly-one-assignment check.
- Added regressions for reassigned constructor fields beyond the alias bound and repeated constructor assignments from the same parameter.
