# SQL Evidence Route-Flow Composition Implementation State

Status: ready-for-implementation
Implementation branch: _unassigned_
Target base: `main` (`dev` and `main` are the same hash at spec creation)
Public claim level: static evidence packet

## Scope State

Spec skeleton only — no code implemented in this pass. This slice **composes**
the already-shipped PostgreSQL SQL runway evidence into the combined route-flow
report and/or the release-review packet. It does not add SQL extraction.

Confirmed shipped on `main` at spec creation (audited files present at HEAD):

- `SqlExecutionContextExtractor` is wired into the scan pipeline at
  `src/dotnet/TraceMap.Core/ScanEngine.cs` (execution-context runs on every scan).
- `SqlSecretSafetyExtractor`, `PostgresPermissionEvidenceExtractor`,
  `PostgresArchiveLinkExtractor` (`TraceMap.Core`) and `SqlRunbookPacket`
  (`TraceMap.Reporting`) are present with tests and rule-catalog entries.
- Evidence currently surfaces only in single-scan `report.md` and the standalone
  `sql-runbook.md` / `sql-runbook.json` artifacts.

Confirmed composition gap (the reason this spec exists):

- `CombinedRouteFlowReport` and `ReleaseReviewReport` do **not** read the SQL
  execution-context / permission / archive-link / runbook evidence.
  `ReleaseReviewReport` has a `SqlSchemaImpact` section, but that is the older
  SQL-*shape* path, not the operator/context evidence.

## Implementation Choices To Record When Started

- Which target(s) this PR delivers: Target B (release-review) recommended first;
  Target A (route-flow) optional / second PR.
- Whether a read-side hook was needed (Requirement 6) and its exact surface.
- Any sample/fixture added beyond the existing SQL samples.

## Boundaries (do not cross in this spec)

- No new extraction, no engine beyond PostgreSQL, no rule/tier/coverage/
  extractor-version changes. A read-side hook is the maximum extraction-adjacent
  change; anything larger becomes a follow-up spec.
- No runtime, reachability, production-state, permission-effectiveness,
  validation-result, rollback, or release-approval claims. Preserve every
  existing SQL non-claim.
- No raw SQL, connection strings, credentials, scheduled command bodies, private
  identities, or local absolute paths in output.

## Adjacent / Follow-Up (tracked in tasks.md, not blocking the composition PR)

- Phase 5: `docs/ACCEPTANCE.md` SQL acceptance rows; widen site claim guardrails
  to all `dist/**/index.html`.
- Phase 6: audit status-drift cleanup (`NEXT_EXECUTION_REPORT.md` refresh,
  headerless implementation-state files, stale in-flight statuses, README SQL
  mention).

## Related Specs

- [[sql-execution-context-contracts]]
- [[sql-operator-runbook-packet]]
- [[sql-permission-prerequisite-evidence]]
- [[postgres-archive-link-evidence]]
- [[sql-secret-bearing-step-safety]]
- [[route-flow-service-data-composition]]
- [[route-centered-endpoint-trace-completeness]]
