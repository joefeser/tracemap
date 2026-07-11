# SQL Operator Runbook Packet Implementation State

Status: implemented
Implementation branch: `codex/sql-operator-runbook-packet-impl`
Target base: `dev`
Public claim level: static evidence packet

## Scope State

The v0 packet is implemented as a deterministic allowlisted projection over the
four upstream SQL evidence contracts. Every scan now writes `sql-runbook.md` and
`sql-runbook.json`; `report.md` includes a bounded packet summary. The packet
contains no executable SQL or generic fact-property bags.

Implemented scope:

- `sql-operator-runbook-packet/v1` JSON DTO and deterministic Markdown renderer.
- Ordered categorical context groups, explicit transition checkpoints, and
  manual-client active-connection verification reminders.
- Static milestones for extensions, archive-link surfaces, permissions,
  scheduled jobs, validation steps, and cleanup/rollback candidates.
- Safe permission, protected-step, stop-condition, gap, and owner-question
  projections with rule/tier/span/commit/extractor/coverage provenance.
- Synthetic complete and failure-variant tests, CLI artifact smoke, deterministic
  serialization, forbidden-phrase checks, and planted-value leak checks.
- Documentation of semantics, non-claims, validation workflow, and the reserved
  future `sql-validation-summary/v1` boundary.

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

## Validation

- Focused `SqlRunbookPacketTests`: 6 passed.
- Synthetic CLI smoke produced all standard scan artifacts plus
  `sql-runbook.md` and `sql-runbook.json`; planted values were absent.
- Full solution build succeeded with zero warnings/errors; 742 full-suite tests
  passed; the private-path guard and `git diff --check` passed.

## Scope Decisions

- Emit both standalone Markdown/JSON artifacts and a bounded `report.md` summary.
- Keep phrase checks on packet conclusions while allowing clearly negated
  limitation language elsewhere in the general scan report.
- Do not ingest validation artifacts in v0; reserve the schema boundary only.

## Remaining Follow-Ups

- Future validation-summary ingestion requires a separate spec and provenance
  contract.
- Additional SQL engines may populate the shared packet only after their own
  cataloged evidence contracts exist.
