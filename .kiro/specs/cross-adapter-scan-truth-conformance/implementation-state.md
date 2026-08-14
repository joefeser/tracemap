# Implementation state: Cross-adapter scan-truth conformance

- Issue: #664
- Branch: `codex/cross-adapter-scan-truth`
- Integrated `origin/dev` prerequisite head: `1d45ba53755b95c3cfd5ccd7f0239bd57810cc21`
- Review-patch validation is recorded against the branch state immediately
  before its final review-fix commit; the exact commit is recorded after commit.
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

- Python: 32/32 adapter tests passed in an isolated temporary virtual environment.
- TypeScript: 39/39 tests passed; `npm run build` passed.
- JVM: Gradle test suite passed with Java 21.
- Swift: full smoke-test executable passed.
- .NET: 1,551/1,551 solution tests passed on the final integrated `origin/dev` stack.
- The final #675 reduced Web Forms packet-coverage propagation is included;
  focused local-review/explorer tests passed 19/19 on the final integrated stack,
  including all exact-head failure-evidence regressions from #675 review.
- The full five-adapter executable matrix returned `supported`; its sanitized
  JSON SHA-256 was
  `19431acded95d068c61fc8af8fab971019a1282c04063c53828a310dd2d8221d`.
- Combined public paths/reverse smoke, private-path guard, Python bytecode
  compilation, and `git diff --check` passed.
- The authoritative matrix was rerun after stacking the completed prerequisite
  branches and again after review hardening. The final suites passed with
  Python 32/32, TypeScript 39/39, JVM on Java 21, and the Swift executable smoke.

Final prerequisite integration on 2026-08-13 includes merged Web Forms explorer
PR #676 at exact feature head
`625d3d8d4cb512c2ebde9d3d230b354f32e1b47c` and `origin/dev` merge
`1d45ba53755b95c3cfd5ccd7f0239bd57810cc21`. The first earlier matrix invocation
through the system Python truthfully returned `unsupported` because pytest was
not installed in that interpreter; it did not report a product conformance
failure. Following `docs/VALIDATION.md`, all authoritative reruns used an
isolated temporary venv containing `src/python[dev]`.

The final post-merge matrix returned `supported` for all five adapters and
produced the sanitized digest
`19431acded95d068c61fc8af8fab971019a1282c04063c53828a310dd2d8221d`.
Exact integrated validation then passed: full .NET 1,551/1,551; public combined
paths/reverse smoke; Python bytecode compilation; private-path guard; and
`git diff --check`. The matrix itself rebuilt and exercised the shipped .NET,
JVM, Python, TypeScript, and Swift adapters at the final integrated checkpoint.

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
- .NET, JVM, Python, TypeScript, and Swift now have deterministic staged-write
  failure coverage proving a prior complete packet survives a failure after the
  new manifest is staged. Swift selected-input read failure is fatal rather
  than represented by a constant digest marker.
- The conformance harness pins SHA-1 fixture repositories, compares every
  manifest commit to the fixture's actual `HEAD`, requires Java 21, verifies
  that an unreadable-file precondition is real before claiming support, and
  always emits non-empty rule-governed capability evidence.
- TypeScript snapshot ordering is ordinal rather than locale-sensitive.
- Partial `--adapters` invocations retain all five required adapter rows and
  report omitted adapters as `not-run`; they cannot emit a green full-profile
  result. Adapter option lists use unambiguous framing so distinct authority
  does not collapse to one scan ID, and Swift identity includes its concrete
  scanner and extractor versions.
- JVM, TypeScript, and Swift publication now reject a repository root, ancestor,
  file, or unrecognized nonempty output directory before any swap; existing
  unrelated content is never treated as a replaceable TraceMap packet.

## Deferred

- Go adapter #665.
- Runtime evidence and semantic parity across compiler toolchains.
- Any protected or real-customer repository validation.
- Filesystem-correct Unicode exclusion is `not-applicable` on hosts that report
  NFC and NFD names as distinct; the harness does not collapse such names.
