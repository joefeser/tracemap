# Package Admission and Revocation Correlation Implementation State

## Status

Specification only — not implemented. Issue #690 explicitly does not authorize implementation during current onboarding testing; every task in `tasks.md` is unchecked. This file is the resume point for future implementation contexts.

## Specification PR

- Branch: `codex/spec-package-admission-revocation-correlation`
- Base: `dev` at `5ca869d9f7e8e73191985423b8453633eaac426c`
- Scope: `.kiro/specs/package-admission-revocation-correlation/` only (requirements.md, design.md, tasks.md, review-prompts.md, implementation-state.md). No product code, no rule-catalog changes, no sample changes.
- PR text uses `Part of #690`, not `Closes #690`.

## Scope decisions baked into the spec

- Three new rules (`package.decision.record.v1`, `package.decision.correlation.v1`, `package.decision.advisory.v1`) plus the neutral `package-decision.v1` envelope; no overloading of `package.upgrade.impact.v1`, portfolio, paths, diff, or reverse rules.
- Nine-rung correlation ladder with `ExactArtifactMatch` and `PossibleNameVersionMatch` permanently separate, plus `ArtifactDigestMismatch` as its own rung so same name/version with a different digest never collapses to a match.
- Digests prove integrity, not authority; signature verification stays upstream (88mph/trusted producer); no trust inference from provenance.
- Command `tracemap package-decision`, read-only, in-memory composition (portfolio pattern), no persisted tables in v1, `--exit-code` fires on exact/possible/mismatch/runtime-unproven (owner-confirmable).
- Adapter capability extensions land as per-ecosystem slices (npm first) that update their own rule-catalog limitations and `docs/VALIDATION.md` entries; the capability matrix in requirements.md Requirement 10 is part of the contract.
- Staleness uses record `decisionTimeUtc` vs scan manifest `scannedAt` (non-authoritative, limitation stated) plus optional `--as-of`; staleness never changes a rung.
- Issue #689 is a boundary, not a dependency; 88mph issues #575/#576/#577 own decisions, inspection, and mirror response.

## Open owner decisions (blocking items marked)

From design.md §14:

1. `quarantine` decision kind inclusion — decide before slice 2 (blocking vocabulary).
2. `--exit-code` policy on possible matches — decide before slice 2 (blocking exit semantics).
3. First digest-capable adapter (recommendation: npm `package-lock.json`) — decide before slice 4 (blocking).
4. Gradle `verification-metadata.xml` adoption — decide within slice 8 (stop condition documented).
5. Python lockfile scope and source-hash eligibility — decide at slice 7 with format evidence.
6. Supersession resolution (v1 renders chain context only) — non-blocking.
7. Deployment-reference input timing — non-blocking, slice 12 confirmable.
8. Command naming (`package-decision`) — confirm at slice 1.

## Validation performed for this spec PR

- Research base: `dev` at `5ca869d9f7e8e73191985423b8453633eaac426c` (fresh fetch; matches expected origin/dev).
- No open PR or spec owned #690 before this PR; grep hits for "690" elsewhere were incidental (counts/SHAs/UUIDs).
- Focused regression suites run on the spec branch (no product code changed, so results match `dev`): package-impact, portfolio, combined paths/reverse, package-evidence rule-catalog assertions.
- `./scripts/check-private-paths.sh` and `git diff --check` pass; diff confirmed limited to this spec directory.

## Oddities and notes for future contexts

- No adapter today emits artifact digests, resolved versions (except partial Swift), registry origins, lockfile identity (except partial Swift), or direct/transitive relations — see the audited matrix (requirements.md Requirement 10 / design.md §11). Until adapter slices land, honest correlation output is possible/ambiguous plus capability gaps; tests must assert that, not treat it as failure.
- Podfile.lock SPEC CHECKSUMS are podspec checksums, not artifact digests; the spec forbids using them as `artifactDigest` (design §5.2).
- NuGet `packages.lock.json` has no content hash: nuget can reach `resolved-version` possible matches but never exact ones from lockfile evidence alone.
- The sql-validation canonical JSON digest routine is the model for record digests; slice 1 extracts a shared helper behind a new name rather than modifying the live one (stop condition documented).
- `Pipfile` is currently inventoried but silently ignored by the Python adapter; slice 7 turns that silence into a gap.
- `scannedAt` is a non-authoritative manifest field per `contracts/scan-truth-conformance.v1.json`; the staleness overlay states this limitation in every report.

## Follow-ups

- Resolve blocking owner decisions 1–3 before slice 2/4.
- Implementation proceeds strictly by slices in `tasks.md`; update checkboxes there and this file as slices ship.
- Deferred items live in `tasks.md` "Deferred follow-ups" (persistence, quarantine/effective-decision rollups, release-review composition, shared envelope with #689, safe-metadata allowlist additions).
