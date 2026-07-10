# SQL Secret-Bearing Step Safety Implementation State

Status: ready-for-implementation
Spec branch: `codex/sql-evidence-runway-specs`
Target base: `dev`
Public claim level: hidden

## Scope State

This folder is specification-only. It defines a category-only SQL safety
boundary and leak-test expectations. No secret detector or production output
change is implemented by this branch.

## Related Issues

- [#455 Secret-bearing SQL step detection](https://github.com/joefeser/tracemap/issues/455)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Ordering Recommendation

Implement third, after the shared context/span contract and initial archive-link
construct inventory. The classifier can be prototyped alongside archive-link
work, but downstream reporting should not ship archive-link value projection
until this safety boundary is enforced.

Recommended runway:

1. `sql-execution-context-contracts`
2. `postgres-archive-link-evidence`
3. `sql-secret-bearing-step-safety`
4. `sql-permission-prerequisite-evidence`
5. `sql-operator-runbook-packet`

## Decisions

- Findings are category-only and constructed from an allowlist.
- Raw secret values are omitted, never hashed.
- Absence of a finding is not proof that SQL is secret-free.
- Unresolved high-risk parsing fails closed for rendering and emits a gap.
- No secret management or runnable SQL belongs in this lane.

## Validation Expected During Implementation

- Synthetic unique sentinels and false-positive controls.
- Leak assertions across every artifact, log, error, export, and combined path.
- Rule-catalog, parser, determinism, and full .NET tests.
- A CLI sentinel-fixture scan plus `./scripts/check-private-paths.sh` and
  `git diff --check`.

## Open Design Questions

- Decide whether safe statement-shape hashes add enough correlation value to
  justify their narrow, value-excluding normalization contract.
- Confirm the existing shared safe-output helpers can enforce omission before
  generic properties are materialized; otherwise add a dedicated DTO boundary.
