# Legacy Build Environment Diagnostics Tasks

## Implementation Tasks

- [x] 1. Add diagnostic vocabulary and rule catalog entries. Requirements: 1, 7.
  - [x] Define the minimal fact type/property shape for build environment diagnostics.
  - [x] Add stable diagnostic codes and guidance codes.
  - [x] Ensure diagnostic fact IDs are derived from sanitized fields only.
  - [x] Specify and test the observed-value hash algorithm, deterministic input construction, `messageHash` convention, and category-only handling for secrets.
  - [x] Add rule IDs for target-framework, toolset, project-format, restore, generated-file, and workspace diagnostics.
  - [x] Document every new rule ID in `rules/rule-catalog.yml` with limitations before emitting that rule from scanner code.
  - [x] Add tests that facts include rule IDs, evidence tiers, commit SHA, spans, and extractor versions.

- [x] 2. Implement static project and toolset inspection. Requirements: 1, 2, 4, 7.
  - [x] Read SDK-style and non-SDK-style target framework declarations with line spans.
  - [x] Read legacy `TargetFrameworkVersion`, `ToolsVersion`, `VisualStudioVersion`, safe import basenames, and project type GUID categories.
  - [x] Extend `FileInventory` for any `.props`, `.targets`, `.resx`, `.settings`, `.vbproj`, or `.fsproj` diagnostics included in the slice, or explicitly defer unsupported extensions with gaps.
  - [x] Detect non-SDK-style, Web Application, known unsupported, and unknown project formats.
  - [x] Emit conservative guidance codes without evaluating arbitrary MSBuild conditions.
  - [x] Keep legacy `TargetFrameworkVersion` diagnostics separate from existing manifest `TargetFrameworks` unless compatibility tests approve adding them to the manifest list.
  - [x] Add deterministic ordering and stable IDs for diagnostics.

- [x] 3. Add restore-state diagnostics. Requirements: 3, 5, 7.
  - [x] Detect `packages.config`, `PackageReference`, `packages.lock.json`, and `nuget.config` shape without exposing raw sources or values.
  - [x] Represent `RestoreNotRequested` as manifest/report scan-option state, not a standalone fact, and avoid noisy diagnostics on clean successful scans.
  - [x] Capture sanitized restore failure categories only when explicit restore was requested.
  - [x] Hash or omit package source URLs, credentials, config values, and unsafe command output.
  - [x] Test skipped restore does not claim packages are missing.

- [x] 4. Add workspace/MSBuild diagnostic categorization. Requirements: 1, 2, 5, 6.
  - [x] Map MSBuild registration, SDK resolution, reference assembly, project load, and compilation creation failures to stable categories.
  - [x] Replace or sanitize the existing `csharp.semantic.workspace.v1` raw `message` property path at gap construction time.
  - [x] Replace or sanitize the existing explicit restore-failure gap message path at restore-output capture time.
  - [x] Ensure `ScanEngine.GetGapMessage`, manifest `KnownGaps`, fact creation, and fact ID hashing never observe raw native diagnostics.
  - [x] Emit `AnalysisGap` facts for unknown or unprovable causes.
  - [x] Preserve reduced coverage while allowing syntax/config fallback extractors to run.
  - [x] Sanitize diagnostics before writing facts, manifest gaps, reports, SQLite rows, or analyzer logs.
  - [x] Add tests for unsafe path, remote, credential, config, and source-like diagnostic inputs.

- [x] 5. Add generated/designer-file diagnostics. Requirements: 4, 6, 7.
  - [x] Detect missing, malformed, excluded, or unlinked generated/designer files for supported legacy patterns.
  - [x] Cover WebForms markup/code-behind/designer, WCF service-reference metadata/proxies, resources, and settings where deterministic.
  - [x] Connect generated/designer gaps to coverage limitations without suppressing existing syntax/config facts.
  - [x] Emit unknown gaps when expected generated behavior cannot be proven.

- [x] 6. Add artifact presentation. Requirements: 1, 5, 6, 7.
  - [x] Write diagnostics to `facts.ndjson` through existing stable fact serialization.
  - [x] Ensure diagnostics are queryable in `index.sqlite` through existing fact storage or an additive documented view/table.
  - [x] Keep `scan-manifest.json` compatible while adding stable gap or summary information only if needed.
  - [x] Add a deterministic `report.md` section for build environment diagnostics and pin its section order in tests.
  - [x] Ensure `logs/analyzer.log` is sanitized by default.
  - [x] Add compatibility tests showing combine, snapshot diff, portfolio, and report readers tolerate new diagnostic fact types.

- [x] 7. Add focused tests and fixtures. Requirements: 2, 3, 4, 5, 8.
  - [x] Test modern SDK-style project diagnostics.
  - [x] Test non-SDK-style target framework and old MSBuild toolset diagnostics.
  - [x] Test Web Application project indicators and unsupported project formats.
  - [x] Test packages.config and restore-not-requested behavior.
  - [x] Test sanitized restore failure categories.
  - [x] Test generated/designer missing, malformed, and unlinked gaps.
  - [x] Test semantic load failure still produces fallback syntax/config evidence and reduced coverage.
  - [x] Test unsafe values do not appear in facts, manifest, report, SQLite, or analyzer log artifacts.
  - [x] Test existing workspace/restore gap message regressions no longer leak raw diagnostics.
  - [x] Test manifest `KnownGaps` contains only sanitized categories or hashes.
  - [x] Test the real restore stdout/stderr capture path with unsafe output.
  - [x] Test sanitized-only fact ID stability when raw diagnostics differ but categories match.
  - [x] Test observed-value hash stability and distinct-value behavior.
  - [x] Test secret-like unsafe values are omitted or category-only rather than hashed.
  - [x] Test snapshot-diff or equivalent gap fingerprint stability after migration to `messageHash`-style properties.
  - [x] Test clean SDK-style successful scans do not emit noisy restore-not-requested diagnostics.
  - [x] Test multiple diagnostics for one project remain separate facts.
  - [x] Test inventory extension behavior or explicit unsupported-inventory gaps for `.props`, `.targets`, `.resx`, `.settings`, and non-C# project files included in scope.

- [x] 8. Validate implementation. Requirements: 8.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run the CLI against a checked-in sample or generated temporary fixture and verify `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
  - [x] Run relevant pinned smoke checks from `docs/VALIDATION.md` or record an explicit deferral rationale.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Promote diagnostics into combined or portfolio reports if users need
  cross-source environment rollups.
- Add more legacy project type GUID mappings after safe public fixtures prove
  the categories.
- Add first-class local-only raw diagnostic capture only if a later spec defines
  an explicit unsafe/debug artifact boundary.
