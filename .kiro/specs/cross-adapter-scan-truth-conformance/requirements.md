# Requirements: Cross-adapter scan-truth conformance

## Scope

This specification implements issue #664 for the shipped .NET, JVM, Python,
TypeScript, and Swift adapters. It defines shared truth semantics without
requiring shared implementation code or pretending that language toolchains
provide equivalent semantic coverage.

## Requirements

### R1 — Versioned neutral profile

TraceMap shall publish one implementation-neutral `scan-truth-conformance.v1`
profile defining required artifacts, identity, determinism, coverage, failure,
path, persistence, and schema behavior. The profile shall distinguish required,
supported, reduced, unsupported, and not-applicable outcomes.

### R2 — Concrete scan authority

Every conforming scan shall bind to a concrete repository identity and commit
SHA. Missing Git authority shall stop before a successful artifact set is
published. Repository paths and machine-local identity shall not become public
evidence.

### R3 — Actual analyzed-byte identity

Every adapter shall emit a deterministic SHA-256 source snapshot digest derived
from the repository-relative path, file kind, and actual bytes selected for the
scan. The scan ID shall include that digest and option authority. Materially
different selected bytes shall not share an authoritative scan ID, including
same-size dirty mutations.

### R4 — Mutation and inaccessible-input truth

The adapter shall verify the selected snapshot after extraction and before
publishing success. Changed, removed, or newly unreadable selected inputs shall
either stop with a typed failure or emit a rule-backed partial result according
to the adapter's documented policy. Directory or file access failures shall not
be silently interpreted as absence.

### R5 — Deterministic persistence

For identical bytes, options, tool version, repository identity, and commit,
the scan ID, manifest truth fields, facts, report, analyzer log, and SQLite fact
projection shall be deterministic. Volatile display metadata shall be omitted
or explicitly excluded from authoritative comparison.

### R6 — Artifact transaction

The five required outputs are `scan-manifest.json`, `facts.ndjson`,
`index.sqlite`, `report.md`, and `logs/analyzer.log`. They shall be published as
one completed or explicitly partial scan result, never as a plausible leftover
success after a failed attempt. Unknown or malformed persisted schemas shall
fail closed.

### R7 — Exclusion authority

Explicit include/exclude authority shall govern both inventory and downstream
compiler/parser inputs. Matching shall follow the host filesystem's case and
Unicode-equivalence behavior without globally normalizing persisted evidence
paths or collapsing filesystem-distinct names.

### R8 — Reduced analysis

Compiler, project-load, parser, or semantic failure shall preserve independently
provable syntax/structural evidence and emit rule-backed gaps. A failed build or
reduced toolchain shall never be labeled full success.

### R9 — Evidence round trip

Fact ID, direction-bearing source/target identity, rule ID, tier, path, span,
extractor ID/version, coverage, limitations, and supporting identities shall
survive NDJSON and SQLite persistence wherever the adapter emits them. Missing
fields shall be reported as capability gaps rather than manufactured.

### R10 — Executable matrix and report

A deterministic offline harness shall create synthetic repositories, run each
available adapter twice, validate the profile, and write a sanitized readiness
report. Missing toolchains shall be `unsupported` or `not-run`, never passing.
The harness shall return nonzero when a required invariant fails.

## Non-claims

Conformance does not prove language-semantic parity, build success, runtime
reachability, execution, production behavior, complete dependency coverage, or
Go readiness beyond the explicitly passing profile. It adds no LLM, embedding,
vector, prompt classification, runtime execution, protected source, or network
dependency.
