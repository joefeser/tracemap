# Swift Adapter v0 Scaffold CLI and Output Contract Design

Issue: [#378](https://github.com/joefeser/tracemap/issues/378)

## Overview

This spec defines the first Swift adapter implementation slice: scaffold a future Swift adapter lane and make its command/output contract compatible with existing TraceMap artifacts. The slice is intentionally small. It should prove that Swift scans can produce deterministic, evidence-backed outputs with honest reduced coverage before any broad Swift analysis is attempted.

The future implementation should create a sibling adapter under `src/swift` unless implementation discovery records a better reason. The adapter should write:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

The scaffold can emit inventory/project metadata facts and explicit `AnalysisGap` facts. It must not claim full Swift semantic analysis, runtime reachability, app behavior, UI navigation, storage behavior, or impact.

## Non-Goals

- No Swift analyzer depth beyond scaffold-level inventory, output writing, and reduced-coverage/gap behavior.
- No SwiftSyntax, SourceKit, SwiftPM semantic, or Xcode semantic implementation in this issue unless only used to prove toolchain-unavailable reduced behavior.
- No simulator, device, app launch, test execution, dependency restore, package resolution, or network access during scan.
- No Objective-C runtime, selector, responder-chain, property-wrapper, macro, result-builder, dependency-injection, protocol-dispatch, Combine, async scheduling, storyboard/xib runtime, or SwiftUI navigation inference.
- No raw source snippets by default.
- No LLM calls, embeddings, vector stores, or prompt-based classification.
- No public claim that Swift support is complete, runtime-proven, production-safe, or AI impact analysis.

## Proposed Adapter Shape

Preferred repository layout:

```text
src/swift/
  README.md
  <package/build files>
  <adapter source>
  tests/
```

The implementation may choose Swift-native packaging, Node, .NET, or another existing repo-friendly runtime. The decision should be made in the implementation PR and recorded in the Swift adapter README and implementation-state notes. The spec favors:

- deterministic local build/test commands;
- minimal dependency footprint;
- easy CI execution;
- direct access to stable file/project metadata;
- later compatibility with SwiftSyntax and optional SourceKit/SwiftPM/Xcode enrichment.

The first command should be documented as:

```bash
tracemap-swift scan --repo <path> --out <path>
```

If the actual executable name differs, implementation must keep the docs and tests exact.

Required command options:

- `--repo <path>`: Git-backed repository root or selected scan root.
- `--out <path>`: output directory to rebuild.
- `--project <path>` repeatable: selected SwiftPM/Xcode/package sub-scope.
- `--include <glob>` repeatable.
- `--exclude <glob>` repeatable.
- `--max-file-byte-size <value>` with fixed default `1048576` bytes.
- `--version`.
- `--help` and `scan --help`.

## Pipeline

1. Validate CLI arguments.
2. Resolve Git repository metadata and concrete commit SHA.
3. Build a deterministic selected file/project inventory.
4. Apply default excludes and user include/exclude/project filters.
5. Detect available Swift-related toolchain/project signals without requiring a successful build.
6. Inventory supported Swift/project metadata files.
7. Emit `AnalysisGap` facts for unavailable toolchain, failed project load, unsupported metadata, and each skipped file based on the signals detected above.
8. Emit scaffold inventory and project metadata facts only where rule-backed.
9. Compute coverage from evidence and gaps.
10. Rebuild the requested output path.
11. Write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
12. Validate local output determinism and downstream reader compatibility in tests.

## Default File Scope

The scaffold should select:

- `*.swift`
- `Package.swift`
- `Package.resolved`
- `.xcodeproj` and `.xcworkspace` bundle presence plus deterministic contained metadata files such as `project.pbxproj` and `contents.xcworkspacedata`
- `Podfile`, `Podfile.lock`
- `Cartfile`, `Cartfile.resolved`
- `Info.plist`
- `.entitlements`
- `.xcdatamodel` and `.xcdatamodeld` inventory markers
- `.storyboard` and `.xib` inventory markers
- privacy manifests and selected safe app metadata files

Default excludes should include:

- `.git`
- TraceMap output directories
- `.build`
- `DerivedData`
- Xcode build products
- dependency cache/checkouts
- Carthage build/checkouts
- Pods build outputs where generated/dependency content would dominate
- hidden tool caches
- files over `--max-file-byte-size`

Project metadata that requires executing code, running package resolution, invoking build scripts, reading environment-dependent settings, or accessing the network is a dynamic boundary.

## Manifest Contract

The Swift manifest must follow `docs/LANGUAGE_ADAPTER_CONTRACT.md` and include:

- deterministic `scanId`
- repo identity
- remote URL when available
- branch when available
- commit SHA
- scanner version
- extractor versions
- scan timestamp required by shared manifest and SQLite compatibility
- analysis level
- build status
- selected project/workspace identifiers
- known gaps
- scan root metadata: `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash` when applicable

Manifest JSON should include extractor versions as a deterministic object keyed by extractor ID, for example `extractorVersions: { "swift.scaffold": "0.1.0" }`. Each emitted fact must also carry its own `extractorId` and `extractorVersion` so downstream readers preserve fact-level provenance even when manifest summaries evolve.

`scanId` inputs:

```text
swift-scan/v1
repo identity string
commit SHA
normalized scan options
sorted selected inventory signature
```

The `swift-scan/v1` prefix is the fixed adapter discriminator. It must be a hardcoded string constant, not derived from a scanner version, package version, docs version, rule-catalog version, or adapter-contract version variable. `scanId` must not include output path, current time, process ID, random values, local absolute paths, or a separately versioned adapter-contract input that could churn IDs when docs change.

Manifest timestamps such as JSON `scannedAt` and SQLite `scanned_at` are required compatibility fields, but they are the documented non-stable local scan fields. They must be excluded from `scanId`, fact ID inputs, `facts.ndjson`, and deterministic byte-stability comparisons. Tests should verify repeated identical scans differ only in the timestamp fields where the manifest schema requires them.

Coverage is conservative:

- `Level1SemanticAnalysis` is reserved for a future slice with proven deterministic semantic coverage for the selected scope and no known gaps.
- `Level1SemanticAnalysisReduced` is the normal useful Swift static evidence path.
- `Level3SyntaxAnalysis` covers syntax/textual/project metadata fallback when no semantic proof exists.
- `FailedOrPartial` means selected scope did not reach full semantic coverage.
- Missing Swift toolchain, failed SwiftPM load, failed Xcode load, skipped files, parser errors, unsupported encodings, dynamic metadata, and unavailable optional extractors reduce coverage.

`Level1SemanticAnalysis` / `Succeeded` is reserved for future Swift slices with proven deterministic semantic coverage for the full selected scope and no known gaps. The issue #378 scaffold can only produce `Level1SemanticAnalysisReduced` or lower.

## Fact Contract

Every fact must include:

- deterministic `factId`
- `scanId`
- repo and commit SHA
- `factType`
- `ruleId`
- `evidenceTier`
- source/target/role symbols when available
- `contractElement` when applicable
- relative file path and line span when file-backed
- extractor ID
- extractor version
- sorted safe properties

Non-file-backed facts, including repo-level gaps and toolchain-unavailable gaps, should use `scan-manifest.json` as the file path with `startLine = 1` and `endLine = 1`. This convention follows existing adapter precedent and is intentional; `scan-manifest.json` is the generated output artifact name used for provenance, not a repo source file. Toolchain gap facts should use the scaffold extractor ID `swift.scaffold` and its version constant even when no optional extractor successfully ran.

Bundle inventory facts for `.xcodeproj` and `.xcworkspace` should use the bundle directory path with `startLine = 1` and `endLine = 1`; facts derived from contained files should use the contained file path and actual line span where available.

Initial scaffold fact types should be limited to:

- `FileInventoried`
- package/project metadata facts that already have shared precedent, or clearly documented Swift-specific facts
- `AnalysisGap`

Reducer-facing facts should reuse existing shared fact types and property keys where possible. Swift-specific facts are allowed only when rule-catalog entries and report/reader expectations are documented before emission.

## Initial Rule ID Direction

The exact rule IDs belong in `rules/rule-catalog.yml` during implementation. Suggested v0 scaffold IDs:

| Rule ID | Fact type | Tier | Purpose | Required limitations |
| --- | --- | --- | --- | --- |
| `swift.repo.manifest.v1` | `FileInventoried` or metadata fact | Tier2Structural | Swift repo/project metadata inventory | Static inventory only; no build, package resolution, runtime app, deployment, or reachability proof. |
| `swift.file.inventory.v1` | `FileInventoried` | Tier3SyntaxOrTextual | Selected Swift file inventory | File presence only; Tier3 because a standalone `.swift` file does not prove project membership, compilation, or semantic symbol availability. |
| `swift.package.swiftpm.v1` | package/dependency metadata | Tier2Structural | SwiftPM file/dependency metadata when parseable without resolving packages | Literal metadata only; no package resolution, network access, build success, transitive dependency completeness, or platform compatibility proof. |
| `swift.package.cocoapods.v1` | package/dependency metadata | Tier2Structural | CocoaPods literal lockfile metadata | Literal metadata only; no pod install, build integration, transitive completeness beyond checked-in metadata, or runtime use proof. |
| `swift.package.carthage.v1` | package/dependency metadata | Tier2Structural | Carthage literal metadata | Literal metadata only; no checkout/build proof, binary compatibility proof, or runtime use proof. |
| `swift.project.xcode.v1` | project metadata fact | Tier2Structural | Deterministic Xcode project/workspace metadata inventory | Static project metadata only; no Xcode build success, target selection, scheme behavior, build setting evaluation, signing, simulator/device, or deployment proof. |
| `swift.toolchain.unavailable.v1` | `AnalysisGap` | Tier4Unknown | Swift toolchain unavailable or unusable | Explicit analysis gap; absence of toolchain evidence is not absence of Swift behavior. |
| `swift.project.load-failed.v1` | `AnalysisGap` | Tier4Unknown | SwiftPM/Xcode project load failed or skipped | Explicit reduced-coverage gap; does not prove project invalidity or app behavior absence. |
| `swift.unsupported.dynamic-boundary.v1` | `AnalysisGap` | Tier4Unknown | Runtime/dynamic Swift behavior boundary | Unsupported dynamic boundary only; no protocol dispatch, Objective-C selector, UI navigation, storage, network, app lifecycle, or runtime conclusion. |

Limitations must say these facts are static evidence only and do not prove builds, runtime execution, deployment, simulator/device state, UI navigation, storage access, or network reachability.

## SQLite Contract

`index.sqlite` should use the shared SQLite schema used by existing adapters. Minimum tables:

- `scan_manifest`
- `facts`

When Swift facts include symbols or relationships in future slices, the adapter should populate shared tables:

- `symbols`
- `symbol_occurrences`
- `fact_symbols`
- `symbol_relationships`
- `call_edges`
- `object_creations`
- `argument_flows`
- `local_aliases`
- `field_aliases`
- `parameter_forward_edges`
- relationship tables supported by the shared schema

The implementation should create the shared DDL in full, including empty supported relationship/flow tables, unless the shared schema explicitly allows an omission. No Swift-only schema fork should be introduced. Additive schema changes require shared docs/tests.

## Report Contract

`report.md` should include:

- scan metadata: repo label, commit SHA, branch, analysis level, build status, scanner/extractor versions;
- artifact list;
- selected scope summary;
- fact counts by type, rule, and evidence tier;
- coverage warnings;
- known gaps;
- Swift limitations;
- downstream command hints using existing `.NET` readers.

The report must not render raw source snippets, raw SQL, raw URLs, config values, secrets, local absolute paths, raw remotes, provisioning/profile details, or private labels. Reduced scans must say absence of evidence is coverage-relative.

Downstream command hints must use placeholders such as `<swift-scan-output>` and `<tmp>` rather than the literal `--out` value passed at scan time or any local absolute path.

## Reduced-Coverage Behavior

The scaffold should prefer useful partial output when a concrete commit SHA and safe static evidence exist. Examples:

- Swift toolchain missing: inventory supported files/project metadata, emit `AnalysisGap`, use reduced/syntax coverage.
- SwiftPM project load fails: keep file/project metadata and syntax fallback where available, emit `AnalysisGap`, mark reduced.
- Xcode project load fails or Xcode unavailable: keep SwiftPM/file/config fallback where available, emit `AnalysisGap`, mark reduced.
- Parser error or unsupported Swift syntax: emit gap for affected files and continue over other files.
- No supported inputs: write an explicit empty/reduced report only if commit SHA is known and the absence of supported input is itself documented.

The scanner should fail before success artifacts when:

- repo path is invalid;
- commit SHA cannot be resolved;
- output path cannot be safely prepared;
- no credible evidence source is available and emitting a reduced report would be misleading.

## Safe Boundaries

Swift v0 scaffold and later Swift extractors must treat these as gaps or limitations unless a future rule proves them:

- protocol and dynamic dispatch targets;
- Objective-C selectors and bridging;
- responder chain behavior;
- SwiftUI navigation and view identity at runtime;
- UIKit storyboard/xib wiring at runtime;
- macros and result builders beyond static syntax;
- property wrapper side effects;
- dependency injection containers;
- async scheduling and Combine pipeline runtime behavior;
- notification delivery;
- Keychain/UserDefaults/CoreData/SQLite/Realm runtime reads/writes;
- build configuration, entitlements, permissions, and environment values;
- branch feasibility, feature flags, auth, deployment, and production usage.

## Implementation Validation

Exact commands for this spec-only PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-scaffold-cli-output-contract --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-scaffold-cli-output-contract --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Exact commands expected for the future implementation PR:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
<swift-adapter-build-command>
<swift-adapter-test-command>
<swift-scan-minimal-fixture-command>
<swift-scan-reduced-fixture-command>
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <swift-scan-output>/index.sqlite --out <tmp>/swift-report
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <swift-scan-output>/index.sqlite --out <tmp>/swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan-output>/index.sqlite --label swift --out <tmp>/swift-combined.sqlite
./scripts/check-private-paths.sh
git diff --check
```

If the implementation changes shared reader/report/reducer behavior, follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks.
