# PostgreSQL Archive-Link Evidence

TraceMap classifies checked-in PostgreSQL archive-link statements without
connecting to a database or executing SQL. The PostgreSQL-first mechanisms are
`postgres-fdw`, `dblink`, `logical-publication`, `logical-subscription`, and
`pg-cron-scheduled-operation`.

Surface facts retain only mechanism, categorical surface/context, statement
ordinal, repo-relative span, coverage, limitation, a span-derived opaque
identity, and (where required for exact linking) a one-way key derived from a
non-secret SQL object identifier. Raw server, publication, database, role,
host, connection, subscription, job, remote-query, and scheduled-command values
are omitted. User mappings, dblink inputs, and subscription connections
continue to use the protected-material boundary.

Non-protected statements may retain a hash of structural SQL after comments and
quoted/dollar-quoted contents are removed. Protected-material statements remain
span-only and hash-free. Reduced prerequisite surfaces are labeled
`reduced-static-evidence` and do not create an archive-link edge.

Prerequisite reduction checks only the analyzed script set. It can establish
static evidence for extension declarations, foreign servers, user mappings,
and publications, or emit `missing-evidence`. Missing evidence is not a claim
that a live PostgreSQL/RDS object or capability is absent.

Direction is emitted only when compatible explicit execution-context
declarations establish `source` and `archive-target` roles. Inferred, unknown,
conflicting, dynamic, or malformed evidence remains reduced and produces a
cataloged gap. Static edges do not prove connectivity, applied state,
permissions, replication health, job registration/execution, or archive
correctness, and they do not replace DBA/operator approval.
