# Swift Adapter v0 Package And Dependency Surfaces Design

## Overview

This spec defines the Swift v0 dependency surface slice. The adapter should
convert checked-in SwiftPM, CocoaPods, and Carthage metadata into deterministic
static dependency facts that downstream TraceMap reports can consume safely.

The intended evidence flow is:

```text
checked-in metadata files
  -> safe deterministic parsers
  -> normalized dependency identity or hash-only unsafe identity
  -> Swift dependency facts and gap facts
  -> facts.ndjson + index.sqlite + report.md
  -> export/combine/report compatibility
```

The implementation must stay read-only. It must not execute package managers,
resolve dependencies, fetch registries, build projects, or infer runtime usage.

## Goals

- Emit Swift dependency surface facts from SwiftPM, CocoaPods, and Carthage
  metadata.
- Preserve rule IDs, evidence tiers, repo-relative paths, one-based line spans,
  commit SHA, extractor ID, extractor version, and coverage labels.
- Normalize safe dependency identities and hash/omit unsafe locations.
- Keep generated output deterministic and public-safe.
- Make export/combine/report useful without overclaiming package health or
  runtime behavior.
- Emit explicit gaps for unsupported, dynamic, malformed, ambiguous, or unsafe
  dependency metadata.

## Non-Goals

- No package restore, dependency solving, build, test, registry lookup, license
  lookup, vulnerability lookup, freshness lookup, or compatibility analysis.
- No runtime proof that packages are linked, loaded, used, deployed, or reachable
  in production.
- No Swift compiler semantic model or SourceKit proof.
- No Xcode scheme or target membership proof beyond checked-in metadata.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No raw source snippets, raw manifest snippets, raw URLs, hostnames, local
  absolute paths, raw remotes, credentials, secrets, or private labels in
  default artifacts.

## Proposed Fact Vocabulary

The implementation should add Swift-specific fact types first, because existing
shared dependency vocabulary is not precise enough for package-manager metadata
without risking runtime or compatibility overclaims.

| Fact type | Purpose | Evidence tier |
| --- | --- | --- |
| `SwiftDependencyDeclared` | Static dependency declaration from checked-in manifest-style metadata. | Always `Tier3SyntaxOrTextual` in v0. |
| `SwiftDependencyLockfileEntryDeclared` | Static checked-in lockfile entry from SwiftPM, CocoaPods, or Carthage. | Supported `Package.resolved` v1/v2 JSON rows SHALL use `swift.dependency.lockfile.swiftpm.v1` at `Tier2Structural`; `Podfile.lock` and `Cartfile.resolved` rows use `swift.dependency.lockfile.text.v1` at `Tier3SyntaxOrTextual`. |
| `SwiftDependencySurfaceDeclared` | Deferred optional normalized surface row that composes declaration and lockfile evidence for the same safe identity. | Deferred/planned only in the first cut. If later emitted, no stronger than the weakest supporting evidence and never emitted when any supporting evidence is `Tier4Unknown`. |
| `AnalysisGap` | Dynamic, malformed, unsupported, ambiguous, unsafe, omitted, too-large, or reduced dependency metadata. | `Tier4Unknown`. |

The implementation may choose to skip `SwiftDependencySurfaceDeclared` in the
first cut if declaration and lockfile facts are enough for reports. If it does
emit a composed surface, the rule catalog must state that it is a static
metadata composition only and not dependency resolution proof.

For the first implementation cut, `SwiftDependencySurfaceDeclared` is deferred.
Cross-source manifest-to-lockfile composition requires a stricter normalization
rule than this slice needs for useful per-row facts. Add
`swift.dependency.surface.v1` as a planned/deferred catalog entry, introduce
`status: deferred` as the rule-catalog convention for planned-but-not-emitted
rules, document that convention in the rule catalog header, and assert no
emitted fact uses the rule. Promote it to `status: active` only when composition
is implemented with tests.

## Proposed Rule IDs

- `swift.dependency.manifest.v1`
- `swift.dependency.lockfile.swiftpm.v1`
- `swift.dependency.lockfile.text.v1`
- `swift.dependency.surface.v1`
- `swift.dependency.analysis-gap.v1`

`AnalysisGap` facts emitted by this slice use fact type `AnalysisGap` with
`ruleId = swift.dependency.analysis-gap.v1`. This is compatible with existing
shared readers because gap routing is by fact type and properties; the
dependency-specific rule ID documents the package-surface boundary.

Draft lockfile tier rationale for the rule catalog:

- `swift.dependency.lockfile.swiftpm.v1`: `Package.resolved` v1/v2 supported
  JSON schema with machine-readable schema version, pins, identity/package
  fields, and state fields; this is structural metadata and may be
  `Tier2Structural`.
- `swift.dependency.lockfile.text.v1`: `Podfile.lock` YAML-like text with
  conventional section headings/indentation and `Cartfile.resolved` line-scanned
  entries; these remain `Tier3SyntaxOrTextual` in v0.

Existing inventory rules remain aggregate/count-level handoff evidence:

- `swift.package.swiftpm.v1` inventories SwiftPM metadata presence.
- `swift.swiftpm.manifest.v1` emits aggregate `SwiftPackageManifestDeclared`
  manifest evidence such as dependency declaration counts.
- `swift.swiftpm.resolved.v1` emits aggregate `SwiftPackageResolvedDeclared`
  lockfile evidence such as safe identity counts.
- `swift.package.cocoapods.v1` and `swift.package.carthage.v1` inventory
  ecosystem metadata files.
- `swift.ecosystem.metadata.v1` emits aggregate CocoaPods/Carthage metadata
  evidence such as dependency identity counts.

The new `swift.dependency.*` rules are per-dependency-row facts. They should
cite existing aggregate inventory facts through `supportingFactIds` when
possible, but reports must not sum aggregate inventory facts and per-row
dependency facts as independent dependency counts.

## Shared Safe Properties

All dependency facts should use sorted string properties. Suggested properties:

| Property | Meaning |
| --- | --- |
| `packageManager` | `swiftpm`, `cocoapods`, or `carthage`. |
| `dependencyIdentityStatus` | `safe`, `hashed`, `unsafe-omitted`, `dynamic`, `ambiguous`, or `unknown`. |
| `normalizedDependencyIdentity` | Safe rendered identity when allowed. |
| `dependencyIdentityHash` | Full SHA-256 for unsafe or non-rendered identity material. |
| `declarationKind` | Closed vocabulary: `swiftpm-manifest-dependency`, `swiftpm-lockfile-pin`, `podfile-declaration`, `podfile-lock-entry`, `cartfile-declaration`, `cartfile-resolved-entry`. The names intentionally mirror source-file language used elsewhere in Swift reports instead of forcing one cross-manager suffix pattern. New values require a spec update. |
| `sourceKind` | Optional source-kind classifier for metadata formats that expose one. Carthage manifest rows use `github`, `git`, `binary`, or `unknown`; all Cartfile manifest entries still use `declarationKind=cartfile-declaration` regardless of source kind. SwiftPM and CocoaPods rows omit this property unless a future spec defines a closed vocabulary for them. |
| `sourceMetadataKind` | `Package.swift`, `Package.resolved`, `Podfile`, `Podfile.lock`, `Cartfile`, or `Cartfile.resolved`. |
| `sourceSection` | Optional safe section label derived from the source file's actual section heading, such as `dependencies`, `pins`, `PODS`, or `DEPENDENCIES`. Pass source headings through the shared safe-value policy before emission; unsafe headings are hashed or omitted. Omit this property when no source section heading exists, including SwiftPM manifest rows. |
| `versionStatus` | `present`, `absent`, `hashed`, `unsafe-omitted`, `dynamic`, or `unknown`. |
| `revisionStatus` | `present`, `absent`, `hashed`, `unsafe-omitted`, `dynamic`, or `unknown`. |
| `sourceLocationStatus` | `safe`, `hashed`, `unsafe-omitted`, `dynamic`, or `unknown`. |
| `supportingFactIds` | Deterministic IDs sorted by ascending UTF-8 byte order of the fact ID string when composing from inventory facts. |
| `stableDependencySurfaceKey` | Deferred composed-surface key only. `SwiftDependencySurfaceDeclared` is considered active only when both `swift.dependency.surface.v1` is `status: active` in the rule catalog and a test assertion covering composed output exists. Until both conditions are met, `stableDependencySurfaceKey` MUST NOT appear in any emitted fact. The guard is a compile-time or initialization-time code constant, not a runtime read of `rules/rule-catalog.yml`. |

Do not persist raw dependency locations. GitHub owner/repo-like labels are not
automatically safe; pass them through the existing safe-value policy or store a
hash only.

For URL-only SwiftPM dependencies, derive a candidate identity from the final
URL path component after removing a `.git` suffix, then pass that candidate
through the shared safe-value policy. Persist it only when allowed. Store the
full URL-derived identity material as a SHA-256 hash when the safe-value policy
rejects rendering. Never persist the raw URL or hostname.

New per-row dependency hashes use a full 64-character lowercase SHA-256 hex
value with no truncated `sha256:<prefix>` shortcut. Truncated safe labels used
by older aggregate inventory helpers are display aids only and must not be used
as dependency fact identity hashes or stable-key inputs.

`declarationKind` is a property of `SwiftDependencyDeclared` and
`SwiftDependencyLockfileEntryDeclared` facts only. `AnalysisGap` facts do not use
this property.

For Carthage `github "owner/repo"` entries, the first cut should treat the full
owner/repo value and the repo component as non-rendered identity material by
default. Store a full SHA-256 hash and `sourceKind=github` unless the shared
safe-value policy explicitly allows rendering that exact candidate. This avoids
leaking private SDK or repository names such as `AcmeSecret/PaymentsSDK`.

## Parser Strategy

### SwiftPM `Package.swift`

Use a conservative syntax/text scanner over checked-in manifest text. The first
implementation cut should support simple static shapes such as:

```swift
.package(url: "https://example.invalid/repo.git", from: "1.0.0")
.package(name: "PackageName", url: "...", branch: "main")
.package(path: "../LocalDependency")
```

The parser should emit safe dependency identity if available, otherwise a hash
only. Unsupported shapes become gaps:

- variables or computed package names;
- environment reads;
- string interpolation;
- conditionals;
- plugin/macro-driven construction;
- local paths or unsafe URLs that cannot be safely normalized;
- syntax too ambiguous to associate a line span.

### SwiftPM `Package.resolved`

Parse supported schema v1 and v2 lockfiles as JSON data. Schema v3 and later
are unsupported in this slice and must emit an unsupported-schema gap rather
than partial rows until explicitly specified. For each supported pin:

- keep safe package identity when allowed;
- record schema version and pin count;
- hash or omit raw locations;
- represent revision/version/branch presence without rendering unsafe values;
- emit gaps for malformed JSON, unsupported schema, or missing expected fields.

If two supported pins in the same `Package.resolved` file share the same
`identity` or `package` value, emit one `SwiftDependencyLockfileEntryDeclared`
fact per pin with distinct occurrence indexes and also emit one `AnalysisGap`
with kind `swift-dependency-lockfile-malformed` to flag the ambiguous duplicate
lockfile state.

### CocoaPods `Podfile`

Use a conservative line scanner for static `pod` declarations:

```ruby
pod 'Alamofire'
pod "RxSwift", "~> 6.0"
```

Treat dynamic Ruby as a gap. Do not execute Ruby. Hash or omit git/path/source
options and unsafe source values. CocoaPods `source`, `target`, `platform`,
`use_frameworks!`, `plugin`, and similar configuration lines are out of scope
for v0 dependency rows; skip them unless their shape prevents safe parsing of
nearby dependency rows.

### CocoaPods `Podfile.lock`

Parse common lockfile section headings and indentation enough to identify safe
pod names under `PODS` and `DEPENDENCIES`. `SPEC CHECKSUMS` values are public
hashes, but this slice should record only checksum presence/count or an
aggregate hash, not individual pod-to-checksum mappings. `SPEC REPOS`,
`COCOAPODS`, and unknown sections should be silently skipped unless the file is
otherwise malformed; they are not dependency rows. If an aggregate checksum
file-level property named `podChecksumSectionHash` is emitted once per
`Podfile.lock` file, its input is the deduplicated list of raw key strings from
`SPEC CHECKSUMS` exactly as they appear in the file, with no case folding or
normalization, sorted by ascending UTF-8 byte order, encoded as UTF-8 with `\n`
separators and no trailing newline, then hashed with SHA-256.
`podChecksumSectionHash` is not emitted on each per-entry lockfile fact.
Duplicate names in `SPEC CHECKSUMS` emit one
`swift-dependency-lockfile-malformed` gap for the duplicate but contribute only
one entry to the hash input. Omit `podChecksumSectionHash` when `SPEC
CHECKSUMS` is absent. The implementation should
avoid claiming the full dependency graph, transitive closure, checksum validity,
spec repo reachability, CocoaPods version compatibility, or installed state.

A pod name appearing in both `PODS` and `DEPENDENCIES` emits one
`SwiftDependencyLockfileEntryDeclared` per section occurrence, not one merged
fact. The `sourceSection` value and occurrence index distinguish the facts if
stable-key values would otherwise collide.

### Carthage `Cartfile` And `Cartfile.resolved`

Line-scan static entries:

```text
github "ReactiveX/RxSwift" "6.0.0"
git "https://example.invalid/repo.git" "main"
binary "https://example.invalid/framework.json" ~> 1.0
```

Persist only safe identity labels when the safety helper allows them. Otherwise
store a full hash and source kind. Do not render URLs or hostnames. Existing
aggregate helpers such as `parsePodIdentities` and `parseCartfileIdentities`
may inform safe-label normalization, but the implementation must add per-row
parsers that preserve duplicates, file spans, source sections, declaration
kinds, unsafe hash/gap behavior, and deterministic occurrence order. Clean
semantic-version strings in `Cartfile.resolved`, such as `6.0.0`, may be
persisted as safe version labels; non-semver revisions, branches, URLs, and
unsafe values are reduced to status flags plus hashes. Version range strings in
manifest `Cartfile` entries, such as `~> 1.0`, are treated as
`versionStatus=present` with the raw range hashed rather than rendered.
Manifest Cartfile rows emit `revisionStatus=absent` when only a version
constraint is present.

## Stable Identity

Dependency fact IDs and optional surface keys should use:

```text
swift-dependency/v1
scanId
packageManager
sourceMetadataKind
sourceSection when present after safety normalization
declarationKind
dependencyIdentityStatus
normalizedDependencyIdentity or dependencyIdentityHash
repo-relative file path
start line
end line
occurrence discriminator when needed
```

The discriminator must be deterministic and body/output-path independent. The
stable key always includes file path, start line, end line, source section when
present after safety normalization, and a 1-based occurrence index. The
occurrence index is assigned and included in the stable key before dependency
identity normalization or hashing from the byte offset of the row's anchor token
in the original UTF-8 file bytes: `.package(` for SwiftPM manifest rows, `pod`
for Podfile rows, `github`/`git`/`binary` or other source-kind token for
Cartfile rows, and the `identity`/`package` field key offset for lockfile JSON
pins. If two rows in the same file have identical values for all other key
fields after normalization/hashing, the occurrence index is the final
discriminator. Do not use timestamps, UUIDs, output paths, local absolute paths,
or raw unsafe persisted values.

Parsers must capture the anchor byte offset when they first recognize the row's
construct, before safety classification or identity normalization, so occurrence
ordering does not depend on whether a row later becomes safe, hashed, or a gap.

For multi-line Podfile declarations, the anchor byte offset is always the byte
position of the `pod` keyword on the first line of the declaration, not the
closing delimiter or the final option line.

Files are processed in ascending ordinal order by repo-relative path before
per-file parser events are assigned. For JSON object members, event order uses
the source-byte offset of the member key from the original UTF-8 text, not the
runtime dictionary order.

## SQLite And Report Behavior

The first cut should store dependency facts in the generic `facts` and
properties schema. It may add a Swift-specific dependency table only if the
shared readers need indexed lookup and the table schema is documented.

Both reports are in scope when behavior changes: the Swift adapter's local
`report.md` and the .NET combined `dependency-report` after `tracemap combine`.
Reports should add a Swift dependency section or extend existing Swift sections
with:

- counts by package manager;
- counts by source metadata kind;
- dependency identity status counts;
- gap counts;
- short limitations.

Reports must not render raw dependency source URLs, hostnames, local paths,
manifest snippets, lockfile snippets, config values, credentials, or private
labels.

### Ordering Invariant

Aggregate inventory facts must be emitted before per-row dependency facts in
`facts.ndjson`. This is an adapter-level serialization invariant, not a side
effect of filesystem enumeration order. Dependency facts reference those
aggregate IDs when present. If the aggregate fact is absent, omit the
`supportingFactIds` property entirely rather than emitting an empty string or
placeholder. `supportingFactIds` values are sorted by ascending UTF-8 byte order
of the fact ID string and must be stable across identical scans.

Report count logic must use per-row `swift.dependency.*` facts for dependency
surface counts and must not add aggregate inventory counts from
`swift.swiftpm.manifest.v1`, `swift.swiftpm.resolved.v1`, or
`swift.ecosystem.metadata.v1` into the same dependency count.

Forbidden-word checks are context-aware: allow literal Swift metadata filenames
and TraceMap fact type or rule ID identifiers as classified by the shared
safe-value policy; do not hard-code a brittle allowlist of only a few current
fact names. Forbid overclaim narrative wording such as dependency
restored/resolved, package installed, compatible, vulnerable, fresh, linked,
loaded, runtime used, production used, or build succeeded outside Markdown
sections whose headings contain `Limitations` or `Gaps`.

A Markdown heading at any level that contains `Limitations` or `Gaps`
case-insensitively exempts its entire section content, including subsections,
from forbidden-word assertions.

## Gap Vocabulary

Suggested gap kinds:

- `swift-dependency-manifest-dynamic`
- `swift-dependency-manifest-unsupported-shape`
- `swift-dependency-manifest-ambiguous-line-span`
- `swift-dependency-lockfile-malformed`
- `swift-dependency-lockfile-unsupported-schema`
- `swift-dependency-identity-unsafe`
- `swift-dependency-identity-ambiguous`
- `swift-dependency-source-location-omitted`
- `swift-dependency-local-path-omitted`
- `swift-dependency-metadata-too-large`
- `swift-dependency-section-unsupported`

Existing generic metadata gaps may continue to exist, but dependency-surface
gaps should use a dependency-specific rule ID when emitted by this slice.
Scan-level reduced coverage remains represented in the manifest/report; the new
dependency gap rule explains the package-surface extraction boundary and should
not replace existing scan-level gap evidence.

When no supported dependency metadata file is present, this slice does not alter
the scan coverage label. When a supported metadata file is present and yields
zero dependency rows without a dependency gap, coverage remains whatever the
prior metadata inventory established.

If the implementation emits `swift-dependency-metadata-too-large`, it must first
document the exact file-size or dependency-count threshold in
`rules/rule-catalog.yml` and add a fixture at that boundary. Until then, prefer
more specific malformed, unsupported, dynamic, ambiguous, or unsafe gap kinds.
A reasonable starting threshold for consideration is 500 declared dependencies
or a 1 MB metadata file, but the implementation team must validate the chosen
number against real Swift repositories before documenting and emitting it.

Dependency-surface gaps such as `swift-dependency-local-path-omitted` document
dependency extraction omissions. They do not independently downgrade scan
coverage; the prior inventory/metadata phases own the coverage label.

## Implementation Slices

### Slice 1: Rules, Model, And Fixtures

- Add rule catalog entries.
- Add public-safe fixtures for SwiftPM, CocoaPods, and Carthage.
- Add unsafe/malformed fixtures.
- Add or extend a checked-in sample, for example
  `samples/swift-dependency-surfaces`, that contains Podfile, Podfile.lock,
  Cartfile, Cartfile.resolved, duplicate dependency rows, URL-only SwiftPM
  dependencies, local-path dependencies, and unsafe dependency values.
- Define dependency identity safety helpers and stable-key inputs.

### Slice 2: SwiftPM Surfaces

- Emit manifest dependency declarations from simple static shapes.
- Emit lockfile entries from supported `Package.resolved` schemas.
- Emit gaps for dynamic and unsupported SwiftPM metadata.

### Slice 3: CocoaPods And Carthage Surfaces

- Emit Podfile and Podfile.lock dependency evidence with new per-row parsers
  that may reuse existing safe-label predicates but must preserve duplicates,
  spans, source sections, declaration kinds, unsafe hash/gap behavior, and
  occurrence order.
- Emit Cartfile and Cartfile.resolved dependency evidence with new per-row
  parsers that may reuse existing safe-label predicates but must preserve
  duplicates, spans, source kinds, unsafe hash/gap behavior, and occurrence
  order.
- Hash/omit unsafe locations and raw options.

### Slice 4: Reports And Shared Reader Smokes

- Add report summaries.
- Verify export/combine/report preserve dependency facts.
- Run focused Swift and .NET validation.

## Safety And No-Overclaim Boundaries

Safe wording:

- static dependency metadata;
- checked-in package metadata;
- lockfile entry evidence;
- dependency identity hash;
- reduced/partial coverage;
- unsupported/dynamic metadata gap.

Unsafe wording:

- dependency resolved;
- package installed;
- package used at runtime;
- package vulnerable or safe;
- license approved or incompatible;
- package fresh or stale;
- build succeeded;
- app linked this dependency;
- dependency caused impact;
- production dependency graph.

## Validation

Spec-only PR validation:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Future implementation PR validation:

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
