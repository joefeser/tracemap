# SQL Evidence Route-Flow Composition Tasks

## Implementation Plan

Keep the implementation slice PR-sized. Phases 1–3 are the shippable increment
(prefer Target B first per design). Phase 4 tests ship with that increment.
Phase 5 is adjacent cleanup and may be split into its own PR. Phase 6 is
audit-driven status-drift cleanup and is follow-up, not part of the composition
PR.

### Phase 1: Read gate (no new extraction)

- [x] 1.1 Confirm SQL runway facts are readable from the combined/index readers used by route-flow and release-review; list exactly which rule IDs and fact types are reachable.
- [x] 1.2 Decide Target A (route-flow), Target B (release-review), or both for this PR; record the choice in implementation-state.
- [x] 1.3 Confirm no rule/tier/coverage/extractor-version change is required. If a change larger than a read-side hook is needed, stop and open a follow-up spec (Requirement 6).

### Phase 2: Release-review SQL evidence section (Target B — recommended first)

- [x] 2.1 Add a `SqlEvidence` `ReleaseReviewSection` parallel to `SqlSchemaImpact`, wired into the section list, JSON DTO, and Markdown writer.
- [x] 2.2 Set section status from `ReleaseReviewStatuses` (`available` / `not_requested` / `unavailable` / `deferred` / `truncated`) based on presence of SQL runway evidence in the selected inputs; keep context/permission/archive/secret-safety gaps as `ReleaseReviewGap` entries, never as a status value.
- [x] 2.3 Reuse `SqlRunbookPacketBuilder` output; classify findings only with the existing attention levels; append section gaps to the packet-level `gaps`.
- [x] 2.4 Add the non-claim footer and route output through the safe-output allowlist / forbidden-phrase checks.

### Phase 3: Route-flow SQL context group (Target A — same PR only if small)

- [x] 3.1 Add a SQL-context `RouteFlowContextGroup` candidate in `BuildContextGroups`, additive only, keyed off data-facing routes.
- [x] 3.2 Summarize ordered categorical context and transition checkpoints; list permission prerequisites and stop conditions by upstream closed status.
- [x] 3.3 Emit a gap row when a data-facing route lacks SQL context; preserve provenance via `ContextGroupRuleIds` / `ContextGroupLocation`.
- [x] 3.4 Assign the `sql-context` kind a deterministic rank in `ContextGroupKindRank` between `query` and `data-surface`; confirm ordering stability.

### Phase 4: Fixtures and tests (ship with the increment)

- [x] 4.1 Add `available`, `deferred`/`unavailable`, and `gap` variant tests for the chosen target(s) using existing SQL samples.
- [x] 4.2 Assert projected rows preserve rule ID, tier, coverage, span, commit SHA, extractor version, and fact IDs from upstream.
- [x] 4.3 Assert no planted sentinel values / raw SQL / paths appear; protected steps stay span-only and hash-free.
- [x] 4.4 Extend `docs/VALIDATION.md` with a route-flow and/or release-review SQL-evidence smoke check.

### Phase 5: Adjacent cleanup (separate PR where noted)

- [x] 5.1 Add acceptance criteria to `docs/ACCEPTANCE.md` for the SQL runway families (execution-context, permission, archive-link, secret-safety, runbook) if missing.
- [x] 5.2 Extend the site claim guardrails validator (`site/scripts/site-claim-guardrails.mjs`) to scan all generated `dist/**/index.html`, not just the guardrails page; add a regression test.

### Phase 6: Audit-driven status-drift cleanup (focused follow-up PR)

- [x] 6.1 Refresh or mark-superseded `docs/NEXT_EXECUTION_REPORT.md` (recommends already-done Swift v0; describes `dev` under "Product Shape On Main").
- [x] 6.2 Add parseable `Status:` headers to `.kiro/specs/evidence-export-usability-polish/implementation-state.md` and `.kiro/specs/route-centered-endpoint-trace-completeness/implementation-state.md`.
- [x] 6.3 Resolve stale in-flight statuses: `legacy-data-model-relationship-completion` (`pr-open-ack-patch-pass`) and `static-dispatch-candidate-bridges` (`awaiting-pr-loop`).
- [x] 6.4 Add the shipped SQL runway to `README.md` (currently unmentioned) now that it is on `main`.

## Definition of done (composition PR)

- Chosen target(s) render release-review status values from
  `ReleaseReviewStatuses` and structured gap entries with full upstream
  provenance and zero new runtime claims.
- All existing tests plus new variant tests pass; VALIDATION.md smoke updated.
- No extraction, rule-catalog, or extractor-version changes.
- PR stays PR-sized; anything larger is split per the phase boundaries above.
