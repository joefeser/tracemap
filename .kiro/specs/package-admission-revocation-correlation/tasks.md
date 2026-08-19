# Package Admission and Revocation Correlation Tasks

Issue #690 does not authorize implementation during current onboarding testing. All tasks below are unchecked and stay unchecked until implementation is authorized. Slices are ordered so each lands as an independently testable PR with its own validation gate; no slice depends on unmerged later work.

Each slice must, at minimum: run its listed validation commands, follow `docs/VALIDATION.md` for any adapter change, update `rules/rule-catalog.yml` before emitting any new row type, keep outputs deterministic, and stop at its stop conditions instead of improvising.

## Slice 0 — Specification (this PR)

- [ ] 0.1 Merge this specification (requirements.md, design.md, tasks.md, review-prompts.md, implementation-state.md) targeting `dev`.
- [ ] 0.2 Owner records decisions for design.md §14 items 1, 2, 3, and 8 in the spec (or in implementation-state.md) before slice 1 starts.

Validation: `./scripts/check-private-paths.sh`; `git diff --check`; diff limited to `.kiro/specs/package-admission-revocation-correlation/`.

## Slice 1 — Decision record reader and admission gaps

- [ ] 1.1 Implement `PackageDecisionRecordReader` (new file in `src/dotnet/TraceMap.Reporting/`): envelope-first validation, per-record validation, closed-set input classifications (`DecisionInputSchemaUnsupported`, `DecisionInputMalformed`, `DecisionInputDigestMismatch`, `DecisionInputDecisionKindUnsupported`, `DecisionInputIdentityUnsafe`, `DecisionInputDuplicateConflict`, `DecisionInputLimitReached`, `DecisionInputReadFailed`), duplicate/conflict policy, whole-input limits (200 records, field lengths).
- [ ] 1.2 Compute canonical record digests with the `sha256-canonical-json-v1` routine (reuse `SqlValidationSummary.Canonicalize` semantics; extract a shared helper rather than duplicating) and verify optional self-attested digests with `CryptographicOperations.FixedTimeEquals`.
- [ ] 1.3 Add rule-catalog entries `package.decision.record.v1` (active) with limitations from requirements.md; no row may be emitted before the entry exists.
- [ ] 1.4 Unit tests: every classification triggered; duplicate-identical vs duplicate-conflict; digest mismatch on tampered file; adversarial values (credential URLs, path-shaped names, `git+ssh://user:pass@host` versions) rejected and absent from all outputs; property-order and record-order determinism of digests; rule-catalog presence test.
- [ ] 1.5 No CLI surface yet. The reader is exercised through tests only.

Validation: `dotnet build src/dotnet/TraceMap.sln`; `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if reusing the sql-validation canonicalizer would change its existing digest behavior, copy the routine behind a new shared helper instead of modifying the live one, and record the decision in implementation-state.md.

## Slice 2 — Correlation engine and single-index command

- [ ] 2.1 Implement the correlation engine: per-ecosystem name normalization (design §6.1), exact-string version equality, rung evaluation in fixed order (`ExactArtifactMatch`, `ArtifactDigestMismatch`, `PossibleNameVersionMatch` with `matchBasis`, `AmbiguousIdentity`, `ExcludedSource`, `UnknownAnalysisGap`), origin-mismatch capping, staleness overlay.
- [ ] 2.2 Implement `tracemap package-decision --decision <file> --index <index.sqlite> --out <path>` with Markdown/JSON outputs per design §9, single-index source promotion (`default` label), summary with per-rung counts kept separate, focused-review rendering, fixed limitations text.
- [ ] 2.3 Add rule-catalog entry `package.decision.correlation.v1` (active) with limitations; emitted-rule-to-catalog resolution test.
- [ ] 2.4 Implement `--exit-code` per requirements 12.3 and the resolved owner decision from slice 0.
- [ ] 2.5 Capability gaps: emit `LockfileDigestUnavailable` and `DirectTransitiveUnavailable` gaps for every pairing where the evidence lacks digest/relation capability (which, pre-slice-4, is every ecosystem — assert this honestly in tests).
- [ ] 2.6 Tests: fixtures F1-possible path (literal pin, no digests yet → possible), F3 (digest-mismatch simulation via injected properties), F5 (semver-only stays ambiguous), F7 (git dep hashed, no raw URL), F11 (input gaps), F13 (adversarial), F14 (byte-identical rerun), exit codes, selector and cap behavior, `SelectorNoMatch`/`TruncatedByLimit`.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; CLI smoke against a `samples/` scan output; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: none expected. If exact/possible separation cannot be preserved through some summary path, stop and fix the summary rather than merging counts.

## Slice 3 — Combined index and portfolio manifest inputs

- [ ] 3.1 Accept combined indexes and repeatable `--index`/`--label` pairs; expand `index_sources` preserving container/original labels; reuse the package-config surface projection path that `package-impact` uses.
- [ ] 3.2 Accept `--manifest <portfolio.json>` (v1.0 format, existing label/path/identity-hint rules); portfolio-style source rows with coverage status; duplicate-identity → `UnknownAnalysisGap`; unknown commit SHA → `UnknownAnalysisGap`.
- [ ] 3.3 Fixture F9 (two-repo portfolio) with committed expected-output assertions; per-source excluded-vs-gap coverage behavior tests; `--source`, `--ecosystem`, `--decision-id`, `--classification` selectors.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; focused portfolio regression `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PortfolioReport"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if portfolio manifest reuse requires changing the portfolio reader's public behavior, stop and propose an additive local reader instead of mutating portfolio semantics.

## Slice 4 — npm lockfile artifact identity (first digest-capable adapter)

- [ ] 4.1 Extend the TypeScript adapter with `package-lock.json` (v2/v3) extraction emitting `PackageReferenced` rows with `sourceKind=lockfile`, `resolvedVersion`, `lockfilePath`, `lockfileHash`, `registryOrigin` (host-only from `resolved`), `artifactDigestAlgorithm=sha512-base64`, `artifactDigest`, `dependencyRelation` (direct = also declared in `package.json`; transitive = lockfile-only), `dependencyPathDepth` where recorded.
- [ ] 4.2 Update `typescript.package.v1` catalog entry (emits + limitations: lockfile is checked-in metadata; integrity is the registry-declared tarball integrity, not content verified by TraceMap; no solving) and `docs/VALIDATION.md` smoke entries.
- [ ] 4.3 End-to-end fixtures F1 (exact + direct), F2 (revoked artifact B), F4 (transitive), F6 (missing integrity → gap + possible), F3 (same name/version, different digest → `ArtifactDigestMismatch`): prove the rung changes when only the digest changes.
- [ ] 4.4 Adapter validation per `docs/VALIDATION.md`: `npm run check --prefix src/typescript`; pinned TypeScript smokes; `python3 scripts/validate-adapter-artifacts.py <scan-output>`; combined combine/report/paths smoke.

Validation: adapter matrix commands above plus `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if npm lockfile parsing cannot be deterministic offline for v2/v3 shapes (for example embedded workspace graphs), restrict to the documented subset, emit `AnalysisGap` for the rest, and record the subset in implementation-state.md. Do not shell out to npm.

## Slice 5 — NuGet `packages.lock.json` resolved versions

- [ ] 5.1 Parse `packages.lock.json` content (today presence-only): emit `PackageReferenced` with `sourceKind=lockfile`, `resolvedVersion`, `lockfilePath`, `lockfileHash`, `dependencyRelation` (direct = `DependencyType=Direct`/root project references per lock format; transitive otherwise). NuGet lockfiles carry no artifact content hash: always emit `LockfileDigestUnavailable` capability gap for nuget.
- [ ] 5.2 Update `project.file.v1` catalog entry and `docs/VALIDATION.md`.
- [ ] 5.3 Tests: resolved-version possible matches; direct/transitive from lock rows; digest-unavailable gap; presence-diagnostics unchanged.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision|FullyQualifiedName~ScanEngine"`; `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 6 — Swift resolved identities (no artifact digests)

- [ ] 6.1 Promote safe literal resolved versions from `Package.resolved` pins and Podfile.lock version lines into `resolvedVersion` on the existing lockfile facts; keep revision/location values hashed; capture `specChecksum` (podspec checksum) as its own labeled property with the documented never-an-artifact-digest limitation.
- [ ] 6.2 Update `swift.dependency.lockfile.*` catalog entries and `docs/VALIDATION.md` Swift smokes (`tracemap-swift-smoke-tests`, `samples/swift-*` runs).
- [ ] 6.3 Tests: possible matches with `resolved-version` basis; no exact match ever claimed from Swift evidence; schema v3 gap unchanged.

Validation: Swift matrix from `docs/VALIDATION.md` (build, smoke tests, sample scans, `validate-adapter-artifacts.py`); `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 7 — Python lockfile resolved versions

- [ ] 7.1 Implement `uv.lock` and/or `poetry.lock` parsing per the resolved owner decision (design §14 item 5): `resolvedVersion`, lockfile identity, `dependencyRelation`; treat source-distribution hashes per the decision (exact-eligible only if the record's digest form matches; otherwise `matchBasis` context).
- [ ] 7.2 Fix the silent `Pipfile` skip: emit an `AnalysisGap` (`unsupported-metadata`) instead of silence, per the no-silence rule.
- [ ] 7.3 Update `python.package.metadata.v1` catalog entry; validation via temp venv pytest (`python3 -m venv /tmp/tracemap-python-venv && .../pip install -e "src/python[dev]" && pytest src/python/tests`) plus `scripts/smoke-python-endpoints.sh` and `validate-adapter-artifacts.py`.

Stop conditions: if lock hash field semantics cannot be verified deterministically offline, ship resolved versions only and record the digest question as an open item.

## Slice 8 — JVM resolved versions and (conditionally) verification metadata

- [ ] 8.1 Parse `gradle.lockfile` resolved versions and, if the slice-8 evaluation confirms determinism, `gradle/verification-metadata.xml` artifact SHA-256s as `artifactDigest`; Maven has no standard lockfile — document and emit the capability gap.
- [ ] 8.2 Update `jvm.buildfile.v1` catalog entry and JVM validation matrix (Java 21 Homebrew path per AGENTS.md).

Stop conditions: if `verification-metadata.xml` parsing is not deterministic offline (transform-generated formatting variance, for example), ship `gradle.lockfile` resolved versions only, emit the digest capability gap, and record the decision.

## Slice 9 — Optional path and reverse context

- [ ] 9.1 `--include-paths`/`--include-reverse` over combined inputs reusing the existing graph inventory and bounds; dedicated report sections; path context never upgrades rungs; `TruncatedByLimit` preserved.
- [ ] 9.2 Tests: attachment to matched `package-config` surfaces; unavailable/truncated statuses; determinism with path context enabled.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~CombinedDependencyPath|FullyQualifiedName~CombinedReverseQuery|FullyQualifiedName~PackageDecision"`.

## Slice 10 — Before/after artifact replacement

- [ ] 10.1 `--before-manifest`/`--after-manifest` comparison mode: per design §6.6; `ArtifactReplaced` exact-label rows only when both sides are digest-bound; possible-only change rows otherwise; cross-snapshot wording.
- [ ] 10.2 Fixture F10 with committed expected output; identity-mismatch downgrade per portfolio comparison rules.

Validation: package-decision and portfolio-focused tests; `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 11 — Advisory profile external claims

- [ ] 11.1 `advisory-profile.v1` reader with the closed grammar (`exact`/`any` predicates, `framework-implied-server-surface`, bounded params); rule `package.decision.advisory.v1` (active) with external-claim limitations; dedicated report section; never merged into rungs or facts.
- [ ] 11.2 Fixture F12; rejection tests for out-of-grammar claims; severity/CVE-shaped fields rejected by closed schema.

## Slice 12 — Deployment references (runtime-unproven)

- [ ] 12.1 `package-deployment-reference.v1` reader per design §4; all rows `RuntimeUnprovenReference` with the fixed limitation; optional commit-SHA join to portfolio sources as provenance only; runtime-load claims rejected.
- [ ] 12.2 Fixture F15; exit-code inclusion per requirements 12.3; per the slice-0 owner decision, confirm or defer this slice's timing (design §14 item 7).

## Slice 13 — Docs, acceptance, and closure

- [ ] 13.1 Update `docs/ACCEPTANCE.md` with `tracemap package-decision` acceptance criteria; README command list; `docs/VALIDATION.md` full matrix entries for changed adapters.
- [ ] 13.2 Final capability matrix refresh in requirements.md (Requirement 10) to reflect shipped adapter slices; implementation-state.md closure note.
- [ ] 13.3 Full gate: `dotnet build src/dotnet/TraceMap.sln`; `dotnet test src/dotnet/TraceMap.sln`; `./scripts/check-private-paths.sh`; `git diff --check`; the `docs/VALIDATION.md` required local commands for every touched adapter.

## Deferred follow-ups (not in this spec's slices)

- Derived persistence behind `--write-derived` (portfolio-style house deferral).
- `quarantine` decision kind and effective-decision supersession rollups (pending owner decisions).
- Release-review composition of decision-derived context (separate spec).
- Shared external-evidence envelope refactor with issue #689's admission work (separate spec if both ship).
- Safe-metadata allowlist additions for lockfile properties in `diff`/`reverse` rendering (each report's own change).
