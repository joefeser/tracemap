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

## Bounded array follow-up (IL 0.2.70 / policy v71)

Owner authorized implementation and local case expansion on this branch.
Control-flow operand state now retains up to 16 local string arrays of at most
16 elements, charging heap entries to the existing work/slot bounds. Array
contents use immutable copy-on-write snapshots across state copies. Only exact
mscorlib 2/4 string[] allocations and Concat(String[]) consumers qualify. Local
aliases preserve allocation identity; unknown calls, addresses/field stores,
unsupported/repeated writes, missing initialization and conflicting joins refuse
contents. Bodies with back edges refuse array tracking because allocation-site
identity alone cannot distinguish loop generations. No general heap/array load,
parameter binding, transaction identity or SQL execution support is implied.

At the admitted concat call, argumentOrigins may carry string-array-elements
with a JSON-encoded ordered list of hashed string/scalar symbolic origins.
The reporting rule validates exact call/body/operand provenance, bounds and
allowed element kinds, then uses the existing bounded symbolic composition
projection. Malformed/unknown/oversize snapshots refuse. Scalar two/three/four
string concat and the string-array overload are exact-profile recognized.
No raw strings, joined plaintext or assumed unknown values are emitted.

Expanded direct controls cover explicit null/empty/16/17-element arrays, local
aliases, 16-array cap, missing/repeated/out-of-range/unknown writes, escaped slots,
byref/field/element-address exposure, branches, back edges, fake framework
identities and the legacy-2 profile. Compiled C# and VB cover direct/compiler
selected/local/nested/protected shapes and object-array/escape refusals. Retained
fact tamper tests cover provenance, malformed encoding, oversize and unknown
origins. One unknown-element control exposed too-permissive report validation;
fixed it before proceeding. The preceding focused lane passed 152, skipped one
existing Windows-only publish test, failed zero. Final expanded lane/full gate
are pending; no private validation or completion claim.

Public CLI smoke completed: 530 facts, two accepted array snapshots, three exact
array-case root reports, failedQueries=0. The escaped-array case remains an
explicit refusal. These are compile/static-only samples, never executed SQL.
Use prompts/verify-bounded-string-array-concat-once.md after final validation.

Final expanded focused gate passed: 266 passed, 1 existing Windows-only skip,
0 failed, 3m1s, Debug -warnaserror. Filter: IlStringArrayValueTests,
SqlWrapperRouteTests, IlCallValueExtractorTests, IlCommandBindingExtractorTests,
IlBodyEvidenceExtractorTests, SqlDatabaseMatrixTests. Python SQL report helper
17/17 passed. Diff/private-path checks passed. Full required Debug gate pending
on the immutable implementation commit; publish remains provisional until that
result is recorded. No branch-specific budget increases or private runs.


### Exact-head full gate completed

Implementation `17d5ab2f65c109116c07dd600064d96742de65b0` passed the complete
Debug gate in a clean exact-head clone on Windows-hosted WSL Ubuntu-24.04:

```sh
dotnet build src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build -c Debug -warnaserror --filter 'FullyQualifiedName!~TraceMap.Tests.SelfDiagnosticsTests'
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build -c Debug -warnaserror --filter 'FullyQualifiedName~TraceMap.Tests.SelfDiagnosticsTests'
```

Build: 0 warnings/errors. Main lane 6132 passed, 3 existing skips, 0 failed,
14.0616 minutes; isolated same-binary diagnostics 4 passed, 0 skipped/failed,
1.6282 seconds. Total 6139 tests: 6136 passed, 3 skipped, 0 failed. The documented
global-listener isolation remains; no claim of fixing that unrelated test race.

Exact committed-head public CLI smoke repeated: 530 facts, two accepted array
snapshots, all three exact VB array-root reports, failedQueries=0. The native
Windows ASP.NET publisher smoke remains the existing platform skip locally/WSL;
no private/Windows application/runtime validation is claimed. Compile-only native
C#/VB profiles and the full static regression gate passed.

Final closing diff is task/state/prompt documentation only, with no src/dotnet,
scripts or samples delta from the tested implementation. Private-path and diff
checks passed. Pushed on codex/workspace-scope-preflight without PR/merge. Owner's
next action is the single bounded private verification prompt, not another setup
wizard or an automatic series of scans. Complex parameter-loop command binding,
transaction identity and unrelated array/StringBuilder/enumerable capabilities
remain outside this change.

### VB source argument review

Branch `codex/workspace-scope-preflight`, rule `vb.syntax.sql-text-argument.v1`,
extractor `vb-syntax/0.4.19`. The public sample is `samples/vb-sql-argument`.
The scanner exports the SQL verb, construction and call spans, textual target
and unverified command-instance status. This additive source rule does not
join to compiled evidence. The sample report and SQLite query each produced
the two expected hits and excluded the four controls. Hand review can use
the sample source directly. Private verification is outside this slice.

Validation on implementation `abe1b6284cb55564b66ae8c4e44fdf3ee966f3e2`:

- Focused VB SQL argument, VB extraction and SQL wrapper tests passed 67/67.
- Full Debug solution with warnings as errors passed 6,355 tests with three
  skips. The separate SelfDiagnosticsTests pass added four. Total 6,359
  passed, three skipped, zero failed.
- The sample scan retained 47 facts with matching NDJSON and SQLite IDs,
  SQLite integrity OK and exactly two review rows.
- The pinned community-visual-basic smoke completed with expected reduced
  coverage at `20d2a51dfc9f342848ad134952ceaa8d79302559`. Private-path
  guard passed.

Full gate commands:

```sh
dotnet test src/dotnet/TraceMap.sln -c Debug --no-restore -warnaserror --nologo --filter 'FullyQualifiedName!~TraceMap.Tests.SelfDiagnosticsTests'
dotnet test src/dotnet/tests/TraceMap.Tests --no-build -c Debug -warnaserror --filter 'FullyQualifiedName~TraceMap.Tests.SelfDiagnosticsTests'
```
