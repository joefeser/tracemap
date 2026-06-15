# Legacy Build Environment Diagnostics Tasks

## Implementation Tasks

- [ ] 1. Add diagnostic vocabulary and rule catalog entries. Requirements: 1, 7.
  - [ ] Define the minimal fact type/property shape for build environment diagnostics.
  - [ ] Add stable diagnostic codes and guidance codes.
  - [ ] Ensure diagnostic fact IDs are derived from sanitized fields only.
  - [ ] Specify and test the observed-value hash algorithm, deterministic input construction, `messageHash` convention, and category-only handling for secrets.
  - [ ] Add rule IDs for target-framework, toolset, project-format, restore, generated-file, and workspace diagnostics.
  - [ ] Document every new rule ID in `rules/rule-catalog.yml` with limitations before emitting that rule from scanner code.
  - [ ] Add tests that facts include rule IDs, evidence tiers, commit SHA, spans, and extractor versions.

- [ ] 2. Implement static project and toolset inspection. Requirements: 1, 2, 4, 7.
  - [ ] Read SDK-style and non-SDK-style target framework declarations with line spans.
  - [ ] Read legacy `TargetFrameworkVersion`, `ToolsVersion`, `VisualStudioVersion`, safe import basenames, and project type GUID categories.
  - [ ] Extend `FileInventory` for any `.props`, `.targets`, `.resx`, `.settings`, `.vbproj`, or `.fsproj` diagnostics included in the slice, or explicitly defer unsupported extensions with gaps.
  - [ ] Detect non-SDK-style, Web Application, known unsupported, and unknown project formats.
  - [ ] Emit conservative guidance codes without evaluating arbitrary MSBuild conditions.
  - [ ] Keep legacy `TargetFrameworkVersion` diagnostics separate from existing manifest `TargetFrameworks` unless compatibility tests approve adding them to the manifest list.
  - [ ] Add deterministic ordering and stable IDs for diagnostics.

- [ ] 3. Add restore-state diagnostics. Requirements: 3, 5, 7.
  - [ ] Detect `packages.config`, `PackageReference`, `packages.lock.json`, and `nuget.config` shape without exposing raw sources or values.
  - [ ] Represent `RestoreNotRequested` as manifest/report scan-option state, not a standalone fact, and avoid noisy diagnostics on clean successful scans.
  - [ ] Capture sanitized restore failure categories only when explicit restore was requested.
  - [ ] Hash or omit package source URLs, credentials, config values, and unsafe command output.
  - [ ] Test skipped restore does not claim packages are missing.

- [ ] 4. Add workspace/MSBuild diagnostic categorization. Requirements: 1, 2, 5, 6.
  - [ ] Map MSBuild registration, SDK resolution, reference assembly, project load, and compilation creation failures to stable categories.
  - [ ] Replace or sanitize the existing `csharp.semantic.workspace.v1` raw `message` property path at gap construction time.
  - [ ] Replace or sanitize the existing explicit restore-failure gap message path at restore-output capture time.
  - [ ] Ensure `ScanEngine.GetGapMessage`, manifest `KnownGaps`, fact creation, and fact ID hashing never observe raw native diagnostics.
  - [ ] Emit `AnalysisGap` facts for unknown or unprovable causes.
  - [ ] Preserve reduced coverage while allowing syntax/config fallback extractors to run.
  - [ ] Sanitize diagnostics before writing facts, manifest gaps, reports, SQLite rows, or analyzer logs.
  - [ ] Add tests for unsafe path, remote, credential, config, and source-like diagnostic inputs.

- [ ] 5. Add generated/designer-file diagnostics. Requirements: 4, 6, 7.
  - [ ] Detect missing, malformed, excluded, or unlinked generated/designer files for supported legacy patterns.
  - [ ] Cover WebForms markup/code-behind/designer, WCF service-reference metadata/proxies, resources, and settings where deterministic.
  - [ ] Connect generated/designer gaps to coverage limitations without suppressing existing syntax/config facts.
  - [ ] Emit unknown gaps when expected generated behavior cannot be proven.

- [ ] 6. Add artifact presentation. Requirements: 1, 5, 6, 7.
  - [ ] Write diagnostics to `facts.ndjson` through existing stable fact serialization.
  - [ ] Ensure diagnostics are queryable in `index.sqlite` through existing fact storage or an additive documented view/table.
  - [ ] Keep `scan-manifest.json` compatible while adding stable gap or summary information only if needed.
  - [ ] Add a deterministic `report.md` section for build environment diagnostics and pin its section order in tests.
  - [ ] Ensure `logs/analyzer.log` is sanitized by default.
  - [ ] Add compatibility tests showing combine, snapshot diff, portfolio, and report readers tolerate new diagnostic fact types.

- [ ] 7. Add focused tests and fixtures. Requirements: 2, 3, 4, 5, 8.
  - [ ] Test modern SDK-style project diagnostics.
  - [ ] Test non-SDK-style target framework and old MSBuild toolset diagnostics.
  - [ ] Test Web Application project indicators and unsupported project formats.
  - [ ] Test packages.config and restore-not-requested behavior.
  - [ ] Test sanitized restore failure categories.
  - [ ] Test generated/designer missing, malformed, and unlinked gaps.
  - [ ] Test semantic load failure still produces fallback syntax/config evidence and reduced coverage.
  - [ ] Test unsafe values do not appear in facts, manifest, report, SQLite, or analyzer log artifacts.
  - [ ] Test existing workspace/restore gap message regressions no longer leak raw diagnostics.
  - [ ] Test manifest `KnownGaps` contains only sanitized categories or hashes.
  - [ ] Test the real restore stdout/stderr capture path with unsafe output.
  - [ ] Test sanitized-only fact ID stability when raw diagnostics differ but categories match.
  - [ ] Test observed-value hash stability and distinct-value behavior.
  - [ ] Test secret-like unsafe values are omitted or category-only rather than hashed.
  - [ ] Test snapshot-diff or equivalent gap fingerprint stability after migration to `messageHash`-style properties.
  - [ ] Test clean SDK-style successful scans do not emit noisy restore-not-requested diagnostics.
  - [ ] Test multiple diagnostics for one project remain separate facts.
  - [ ] Test inventory extension behavior or explicit unsupported-inventory gaps for `.props`, `.targets`, `.resx`, `.settings`, and non-C# project files included in scope.

- [ ] 8. Validate implementation. Requirements: 8.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run the CLI against a checked-in sample or generated temporary fixture and verify `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
  - [ ] Run relevant pinned smoke checks from `docs/VALIDATION.md` or record an explicit deferral rationale.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Deferred Follow-Ups

- Promote diagnostics into combined or portfolio reports if users need
  cross-source environment rollups.
- Add more legacy project type GUID mappings after safe public fixtures prove
  the categories.
- Add first-class local-only raw diagnostic capture only if a later spec defines
  an explicit unsafe/debug artifact boundary.
