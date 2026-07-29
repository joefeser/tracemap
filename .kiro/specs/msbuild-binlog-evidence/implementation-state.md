# MSBuild Binlog Evidence Implementation State

- Status: implementation complete; ready for PR review
- Branch: `codex/msbuild-binlog-evidence`
- Base: `origin/dev`
- Base SHA: `26189d1dd8d97d5741005ae7cbd033840099f216`

## Scope decision

Implement the smallest useful v0 inside the normal `tracemap scan` artifact
pipeline. Binlogs are supplied only by repeated `--binlog` options and require
`--binlog-commit-sha` equal to the detected repository commit. The adapter uses
the pinned MIT-licensed official `Microsoft.Build` binary-log replay API and
projects only build result, repository-relative project/graph identity, bounded
diagnostic code/severity/location, aggregate counts, and explicit gaps. It
shares the existing MSBuild Locator registration and excludes parser runtime
assets so it cannot poison semantic-workspace assembly resolution.

The dependency is pinned to `Microsoft.Build` 18.6.3, MIT licensed, and the
NuGet vulnerability audit reports no known vulnerable packages in
`TraceMap.Core`. `ExcludeAssets="runtime"` keeps the package as a compile-time
contract; runtime replay and semantic project loading both use the toolset
selected by the existing `Microsoft.Build.Locator`. The adapter adds no
telemetry or outbound request path.

No downstream report composition beyond the standard facts/index/report
artifacts is planned for this slice. No target framework, configuration, package,
property, item, task, command, message, or embedded-content projection is
authorized.

## Validation

- Focused `MsBuildBinlogExtractorTests`: 6 passed.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors; the existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 warning remains outside this slice.
- `dotnet test src/dotnet/TraceMap.sln`: 939 passed.
- Pinned modern-sample product smoke: passed; observed one project and a
  successful recorded build with no local path in standard outputs.
- `./scripts/smoke-combined-paths.sh`: passed.
- `dotnet list ... package --include-transitive --vulnerable`: no vulnerable
  packages for `TraceMap.Core`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

An early implementation used `MSBuild.StructuredLogger`; full regression
testing showed that its older transitive MSBuild runtime assemblies could alter
semantic-workspace loading. It was removed. The final implementation uses the
official replay source with compile-only assets and shares Locator registration.

The full-repository smoke also exposed a pre-existing absolute-path boundary in
compiler-generated semantic evidence. It does not originate in binlog ingestion
and is tracked separately as #546.

## Deferred

- Evaluated framework/configuration evidence.
- Package evidence.
- Downstream review and public-site composition.
- Artifact attestation and stronger declared-commit corroboration.
- Out-of-process resource sandboxing.
