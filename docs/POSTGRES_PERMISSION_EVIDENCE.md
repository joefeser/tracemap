# PostgreSQL Permission Prerequisite Evidence

TraceMap indexes supported PostgreSQL permission statements and compares them
with static operation capability candidates. This is script-set evidence, not
an effective privilege calculator.

The versioned `postgres-permission-prerequisites/v1` registry covers extension
creation, foreign server creation/usage, user mappings, foreign schema import,
foreign table creation, publication/subscription setup, and `pg_cron`
scheduling. Administrative capabilities remain `needs-owner-review`; registry
entries are deterministic review guidance rather than authoritative PostgreSQL,
RDS, provider, or version-specific authorization rules.

Supported statement evidence includes `GRANT`, `REVOKE`, `ALTER ... OWNER TO`,
`ALTER DEFAULT PRIVILEGES`, and role membership for database, schema, table,
sequence, routine, foreign-server, and foreign-wrapper categories. Raw object,
role, server, database, and infrastructure names are omitted. One-way keys from
non-secret SQL identifiers are used only where exact object linking is needed.

Coverage statuses are:

- `present-in-scripts`: compatible checked-in grant evidence exists;
- `missing-evidence`: no compatible statement was linked;
- `conflicting-evidence`: revoke or grant/revoke evidence conflicts;
- `unknown`: input identity, context, or parsing coverage is reduced;
- `needs-owner-review`: administrative capability, cross-file order, or
  statement order requires human validation.

`present-in-scripts` does not mean a permission is active, effective,
sufficient, or granted to the runtime role. The reducer does not simulate
transactions, branches, stored procedures, default privilege expansion, role
inheritance, RLS/policies, live catalog state, or cloud IAM. It never generates
or executes a grant and does not replace DBA/operator approval.
