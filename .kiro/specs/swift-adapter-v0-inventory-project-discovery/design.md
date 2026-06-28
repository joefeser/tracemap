# Swift Adapter v0 Inventory And Project Discovery Design

## Overview

This spec defines the first Swift adapter implementation slice: deterministic
repository inventory and project/package discovery from checked-in files. The
slice should establish Swift scan artifacts, coverage labels, public-safe
metadata handling, and reduced-coverage diagnostics without claiming compiler
semantic analysis or runtime behavior.

The intended evidence flow is:

```text
git repo + commit SHA
  -> included/excluded file inventory
  -> Swift source roots and metadata file discovery
  -> SwiftPM / Xcode / plist / ecosystem metadata parsing where deterministic
  -> toolchain availability diagnostics without builds/restores
  -> facts.ndjson + index.sqlite + scan-manifest.json + report.md + analyzer.log
```

## Goals

- Add a Swift adapter v0 entry point that satisfies the shared language adapter
  artifact contract.
- Inventory Swift files, source roots, test roots, generated roots, vendor
  roots, and metadata files deterministically.
- Discover `Package.swift`, `Package.resolved`, `*.xcodeproj`,
  `*.xcworkspace`, `Info.plist`, `Podfile`, `Podfile.lock`, `Cartfile`, and
  `Cartfile.resolved`.
- Parse safe, structural metadata when it is available without executing Swift,
  SwiftPM, CocoaPods, Carthage, Xcode, app code, simulators, or devices.
- Emit explicit gaps for dynamic manifests, unsupported metadata, missing
  tooling, malformed files, private/unsafe values, and partial project graphs.
- Keep package metadata limited to inventory needed by issue #382.
- Keep artifacts deterministic, rule-backed, coverage-labeled, and public-safe.

## Non-Goals

- No Swift compiler semantic model, SourceKit enrichment, or runtime proof.
- No `swift build`, `swift test`, `swift package resolve`, `xcodebuild`,
  CocoaPods install/update, Carthage bootstrap/update, simulator/device access,
  test execution, or app code execution.
- No package vulnerability, license, compatibility, freshness, or registry
  analysis.
- No dependency surface interpretation beyond inventory handoff fields for
  issue #382.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic ranking.
- No raw source snippets, manifest snippets, unsafe URLs, hostnames, local
  absolute paths, raw remotes, credentials, secrets, or private labels in
  default artifacts.

## Proposed Implementation Slices

## Adapter Host Decision

Swift v0 should be implemented as an independent Swift package scanner under
`src/swift`, following TraceMap's separate language-adapter direction. The
package executable should be named `tracemap-swift` and should expose a scan
command that writes the standard adapter artifacts:

```bash
swift run --package-path src/swift tracemap-swift scan --repo <repo> --out <out>
```

This spec does not require adding `--language swift` dispatch to the existing
.NET CLI. If a future architecture decision wants unified CLI dispatch, that
should be specified separately and must preserve independent adapter outputs and
validation.

### Slice 1: Adapter Scaffold And Manifest Contract

- Add Swift adapter CLI integration that can produce the required scan artifacts
  even when only file inventory is available.
- Require repo identity and commit SHA before successful output.
- Define deterministic scan ID inputs for Swift inventory: repository identity
  hash, commit SHA, normalized scan root, lexicographically sorted
  repo-relative paths of all included files, including Swift source files and
  supported metadata files, scan options, and extractor version. Excluded files
  and output directories are not scan ID inputs. Sorting must be
  ordinal/byte-stable and not locale-sensitive. Do not include timestamps,
  process IDs, output directories, or local absolute paths.
- Emit reduced coverage whenever the adapter cannot prove complete metadata
  inventory for the selected scope.

### Slice 2: File Inventory And Root Classification

- Walk the scan root with deterministic ordering.
- Include `.swift` source files and known metadata files.
- Classify conventional SwiftPM roots:
  - `Sources/**`
  - `Tests/**`
  - package-local plugin or macro roots only as inventory, not execution proof
- Label generated/vendor/build/cache roots:
  - `.build/**`
  - `DerivedData/**`
  - `Pods/**`
  - `Carthage/Build/**`
  - `SourcePackages/**`
  - `vendor/**`
  - `Generated/**`
  - `*.generated.swift` suffixes after normalized path-segment checks
- Match directory roots by normalized repo-relative path segments, not by
  substring containment. For example, `Pods` should match a path segment named
  `Pods`, not a source folder whose name merely contains those characters.
- Keep lockfiles and package metadata parseable even if they live near vendor
  roots.
- Persist skipped counts and exclusion reasons with rule-backed diagnostics.

### Slice 3: SwiftPM Metadata Inventory

- Parse `Package.swift` without executing it. In v0, manifest parsing uses a
  token/line scanner only, without SwiftSyntax. Extracted manifest values are
  Tier3 syntax/textual evidence unless a future implementation introduces a
  stronger deterministic parser with cataloged limitations. Any extraction that
  would require SwiftSyntax tree parsing is deferred and emitted as a gap.
- Record dynamic or unsupported manifest constructs as gaps rather than
  invented package metadata.
- Parse `Package.resolved` as data. Support known JSON/plist lockfile shapes
  with schema-version diagnostics.
- Store safe package identities and hashes. Hash or omit unsafe locations,
  URLs, hostnames, remotes, and local paths.
- Expose enough stable metadata for issue #382 to map dependency surfaces later:
  ecosystem family, metadata file fact ID, safe identity or identity hash,
  lockfile state count, declared target/product labels where safe, and parser
  support state.

### Slice 4: Xcode Project And Workspace Inventory

- Detect `*.xcodeproj` and `*.xcworkspace` bundles by path.
- Parse `*.xcworkspace/contents.xcworkspacedata` for referenced project paths
  using a secure XML parser.
- Treat `*.xcodeproj/project.pbxproj` as bundle metadata first. The v0 default
  is to inventory the project and emit a parse-gap fact unless a documented
  narrow deterministic reader is implemented. Acceptable v0 readers are limited
  to allowlisted line/key extraction or a named dependency with deterministic
  cross-platform behavior and tests; unbounded object-graph parsing is out of
  scope.
- Record public-safe target names, product type labels, build configuration
  labels, source group references, package references, and Info.plist
  references only when visible in checked-in metadata.
- Treat absolute local paths, missing referenced files, unsupported objects, and
  external references as gaps or hashed metadata.
- Do not claim active schemes, build success, selected destinations, runtime
  app entry points, or target reachability.

### Slice 5: Info.plist And Ecosystem Metadata

- Parse XML plist files where deterministic parser support exists. Binary plist
  parsing is deferred by default and should emit a gap/reduced-coverage fact
  unless the implementation adds a pure deterministic parser with macOS and
  non-macOS tests before enabling parsed binary plist facts.
- Allowlist safe plist fields or hash sensitive values. Candidate fields include
  bundle identifier hash, executable/display-name status, platform family,
  permission-key presence, URL-scheme count/hash, ATS-key presence, and
  entitlement-reference path hash.
- Inventory `Podfile`, `Podfile.lock`, `Cartfile`, and `Cartfile.resolved`
  without running tools.
- For CocoaPods/Carthage files, emit ecosystem metadata presence, parser support
  state, safe dependency identity counts, and reduced-coverage gaps. Keep
  interpretation of dependency surfaces for issue #382.

### Slice 6: Toolchain Diagnostics

- Probe tool availability with bounded, non-mutating checks such as version
  commands when available and safe. Probes must use a bounded timeout such as
  five seconds, run with stdin closed or redirected from `/dev/null`, and treat
  timeout, prompt-like behavior, or non-zero exit without safe output as
  toolchain-unavailable diagnostics.
- Do not run any command that mutates the working tree, resolves packages,
  creates build outputs, opens Xcode projects, contacts devices, or executes app
  code.
- Persist only safe labels and version strings. Hash tool paths if recorded.
- Use unavailable or intentionally-not-run diagnostics to explain reduced
  coverage.

### Slice 7: SQLite, Report, Tests, And Docs

- Store Swift inventory facts in the existing facts tables first. Add precise
  tables only if facts/properties cannot support stable downstream queries.
- Add report sections for inventory, project metadata, package metadata,
  excluded roots, toolchain diagnostics, gaps, and coverage.
- Add fixtures for useful inventory and reduced/unsupported metadata.
- Update `docs/VALIDATION.md`, `docs/LANGUAGE_ADAPTER_CONTRACT.md`, and public
  docs only if implementation behavior changes shared adapter expectations.

## Candidate Rule IDs

These rule IDs are design candidates. Implementation must add rule-catalog
entries with limitations before emitting them.

| Rule ID | Purpose | Default tier |
| --- | --- | --- |
| `swift.inventory.source-file.v1` | Swift source file inventory and root classification. | Tier2Structural |
| `swift.inventory.exclusion.v1` | Generated/vendor/build/cache exclusion or labeling. | Tier2Structural |
| `swift.swiftpm.manifest.v1` | Checked-in `Package.swift` manifest inventory. Manifest file presence, path, and hash are Tier2Structural; token/line-scanned package, target, product, and dependency labels are Tier3SyntaxOrTextual. |
| `swift.swiftpm.resolved.v1` | Checked-in `Package.resolved` lockfile inventory. | Tier2Structural |
| `swift.xcode.project.v1` | Checked-in `.xcodeproj` and `project.pbxproj` metadata inventory. | Tier2Structural |
| `swift.xcode.workspace.v1` | Checked-in `.xcworkspace` metadata inventory. | Tier2Structural |
| `swift.plist.info.v1` | Checked-in `Info.plist` safe metadata inventory. | Tier2Structural |
| `swift.ecosystem.metadata.v1` | CocoaPods/Carthage metadata file inventory boundary. | Tier2Structural |
| `swift.toolchain.diagnostic.v1` | Swift/Xcode/package-manager availability and non-run diagnostics. | Tier4Unknown |
| `swift.analysis-gap.v1` | Unsupported, malformed, missing, dynamic, or partial Swift metadata. | Tier4Unknown |

## Fact And Metadata Direction

Prefer shared fact types where they already fit. Swift-specific fact types may
be introduced when they improve clarity and have cataloged limitations.

Candidate Swift-specific facts:

| Fact type | Purpose | Safe metadata examples |
| --- | --- | --- |
| `SwiftSourceFileDeclared` | Included Swift source file inventory. | `rootKind`, `fileKind`, `lineCount`, `pathHash` |
| `SwiftSourceRootDeclared` | Source/test/generated/vendor root classification. | `rootKind`, `classificationEvidence`, `fileCount` |
| `SwiftPackageManifestDeclared` | `Package.swift` inventory. | `manifestHash`, `parserMode`, `targetCount`, `productCount`, `dependencyDeclarationCount` |
| `SwiftPackageResolvedDeclared` | `Package.resolved` inventory. | `schemaVersion`, `stateCount`, `safeIdentityCount`, `identityHashSample`, `unsafeLocationCount` |
| `SwiftXcodeProjectDeclared` | Xcode project bundle inventory. | `targetCount`, `configurationCount`, `productTypeLabels`, `parseStatus` |
| `SwiftXcodeWorkspaceDeclared` | Xcode workspace inventory. | `referencedProjectCount`, `externalReferenceCount`, `parseStatus` |
| `SwiftInfoPlistDeclared` | Info.plist inventory. | `bundleIdentifierHash`, `platformLabels`, `permissionKeyCount`, `urlSchemeCount` |
| `SwiftEcosystemMetadataDeclared` | Podfile/Cartfile metadata boundary. | `ecosystem`, `metadataKind`, `dependencyIdentityCount`, `parseStatus` |
| `AnalysisGap` | Reduced or unsupported Swift inventory. | `gapKind`, `gapReason`, `metadataKind`, `toolLabel`, `parserStatus` |

All string properties must be sorted deterministically. Stable keys must avoid
local absolute paths, raw remotes, timestamps, and output directories.

## Coverage Model

Suggested coverage labels:

| Label | Meaning |
| --- | --- |
| `SwiftInventoryFileBasedSucceeded` | File-based inventory succeeded for the selected scope and all supported metadata files were handled according to their v0 policy. This does not mean Xcode graph completeness, semantic proof, build proof, or runtime proof. |
| `SwiftInventoryReduced` | Useful inventory exists, but at least one supported metadata file, project graph, lockfile, plist, exclusion, or toolchain diagnostic is partial or unavailable. |
| `SwiftInventoryUnsupported` | Swift files or metadata were found, but the adapter could not parse enough supported inventory to produce normal facts beyond gaps. |
| `SwiftInventoryNotDetected` | No Swift files or supported Swift metadata were found in the selected scope. |

These are Swift-specific inventory coverage labels, not semantic coverage
claims. Every Swift inventory artifact should also persist a companion
`coverageCeiling` property with value `syntax-or-structural` for this v0 slice.
Because this v0 slice has no Swift compiler semantic model,
`SwiftInventoryFileBasedSucceeded` still maps to a non-semantic or reduced
semantic manifest state such as `Level3SyntaxAnalysis` or
`Level1SemanticAnalysisReduced` with `FailedOrPartial`; it must never map to
`Level1SemanticAnalysis` plus `Succeeded`. The adapter should default to
reduced coverage whenever Xcode or SwiftPM metadata is partial, malformed,
unsupported, unavailable, or intentionally not enriched by non-running
toolchain policy.

## Package Boundary With Issue #382

This issue may inventory metadata needed by later package-surface work:

- metadata ecosystem family: SwiftPM, CocoaPods, Carthage
- metadata file fact IDs
- safe dependency identities or identity hashes
- lockfile schema/support state
- safe counts and hashes
- file spans and commit provenance

This issue must not:

- determine whether a dependency is used at runtime
- classify dependency compatibility, licenses, vulnerabilities, freshness, or
  risk
- contact package registries or external repositories
- execute package-manager restore, install, update, resolve, or build commands
- emit reducer conclusions about changed packages

## Safety And Public Artifact Rules

- Store repo-relative paths only. Hash scan-root and git-root information per
  the language adapter contract.
- Do not capture output paths in artifacts. This includes `/tmp` paths, macOS
  `/private/tmp` realpaths, and any resolved output directory path.
- Do not persist raw Swift source snippets, manifest snippets, raw plist values,
  raw URLs, hostnames, raw remotes, local absolute paths, credentials, secrets,
  tokens, or private labels.
- Use full lowercase SHA-256 hashes in persisted properties when hashing unsafe
  values. Reports may show short prefixes only as display text.
- Treat package identities, target labels, and bundle labels as allowlisted only
  after applying the safe-value policy.
- For SwiftPM, CocoaPods, and Carthage dependency identity strings, hash by
  default because simple private names can look syntactically safe. Persist raw
  identities only when a rule explicitly proves the value is public-safe and not
  private, path-like, host-like, URL-like, whitespace-bearing, or secret-like.
- Multi-value properties must use deterministic scalar fields rather than
  unbounded inline collections. For lockfile identities, prefer counts plus a
  bounded sorted hash sample over a variable-length array.
- Analyzer logs must be useful but public-safe: no raw environment dumps, local
  paths, command output with private paths, or raw remotes.

## Validation Plan

Spec-only validation for this PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-inventory-project-discovery --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-inventory-project-discovery --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Future implementation validation before opening or updating an implementation
PR:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
swift test --package-path src/swift
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift
test -f /tmp/tracemap-swift-package-basic/scan-manifest.json
test -f /tmp/tracemap-swift-package-basic/facts.ndjson
test -f /tmp/tracemap-swift-package-basic/index.sqlite
test -f /tmp/tracemap-swift-package-basic/report.md
test -f /tmp/tracemap-swift-package-basic/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-reduced/facts.ndjson
test -f /tmp/tracemap-swift-metadata-reduced/index.sqlite
test -f /tmp/tracemap-swift-metadata-reduced/report.md
test -f /tmp/tracemap-swift-metadata-reduced/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-unsupported/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-unsupported/facts.ndjson
test -f /tmp/tracemap-swift-metadata-unsupported/index.sqlite
test -f /tmp/tracemap-swift-metadata-unsupported/report.md
test -f /tmp/tracemap-swift-metadata-unsupported/logs/analyzer.log
test -f /tmp/tracemap-no-swift/scan-manifest.json
test -f /tmp/tracemap-no-swift/facts.ndjson
test -f /tmp/tracemap-no-swift/index.sqlite
test -f /tmp/tracemap-no-swift/report.md
test -f /tmp/tracemap-no-swift/logs/analyzer.log
./scripts/check-private-paths.sh
git diff --check
```

If the implementation chooses different fixture names, update this spec before
implementation review so the commands remain exact.
