# .NET Evidence Completeness Status

Status: compiled metadata foundation implemented and portable cross-platform validation complete as of 2026-09-20; exact-head required review remains pending

Authority: implementation branch based on `origin/dev` at `0b728b62943de7c0c52a44e170e870c2691dcd34`

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
- Source-derived evidence does not establish compiled metadata, PDB, IL, or
  rewritten-member identity. Those are distinct evidence layers and must not be
  collapsed through display strings.
- F# source extraction is not implemented. F# fixtures may validate compiled
  CLR shapes while source-side coverage remains explicitly unsupported.
- Missing, stale, ambiguous, unbound, or mismatched binaries, dependency
  resolution failures, reader disagreement, and bounded-input truncation must
  remain explicit partial or unknown coverage. Timestamps alone cannot prove
  staleness.

## Windows-only validation

- Mono.Cecil can read ordinary managed assemblies on macOS. That does not
  validate legacy .NET Framework builds, Windows PDB behavior, ASP.NET Web
  Forms build behavior, ILAsm/ILDAsm parity, or Windows runtime loading.
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
Its first implementation slice now inventories explicitly admitted managed
assemblies with exact assembly/module/type/member metadata identities, bounded
and privacy-projected provenance, explicit dependency-resolution outcomes, and
explicit gap contracts against small public C#/VB.NET/F# fixtures. Mono.Cecil
rows are independently checked with `System.Reflection.Metadata`; disputed
rows are withheld. Source and compiled facts remain separate, private compiled
facts remain local-only, and the scan identity commits the compiled admission
contract before fact IDs are derived.

Local validation passed 1,972 tests with zero failures or skips, including 12
focused managed-metadata tests and a 19-test managed-metadata/report set. Two
admitted compiled-input CLI scans each emitted 107 facts, including 80 compiled
facts; their `facts.ndjson` files were byte-identical, both artifact sets passed
the adapter validator, and neither output contained local absolute paths. On PR
#772 implementation head `5e17ee4a`, the portable matrix and package smoke
passed on Windows, Ubuntu, and macOS, and the .NET, combined-adapter, and
private-path jobs passed. ACK still withholds merge readiness because required
review evidence names older head `7df78401`; no merge is implied by green CI.

This slice does not add broad IL traversal, source-to-metadata reconciliation,
rewrite analysis, PDB reconciliation, legacy framework execution,
historical-corpus execution, or C++/CLI support.

Correctness work belongs to the open evidence engine. Managed fleet execution,
hosted retention, and managed private Windows workers may belong to a later
commercial operating layer, subject to
[OPEN_CORE_BOUNDARY.md](OPEN_CORE_BOUNDARY.md).
