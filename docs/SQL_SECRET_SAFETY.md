# SQL Protected-Material Safety

TraceMap statically identifies SQL steps whose handling may expose credentials or
connection material. It emits category-only evidence for supported PostgreSQL
surfaces and never connects to a database or executes SQL.

The v0 classifications are:

- `secret-bearing`: a supported structural position contains inline material.
- `secret-reference`: a supported position refers to an external value.
- `possible-secret`: textual evidence requires owner review.
- `not-established`: dynamic, malformed, unreadable, or unsupported evidence
  failed closed.

Findings contain rule and tier, closed category codes, safe relative line spans,
statement ordinal, coverage, limitation, and `secret-owner-review`. Raw SQL,
option values, connection strings, server/database/user names, secret values,
and secret-derived hashes are omitted. Existing SQL context and shape extractors
also suppress hashes at protected-material boundaries.

Supported PostgreSQL-first surfaces include `CREATE USER MAPPING`, credential
options on `CREATE SERVER`, dblink connection inputs, subscription connection
inputs, credential-like content in `pg_cron` scheduled commands, and active
credential-assignment comments. Dynamic concatenation and malformed high-risk
statements produce explicit reduced-coverage gaps.

These facts do not prove that a value is valid, that it is used at runtime, that
a repository contains no other secrets, or that a script is safe to run. Absence
of a finding does not prove absence of secrets. Human/DBA approval and independent
verification of the active pgAdmin connection remain required.
