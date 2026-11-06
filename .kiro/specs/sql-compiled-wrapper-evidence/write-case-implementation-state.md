# Write-case evidence reconciliation — 2026-11-06

Branch: `codex/workspace-scope-preflight`. Scope: public VB fixtures, focused
regression tests, documentation and one work-machine reconciliation prompt.
No Core/Reporting scanner implementation, schema, budget, ACK or SQL parser change.
No private inputs or source copied into fixtures; no database execution.

## Measured outcomes

- Simple incremental UPDATE through the separate compiled wrapper recovers a
  symbolic composition with exact hashed literal operands. This was already
  supported; readable SQL is intentionally not stored in ordinary facts.
- The custom-parameter ArrayList wrapper loop retains ExecuteNonQuery routing,
  but ordinary command binding emits IlCommandTextBindingUnavailable (also
  operand/parameter-flow gaps). This test does not exercise selected-root
  lifecycle extraction and does not imply it has the same limitation.
- Transactional DELETE + bulk-copy retains both endpoint routes. DELETE binding
  is unavailable for this body. The exact destination setter has a linked
  ManagedIlCallValuesObserved constant-string-hash operand, matched to the
  independently authored destination. Bulk destination path projection and
  transaction identity are not established. Commit/rollback call observations
  are not execution or transaction linkage proof.
- Missing-provider controls refuse complete paths.

Initial positive binding expectations failed on both complex bodies. Tests were
split into a simple positive text control and explicit complex-body gap controls,
without altering the extractor or assuming unavailable bindings. One test selector
initially also matched a caller's parameter type; corrected it to exact declaring
owner encoding. These were test investigation failures, not product fixes.

## Validation

```sh
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --nologo --no-restore --filter 'FullyQualifiedName~SqlWrapperRouteTests|FullyQualifiedName~SqlDatabaseMatrixTests'
python3 scripts/validation/test_sql_wrapper_report.py
```

.NET: 28 passed, 0 failed/skipped, 1m24s. Python: 17 passed. Full suite not
repeated for fixture/test/docs-only changes, following AGENTS touched-lane policy.
The earlier full Debug gate belongs to the parent head, not this expanded fixture.

CLI compiled-only scan of the public website plus both absolute compiled inputs
completed with 498 facts and 100 ManagedIlCallObserved facts. The existing helper
reported all three exact WritePage roots with selections=3, failedQueries=0.
Coverage remains reduced (compiled-only excludes source semantic analysis).
An earlier smoke supplied inputs relative to the source repo incorrectly and
produced six gap facts; preserved separately, then corrected to absolute paths
in a fresh output directory. Do not treat exit zero alone as scan acceptance.

Diff check and private-path guard passed. Use
`prompts/verify-inline-and-bulk-evidence-once.md` for a bounded private review.
No PR/merge or private rescan performed. Remaining implementation gaps above
are explicit follow-up capabilities, not claimed fixed by adding coverage.
