# VB compilation-context gate

The original gate fixed a coverage/CI omission. The opt-in implementation is now
documented in [EXPLICIT_VB_CONTEXT.md](EXPLICIT_VB_CONTEXT.md), with separate
`ExplicitVisualBasicContextTests`. Default projectless behavior is unchanged.
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

Automatic reconstruction of projectless compilation is not implemented. The new
explicit-input mode requires source membership, root namespace, imports, compiler
options and exact reference bytes; its tests cover successful and partial binding,
hash mismatch, duplicate reference identities and missing references. Authentic
website assembly partitioning and source/binary correspondence remain unproven.
No guessed reference set or
fabricated project may silently remove the projectless notices. Existing compiled
provenance tests remain a separate gate, not evidence of source compilation.

This is a finite cross-product plus existing interaction suites, not “all possible
VB programs.” Extend the table and executable cases when admitting a new shape.
The original test-only gate required no private rescan. Acceptance of the new
opt-in semantic path requires an approved context and fresh scan. Full solution tests,
non-.NET adapter suites and pinned external OSS smokes are deferred; run the public
stacked migration CLI sample as the focused integration smoke.
