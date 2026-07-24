# SQL Validation Harness v1 Design

## Boundary

```text
strict local plan + explicitly named connection environment variable
  -> validate all non-secret inputs before connection
  -> read-only transaction
  -> compiled-in catalog queries with parameters only
  -> categorical outcomes
  -> canonical public-safe sql-validation-summary/v1 artifact
```

The harness is a separate executable. TraceMap ingestion remains offline and
does not gain database connectivity.

## Plan contract

`sql-validation-plan/v1` is intentionally local-only. It may contain private
PostgreSQL identifiers needed as parameter values, but it cannot contain SQL,
credentials, hosts, database names, or connection material. Unknown properties
and duplicate check codes are rejected.

Supported configured checks are:

| Assertion | Local input | Observation |
| --- | --- | --- |
| `postgres.server-version-compatible` | expected major version | server major equals expected |
| `postgres.required-extension-available` | extension names | every name is installed |
| `postgres.migration-schema-present` | qualified relation names | every relation resolves |
| `postgres.permission-probe-authorized` | schema names | current user has `USAGE` on every schema |
| `postgres.archive-function-callable` | qualified function signatures | every function resolves and current user has `EXECUTE` |
| `postgres.scheduled-job-registered` | job names | `cron.job` exists and every named job is active |

All other summary assertion codes are always emitted as `not-run` in v1.

## Execution safety

- The Npgsql provider is isolated in the harness project.
- The connection string is loaded from a safe environment-variable name and
  is never placed in a model, artifact, message, or log.
- The connection opens with pooling disabled and a bounded timeout.
- Every probe runs in its own read-only transaction with a bounded command
  timeout, so a denied catalog or missing optional surface makes only that
  assertion indeterminate.
- SQL strings are compiled into the executable. Plan values are parameters;
  they are never concatenated into command text.
- Only scalar booleans or the server version integer are consumed. Rows and
  identifiers are never rendered.
- Connection and provider failures become safe classifications and configured
  assertions become `observed-indeterminate`.

## Determinism and output

The plan supplies all timestamps and identity values. Assertions and
limitations are sorted by ordinal code. The artifact digest uses recursively
key-sorted compact JSON with the digest field blank, matching the v1 reader.
The output file is created with create-new semantics.

The summary is a transport artifact, not a standalone TraceMap finding. On
ingestion, each accepted assertion receives
`database.sql.validation-summary.observation.v1`; rejected input receives
`database.sql.validation-summary.gap.v1`. Consumers must not present the raw
transport assertion as rule-backed evidence without that composition step.

Dry run validates the plan without reading the connection environment variable
and emits all assertions as `not-run`.

## Limitations

The digest detects post-production modification but does not authenticate the
machine or operator. Catalog and privilege checks are point-in-time and do not
execute migrations, functions, jobs, dblink connections, or application
queries. No result means the target is safe, healthy, or approved.
