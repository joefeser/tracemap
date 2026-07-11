# SQL Evidence Route-Flow Composition Design

## Overview

This is a projection over already-shipped facts. Nothing new is extracted:

```text
scan (already ships)          composition (this spec)
  SqlExecutionContextExtractor   ->  read cataloged SQL facts from the index
  Postgres{Permission,ArchiveLink} ->  route-flow context group (additive)
  SqlSecretSafetyExtractor       ->  release-review SQL section (status plus gaps)
  SqlRunbookPacketBuilder        ->  (reuse packet DTO where convenient)
```

The design goal is that a projected row is byte-for-byte traceable to an upstream
fact: same rule ID, tier, coverage label, span, commit SHA, extractor version,
and fact ID. The composition layer classifies and orders; it never re-derives.

## Upstream contracts consumed (all already on `main`)

- Fact types: `FactTypes.SqlExecutionContextDeclared`,
  `SqlExecutionContextCandidate` (`src/dotnet/TraceMap.Core/Models.cs`).
- Rule IDs: `database.sql.context.declaration.v1`, `database.sql.context.syntax.v1`,
  `database.sql.context.gap.v1`, `database.sql.secret-bearing-step.v1`,
  `database.sql.secret-text-candidate.v1`,
  `database.sql.secret-safety-gap.v1`,
  `database.postgres.permission.statement.v1`,
  `database.postgres.permission.prerequisite.v1`,
  `database.postgres.permission.coverage.v1`,
  `database.postgres.permission.gap.v1`, `database.postgres.archive-link.v1`,
  `database.postgres.archive-link.prerequisite.v1`,
  `database.postgres.archive-link.gap.v1`.
- Extractor versions: `sql-execution-context/0.1.0`, `sql-secret-safety/0.1.0`,
  `postgres-permission-evidence/0.1.0`, `postgres-archive-link/0.1.0`.
- Packet contract: `sql-operator-runbook-packet/v2` via
  `SqlRunbookPacketBuilder.Build(result)` (`TraceMap.Reporting/SqlRunbookPacket.cs`).

## Target A: Combined route-flow context group

`CombinedRouteFlowReport.BuildContextGroups` already produces additive
`RouteFlowContextGroup` rows (method, service, repository, query, data-surface,
dependency, value-origin, gap). Add a SQL-context group candidate that:

- keys off routes whose selected rows already reach SQL/data surfaces;
- summarizes ordered categorical context (engine → server → database → schema →
  mode) and transition checkpoints from the execution-context facts;
- lists permission-prerequisite candidates and stop conditions by their closed
  upstream statuses (`present-in-scripts`, `missing-evidence`,
  `conflicting-evidence`, `unknown`, `needs-owner-review`);
- emits a gap row when a data-facing route lacks SQL context.

Constraints: reuse `ContextGroupRuleIds` / `ContextGroupLocation`; obey the
existing deterministic `ContextGroupKindRank` ordering by adding the new
`sql-context` kind at a defined rank between `query` and `data-surface`; never
introduce a new runtime conclusion.

## Target B: Release-review SQL evidence section

`ReleaseReviewReport` composes sections as `ReleaseReviewSection` records with a
status string from `ReleaseReviewStatuses` (`available`, `not_requested`,
`unavailable`, `deferred`, `truncated`). Add a `SqlEvidence` section parallel to
the existing `SqlSchemaImpact` section:

- `available` when the after-index carries SQL runway evidence;
- `unavailable`/`deferred` when no compatible SQL evidence is present or no SQL
  input was requested;
- findings classified only with the existing attention levels
  (`ActionableStaticEvidence`, `ReviewRecommended`, `NoActionableEvidence`,
  `PartialAnalysis`, `SelectorNoMatch`);
- section gaps appended to the packet-level `gaps`.

Gaps are never status values. Secret text candidates, secret-safety gaps,
permission gaps, archive-link gaps, and runbook protected-material owner
questions must remain `ReleaseReviewGap` entries on the section and must be
propagated into the packet-level `gaps` collection.

Prefer reusing `SqlRunbookPacketBuilder` output to avoid duplicating the safe
projection logic. Wire the section into the section list, the JSON DTO, and the
Markdown writer following the `SqlSchemaImpact` precedent.

## Recommended sequencing

Ship Target B (release-review section) first — it is the smaller, more isolated
change with a clear existing precedent (`SqlSchemaImpact`) and no new ordering
rank. Target A (route-flow context group) can follow as a second PR if the slice
gets large. Either target alone is a shippable, PR-sized increment; do not
attempt both plus the adjacent cleanup in one PR.

## Safety and determinism

- Route projected output through the same safe-output allowlist and
  forbidden-phrase checks the runbook packet uses; protected steps stay
  span-only and hash-free.
- No `Date.now()`-style nondeterminism; ordering is fully deterministic.
- Add the standard non-claim footer to each new section, matching
  `SqlRunbookPacket` and `docs/SQL_OPERATOR_RUNBOOK_PACKET.md`.

## Testing strategy

- Reuse existing synthetic fixtures: `samples/sql-operator-runbook`,
  `samples/postgres-permission-evidence`, `samples/postgres-archive-link`,
  `samples/sql-execution-context`.
- Unit tests: `available`, `deferred`/`unavailable`, and `gap` variants for each
  target; provenance-preserved assertions; planted-value leak assertions.
- CLI smoke: extend `docs/VALIDATION.md` route-flow and release-review sections
  with a SQL-evidence check; assert artifacts contain the section and never the
  planted sentinels.

## Out-of-design (explicit)

No changes to extraction, engines beyond PostgreSQL, rule catalog semantics, or
the four upstream extractor versions. If a change beyond a read-side hook is
needed, stop and open a follow-up spec (see Requirement 6 in requirements.md).
