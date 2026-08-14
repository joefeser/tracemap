# Implementation state: Cross-adapter scan-truth conformance

- Issue: #664
- Branch: `codex/cross-adapter-scan-truth`
- Base stack head: `4dd38ea7f6628a670f21f36f3daebec861f8e71f`
- Validated integrated checkpoint: `28d6fe9cf59dedb683ffac7f2f91e9df84776657`
- Scope: five shipped adapters only; Go #665 deferred

## Evidence inventory

- All adapters emit the five named artifacts and require a Git commit through
  their current CLI paths.
- .NET currently binds `scanId` to a SHA-256 `sourceSnapshotDigest` and has
  regression coverage for same-size dirty mutation, inaccessible sources,
  mutation during scan, and Unicode-equivalent exclusions.
- JVM, Python, TypeScript, and Swift now persist analyzed-byte digests and bind
  scan identity to those digests, exact commit/repository authority, normalized
  options, and adapter version.
- Coverage vocabularies and semantic ceilings intentionally differ. The profile
  maps their truth posture; it does not rename them into false parity.

## Validation

- Python: 30/30 adapter tests passed in an isolated temporary virtual environment.
- TypeScript: 35/35 tests passed; `npm run build` passed.
- JVM: Gradle test suite passed with Java 21.
- Swift: full smoke-test executable passed.
- .NET: 1,543/1,543 solution tests passed on the integrated #674/#675/#667 stack.
- The full five-adapter executable matrix returned `supported`; its sanitized
  JSON SHA-256 was
  `59f0849c4eff9e1f9271c6949845095f865f330c7ce5c4cc5f7d7fb9f64d1f96`.
- Combined public paths/reverse smoke, private-path guard, Python bytecode
  compilation, and `git diff --check` passed.
- The authoritative matrix was rerun after stacking the completed prerequisite
  branches and reproduced the exact prior five-adapter readiness digest. Python
  remained 30/30, TypeScript 35/35, JVM passed with Java 21, and the Swift
  executable smoke passed.

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
- Each adapter has a deterministic test-only seam proving that a selected source
  mutation after extraction and before final snapshot verification fails before
  publishing a replacement artifact set.
- Include/exclude comparison now follows the host filesystem's Unicode
  equivalence semantics without rewriting persisted evidence paths.
- The matrix covers repeat, same-size dirty mutation, inaccessible input,
  Unicode exclusion, invalid source/reduced analysis, malformed manifest,
  required-artifact transaction, relative-path safety, and persistence stages.
- TypeScript now writes into a sibling staging directory and swaps a completed
  packet into place only after source verification. This fixes the real
  conformance divergence found by the matrix: an inaccessible input previously
  deleted the prior valid output before the failed replacement scan completed.

## Deferred

- Go adapter #665.
- Runtime evidence and semantic parity across compiler toolchains.
- Any protected or real-customer repository validation.
- Filesystem-correct Unicode exclusion is `not-applicable` on hosts that report
  NFC and NFD names as distinct; the harness does not collapse such names.
