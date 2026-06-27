# Swift Adapter v0 Inventory And Project Discovery Requirements

## Introduction

TraceMap needs a first Swift adapter slice that can inventory Swift repositories
and discover project/package metadata without executing builds, app code,
simulators, devices, package restores, or Xcode actions. This spec is for
GitHub issue #379 and is a child of the Swift v0 runway in issue #377.

This is a spec-only phase. It must not implement Swift analyzer or runtime code.
The implementation branch should be
`codex/implement-swift-inventory-project-discovery`.

## Issue Links

- Parent: https://github.com/joefeser/tracemap/issues/377
- This spec: https://github.com/joefeser/tracemap/issues/379
- Adjacent package-surface follow-up: https://github.com/joefeser/tracemap/issues/382

## Current State

- TraceMap has a shared language adapter contract in
  `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
- TraceMap validation guidance is in `docs/VALIDATION.md`.
- No Swift adapter spec folder exists before this issue.
- The Swift v0 parent issue requires deterministic static evidence,
  adapter-contract-compatible outputs, reduced-coverage labels, and no runtime
  proof claims.

## Scope Decisions

- The implementation SHALL discover Swift repository inventory and project
  metadata from checked-in files only.
- The implementation SHALL support `Package.swift`, `Package.resolved`,
  `*.xcodeproj`, `*.xcworkspace`, `Info.plist`, Swift source roots, Swift test
  roots, generated-source candidates, vendor/external dependency candidates,
  `Podfile`, `Podfile.lock`, `Cartfile`, and `Cartfile.resolved`.
- The implementation SHALL parse enough package/dependency inventory to hand
  stable metadata forward to issue #382, but SHALL NOT implement dependency
  surface interpretation, package compatibility analysis, vulnerability/license
  analysis, registry lookups, or restore/build execution in this slice.
- The implementation SHALL remain useful when Swift, SwiftPM, Xcode, CocoaPods,
  or Carthage tooling is unavailable.
- The implementation SHALL mark coverage as reduced when toolchain diagnostics,
  partial project metadata, unreadable files, malformed files, unsupported
  formats, or excluded roots prevent complete inventory.
- The implementation SHALL emit deterministic facts with rule IDs, evidence
  tiers, file paths, line spans, commit SHA, extractor identity, extractor
  version, and documented limitations.
- The implementation SHALL add an independent Swift scanner package under
  `src/swift` unless a later architecture spec explicitly changes adapter
  dispatch. The Swift package executable name SHOULD be `tracemap-swift`.
- The implementation SHALL write the normal scanner artifacts:
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`.
- The implementation SHALL not store raw source snippets by default. Raw Swift
  code, raw manifest snippets, unsafe URLs, hostnames, local absolute paths, raw
  remotes, credentials, secrets, and private labels must be omitted or hashed.

## Public Claim Level

Public copy MAY say Swift v0 inventories checked-in Swift project and package
metadata with deterministic static evidence, rule IDs, evidence tiers, commit
SHA provenance, extractor versions, coverage labels, and public-safe paths.

Public copy MUST NOT say TraceMap proves runtime behavior, build success,
dependency compatibility, package vulnerability, package license status,
simulator/device behavior, app target reachability, Xcode scheme behavior,
Swift compiler semantic resolution, or production impact for this slice.

## Requirements

### Requirement 1: Repository Identity And Commit Provenance

**User Story:** As a reviewer, I want every Swift inventory scan tied to a
specific repository and commit so findings are reproducible.

#### Acceptance Criteria

1. WHEN `tracemap-swift scan` or the equivalent independent Swift scanner entry
   point runs THEN the adapter SHALL require a repo identity and concrete commit
   SHA before writing successful scan artifacts.
2. WHEN the commit SHA cannot be determined THEN the adapter SHALL fail before
   writing a successful artifact set and SHALL explain the missing commit
   provenance in `logs/analyzer.log`.
3. WHEN Swift inventory facts are emitted THEN every fact SHALL include scan ID,
   repo identity, commit SHA, file path, line span where applicable, rule ID,
   evidence tier, extractor ID, and extractor version.
4. WHEN the scan root is below the git root THEN the manifest SHALL include
   public-safe scan-root metadata consistent with
   `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
5. WHEN repository remotes or local absolute paths are available THEN persisted
   artifacts SHALL use public-safe labels or hashes instead of raw private
   values. If the `remote URL when available` manifest field from
   `docs/LANGUAGE_ADAPTER_CONTRACT.md` would contain a private raw remote, the
   Swift adapter SHALL populate a hashed or redacted equivalent instead.

### Requirement 2: Swift Source Root And File Inventory

**User Story:** As a maintainer, I want TraceMap to identify Swift source
roots and excluded areas before deeper Swift analysis starts.

#### Acceptance Criteria

1. WHEN `.swift` files are present under included roots THEN the adapter SHALL
   inventory them with deterministic relative paths, line-count metadata when
   available, and a rule-backed source-file fact.
2. WHEN source files live under conventional SwiftPM roots such as
   `Sources/**` or `Tests/**` THEN the adapter SHALL classify the root as
   source or test evidence using Tier2 structural evidence.
3. WHEN source files live under Xcode-style or custom roots THEN the adapter
   SHALL inventory them as Swift files and mark the source-root classification
   as structural or unknown according to the evidence available.
4. WHEN files live under generated, vendor, build, dependency, or cache roots
   such as `.build/**`, `DerivedData/**`, `Pods/**`, `Carthage/Build/**`,
   `SourcePackages/**`, `vendor/**`, `Generated/**`, or `*.generated.swift`
   THEN the adapter SHALL exclude or separately label them using deterministic
   exclusion rules documented in the rule catalog. Path-root checks SHALL split
   repo-relative paths into normalized segments and match whole segments or
   documented suffixes, not substring containment.
5. WHEN an excluded file is relevant to project metadata, such as
   `Podfile.lock` or `Package.resolved`, THEN the adapter SHALL still inventory
   that metadata file if it is in scope and safe to parse.
6. WHEN include/exclude rules skip files THEN the manifest and report SHALL
   summarize skipped counts and coverage labels without listing private local
   absolute paths.

### Requirement 3: SwiftPM Package Discovery

**User Story:** As a Swift maintainer, I want TraceMap to detect SwiftPM
packages and lockfile metadata without running package resolution.

#### Acceptance Criteria

1. WHEN `Package.swift` exists THEN the adapter SHALL emit rule-backed package
   manifest evidence with package manifest path, line span, manifest hash,
   parser mode, rule ID, evidence tier, extractor version, and limitations.
   The implementation MAY use a Swift-specific fact type such as
   `SwiftPackageManifestDeclared` only after documenting it in the rule catalog
   and tests.
2. WHEN `Package.swift` can be parsed syntactically enough to identify package
   name, target declarations, product declarations, or dependency declaration
   names without executing Swift THEN the adapter MAY emit those as structural
   inventory properties or facts.
3. WHEN `Package.swift` uses dynamic Swift code, computed variables, conditionals,
   plugins, environment-dependent values, or unsupported syntax THEN the
   adapter SHALL emit `AnalysisGap` evidence rather than infer missing package
   metadata.
4. WHEN `Package.resolved` exists THEN the adapter SHALL parse supported
   lockfile versions deterministically and emit safe lockfile inventory such as
   package identity, version/revision hashes where needed, state count, and
   lockfile schema version.
5. WHEN lockfile values contain raw URLs or hostnames THEN persisted properties
   SHALL omit or hash unsafe values and MAY keep safe package identities when
   they pass the safe-value policy.
6. WHEN SwiftPM metadata is malformed, unsupported, absent, or partial THEN the
   adapter SHALL emit reduced coverage or gap facts with rule IDs and
   limitations.
7. WHEN this slice records dependency metadata THEN it SHALL stop at inventory
   and handoff fields needed by issue #382; it SHALL NOT classify dependency
   surfaces, compatibility, risk, licenses, or vulnerabilities.

### Requirement 4: Xcode Project And Workspace Discovery

**User Story:** As an iOS/macOS maintainer, I want TraceMap to discover Xcode
projects and workspaces without requiring Xcode to build or open them.

#### Acceptance Criteria

1. WHEN `*.xcodeproj` directories exist THEN the adapter SHALL inventory the
   project bundle path. It SHALL emit a parse-gap fact for `project.pbxproj` by
   default unless a specific deterministic narrow reader is implemented and
   documented in the rule catalog.
2. WHEN a documented narrow `project.pbxproj` reader succeeds THEN the adapter
   MAY emit structural facts for target names, product types, build
   configuration names, referenced source groups, package references, and
   Info.plist references.
3. WHEN `*.xcworkspace` directories exist THEN the adapter SHALL inventory the
   workspace bundle and parse safe workspace metadata such as referenced project
   paths from `contents.xcworkspacedata`.
4. WHEN Xcode project/workspace metadata is missing, malformed, has unsupported
   object graph shapes, contains absolute local paths, or references files
   outside the scan root THEN the adapter SHALL emit `AnalysisGap` facts and
   reduced coverage instead of inventing project structure.
5. WHEN Xcode command-line tooling is unavailable THEN the adapter SHALL still
   perform file-based project/workspace inventory and emit toolchain
   diagnostics explaining that Xcode enrichment was unavailable.
6. WHEN project schemes, build settings, or target membership cannot be proven
   from checked-in metadata THEN the report SHALL avoid claiming buildability,
   active schemes, runtime entry points, or app target reachability.

### Requirement 5: Info.plist And Bundle Metadata Discovery

**User Story:** As a reviewer, I want checked-in app bundle metadata surfaced
without leaking sensitive values.

#### Acceptance Criteria

1. WHEN `Info.plist` files are present under included roots or referenced by
   project metadata THEN the adapter SHALL inventory the plist path and parse
   safe structural keys where deterministic plist parsing succeeds. In v0, XML
   plist parsing is in scope; binary plist parsing SHALL emit a reduced-coverage
   gap unless the implementation adds a cross-platform deterministic parser and
   tests before emitting parsed binary plist facts.
2. WHEN bundle identifier, executable name, display name, URL schemes, app
   transport settings, permissions, entitlements references, or platform keys
   are visible THEN the adapter MAY emit safe hashed or allowlisted metadata
   according to the rule catalog.
3. WHEN plist values include raw URLs, hostnames, local paths, secrets,
   credentials, private labels, or environment-specific values THEN the adapter
   SHALL omit or hash those values.
4. WHEN plist parsing fails or the file is binary/unsupported THEN the adapter
   SHALL emit a gap fact and keep the scan partial rather than failing the
   whole adapter.
5. WHEN plist metadata is emitted THEN the report SHALL call it checked-in
   metadata evidence, not runtime bundle behavior or deployed configuration.

### Requirement 6: Ecosystem Metadata Inventory Boundary

**User Story:** As a future package-surface implementer, I want enough
ecosystem metadata inventory to build on without duplicating issue #382.

#### Acceptance Criteria

1. WHEN `Podfile` or `Podfile.lock` exists THEN the adapter SHALL inventory the
   file path, parser support state, safe lockfile/package identity counts where
   available, and any reduced coverage gaps without running CocoaPods.
2. WHEN `Cartfile` or `Cartfile.resolved` exists THEN the adapter SHALL
   inventory the file path, parser support state, safe dependency identity
   counts where available, and any reduced coverage gaps without running
   Carthage.
3. WHEN ecosystem metadata contains external repository URLs, hostnames, local
   paths, tokens, or private names THEN persisted output SHALL omit or hash the
   unsafe value.
4. WHEN package metadata is inventory-only THEN fact names, report copy, and
   docs SHALL avoid dependency-surface, compatibility, license, vulnerability,
   or runtime-use claims reserved for issue #382.
5. WHEN unsupported package manager metadata is present THEN the adapter SHALL
   emit gap evidence and keep coverage labels honest.

### Requirement 7: Toolchain Diagnostics And Reduced Coverage

**User Story:** As a user scanning mixed Swift repos, I want TraceMap to
continue producing useful inventory when local Swift tooling is missing.

#### Acceptance Criteria

1. WHEN `swift`, `swift package`, `xcodebuild`, CocoaPods, or Carthage commands
   are unavailable THEN the adapter SHALL record toolchain diagnostics and
   continue file-based inventory.
2. WHEN toolchain probes are attempted THEN each probe SHALL use a bounded
   timeout no greater than five seconds and SHALL close or redirect stdin to
   prevent interactive prompts.
3. WHEN toolchain commands are available THEN this slice MAY record versions or
   availability diagnostics, but SHALL NOT run builds, restores, package
   resolution, simulators, devices, tests, or app code.
4. WHEN project/package metadata is partial because required tooling is
   unavailable or intentionally not run THEN the manifest SHALL mark Swift
   coverage as reduced.
5. WHEN toolchain diagnostics are persisted THEN local absolute tool paths,
   usernames, machine names, raw environment variables, and private paths SHALL
   be omitted or hashed.
6. WHEN diagnostics explain a gap THEN they SHALL include a rule ID, evidence
   tier, gap reason, extractor version, and public-safe file or tool label.

### Requirement 8: Output Artifacts And Report Wording

**User Story:** As an automation author, I want Swift inventory output to be
stable, machine-readable, and honest.

#### Acceptance Criteria

1. WHEN the Swift adapter completes THEN it SHALL write
   `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
   `logs/analyzer.log`, including when no Swift evidence is detected. A
   no-Swift scan SHALL use the Swift no-evidence coverage label with an empty
   fact set rather than skipping artifacts. `index.sqlite` SHALL use the shared
   TraceMap SQLite index schema required by combine/report/reducer tooling, not
   a Swift-only placeholder database.
2. WHEN facts are written THEN fact IDs, scan IDs, property ordering, report
   ordering, SQLite rows, and NDJSON ordering SHALL be deterministic for the
   same repo, commit, options, and toolchain diagnostics.
3. WHEN analysis is partial THEN all artifacts that summarize coverage SHALL
   use reduced or partial coverage labels.
4. WHEN reporting Swift inventory THEN wording SHALL say "discovered",
   "declared", "referenced", "inventory", "metadata", "candidate", or
   "reduced coverage" as appropriate.
5. WHEN reporting Swift inventory THEN wording SHALL NOT say "builds",
   "runs", "is used at runtime", "impacted", "production dependency",
   "compatible", "vulnerable", "safe", or "reachable" unless a future
   evidence-backed reducer explicitly supports that conclusion about the
   scanned subject. Artifact-safety wording such as "public-safe path",
   "redacted", "hashed", or "safe-value policy" is allowed.

### Requirement 9: Rule Catalog, Tests, And Validation

**User Story:** As a maintainer, I want implementation work to be reviewable
and evidence-backed before Swift v0 ships.

#### Acceptance Criteria

1. WHEN implementation emits or changes any Swift rule ID THEN
   `rules/rule-catalog.yml` SHALL be updated before product code emits that
   rule.
2. WHEN a new Swift fact type is introduced THEN tests SHALL prove rule ID,
   evidence tier, file path, line span, commit SHA, extractor version,
   deterministic ordering, and public-safe properties.
3. WHEN toolchain, malformed metadata, unsupported metadata, generated/vendor
   exclusions, and missing project metadata paths are implemented THEN tests
   SHALL prove reduced coverage or gap behavior.
4. WHEN fixtures are added THEN at least one fixture SHALL represent useful
   inventory and at least one fixture SHALL represent reduced or unsupported
   coverage.
5. WHEN the implementation branch is prepared for review THEN validation SHALL
   include the exact commands listed in `implementation-state.md`.

## Out Of Scope

- Swift declaration, call, object, symbol, or semantic analysis.
- SwiftSyntax-backed symbol extraction beyond package manifest syntax
  inventory.
- SourceKit, sourcekit-lsp, `swift build`, `swift test`, `xcodebuild`, simulator
  or device execution.
- Runtime proof of UI, route, storage, network, dependency, or package usage.
- Package dependency surface interpretation reserved for issue #382.
- Vulnerability, license, compatibility, freshness, or registry analysis.
- LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact analysis.
- Raw source snippets or raw manifest snippets by default.
