# PostgreSQL Archive-Link Evidence Design

## Overview

This slice consumes ordered SQL statements and context facts, then emits a
PostgreSQL-specific archive-link graph:

```text
contextual SQL statements
  -> PostgreSQL archive-link extractors
  -> safe objects + prerequisite candidates + gaps
  -> deterministic static dependency edges
```

It does not connect to RDS or PostgreSQL.

## Proposed Rules

Names are recommendations and must be reconciled with the live catalog before
implementation.

| Rule ID | Fact families | Tier ceiling | Limitation |
| --- | --- | --- | --- |
| `database.postgres.archive-link.v1` | `DatabaseLinkSurfaceDeclared`, `DatabaseLinkEdgeCandidate` | `Tier2Structural` | Static statement evidence only; no connectivity or applied-state proof. |
| `database.postgres.archive-link.prerequisite.v1` | `DatabasePrerequisiteCandidate` | `Tier2Structural` | A script prerequisite candidate, not live capability proof. |
| `database.postgres.archive-link.gap.v1` | `AnalysisGap` | `Tier4Unknown` | Missing/ambiguous checked-in evidence, not a runtime failure. |

The implementation should reuse existing SQL fact types when their semantics
fit rather than add parallel types. New properties require catalog ownership.

## Extractor Families

### postgres_fdw

Recognize bounded statement shapes for extension, foreign server, user mapping,
foreign schema import, foreign table, and relevant grants. Link by normalized
safe/opaque object identity and context. Server `OPTIONS` values are unsafe;
option keys may be category-only when useful.

### dblink

Recognize extension and known call forms. Arguments that could be connection
strings or remote SQL are omitted. A call can become a dblink surface without a
remote identity or target-table claim.

### Logical replication

Recognize publication/subscription declarations and explicit membership.
Subscription connection values are always secret-bearing input. Direction
requires context evidence; a publication name paired with a subscription name
is insufficient by itself.

### pg_cron

Recognize extension/config declarations and known schedule/unschedule call
forms. Retain statement span, safe job category, and an allowed deterministic
hash only; omit schedule command, connection data, and free-form job names when
unsafe. Scheduling evidence does not prove registration or execution.

## Identity and Linking

Stable object identity should combine repository source identity, commit SHA,
mechanism, categorical context, safe object kind, and a non-secret normalized
identity token. Secret-like and infrastructure values are not identity inputs;
when no safe identity exists, use source span plus a `VolatileIdentity`-style
limitation and keep confidence review-tier.

Edges link fact IDs and carry `direction`, `linkKind`, `coverage`, and
limitation. They do not render endpoints as network addresses.

## Prerequisite Reduction

The reducer builds a small expected-evidence graph per mechanism. For example,
an FDW schema import can look for extension, foreign server, mapping, and
permission evidence in compatible declared contexts. Absence produces
`missing-evidence`; it never claims the object or grant is absent from the live
database.

Precedence for ambiguous evidence is deterministic: secret/unsafe boundary,
parse failure, context conflict, ambiguous identity, missing checked-in
prerequisite, then complete static candidate.

## Safety

The extractor tokenizes before classification so keywords inside comments and
strings do not create surfaces. It must not store:

- host, port, database, user, password, or connection option values;
- dblink connection strings or remote query text;
- subscription connection data;
- scheduled command bodies;
- raw SQL or comments.

High-risk secret values are omitted, not hashed. Safe hashes may be used only
for non-secret statement/shape correlation under the shared SQL safety policy.

## Testing

Use one public-safe fixture family per mechanism plus mixed/context-conflict and
malformed cases. Assertions cover facts, links, missing-evidence gaps, tiers,
limitations, ordering, stable IDs, and leakage across NDJSON, SQLite, Markdown,
and logs. No fixture needs a PostgreSQL parser service, server, cloud account,
or credential.

## Future Engines and Validation

Engine-specific extractors sit behind a shared database-link contract so later
SQL Server linked servers, Oracle database links, or MySQL federation work does
not distort PostgreSQL semantics. Runtime validation, if ever added, must be a
separate artifact lane with explicit provenance and cannot overwrite static
facts.
