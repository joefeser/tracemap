# .NET Evidence Completeness Status

Status: Task 8 source/metadata reconciliation merged; Task 9 portable PDB identity and sequence-point evidence implemented locally as of 2026-09-20, with native Windows fail-closed CI validation pending

Authority: `codex/pdb-sequence-point-evidence` based on `origin/dev` at `7dc943f2f9d5de82b0963e3e1b8aa9196116b51c`

This page is the current index for completed Web Forms work, remaining .NET
evidence gaps, and the next implementation slice. Older Kiro
`implementation-state.md` files are historical delivery records; they do not
override this page or a later current-head record.

## Complete on `dev`

- Web Forms terminal reachability is complete for the retained graph contract
  delivered by PR #770. Algorithm `1.2` inventories every distinct supported
  terminal admitted by the bounded graph and retains one shortest deterministic
  witness per terminal. It separately reports terminal-inventory completeness
  and path-detail truncation.
- The verified `page-011` comparison retained 3 event chains, 1 downstream
  boundary, 26 path-evidence records, 24 call-evidence records, 167 supporting
  facts, 43 citations, 12 server behaviors, 1 structural candidate, 266
  supporting IDs, and 5 retrieval hints. Chain and boundary outcome deltas were
  zero; the classification was
  `stable-retained-outcomes-with-terminal-inventory-added`.
- The handoff size reduction from 338,775 to 174,738 bytes reflects 22 removed
  `LegacyPathEvidenceCoverageUnavailable` gaps after overlapping alternate
  terminal routes were replaced by one deterministic shortest witness per
  distinct terminal. The comparison found no loss of retained terminal
  inventory or chain/boundary outcomes; alternate path detail was deliberately
  reduced and remains labeled as such.
- VB.NET adapter foundation, data/external-boundary extraction, Web Forms event
  composition, review/export parity, annotated source views, and agent evidence
  handoffs are merged. Their Kiro state files retain the implementation-time
  validation receipts.

## Remaining evidence gaps

- Projectless VB receiver resolution remains reduced coverage. The verified
  comparison retained a combined 20 receiver gaps:
  `ProjectlessVisualBasicReceiverCreationUnavailable` changed 7 to 8 and
  `ProjectlessVisualBasicReceiverTargetUnavailable` changed 13 to 12. One item
  changed category; the combined count did not improve.
- Source-derived evidence does not by itself establish compiled metadata, PDB,
  IL, or rewritten-member identity. Task 8 adds exact receipt-bound
  source/metadata joins, and Task 9 adds exact receipt-bound portable PDB
  method/document/sequence-point evidence; the layers remain distinct and are
  never collapsed through display strings.
- F# source extraction is not implemented. F# fixtures may validate compiled
  CLR shapes while source-side coverage remains explicitly unsupported.
- Missing, stale, ambiguous, unbound, or mismatched binaries, dependency
  resolution failures, reader disagreement, and bounded-input truncation must
  remain explicit partial or unknown coverage. Timestamps alone cannot prove
  staleness.

## Windows-only validation

- Portable PDB evidence is cross-read by System.Reflection.Metadata and
  Mono.Cecil on ordinary managed fixtures. Native Windows PDBs remain
  fail-closed: Windows emits `WindowsPdbIndependentReaderUnavailable` and
  non-Windows hosts emit `WindowsPdbRequiresWindows`. The Windows CI lane builds
  real C# and VB native PDBs and must prove zero positive PDB facts; this does
  not validate legacy .NET Framework builds, ASP.NET Web Forms build behavior,
  ILAsm/ILDAsm parity, or Windows runtime loading.
- The pinned historical `dotnetperf` corpus and .NET Framework-era toolchain
  require an isolated Windows x64 lane described in
  [DOTNETPERF_CORPUS_RUNWAY.md](validation/DOTNETPERF_CORPUS_RUNWAY.md).
- C++/CLI is a separate Windows-only feasibility question. Ordinary Mono.Cecil
  support for managed metadata must not be described as support for native or
  mixed-mode C++.

## Current issue map

- [#759](https://github.com/joefeser/tracemap/issues/759): parent .NET identity
  and rewrite-correctness epic.
- [#766](https://github.com/joefeser/tracemap/issues/766): public ECMA-335, IL,
  PDB, and rewrite identity suite.
- [#767](https://github.com/joefeser/tracemap/issues/767): public C#, VB.NET,
  and F# CLR-equivalence fixtures.
- [#768](https://github.com/joefeser/tracemap/issues/768): isolated Windows
  `dotnetperf` and C++/CLI lane.
- [#769](https://github.com/joefeser/tracemap/issues/769): mine and minimize the
  access-controlled rewrite corpus.
- [#762](https://github.com/joefeser/tracemap/issues/762): historical VB
  receiver regression. PR #770 supplies the current-head terminal-reachability
  outcome; issue disposition should be reconciled against that merge rather
  than inferred from the older branch plan.

## Active implementation

The active design is
[`compiled-dotnet-evidence-foundation`](../.kiro/specs/compiled-dotnet-evidence-foundation/requirements.md).
Its completed foundation inventories explicitly admitted managed assemblies
with exact assembly/module/type/member metadata identities, bounded and
privacy-projected provenance, explicit dependency-resolution outcomes, and
explicit gap contracts against small public C#/VB.NET/F# fixtures. Task 8 adds
exact deterministic source/metadata reconciliation only for complete one-candidate
identities backed by bound receipts. Task 9 adds explicit PDB inputs, exact
portable content-ID/CodeView binding, independent SRM/Cecil sequence-point
shape agreement, exact metadata method-row joins, exact source document
checksum joins, and bounded PDB provenance/endpoint summaries in every scan
artifact. Missing, ambiguous, mismatched, unbound, unsupported, over-budget,
or disputed inputs remain gaps.

Current local Task 9 validation passes 2,019/2,019 full-suite tests and the
59/59 combined PDB, source/metadata reconciliation, and managed metadata tests;
the PDB-focused portion is 18/18. The PDB matrix proves C#/VB/F# portable
evidence, hidden and non-monotonic
points, multi-document methods, generated-member separation, zero/multiple
candidates, unacceptable provenance, bounded limits, privacy projection,
deterministic repeat output, and all required artifacts. The Windows-produced
native PDB lane and cross-platform workflow remain CI evidence, not a local
macOS claim. Exact-head ACK remains the merge-readiness authority; green CI
alone does not imply merge readiness.

Task 9 does not add IL body/call extraction, rewrite analysis, positive native
Windows PDB reading, legacy framework execution, historical/private corpus
execution, or C++/CLI support.

Correctness work belongs to the open evidence engine. Managed fleet execution,
hosted retention, and managed private Windows workers may belong to a later
commercial operating layer, subject to
[OPEN_CORE_BOUNDARY.md](OPEN_CORE_BOUNDARY.md).
