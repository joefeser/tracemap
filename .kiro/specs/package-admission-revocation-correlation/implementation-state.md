# Package Admission and Revocation Correlation Implementation State

## Status

Grouped implementation PR1 and the grouped PR2 scope are authorized by Joe
after specification PR #698 merged. This state note is the resume point for
the shipped record/correlation core plus combined/portfolio/npm/path work.

## Current implementation branch

- Branch: `codex/package-decision-npm-composition`
- Base: `origin/dev` at `f42987ed0801e6a02ab9c626bdbe47dff523eeb7`
- Target: `dev`
- Delivery: ready-for-review PR, `Part of #690` (never draft)
- Head before final state-only update: `497a2eb068081951d03dabceeb2afd5a1730a0be`

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

## Owner decisions recorded

- `quarantine` is accepted as an externally supplied non-terminal state and is
  never TraceMap enforcement or authority.
- `--exit-code` is exact reject/revoke only.
- npm `package-lock.json` remains the first digest-capable adapter (grouped PR2).
- Command name is `package-decision`.

## Validation

- `dotnet restore src/dotnet/TraceMap.sln`
- `dotnet build src/dotnet/TraceMap.sln --no-restore`
- Focused `dotnet test ... --filter FullyQualifiedName~PackageDecision`
- Synthetic single-index CLI tests cover possible, injected exact, digest
  mismatch behavior, privacy, deterministic outputs, quarantine, and exit code.
- SQL validation digest computation delegates to the shared canonical helper;
  existing SQL validation tests remain the regression gate.
- Final full test, format, private-path, and diff checks are required before
  pushing this branch.
- PR2 validation run so far: `dotnet restore src/dotnet/TraceMap.sln`,
  `dotnet build src/dotnet/TraceMap.sln --no-restore`, focused package,
  portfolio, path, and reverse .NET tests (100 passed), and
  `npm run check --prefix src/typescript` (48 passed).
- Final PR2 validation: full `dotnet test src/dotnet/TraceMap.sln` (1,629
  passed), full TypeScript check (48 passed), TypeScript lockfile scan plus
  `scripts/validate-adapter-artifacts.py`, combined/report/package-decision
  CLI smoke with optional context, `./scripts/check-private-paths.sh`, and
  `git diff --check`. Targeted format verification passed for Reporting and
  test files; the existing CLI project reports unrelated whitespace findings
  outside the changed package-decision region.

## Limitations and deferred work

PR2 intentionally does not add NuGet/Swift/Python/JVM lockfile extraction,
before/after comparison, advisory
profiles, deployment references, or adapter capability upgrades. Those are
grouped PR3–PR5 work and must not be checked off here. npm lockfile integrity
is producer/registry-declared metadata, not TraceMap content verification;
workspace/embedded lockfile shapes outside the v2/v3 `packages` map emit an
analysis gap. Path/reverse context is static graph evidence only and does not
prove runtime reachability or enforcement. Before adapter evidence is
available, production package facts remain possible/ambiguous with explicit
capability gaps.
