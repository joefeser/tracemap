# SQL Validation Harness v1 Tasks

## Contract and boundary

- [x] Define the separate operator-controlled execution and non-claim boundary.
- [x] Define the strict local plan and closed v1 probe vocabulary.
- [x] Add the machine-readable local plan schema.

## Implementation

- [x] Add the deterministic plan parser and summary writer.
- [x] Add the parameterized read-only PostgreSQL catalog executor.
- [x] Add the standalone CLI and dry-run path.

## Validation and documentation

- [x] Add synthetic executor, parser, CLI, determinism, and leakage tests.
- [x] Prove generated summaries are accepted by the shipped ingestion reader.
- [x] Update operator and validation documentation.
- [x] Run focused tests, solution build/test, private-path guard, and diff check.
