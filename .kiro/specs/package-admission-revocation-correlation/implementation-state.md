# Package Admission and Revocation Correlation Implementation State

## Status

Grouped implementation PR1 is authorized by Joe after specification PR #698
merged. This state note is the resume point for the record reader, deterministic
single-index correlation, CLI, and synthetic fixture work only.

## Current implementation branch

- Branch: `codex/package-decision-correlation-core`
- Base: `origin/dev` at `c6148ee8c8084839c8be97bd89d8e603f077530e`
- Target: `dev`
- Delivery: ready-for-review PR, `Part of #690` (never draft)

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

## Limitations and deferred work

PR1 does not add npm/NuGet/Swift/Python/JVM lockfile extraction, combined or
portfolio inputs, path/reverse consumers, before/after comparison, advisory
profiles, deployment references, or adapter capability upgrades. Those are
grouped PR2–PR5 work and must not be checked off here. Until adapter slices
ship, production package facts cannot produce exact matches and capability gaps
are intentionally emitted.
