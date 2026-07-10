# SQL Permission Prerequisite Evidence Requirements

## Introduction

PostgreSQL setup scripts can depend on ownership, role membership, extension
capabilities, schema permissions, foreign-server usage, and object privileges.
Static analysis can show explicit grants and likely prerequisite candidates,
but it cannot prove the privileges of the live execution role.

This spec defines a conservative prerequisite reducer over SQL context and
archive-link evidence. It is spec-only.

Related issues: [#456](https://github.com/joefeser/tracemap/issues/456),
[#453](https://github.com/joefeser/tracemap/issues/453),
[#454](https://github.com/joefeser/tracemap/issues/454),
[#455](https://github.com/joefeser/tracemap/issues/455),
[#435](https://github.com/joefeser/tracemap/issues/435), and
[#438](https://github.com/joefeser/tracemap/issues/438).

## Scope

In scope:

- PostgreSQL-first extraction of `GRANT`, `REVOKE`, `ALTER OWNER`, `ALTER
  DEFAULT PRIVILEGES`, role membership, schema/table/sequence/function
  privileges, foreign-server usage, and extension-related capability evidence.
- Links between explicit permission statements and safely identified objects or
  categorical capabilities.
- Statuses `present-in-scripts`, `missing-evidence`, `conflicting-evidence`,
  `unknown`, and `needs-owner-review`.
- Context-sensitive prerequisite candidates for archive-link and `pg_cron`
  surfaces.

Out of scope:

- Runtime role introspection, effective privilege calculation, inherited role
  expansion from a live cluster, RLS/policy evaluation, or cloud IAM checks.
- Proof that an operator can execute a statement or that a grant is sufficient.
- Grant generation, execution, remediation, or DBA replacement.
- Raw role, server, database, connection, credential, or private infrastructure
  values in default reports.

## Requirements

### Requirement 1: Permission Statement Evidence

**User Story:** As a database owner, I want explicit permission statements
indexed as evidence with their limitations.

Acceptance Criteria:

1. TraceMap SHALL recognize supported PostgreSQL forms of `GRANT`, `REVOKE`,
   `ALTER ... OWNER TO`, `ALTER DEFAULT PRIVILEGES`, and role membership.
2. Supported object families SHALL include database, schema, table, sequence,
   function/procedure, foreign server, foreign data wrapper, and categorical
   extension capability where syntax provides evidence.
3. Facts SHALL record permission/capability codes, grant/revoke/owner/membership
   action, safe object kind/identity, declared context, statement span, rule,
   tier, commit SHA, extractor version, coverage, and limitation.
4. Role and infrastructure names SHALL be omitted, categorized, or represented
   through a non-secret safe identity policy; secret-like values SHALL never be
   hashed.
5. Unsupported grant options, dynamic identifiers, procedural SQL, and parsing
   ambiguity SHALL emit reduced-coverage gaps.

### Requirement 2: Prerequisite Candidate Rules

**User Story:** As an operator, I want to see which capabilities a setup step
appears to require before I attempt it.

Acceptance Criteria:

1. Each supported context-sensitive operation SHALL map to a cataloged set of
   prerequisite candidate codes, with PostgreSQL version/provider caveats where
   applicable.
2. Initial mappings SHALL cover extension creation, foreign server creation and
   usage, user mapping, foreign schema import, schema/table access, publication/
   subscription setup, ownership changes, and `pg_cron` scheduling.
3. A prerequisite candidate SHALL be labeled as static review guidance, not a
   definitive PostgreSQL authorization rule.
4. Candidate mappings SHALL be versioned and deterministic; changes require
   rule-catalog limitation updates and tests.
5. RDS-specific capability candidates SHALL require explicit RDS context
   declarations and SHALL remain `needs-owner-review`.

### Requirement 3: Present, Missing, Conflicting, and Unknown Evidence

**User Story:** As a reviewer, I want permission coverage summarized honestly
across a script set.

Acceptance Criteria:

1. `present-in-scripts` SHALL mean a compatible explicit permission statement
   exists in the analyzed evidence set; it SHALL not mean the permission is
   active in a database.
2. `missing-evidence` SHALL mean no compatible checked-in statement was linked;
   it SHALL not mean the live grant is missing.
3. A linked `REVOKE`, incompatible context, ambiguous role/object identity, or
   order-sensitive contradiction SHALL produce `conflicting-evidence` or
   `needs-owner-review`, not an arbitrary final permission state.
4. Unsupported or unsafe identity SHALL produce `unknown` while retaining the
   safe prerequisite category.
5. Reducer results SHALL retain supporting and contradicting fact IDs and
   deterministic reason codes.

### Requirement 4: Ordering and Partial State

**User Story:** As a reviewer, I want statement order represented without
pretending TraceMap evaluates PostgreSQL state transitions.

Acceptance Criteria:

1. The reducer SHALL preserve script/step order and flag grant-before-object,
   revoke-after-grant, ownership-change, and default-privilege ordering as
   review conditions when deterministically visible.
2. It SHALL not simulate transactions, conditional branches, exception blocks,
   role inheritance, or live catalog state.
3. Unknown file execution order SHALL be explicit and SHALL cap cross-file
   prerequisite results at review-tier evidence unless order is declared.
4. Partial parser or context coverage SHALL propagate to prerequisite results.
5. Failed analysis SHALL not be presented as satisfied prerequisites.

### Requirement 5: Reporting and Safety

**User Story:** As an operator, I want a concise prerequisite table that is safe
to hand off.

Acceptance Criteria:

1. Reports SHALL show operation/object category, candidate capability,
   categorical context, evidence status, supporting rule/tier/span, coverage,
   owner question, and limitation.
2. Reports SHALL not display raw SQL, raw role/object/infrastructure names,
   connection strings, credentials, local paths, or private details.
3. Reports SHALL state that permission sufficiency and effective runtime access
   require DBA/operator validation.
4. Secret-bearing steps SHALL link by safe fact ID/category only.
5. No report action SHALL generate or execute a grant statement.

### Requirement 6: Synthetic Acceptance Tests

**User Story:** As a contributor, I want public-safe permission fixtures that
exercise both explicit and missing evidence.

Acceptance Criteria:

1. Synthetic fixtures SHALL cover supported grants, revokes, ownership, default
   privileges, memberships, extension capabilities, foreign-server usage,
   archive schema access, replication, and `pg_cron` candidates.
2. Tests SHALL cover present, missing-evidence, conflicting, unknown identity,
   unknown cross-file order, and reduced parser/context coverage.
3. Tests SHALL prove no planted role, server, database, credential, raw SQL, or
   absolute-path sentinel appears in any artifact or log.
4. Repeated reduction with shuffled input enumeration SHALL produce stable
   results when declared step order is unchanged.
5. CLI fixture output SHALL use `present-in-scripts` wording and never claim
   effective access or permission sufficiency.
