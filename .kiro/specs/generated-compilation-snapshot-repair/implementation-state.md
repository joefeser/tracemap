# Implementation state

PR #847, target dev. Repair branch codex/snapshot-review tracks the existing
zcode/snapshot-diff-naming PR head e53824df; no force push or merge.
Owner requested consolidated fixes and a fresh Codex review on #847 and #848;
Baz re-reviews automatically. ACK 0.5.5 has a known empty-body Baz review URL
reader defect, so owner authorized repair despite that review-batch blockage.
Qodo is retired; the lane now uses Codex and exact-head Baz evidence.

Generated filenames/path segments nominate exact compilation inputs for capture;
they never exempt inputs from hashing. Capture is bounded to 4,096 files and
64 MiB, rejects links/escaping paths, and feeds both protected-input verification
and the authoritative source snapshot. Only exact semantic compilation paths are
added, never arbitrary bin output. Newly appearing ordinary source still fails.
Re-read details are categorical and never copy native exception messages/paths.

Validation: locked restore succeeded. 51 focused tests pass with --no-restore
-warnaserror and filter FullyQualifiedName~ScanEngineTests|FullyQualifiedName~CliTests|FullyQualifiedName~SemanticExtraction.
Tests cover generated same-size content changes, obj/Obj/BIN casing, missing,
escaping, oversized and linked inputs, and private-path-safe read failures.
No external adapter behavior changed; pinned external adapter smokes deferred.
Windows host coverage is deferred to hosted CI. Full final-head suite and review
remain required; local focused results do not establish merge readiness.

Public ASP.NET endpoint sample CLI smoke passes with 103 facts and semantic
coverage. An initial smoke command named the #848-only deps fixture, which is
absent on this independent branch and correctly returned input-unavailable;
the corrected existing sample succeeded. Private-path and diff guards pass.

## Returned review: compiler-document identity

The fresh review exposed a post-extraction race and omitted SDK-generated inputs.
C# and VB now retain checksums from immutable Roslyn documents before fact
extraction, including generated project documents. Post-generation disk capture
must match those compiler checksums, and final verification rechecks them.
Multiple project evaluations with conflicting document checksums fail closed.
Bare generated-looking names no longer authorize admission. Only exact compiler
inputs below inventory-excluded obj/bin directories may enter bounded capture,
and only with compiler checksum evidence; this establishes observed compilation
identity, not a claim about who generated the file. New ordinary source outside
those excluded directories still requires its pre-evaluation baseline.

The original SourceSnapshotException(Exception) CLR constructor is preserved.
Additional regression cases cover a rewrite before post-extraction capture,
missing compiler evidence, and real SDK GlobalUsings/AssemblyInfo snapshot
coverage. Operator runbook and lane tests now reflect retired Qodo and required
Codex/Baz. The validation paragraph was moved to remove the inter-PR docs conflict.

Final local validation: 160 focused C#/VB/CLI/snapshot tests passed with
--no-restore -warnaserror (36s), including all 95 matching VisualBasic tests.
Three reviewer-lane regressions pass. Generated compiler inputs remain only in
SourceSnapshotInventory, not the ordinary extraction inventory; SDK-generated
GlobalUsings and AssemblyInfo are positively asserted. Final-head hosted full
suite and Windows checks remain required. No runtime application execution.

## Final diagnostics and Windows validation limitation

Fresh Codex findings now name validated repository-relative paths in compiler
checksum errors and escape control/line-separator characters in CLI details.
166 focused cases pass with warnings as errors (57s). The final exact-type
IOException assertion was rebuilt and all five wizard tests passed (19s).

Hosted Windows run 37883651935 on 01e5dc94 failed one of 148 corpus tests:
WebFormsWizardExecutionTests.Output_failure_after_verified_completion_preserves_completed_cursor.
The retained public synthetic artifacts show a succeeded scan receipt followed
by a scan-failed native artifact-validation checkpoint. This projectless case
has no generated compiler inputs. An isolated local rerun passed (one test, 3s);
root cause is not established and the Windows failure is not erased by that pass.
The test retains its required IOException assertion and now includes the actual
exit code and categorical native diagnostics when completion was never reached.
Final-head hosted Windows rerun is required; no customer/private-worker execution.

## 2026-10-09 renewed current-head repair

Branch: codex/snapshot-review, remote zcode/snapshot-diff-naming. Owner renewed
repair of both PR heads after hosted failures. This batch preserves the three
separate boundaries: ordinary source-scope membership, immutable compiler input
identity and runtime Git availability. Exact-source validation uses ordinary
inventory; generated compiler inputs remain hashed in the authoritative snapshot.
Snapshot exceptions cannot be swallowed by C#/VB workspace fallback. Cross-language
checksum conflicts retain validated relative paths; Unix filename colons remain
readable while drive/absolute/traversal paths are redacted.

The Linux full-suite failure was reproduced locally: a hidden framework-attribute
file under custom build-int was omitted by FileInventory, and the fixture's first
post-commit evaluation changed AssemblyInfo from 965 to 1006 bytes. Exact compiler
inputs omitted by Hidden/System attributes now receive bounded checksum-backed
capture, like obj/bin inputs. The unchanged-scan fixture builds again after its
Git commit to stabilize generated informational-version metadata. Existing mutation
guards are retained; a scan that changes initially inventoried bytes still fails.

Windows previously failed WEBFORMS_REVIEW_SOURCE_IDENTITY_CHANGED before the output
writer was reached. The completion test now uses the existing nonparallel Git
metadata sensitive collection; production Git timeouts, retries and identity gates
are unchanged. This isolates the completion assertion from unrelated concurrent
Git-heavy corpus load. It is not proof of the exact transient probe that failed;
final-head hosted Windows validation is still required.

Added a real ACK 0.5.5 offline consumer regression using the actual lane YAML:
missing/stale Baz holds a clean Codex batch, current Baz permits it, retired Qodo
is not requested or awaited. The separate manual request-review command is outside
that lane test and remains prohibited for retired Qodo. No network/provider calls
are made by the regression; no ACK source changes or paid review invoked.

Focused command: dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
--no-restore -warnaserror --filter
'FullyQualifiedName~CSharpFullSnapshotStabilityTests|FullyQualifiedName~ScanEngineTests|FullyQualifiedName~CSharpSemanticExtractorTests|FullyQualifiedName~VisualBasic|FullyQualifiedName~ExactSource|FullyQualifiedName~WebFormsWizardExecutionTests'
passed 176 tests (71s). Lane configuration tests: 3 pass. Pinned consumer tests:
3 pass with ACK_ROOT pointing to the verified v0.5.5 installation. Full Debug
solution validation completed: dotnet test src/dotnet/TraceMap.sln --no-restore
-warnaserror passed 3,451 tests, zero failed, one Windows-only skip (20m18s).
The failed Linux case and all wizard completion assertions now pass in the full
suite. Windows hosted validation remains required; no retry erased a failure.

Public endpoint-server-aspnet CLI smoke: 103 facts, Level1SemanticAnalysis;
validate-adapter-artifacts.py passed. Private-path and diff guards passed.
