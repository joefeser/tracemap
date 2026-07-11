# SQL Evidence Route-Flow Composition Requirements

## Introduction

The PostgreSQL SQL runway is already shipped on `main` as scan-time analyzer
behavior: `SqlExecutionContextExtractor` runs inside the scan pipeline
(`src/dotnet/TraceMap.Core/ScanEngine.cs`), and execution-context, secret-safety,
permission-prerequisite, archive-link, and operator-runbook evidence are emitted
with rule IDs, evidence tiers, coverage labels, spans, commit SHA, and extractor
versions. That evidence currently surfaces only in single-scan `report.md` and
the standalone `sql-runbook.md` / `sql-runbook.json` artifacts.

It does **not** yet reach the cross-index composition reports where the
endpoint-to-service-to-data conversation actually happens:
`CombinedRouteFlowReport` and `ReleaseReviewReport` never read the SQL context /
permission / runbook evidence. This spec composes the already-shipped SQL
evidence into those packets as bounded, non-runtime sections. It is a
**composition** slice, not an extraction slice.

This spec is spec-only. No code is implemented in the skeleton pass.

## Scope

In scope:

- Project already-cataloged SQL runway evidence into the combined route-flow
  report and/or the release-review packet as additive, labeled sections.
- Set release-review section status only with the existing
  `ReleaseReviewStatuses` vocabulary (`available`, `not_requested`,
  `unavailable`, `deferred`, `truncated`). Carry upstream context/permission/
  archive/secret-safety gaps as structured `ReleaseReviewGap` entries on the
  SQL section and in the packet-level `gaps` collection rather than inventing a
  `gap` status.
- Preserve full provenance on every projected row: rule ID, evidence tier,
  coverage label, file span, commit SHA, extractor version, and supporting fact
  IDs — sourced from the upstream facts, never recomputed.
- Add acceptance criteria in `docs/ACCEPTANCE.md` for the shipped SQL runway
  evidence families if they are still missing there.
- Extend the site claim guardrails validator to scan all generated
  `dist/**/index.html`, not only the single guardrails page.

Out of scope (do not build in this slice):

- New SQL extraction, new engines beyond PostgreSQL, or new fact/rule types.
  A **tiny** adapter/read hook is permitted only if composition is otherwise
  impossible (see Requirement 6); anything larger is a separate spec.
- Consuming SQL evidence into `diff`, `impact`, `snapshot-diff`, `portfolio`,
  `contract-diff`, or `paths` — route-flow and/or release-review only.
- Any runtime, reachability, production-state, permission-effectiveness,
  validation-result, rollback, or release-approval claim.
- Raw SQL, statement text, connection strings, credentials, scheduled command
  bodies, private infrastructure identities, or local absolute paths in any
  projected output.

## Requirements

### Requirement 1 — Read shipped SQL evidence without recomputation

**User story:** As a reviewer, I want the composition reports to reuse the SQL
evidence the scan already produced, so conclusions stay identical to the
single-scan report.

Acceptance criteria:

1. WHEN a selected index contains SQL execution-context facts
   (`FactTypes.SqlExecutionContextDeclared` / `SqlExecutionContextCandidate`,
   rules `database.sql.context.declaration.v1`, `database.sql.context.syntax.v1`,
   `database.sql.context.gap.v1`), THEN the composition reader SHALL surface them
   without re-parsing SQL and without altering their tier or coverage.
2. WHEN permission (`database.postgres.permission.*.v1`), archive-link
   (`database.postgres.archive-link*.v1`), or secret-bearing
   (`database.sql.secret-bearing-step.v1`,
   `database.sql.secret-text-candidate.v1`,
   `database.sql.secret-safety-gap.v1`) evidence is present, THEN it SHALL be
   projected with its upstream rule ID and status/gap verbatim.
3. WHEN no compatible SQL evidence exists in the selected inputs, THEN the
   section SHALL render as `deferred`/`unavailable` — never as "no SQL risk".

### Requirement 2 — Route-flow SQL data-surface context group

**User story:** As a reviewer tracing an endpoint to its data surfaces, I want
SQL execution-context and permission/stop evidence to appear alongside the
existing query/data-surface context, so the endpoint-to-data story is complete.

Acceptance criteria:

1. WHEN `CombinedRouteFlowReport.BuildContextGroups` runs over a route whose
   selected rows include SQL/data-surface evidence with cataloged SQL context,
   THEN an additive `RouteFlowContextGroup` SHALL summarize the ordered
   categorical context (engine/server/database/schema/mode), prerequisite
   candidates, and stop conditions.
2. The group SHALL follow existing context-group rules: additive only, no new
   runtime conclusions, deterministic ordering, and provenance preserved.
3. WHEN SQL context is missing for an otherwise data-facing route, THEN the
   group SHALL record a gap row rather than omitting the surface silently.

### Requirement 3 — Release-review SQL evidence section

**User story:** As a release reviewer, I want a bounded SQL evidence section in
the release-review packet, so operator/DBA context is visible next to contract
and package impact.

Acceptance criteria:

1. The release-review packet SHALL render a SQL runway section as a
   `ReleaseReviewSection` with a status of `available`, `not_requested`,
   `unavailable`, `deferred`, or `truncated`, mirroring the existing
   `SqlSchemaImpact` section pattern (this is distinct from and additive to
   `SqlSchemaImpact`).
2. The section SHALL classify findings only with the existing attention
   vocabulary (`ActionableStaticEvidence`, `ReviewRecommended`,
   `NoActionableEvidence`, `PartialAnalysis`, `SelectorNoMatch`) — no numeric
   scores, no new severity vocabulary.
3. Section gaps SHALL be emitted as `ReleaseReviewGap` entries and SHALL flow
   into the packet-level `gaps` collection exactly as other sections do.

### Requirement 4 — Non-claims preserved end to end

Acceptance criteria:

1. Projected Markdown/JSON SHALL NOT assert SQL execution, database existence,
   endpoint reachability, production traffic, effective permissions, validation
   results, successful rollback, deployment, release approval, or "safe to run".
2. Every projected section SHALL carry the same non-claim language already used
   by `SqlRunbookPacket` and the SQL docs.
3. Output SHALL pass the existing safe-output allowlists and planted-value leak
   checks used by the runbook packet.

### Requirement 5 — Safe-output and public-demo parity

Acceptance criteria:

1. No raw SQL, statement text, connection strings, credentials, scheduled
   command bodies, private infrastructure identities, or local absolute paths
   SHALL appear in composed output; protected steps remain span-only and
   hash-free, matching upstream behavior.
2. Public-safe demo/report paths that render these sections SHALL run the
   private-path guard and forbidden-phrase checks.

### Requirement 6 — Minimal adapter hook (only if unavoidable)

Acceptance criteria:

1. IF composition cannot read the SQL evidence from existing combined/index
   readers, THEN at most a small read-side hook (e.g. exposing already-emitted
   SQL facts to the combined index reader) MAY be added.
2. Any such hook SHALL NOT change extraction, fact shapes, rule IDs, tiers,
   coverage, or the rule catalog. IF a change larger than a read hook is
   required, THEN this slice SHALL stop and open a follow-up spec.

### Requirement 7 — Documentation and guardrails cleanup (adjacent)

Acceptance criteria:

1. `docs/ACCEPTANCE.md` SHALL gain acceptance rows for the SQL runway families
   if missing.
2. The site claim guardrails validator SHALL scan all generated
   `dist/**/index.html`.
3. These items are adjacent cleanup; they MUST NOT expand the implementation
   PR beyond PR-sized. IF they grow, THEN split them into their own PR.
