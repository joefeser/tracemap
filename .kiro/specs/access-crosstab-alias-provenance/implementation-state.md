# Implementation state

- Branch: `codex/access-crosstab-alias-provenance`
- Worktree: `/private/tmp/tracemap-access-crosstab-alias`
- Base: `origin/dev` at `6a8b5bd14187e5258a9805ee73cad6ff66cb9079`
- Tracking: focused follow-up for the `qrptSAsByWeek` crosstab `[1]` versus
  `W0`–`W11` output mismatch; issue number is owner-assigned separately.
- Scope: static QueryDef/SQL projection provenance only. Existing output facts
  now preserve alias kind, role-scoped source-expression hashes, aggregate
  source candidates, and pivot-expression source candidates.
- Decision: do not infer that numeric pivot literals are aliases for report
  control names. The output catalog exposes the evidence needed for that
  reconciliation while keeping the mismatch a gap.
- Safety: no query execution, recordset/row access, report rendering, or new
  Access COM surface.
- Validation: Access projector/composition focus passed (131 tests); the full
  solution built with zero warnings/errors; all 1,304 solution tests passed;
  the private-path guard and `git diff --check` passed.
- Deferred: exact DAO QueryDef property reconciliation from the private Windows
  copy, dynamic pivots, DCount runtime behavior, and report layout/runtime
  validation.
