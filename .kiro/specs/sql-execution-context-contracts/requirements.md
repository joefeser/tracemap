# SQL Execution Context Contracts Requirements

## Introduction

SQL setup scripts are often correct only in a specific engine, server role,
database, schema, and operator-role context. A script opened in the wrong
pgAdmin tab can therefore fail or mutate the wrong surface even when its SQL is
otherwise valid. TraceMap should make the intended context and missing context
evidence reviewable before execution without claiming that a script is safe or
that any database state exists.

This is a spec-only slice. Product code is out of scope for this branch.

Related issues: [#453](https://github.com/joefeser/tracemap/issues/453),
[#435](https://github.com/joefeser/tracemap/issues/435), and
[#438](https://github.com/joefeser/tracemap/issues/438).

## Scope

In scope:

- PostgreSQL-first static analysis of checked-in `.sql` files and an optional
  checked-in sidecar manifest.
- Expected engine, server role, database context, schema context,
  role/capability, step kind, ordering, validation-only markers, and stop
  conditions.
- Explicit declarations plus conservative syntax-derived context candidates.
- Human-factor warnings for manual clients such as pgAdmin when context is
  absent, conflicting, or changes between adjacent steps.
- Rule-backed facts and `AnalysisGap` evidence with safe file spans, hashes,
  commit SHA, extractor version, evidence tier, coverage, and limitations.

Out of scope:

- SQL execution, database connections, schema introspection, credential use, or
  runtime role inspection.
- Proof that the selected tab, server, database, role, or transaction is
  correct at execution time.
- A claim that a script is safe, idempotent, authorized, or successfully run.
- Replacement of DBA review or human approval.
- LLM calls, embeddings, vector databases, or prompt-based classification.
- Raw SQL, connection strings, secrets, local absolute paths, private server or
  database names, or private ticket details in default outputs.

## Requirements

### Requirement 1: Versioned Context Contract

**User Story:** As an operator, I want each script step to state where it is
intended to run so I can detect a context mismatch before manual execution.

Acceptance Criteria:

1. TraceMap SHALL define a versioned `sql-execution-context/v1` contract with
   optional fields for `engineFamily`, `serverRole`, `databaseRole`,
   `schemaRole`, `requiredCapabilities`, `stepKind`, `executionMode`, and
   `stopConditions`.
2. Closed v1 values SHALL include PostgreSQL, `source`, `archive-target`,
   `admin`, `validation-only`, and `unknown` where applicable; literal
   infrastructure identities SHALL not be required.
3. A contract value SHALL record whether it came from an explicit sidecar,
   bounded comment directive, file structure, or SQL syntax candidate.
4. Explicit declarations SHALL not be silently overridden by syntax evidence;
   conflicts SHALL emit a rule-backed `AnalysisGap`.
5. The sidecar schema and supported comment directive grammar SHALL be
   documented, deterministic, and reject unknown fields or versions with a
   reduced-coverage gap.

### Requirement 2: Static Context Evidence

**User Story:** As a reviewer, I want context inferred only when the script
contains deterministic evidence.

Acceptance Criteria:

1. PostgreSQL context candidates MAY be derived from explicit statements such
   as `CREATE EXTENSION`, `CREATE SERVER`, `CREATE USER MAPPING`, `IMPORT
   FOREIGN SCHEMA`, grants, publications, subscriptions, and `pg_cron`
   scheduling calls.
2. Syntax-derived context SHALL be capped at `Tier2Structural` for a recognized
   PostgreSQL construct and `Tier3SyntaxOrTextual` for bounded text-only
   detection.
3. A declaration SHALL describe intended context, not observed runtime state.
4. When engine or database context cannot be established, TraceMap SHALL emit
   `Tier4Unknown` gap evidence rather than assume the current connection.
5. Context detection SHALL preserve useful step evidence when parsing is
   partial and label coverage `reduced`.

### Requirement 3: Stop and Review Conditions

**User Story:** As an operator using a manual SQL client, I want ambiguity to be
visible before I press the run button.

Acceptance Criteria:

1. The context reducer SHALL classify a step as `declared`, `inferred`,
   `conflicting`, `missing-evidence`, or `unknown`.
2. `conflicting`, unknown engine, unknown database role for a context-sensitive
   operation, and secret-bearing context dependencies SHALL produce explicit
   stop/needs-review evidence under a cataloged rule.
3. Adjacent steps that require different server or database roles SHALL be
   shown as a context transition, not merged into one runnable block.
4. Reports SHALL include a manual-client warning that the active connection and
   database must be independently verified by the operator.
5. TraceMap SHALL never phrase a stop-condition-free result as safe to run.

### Requirement 4: Evidence Safety and Determinism

**User Story:** As a maintainer, I want context evidence to be publishable
without exposing infrastructure or credentials.

Acceptance Criteria:

1. Proposed rules SHALL be added to `rules/rule-catalog.yml` before emission,
   with fact types, tiers, safe properties, and limitations.
2. Default facts and reports SHALL use categorical roles and hashes; they SHALL
   omit raw SQL, connection strings, credentials, hostnames, raw database names,
   local absolute paths, and comment bodies.
3. Secret-like values SHALL be omitted or category-only, not hashed into an
   enumerable evidence field.
4. Repeated scans of the same commit and fixture SHALL produce stable fact IDs,
   ordering, classifications, and report output.
5. Every conclusion SHALL retain rule ID, evidence tier, repo-relative file
   span, commit SHA, extractor version, coverage, and limitation.

### Requirement 5: Synthetic Validation

**User Story:** As a contributor, I want public-safe fixtures that prove context
behavior without relying on a live database.

Acceptance Criteria:

1. Tests SHALL use synthetic PostgreSQL scripts with neutral categorical names
   for source, archive-target, admin, and validation contexts.
2. Fixtures SHALL cover explicit sidecars, bounded directives, syntax-only
   inference, missing context, conflicting context, adjacent context
   transitions, `pg_cron`, and unsupported dialect input.
3. Tests SHALL model a wrong-tab/wrong-database risk and assert that the report
   emits a stop/needs-review condition without claiming runtime mismatch.
4. Leak tests SHALL plant credential-like values, connection strings,
   hostnames, raw SQL markers, and absolute paths and prove they do not appear
   in facts, SQLite properties, reports, logs, or committed fixture output.
5. Validation SHALL include deterministic repeated scans and rule-catalog
   coverage for every emitted classification and gap.
