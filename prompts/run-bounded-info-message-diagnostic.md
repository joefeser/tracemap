# One guarded compiled-only InfoMessage diagnostic — Windows self-test first

This authorizes a resource-contained diagnostic, not an unguarded retry or an
acceptance scan. The original memory cause remains unknown. All old unguarded
InfoMessage run instructions remain suspended.

EXPECTED_IMPLEMENTATION_COMMIT: `8fd944aaf4cda4ff21d373fb8bea72be219f57ae`

Use that clean public implementation, or a verified docs-only descendant with
no scanner, test-worker or guard-script changes. Verify the built CLI source
commit/state, not merely the checked-out branch. Record the actual 40-character
tool SHA. IL extractor is 0.2.24 / evidence policy v25; InfoMessage policy is
explicit-sql-info-message/3, adding exact signed ordinal XML attribute and InnerXml
callback signatures under the existing framework-2 and framework-4 profiles. The compiled-only mode explicitly excludes all source inventory, MSBuild, semantic and syntax analysis. Source coverage is reduced; the empty source snapshot does not bind private source files. Preserve dirty
patches, private sources, binaries, receipts and outputs. Use isolated clean tool
sources. Read AGENTS.md and docs/validation/BOUNDED_SCAN_DIAGNOSTICS.md.

1. Ensure the prior runaway job is stopped. Do not reuse a photographed PID or
   kill unrelated dotnet/build processes. No private application/SQL execution.
2. Perform at most ONE public CLI build. Verify PowerShell 7 already exists. If
   a prerequisite is unavailable, stop; do not install tools or bypass checks.
3. Run exactly ONCE:
   pwsh -NoProfile -File scripts/Test-BoundedScanGuard.ps1
   This runs only public synthetic workers, under their guard limits. Require
   all five PASS lines AND WINDOWS JOB SELFTEST PASSED. If anything fails, stop
   and report the categorical failure. No private scan is permitted after failure.
4. Reuse the existing inner-root selectors and compiled inputs from local receipts;
   verify binary hashes locally against the retained baseline and report matches
   or unknowns. Record whether source scope is actually verified; do not assume it.
   Do not broaden roots, add framework inputs, restore private projects or edit code.
5. Choose ONE fresh output directory and an existing diagnostics parent directory
   outside ALL repositories and the scan output. Choose fresh, distinct phase and
   guard checkpoint names; no existing sidecars or temporary files may collide.
6. Construct the scan argument array, retaining --repo, the two compiled
   inputs, two selected inner roots and all four existing boundary flags. Add:
   --compiled-only
   --diagnostic-progress <fresh-phase-checkpoint>
   Remove source-only switches (restore, project/solution selectors, source include/exclude,
   target framework, binlog, VB compilation/import contexts, CI/deps indexing); do not
   change any compiled-input/root/boundary values. The mode rejects conflicting source
   switches. Do not omit --repo. Do not invoke the scan directly.
7. Run exactly ONCE in PowerShell 7, using structured variables populated from
   the verified private receipts, not invented paths or command strings:

   & "$toolRoot/scripts/Invoke-BoundedScan.ps1" `
     -Executable (Get-Command dotnet).Source `
     -Arguments $scanArguments `
     -CheckpointPath $guardCheckpoint `
     -TimeoutSeconds 60 -MemoryLimitMiB 2048

   $scanArguments must begin with the public tracemap.dll path and 'scan', and
   contain the new --diagnostic-progress argument. Never use -PublicTestMode here.
   Never raise limits, retry, run a second version, collect a new dump/profile,
   launch subagents or change private input code. If stopped, keep partial output
   and both checkpoints; do not resume or classify it as successful.
8. Return at most 30 sanitized text lines, then STOP:
   - exact clean tool SHA/build status; PowerShell version; five Windows self-test
     results; guard generator/input hashes, enforcement metric and configured caps;
   - scanner child PID status, guard state/reason/exit, elapsed WALL milliseconds,
     peak committed bytes and peak sampled job working-set bytes (keep distinct);
   - last phase checkpoint stage/state, last completed stage and relevant history:
     metadata/decode, materialization, lifecycle, callback/naming;
   - confirm zero source files/projects/solutions, SourceAnalysisExcludedByCompiledOnly
     gap and build NotRun; solution-load/compilation/MSBuild stages must be absent;
   - prior binary hash comparison and effective scope verification status;
   - output complete/incomplete; entry count only if actual valid facts exist.
     No output directory or exit zero alone establishes completion or phase.
   - next gap/phase as observed, not a claim about root cause or private acceptance.

Keep command values, source, SQL, private identifiers, complete receipts and logs
local. Use aliases and categorical summaries; no email, uploads or new photos.
This diagnostic is capped to identify where the scan spends resources, not to
prove the old 43.5-GiB failure is fixed. STOP after the one report.
