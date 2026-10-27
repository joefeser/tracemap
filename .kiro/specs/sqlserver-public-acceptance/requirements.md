# Bounded public SQL acceptance package

Parent: `codex/sqlserver-field-reads` at
`6aa8c40e975b9277418aa3bc4b561073887c7f3e`. Child:
`codex/sqlserver-public-acceptance`. This stack does not change the parent’s
fixtures, contracts, tests, runtime harness, or completion claims.

## Finite denominator and completion criteria

1. Recheck exactly one real public operation: DNN v9.13.8 `GetRoleGroups`, pin
   `33b8fbe440ab665b8423599789db1f34d2a500fc`, through the existing complete-file
   slice. Preserve its source/license verification and exact partial scan checks.
   Missing reader/provider, qualifier and hydration bindings are blockers, not
   successful end-to-end DNN validation.
2. Replay four existing independently authored C#/VB native matrices unchanged:
   parameters (52 selected rows), reader results (82 JSON + 104 DDL rows), private
   helpers (230 rows), and Field reads (156 rows). These are synthetic controls,
   never additional real DNN operations. Preserve per-case outcomes and refusals.
3. Produce one canonical local evidence packet with script/input/binary
   hashes, exact revision, per-leg counts, typed real-application blockers and
   explicit no-aggregate-clean verdict. Do not join separate fixture facts by
   short procedure names or substitute parameter bindings for consumer bindings.
   Native comparisons repeat byte-identically; the outer packet binds actual
   run-specific scan/log bytes, so fresh-run clocks need not yield identical hashes.
4. One command performs all offline steps from committed public sources. No
   application method, SQL, container, SSH or full DNN build executes. Public DNN
   fetch is explicit; no private source or previously generated arbitrary receipt
   is accepted as a replacement for fresh execution.
5. Guard against source/binary drift, missing/extra/duplicate cases, false counts,
   stale versions, missing refusals, changed report bytes and absent provenance.
6. One consolidated later acceptance prompt records all local findings and asks
   for only genuinely missing private schema/identity inputs together. Full-suite
   and live gates remain pending while the parent owns the validation lane.

## Not a new feature roadmap

No ORM, PetaPoco, generic hydration, qualifier inference, SQL interpreter or
deployment proof. No production defect is presumed. Issue #870 remains broader
than this finite package; #908 does not close merely because synthetic tests pass.
