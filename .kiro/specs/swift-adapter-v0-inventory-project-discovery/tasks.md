# Swift Adapter v0 Inventory And Project Discovery Tasks

## Implementation Plan

### Phase 1: Rule Catalog And Fixture Contract

- [ ] 1.1 Add rule-catalog entries and documented limitations before product
  code emits any Swift inventory, project, package, plist, ecosystem metadata,
  toolchain diagnostic, or analysis-gap rule ID.
- [ ] 1.2 Add minimal Swift fixture repositories for useful inventory and
  reduced/unsupported metadata coverage.
- [ ] 1.3 Define deterministic fixture commits or fixture SHA strategy so scan
  artifacts always include repo and commit SHA.
- [ ] 1.4 Add expected-output tests for rule IDs, evidence tiers, file spans,
  extractor IDs, extractor versions, commit SHA, and public-safe properties.
- [ ] 1.5 Add byte-stability tests for `facts.ndjson`, report ordering, and
  stable SQLite rows for identical Swift inventory inputs.
- [ ] 1.6 Add end-to-end assertions for every Swift inventory coverage label:
  file-based succeeded inventory, reduced inventory, unsupported inventory, and
  no Swift detected.
- [ ] 1.7 Add traversal-order tests proving scan ID and fact ordering are
  stable across unordered filesystem enumeration.
- [ ] 1.8 Add a direct scan-ID test that computes the scan ID from two
  differently ordered file lists for the same repo/commit/options and asserts
  the IDs are identical.
- [ ] 1.9 Add a scan-ID test proving adding or removing files under excluded
  roots such as `.build/**` does not change the scan ID.

### Phase 2: Adapter Scaffold And Required Artifacts

- [ ] 2.1 Add an independent Swift package scanner under `src/swift` with a
  `tracemap-swift` executable and artifact writer integration.
- [ ] 2.2 Require repo identity and commit SHA before successful Swift scan
  output.
- [ ] 2.3 Define Swift deterministic scan ID inputs without timestamps, process
  IDs, output directories, or local absolute paths.
- [ ] 2.4 Write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log` for inventory-only scans.
- [ ] 2.5 Mark coverage as reduced whenever metadata parsing, tool availability,
  unsupported files, or project references are partial.

### Phase 3: File Inventory And Source Root Classification

- [ ] 3.1 Implement deterministic walking for `.swift` files and supported
  Swift metadata files.
- [ ] 3.2 Classify conventional SwiftPM source and test roots such as
  `Sources/**` and `Tests/**`.
- [ ] 3.3 Label or exclude generated, vendor, build, dependency, and cache
  roots including `.build/**`, `DerivedData/**`, `Pods/**`,
  `Carthage/Build/**`, `SourcePackages/**`, `vendor/**`, `Generated/**`, and
  `*.generated.swift`.
- [ ] 3.4 Keep in-scope metadata files parseable even when they are near
  excluded dependency roots.
- [ ] 3.5 Add tests for include/exclude ordering, skipped counts, generated
  source labeling, vendor roots, and reduced coverage from skipped or
  ambiguous roots.

### Phase 4: SwiftPM Metadata Inventory

- [ ] 4.1 Inventory `Package.swift` with path, line span, manifest hash, parser
  mode, rule ID, evidence tier, extractor version, and limitations.
- [ ] 4.2 Extract only statically visible package name, target labels, product
  labels, and dependency declaration labels when parsing can do so without
  executing Swift.
- [ ] 4.3 Emit gaps for dynamic manifests, unsupported syntax, computed values,
  plugins, conditionals, malformed manifests, or unsafe values.
- [ ] 4.4 Parse supported `Package.resolved` schemas as data and record safe
  identity/hash metadata and lockfile state counts.
- [ ] 4.5 Add tests proving raw URLs, hostnames, remotes, local absolute paths,
  credentials, and source snippets are omitted or hashed.
- [ ] 4.6 Add fixtures for `Package.resolved` schema version 1 and schema
  version 2 and assert the emitted schema version for each.
- [ ] 4.7 Add a fixture for an unknown future `Package.resolved` schema version
  and assert it emits schema-version gap evidence instead of crashing or
  silently parsing.

### Phase 5: Xcode Project And Workspace Inventory

- [ ] 5.1 Detect `*.xcodeproj` and inventory `project.pbxproj` support state.
- [ ] 5.2 Keep `project.pbxproj` parsing as a parse-gap by default in v0.
  Parse safe target labels, product type labels, build configuration labels,
  source references, package references, or Info.plist references only if a
  named dependency or allowlisted line/key extraction strategy is documented in
  the rule catalog first.
- [ ] 5.3 Detect `*.xcworkspace` and parse
  `contents.xcworkspacedata` with a secure XML parser for safe referenced
  project paths.
- [ ] 5.4 Emit gaps for malformed project/workspace files, unsupported object
  graphs, absolute local paths, external references, missing references, and
  partial metadata.
- [ ] 5.5 Add tests proving the report does not claim build success, active
  schemes, selected destinations, runtime entry points, or app target
  reachability.
- [ ] 5.6 Add a `contents.xcworkspacedata` fixture with an external URL
  reference and assert it emits a gap instead of a project-path fact.
- [ ] 5.7 Add a `contents.xcworkspacedata` fixture with a missing local
  `.xcodeproj` reference and assert it emits a gap instead of a project-path
  fact.

### Phase 6: Info.plist And Ecosystem Metadata Inventory

- [ ] 6.1 Inventory and parse supported `Info.plist` files from included roots
  and Xcode references.
- [ ] 6.2 Persist only allowlisted or hashed plist metadata such as platform
  labels, permission-key counts, bundle identifier hash, URL scheme counts, and
  entitlement-reference path hash.
- [ ] 6.3 Inventory `Podfile`, `Podfile.lock`, `Cartfile`, and
  `Cartfile.resolved` without running CocoaPods or Carthage.
- [ ] 6.4 Persist only inventory handoff metadata needed by issue #382:
  ecosystem family, metadata file fact IDs, safe identity/hash values, support
  state, counts, file spans, and commit provenance.
- [ ] 6.5 Add tests proving no dependency surface, compatibility, license,
  vulnerability, freshness, runtime-use, or reducer-impact claims appear in
  this slice.
- [ ] 6.6 Add tests for binary or unsupported `Info.plist` inputs proving gap
  evidence and reduced coverage instead of scan failure. These tests must pass
  on macOS and non-macOS hosts so binary plist handling does not depend on
  platform-local tools.
- [ ] 6.7 Add ecosystem metadata safety tests proving unsafe values in
  `Podfile.lock` and `Cartfile.resolved` are omitted or hashed.

### Phase 7: Toolchain Diagnostics

- [ ] 7.1 Add bounded non-mutating availability/version diagnostics for Swift,
  SwiftPM, Xcode, CocoaPods, and Carthage where available.
- [ ] 7.2 Ensure diagnostics never run builds, restores, package resolution,
  installs, updates, tests, simulators, devices, or app code.
- [ ] 7.3 Hash or omit local absolute tool paths, usernames, machine names, raw
  environment variables, and private paths.
- [ ] 7.4 Add tests for missing toolchain, partially available toolchain, and
  intentionally-not-run diagnostics with reduced coverage.
- [ ] 7.5 Add a test proving a hanging or prompt-like toolchain probe times out
  within the bounded limit and emits a public-safe unavailable diagnostic.

### Phase 8: SQLite, Reports, Docs, And Safety

- [ ] 8.1 Persist Swift facts in the existing fact/property schema first; add
  precise tables only when tests prove the existing schema is insufficient.
- [ ] 8.2 Add report sections for source inventory, project metadata, package
  metadata, plist metadata, ecosystem metadata, excluded roots, toolchain
  diagnostics, gaps, and coverage labels.
- [ ] 8.3 Update `docs/VALIDATION.md` with Swift fixture and smoke commands
  after implementation behavior exists.
- [ ] 8.4 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if Swift inventory
  requires shared adapter-contract changes.
- [ ] 8.5 Add safety tests proving generated artifacts contain no raw source
  snippets, manifest snippets, plist values, raw URLs, hostnames, local
  absolute paths, raw remotes, credentials, secrets, or private labels.
- [ ] 8.6 Add a log-safety test proving `logs/analyzer.log` contains no raw
  local absolute paths, usernames, machine names, raw environment values, raw
  tool paths, raw remotes, credentials, or secrets.

### Phase 9: Validation

- [ ] 9.1 Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] 9.2 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 9.3 Run `swift test --package-path src/swift`.
- [ ] 9.4 Run `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic`.
- [ ] 9.5 Run `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced`.
- [ ] 9.6 Run `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported`.
- [ ] 9.7 Run `swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift`.
- [ ] 9.8 Confirm all Swift sample scans write `scan-manifest.json`,
  `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
- [ ] 9.9 Run `./scripts/check-private-paths.sh`.
- [ ] 9.10 Run `git diff --check`.
- [ ] 9.11 Confirm generated outputs contain no raw source snippets, manifest
  snippets, plist values, raw URLs, hostnames, local absolute paths, raw
  remotes, credentials, secrets, or private labels.

## Deferred Follow-Ups

- SwiftSyntax declaration, call, object, or symbol extraction.
- SourceKit or sourcekit-lsp enrichment.
- Xcode build graph or scheme resolution beyond checked-in metadata.
- Package dependency surface interpretation in issue #382.
- HTTP, UI, storage, serializer, and runtime surface extraction.
- Reducer impact conclusions for Swift.
- Public site or marketing copy changes.
