# SQL Operator Runbook Packet Implementation State

Status: ready-for-implementation
Spec branch: `codex/sql-evidence-runway-specs`
Target base: `dev`
Public claim level: hidden

## Scope State

This folder is specification-only. It combines the operator packet and static
postmortem/partial-state stories into one downstream consumer. No renderer,
schema, or reducer is implemented by this branch.

## Related Issues

- [#458 SQL operator runbook evidence packet](https://github.com/joefeser/tracemap/issues/458)
- [#457 SQL setup postmortem and partial-state report](https://github.com/joefeser/tracemap/issues/457)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#455 Secret-bearing SQL step detection](https://github.com/joefeser/tracemap/issues/455)
- [#456 SQL permission prerequisite evidence](https://github.com/joefeser/tracemap/issues/456)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Ordering Recommendation

Implement fifth, after the four upstream evidence contracts. This keeps the
packet a thin deterministic projection instead of forcing report code to invent
context, secret, permission, or archive-link semantics.

Recommended runway:

1. `sql-execution-context-contracts`
2. `postgres-archive-link-evidence`
3. `sql-secret-bearing-step-safety`
4. `sql-permission-prerequisite-evidence`
5. `sql-operator-runbook-packet`

## Decisions

- Issue #457 is absorbed here as static milestone/postmortem behavior, avoiding
  a sixth overlapping spec.
- V0 includes validation step candidates but no observed/live validation.
- Packet JSON is an allowlisted summary, not raw fact or SQLite export.
- Manual-client context transitions are first-class stop/checkpoint surfaces.
- The packet never emits executable SQL or claims safety, success, or approval.

## Validation Expected During Implementation

- One complete synthetic public-safe archive story and focused failure variants.
- Golden Markdown/JSON, schema, reducer, phrase, leak, and determinism tests.
- Full .NET tests and a CLI packet smoke producing all standard artifacts.
- `./scripts/check-private-paths.sh` and `git diff --check`.

## Open Design Questions

- Decide whether the packet is initially a section in `report.md`, a separate
  `sql-runbook.md`, or both while preserving the required scan output contract.
- Define exact negated/qualified phrase-test rules so limitations can say what
  TraceMap does not prove without allowing unqualified runtime claims.
