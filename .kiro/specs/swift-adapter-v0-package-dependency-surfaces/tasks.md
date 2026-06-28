# Swift Adapter v0 Package And Dependency Surfaces Tasks

Issue: [#382](https://github.com/joefeser/tracemap/issues/382)

This spec PR is documentation/planning only. Do not check implementation tasks
until product code, tests, validation, and PR-loop evidence have landed.

## Phase 0: Scope And Contracts

- [ ] Confirm implementation branch name:
  `codex/implement-swift-package-dependency-surfaces`.
- [ ] Confirm this implementation-state file exists with initial branch, scope,
  prerequisite, and validation notes before beginning Phase 1.
- [ ] Re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`,
  `docs/ACCEPTANCE.md`, issue #377, issue #382, and the merged Swift v0
  prerequisite specs before implementation.
- [ ] Confirm the implementation starts from latest `origin/dev` after #381 is
  merged.
- [ ] Confirm `swift run --package-path src/swift tracemap-swift-smoke-tests`
  exits 0 against an existing Swift sample before implementing Phase 3-5 code.
  If the smoke executable is absent or broken, stop and resolve the prerequisite
  before continuing.
- [ ] Confirm no package manager restore/build/registry/network operation is in
  scope.
- [ ] Confirm all new fact types and rule IDs are added to
  `rules/rule-catalog.yml` before emission.

## Phase 1: Rule Catalog, Model, And Safety Helpers

- [ ] Add rule catalog entries for:
  `swift.dependency.manifest.v1`,
  `swift.dependency.lockfile.swiftpm.v1`,
  `swift.dependency.lockfile.text.v1`,
  `swift.dependency.analysis-gap.v1`.
- [ ] Add `swift.dependency.surface.v1` only as a planned/deferred rule with
  `status: deferred`, and document that `status` convention in the rule catalog
  header; promote it to `status: active` only when Phase 6 implements and tests
  composition.
- [ ] Update `rules/rule-catalog.yml` header to document status field vocabulary
  (`active`, `deferred`, `planned`), confirm existing rules are not broken by
  the addition, and add a catalog lint check or validation note that new rules
  require an explicit status field when the convention is introduced.
- [ ] Document limitations for SwiftPM, CocoaPods, Carthage, lockfiles, dynamic
  metadata, unsafe identity values, and absent runtime/build proof.
- [ ] Define dependency fact/property model and stable identity inputs.
- [ ] Add or reuse safe-value helpers for dependency identities, locations,
  versions, revisions, and source kinds.
- [ ] Reuse existing safe-label predicates or normalization behavior where
  useful, but build new per-row parsers instead of reusing aggregate
  `parsePodIdentities` or `parseCartfileIdentities` results directly.
- [ ] Document that existing Swift inventory facts are aggregate/count-level
  handoff evidence and new dependency facts are per-row evidence that may cite
  aggregate facts via `supportingFactIds`.
- [ ] Define per-source lockfile evidence tiers:
  `Package.resolved` v1/v2 uses `swift.dependency.lockfile.swiftpm.v1` and
  SHALL be `Tier2Structural`; `Podfile.lock` and `Cartfile.resolved` use
  `swift.dependency.lockfile.text.v1` and remain `Tier3SyntaxOrTextual` in v0.
- [ ] Define closed vocabularies for dependency identity, version, revision,
  source-location, Cartfile `sourceKind`, and metadata status properties.
- [ ] Emit aggregate inventory facts before dependency surface facts, omit
  `supportingFactIds` entirely when no aggregate fact exists, and add tests for
  both present and absent aggregate support. Sort `supportingFactIds` by
  ascending UTF-8 byte order of the fact ID string.
- [ ] Document sorted repo-relative file processing order before per-file
  parser-event occurrence indexes are assigned.
- [ ] Document byte-offset anchor tokens for occurrence indexing:
  `.package(`, `pod`, `github`/`git`/`binary` source-kind tokens, and
  lockfile JSON `identity`/`package` keys.
- [ ] Document full 64-character SHA-256 dependency hash format and assert
  stable keys/fact IDs never contain raw URLs, hosts, or local paths.
- [ ] Do not emit `swift-dependency-metadata-too-large` until the exact
  file-size or dependency-count threshold is documented in
  `rules/rule-catalog.yml` and covered by a boundary fixture.
- [ ] Add tests proving raw URLs, hostnames, local paths, credentials, raw
  snippets, raw remotes, and unsafe labels do not appear in generated artifacts.

## Phase 2: Fixtures And Test Harness

- [ ] Add SwiftPM fixture data for static `Package.swift` dependency
  declarations and supported `Package.resolved` v1/v2 pins.
- [ ] Add CocoaPods fixture data for static `Podfile` declarations and
  parseable `Podfile.lock` sections.
- [ ] Add Carthage fixture data for static `Cartfile` and
  `Cartfile.resolved` entries.
- [ ] Add or extend a checked-in sample such as
  `samples/swift-dependency-surfaces` and include it in validation commands.
  The sample must include SwiftPM (`Package.swift`, `Package.resolved`),
  CocoaPods (`Podfile`, `Podfile.lock`), and Carthage (`Cartfile`,
  `Cartfile.resolved`) metadata unless a prerequisite sample is explicitly
  cited for that manager.
- [ ] Add malformed/dynamic/unsupported metadata fixtures for each package
  manager.
- [ ] Add duplicate dependency fixture rows proving deterministic distinct fact
  IDs and no arbitrary winner behavior.
- [ ] Add unsafe same-line dependency rows proving parser-event occurrence
  discriminators are deterministic and distinct.
- [ ] Interpret the unsafe same-line fixture as two dependency entries that
  normalize to the same safe identity, file path, and line range, proving the
  occurrence index is the sole discriminator and both facts appear in output.
  This may be a synthetic fixture constructed specifically for stable-key
  collision coverage.
- [ ] Add `Package.resolved` schema v3 fixture proving unsupported schema emits
  a gap rather than a crash.
- [ ] Add a unit test asserting the schema v3 fixture emits exactly the
  dependency-specific unsupported-schema gap with
  `ruleId = swift.dependency.analysis-gap.v1` and zero
  `SwiftDependencyLockfileEntryDeclared` rows for that file.
- [ ] Ensure the schema v3 fixture is structurally valid JSON with a v3 schema
  marker such as `"version": 3`, so the test exercises unsupported schema
  handling rather than malformed JSON handling.
- [ ] Add the same schema v3 assertion to the Swift smoke executable as
  validation backstop.
- [ ] Add local-path `.package(path:)` fixture proving raw local paths are
  omitted and gap/status evidence is emitted.
- [ ] Add empty-pins `Package.resolved` v1 or v2 fixture proving zero lockfile
  entry facts, zero lockfile gaps, and no coverage downgrade.
- [ ] Add Cartfile `binary` fixture proving unsafe source location handling.
- [ ] Assert binary-source Cartfile facts use `sourceLocationStatus` of
  `hashed` or `unsafe-omitted` and never `safe`.
- [ ] Add mixed safe/dynamic Podfile fixture proving safe rows and gap rows
  coexist.
- [ ] Add multi-line pod declaration fixture, for example `pod 'Alamofire',`
  followed by an indented version line, and assert the occurrence anchor is the
  `pod` keyword on the first line rather than the closing delimiter or final
  option line.
- [ ] Add duplicate `SPEC CHECKSUMS` pod-name fixture proving exactly one
  `swift-dependency-lockfile-malformed` gap and that the checksum hash input
  includes only one entry for the duplicated raw key string.
- [ ] Add zero-dependency metadata fixture proving reports do not imply an
  error or clean absence beyond the existing coverage label.
- [ ] Add aggregate-vs-per-row report fixture proving dependency counts are not
  double-counted.
- [ ] Add ordering assertion proving aggregate inventory facts appear before
  `SwiftDependencyDeclared` and `SwiftDependencyLockfileEntryDeclared` facts in
  `facts.ndjson` within the same scan.
- [ ] Ensure the ordering fixture contains at least one dependency row whose
  aggregate inventory fact comes from the same scan and would appear later by
  file-order coincidence unless the emitter enforces aggregate-before-per-row
  ordering.
- [ ] Add supportingFactIds stability assertion across identical scans.
- [ ] Add supportingFactIds absence assertion when an aggregate inventory fact
  is absent from the same scan.
- [ ] Add context-aware forbidden-word fixture that renders
  Swift metadata filenames and TraceMap fact type/rule ID identifiers without
  false failures while still rejecting narrative overclaims. Do not hard-code a
  brittle allowlist of only the current fact names.
- [ ] Add closed-vocabulary tests for `dependencyIdentityStatus`,
  `versionStatus`, `revisionStatus`, `sourceLocationStatus`,
  `declarationKind`, Cartfile `sourceKind`, and any additional metadata status
  fields emitted by the implementation.
- [ ] Add sourceSection safety test proving raw unsafe section headings are
  hashed or omitted before emission.
- [ ] Add zero-static-dependency `Package.swift` fixture proving no false clean
  absence claim.
- [ ] Add duplicate `Package.resolved` identity fixture proving deterministic
  distinct rows and one `swift-dependency-lockfile-malformed` gap for the
  duplicate-pin ambiguity.
- [ ] Add test proving `normalizedDependencyIdentity` is absent when
  `dependencyIdentityStatus` is `hashed` or `unsafe-omitted`.
- [ ] Confirm `tracemap-swift-smoke-tests`, created by the prerequisite Swift
  v0 smoke-test work, remains the executable validation target and add new
  smoke assertions there unless a stronger Swift test target exists.
- [ ] Add byte-stability tests proving dependency facts are unchanged when only
  output path changes after Phase 3-5 emit dependency facts.

## Phase 3: SwiftPM Dependency Surfaces

- [ ] Parse conservative static `.package(...)` declarations from
  `Package.swift` without executing Swift.
- [ ] Emit `SwiftDependencyDeclared` facts for safe SwiftPM manifest
  dependencies.
- [ ] Emit `SwiftDependencyLockfileEntryDeclared` facts for supported
  `Package.resolved` pins.
- [ ] Treat `Package.resolved` schema v3 and later as unsupported until
  explicitly specified and emit a dependency-specific gap.
- [ ] Hash or omit unsafe SwiftPM URLs, hostnames, local paths, revisions, and
  branches.
- [ ] For URL-only SwiftPM dependencies, derive a candidate identity from the
  final URL path component minus `.git`, pass it through the shared safe-value
  policy, and hash full identity material when rendering is unsafe.
- [ ] Emit dependency-specific gaps for dynamic manifests, unsupported shapes,
  malformed lockfiles, unsupported schemas, and unsafe identities.
- [ ] Add tests for safe rows, unsafe rows, malformed rows, duplicate rows, and
  report rendering.

## Phase 4: CocoaPods Dependency Surfaces

- [ ] Parse conservative static `pod` declarations from `Podfile` without
  executing Ruby.
- [ ] Emit `SwiftDependencyDeclared` facts for safe CocoaPods declarations.
- [ ] Parse common `Podfile.lock` `PODS` and `DEPENDENCIES` sections.
- [ ] Emit `SwiftDependencyLockfileEntryDeclared` facts for safe lockfile
  entries.
- [ ] When a pod appears in both `PODS` and `DEPENDENCIES`, emit one lockfile
  entry fact per section occurrence rather than merging the rows.
- [ ] Skip `SPEC REPOS`, `COCOAPODS`, and unknown sections unless the file is
  otherwise malformed; read `SPEC CHECKSUMS` only for checksum presence/count
  or an aggregate hash over sorted pod names.
- [ ] For duplicate `SPEC CHECKSUMS` pod-name keys, emit one
  `swift-dependency-lockfile-malformed` gap and deduplicate the raw key before
  computing `podChecksumSectionHash`.
- [ ] Treat `source`, `target`, `platform`, `use_frameworks!`, `plugin`, and
  similar Podfile configuration lines as out of scope for v0 dependency rows.
- [ ] Hash or omit git/path/source options and unsafe external values.
- [ ] Emit gaps for dynamic Ruby, unsupported sections, malformed lockfiles,
  and ambiguous identities.
- [ ] Add tests for safe rows, unsafe rows, malformed rows, duplicate rows, and
  report rendering.

## Phase 5: Carthage Dependency Surfaces

- [ ] Parse conservative static `Cartfile` entries for `github`, `git`,
  `binary`, and other documented source kinds.
- [ ] Emit `SwiftDependencyDeclared` facts for safe Carthage declarations.
- [ ] Emit `sourceKind=github`, `git`, `binary`, or `unknown` for Cartfile
  manifest rows while keeping `declarationKind=cartfile-declaration`.
- [ ] Parse supported `Cartfile.resolved` entries.
- [ ] Emit `SwiftDependencyLockfileEntryDeclared` facts for safe resolved rows.
- [ ] Persist clean semver version strings when safe, and reduce non-semver
  revisions, branches, URLs, and unsafe values to status flags plus hashes.
- [ ] Emit `revisionStatus=absent` for manifest Cartfile rows that contain only
  a version constraint.
- [ ] Treat manifest `Cartfile` version ranges such as `~> 1.0` as
  `versionStatus=present` with the raw range hashed, not rendered.
- [ ] Hash or omit unsafe external locations and versions/revisions where
  needed.
- [ ] Emit gaps for malformed, unsupported, unsafe, or ambiguous Carthage
  metadata.
- [ ] Add tests for safe rows, unsafe rows, malformed rows, duplicate rows, and
  report rendering.

## Phase 6: Optional Surface Composition

- [ ] Defer `SwiftDependencySurfaceDeclared` in this slice. If a planned
  catalog entry is added, mark/document it as planned/deferred and assert no
  emitted fact uses the rule.
- [ ] Treat `SwiftDependencySurfaceDeclared` as active only when
  `swift.dependency.surface.v1` is `status: active` in the rule catalog and a
  test assertion covering composed output exists. Until both conditions are met,
  assert `stableDependencySurfaceKey` does not appear in any emitted fact.
- [ ] Implement the `stableDependencySurfaceKey` suppression as a code-level
  guard in the Swift adapter; do not read `rules/rule-catalog.yml` at runtime to
  decide whether to emit the property. The catalog `status: deferred` entry is
  documentation and validation evidence only.
- [ ] Add a smoke assertion that no emitted `facts.ndjson` row uses
  `ruleId = swift.dependency.surface.v1` while the composed surface is deferred.
- [ ] Add an assertion that `stableDependencySurfaceKey` is absent from all
  emitted facts, including dependency rows and `AnalysisGap` rows, while
  composed surfaces are deferred.
- [ ] Record the future composition rule: if emitted later, compose only
  declaration/lockfile evidence with matching safe package-manager identity,
  preserve supporting fact IDs, use no stronger tier than the weakest support,
  and never emit when any supporting dependency evidence is `Tier4Unknown`.

## Phase 7: SQLite, Reports, Export, And Combine

- [ ] Persist dependency facts in `facts.ndjson` and `index.sqlite` with sorted
  properties and stable fact IDs.
- [ ] Add report sections or summary rows for Swift dependency counts by
  package manager, metadata kind, identity status, and gaps.
- [ ] Add report/export tests proving aggregate inventory counts and per-row
  dependency facts are not double-counted.
- [ ] Identify the Swift local report and .NET combined dependency-report
  sections that render dependency counts, and guard them against summing
  aggregate inventory facts with per-row dependency facts.
- [ ] Verify report wording uses static dependency metadata language only.
- [ ] Add forbidden-word assertions for report output: no unsupported claims of
  dependency restored/resolved, package installed, compatible, vulnerable,
  fresh, linked, loaded, runtime used, production used, or build succeeded
  dependency behavior. Allow literal Swift metadata filenames and TraceMap fact
  type or rule ID identifiers as classified by the shared safe-value policy.
- [ ] Include a forbidden-word fixture that renders rule IDs such as
  `swift.dependency.lockfile.swiftpm.v1` next to dependency language without
  false positives while still rejecting narrative overclaims outside
  `Limitations` or `Gaps` sections.
- [ ] Verify `.NET` `export`, `combine`, and `report` preserve dependency
  facts, rule IDs, evidence tiers, source labels, commit SHA, and gaps.
- [ ] Add focused tests or smoke assertions for generated public-safety and
  deterministic output.

## Phase 8: Documentation And Validation

- [ ] Update `docs/VALIDATION.md` with any new Swift fixture commands or report
  checks.
- [ ] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if a shared dependency
  surface contract is introduced.
- [ ] Before Phase 7 implementation merges, record in implementation-state
  whether aggregate-before-per-row emission ordering is a shared adapter
  contract or Swift-specific. If shared, add it to
  `docs/LANGUAGE_ADAPTER_CONTRACT.md` and link from the adapter contract
  section.
- [ ] Update `docs/ACCEPTANCE.md` only if public acceptance claims change.
- [ ] Keep public/site wording bounded to static checked-in metadata evidence.
- [ ] Record implementation decisions, validation, and follow-ups in
  `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/implementation-state.md`.

## Phase 9: Validation

- [ ] Run Swift adapter validation:

```bash
test -d samples/swift-dependency-surfaces
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-dependency-surfaces --out /tmp/tracemap-swift-dependency-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
```

The `samples/swift-metadata-reduced` and
`samples/swift-metadata-unsupported` inputs come from prerequisite Swift v0
metadata/gap specs. This spec owns `samples/swift-dependency-surfaces`.

- [ ] Run shared reader validation:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-dependency-surfaces/index.sqlite --out /tmp/tracemap-swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-dependency-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
```

- [ ] Run core validation when shared reader behavior changes:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

- [ ] Run safety checks:

```bash
./scripts/check-private-paths.sh
git diff --check
```

## Spec PR Review Commands

Run these for this spec-only PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```
