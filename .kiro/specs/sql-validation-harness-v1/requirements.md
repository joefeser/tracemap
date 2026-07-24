# SQL Validation Harness v1 Requirements

## Goal

Produce the `sql-validation-summary/v1` artifact accepted by TraceMap from a
separately invoked, operator-controlled PostgreSQL validation harness.

## Requirements

1. The harness must not run from `scan`, `combine`, or `release-review`.
2. The harness must accept a strict local `sql-validation-plan/v1` document,
   read a connection string only from an explicitly named environment
   variable, and write one new summary file.
3. The plan must bind repository, commit SHA, observation and expiry times,
   categorical target context, and a unique safe artifact ID.
4. The harness must execute only compiled-in, parameterized, read-only catalog
   probes. It must reject unknown properties, duplicate checks, unsupported
   check codes, unsafe identifiers, and invalid time windows before connecting.
5. V1 may observe only:
   - PostgreSQL major-version compatibility;
   - required installed extensions;
   - expected migration/catalog relations;
   - bounded schema `USAGE` privileges;
   - registered function identity plus current-user `EXECUTE` privilege; and
   - active `pg_cron` job registration.
6. Archive-link connectivity, arbitrary validation queries, function
   invocation, cleanup mutation, and target-schema compatibility must remain
   `not-run` in v1. The harness must not accept SQL text.
7. Probe results must map only to `observed-pass`, `observed-fail`,
   `observed-indeterminate`, or `not-run`. Provider error text must never be
   emitted.
8. The output must satisfy `sql-validation-summary/v1`, include all closed
   assertion codes exactly once, use validator identity
   `tracemap.sql-validation-harness` version `1.0.0`, and contain the canonical
   SHA-256 digest expected by the ingestion reader.
9. Normal output and artifacts must not contain connection strings,
   credentials, plan paths, target identifiers, host/database names, SQL text,
   query results, or provider exception messages.
10. The harness must support a no-connection dry run that emits honest
    `not-run` statuses and enables deterministic cross-platform validation.
11. Tests must prove strict parsing, deterministic output, parameter-only probe
    input, failure classification, ingestion compatibility, and secret/private
    value non-disclosure.

## Non-claims

- The harness does not establish continuing database state.
- It does not approve a release or replace an operator.
- It does not claim scripts or migrations are safe to run.
- Catalog presence and privilege observations do not prove runtime execution,
  business correctness, scheduling success, replication health, or rollback.
