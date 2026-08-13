# Implementation state: Cross-adapter scan-truth conformance

- Issue: #664
- Branch: `codex/cross-adapter-scan-truth`
- Base stack head: `d5d5d0960115f160ccb0fdf66f1bc87156afa0a9`
- Scope: five shipped adapters only; Go #665 deferred

## Evidence inventory

- All adapters emit the five named artifacts and require a Git commit through
  their current CLI paths.
- .NET currently binds `scanId` to a SHA-256 `sourceSnapshotDigest` and has
  regression coverage for same-size dirty mutation, inaccessible sources,
  mutation during scan, and Unicode-equivalent exclusions.
- JVM, Python, TypeScript, and Swift currently derive scan IDs from paths/kinds/
  sizes and commit identity without a persisted analyzed-byte digest. They are
  red for the R3 invariant until patched and independently tested.
- Coverage vocabularies and semantic ceilings intentionally differ. The profile
  maps their truth posture; it does not rename them into false parity.

## Validation

- Python: 29/29 adapter tests passed in an isolated temporary virtual environment.
- TypeScript: 34/34 tests passed; `npm run build` passed.
- JVM: Gradle test suite passed with Java 21.
- Swift: full smoke-test executable passed.
- The first executable matrix stage proves Python exact Git authority, persisted
  source digest, repeated-scan determinism, same-size dirty-byte divergence,
  required artifacts, relative paths, and NDJSON/SQLite round trip. The harness
  intentionally returns unsupported while its inaccessible, during-scan
  mutation, Unicode exclusion, reduced-analysis, and malformed-schema stages
  remain not-run.

## Current implementation

- JVM, Python, TypeScript, and Swift manifests now persist a 64-hex
  `sourceSnapshotDigest`; scan IDs include actual selected-byte identity,
  normalized options, commit/repository authority, and adapter version.
- Each adapter recomputes the digest before artifact publication and stops on a
  changed snapshot. Same-size dirty mutations are covered directly in each
  adapter suite.
- Oversized pre-analysis skips are represented by a closed skip marker and
  observed size rather than falsely hashing their bytes as analyzed.
- `validate-adapter-artifacts.py` now rejects missing or malformed snapshot
  digests and continues to prove NDJSON/SQLite fact equivalence.
- `scan-truth-conformance.py` currently implements the deterministic baseline,
  repeat, dirty-mutation, artifact, path-safety, and persistence stages. It must
  remain red until the remaining adversarial stages are executable.

## Deferred

- Go adapter #665.
- Runtime evidence and semantic parity across compiler toolchains.
- Any protected or real-customer repository validation.
