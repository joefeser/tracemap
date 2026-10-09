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
