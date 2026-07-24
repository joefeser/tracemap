# SQL Validation Harness

`tracemap-sql-validation` is a separately invoked, operator-controlled producer
for `sql-validation-summary/v1`. It does not run during a TraceMap scan or
release review.

## Boundary

The harness accepts a strict local `sql-validation-plan/v1` file and reads a
PostgreSQL connection string only from an explicitly named environment
variable. The environment-variable name must match `[A-Z][A-Z0-9_]{0,63}`.
Neither the variable name nor value is written to the summary.

The local plan may contain PostgreSQL object identifiers needed by the fixed
catalog probes. Treat the plan as private operator input. It cannot contain SQL,
host/database names, credentials, or arbitrary prose, and its identifiers are
never copied into the public-safe output.

V1 executes six bounded checks:

- PostgreSQL major version equals the planned major version;
- every named extension is installed;
- every named migration/catalog relation resolves;
- the current user has `USAGE` on every named schema;
- every named function signature resolves and the current user has `EXECUTE`;
- `cron.job` exists and every named job is active.

Each check uses compiled-in SQL and positional parameters in its own read-only
transaction. Pooling is disabled and connection/command timeouts are bounded.
The harness consumes only scalar booleans or the server version integer.

V1 does not test archive-link connectivity, declarative target-schema
compatibility, arbitrary query shapes, cleanup operations, function execution,
job execution, migration execution, or rollback. Those assertion codes are
present in the summary as `not-run`.

## Run

Copy and edit the local example plan. Bind its repository and commit to the
static scan that will consume the result, and supply explicit observation and
expiry timestamps.

For a two-database archive rollout, start with the separate parser-valid
`source-plan.template.json` and `archive-target-plan.template.json` files under
`samples/sql-validation-harness/`. Copy them to private operator storage before
adding real identifiers. The ordered ownership, target-selection, stop, and
interpretation workflow is in
[`SQL_VALIDATION_OPERATOR_PACKET.md`](SQL_VALIDATION_OPERATOR_PACKET.md).

```bash
export TRACEMAP_SQL_VALIDATION_CONNECTION='<operator-supplied PostgreSQL connection string>'

dotnet run --project src/dotnet/TraceMap.SqlValidation.Cli -- validate \
  --plan /private/operator/sql-validation-plan.json \
  --connection-env TRACEMAP_SQL_VALIDATION_CONNECTION \
  --out /private/operator/sql-validation-summary.json

unset TRACEMAP_SQL_VALIDATION_CONNECTION
```

The output file must not already exist. Normal console output contains only a
completion classification and assertion count. A connection or provider
failure never prints provider details. A connection-level failure makes all
configured checks `observed-indeterminate`; an individual catalog denial makes
that check indeterminate while preserving other bounded observations.

Before connecting, validate the plan and artifact path with a dry run:

```bash
dotnet run --project src/dotnet/TraceMap.SqlValidation.Cli -- validate \
  --plan samples/sql-validation-harness/plan.example.json \
  --out /tmp/sql-validation-summary.json \
  --dry-run
```

Dry run never reads a connection environment variable and emits every assertion
as `not-run`.

## Disposable PostgreSQL integration smoke

For an explicit local integration check against a synthetic PostgreSQL 16.8
server, run:

```bash
./scripts/smoke-sql-validation-postgres.sh
```

The script uses the official PostgreSQL 16.8 Alpine image pinned by digest, a
random loopback-only port, no host volume, and a disposable synthetic role and
fixture. It checks deterministic `observed-pass`, `observed-fail`, and `not-run`
outcomes, verifies public-safe projection boundaries, and removes the container
and scratch artifacts on exit. Its trust authentication setting is acceptable
only inside that isolated disposable container and must not be copied to a
persistent or externally reachable server.

This smoke does not exercise RDS, `pg_cron`, `dblink` connectivity, function or
job execution, migrations, data movement, rollback, or private infrastructure.

## Compose

Supply the generated summary explicitly to a scan or release review and use an
explicit evaluation time:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo /path/to/repository \
  --out /tmp/tracemap-scan \
  --sql-validation-summary /private/operator/sql-validation-summary.json \
  --sql-validation-as-of 2026-07-22T12:00:00Z
```

TraceMap verifies the digest, repository, commit, context, validator version,
and freshness before composing categorical observations separately from static
evidence. The summary is a transport artifact rather than a standalone finding;
ingestion assigns the documented observation or gap rule ID. The summary must
be handled according to the operator's artifact retention policy even though it
is designed to be public-safe.

## Non-claims

An `observed-pass` is narrow and point-in-time. It does not establish continuing
state, safe execution, correct data, successful jobs, replication health,
rollback, release approval, or DBA/operator attestation.
