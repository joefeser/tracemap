# Implementation state: Cross-adapter scan-truth conformance

- Issue: #664
- Branch: `codex/cross-adapter-scan-truth`
- Base stack head: `a48ce71d9abb8605ea60db12c84a0835e2146082`
- Validated integrated checkpoint: `cd30744bcd402d6c1ad69ef16397f481089d8050`
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
- .NET: 1,546/1,546 solution tests passed on the integrated #674/#675/#667 stack.
- The final #675 reduced Web Forms packet-coverage propagation is included;
  focused local-review/explorer tests passed 19/19 on the final integrated stack,
  including all exact-head failure-evidence regressions from #675 review.
- The full five-adapter executable matrix returned `supported`; its sanitized
  JSON SHA-256 was
  `59f0849c4eff9e1f9271c6949845095f865f330c7ce5c4cc5f7d7fb9f64d1f96`.
- Combined public paths/reverse smoke, private-path guard, Python bytecode
  compilation, and `git diff --check` passed.
- The authoritative matrix was rerun after stacking the completed prerequisite
  branches and reproduced the exact prior five-adapter readiness digest. Python
  remained 30/30, TypeScript 35/35, JVM passed with Java 21, and the Swift
  executable smoke passed.

Final prerequisite integration on 2026-08-13 includes Web Forms explorer head
`a48ce71d9abb8605ea60db12c84a0835e2146082`. The first matrix invocation through
the system Python truthfully returned `unsupported` because pytest was not
installed in that interpreter; it did not report a product conformance failure.
Following `docs/VALIDATION.md`, the matrix was rerun from an isolated temporary
venv containing `src/python[dev]` and returned `supported` for all five adapters,
reproducing the exact sanitized digest
`59f0849c4eff9e1f9271c6949845095f865f330c7ce5c4cc5f7d7fb9f64d1f96`.

Exact integrated validation then passed: Python 30/30; TypeScript 35/35 plus
build; JVM full tests with Java 21; Swift smoke; full .NET 1,549/1,549; public
combined paths/reverse smoke; Python bytecode compilation; private-path guard;
and `git diff --check`.

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
