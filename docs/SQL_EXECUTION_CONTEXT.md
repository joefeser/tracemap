# SQL Execution Context Evidence

TraceMap extracts deterministic intended execution-context evidence from
checked-in PostgreSQL `.sql` files. It does not connect to a database, execute
SQL, inspect the active client tab, certify a script as safe, or replace
operator/DBA approval.

## Contract

The v1 contract identifier is `sql-execution-context/v1`. A script can declare
context in a companion JSON file named:

```text
<script>.sql.tracemap-sql-context.json
```

The root accepts only `schemaVersion` and `steps`. Each step requires a positive
`statementOrdinal` and may declare these closed fields:

- `engineFamily`: `postgresql` or `unknown`
- `serverRole`: `source`, `archive-target`, `admin`, or `unknown`
- `databaseRole`: `source-data`, `archive-data`, `admin`, `validation-only`, or
  `unknown`
- `schemaRole`: `application`, `archive`, `extension`, `unspecified`, or
  `unknown`
- `executionMode`: `manual`, `scheduled`, `validation-only`, or `unknown`
- `stepKind`: one of the cataloged setup, permission, validation, scheduled,
  destructive, or unknown step kinds
- `requiredCapabilities` and `stopConditions`: closed cataloged code arrays

Unknown fields, versions, values, duplicate ordinals, or malformed JSON produce
`database.sql.context.gap.v1` evidence and reduced coverage. Values from an
invalid sidecar are not rendered.

## Bounded Directive

For small scripts, a line immediately before a statement can use the exact
bounded grammar:

```sql
-- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
```

Keys and values are closed. Comma separates multiple capability or stop codes.
Ordinary comments are not declarations. A sidecar declaration has precedence;
conflicting declarations emit a gap.

## Static Inference

Recognized PostgreSQL statement shapes include extension setup, FDW/server
setup, user mapping, schema import, foreign tables, grants/revokes,
publications, subscriptions, `pg_cron`, validation queries, and destructive
operations. Unresolved server/database roles remain `unknown` and produce
missing-context evidence for context-sensitive steps.

Context facts store categorical metadata, line spans, rule IDs, evidence tiers,
commit SHA, extractor version, and a hash of structural tokens after comments
and quoted/dollar-quoted bodies are removed. They do not store raw SQL,
directive bodies, sidecar values, connection strings, credentials, scheduled
command bodies, or infrastructure identities.

## Human-Factor Boundary

The scan report groups ordered context rows and highlights transitions between
server, database, or execution modes. Every manual workflow still requires an
operator to verify the active connection, server, database, schema, and role.
Absence of a context gap is not a statement that a script is safe to run.
