# VB compilation-context gate

This gate fixes a coverage/CI omission, not the absence of a private application's
project metadata. Extractor versions and production behavior are unchanged.
Never claim that configuration strings or a synthetic project establish the
original application's compilation context.

| Dimension | Executable coverage | Expected boundary |
| --- | --- | --- |
| App_Code / page / mixed C# and VB × source / configured / alias imports × absent / short-name / missing / conflicting configured assembly identities | 36 ProjectlessCompilationContextMatrixTests cases | No semantic attempt or Tier1 promotion; workspace and per-file gaps retained; source syntax survives; deterministic fact IDs under inventory permutation |
| Configured import scopes, providers, assembly identities, clear/remove, aliases, limits and malformed input | VisualBasicConfiguredImportsTests | Hash-backed bounded syntax metadata, refusal outside supported scope |
| VB project, excluded source, generated source, compiler errors, mutation, privacy, determinism | VisualBasicValidationMatrixTests | Per-file semantic/fallback boundary; failed/partial is not clean |
| Registration × source language × scope × missing/duplicate/wrong targets; events × lifecycle | AppCodeRegistrationMatrixTests | Unique source candidates only; no assembly or runtime claim |
| Handler-to-database path and stacked property/constructor/nested-receiver paths | ProjectlessVisualBasicWebFormsDiagnosticsTests and StackedMigrationRegressionTests | Candidate paths and their reduced coverage survive |

Run the exact filter in `.github/workflows/vb-compilation-context-matrix.yml`.
Linux and Windows run the same gate with independent results. A timeout is a
failed gate, not a pass. The 36 new combinations use configured identity strings,
not actual loaded DLL collisions. Existing semantic tests exercise modern SDK
projects; they do not establish authentic ASP.NET/.NET Framework compatibility.

## Explicit uncovered work

Projectless semantic compilation is not implemented by this change. Before adding
it, define authoritative source membership, root namespace, imports, compiler
options, framework identity and exact reference bytes. Add positive and negative
tests for actual conflicting DLL identities, source/binary mismatch, missing
framework references, and partial compilations. No guessed reference set or
fabricated project may silently remove the projectless notices. Existing compiled
provenance tests remain a separate gate, not evidence of source compilation.

This is a finite cross-product plus existing interaction suites, not “all possible
VB programs.” Extend the table and executable cases when admitting a new shape.
No private rescan is required for this test-only change. Full solution tests,
non-.NET adapter suites and pinned external OSS smokes are deferred; run the public
stacked migration CLI sample as the focused integration smoke.
