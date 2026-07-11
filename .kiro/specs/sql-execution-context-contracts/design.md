# SQL Execution Context Contracts Design

## Overview

The first SQL evidence runway slice introduces a context contract that other
SQL specs can consume:

```text
checked-in SQL + optional sidecar
  -> safe statement boundaries and declarations
  -> context facts and gaps
  -> index.sqlite / facts.ndjson / report.md
```

The pipeline is static-only. It does not connect to PostgreSQL or validate the
active pgAdmin connection.

## Proposed Evidence Model

Implementation must finalize and catalog rule names before use. The recommended
starting vocabulary is:

| Rule ID | Fact | Tier ceiling | Purpose and limitation |
| --- | --- | --- | --- |
| `database.sql.context.declaration.v1` | `SqlExecutionContextDeclared` | `Tier2Structural` | Captures a checked-in declaration; does not prove runtime context. |
| `database.sql.context.syntax.v1` | `SqlExecutionContextCandidate` | `Tier2Structural` | Derives categorical context from recognized syntax; does not prove operator intent. |
| `database.sql.context.gap.v1` | `AnalysisGap` | `Tier4Unknown` | Records missing, conflicting, or unsupported context evidence. |

Safe fact properties are closed categorical values, declaration source kind,
statement ordinal, safe file span, and hashes. Infrastructure identities and
raw directive/comment text are excluded.

## Sidecar Contract

Use a repo-relative sidecar such as `<script>.tracemap-sql-context.yml`. The
schema should contain a version, script-relative statement selectors or bounded
step IDs, categorical contexts, required capability codes, and stop-condition
codes. It must not accept credentials or connection strings.

Recommended v1 categorical fields:

| Field | Example values |
| --- | --- |
| `engineFamily` | `postgresql`, `unknown` |
| `serverRole` | `source`, `archive-target`, `admin`, `unknown` |
| `databaseRole` | `source-data`, `archive-data`, `admin`, `validation-only`, `unknown` |
| `schemaRole` | `application`, `archive`, `extension`, `unspecified` |
| `executionMode` | `manual`, `scheduled`, `validation-only`, `unknown` |
| `requiredCapabilities` | cataloged codes such as `create-extension`, `create-server`, `schedule-job` |

Comment directives should be opt-in, line-bounded, and parse only the declared
grammar. Ordinary comments are not a trusted context contract.

## Statement and Context Classification

The extractor should use a PostgreSQL-aware tokenizer or conservative statement
boundary reader that ignores comments and string bodies for keyword matching.
It must preserve statement ordinal and line span without storing text.

Precedence is deterministic:

1. valid sidecar declaration;
2. valid bounded in-file directive;
3. recognized structural syntax candidate;
4. unknown.

Lower-precedence evidence cannot replace higher-precedence evidence. A
disagreement emits both the safe evidence records and a conflict gap.

`pg_cron` calls are classified as scheduled execution surfaces. The scheduled
command body is never stored or rendered; only safe job-kind/context metadata
and a hash may be retained if the shared safety policy permits it.

## Human-Factor Rendering

The report groups steps in source order and inserts a visible context boundary
whenever server role, database role, or execution mode changes. Each group
shows:

- expected categorical context and provenance;
- required capability codes;
- stop/needs-review conditions;
- an operator verification prompt;
- limitations stating that active connection and runtime state are unknown.

The renderer must never concatenate statements into executable SQL or add a
copy/run action.

## Coverage and Failure Behavior

Malformed sidecars, unsupported directives, tokenizer failures, ambiguous
statement boundaries, unknown dialects, and dynamic scheduled-command bodies
produce `AnalysisGap` evidence and reduced coverage. Other safe facts remain
available. A failed parser is not a clean script.

## Test Design

Use small synthetic fixtures only. Unit tests cover schema validation,
precedence, conflict classification, statement spans, `pg_cron`, redaction, and
stable identities. Integration tests scan a fixture twice and compare
`facts.ndjson`, selected SQLite rows, and `report.md` byte-for-byte after
excluding already-established nondeterministic manifest fields, if any.

No test requires PostgreSQL, pgAdmin, RDS, credentials, or network access.

## Future Extension

The contract reserves engine-family routing for SQL Server, Oracle, and MySQL.
Future live validation must arrive as a separately scoped, explicit validation
artifact with provenance; it must not silently upgrade static context facts.
