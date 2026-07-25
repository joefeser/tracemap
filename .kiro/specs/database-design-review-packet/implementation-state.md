# Database Design-Review Packet Implementation State

Status: implementation in progress (product complete; PR review active)

Branch: `codex/database-design-review-packet`

Base: `origin/dev` at `da5a56aa8ac0188ac1f010b276b0cd2d34713535`

Issue: [#438](https://github.com/joefeser/tracemap/issues/438)

PR: [#533](https://github.com/joefeser/tracemap/pull/533)

## Scope decision

Build a combined-index read-side packet over already-shipped PostgreSQL schema,
migration, snapshot, SQL/query surface, and bounded path evidence. Add no
extractor, parser, database connection, execution, runtime probe, or Windows
behavior.

The packet will use existing path-report results as the route authority and
will label exact query/table correlation as `static-name-match`. Missing links
remain gaps.

## Bookkeeping dependency

PR #532 merged into `dev` as
`da5a56aa8ac0188ac1f010b276b0cd2d34713535`. Issue #435 is narrowed to the
completed bounded PostgreSQL v0. Deeper extraction remains in #531; EF mapping
and operation call-pattern depth remain #436 and #437.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
  --filter FullyQualifiedName~DatabaseDesignReviewTests` — passed 7/7.
- `dotnet build src/dotnet/TraceMap.sln --no-restore` — passed with the existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build` — passed
  900/900.
- End-to-end sample smoke:
  `scan samples/postgres-schema-migration` → `combine` →
  `database-design-review` — passed; emitted deterministic Markdown/JSON with
  four table groups, eight global objects, explicit partial coverage, and no
  scratch path.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Implemented behavior

- New combined-index-only `database-design-review` CLI command.
- Deterministic `database-design-review.md` and
  `database-design-review.json` outputs under rule
  `database.design-review.packet.v1`.
- Separate declaration and migration-operation groupings.
- Exact source-scoped `static-name-match` query/table composition.
- Existing bounded endpoint/legacy-root path composition only.
- Full upstream provenance where available, allowlisted safe metadata,
  bounded output caps, and rule-backed explicit coverage gaps.

## Review fixes

ACK authorized the current-head Codex and Qodo findings for patching. The review
patch preserves route extractor provenance, marks source warnings partial,
retains global enum/routine migration operations, labels unlinked queries
without claiming a match, normalizes tiers, hardens Markdown/commit rendering,
and corrects route/gap truncation accounting with regression coverage.

## Deferred

- Single-index packet input.
- New PostgreSQL DDL or framework migration extraction.
- EF/model mapping and operation-call extraction.
- Runtime database identity, connectivity, execution, telemetry, or approval.
- Public site publication.
