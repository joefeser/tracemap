# Package Admission and Revocation Correlation Implementation State

## Status

Grouped implementation PR1 and the grouped PR2 scope are merged. This state
note is the resume point for the shipped record/correlation core,
combined/portfolio/npm/path work, and the grouped PR3 NuGet + Swift resolved
evidence slices.

## Current implementation branch

- Branch: `codex/package-decision-nuget-swift-evidence`
- Base: `origin/dev` at `a884d9e75cbb60b50e58c2b3f0da40027e51aafa` (PR #700)
- Target: `dev`
- Delivery: ready-for-review PR, `Part of #690` (never draft)
- Head before final state-only update: see the final commit of the PR3 branch.

## Shipped PR1 scope

- Envelope-first/per-record `package-decision.v1` reader with closed decision
  vocabulary (`admit`, `reject`, `revoke`, `quarantine`), safe bounded fields,
  duplicate/conflict handling, whole-input limits, and closed input gaps.
- Shared `sha256-canonical-json-v1` helper used by package decisions and SQL
  validation without changing SQL validation canonical bytes; supplied record
  digests use constant-time comparison.
- Active `package.decision.record.v1` and `package.decision.correlation.v1`
  catalog entries with bounded limitations.
- Read-only/in-memory `tracemap package-decision` single-index command,
  deterministic Markdown/JSON outputs, default source label, selectors/caps,
  separate rung sections/counts, provenance, coverage, and focused review rows.
- Fixed rung order and exact-string versions: ExactArtifactMatch,
  ArtifactDigestMismatch, PossibleNameVersionMatch, AmbiguousIdentity,
  ExcludedSource, and UnknownAnalysisGap. Stale scan is an overlay only.
- Honest `LockfileDigestUnavailable` and `DirectTransitiveUnavailable` gaps
  before digest-capable adapter slices land.
- `--exit-code` returns nonzero only for an exact row tied to an external
  `reject` or `revoke`; quarantine, admit, possible, ambiguous, mismatch,
  exclusion, and unknown rows never trigger it.

## Shipped grouped PR2 scope

- Combined indexes and repeatable `--index`/`--label` inputs are expanded
  in-memory through the existing combined package-config projection, retaining
  container/original source labels, source identity, coverage, and selectors.
- Portfolio manifest v1.0 inputs reuse its relative-path and identity-hint
  contract. Duplicate source identity and unknown commits are explicit
  `UnknownAnalysisGap` coverage, never exclusion.
- TypeScript/npm parses package-lock.json v2/v3 offline. Lockfile facts carry
  resolved versions, lockfile path/hash, host-only registry origin, the
  registry-declared sha512 integrity value, direct/transitive relation, and
  proven dependency path depth. No package content is fetched or verified.
- `--include-paths` and `--include-reverse` attach bounded existing graph
  inventory context with dedicated statuses and preserved truncation/gaps;
  context never upgrades an exact/possible/mismatch/ambiguous rung.

## Shipped grouped PR3 scope (this branch, slices 5–6)

- NuGet: `ProjectFileReader.ReadNuGetLockfiles` parses checked-in
  `packages.lock.json` offline (schema versions 1 and 2) with a sequential
  `Utf8JsonReader` pass that records exact per-entry line spans and a
  deterministic `sha256(lockfile-bytes)[0..32]` lockfile hash. ScanEngine
  emits `PackageReferenced` lockfile rows with `ecosystem=nuget`,
  `sourceKind=lockfile`, `manifestKind=packages.lock.json`, `resolvedVersion`,
  `lockfilePath`, `lockfileHash`, `dependencyRelation`
  (`Direct`/`Transitive` from the lockfile's own `type` field only; any other
  or missing type is `unknown`), per-target-framework rows preserved
  distinctly (runtime-suffixed groups included), bounded safe
  `dependencyNames`/`dependencyCount` from entry `dependencies` maps, and the
  `nuget-lockfile/0.1.0` extractor identity under `project.file.v1`
  (Tier2Structural). Malformed, truncated, unsupported-schema, unsafe-name,
  unsafe-target-framework, and resolved-missing shapes emit rule-backed
  `AnalysisGap` facts (`packages-lock-parse`, `packages-lock-unsupported`,
  `packages-lock-group-unsupported`, `packages-lock-entry-unsafe`,
  `packages-lock-entry-resolved-missing`, `packages-lock-read-failed`);
  unsafe resolved versions are hashed (`versionHash` +
  `unsafe-package-version` redaction) instead of emitted. NuGet lockfile
  `contentHash` is never read into evidence: package-decision therefore
  produces `PossibleNameVersionMatch` with `matchBasis=resolved-version` plus
  `LockfileDigestUnavailable` and never `ExactArtifactMatch`. The
  `PackagesLockPresent` build-environment presence diagnostic is unchanged.
- Swift: the existing pure lockfile projectors (no second parser) promote safe
  literal resolved versions to `resolvedVersion`: `Package.resolved` v1/v2
  `state.version`, `Podfile.lock` PODS parenthesized versions (DEPENDENCIES
  rows never resolve), and `Cartfile.resolved` semver literals. Revisions,
  locations, and URLs stay hashed; branch-only/revision-only/unsafe versions
  stay hashed with no `resolvedVersion`. `Podfile.lock` SPEC CHECKSUMS are
  captured only as explicitly labeled `specChecksum` +
  `specChecksumKind=podspec-sha1` metadata (validated 40-hex; non-hex values
  emit a `swift-dependency-lockfile-checksum-unusable` gap) and are never
  mapped to `artifactDigest`. Unsupported `Package.resolved` schema versions
  keep their existing gap behavior.
- End-to-end consumption seam: `PackageDecisionCorrelation` admits
  `SwiftDependencyLockfileEntryDeclared` facts through a minimal internal
  projection (`ecosystem=swift` plus `packageName` from
  `normalizedDependencyIdentity` when safe) next to native
  `PackageReferenced` rows. This is a read-side seam inside the correlation
  engine; no facts are duplicated and the Swift public fact vocabulary is
  unchanged. Swift evidence produces `PossibleNameVersionMatch`
  `resolved-version` rows carrying the Swift lockfile rule IDs and tiers, with
  `LockfileDigestUnavailable`/`DirectTransitiveUnavailable` gaps and no
  `ExactArtifactMatch`.
- Fixtures: `samples/package-decisions/nuget-lock-fixture/` (lockfile +
  csproj + decision records) and `samples/package-decisions/swift-possible.json`
  against the refreshed `samples/swift-dependency-surfaces` scan (its SPEC
  CHECKSUMS now use synthetic 40-hex values so `specChecksum` capture is
  demonstrated; the duplicate-checksum gap fixture shape is preserved).

## Owner decisions recorded

- `quarantine` is accepted as an externally supplied non-terminal state and is
  never TraceMap enforcement or authority.
- `--exit-code` is exact reject/revoke only.
- npm `package-lock.json` remains the first digest-capable adapter (grouped PR2).
- Command name is `package-decision`.
- PR3 keeps NuGet and Swift digest-ineligible by construction: NuGet
  `contentHash`, podspec checksums, revisions, and lockfile hashes are never
  `artifactDigest`.

## Validation

PR3 validation run on this branch:

- `dotnet restore src/dotnet/TraceMap.sln`
- `dotnet build src/dotnet/TraceMap.sln --no-restore`
- `dotnet test src/dotnet/TraceMap.sln --no-restore` (full suite)
- Focused `dotnet test` filters: `FullyQualifiedName~PackageDecision`,
  `FullyQualifiedName~ScanEngine`, package surface/combine/path/reverse
  suites (subsumed by the full run and re-run individually).
- `swift build --package-path src/swift`
- `swift test --package-path src/swift`
- `swift run --package-path src/swift tracemap-swift-smoke-tests`
- `swift run --package-path src/swift tracemap-swift scan --repo
  samples/swift-dependency-surfaces --out
  /tmp/tracemap-swift-dependency-surfaces-pr3`
- `python3 scripts/validate-adapter-artifacts.py
  /tmp/tracemap-swift-dependency-surfaces-pr3`
- Package-decision synthetic CLI smokes documented in `docs/VALIDATION.md`
  (NuGet lockfile fixture scan + correlation; Swift composed-consumer
  correlation), including exit-code, no-leak, and repeat-run determinism
  checks.
- `./scripts/check-private-paths.sh`; `git diff --check`; targeted
  `dotnet format` verification for changed C# files.

## Limitations and deferred work

- NuGet lockfile `contentHash` is package-content metadata, not a registry
  artifact digest; NuGet evidence can never produce `ExactArtifactMatch`.
- SwiftPM versions, CocoaPods versions and podspec checksums, and Carthage
  versions do not prove downloaded artifact bytes; Swift evidence can never
  produce `ExactArtifactMatch`. `specChecksum` is lineage metadata only.
- Direct/transitive for Swift remains unproven (no `dependencyRelation` on
  Swift facts); the correlation reports the explicit
  `DirectTransitiveUnavailable` gap.
- PR3 intentionally does not implement slices 7–13: Python and JVM lockfile
  evidence, before/after artifact replacement, advisory profiles, deployment
  references, docs/acceptance closure, and the final capability-matrix
  refresh remain grouped PR4–PR5 work and must not be checked off here.
- Task 4.4 remains open exactly as recorded in PR2: the pinned
  `scip-typescript` commit has no `package-lock.json` and the shared artifact
  validator reported pre-existing unrelated absolute-path findings; PR3 did
  not revisit it.
- Path/reverse context is static graph evidence only and does not prove
  runtime reachability or enforcement.
