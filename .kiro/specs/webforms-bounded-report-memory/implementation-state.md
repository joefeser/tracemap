# Implementation state

Branch: `codex/webforms-bounded-report-memory`
Base: `ce6b449f0be49b04f524c23641c42ff56c155ec8` (fresh origin/dev).
Implementation commit: `15c699ee` (reader, packet, CLI, rules, and 11 regression cases).

## Scope and design

The reported work scan produced approximately 1.65 million facts and a 9 GB
SQLite index. Screenshots report successful 200/300-surface packets and OOM at
larger caps; they are not a heap profile. Local source confirms unbounded fact
materialization and whole-packet string serialization.

The first implementation deliberately preserves repository-wide graph candidate
context. Filtering by roots before existing symbol reconciliation/dispatch would
risk hiding collisions and promoting ambiguous evidence. Instead, stream the
single-index reader in its existing graph insertion order, compact a closed list
of graph-inert syntax facts to the first symbol witnesses, and retain full rows
for all graph-relevant facts, unknown types, declared surfaces, and legacy rules.
Retain referenced supporting IDs/provenance even for omitted symbol witnesses.
Preserve the ordinary paths/combine readers.

Explicit admission limits bound retained facts, edges, serialized text bytes, and
individual row size before allocating strings/JSON dictionaries. Incomplete
graph input must never be classified: return a typed rule-backed gap and retain
independent Web Forms snapshot evidence. Snapshot input also has admission bounds.
Stream JSON bytes directly to the staging file, preserving atomic publication
and the existing v1 schema. Limits are not runtime reachability claims or a hard
OS RSS guarantee.

Root-specific lazy graph loading remains a later optimization requiring a
complete ambiguity/context contract. This slice removes repetitive fact payload
retention and whole-JSON string copies and adds safe failure behavior, with
synthetic parity and memory validation. The private work index stays local to the
owner; its all-surfaces rerun is final real-world validation, not claimed here.

## Validation

- Build passes with the pre-existing `PropertyMappingTests.cs:560` nullable
  warning (`CS8602`); no build errors. Unrelated CLI switch formatting was left
  unchanged after limiting formatter churn.
- Final full solution: **1,729/1,729 passed**. Focused reader/packet/path suite:
  **74/74 passed**, including 11 new memory regression cases. The final parity
  fixture also protects duplicate `surfaceKind` JSON keys: any key presence
  prevents compaction, avoiding SQLite first-key versus JSON last-key disagreement.
- CLI scan of `samples/modern-sample`: 27 facts. Separate scratch synthetic
  non-compiling .NET Framework 4.5 Web Forms project with a missing generated
  compile input: 67 facts, reduced semantic coverage. Its packet preserves one
  page and one event chain; no backend terminal is invented. A constrained CLI
  rerun emits the typed snapshot input-limit gap and `truncated: true`.
- Input hash, report JSON/provenance parity, and 518 independently rooted
  surface/chain/boundary assertions pass. Packet serialization is byte-equivalent
  to the previous JSON contract, including its final newline.
- Formatting verification passes for the changed reporting/test files;
  private-path guard and diff whitespace checks pass.
- Non-.NET language extractors are unchanged. TypeScript checks pass **49/49**;
  JVM/Python and pinned adapter smokes are deferred as unrelated to this
  single-index .NET report-reader change.

### Synthetic memory experiment (macOS, .NET 10.0.10)

| Noise rows | Reader experiment | Index bytes | Report-call managed allocations | Retained graph input |
| --- | --- | --- | --- | --- |
| 100,000 | original full reader | 153,645,056 | 767,308,848 bytes | all facts/properties materialized |
| 100,000 | bounded reader | 153,645,056 | 12,976,968 bytes | 5 facts + 1 edge; 2,010 text bytes |
| 1,000,000 | bounded reader | 1,536,245,760 | 128,179,640 bytes | 5 facts + 1 edge; 2,010 text bytes |

Managed allocations count temporary objects, not retained heap. The bounded
100,000-row call inside the full-reader comparison allocated 12,971,440 bytes;
small test-process variation is expected. At one million rows, the test visited
1,000,005 facts and still returned identical report bytes. A separate 50 ms
`ps` sampler observed peak aggregate RSS of the test command and descendants:
928,416 KiB for the full-reader comparison, 396,800 KiB for bounded 100,000,
and 402,208 KiB for bounded one million. Those are sampled test-process-tree
measurements including fixture creation/runner overhead, not an isolated
reporter RSS claim or a Windows prediction. The native .NET peak-working-set
counter returned zero on this host and was not used as evidence.

### ACK preflight / review boundary

No PR or external review request was created; no merge was attempted. The exact
documented ACK v0.4.4 tag/HEAD `855428f7a8e9bd084decc3a1569aa59f7d50583d`
was located and rebuilt, but its release receipt is missing. Release verification
fails with ENOENT; doctor loads the lane and returns `LOCAL_ACK_CHANNEL_NOT_ALLOWED`
because the build has `unverified_build` / `preview` release provenance. Do not
substitute a mutable or unverified binary or declare merge readiness.

The consumer lane test independently fails 1 of 3 cases on the fresh base:
the committed lane allows `>=0.4.4 <0.6.0`, while the test expects
`>=0.4.4 <0.5.0`. Neither lane nor its test was changed in this slice. This
review-tooling mismatch and verified-release setup need a separate repair;
recorded failures are not a reason to weaken gates in the Web Forms patch.

## Handoff

The sanitized restricted Windows rerun observation from 2026-09-02 is recorded
in [`restricted-run-2026-09-02.md`](restricted-run-2026-09-02.md). It completed
with a partial/reduced result, 1,653,627 facts, 13,460 gaps, and complete timing
coverage. Artifact writing dominated the 1,104,151 ms run. No OOM or failed
process was observed. A later bounded local review of the sanitized summaries
reported 932,070 Tier1 facts and identified
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` as the
dominant workspace blocker at 10,588 occurrences. The screenshots and
transcribed review do not replace an exact-text artifact or synthetic
reproducer for a subsequent scanner change.

Implementation and runbook are committed locally; pushing/opening a PR and
repairing ACK setup remain separate next steps. No merge readiness is claimed.
Use the README's large-index/OOM link for the private Windows rerun after the
branch is made available there. Keep the successful 300-surface packet and the
original scan index, select a new output directory, and compare counts/gaps and
memory. A deterministic input-limit gap is a truthful partial result, not proof
that every requested event chain was analyzed. Full private-index completion
and any subsequent root-specific lazy-loading design remain unverified here.
