# Swift Adapter v0 Scaffold CLI and Output Contract Tasks

Issue: [#378](https://github.com/joefeser/tracemap/issues/378)

All tasks are implementation tasks for a future PR. They are intentionally unchecked because this PR is spec-only.

## Implementation Plan

### Phase 0: Scope Lock

- [ ] 0.1 Confirm the implementation branch name and update this spec's implementation state before coding.
- [ ] 0.2 Re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/ACCEPTANCE.md`, `docs/VALIDATION.md`, issue #378, and parent issue #377.
- [ ] 0.3 Confirm the Swift adapter implementation root, preferring `src/swift`.
- [ ] 0.4 Confirm the adapter host/tooling choice and record exact local build/test commands in `implementation-state.md` before starting Phase 1.
- [ ] 0.5 Confirm this slice does not implement SwiftSyntax, SourceKit, SwiftPM semantic, Xcode semantic, UI, HTTP, storage, serializer, or runtime analysis beyond scaffold-level inventory/gap proof.
- [ ] 0.6 Confirm the scanner will not execute app code, run simulators/devices, run target tests, resolve packages over the network, or require successful Xcode build for basic useful output.
- [ ] 0.7 Confirm real Swift v0 scaffold output cannot claim full semantic coverage or runtime proof.

### Phase 1: Adapter Project and CLI Scaffold

- [ ] 1.1 Create `src/swift` package/build files, source layout, tests, and README.
- [ ] 1.2 Add CLI entry point with `scan`, `--repo`, `--out`, repeatable `--project`, repeatable `--include`, repeatable `--exclude`, `--max-file-byte-size`, `--help`, and `--version`.
- [ ] 1.3 Add deterministic version reporting and extractor version constants.
- [ ] 1.4 Document and test fixed `--max-file-byte-size` default `1048576` bytes.
- [ ] 1.5 Add safe output directory preparation that rebuilds only the requested output path.
- [ ] 1.6 Add missing repo, invalid output, and help/version tests.

### Phase 2: Git Metadata, Inventory, and Scan IDs

- [ ] 2.1 Implement Git metadata detection for repo name, remote URL when available, branch when available, and concrete commit SHA.
- [ ] 2.2 Fail before writing success artifacts when commit SHA is unavailable.
- [ ] 2.3 Implement selected file inventory for Swift and project metadata files.
- [ ] 2.4 Implement default excludes for `.git`, TraceMap outputs, `.build`, DerivedData, Xcode build products, dependency caches, Pods build outputs, Carthage build/checkouts, and hidden tool caches by normalizing repository-relative paths, splitting them into path segments, and checking individual segment names or documented segment sequences.
- [ ] 2.5 Implement include/exclude/project filters and max file byte-size skip gaps.
- [ ] 2.6 Implement deterministic `scanId` from fixed `swift-scan/v1` discriminator, repo identity, commit SHA, normalized options, and selected inventory signature.
- [ ] 2.7 Add tests for stable inventory ordering, stable scan IDs, skipped-file gaps, missing Git SHA failure, `scanId` invariance across different output paths and local absolute checkout paths, and repeatable flag ordering for `--project`, `--include`, and `--exclude`.

### Phase 3: Fact Model and Rule Catalog

- [ ] 3.1 Add scaffold rule IDs to `rules/rule-catalog.yml` before emitting them.
- [ ] 3.2 Add tests that emitted rule IDs are present in the rule catalog.
- [ ] 3.3 Implement the TraceMap fact envelope with deterministic fact IDs, sorted properties, commit SHA, file paths, line spans, rule IDs, evidence tiers, extractor ID, and extractor version.
- [ ] 3.4 Use `scan-manifest.json` with `startLine = 1` and `endLine = 1` for non-file-backed repo-level and toolchain gap facts.
- [ ] 3.5 Emit only scaffold-safe facts: file inventory, deterministic project/package metadata where implemented, and `AnalysisGap`.
- [ ] 3.6 Add tests proving raw source snippets and unsafe raw values are not stored by default in `facts.ndjson`, SQLite `properties_json`, `report.md`, or `logs/analyzer.log`.

### Phase 4: Manifest, NDJSON, SQLite, Report, and Log Writers

- [ ] 4.1 Write `scan-manifest.json` with required adapter-contract fields, coverage labels, build status, known gaps, scanner version, extractor versions, scan timestamp, repo identity, and scan-root metadata.
- [ ] 4.2 Include manifest `extractorVersions` as a deterministic extractor-ID-to-version object and include per-fact `extractorId`/`extractorVersion`.
- [ ] 4.3 Include required manifest timestamp fields such as `scannedAt`/`scanned_at`, while excluding them from `scanId`, fact ID inputs, and byte-stability assertions.
- [ ] 4.4 Write `facts.ndjson` as one stable JSON object per line.
- [ ] 4.5 Write `index.sqlite` using the shared schema conventions with at least `scan_manifest` and `facts`.
- [ ] 4.6 Write `report.md` with metadata, artifact list, coverage, fact counts, known gaps, and Swift limitations.
- [ ] 4.7 Write `logs/analyzer.log` with bounded diagnostics and no unsafe raw values.
- [ ] 4.8 Add deterministic output tests for manifest timestamp carve-outs, byte-identical `facts.ndjson`, stable fact IDs when only `--out` changes, SQLite facts, report ordering, analyzer log creation, `Level1SemanticAnalysisReduced` never pairing with `Succeeded`, safe placeholder command hints, required empty shared tables, and no unsafe local path/remote/value leakage in facts/report/log output.

### Phase 5: Reduced-Coverage and Toolchain Failure Behavior

- [ ] 5.1 Detect missing or unusable Swift toolchain as a reduced-coverage condition when useful file/project metadata can still be emitted.
- [ ] 5.2 Represent SwiftPM package load failure as `AnalysisGap` and reduced coverage.
- [ ] 5.3 Represent Xcode project/workspace load failure or unavailable Xcode as `AnalysisGap` and reduced coverage.
- [ ] 5.4 Represent unsupported syntax, unsupported encoding, oversized files, dynamic project metadata, and skipped files as gaps.
- [ ] 5.5 Add fixtures/tests for a static minimal SwiftPM-style repo that does not require `swift package resolve`, a broken/reduced path, and metadata-only or unsupported-project path.
- [ ] 5.6 Ensure no reduced scan reports absence of evidence as clean absence.
- [ ] 5.7 Add default-exclude tests for `.git`, `.build`, DerivedData, Pods/Carthage build outputs, dependency caches, hidden tool caches, and generated TraceMap outputs, using at least one fixture where an excluded `.build` or DerivedData directory contains a `.swift` file.
- [ ] 5.8 Add a report assertion that reduced-coverage fixture output renders the exact reduced coverage label.

### Phase 6: Downstream Compatibility Smoke

- [ ] 6.1 Verify existing `.NET` readers can open the Swift scaffold `index.sqlite`.
- [ ] 6.2 Run `tracemap export --format json` against minimal and reduced single Swift scaffold indexes and verify export artifacts are produced without raw snippets or unsafe values.
- [ ] 6.3 Run `tracemap combine` against minimal and reduced Swift scaffold indexes and verify source metadata, commit SHA, analysis level, build status, rule IDs, evidence tiers, source labels, and gaps survive.
- [ ] 6.4 Run `tracemap report` against the combined SQLite output, not the single-language scan index, and verify report artifacts are produced.
- [ ] 6.5 Decide whether downstream reader compatibility lives in Swift adapter tests, a .NET smoke script, or an integration test; record the decision in `implementation-state.md`.
- [ ] 6.6 Add tests or smoke scripts for reader compatibility without requiring full Swift analyzer depth.
- [ ] 6.7 Update docs only if implementation changes shared adapter contract, acceptance, validation, or rule catalog behavior.

### Phase 7: Validation and PR Readiness

- [ ] 7.1 Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] 7.2 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 7.3 Run the Swift adapter build command documented in `src/swift/README.md`.
- [ ] 7.4 Run the Swift adapter test command documented in `src/swift/README.md`.
- [ ] 7.5 Run a minimal Swift fixture scan.
- [ ] 7.6 Run a reduced-coverage Swift fixture scan.
- [ ] 7.7 Run `dotnet run --project src/dotnet/TraceMap.Cli -- export --index <swift-scan-output>/index.sqlite --out <tmp>/swift-export --format json`.
- [ ] 7.8 Run `dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan-output>/index.sqlite --label swift --out <tmp>/swift-combined.sqlite`.
- [ ] 7.9 Run `dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/swift-combined.sqlite --out <tmp>/swift-report`.
- [ ] 7.10 Run `./scripts/check-private-paths.sh`.
- [ ] 7.11 Run `git diff --check`.
- [ ] 7.12 Run or explicitly defer relevant `docs/VALIDATION.md` pinned smoke checks if shared reader/report/reducer behavior changed.

## Future Backlog After Issue #378

- [ ] Add SwiftSyntax declaration/call/object extraction.
- [ ] Add stable Swift symbol identity and relationship rows.
- [ ] Add deterministic SwiftPM metadata and optional semantic enrichment.
- [ ] Add optional SourceKit/sourcekit-lsp enrichment with toolchain diagnostics.
- [ ] Add Xcode project/workspace support with explicit reduced-coverage behavior.
- [ ] Add package/dependency surfaces for SwiftPM, CocoaPods, and Carthage beyond scaffold metadata.
- [ ] Add HTTP/client endpoint surfaces where statically visible.
- [ ] Add SwiftUI/UIKit UI surfaces where evidence-backed.
- [ ] Add CoreData, UserDefaults, Keychain, SQLite/GRDB, and Realm static storage/data surfaces where evidence-backed.
- [ ] Add validation docs for pinned public Swift smoke repos.
