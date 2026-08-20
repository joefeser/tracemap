# Package Admission and Revocation Correlation Implementation State

## Status

Grouped implementation PR1, PR2, and PR3 are merged into `dev`. The grouped
PR4 scope (Python and JVM lockfile resolved evidence, slices 7–8) is shipped
on this branch. This state note is the resume point for the shipped
record/correlation core, combined/portfolio/npm/path work, the NuGet + Swift
resolved-evidence slices, and the Python + JVM adapter evidence slices.

## Current implementation branch

- Branch: `codex/package-decision-python-jvm-evidence`
- Base: `origin/dev` at `7da398abbc723870043abdd06d659d14969154fc` (PR #701)
- Target: `dev`
- Delivery: ready-for-review PR, `Part of #690` (never draft)
- Head before final state-only update: see the final commit of the PR4 branch.

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

## Shipped grouped PR3 scope (slices 5–6)

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

## Shipped grouped PR4 scope (this branch, slices 7–8)

- Python: `tracemap_py/lockfiles.py` parses checked-in `uv.lock` (format
  version 1) and `poetry.lock` (lock-versions 1.0/1.1/2.0/2.1) offline with
  stdlib `tomllib`. Registry-source entries emit `PackageReferenced` rows
  (`ecosystem=python`, `sourceKind=lockfile`, `manifestKind`, safe normalized
  name, exact `resolvedVersion`, `lockfilePath`, 32-hex `lockfileHash`, and
  host-only `registryOrigin` for uv registry URLs) under
  `python.package.metadata.v1` (Tier2Structural) with extractor identity
  `PythonLockfileExtractor`/`python-lockfile/0.1.0`. Evidence spans anchor
  each entry's `[[package]]` header line when the header-line scan matches
  the parsed entry count and fall back to the file anchor otherwise (for
  example a `[[package]]` literal inside a TOML multiline string). The uv
  root project entry emits no dependency row. Gap facts (rule
  `python.package.metadata.v1`): `python-lock-parse` (malformed/truncated
  TOML), `python-lock-unsupported` (unknown format version),
  `python-lock-entry-unsafe` (unsafe/non-literal names),
  `python-lock-entry-resolved-missing`, `python-lock-entry-source-unsupported`
  (git/url/path/directory sources), plus per-lockfile
  `LockfileDigestUnavailable` and (when unproven)
  `DirectTransitiveUnavailable` capability gaps. Both limitations are also
  recorded in the manifest gap collector so the report and fact stream agree.
  Unsafe resolved versions are
  hashed (`versionHash` + `redactionReason=unsafe-package-version`) instead
  of emitted.
- Python Pipfile fix (task 7.2): an inventoried `Pipfile` now emits an
  `AnalysisGap` with `gapKind=unsupported-metadata` plus a collector log
  entry instead of silently disappearing. `Pipfile.lock` remains
  un-inventoried and is documented as unsupported in the catalog limitations.
- JVM: `GradleLockfileExtractor` parses checked-in `gradle.lockfile` rows
  (`group:artifact:version=configuration[,configuration...]`) offline.
  Inventory now includes `gradle.lockfile` (kind `GradleLockfile`, also
  passed by the java/kotlin language filters). Rows emit `PackageReferenced`
  facts (`ecosystem=gradle`, `sourceKind=lockfile`,
  `manifestKind=gradle.lockfile`, `packageName=group:artifact`, exact
  `resolvedVersion`, `lockfilePath`, 32-hex `lockfileHash`, per-row line
  spans) under `jvm.buildfile.v1` (Tier2Structural) with extractor identity
  `GradleLockfileExtractor`/`jvm-gradle-lockfile/0.1.0`. The format proves
  neither digests nor direct/transitive, so every parsed lockfile emits
  `LockfileDigestUnavailable` and `DirectTransitiveUnavailable` capability
  gap facts, records both in the manifest gap collector, and carries no
  `dependencyRelation`/`artifactDigest` on package rows.
  Collector gaps (coverage-downgrading, matching existing JVM vocabulary):
  `GradleLockParseFailed`, `GradleLockRowMalformed` (unparseable rows,
  missing version, unsafe coordinates, or empty/malformed configuration
  lists), and `GradleLockRowUnsupported` (the
  Gradle `empty=` placeholder notation). Unsafe versions hash like existing
  JVM rows. `gradle/verification-metadata.xml` is inventoried through its
  pre-existing generic `XmlConfig` treatment only and is never consumed for
  digests (see owner decisions).
- Maven: every scanned `pom.xml` emits a `MavenLockfileUnavailable`
  capability gap fact under `jvm.buildfile.v1` and the matching manifest gap;
  Maven rows keep their existing declared build-file evidence and
  the correlation engine reports `LockfileDigestUnavailable`/
  `DirectTransitiveUnavailable` per maven pairing.
- End-to-end composition reuses the existing `PackageReferenced` projection
  unchanged: no new fact types, no correlation-engine semantics, and no
  parallel matching were added. Python and gradle lockfile rows correlate as
  `PossibleNameVersionMatch` with `matchBasis=resolved-version`; changing a
  record's digest value never changes the rung (no evidence digest exists to
  mismatch), and Python/JVM evidence can never produce `ExactArtifactMatch`.
- Record-reader fix required for JVM coordinates: the `package-decision.v1`
  package-name pattern now admits hyphens (see owner decisions). Without it,
  every real-world Maven/Gradle artifact name (`spring-web`,
  `fixture-lib`) was rejected at admission.
- Fixtures: `samples/package-decisions/python-lock-fixture/` (pyproject +
  uv.lock + decision records, with the revoke record's sha256 deliberately
  equal to the synthetic sdist hash to prove possible-only correlation) and
  `samples/package-decisions/gradle-lock-fixture/` (settings.gradle +
  gradle.lockfile + decision records). `docs/VALIDATION.md` gained the PR4
  section with the pinned Python and Gradle smoke commands.

## Owner decisions recorded

- `quarantine` is accepted as an externally supplied non-terminal state and is
  never TraceMap enforcement or authority.
- `--exit-code` is exact reject/revoke only.
- npm `package-lock.json` remains the first digest-capable adapter (grouped PR2).
- Command name is `package-decision`.
- PR3 keeps NuGet and Swift digest-ineligible by construction: NuGet
  `contentHash`, podspec checksums, revisions, and lockfile hashes are never
  `artifactDigest`.
- PR4 digest eligibility (slice 7 evaluation): Python `uv.lock` and
  `poetry.lock` hashes are never `artifactDigest`. Both formats carry hashes
  per artifact form (each wheel and the source distribution separately), and a
  `package-decision.v1` record does not identify an artifact form, so no
  digest equality could prove the same artifact. Wheel/sdist hashes are not
  emitted in any property; lineage is bounded to `lockfilePath` plus the
  deterministic 32-hex lockfile content hash, with a per-lockfile
  `LockfileDigestUnavailable` capability gap.
- PR4 relation evidence: `dependencyRelation` is emitted only where proven.
  `uv.lock` proves it from complete, well-typed main/development-group/
  optional-group declarations on the root or a declared workspace entry.
  Version and registry qualifiers select a direct dependency only when they
  uniquely identify one locked registry package; ambiguous same-name rows stay
  unclassified with a capability gap. An unrelated editable source is an
  explicit unsupported-source gap. `poetry.lock` cannot prove the relation
  from the lockfile, so direct/transitive is derived only by cross-referencing
  complete sibling `pyproject.toml` project, optional, Poetry main, legacy
  development, and named-group declarations (the design §5.1
  "direct = declared in a root manifest" rule); when neither proof exists the
  property is omitted and a `DirectTransitiveUnavailable` gap is emitted.
  Monorepo manifests are never unioned across lockfiles. TOML-valid but
  schema-invalid pyproject sections fail closed without aborting the scan, and
  Poetry Git/path/URL/directory sources emit unsupported-source gaps rather
  than registry-style package evidence.
- PR4 digest eligibility (slice 8 evaluation): `gradle/verification-metadata.xml`
  is NOT consumed for digests. Parsing could be bounded offline, but a
  component typically carries several artifacts (module jar, POM, sources,
  classifiers) each with its own SHA-256, competing digest origins exist, and
  the decision record cannot identify the artifact form, so no unambiguous
  artifact-form match is possible. The file stays in its existing generic
  `XmlConfig` inventory treatment; `gradle.lockfile` resolved versions ship
  with `LockfileDigestUnavailable` and the digest work stays deferred.
- PR4 Maven: Maven has no standard lockfile; every scanned `pom.xml` emits a
  `MavenLockfileUnavailable` capability gap fact and matching
  manifest-visible gap; Maven rows keep declared build-file
  evidence only.
- PR4 record admission fix: the `package-decision.v1` reader's package-name
  pattern now admits hyphens in non-leading positions (`left-pad`,
  `flask-sqlalchemy`, `org.springframework:spring-web`) and digit-leading
  distribution names. The previous pattern
  rejected every hyphenated Maven/Gradle artifact and hyphenated Python/npm
  name at admission even though design §6.1's normalization contract expects
  them. The widening is additive (no previously admitted shape is rejected;
  leading hyphens, slashes, multi-colon, and URL shapes still fail closed as
  `DecisionInputIdentityUnsafe`).

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

Exact-head review remediation additionally proved that malformed NuGet
lockfiles downgrade the manifest to reduced/partial coverage, unsafe resolved
values are accepted only by a bounded NuGet-version allowlist and retain only
normalized hash evidence, and Carthage prerelease/build-qualified literals use
the same bounded Swift resolved-version predicate. The focused NuGet/package
decision suite passed 7 tests, the full .NET suite passed 1,637 tests, the
Swift build and smoke executable passed, and the private-path, formatting, and
diff guards passed. Local `swift test` remains unavailable in this shell
because its active toolchain cannot load `XCTest`; exact-head Swift CI is the
platform test authority for this branch.

The subsequent exact-head CocoaPods review pass added two fail-closed cases:
PODS parent rows may end in the standard dependency-list colon without losing
their bounded resolved version, while repeated SPEC CHECKSUMS names discard
all competing values rather than selecting an arbitrary checksum. The Swift
build and adversarial smoke executable passed with both shapes.

## Validation (PR4)

PR4 validation run on this branch:

- `python3 -m venv /tmp/tracemap-python-venv` &&
  `/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"`
  (installed from this worktree) &&
  `/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests` —
  47 passed (33 pre-existing plus 14 lockfile/Pipfile tests).
- `PYTHON_BIN=/tmp/tracemap-python-venv/bin/python
  ./scripts/smoke-python-endpoints.sh` — completed with the endpoint report.
- `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
  gradle -p src/jvm test` — 35 tests passed (27 pre-existing plus 8
  gradle.lockfile/Maven-gap tests); `gradle -p src/jvm installDist` built the
  scanner distribution.
- JVM smokes: `tracemap-jvm scan` over `samples/jvm-modern-sample` (artifact
  validation passed; `Level1SemanticAnalysisReduced` only because the working
  tree had uncommitted changes — the clean-repo integration test still asserts
  `Level1SemanticAnalysis`/`Succeeded`, proving the new
  `MavenLockfileUnavailable` fact does not downgrade coverage) and over
  `samples/package-decisions/gradle-lock-fixture` (3 lockfile rows, both
  capability gaps); `python3 scripts/validate-adapter-artifacts.py` passed for
  both outputs.
- Python fixture smoke: `samples/package-decisions/python-lock-fixture`
  copied into a temp git repo, scanned with the venv adapter (3 resolved
  versions, direct×2/transitive×1, `registryOrigin=pypi.org`), artifacts
  validated, then correlated with `tracemap package-decision --exit-code`:
  exit 0, `requests` (revoke record whose sha256 equals the lockfile's
  synthetic sdist hash) and `urllib3` correlate only as `resolved-version`
  possible matches, the pyproject `requests>=2.31.0` range row stays
  ambiguous, `LockfileDigestUnavailable` reported, no evidence
  `artifactDigest`, and the repeated run is byte-identical (JSON and
  Markdown).
- Gradle fixture smoke: `tracemap package-decision --exit-code` over the
  fixture scan: exit 0, both records correlate only as `resolved-version`
  possible matches under `jvm.buildfile.v1`/`GradleLockfileExtractor` with
  `LockfileDigestUnavailable` + `DirectTransitiveUnavailable` gaps and
  unknown relations; repeated run byte-identical.
- `dotnet build src/dotnet/TraceMap.sln` — 0 errors/warnings;
  `dotnet test src/dotnet/TraceMap.sln` — 1,641 passed (1,637 pre-existing
  plus 4 new); focused
  `dotnet test --filter "FullyQualifiedName~PackageDecision|FullyQualifiedName~ScanEngine"`
  — 50 passed.
- `python3 scripts/test_validate_adapter_artifacts.py` — OK.
- `./scripts/check-private-paths.sh` — passed; `git diff --check` — clean.
- Formatting: `dotnet format --verify-no-changes` reports 254 pre-existing
  whitespace errors in `TraceMap.SqlValidation` and elsewhere, identical with
  this branch's changes stashed, and none in the changed files; no Python or
  Java formatter is configured in this repository (pytest and `gradle test`
  are the respective gates).
- Local `swift test` availability was not needed for PR4 (no Swift changes).

Exact-head review remediation added six adversarial Python cases: each Poetry
lockfile now uses only its sibling manifest in a monorepo; malformed but valid
pyproject table shapes cannot abort extraction; non-registry Poetry sources
fail closed while bounded legacy registries retain only host origin; uv root
relations require a well-typed dependency list; unrelated editable sources
emit gaps; and only root or declared, non-excluded uv workspace entries are
skipped as local project identity. The Python suite passed 53/53, the endpoint
smoke completed, the focused package-decision/rule-catalog suite passed 28/28,
the full .NET suite passed 1,641/1,641, and private-path/diff guards passed.

The next exact-head review remediation added five fail-closed declaration
fixtures: uv grouped development dependencies, qualifier-selected same-name
versions, ambiguous unqualified same-name versions, Poetry named groups, and
malformed Poetry group tables. Relation evidence is now emitted only after the
entire recognized declaration surface is structurally complete; ambiguity or
dynamic/incomplete declarations retain the package evidence but omit the
relation and emit `DirectTransitiveUnavailable`.

A subsequent exact-head consistency batch keeps declared Poetry packages
unclassified when more than one eligible same-name lock row exists, records
Python and Gradle capability limitations in both facts and manifest gap
collectors, rejects Gradle rows with empty or malformed configuration lists,
and aligns decision admission with digit-leading names emitted by the Python
adapter.

The final consistency fixes validate Poetry dependency values before declaring
the manifest surface complete, classify invalid registry ports as unsupported
sources without discarding the rest of a lockfile, and record Maven's existing
lockfile-unavailable limitation in the manifest collector as well as facts.

## Limitations and deferred work

- NuGet lockfile `contentHash` is package-content metadata, not a registry
  artifact digest; NuGet evidence can never produce `ExactArtifactMatch`.
- SwiftPM versions, CocoaPods versions and podspec checksums, and Carthage
  versions do not prove downloaded artifact bytes; Swift evidence can never
  produce `ExactArtifactMatch`. `specChecksum` is lineage metadata only.
- Direct/transitive for Swift remains unproven (no `dependencyRelation` on
  Swift facts); the correlation reports the explicit
  `DirectTransitiveUnavailable` gap.
- Python `uv.lock`/`poetry.lock` wheel and source-distribution hashes are
  artifact-form specific and are never emitted as `artifactDigest`; Python
  evidence can never produce `ExactArtifactMatch`. uv and Poetry transitive
  labels require complete root declaration surfaces; same-name uv variants
  require unique version/registry qualification before any direct label.
- JVM `gradle.lockfile` has no digests and no direct/transitive proof;
  `gradle/verification-metadata.xml` digest correlation stays deferred until a
  decision-record contract can identify the artifact form (jar/POM/sources/
  classifier). JVM evidence can never produce `ExactArtifactMatch` today.
- `Pipfile.lock` is not inventoried or parsed; `Pipfile` is inventoried with
  an explicit `unsupported-metadata` gap only.
- PR4 intentionally does not implement slices 10–13: before/after artifact
  replacement, advisory profiles, deployment references, docs/acceptance
  closure, and the final capability-matrix refresh remain grouped PR5 work
  and must not be checked off here.
- Task 4.4 remains open exactly as recorded in PR2: the pinned
  `scip-typescript` commit has no `package-lock.json` and the shared artifact
  validator reported pre-existing unrelated absolute-path findings; PR3 and
  PR4 did not revisit it.
- Path/reverse context is static graph evidence only and does not prove
  runtime reachability or enforcement.
