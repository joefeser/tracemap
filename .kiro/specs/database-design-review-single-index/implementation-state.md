# Database Design-Review Single-Index Implementation State

Status: implemented; PR handoff pending

Branch: `codex/database-design-review-single-index`

Base: `origin/dev` at `efb404a284fe102b60b6b4a870ee96354ba7f85d`

Issue: [#438](https://github.com/joefeser/tracemap/issues/438)

PR: [#537](https://github.com/joefeser/tracemap/pull/537)

## Scope decision

Implement the optional single-index packet input identified by #438. Reuse the
existing SQL evidence reader and the database design-review packet projection.
Add no extraction or runtime behavior.

Single indexes do not contain the combined graph/path contract used by the
packet. The reporter will therefore emit zero route references plus one
`SingleIndexRoutePathUnavailable` gap. It will not turn missing graph
infrastructure into per-query or per-operation route-absence claims.

Combined-index behavior remains unchanged. Public-site publication is deferred
to a separate slice.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
  --no-restore --filter FullyQualifiedName~DatabaseDesignReviewTests` — passed
  10/10.
- `dotnet build src/dotnet/TraceMap.sln --no-restore` — passed with the existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build` — passed
  907/907.
- End-to-end sample smoke: scan `samples/postgres-schema-migration`, render the
  packet directly from `index.sqlite`, combine the same scan, and render the
  combined packet — passed. Both packets contained four table groups and
  explicit partial coverage; the single packet contained zero route references
  plus `SingleIndexRoutePathUnavailable`.
- Protected-value/local-scratch search over both smoke outputs — passed.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Implemented behavior

- Automatic validation and detection of scanner and combined TraceMap indexes.
- Direct projection of stored PostgreSQL, EF mapping, application operation,
  and SQL/query evidence from `index.sqlite`.
- One explicit single-index graph/path coverage gap, with no per-query or
  per-operation route-absence claims and zero route rows.
- Unchanged combined-index path composition.
- Opt-in query-surface inclusion on the shared SQL evidence reader; existing
  callers retain their previous default behavior.
- Updated CLI help, rule limitations, validation guidance, deterministic
  regression coverage, and protected-value checks.

## Deferred

- Public-site publication.
- Richer PostgreSQL or framework extraction beyond already-shipped facts.
- Runtime database identity, connectivity, execution, telemetry, or approval.
