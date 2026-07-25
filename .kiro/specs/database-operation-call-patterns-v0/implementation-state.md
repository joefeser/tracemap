# Database Operation Call-Patterns v0 Implementation State

Status: implemented; PR/ACK pending

Branch: `codex/database-operation-call-patterns-v0`

Base: `origin/dev` at `7ddb6171c2bab08c653cf23a662c3163b5efb2a2`

Issue: [#437](https://github.com/joefeser/tracemap/issues/437)

## Scope decision

Add one semantic static-operation candidate fact and explicit gaps, then compose
it through already-shipped PostgreSQL declarations and EF mappings. Reuse the
existing SQL shape parser for constant text; add no SQL parser, execution,
connection, startup, migration, or runtime behavior.

## Validation

- Focused semantic, design-review, SQL-shape, integration, path, and route-flow
  tests after review corrections: 129 passed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 errors
  and the repository's existing NU1903 SQLite package warnings.
- `dotnet test src/dotnet/TraceMap.sln --no-build --no-restore`: 905 passed.
- CLI smoke against `samples/modern-sample`: passed with all five required
  scan artifacts and Tier 1 semantic analysis.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Review correction

Exact-head Codex review identified four issues, all corrected with regression
coverage: application methods on `DbContext` subclasses no longer masquerade
as EF methods; safe schema qualification is retained before table matching;
truncated operation-path searches emit reduced coverage rather than absence;
and files without semantic models receive Tier 4 operation-rule fallback gaps.

## Deferred

- Complete repository abstraction, DI, branch, transaction-scope, command
  variable, and parameter-value flow.
- Convention-only EF targets and generated SQL.
- Complete route coverage when more than one operation surface shares one
  containing symbol; retained operation-specific route gaps expose that
  bounded path projection limitation.
- Database connectivity, execution, telemetry, state, and approval.
