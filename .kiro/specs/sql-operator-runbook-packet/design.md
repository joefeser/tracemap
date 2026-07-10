# SQL Operator Runbook Packet Design

## Overview

The packet is a safe projection over already-cataloged facts:

```text
context + archive links + secret findings + prerequisites + gaps
  -> ordered milestone reducer
  -> safe packet DTO
  -> report.md section / standalone Markdown + versioned JSON summary
```

It is deliberately not a SQL renderer or workflow runner.

## Dependency Gate

Implementation should begin after the other runway specs establish stable fact
contracts. The packet may ship incrementally, but every displayed row must come
from a cataloged fact or gap. Missing upstream evidence remains explicit.

## Packet Schema

Recommended top-level fields:

| Field | Purpose |
| --- | --- |
| `schemaVersion` | Stable packet contract version |
| `purpose` | Safe categorical purpose and bounded scope summary |
| `source` | Safe repo/commit/scan provenance without local paths or raw remotes |
| `coverage` | Complete/reduced/failed components and reasons |
| `stepGroups` | Ordered context groups and transitions |
| `milestones` | Intended static outcomes and validation expectations |
| `prerequisites` | Safe prerequisite evidence rows |
| `protectedSteps` | Category-only secret-handling rows |
| `validationExpectations` | Validation step candidates and absent-observation state |
| `cleanupEvidence` | Static rollback/cleanup candidates with limitations |
| `stopConditions` | Cataloged reasons preventing an unqualified handoff |
| `gaps` | Safe cataloged missing, conflicting, unsupported, or reduced-coverage evidence |
| `ownerQuestions` | Deterministic category-level questions |
| `limitations` | Non-claims and coverage boundaries |

The JSON DTO is allowlisted and compact; it does not embed fact property bags.

## Step Grouping

Use declared sequence first, then statement ordinal within a file, then stable
fact identity. Unknown cross-file order creates separate unordered groups with a
stop/review condition. Server/database/execution-mode changes create transition
cards. Each manual group includes an active-connection verification checkpoint.

`pg_cron` steps form a scheduled group even if their source declaration appears
beside manual setup. Scheduled command text is not available to the packet.

## Milestone Reducer

Milestones pair an intended static surface with zero or more validation step
candidates and future observation artifacts. V0 states are intentionally
non-runtime:

- `intended-by-script`;
- `validation-step-present`;
- `validation-evidence-not-provided`;
- `conflicting-static-evidence`;
- `unknown`;
- `not-applicable` only from an explicit contract.

A query-shaped statement marked validation-only can support
`validation-step-present`; TraceMap does not inspect its result. Cleanup and
rollback statements are candidates only and never proof of reversibility.

## Owner Questions

Map closed gap/reason codes to templated category-level questions, for example:

- verify the active categorical context before the next manual group;
- have a database owner confirm a prerequisite candidate;
- route protected material through an approved owner process;
- provide or perform the declared validation outside TraceMap;
- resolve unknown ordering or conflicting static evidence.

Templates use no free-form source data.

## Safety

The packet accepts only safe upstream DTOs and reapplies allowlists. Markdown
contains no fenced SQL, shell commands, copy buttons, raw fact JSON, raw
validation output, connection data, private names, or absolute paths. Safe
repo-relative file links/spans are permitted in local reports.

Automated phrase tests catch unqualified runtime/safety claims. Negated
limitation phrases are allowed only in the limitations section or clearly
qualified status descriptions.

## Future Validation Summary

Reserve a separate `sql-validation-summary/v1` boundary but do not implement it
in v0. A future artifact uses categorical target context and closed assertion
codes, includes observation provenance and freshness, and contains no raw query
or result data. The packet shows static and observed evidence in separate
columns and never upgrades the source fact's tier.

## Testing

Use a comprehensive synthetic PostgreSQL archive fixture plus variants. Golden
tests cover Markdown and JSON schema, deterministic ordering, transition cards,
milestones, stop conditions, owner questions, gaps, limitations, and absence of
executable blocks. Leak/phrase scanners inspect every output and log.

The CLI acceptance test confirms all standard scan artifacts still exist and
that failed/partial analysis remains visibly reduced.

## Future Delivery

Additional SQL engines can provide facts to the shared packet contract. Live
validation and external workflow/ticket integrations require separate specs and
authority; neither belongs in the packet generator.
