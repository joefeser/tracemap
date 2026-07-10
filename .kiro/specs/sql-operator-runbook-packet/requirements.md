# SQL Operator Runbook Packet Requirements

## Introduction

After repeated SQL setup attempts, teams need a compact, evidence-backed packet
that separates intended steps, prerequisites, validation expectations, and
unknown runtime state. TraceMap should assemble that packet from static evidence
without turning a report into an execution engine or claiming that a database
matches the scripts.

This spec combines the operator handoff goal in issue #458 with the static-first
postmortem/partial-state goal in issue #457. It is spec-only.

Related issues: [#458](https://github.com/joefeser/tracemap/issues/458),
[#457](https://github.com/joefeser/tracemap/issues/457),
[#453](https://github.com/joefeser/tracemap/issues/453),
[#454](https://github.com/joefeser/tracemap/issues/454),
[#455](https://github.com/joefeser/tracemap/issues/455),
[#456](https://github.com/joefeser/tracemap/issues/456), and
[#438](https://github.com/joefeser/tracemap/issues/438).

## Scope

In scope:

- A deterministic, human-safe Markdown packet plus machine-readable summary
  derived from SQL context, archive-link, secret-safety, permission, validation,
  and gap facts.
- Purpose/scope, ordered steps, categorical context, prerequisites, protected
  handling flags, intended object changes, validation expectations, cleanup/
  rollback evidence, coverage, gaps, stop conditions, and owner questions.
- Static postmortem milestones and optional future checked-in public-safe
  validation summaries with explicit provenance.
- Manual-client safeguards, especially context transitions and wrong-tab/
  wrong-database risk.

Out of scope:

- SQL execution, copy/run controls, database connections, workflow automation,
  ticket updates, or DBA approval replacement.
- Proof that a step ran, an object exists, a rollback works, a permission is
  effective, or an archive is correct.
- Raw SQL, source snippets, connection strings, credentials, local absolute
  paths, private names, raw validation output, or internal ticket details.
- Live validation artifacts in v0.

## Requirements

### Requirement 1: Packet Contract

**User Story:** As an operator receiving a handoff, I want one bounded packet
that makes the intended procedure and its evidence limitations explicit.

Acceptance Criteria:

1. The packet SHALL contain `purpose`, `source`, `coverage`, `stepGroups`,
   `milestones`, `prerequisites`, `protectedSteps`,
   `validationExpectations`, `cleanupEvidence`, `stopConditions`, `gaps`,
   `ownerQuestions`, and `limitations` as defined by the packet schema.
   `stepGroups` carries ordered steps and expected categorical context;
   `milestones` carries intended object/surface changes and validation state.
2. Every step or conclusion SHALL reference supporting rule IDs, evidence tiers,
   repo-relative file spans, commit SHA, extractor versions, and coverage.
3. The machine-readable summary SHALL use a versioned stable schema and SHALL
   not be a dump of raw facts or SQLite rows.
4. Empty sections SHALL say `no static evidence found` or `not established`,
   never `none required` or an equivalent clean-state claim.
5. Packet ordering SHALL be deterministic and based on declared step order,
   context boundaries, and stable evidence identities.

### Requirement 2: Ordered Context and Manual-Client Safety

**User Story:** As an operator using pgAdmin or another manual client, I want
context transitions to be impossible to overlook.

Acceptance Criteria:

1. Steps SHALL be grouped by expected engine/server/database/schema/role
   context and execution mode.
2. A change in server role, database role, or manual/scheduled/validation mode
   SHALL create an explicit transition and operator verification checkpoint.
3. Missing or conflicting context SHALL create a stop/needs-review condition.
4. The packet SHALL state that the active client tab/connection/database is not
   observed and must be independently checked.
5. The packet SHALL not concatenate, reconstruct, or render executable SQL.

### Requirement 3: Prerequisites and Protected Steps

**User Story:** As an owner, I want the packet to show what must be reviewed
before execution without leaking protected material.

Acceptance Criteria:

1. Permission rows SHALL use `present-in-scripts`, `missing-evidence`,
   `conflicting-evidence`, `unknown`, or `needs-owner-review` exactly as defined
   by the permission spec.
2. Secret-bearing/reference/possible-secret findings SHALL appear as
   category-only owner-handling steps and stop conditions.
3. The packet SHALL not display or hash raw credentials, connection data,
   remote SQL, scheduled command bodies, or private infrastructure names.
4. Prerequisites SHALL be labeled as static candidates requiring operator/DBA
   validation, not guaranteed requirements or fulfilled capabilities.
5. The packet SHALL not generate grant statements or credential templates.

### Requirement 4: Static Milestones and Validation Expectations

**User Story:** As a postmortem reviewer, I want intended milestones separated
from evidence that validates them.

Acceptance Criteria:

1. V0 milestone states SHALL be `intended-by-script`,
   `validation-step-present`, `validation-evidence-not-provided`,
   `conflicting-static-evidence`, `unknown`, or `not-applicable` only when the
   contract proves non-applicability.
2. Initial PostgreSQL milestone kinds SHALL include extension, foreign server,
   user mapping, schema import/foreign table, permission, publication,
   subscription, scheduled job, validation, and cleanup/rollback candidates.
3. A validation query in SQL SHALL count only as a validation step candidate;
   it SHALL not prove its expected result.
4. The packet SHALL distinguish missing validation-step evidence from missing
   observed validation evidence.
5. No v0 state SHALL imply `applied`, `exists`, `succeeded`, `healthy`,
   `replicating`, `scheduled`, or `rolled back` at runtime.

### Requirement 5: Future Validation Artifact Boundary

**User Story:** As a product architect, I want room for later validation without
weakening the static-first contract.

Acceptance Criteria:

1. V0 SHALL accept no live database connection and no raw command output.
2. A future validation summary SHALL require a separate versioned schema,
   explicit source/provenance, observation time, target categorical context,
   validator identity/version, safe assertion code, result status, and
   limitations.
3. A future validation artifact SHALL be opt-in, checked-in or explicitly
   supplied, public-safe, and free of credentials, connection data, raw SQL,
   private infrastructure names, and raw database output.
4. Validation evidence SHALL remain distinguishable from static script evidence
   and SHALL not rewrite the original fact tier.
5. Unsupported, expired, mismatched-commit, or mismatched-context validation
   artifacts SHALL produce gaps and SHALL not upgrade milestone state.

### Requirement 6: Gaps, Questions, and Limitations

**User Story:** As a handoff recipient, I want unresolved questions stated
directly so I know where human ownership is required.

Acceptance Criteria:

1. The packet SHALL convert cataloged context, archive-link, secret,
   prerequisite, ordering, parsing, and validation gaps into deterministic owner
   questions.
2. Owner questions SHALL use category-level wording and SHALL not repeat unsafe
   source text or private identities.
3. Failed build/parser/coverage states SHALL remain visible and SHALL not be
   interpreted as clean evidence.
4. Limitations SHALL state that TraceMap does not execute SQL, observe a live
   database, certify safety, approve changes, or replace DBA judgment.
5. Runtime state SHALL remain `unknown` unless a future explicit validation
   artifact supports a narrowly defined observation.

### Requirement 7: Synthetic Acceptance Tests

**User Story:** As a contributor, I want a complete public-safe archive setup
story that proves packet usefulness without private data.

Acceptance Criteria:

1. A synthetic fixture SHALL include ordered admin/source/archive/validation
   contexts, one archive-link mechanism, one protected step, permission
   candidates, `pg_cron`, validation queries, and cleanup evidence.
2. Variants SHALL cover wrong-context conflict, missing permission evidence,
   missing validation step, unknown file order, partial parser coverage, and
   repeated failed-attempt-style contradictory static evidence without any
   workplace or ticket details.
3. Golden Markdown and machine-readable summaries SHALL be deterministic and
   contain no executable SQL blocks.
4. Leak tests SHALL plant secrets, connection strings, infrastructure names,
   local paths, raw SQL markers, validation output, and ticket-like identifiers
   and prove they do not appear in packet artifacts or logs.
5. Phrase tests SHALL reject unqualified runtime claims such as `applied`,
   `exists`, `succeeded`, `healthy`, `safe to run`, or `permissions satisfied`.
