# PostgreSQL Archive-Link Evidence Requirements

## Introduction

PostgreSQL archive workflows can span `postgres_fdw`, `dblink`, logical
replication, and scheduled `pg_cron` operations. Their checked-in scripts expose
useful static dependency evidence, but that evidence does not prove that a
remote is reachable, an extension is installed, replication is healthy, or a
scheduled job ran.

This spec defines a PostgreSQL-first archive-link lane over the execution
context contract. It is spec-only and adds no production implementation.

Related issues: [#454](https://github.com/joefeser/tracemap/issues/454),
[#453](https://github.com/joefeser/tracemap/issues/453),
[#435](https://github.com/joefeser/tracemap/issues/435),
[#437](https://github.com/joefeser/tracemap/issues/437), and
[#438](https://github.com/joefeser/tracemap/issues/438).

## Scope

In scope:

- Static PostgreSQL evidence for `postgres_fdw`, `dblink`, logical replication,
  and `pg_cron` archive-related scheduling surfaces.
- Categorical source, archive-target, admin, scheduled, and validation contexts
  when declared or structurally supported.
- Object/dependency candidates, prerequisites, safe identities, gaps, and
  deterministic cross-statement links.
- RDS-relevant capability gaps only when checked-in evidence explicitly
  declares them; no cloud-account inference.

Out of scope:

- Network access, database connections, remote validation, replication status,
  extension installation, or job execution.
- Proof that an archive link is correctly configured, secure, reachable, or in
  use.
- Raw hostnames, database names, usernames, passwords, connection strings,
  subscription connection data, or scheduled SQL bodies in default output.
- DBA approval, provider-specific operational advice, or SQL execution.
- Non-deterministic or AI-based classification.

## Requirements

### Requirement 1: Archive-Link Surface Classification

**User Story:** As a database reviewer, I want the script set grouped by link
mechanism so I can see what kind of cross-database setup it intends.

Acceptance Criteria:

1. TraceMap SHALL classify recognized PostgreSQL surfaces as `postgres-fdw`,
   `dblink`, `logical-publication`, `logical-subscription`, or
   `pg-cron-scheduled-operation`.
2. `postgres-fdw` SHALL recognize extension declarations, foreign data wrapper
   servers, user mappings, foreign schema imports, foreign tables, and related
   server/schema grants when structurally visible.
3. `dblink` SHALL recognize extension declarations and known dblink call
   surfaces while treating connection arguments as unsafe.
4. Logical replication SHALL recognize publication, subscription, and related
   table membership statements without retaining subscription connection data.
5. `pg_cron` SHALL be an execution/scheduling surface linked to a safe operation
   category or hash, never stored scheduled-command text.

### Requirement 2: Context and Direction

**User Story:** As an operator, I want to know which side of an archive link a
step appears to configure.

Acceptance Criteria:

1. Each surface SHALL carry `source`, `archive-target`, `admin`, `scheduled`,
   `validation-only`, or `unknown` context from the execution-context contract.
2. Direction SHALL be `source-to-archive`, `archive-to-source`,
   `bidirectional-candidate`, or `unknown`, and SHALL be emitted only from an
   explicit declaration or deterministic relationship between categorized
   steps.
3. Object names alone SHALL NOT determine source/target direction.
4. Unknown or conflicting direction SHALL emit review-tier/gap evidence rather
   than an arbitrary edge.
5. Cross-context edges SHALL retain the supporting fact IDs and limitations.

### Requirement 3: Safe Object and Dependency Evidence

**User Story:** As a maintainer, I want dependency rows without exposing private
infrastructure.

Acceptance Criteria:

1. Archive-link facts SHALL use safe object kinds, statement spans, stable
   opaque object identities, mechanism, context, and prerequisite codes.
2. Safe public fixture identifiers MAY render after passing the shared safe
   identifier policy; production-like server, database, role, host, and remote
   values SHALL be omitted or represented categorically.
3. User mappings and connection-bearing constructs SHALL link to a
   secret-bearing-step finding without copying their option values.
4. The reducer SHALL not infer a remote dependency from a string literal alone.
5. Facts SHALL include rule ID, evidence tier, repo-relative file span, commit
   SHA, extractor version, coverage, and documented limitations.

### Requirement 4: Prerequisites and Gaps

**User Story:** As a reviewer, I want absent evidence to be visible without it
being mistaken for a live configuration failure.

Acceptance Criteria:

1. The lane SHALL identify static prerequisite candidates for required
   extensions, server definitions, user mappings, schema imports, permissions,
   publications/subscriptions, and scheduler extension/configuration.
2. When a referenced prerequisite is not present in the analyzed script set,
   TraceMap SHALL emit `missing-evidence`, not `missing-at-runtime`.
3. Unsupported options, dynamic identifiers, procedural SQL, ambiguous object
   links, parse failure, and unknown context SHALL produce cataloged gaps and
   reduced coverage.
4. RDS capability/role caveats SHALL appear only from explicit declarations or
   recognized checked-in configuration evidence and SHALL be labeled as
   prerequisites requiring owner validation.
5. A failed or partial archive-link parse SHALL not be reported as no link.

### Requirement 5: Rules, Tiers, and Limitations

**User Story:** As a TraceMap user, I want every archive-link conclusion to show
exactly what supports it.

Acceptance Criteria:

1. Every emitted surface, edge, prerequisite, and gap SHALL have an owning rule
   in `rules/rule-catalog.yml` before implementation merges.
2. Recognized PostgreSQL statement structure MAY be `Tier2Structural`; textual
   fallback SHALL be `Tier3SyntaxOrTextual`; unresolved links and missing
   evidence SHALL be `Tier4Unknown`.
3. Rule limitations SHALL state that static script evidence does not prove
   connectivity, object existence, applied state, permissions, replication
   health, scheduling, execution, or archive correctness.
4. No edge SHALL be upgraded from static to runtime proof without a separately
   scoped validation artifact with provenance.

### Requirement 6: Synthetic Acceptance Tests

**User Story:** As a contributor, I want representative PostgreSQL fixtures
that contain no real infrastructure or credentials.

Acceptance Criteria:

1. Synthetic fixtures SHALL separately cover FDW, dblink, publication/
   subscription, and `pg_cron` surfaces using neutral identifiers.
2. Tests SHALL cover complete declared context, unknown context, conflicting
   direction, missing extension/server/mapping/grant evidence, dynamic options,
   and partial parsing.
3. Tests SHALL prove `CREATE USER MAPPING`, dblink connection input,
   subscription connection input, and scheduled command input produce no raw
   values in facts, SQLite, reports, or logs.
4. Cross-statement linking tests SHALL be stable across file order changes and
   repeated scans.
5. A CLI fixture scan SHALL show archive-link rows and explicit limitations
   while making no runtime success claim.
