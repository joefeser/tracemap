# Final bounded validation — whole-protocol state control (one scan, then stop)

EXPECTED_IMPLEMENTATION_COMMIT: `f433e525e777514c30cdd439de28cf217c9a2f07`
TOOL: `il-body-evidence/0.2.27` · policy `explicit-il-body-evidence.v28` ·
state control `explicit-sql-state-control/1` · InfoMessage `/4`

This is the LAST planned private pass. It is pinned to the public whole-protocol
implementation and its synthetic gates; it does not authorize another micro-fix
loop. Either outcome below ends the six-week back-and-forth honestly.

## Preconditions (all public, before any private scan)

1. Public focused suite green at the commit above: IlSql + ScanDiagnosticProgress
   237 passed, 0 failed/skipped, Debug -warnaserror.
2. Public full shared-core Debug -warnaserror gate green on an immutable head copy
   (recorded in `.kiro/specs/sql-compiled-wrapper-evidence/implementation-state.md`).
3. Windows guard self-test: all five `scripts/Test-BoundedScanGuard.ps1` cases pass
   on the work machine (`success, timeout, child memory, parent exit/orphan, stale
   phase`). If the self-test fails, STOP — no private scan.

## The scan (one only)

Same shape as the v12-001 inventory run: `--repo` as before, `--compiled-only`,
the exact verified DLLs (binary receipts unchanged from v12-001) and the two
inner-root selectors, `--il-body-evidence --il-opaque-root-initializers
--il-external-trace-boundary --il-compiled-scalar-boundary
--il-info-message-boundary`, `--il-lifecycle-root` for both roots, fresh
output/progress paths, `scripts/Invoke-BoundedScan.ps1` with the 60-second /
2048-MiB job cap. No new flags: state control is automatic under the InfoMessage
boundary. No retry, no limit increase, no second scan.

## Expected outcomes — EITHER ends the loop

**A. Candidates with state alternatives.** The three `get_State` guard sites no
longer stop the run: entry candidates appear with `sqlLifecycleSchema =
il-sql-lifecycle-state-alternatives/1` and `stateControlPolicy`, carrying bounded
per-path alternatives (exceptional-termination paths included). This advances the
lifecycle claim only — it is NOT SQL acceptance, NOT row proof, NOT a
whole-effort-complete claim.

**B. A stop at a predicted missing signature.** Per the v12-001 reconciliation
(first-gap order), the expected stops are, in order:

1. the configuration chain in the root-side constructor
   (`ConfigurationManager.get_ConnectionStrings` group) — verify-against
   `--il-opaque-root-initializers`; do NOT catalog;
2. `ArrayList.Clear()` / `ArrayList.ToArray(Type)` in the open/exec path;
3. `set_CommandTimeout` / `set_Transaction` / `get_TableMappings` /
   `TableMappings.Add` around command construction;
4. the 4-arg `SqlParameter` ctor / `set_Direction` /
   `VB.Information.GetObjectValue` / `Object.GetType` in the parameter builders;
5. `String.Concat` overloads and `Interaction.IIf` / `Conversion.ToInteger` in the
   exec body; the crypto helper group (verify against
   `--il-compiled-scalar-boundary`) and the EventLog group (verify against
   `--il-external-trace-boundary`) only if traversal reaches them.

If the scan stops at any entry above, record the exact signature + site + refusal
and STOP: that is the documented gap for the next public batch, not a new loop.
If it stops somewhere NOT listed, record it the same way — still stop.

## Reporting

One sanitized report: which outcome (A/B), the stop signature + site if B, the
per-root gap facts, peak sampled working set, wall clock, and the exact commit.
No source, SQL, binaries, screenshots-to-catalog, or private identifiers. Child
exit zero and artifact publication alone prove nothing.
