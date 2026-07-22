# SQL Validation Source/Archive Operator Packet

This packet turns the bounded `tracemap-sql-validation` catalog probes into a
repeatable handoff for a two-database PostgreSQL archive rollout. It does not
create a database, install extensions, run migrations, create functions,
register jobs, move data, or approve a release.

## Choose the target before connecting

Start from one parser-valid public template:

- `samples/sql-validation-harness/archive-target-plan.template.json` checks the
  archive target's major version, required installed extension, migration
  relation, and schema usage.
- `samples/sql-validation-harness/source-plan.template.json` checks the source
  server's major version, required installed extensions, schema usage, callable
  archive function, and registered active job.

Copy each template to a private operator directory. Do not edit the checked-in
sample with real object names. Before every run, bind the private copy to:

1. the repository and exact commit that supplied the static SQL evidence;
2. a unique artifact ID;
3. explicit observation and expiry timestamps;
4. the intended closed target context;
5. the expected PostgreSQL major version and only the identifiers needed by
   the fixed catalog checks.

The plan cannot contain host/database names, credentials, connection strings,
SQL, ticket notes, or arbitrary prose. Treat its allowlisted object identifiers
as private operator input even though generated summaries omit them.

## Responsibility and order

| Checkpoint | Owner evidence needed before proceeding | TraceMap observation |
|---|---|---|
| Archive target provisioned | Infrastructure owner identifies the intended target and version. | Version equality only. |
| Archive migrations applied | Migration owner identifies the expected catalog relation. | Relation resolves only. |
| Archive role prepared | Database owner identifies the schema and validation login. | Current user has schema `USAGE` only. |
| Source prerequisites prepared | Database owner identifies required installed extensions. | Named extensions appear in `pg_extension` only. |
| Source function prepared | Migration owner identifies the function signature. | Signature resolves and current user has `EXECUTE`; the function is not called. |
| Schedule prepared | Scheduler owner identifies the job name. | `cron.job` exists and the named job is active; the job is not run. |

Keep archive-target and source results as separate artifacts. A passing archive
target result does not authorize source changes, and a passing source result
does not prove the archive target is compatible or reachable.

## Run sequence

1. Validate each private plan without a connection using `--dry-run`. Every
   status should be `not-run`; this validates shape, not database state.
2. Independently confirm the active client/server, database, role, and target
   purpose. TraceMap cannot observe a pgAdmin tab or other operator selection.
3. Put the least-privilege connection string in an explicitly named environment
   variable, run one target plan, and unset the variable immediately.
4. Inspect only the categorical summary and completion classification. Preserve
   provider output outside TraceMap under the organization's own handling rules.
5. Compose both summaries with the matching static scan and an explicit
   `--sql-validation-as-of` time. Context, commit, expiry, digest, and validator
   mismatches remain gaps rather than being coerced into pass results.

See [`SQL_VALIDATION_HARNESS.md`](SQL_VALIDATION_HARNESS.md) for exact commands.
Output paths are create-new: choose a new path for every attempt rather than
overwriting prior evidence.

## Stop and interpretation rules

- `observed-pass` means only that one fixed catalog predicate was true for that
  login at the recorded time.
- `observed-fail` is not a repair instruction. Stop and return the named
  assertion code to the responsible owner.
- `observed-indeterminate` means the harness could not make that bounded
  observation. Do not relabel it as pass or fail from terminal prose.
- `not-run` is expected for archive-link connectivity, target-schema
  compatibility, arbitrary validation queries, and cleanup observation in v1.

No combination of these results establishes connectivity, correct data,
successful migrations, function behavior, job execution, rollback, continuing
state, safe execution, release approval, or DBA/operator attestation.
