# SQL Validation Summary Ingestion Tasks

## Specification and contract

- [x] Define the versioned schema, closed vocabularies, digest, freshness,
  duplicate/conflict, provenance, and threat boundaries.
- [x] Add the machine-readable `sql-validation-summary/v1` schema.
- [x] Catalog observation and ingestion-gap rules with limitations.

## Implementation

- [x] Add strict deterministic summary parsing and policy evaluation.
- [x] Add repeatable explicit CLI inputs to scan and release review.
- [x] Compose accepted observations and gaps into SQL runbook JSON/Markdown.
- [x] Compose observations separately from static SQL evidence in release review.

## Tests and documentation

- [x] Add valid and all required negative synthetic fixtures/tests.
- [x] Prove deterministic ordering/IDs, provenance, freshness, and safe output.
- [x] Update operator-runbook and validation documentation.
- [x] Run focused tests, solution build/test, private-path guard, and diff check.
