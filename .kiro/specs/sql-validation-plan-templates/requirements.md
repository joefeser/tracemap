# SQL Validation Plan Template Requirements

## Goal

Provide reusable source and archive-target validation plans and an operator
handoff for the shipped bounded PostgreSQL catalog harness.

## Requirements

1. Both templates SHALL parse as strict `sql-validation-plan/v1` documents and
   use only synthetic public-safe identities.
2. The archive-target template SHALL cover version, installed extension,
   migration relation, and schema-usage observations.
3. The source template SHALL cover version, installed extensions, schema usage,
   callable function, and scheduled-job registration observations.
4. The operator packet SHALL separate ownership, targets, timestamps, static
   evidence, observed results, and approval.
5. Unsupported execution, connectivity, data, compatibility, cleanup, and
   rollback claims SHALL remain explicit limitations or `not-run` outcomes.
6. The slice SHALL add no connection, extraction, or probe capability.
