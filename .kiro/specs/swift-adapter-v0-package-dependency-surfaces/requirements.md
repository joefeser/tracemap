# Swift Adapter v0 Package And Dependency Surfaces Requirements

## Introduction

TraceMap already inventories Swift package and ecosystem metadata from checked-in
files. This spec turns that inventory into normalized Swift dependency surface
facts for SwiftPM, CocoaPods, and Carthage without executing package resolution,
builds, app code, Xcode, simulators, devices, or registry lookups.

This is a spec-only phase for GitHub issue #382. It must not implement Swift
adapter runtime code. The implementation branch should be
`codex/implement-swift-package-dependency-surfaces`.

## Issue Links

- Parent: https://github.com/joefeser/tracemap/issues/377
- This spec: https://github.com/joefeser/tracemap/issues/382
- Prerequisites:
  - https://github.com/joefeser/tracemap/issues/378
  - https://github.com/joefeser/tracemap/issues/379
  - https://github.com/joefeser/tracemap/issues/380
  - https://github.com/joefeser/tracemap/issues/381

## Current State

- The Swift adapter lives under `src/swift`.
- Swift scans write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- Existing Swift metadata inventory emits safe facts such as
  `SwiftPackageManifestDeclared` (`swift.swiftpm.manifest.v1`),
  `SwiftPackageResolvedDeclared` (`swift.swiftpm.resolved.v1`), and ecosystem
  metadata facts for CocoaPods and Carthage files
  (`swift.ecosystem.metadata.v1`).
- Existing Swift scans remain syntax/structural evidence only and must not
  claim build success, dependency compatibility, or runtime package loading.
- Shared dependency-surface reporting exists for other adapters and must stay
  deterministic, evidence-backed, and public-safe.

## Scope Decisions

- The implementation SHALL derive dependency surface facts only from checked-in
  metadata files:
  - `Package.swift`
  - `Package.resolved`
  - `Podfile`
  - `Podfile.lock`
  - `Cartfile`
  - `Cartfile.resolved`
- The implementation SHALL NOT run `swift package resolve`, `swift build`,
  `pod install`, `pod update`, `carthage bootstrap`, `carthage update`,
  `xcodebuild`, app code, tests, simulators, devices, network calls, registry
  APIs, vulnerability feeds, license scanners, or compatibility solvers.
- The implementation SHALL emit deterministic facts with rule IDs, evidence
  tiers, repo-relative file paths, line spans, commit SHA, extractor ID, and
  extractor version.
- The implementation SHALL normalize safe dependency identities where possible
  and hash or omit unsafe raw locations, URLs, hostnames, local paths, remotes,
  credentials, source snippets, manifest snippets, and private labels.
- The implementation SHALL label all rows as static dependency metadata
  evidence. A dependency surface is not proof that the package was restored,
  built, loaded, linked, used at runtime, compatible, vulnerable, licensed, or
  production-reachable.
- The implementation SHALL emit gaps when metadata is malformed, unsupported,
  dynamic, ambiguous, unsafe, or too weak to produce a credible dependency
  surface.

## Public Claim Level

Public copy MAY say Swift v0 can produce deterministic static package/dependency
metadata evidence from checked-in SwiftPM, CocoaPods, and Carthage files, with
rule IDs, evidence tiers, commit SHA provenance, extractor versions, coverage
labels, and public-safe paths.

Public copy MUST NOT say TraceMap proves dependency resolution, installed
package versions, license status, vulnerability status, package freshness,
compatibility, build success, runtime loading, reachable code paths, production
usage, or impact for this slice.

## Requirements

### Requirement 1: Rule-Backed Dependency Surface Facts

**User Story:** As a reviewer, I want Swift dependency evidence represented as
rule-backed facts so downstream reports can explain what was proven and what was
not.

#### Acceptance Criteria

1. WHEN the implementation emits Swift package/dependency surface evidence THEN
   every fact SHALL have a documented rule ID and limitation entry in
   `rules/rule-catalog.yml`.
2. WHEN SwiftPM, CocoaPods, or Carthage metadata is parsed THEN the adapter
   SHALL emit Swift-specific dependency fact types or shared dependency fact
   types only after documenting reducer/report behavior.
3. WHEN a dependency identity is safe to render THEN the fact MAY include a
   normalized dependency name or identity.
4. WHEN a dependency source/location is unsafe or external THEN the fact SHALL
   hash or omit the value and SHALL NOT store raw URLs, hostnames, local paths,
   credentials, raw remotes, or private labels.
5. WHEN evidence is derived from syntax or text only THEN the evidence tier
   SHALL NOT exceed `Tier3SyntaxOrTextual`.
6. WHEN manifest declaration evidence is derived from syntax or text scanning
   THEN all `SwiftDependencyDeclared` manifest rows for SwiftPM, CocoaPods, and
   Carthage SHALL remain `Tier3SyntaxOrTextual`.
   `Package.resolved` v1/v2 lockfile rows SHALL use `Tier2Structural` through
   `swift.dependency.lockfile.swiftpm.v1`. `Podfile.lock` and
   `Cartfile.resolved` rows SHALL use `Tier3SyntaxOrTextual` through
   `swift.dependency.lockfile.text.v1`.

### Requirement 2: SwiftPM Dependency Surfaces

**User Story:** As a Swift maintainer, I want checked-in SwiftPM package
metadata surfaced without running package resolution.

#### Acceptance Criteria

1. WHEN `Package.swift` contains statically visible dependency declarations THEN
   the adapter SHALL emit dependency declaration evidence with safe identity,
   package manager, declaration kind, file path, line span, and rule ID.
2. WHEN `Package.swift` contains dynamic dependency declarations, computed
   variables, environment-dependent values, plugins, conditionals, unsupported
   syntax, or values that cannot be safely normalized THEN the adapter SHALL
   emit an `AnalysisGap` rather than invent a dependency surface.
3. WHEN `Package.resolved` contains supported schema v1 or v2 pins THEN the
   adapter SHALL emit lockfile dependency evidence with safe identity, schema
   version, state kind, version/revision presence flags, and hashed unsafe
   location/revision values where needed.
4. WHEN `Package.resolved` is malformed, missing expected pin fields, or uses an
   unsupported schema version, including schema v3 until explicitly supported,
   THEN the adapter SHALL emit a
   `swift-dependency-lockfile-unsupported-schema` gap where applicable and
   continue scanning.
5. WHEN two pins in the same `Package.resolved` share the same `identity` or
   `package` value THEN the adapter SHALL emit one
   `SwiftDependencyLockfileEntryDeclared` per pin with distinct occurrence
   indexes and one `AnalysisGap` with kind
   `swift-dependency-lockfile-malformed`.
6. WHEN both manifest and lockfile evidence for the same safe identity exists
   THEN the adapter MAY emit a deterministic same-manager supporting key, but
   SHALL NOT claim the dependency was resolved, fetched, or used. This
   criterion applies only to future composed surface/supporting keys and does
   not collapse per-pin lockfile facts.

### Requirement 3: CocoaPods Dependency Surfaces

**User Story:** As a reviewer of older iOS code, I want Podfile and Podfile.lock
metadata surfaced without running CocoaPods.

#### Acceptance Criteria

1. WHEN `Podfile` contains statically visible `pod` declarations with safe
   package names THEN the adapter SHALL emit CocoaPods dependency declaration
   evidence with package manager, dependency identity, declaration kind, file
   path, line span, and rule ID.
2. WHEN `Podfile` contains dynamic Ruby code, variables, computed pod names,
   environment-dependent values, git/path/source options, plugin hooks, or
   unsupported syntax THEN the adapter SHALL emit gaps or hash unsafe values
   rather than render raw data.
3. WHEN `Podfile.lock` contains parseable `PODS`, `DEPENDENCIES`, or checksum
   sections THEN the adapter SHALL emit lockfile evidence for safe dependency
   identities and section counts without storing raw external locations. Any
   `podChecksumSectionHash` is a file-level property emitted once per
   `Podfile.lock` file, not a per-lockfile-entry property.
4. WHEN `Podfile.lock` is malformed or too ambiguous to parse deterministically
   THEN the adapter SHALL emit reduced-coverage gap evidence and continue
   scanning.

### Requirement 4: Carthage Dependency Surfaces

**User Story:** As a reviewer of legacy Apple apps, I want Cartfile metadata
surfaced without running Carthage.

#### Acceptance Criteria

1. WHEN `Cartfile` contains statically visible dependencies THEN the adapter
   SHALL emit Carthage dependency declaration evidence with package manager,
   dependency identity status, declaration kind, source kind, file path, line
   span, and rule ID.
2. WHEN `Cartfile` or `Cartfile.resolved` contains GitHub, git, binary, or other
   external locations THEN persisted properties SHALL hash or omit unsafe raw
   locations and MAY retain only safe owner/repo-like identity labels when the
   shared safety policy allows them.
3. WHEN `Cartfile.resolved` contains parseable pinned entries THEN the adapter
   SHALL emit lockfile evidence with safe dependency identity and version or
   revision presence flags, hashing unsafe version/source values where needed.
4. WHEN Carthage metadata is malformed, unsupported, or dynamic THEN the
   adapter SHALL emit gaps and continue scanning.

### Requirement 5: Stable Identity And Deterministic Output

**User Story:** As a downstream report consumer, I want dependency surface keys
and fact IDs to remain stable across identical scans.

#### Acceptance Criteria

1. WHEN dependency facts are emitted THEN fact IDs SHALL be deterministic from
   scan ID, package manager, dependency kind, safe identity or hash, file path,
   line span, and documented occurrence discriminators.
2. WHEN two metadata rows have the same safe dependency identity in the same
   file THEN the adapter SHALL preserve distinct deterministic facts without
   crashing or picking an arbitrary winner.
3. WHEN only the output directory changes THEN `facts.ndjson` dependency rows
   SHALL remain byte-stable. Timestamp carve-outs apply only to documented
   manifest scan-time fields, not dependency fact rows.
4. WHEN dependency status properties such as `dependencyIdentityStatus`,
   `versionStatus`, `revisionStatus`, or `sourceLocationStatus` are emitted THEN
   their values SHALL come from the closed vocabulary documented in the spec and
   tests SHALL assert that vocabulary.
5. WHEN dependency metadata contains unsafe values THEN no generated artifact
   SHALL contain raw source snippets, raw manifest snippets, raw URLs,
   hostnames, local absolute paths, raw remotes, credentials, secrets, or
   private labels.

### Requirement 6: SQLite, Combine, Report, And Export Compatibility

**User Story:** As a TraceMap user, I want Swift dependency surfaces to flow into
existing shared readers without overclaiming.

#### Acceptance Criteria

1. WHEN Swift dependency facts are written THEN `index.sqlite` SHALL preserve
   the facts and safe properties in the shared fact/property schema.
2. WHEN a dependency surface maps to an existing shared surface table or
   combined-report concept THEN the implementation SHALL document the mapping
   and ensure evidence tier and rule ID survive export/combine/report.
3. WHEN no shared table has an exact safe semantic match THEN the implementation
   SHALL keep Swift-specific facts in the generic facts/properties schema rather
   than forcing them into a misleading table.
4. WHEN `tracemap export`, `tracemap combine`, and `tracemap report` consume a
   Swift index with dependency facts THEN outputs SHALL remain deterministic,
   public-safe, and coverage-labeled.
5. WHEN reports render dependency metadata THEN they SHALL include limitations
   and SHALL NOT imply package health, license, vulnerability, compatibility,
   restore, build, runtime loading, production use, or impact.
6. WHEN reports include both existing aggregate inventory facts and new per-row
   dependency facts THEN reports SHALL NOT sum both families as independent
   dependency counts.

### Requirement 7: Reduced Coverage And Gaps

**User Story:** As a maintainer, I want unsupported or partial package metadata
called out clearly instead of silently ignored.

#### Acceptance Criteria

1. WHEN package metadata is malformed, unsupported, unsafe, dynamic, ambiguous,
   omitted, or too large THEN the adapter SHALL emit rule-backed gap facts.
2. WHEN dependency surface extraction is partial THEN scan manifest and report
   coverage SHALL remain reduced/partial rather than clean.
3. WHEN metadata is absent THEN the adapter MAY report zero dependency surfaces
   without a gap, but SHALL NOT claim absence of dependencies unless the scan
   coverage and selected scope justify that wording.
4. WHEN toolchains such as SwiftPM, CocoaPods, or Carthage are missing THEN
   dependency surface extraction from checked-in metadata SHALL still run
   because this slice never invokes package managers, build tools, or network
   operations.
5. WHEN a supported metadata file is present and yields zero dependency rows
   without a dependency gap THEN this slice SHALL NOT independently downgrade
   coverage; it preserves the coverage state established by the inventory and
   metadata phases.
6. WHEN dependency metadata files are absent THEN this slice SHALL NOT upgrade
   coverage or mark dependency analysis clean; it preserves the coverage state
   established by the inventory and metadata phases.

### Requirement 8: Validation

**User Story:** As a reviewer, I want focused tests and smokes proving the
behavior before merge.

#### Acceptance Criteria

1. WHEN the implementation is complete THEN Swift smoke tests SHALL cover
   SwiftPM manifest dependencies, SwiftPM lockfile pins, Podfile declarations,
   Podfile.lock entries, Cartfile declarations, Cartfile.resolved entries,
   unsafe values, duplicate entries, malformed metadata, and deterministic
   output.
2. WHEN unsupported, unsafe, or dynamic metadata appears next to safe metadata
   in the same file THEN tests SHALL prove safe per-row facts and
   dependency-specific gap facts are both emitted and no rows silently vanish.
3. WHEN shared reader behavior changes THEN `.NET` build/test and export,
   combine, and report smokes SHALL be run against a generated Swift fixture
   index.
4. WHEN rule IDs are emitted THEN tests SHALL prove every emitted rule ID exists
   in `rules/rule-catalog.yml`.
5. WHEN generated artifacts are created THEN validation SHALL include
   `./scripts/check-private-paths.sh` and `git diff --check`.
6. WHEN report output is generated THEN tests SHALL check that dependency
   sections do not render forbidden overclaim wording such as dependency
   restored/resolved, package installed, compatible, vulnerable, fresh, linked,
   loaded, runtime used, production used, or build succeeded except inside
   explicit limitation/gap language. The test SHALL allow literal Swift metadata
   filenames and TraceMap fact type or rule ID identifiers as classified by the
   shared safe-value policy, rather than hard-coding a short allowlist.
7. WHEN `SwiftDependencySurfaceDeclared` composition is deferred THEN no
   emitted artifact SHALL contain a fact with
   `ruleId = swift.dependency.surface.v1`.
8. WHEN dependency identity status is `hashed` or `unsafe-omitted` THEN
   generated facts SHALL omit `normalizedDependencyIdentity`.
9. WHEN `Package.resolved` schema v3 is scanned before explicit support exists
   THEN tests SHALL assert exactly one dependency-specific unsupported-schema
   gap and zero `SwiftDependencyLockfileEntryDeclared` rows for that file.
10. WHEN a zero-dependency manifest is reported THEN output SHALL NOT imply the
    repository has no external dependencies unless scan coverage and selected
    scope justify that conclusion.
