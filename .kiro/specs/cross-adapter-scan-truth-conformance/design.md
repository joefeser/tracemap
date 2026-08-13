# Design: Cross-adapter scan-truth conformance

## Decision

Use a narrow shared data profile plus adapter-owned implementations. Do not add
a shared scanner runtime. Each adapter computes a domain-separated SHA-256 over
its own selected inventory with unambiguous framing of:

```text
ordered repository-relative path + file kind + byte length + actual selected bytes
```

Implementations may stream framed bytes directly or frame a full SHA-256 content
digest. Exact digest equality across languages is not a conformance claim because
their supported inventories differ. The invariant is deterministic sensitivity
to every selected byte without changing existing fact-ID formulas merely to
share implementation code.

Selected inputs participate with their actual byte digest. Inputs skipped before
analysis (for example, an oversized file) participate through their path, kind,
observed size, and closed skip marker, but their bytes are not misrepresented as
analyzed. Explicitly excluded and unsupported files do not participate. Persisted
paths retain their actual repository-relative spelling.

The authoritative scan ID includes repository identity, commit SHA, source
snapshot digest, normalized scan options, and adapter version. `scannedAt` is
retained only as non-authoritative display metadata; the conformance harness
normalizes it when comparing manifests. Existing fact-ID formulas are unchanged.

## Transaction boundary

Adapters stage outputs in a sibling temporary directory, complete post-extraction
snapshot verification, validate all five required artifacts, and then publish by
rename/replace. A failure removes staging and does not replace a prior completed
output. Adapter-specific typed CLI messages remain permitted; the readiness
harness classifies them using closed profile outcomes.

## Inaccessible input policy

- a selected file that cannot be inventoried or re-read makes the snapshot
  unavailable and stops publication;
- a directory traversal failure records a rule-backed gap only when the adapter
  can prove the bounded affected repository-relative scope and marks coverage
  partial; otherwise it stops;
- a source that becomes changed, removed, or unreadable after the initial digest
  stops with `SourceSnapshotChangedDuringScan` (adapter spelling may differ but
  the harness maps it to the neutral closed outcome);
- semantic/toolchain failures after a stable snapshot preserve provable facts and
  publish reduced coverage with gaps.

## Filesystem matching

The fixture keeps persisted NFD/NFC spelling intact. Exclusion comparison uses a
host-semantics comparison key only on filesystems that report equivalent names.
It does not normalize canonical evidence identity globally.

## Harness

`scripts/scan-truth-conformance.py` owns synthetic fixture generation,
subprocess execution, artifact validation, deterministic comparison, SQLite
readback through the installed `sqlite3` CLI where available, and the final
sanitized JSON/Markdown readiness report. It never reads protected repositories
or uses the network. Adapter command descriptors live in a versioned profile and
may be overridden locally without changing truth semantics.

The report has one row per adapter/capability with `required`, `supported`,
`reduced`, `unsupported`, `not-applicable`, or `not-run`; rule ID; evidence; and
limitations. A required row is green only with direct executable evidence.

## Current compatibility finding

At specification start, .NET already exposes `sourceSnapshotDigest`, mutation
verification, dirty-byte identity, inaccessible-source truth, and filesystem-
correct exclusion fixtures. JVM, Python, TypeScript, and Swift manifests do not
yet expose the digest. Their existing size/path inventory scan IDs are therefore
not conforming for same-size dirty mutations. This is a red starting condition,
not an exception to normalize away.

## Safety and limitations

The harness stores only synthetic relative paths, hashes, closed statuses, rule
IDs, counts, and tool versions. No raw source text is included in the readiness
report. Toolchain absence is truthful reduced evidence, not a product failure and
not proof of adapter behavior on an untested host.
