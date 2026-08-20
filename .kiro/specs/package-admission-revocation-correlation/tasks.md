# Package Admission and Revocation Correlation Tasks

Joe authorized implementation after the specification merged in #698. The 13
independently testable slices remain grouped into five delivery PRs below; a
checkbox is marked only when that slice's scoped work is shipped.

Each slice must, at minimum: run its listed validation commands, follow `docs/VALIDATION.md` for any adapter change, update `rules/rule-catalog.yml` before emitting any new row type, keep outputs deterministic, and stop at its stop conditions instead of improvising.

## Slice 0 — Specification (this PR)

- [x] 0.1 Merge this specification (requirements.md, design.md, tasks.md, review-prompts.md, implementation-state.md) targeting `dev`.
- [x] 0.2 Owner records decisions for design.md §14 items 1, 2, 3, and 8 in the spec (or in implementation-state.md) before slice 1 starts.

Validation: `./scripts/check-private-paths.sh`; `git diff --check`; diff limited to `.kiro/specs/package-admission-revocation-correlation/`.

## Slice 1 — Decision record reader and admission gaps

- [x] 1.1 Implement `PackageDecisionRecordReader` with envelope-first validation, closed-set input classifications, duplicate/conflict policy, and whole-input limits.
- [x] 1.2 Compute canonical record digests with the shared `sha256-canonical-json-v1` helper and verify optional self-attested digests with constant-time comparison.
- [x] 1.3 Add active `package.decision.record.v1` catalog entry with limitations before emission.
- [x] 1.4 Add focused reader tests for accepted records, duplicate policy, determinism, and adversarial-value redaction; remaining exhaustive matrix assertions are retained as follow-up test hardening.
- [x] 1.5 Reader is exercised through tests and the PR1 CLI composition.

Validation: `dotnet build src/dotnet/TraceMap.sln`; `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if reusing the sql-validation canonicalizer would change its existing digest behavior, copy the routine behind a new shared helper instead of modifying the live one, and record the decision in implementation-state.md.

## Slice 2 — Correlation engine and single-index command

- [x] 2.1 Implement the single-index correlation engine with normalization, exact-string versions, fixed rung ordering, digest comparison, origin notes, and staleness overlay.
- [x] 2.2 Implement `tracemap package-decision --decision <file> --index <index.sqlite> --out <path>` with deterministic Markdown/JSON, default source label, separated rung counts, focused-review rows, and fixed limitations.
- [x] 2.3 Add active `package.decision.correlation.v1` catalog entry with limitations.
- [x] 2.4 Implement owner-approved `--exit-code`: only exact external reject/revoke rows return nonzero.
- [x] 2.5 Emit `LockfileDigestUnavailable` and `DirectTransitiveUnavailable` capability gaps when the selected evidence lacks those capabilities.
- [x] 2.6 Add synthetic possible/exact, mismatch injection, quarantine, privacy, determinism, CLI, and exit-code coverage; exhaustive fixture matrix expansion remains a follow-up within PR1 review hardening.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; CLI smoke against a `samples/` scan output; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: none expected. If exact/possible separation cannot be preserved through some summary path, stop and fix the summary rather than merging counts.

## Slice 3 — Combined index and portfolio manifest inputs (PR2)

- [x] 3.1 Accept combined indexes and repeatable `--index`/`--label` pairs; expand `index_sources` preserving container/original labels; reuse the package-config surface projection path that `package-impact` uses.
- [x] 3.2 Accept `--manifest <portfolio.json>` (v1.0 format, existing label/path/identity-hint rules); portfolio-style source rows with coverage status; duplicate-identity → `UnknownAnalysisGap`; unknown commit SHA → `UnknownAnalysisGap`.
- [x] 3.3 Fixture F9 (two-repo portfolio) with committed expected-output assertions; per-source excluded-vs-gap coverage behavior tests; `--source`, `--ecosystem`, `--decision-id`, `--classification` selectors.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; focused portfolio regression `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PortfolioReport"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if portfolio manifest reuse requires changing the portfolio reader's public behavior, stop and propose an additive local reader instead of mutating portfolio semantics.

## Slice 4 — npm lockfile artifact identity (first digest-capable adapter, PR2)

- [x] 4.1 Extend the TypeScript adapter with `package-lock.json` (v2/v3) extraction emitting `PackageReferenced` rows with `sourceKind=lockfile`, `resolvedVersion`, `lockfilePath`, `lockfileHash`, `registryOrigin` (host-only from `resolved`), `artifactDigestAlgorithm=sha512-base64`, `artifactDigest`, `dependencyRelation` (direct = also declared in `package.json`; transitive = lockfile-only), `dependencyPathDepth` where recorded.
- [x] 4.2 Update `typescript.package.v1` catalog entry (emits + limitations: lockfile is checked-in metadata; integrity is the registry-declared tarball integrity, not content verified by TraceMap; no solving) and `docs/VALIDATION.md` smoke entries.
- [x] 4.3 End-to-end fixtures F1 (exact + direct), F2 (revoked artifact B), F4 (transitive), F6 (missing integrity → gap + possible), F3 (same name/version, different digest → `ArtifactDigestMismatch`): prove the rung changes when only the digest changes.
- [ ] 4.4 Adapter validation per `docs/VALIDATION.md`: `npm run check --prefix src/typescript`; pinned TypeScript smokes; `python3 scripts/validate-adapter-artifacts.py <scan-output>`; combined combine/report/paths smoke. The focused adapter suite and checked-in lockfile fixture pass. The pinned `scip-typescript` scan completes, but that pinned commit has no `package-lock.json`, and artifact validation currently reports pre-existing absolute-path findings in unrelated facts; see `implementation-state.md`. Keep this task open until a reviewed pinned npm-lockfile smoke exists and the shared validator passes. PR5 re-verified both blockers against the pinned commit `891eb4293709a6a587bf4468dfa1b45a85182fd9`: a fresh scan yields zero lockfile-sourced `PackageReferenced` rows (the repo ships `yarn.lock`, not `package-lock.json`) and the shared validator exits 1 with nine `local-absolute-path` findings in unrelated semantic facts.

Validation: adapter matrix commands above plus `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision"`; `./scripts/check-private-paths.sh`; `git diff --check`.

Stop conditions: if npm lockfile parsing cannot be deterministic offline for v2/v3 shapes (for example embedded workspace graphs), restrict to the documented subset, emit `AnalysisGap` for the rest, and record the subset in implementation-state.md. Do not shell out to npm.

## Slice 5 — NuGet `packages.lock.json` resolved versions

- [x] 5.1 Parse `packages.lock.json` content (today presence-only): emit `PackageReferenced` with `sourceKind=lockfile`, `resolvedVersion`, `lockfilePath`, `lockfileHash`, `dependencyRelation` (direct = `DependencyType=Direct`/root project references per lock format; transitive otherwise). NuGet lockfiles carry no artifact content hash: always emit `LockfileDigestUnavailable` capability gap for nuget.
- [x] 5.2 Update `project.file.v1` catalog entry and `docs/VALIDATION.md`.
- [x] 5.3 Tests: resolved-version possible matches; direct/transitive from lock rows; digest-unavailable gap; presence-diagnostics unchanged.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~PackageDecision|FullyQualifiedName~ScanEngine"`; `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 6 — Swift resolved identities (no artifact digests)

- [x] 6.1 Promote safe literal resolved versions from `Package.resolved` pins and Podfile.lock version lines into `resolvedVersion` on the existing lockfile facts; keep revision/location values hashed; capture `specChecksum` (podspec checksum) as its own labeled property with the documented never-an-artifact-digest limitation.
- [x] 6.2 Update `swift.dependency.lockfile.*` catalog entries and `docs/VALIDATION.md` Swift smokes (`tracemap-swift-smoke-tests`, `samples/swift-*` runs).
- [x] 6.3 Tests: possible matches with `resolved-version` basis; no exact match ever claimed from Swift evidence; schema v3 gap unchanged.

Validation: Swift matrix from `docs/VALIDATION.md` (build, smoke tests, sample scans, `validate-adapter-artifacts.py`); `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 7 — Python lockfile resolved versions

- [x] 7.1 Implement `uv.lock` and/or `poetry.lock` parsing per the resolved owner decision (design §14 item 5): `resolvedVersion`, lockfile identity, `dependencyRelation`; treat source-distribution hashes per the decision (exact-eligible only if the record's digest form matches; otherwise `matchBasis` context).
- [x] 7.2 Fix the silent `Pipfile` skip: emit an `AnalysisGap` (`unsupported-metadata`) instead of silence, per the no-silence rule.
- [x] 7.3 Update `python.package.metadata.v1` catalog entry; validation via temp venv pytest (`python3 -m venv /tmp/tracemap-python-venv && .../pip install -e "src/python[dev]" && pytest src/python/tests`) plus `scripts/smoke-python-endpoints.sh` and `validate-adapter-artifacts.py`.

Stop conditions: if lock hash field semantics cannot be verified deterministically offline, ship resolved versions only and record the digest question as an open item.

## Slice 8 — JVM resolved versions and (conditionally) verification metadata

- [x] 8.1 Parse `gradle.lockfile` resolved versions and, if the slice-8 evaluation confirms determinism, `gradle/verification-metadata.xml` artifact SHA-256s as `artifactDigest`; Maven has no standard lockfile — document and emit the capability gap.
- [x] 8.2 Update `jvm.buildfile.v1` catalog entry and JVM validation matrix (Java 21 Homebrew path per AGENTS.md).

Stop conditions: if `verification-metadata.xml` parsing is not deterministic offline (transform-generated formatting variance, for example), ship `gradle.lockfile` resolved versions only, emit the digest capability gap, and record the decision.

## Slice 9 — Optional path and reverse context (PR2)

- [x] 9.1 `--include-paths`/`--include-reverse` over combined inputs reusing the existing graph inventory and bounds; dedicated report sections; path context never upgrades rungs; `TruncatedByLimit` preserved.
- [x] 9.2 Tests: attachment to matched `package-config` surfaces; unavailable/truncated statuses; determinism with path context enabled.

Validation: `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~CombinedDependencyPath|FullyQualifiedName~CombinedReverseQuery|FullyQualifiedName~PackageDecision"`.

## Slice 10 — Before/after artifact replacement

- [x] 10.1 `--before-manifest`/`--after-manifest` comparison mode: per design §6.6; `ArtifactReplaced` exact-label rows only when both sides are digest-bound; possible-only change rows otherwise; cross-snapshot wording.
- [x] 10.2 Fixture F10 with committed expected output; identity-mismatch downgrade per portfolio comparison rules.

Validation: package-decision and portfolio-focused tests; `./scripts/check-private-paths.sh`; `git diff --check`.

## Slice 11 — Advisory profile external claims

- [x] 11.1 `advisory-profile.v1` reader with the closed grammar (`exact`/`any` predicates, `framework-implied-server-surface`, bounded params); rule `package.decision.advisory.v1` (active) with external-claim limitations; dedicated report section; never merged into rungs or facts.
- [x] 11.2 Fixture F12; rejection tests for out-of-grammar claims; severity/CVE-shaped fields rejected by closed schema.

## Slice 12 — Deployment references (runtime-unproven)

- [x] 12.1 `package-deployment-reference.v1` reader per design §4; all rows `RuntimeUnprovenReference` with the fixed limitation; optional commit-SHA join to portfolio sources as provenance only; runtime-load claims rejected.
- [x] 12.2 Fixture F15; exit-code inclusion per requirements 12.3; per the slice-0 owner decision, deployment-reference timing was approved and shipped in PR5 (design §14 item 7 resolved).

## Slice 13 — Docs, acceptance, and closure

- [x] 13.1 Update `docs/ACCEPTANCE.md` with `tracemap package-decision` acceptance criteria; README command list; `docs/VALIDATION.md` full matrix entries for changed adapters.
- [x] 13.2 Final capability matrix refresh in requirements.md (Requirement 10) to reflect shipped adapter slices; implementation-state.md closure note.
- [x] 13.3 Full gate: `dotnet build src/dotnet/TraceMap.sln`; `dotnet test src/dotnet/TraceMap.sln`; `./scripts/check-private-paths.sh`; `git diff --check`; the `docs/VALIDATION.md` required local commands for every touched adapter.

## Deferred follow-ups (not in this spec's slices)

- Derived persistence behind `--write-derived` (portfolio-style house deferral).
- `quarantine` decision kind and effective-decision supersession rollups (pending owner decisions).
- Release-review composition of decision-derived context (separate spec).
- Shared external-evidence envelope refactor with issue #689's admission work (separate spec if both ship).
- Safe-metadata allowlist additions for lockfile properties in `diff`/`reverse` rendering (each report's own change).
