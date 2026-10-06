# .NET Evidence Completeness Status

## Record-generated member coverage (2026-10-06)

The bounded #767 continuation pins selected C#/F# record-generated object
overrides versus ordinary VB lookalikes, independently read with SRM and Cecil.
Equal signatures preserve distinct assembly/member identities and generated
markers. No runtime record semantics or F# source ownership is inferred.
See the [record acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#record-generated-member-continuation).
The broader epic and private corpus/Windows acceptance remain open.

## Operator/conversion coverage (2026-10-05)

PR #827 is merged at `c155a35f`. The next bounded #767 slice pins C#/VB/F#
addition and implicit/explicit conversion MethodDefs, raw signatures and an
ordinary operator-looking decoy. Assembly scopes stay distinct, including
self-type signatures. F# marks the decoy SpecialName while C#/VB do not;
this is not proof of operator semantics. Bound C#/VB source joins and the F#
unsupported-source gap are tested separately. See the
[acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#operator-and-conversion-continuation).
Private corpus/Windows acceptance and broader language coverage remain open.

## Explicit-interface coverage (2026-10-05)

PR #826 is merged at `b40267b1`. The next bounded #767 slice pins explicit
interface declaration/body associations across C#/VB/F# using independent raw
metadata tokens, with ordinary same-signature methods as decoys. The matrix
preserves language-specific names/flags and does not add production dispatch
edges. See the [acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#explicit-interface-continuation).
Private/Windows acceptance and broader language coverage remain open.

## Default-value coverage (2026-10-05)

PR #825 is merged at `74364fe5`. The next bounded #767 slice distinguishes
required, integer, string, null and decimal defaults across C#/VB/F#. Independent
constant/attribute oracles keep default values separate from method signatures
and optional markers. Production default-value facts remain a gap. See the
[acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#default-value-continuation).
Private/Windows acceptance and the broader epic remain open.

## Generic-constraint coverage (2026-10-05)

PR #824 is merged at `5adfaeb6`. The next bounded #767 matrix pins five
constraint shapes across C#/VB/F#, including F# struct flag/row differences.
Independent metadata oracles retain constraints separately from equal method
signatures. Production constraint evidence remains a gap. See the
[acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#generic-constraint-continuation).
Private/Windows acceptance and the broader epic remain open.

## Property/event accessor coverage (2026-10-05)

PR #823 is merged into `dev` at `c389b20d`. The next bounded #767 slice
pins matched C#/VB/F# property/event signatures and verifies accessor method
endpoints using independent metadata handles. Getter-looking names cannot
create associations; VB's explicit event raiser remains a distinct method.
See the [case and acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#propertyevent-accessor-continuation).
No runtime equivalence or F# source support is inferred; broader epic and
private/Windows acceptance remain open.

## Nullable and F# option coverage (2026-10-05)

PR #822 is merged into `dev` at `75636837`. The next bounded #767 slice
compares C#/VB/F# Nullable<Int32> signatures and preserves distinct F# option,
value-option and optional-argument method identities. It pins exact metadata
scopes, provenance, refusal cases and the F# source-unsupported gap. See the
[case and acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#nullable-and-f-option-continuation).
No source extraction, runtime equivalence or private/Windows acceptance is
inferred. All epic issues remain open.

## Bound source optional-marker coverage (2026-10-05)

PR #821 is merged into `dev` at `a0de5398`. The next bounded #767 slice
pins real C#/VB wide optional-parameter joins, negative/ambiguous comparator
inputs and bounded summary evidence. Source ordinal sorting was already numeric;
no production fix or extractor version change is needed. Source reconciliation
joins the portable metadata CI filter. See the
[acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#bound-source-optional-marker-continuation).
Full default semantics, F# source extraction and private/Windows acceptance
remain open; this does not close any epic issue.

## Optional-parameter continuation (2026-10-05)

PR #820 is merged into `dev` at `e9212c53`. The next bounded #767 slice
requires Cecil/SRM agreement on optional-parameter markers, corrects numeric
ordinal ordering and sparse setter-only property selection, and adds matched
public C#/VB/F# regressions. Marker equality does not establish default-value,
source, runtime or complete API equivalence. See the
[optional-parameter matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md#optional-parameter-agreement-continuation).
The broader issues and Windows/private dependencies below remain open.

## Previous reconciliation (2026-10-05)

The Task 9-only status below is historical. Fetched `origin/dev` at
`9dde9bb5` and `origin/main` at `7f026f5a` contain the metadata,
source-reconciliation, PDB, IL/rewrite, public ILAsm parity and bounded Windows
runner merges (#772–#788). They do not establish full epic acceptance.
See the [current acceptance matrix](validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md)
for exact base SHAs, implementation/test evidence, outstanding gaps and host
requirements. The new #767 slice tests matched C#/VB/F# CLR method signatures
while preserving distinct assembly/member endpoints. F# source extraction,
independent ILDAsm portable-PDB line parity, and private corpus acceptance are
still unclaimed. Tasks 10/11 and issues #759/#766/#767/#768/#769 remain open.

## Historical Task 9 delivery record

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
