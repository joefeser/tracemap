# SQL Secret-Bearing Step Safety Requirements

## Introduction

Some SQL setup steps require credentials or connection material. TraceMap needs
to identify that a step is secret-bearing without storing, hashing, logging, or
rendering the secret. This spec defines a deny-by-default safety boundary for
SQL evidence; it does not validate or manage credentials.

This is a spec-only slice. Related issues:
[#455](https://github.com/joefeser/tracemap/issues/455),
[#453](https://github.com/joefeser/tracemap/issues/453),
[#454](https://github.com/joefeser/tracemap/issues/454), and
[#438](https://github.com/joefeser/tracemap/issues/438).

## Scope

In scope:

- PostgreSQL-first structural detection of credential/connection-bearing fields
  and conservative textual fallback.
- Category-only findings, safe spans, stop/owner-review conditions, and leak
  prevention across facts, SQLite, reports, logs, errors, tests, and exports.
- User mappings, FDW/dblink/replication connection options, variable or
  placeholder boundaries, and credential-like comments when safely detectable.
- A reusable SQL secret-boundary contract for future engines.

Out of scope:

- Secret storage, encryption, transformation, validation, rotation, injection,
  retrieval, or generation.
- Hashing raw secrets or low-entropy credentials into output.
- Reconstructing or emitting runnable secret-bearing SQL.
- Claims that unflagged SQL is free of secrets or safe to share/run.
- Live databases, environment-variable reads, vault access, or cloud secret
  manager integration.

## Requirements

### Requirement 1: Secret-Bearing Step Classification

**User Story:** As a security-conscious reviewer, I want to know which steps
require protected handling without seeing their contents.

Acceptance Criteria:

1. TraceMap SHALL classify a statement as `secret-bearing`,
   `secret-reference`, `possible-secret`, or `not-established`; it SHALL NOT
   classify a statement as proven secret-free.
2. Structural detection SHALL cover PostgreSQL user mapping options, dblink
   connection inputs, subscription connection inputs, foreign server options
   commonly carrying connection material, and recognized credential option
   keys.
3. Placeholder/reference forms SHALL be `secret-reference` when only a safe
   provider category is visible; the variable name/value SHALL be omitted when
   unsafe.
4. Textual fallback MAY emit `possible-secret` at `Tier3SyntaxOrTextual`, but it
   SHALL ignore keyword-like content inside safely tokenized unrelated strings
   where possible.
5. Parser failure or dynamic construction SHALL emit `not-established`/
   `Tier4Unknown` gap evidence, not a clean result.

### Requirement 2: Safe Evidence Contract

**User Story:** As a maintainer, I want findings useful enough to locate a step
without retaining secret material.

Acceptance Criteria:

1. A secret-bearing finding SHALL contain only rule ID, classification,
   category codes, repo-relative file span, statement ordinal, non-secret
   statement-shape hash where permitted, evidence tier, commit SHA, extractor
   version, coverage, and limitation.
2. Raw option values, credentials, connection strings, SQL text, comments,
   variable values, hostnames, usernames, database names, and private server
   names SHALL never be stored in the finding.
3. Raw secrets and secret-like values SHALL be omitted, not hashed.
4. Diagnostics and exception handling SHALL sanitize source-derived tokens
   before logging or rendering an error.
5. High-risk step spans SHALL remain usable for local review while reports use
   repo-relative paths only.

### Requirement 3: Stop and Owner-Handling Behavior

**User Story:** As an operator, I want the packet to stop before it accidentally
publishes or auto-runs a credential-bearing step.

Acceptance Criteria:

1. `secret-bearing`, `secret-reference`, and unresolved possible-secret steps
   SHALL create a stop/owner-review condition for generated runbook packets.
2. Reports SHALL say that protected material is required and must follow an
   owner-approved handling path; they SHALL not suggest a concrete secret value
   or storage mechanism.
3. Secret findings SHALL not be converted into executable SQL, copyable command
   blocks, environment assignments, or connection instructions.
4. TraceMap SHALL state that absence of a finding does not prove absence of
   secrets.
5. Human approval remains required and is not represented as satisfied by a
   static scan.

### Requirement 4: Rule Ownership and Coverage

**User Story:** As a TraceMap user, I want the secret boundary to be explicit
and testable.

Acceptance Criteria:

1. Every classification, category, redaction reason, and gap SHALL be owned by
   a documented rule before emission.
2. Rules SHALL document false-negative limitations for dynamic SQL, encoded or
   split values, custom functions, external includes, procedural SQL, and
   unsupported dialects.
3. Structural detection MAY be `Tier2Structural`; textual detection SHALL be no
   stronger than `Tier3SyntaxOrTextual`; unresolved safety SHALL be
   `Tier4Unknown`.
4. Secret safety SHALL fail closed for rendering: an unsafe property is omitted
   even if doing so reduces identity precision.
5. Reduced identity precision SHALL be labeled and SHALL not cause unrelated
   high-risk steps to collapse silently.

### Requirement 5: Leak-Proof Synthetic Tests

**User Story:** As a contributor, I want tests that prove planted secrets cannot
escape through any supported artifact.

Acceptance Criteria:

1. Synthetic fixtures SHALL plant unique sentinel values in password, user,
   host, database, connection string, token, comment, variable, dblink,
   subscription, user mapping, and scheduled-command positions.
2. Tests SHALL search `facts.ndjson`, `index.sqlite` properties, `report.md`,
   analyzer logs, CLI stdout/stderr, exports, combined reports, and exceptions
   for both full and distinctive partial sentinels.
3. Tests SHALL prove raw sentinels are not hashed into output fields intended
   for secret values.
4. Fixtures SHALL include false-positive controls, mixed case, comments,
   dollar-quoted bodies, dynamic concatenation, malformed SQL, and unsupported
   dialect input.
5. Repeated scans SHALL emit deterministic category-only findings and stable
   ordering without retaining raw secret material.
