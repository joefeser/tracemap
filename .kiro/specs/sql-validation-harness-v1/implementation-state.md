# SQL Validation Harness v1 Implementation State

Status: implementation-complete-awaiting-review
Branch: `codex/sql-validation-harness-v1`
Base: `dev` at `43c422e836c4885ef1521c12275e9ee4ec7519d7`
Issue: [#515](https://github.com/joefeser/tracemap/issues/515)

## Scope decision

Build a standalone producer for the summary contract shipped by PR #514. The
tool performs only compiled-in, parameterized, read-only catalog probes and
does not add connectivity to the TraceMap scanner or report composers.

V1 intentionally leaves archive-link connectivity, target-schema
compatibility, arbitrary validation queries, function execution, cleanup
mutation, and job execution as `not-run`.

## Bookkeeping completed

- Closed #508 against merged PR #514.
- Closed #511 against merged PR #512.

## Validation

- Harness tests: 16 passed, 0 failed.
- Harness plus ingestion compatibility tests: 29 passed, 0 failed.
- Full .NET solution: 861 passed, 0 failed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with only the
  pre-existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisories.
- No-connection CLI dry run: completed with 10 `not-run` assertions.
- Targeted whitespace verification: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Plan schema and sample JSON parse: passed.
- `TraceMap.SqlValidation` transitive vulnerability audit: no vulnerable
  packages reported for Npgsql `10.0.3` and its dependency graph.

No live PostgreSQL target was used. Live validation remains an explicit,
least-privilege operator action outside routine CI.

## Review disposition

- Qodo's low-severity suggestion to add per-assertion evidence fields is not
  applied to the strict `sql-validation-summary/v1` transport schema. Adding a
  field would make the producer incompatible with the shipped closed ingestion
  contract. The artifact-level digest, repository/commit/context provenance,
  validator identity, timestamps, and limitations bind the transport; the
  ingestion step assigns the documented observation or gap rule ID before the
  assertion becomes TraceMap evidence.

## Deferred

- Signed/attested artifacts and an external trust store.
- Remote archive connectivity probes.
- Declarative column/type schema compatibility.
- Execution of validation queries, functions, jobs, or cleanup operations.
- Public site rendering of observed validation evidence.
