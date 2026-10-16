# Verify database evidence-admission hardening

Use this prompt in the public TraceMap checkout. No private application inputs,
builds, database connections or rescans are needed. Do not modify code, commit,
push or merge during this verification.

## Revision

Fetch `origin/codex/evidence-admission-hardening`. Verify that the selected branch
contains the accepted pre-PR code commit `5b2a4d32a8e6538cd5169fc6e4abfa404250d0f3`.
Select the fetched branch tip, including subsequent review fixes, rather than
checking out that ancestor. The original database-admission implementation was
`af2d00ff01f41501f51e892ab1f41e9620350913`; it remains part of the history.
Record the actual checked-out HEAD and compare the implementation with its base,
`dbdd9241e48550dfdf779e7842d04c1912d659eb` (merged dev baseline).
Read the final receipts in `.kiro/specs/evidence-admission-regression-lock/implementation-state.md`;
do not attribute an earlier full-suite result to a later code fix. Preserve local
changes; if checkout would overwrite them, stop and report the conflict.

The Windows follow-up fixes canonical LF Markdown hashing, refusal of redacted
absolute-path markers as relative evidence locations, and cleanup of read-only Git
files in synthetic fixtures. These are validated by the Windows CI comparison,
not by another private-work-machine full-suite run. The base is deliberately
retained as a failing control; inspect candidate job results separately.

## Acceptance

1. Persisted parameter direction and type-kind lists reject unknown, empty,
   duplicate and noncanonical values. Null optional fields and the standalone
   `unavailable` marker remain compatible. Valid canonical lists are admitted.
2. All three raw metadata lists (directions, type kinds and type names) enforce
   the 8,192-character and 128-item bounds before splitting/token allocation.
   Invalid raw metadata becomes unavailable; it must not become positive evidence.
3. Correctly rehashed handoffs still reject database nodes with missing or invalid
   rule, tier, commit, source index, scan, file or line-span provenance. Exercise
   both displayed paths and inventory-only evidence nodes.
4. Valid source evidence and compiled evidence with paired `-1/-1` unavailable
   line spans still round-trip. Shape validation is not proof of source/binary
   correspondence, runtime execution or verified framework ancestry.

## Run

The work-machine acceptance is deliberately focused. Do not run the full solution
suite here, repeat an aborted run, or start background diagnostic runs. Full-suite
validation belongs in CI. Previously accepted focused checks and sample results
can be carried forward across prompt/CI-only commits; do not rerun them merely
because this prompt changed.

If the implementation has not already been accepted, run once from the repository
root (stop on failure):

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~SqlReviewRegressionTests|FullyQualifiedName~GroupedCompiledPathHandoffTests" --nologo --blame-hang-timeout 2m --blame-hang-dump-type none --logger "console;verbosity=normal"
```

Choose two fresh local output directories outside the repository. Replace the
placeholders below with their actual paths; do not overwrite existing artifacts.

```text
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/stacked-migration-regression --out <FIRST_OUTPUT_DIRECTORY>
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/stacked-migration-regression --out <SECOND_OUTPUT_DIRECTORY>
```

Compare `facts.ndjson` and `report.md` byte-for-byte between runs using available
local tooling. Check that all four synthetic operation groups retain candidate
paths and that the required scan outputs exist: scan-manifest.json, facts.ndjson,
index.sqlite, report.md and logs/analyzer.log. Keep reduced-analysis labels intact.

## Return

Report the exact implementation and checkout commits, focused test totals,
skips and their reasons, deterministic comparison results, and any failed
acceptance condition. Distinguish not-run or blocked checks from passing checks.
Do not silently install missing tooling or waive failures.

If a previous full suite crashed, retain that result as blocked. Do not call the
crash pre-existing merely because a test file was unchanged. The repository's
`Windows test-host baseline comparison` workflow runs the implicated classes in
separate processes on the pinned base and candidate, with bounded timeouts and
crash diagnostics. Both failing similarly supports a baseline failure; only the
candidate failing supports a regression. Both passing leaves the full-suite crash
unresolved (including possible test interaction). None of these outcomes waives
the full-suite gate. Do not kill all `testhost` processes on the machine.

Do not rescan the private application during this public verification. The branch
also contains separately tested extraction and call-graph fixes; verify their
current versions from Models.cs and the path reporter when checking their scope.
None of this establishes resolution of the previously reported 266 private
application analysis-gap facts.
