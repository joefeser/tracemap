# SQL Execution Context Contracts Implementation State

Status: ready-for-implementation
Spec branch: `codex/sql-evidence-runway-specs`
Target base: `dev`
Public claim level: hidden

## Scope State

This folder is specification-only. No production code or implementation tasks
are complete. PostgreSQL is the first engine family; the contract remains
extensible to other SQL engines.

## Related Issues

- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Ordering Recommendation

Implement this spec first. Its context vocabulary, statement boundaries,
coverage behavior, and safe evidence model are dependencies for archive-link,
secret-bearing-step, permission-prerequisite, and runbook packet work.

Recommended runway:

1. `sql-execution-context-contracts`
2. `postgres-archive-link-evidence`
3. `sql-secret-bearing-step-safety`
4. `sql-permission-prerequisite-evidence`
5. `sql-operator-runbook-packet`

The secret-safety classifier may begin in parallel with archive-link extraction
only after the shared statement/span and safe-property contracts are fixed.

## Decisions

- Static evidence never proves the active manual-client tab or database.
- Sidecars and bounded directives are declarations of intent, not runtime facts.
- Infrastructure identities are categorical, omitted, or safely represented;
  raw private names are not required.
- Parser failure produces reduced coverage and a gap, not clean absence.
- No runnable SQL or play-button workflow is generated.

## Validation Expected During Implementation

- Synthetic public-safe fixtures only.
- Focused extractor, rule-catalog, storage, report, determinism, and leak tests.
- Full .NET tests and one CLI fixture scan.
- `./scripts/check-private-paths.sh` and `git diff --check`.

## Open Design Questions

- Choose YAML versus JSON for the first sidecar encoding after checking existing
  repository configuration conventions.
- Decide whether statement selectors use stable explicit step IDs only or also
  allow line/ordinal selectors with documented edit-stability limitations.
