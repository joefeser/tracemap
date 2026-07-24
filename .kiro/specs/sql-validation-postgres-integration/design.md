# SQL Validation PostgreSQL Integration Design

## Boundary

The integration is an explicit local smoke, not routine CI and not part of a
TraceMap scan. It starts a digest-pinned PostgreSQL 16.8 container with no
volume and a random `127.0.0.1` port. Trust authentication is limited to this
disposable synthetic container; no credential is created or retained.

The fixture setup creates synthetic schemas, a catalog relation, a login role,
and a non-mutating function definition. The shipped harness then connects as
the synthetic role and executes only its compiled-in, parameterized,
read-only catalog probes.

## Assertions

The positive plan proves version, installed-extension, relation, schema-usage,
and callable-function observations. The intentionally absent cron catalog
proves an observed failure. The four non-executable v1 codes remain `not-run`.

The negative plan proves all six executable codes can report
`observed-fail`. A repeated positive run must be byte-identical. A final
projection check rejects leakage of identifiers, connection material, and
fixture database details.

## Limitations

This proves harness behavior against one disposable PostgreSQL build. It does
not exercise RDS, `pg_cron`, `dblink` connectivity, function bodies, job
execution, migrations, data movement, rollback, or any private environment.
