# Legacy Build Environment Diagnostics Implementation State

Status: implemented
Branch: codex/legacy-build-environment-diagnostics
Scope: core scanner/report implementation
Public claim level: hidden until reviewed
Readiness: merged

Post-promotion note: PR #120 implemented build environment diagnostics and PR
#247 promoted them to `main`.

## Summary

Implemented deterministic build environment diagnostics for TraceMap scans.
The scanner now emits `BuildEnvironmentDiagnostic` facts for static target
framework, toolset, project-format, restore-shape, generated/designer-file, and
sanitized workspace/restore categories. The implementation keeps diagnostics
evidence-backed with rule IDs, evidence tiers, file spans, commit SHA, extractor
IDs/versions, conservative guidance, and limitation text.

## Scope Decisions

- Added one additive fact type stored through existing `facts` /
  `properties_json` SQLite storage rather than a schema migration.
- Added rule catalog entries before emitting new rule IDs:
  `build.environment.target-framework.v1`,
  `build.environment.toolset.v1`,
  `build.environment.project-format.v1`,
  `build.environment.restore.v1`,
  `build.environment.generated-files.v1`, and
  `build.environment.workspace-diagnostic.v1`.
- Kept legacy `TargetFrameworkVersion` diagnostics separate from
  `scan-manifest.json` `TargetFrameworks`; existing manifest target-framework
  behavior is unchanged.
- Expanded inventory for `.props`, `.targets`, `.resx`, `.settings`,
  `.vbproj`, `.fsproj`, `packages.lock.json`, and `nuget.config`.
- Non-C# project files are structural diagnostics only and are not loaded by the
  C# semantic extractor.
- Existing workspace and restore `AnalysisGap` facts are sanitized at gap
  construction time. Raw native messages no longer feed `KnownGaps`, fact
  properties, fact IDs, SQLite rows, reports, or analyzer logs.
- `messageHash` is derived from the sanitized category message, not raw native
  output, so same-category raw messages remain fact-ID stable.
- Compiler diagnostic `AnalysisGap` facts retain only bounded, safe C#
  identifier tokens as reducer match keys; raw compiler prose remains
  sanitized away.
- Generated/designer diagnostics cover deterministic missing, malformed, and
  structurally unlinked WebForms, WCF service-reference, resource, and settings
  patterns.

## Oddities

- Explicit restore failures may categorize as generic `NuGetRestoreFailed` when
  stdout/stderr does not deterministically expose a more specific safe category.
- `./scripts/smoke-sample-repos.sh` was run as an optional pinned smoke and
  stopped on its existing strict expectation that the modern sample reducer
  output contains `DefiniteImpact`; current output was `NeedsReview`. The scan
  and reduce commands completed and artifacts were present. This appears to be
  a stale smoke assertion rather than a diagnostics artifact failure, so the
  script was not changed in this PR.

## Validation

Completed:

- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed.
- Focused review-loop regression test passed:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "CSharpSemanticExtractorTests|ReducerTests"`.
- CLI scan against checked-in `samples/modern-sample` passed and produced:
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

Additional smoke:

- `./scripts/smoke-sample-repos.sh` ran and stopped at the stale
  `DefiniteImpact` assertion described above.

## Follow-Ups

- Consider updating `scripts/smoke-sample-repos.sh` in a separate validation
  maintenance PR if `NeedsReview` is now the accepted reducer classification for
  the modern sample.
- Promote build environment rollups into combined or portfolio reports only if
  a later spec requests cross-source environment summaries.
