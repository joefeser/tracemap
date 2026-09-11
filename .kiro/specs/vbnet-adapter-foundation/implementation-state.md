# VB.NET Adapter Foundation Implementation State

- Status: foundation-slice-implemented (spec tasks 1-4 complete; 5-11 open)
- Branch: `codex/issue-736-vbnet-adapter-foundation`
- Base: `origin/dev` at `c50f82ce0920d3c948e6ec798c6eb4b4d0959c24`
- Fixture corpus merged into this branch via PR #740 (merge commit
  `a357f7b9`); `samples/vb-modern-sample`, `samples/vb-legacy-sample`,
  `samples/vb-webforms-sample` are consumed directly by the implementation
  tests.
- Issue: [#736](https://github.com/joefeser/tracemap/issues/736)
- Parent: [#1](https://github.com/joefeser/tracemap/issues/1)
- Follow-ups: [#738](https://github.com/joefeser/tracemap/issues/738) for
  events/Web Forms and [#737](https://github.com/joefeser/tracemap/issues/737)
  for data/external boundaries.

## Implemented in this slice (tasks 1-4)

- **Inventory/classification (task 2).** `FileInventory` now includes `.vb`
  and classifies `VisualBasic`, `VisualBasicCodeBehind` (`.aspx.vb` /
  `.ascx.vb` / `.master.vb`), `VisualBasicDesigner` (`*.designer.vb`),
  `VisualBasicGenerated` (`*.generated.vb`, `*.g.vb`),
  `VisualBasicAssemblyInfo` (`AssemblyInfo.vb`), and promotes `.vbproj` from
  `NonCSharpProject` to a dedicated `VisualBasicProject` kind (`.fsproj`
  remains `NonCSharpProject`). Classification is filename-convention
  structural evidence only; it is never treated as semantic proof.
- **Snapshot protection (task 2).** `ScanEngine.CaptureSemanticInputSnapshot`
  covers VB source kinds and `VisualBasicProject`; the VB extractor registers
  repo-local VB documents as protected compilation inputs
  (`CompilationInputFiles`), with SDK/compiler-generated documents skipped so
  design-time builds that rewrite `obj/` sources do not fail verification.
  Checked-in designer/generated files stay protected through the
  inventory-level capture.
- **Packages, versions, rules (task 3).** `Microsoft.CodeAnalysis.VisualBasic`
  and `Microsoft.CodeAnalysis.VisualBasic.Workspaces` pinned at the
  repository's Roslyn version `5.3.0` in `TraceMap.Core`. Extractor identity
  `ScannerVersions.VisualBasicSemanticExtractor = "vb-semantic/0.1.0"`, fact
  type `VisualBasicProjectObserved`, rules `vb.semantic.compilation.v1`
  (Tier2Structural observation) and `vb.semantic.workspace.v1` (Tier4Unknown
  gaps), both cataloged in `rules/rule-catalog.yml` with limitations.
- **Project selection and semantic loading (task 4).** New
  `VisualBasicSemanticExtractor` mirrors the C# front end: solution and
  standalone `.vbproj` selection (scope/exclude-aware), MSBuild registration,
  opt-in restore, target-framework workspace property, per-project
  `GetCompilationAsync`, sanitized compilation diagnostic gaps, and a
  `VisualBasicProjectObserved` observation per compiled project.
  `ScanEngine` runs it after the C# extractor and merges results through
  `SemanticExtractionResultMerge` (stable ordering, union of
  analyzed/compilation-input sets, OR of attempt/reduction flags). The C#
  extractor now skips non-C#-language projects inside loaded solutions so
  mixed solutions have exactly one owner per language. Manifest `projects`
  includes VB projects; `ProjectDeclared` facts are emitted for `.vbproj`;
  scope filters and progress counts recognize the kind.
- **Honest reduced coverage.** Failed VB project load, compilation failure,
  compiler errors, or `.vb` sources without any `.vbproj` each emit
  `vb.semantic.workspace.v1` `AnalysisGap` facts (Tier4Unknown) and drive the
  manifest to `FailedOrPartial` / `...Reduced` labels. The legacy fixture
  scan is `FailedOrPartial` with sanitized compiler/workspace gaps; the
  modern fixture is `Succeeded`/`Level1SemanticAnalysis` with no gaps.

## Key decisions and oddities

- **`.vbproj` kind change.** `.vbproj` moved from `NonCSharpProject` to
  `VisualBasicProject`. Non-SDK `.vbproj` files now emit the
  `NonSdkStyleProject` (Tier2) build-environment diagnostic instead of
  `UnknownLegacyProjectFormat` (Tier4); `BuildEnvironmentDiagnosticTests`
  gained a `.fsproj` fixture to preserve the unknown-format coverage.
- **My-template exclusion.** The .NET SDK computes
  `CommandLineArgsForDesignTimeEvaluation` before appending
  `_MyType="Empty"`, so MSBuildWorkspace loads of standard VB projects inject
  the classic My template whose types do not bind on .NET Core (BC30002 for
  `Microsoft.VisualBasic.MyServices/Devices/ApplicationServices` with no
  source location; the same project builds cleanly). Compiler errors that
  point at no source location and reference that template surface are
  excluded from coverage gaps, recorded as `injectedTemplateErrorCount` on
  the observation fact, and documented in both VB rule catalog entries. This
  is deliberately bounded: source-referencing compiler errors always reduce
  coverage.
- **C# behavior preserved.** C# inclusion, tiering, fact families, and
  manifest semantics are unchanged for C#-only repositories (verified by a
  dedicated no-regression test over `samples/modern-sample`). The only C#
  code change beyond visibility of shared helpers is the solution-level
  language guard.
- **Deferred (intentionally).** `ProjectFileReader` (target frameworks,
  package references) still reads only `.csproj`-kind projects, so VB
  target-framework/package facts wait for the shared-artifact merge slice
  (task 7). Per-file VB syntax fallback (task 6), semantic declaration/call
  facts (task 5), symbol joins (task 7), the pinned OSS smoke (task 9), docs
  (task 10), and final PR validation (task 11) remain open.
- One flaky `MSBuildRegistrationFailed` observation occurred once during
  manual CLI testing immediately after a full solution build and never
  reproduced (dozens of runs since, including the full test suite). The
  C# extractor shares the same registration path; watch for recurrence in CI
  before relying on VB gaps for that failure category.

## Validation recorded (2026-09-11, macOS arm64, .NET SDK 10.0.302)

- `dotnet build src/dotnet/TraceMap.sln` - clean.
- Focused: `VisualBasicFoundationTests` + `VbNetFixtureTests` +
  `BuildEnvironmentDiagnosticTests` - 37/37 passed.
- Full `dotnet test src/dotnet/TraceMap.sln` - green (see PR notes for the
  exact run).
- CLI scans: `samples/vb-modern-sample` -> `Level1SemanticAnalysis` /
  `Succeeded`, 13 facts, `VisualBasicProjectObserved` with
  `documentCount=6`, `errorDiagnosticCount=5`, `injectedTemplateErrorCount=5`;
  `samples/vb-legacy-sample` -> `Level1SemanticAnalysisReduced` /
  `FailedOrPartial`, ~90 sanitized compiler-diagnostic gaps plus a workspace
  gap; `samples/vb-webforms-sample` -> reduced, validates cleanly.
- `python3 scripts/validate-adapter-artifacts.py` passes on all three VB
  scan outputs.
- Determinism: two consecutive modern-fixture scans produce identical fact
  sequences and identical source snapshot digests.

## Remaining work

- Task 5: compiler-backed VB declarations, occurrences, references, calls,
  construction, argument flow, and direct relationships (Tier1, VB rule IDs).
- Task 6: per-file VB syntax fallback with explicit Tier4 gaps.
- Task 7: merge VB rows into shared symbols/occurrences/call edges/object
  creations with backing-fact integrity; fold in VB target frameworks and
  package references from `ProjectFileReader`.
- Task 8: full synthetic test matrix extension (fallback, snapshot-mutation
  during a live scan, public-safety) on top of tasks 5-7.
- Task 9: pin and validate an open-source VB smoke repository
  (candidates pre-researched in `docs/VBNET_FIXTURES.md`; re-verify pins).
- Task 10: rule catalog/docs/acceptance updates with exact commands.
- Task 11: final validation pass and PR to `dev`.
