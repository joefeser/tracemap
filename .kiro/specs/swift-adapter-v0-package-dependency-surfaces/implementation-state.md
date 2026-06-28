# Swift Adapter v0 Package And Dependency Surfaces Implementation State

Status: `ready-for-implementation`

Issue: [#382](https://github.com/joefeser/tracemap/issues/382)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-package-dependency-surfaces`

Intended implementation branch:
`codex/implement-swift-package-dependency-surfaces`

## Current Scope

This spec prepares the Swift v0 package/dependency surface slice. It should
turn checked-in SwiftPM, CocoaPods, and Carthage metadata into deterministic
static dependency facts and gap facts.

This spec does not implement analyzer code. All implementation tasks in
`tasks.md` remain unchecked until code, tests, validation, and PR-loop evidence
land.

## Source Material

- GitHub issue #382:
  https://github.com/joefeser/tracemap/issues/382
- Swift v0 runway issue #377:
  https://github.com/joefeser/tracemap/issues/377
- Swift prerequisite issues/specs:
  - #378 scaffold/output contract
  - #379 inventory/project discovery
  - #380 SwiftSyntax declarations/calls
  - #381 symbol identity/relationships
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- `src/swift/README.md`

## Public Claim Level

Claim level: `dev-only after implementation`.

Before implementation, this is a plan only. After implementation and merge,
TraceMap may claim static checked-in Swift dependency metadata evidence for
supported SwiftPM, CocoaPods, and Carthage files.

Public copy must not claim dependency restore, install, build, link, runtime
load, runtime reachability, compatibility, license status, vulnerability status,
freshness, production use, or impact.

## Scope Decisions

- Use checked-in metadata only.
- Add Swift-specific dependency facts first instead of forcing Swift package
  metadata into shared runtime dependency tables.
- Treat existing SwiftPM/CocoaPods/Carthage inventory facts as aggregate
  count-level handoff evidence. New dependency facts are per-dependency rows and
  should cite aggregate facts through `supportingFactIds` where possible.
- Keep SwiftPM manifest parsing conservative and syntax/textual.
- Treat supported `Package.resolved` v1/v2 JSON rows as structural metadata
  through `swift.dependency.lockfile.swiftpm.v1` only when rule limitations are
  documented. Keep `Podfile.lock` and `Cartfile.resolved` rows
  `Tier3SyntaxOrTextual` through `swift.dependency.lockfile.text.v1` in v0.
- Treat `Package.resolved` schema v3 and later as unsupported until explicitly
  specified.
- Defer `SwiftDependencySurfaceDeclared` composition for the recommended first
  implementation cut. If a catalog entry is added, it uses `status: deferred`
  and emitted facts must not use the rule until composition is implemented.
  Composition is active only when the rule catalog marks
  `swift.dependency.surface.v1` as `status: active` and tests cover composed
  output.
- Emit aggregate inventory facts before per-row dependency facts; omit
  `supportingFactIds` entirely when an aggregate fact is unavailable. Sort
  supporting IDs by UTF-8 byte order of the canonical fact ID string.
- Reuse existing safe-label predicates or normalization behavior where useful,
  but build new per-row parsers instead of directly reusing aggregate
  `parsePodIdentities` or `parseCartfileIdentities` results.
- Keep Swift local `report.md` and .NET combined dependency-report changes in
  scope when dependency facts need report coverage.
- Use context-aware forbidden-word checks that allow literal Swift metadata
  filenames and TraceMap fact/rule identifiers while rejecting narrative
  overclaims.
- Hash or omit unsafe raw values by default.
- Preserve useful evidence even when SwiftPM, CocoaPods, Carthage, or Xcode
  tools are unavailable.

## Review Notes

- Opus and Sonnet reviews were run against the spec branch.
- Follow-up Sonnet blockers were patched by making deferred surface activation,
  checksum hashing, Cartfile `sourceKind`, duplicate `Package.resolved` pins,
  and report-safety checks machine-checkable.

## Validation Plan

Spec PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Implementation PR:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-dependency-surfaces --out /tmp/tracemap-swift-dependency-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-dependency-surfaces/index.sqlite --out /tmp/tracemap-swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-dependency-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Follow-Ups

- If a future shared package/dependency fact contract is introduced, reconcile
  Swift-specific facts with that contract.
- Package vulnerability/license/freshness/compatibility analysis remains out of
  scope and should be a separate product decision, not an adapter default.
- Public sample repos for Swift dependency evidence belong to issue #388.
