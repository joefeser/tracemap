# Compiled .NET Evidence Foundation Implementation State

Status: Tasks 1-9 are merged into `dev`; PR #774 completed its authorized exact-head ACK review and was merged into `dev` as `ed2fdf1c028b034a9a6013908e8f5b8c29d900a7` on 2026-09-21. The first Task 10 slice (bounded operand-aware IL body/call evidence, PR #775) is also merged into `dev` as `46b2baa125bed00ee9ac1964ce50febd55c3e68e` on 2026-09-21, including the owner-requested P1/P2 follow-up `2d20b4a3` (prefix-operand signed/unsigned distinction and exact UTF-16 literal hashing). PR #779 (slice 4, bounded rewrite PDB identity evidence) merged into `dev` as `7be1f51c0360a7f80e3b7304aee9677fe3c92ddc` on 2026-09-22. PR #780 (slice 5, public control-flow and exception-handling rewrite suite) merged into `dev` as `2766de6933d4d60bb632389eff24bc4bfc2313f0` on 2026-09-22. PR #781 (slice 6, public metadata operands and member shapes) merged into `dev` as `dbb4f10aef8201da7e046b428b7a5c4a2e5105c2` on 2026-09-22; GitHub reported `MERGED` with this exact merge commit, and current `origin/dev` resolved to the same SHA before this integration branch was created. PR #782 (the public ECMA-335 integration continuation: topology suite, embedded portable PDB binding, Git-SHA fixture hardening) merged into `dev` as the true merge commit `dc446ab560c7badf1f1518814685b33ccb9c375d` on 2026-09-22 (second parent `a52d239d7c1e3abb32eb6c1d20ba7a466a2508f1`); every PR check passed on that head, including `public-mutation-matrix (windows-latest)` at 118/118 and all three `package-smoke` operating systems. The remaining Task 10 public rewrite suite from #766 and Task 11 remain open.

Branch: `codex/il-body-call-evidence`

Base: `origin/dev` at `ed2fdf1c028b034a9a6013908e8f5b8c29d900a7`

Tracking: #759, #766, #767, #768, #769

Task 8 implementation commits: `010502ef` (`feat: reconcile exact source and
metadata identities`) and `55fefd48` (`test: prove source metadata
reconciliation matrix`).

PR #773 merged into `dev` as
`7dc943f2f9d5de82b0963e3e1b8aa9196116b51c` on 2026-09-20.

## Scope decision

The first implementation slice ends at bounded managed-input inventory,
assembly/module/type/member metadata identity, provenance/coverage gaps,
cross-reader agreement, and small public C#/VB.NET/F# fixtures. Source and
compiled facts remain separate in this slice. It does not begin
source-to-metadata reconciliation, broad IL traversal, rewrite analysis, PDB
reconciliation, historical corpus execution, or C++/CLI support.

## Evidence decisions

- Mono.Cecil supplements Roslyn and syntax fallback; it replaces neither.
- Compiled metadata is Tier2 structural evidence unless a future documented
  rule justifies a different tier. Reader/provenance/availability gaps are
  Tier4 unknown.
- Source, metadata, PDB, IL, and rewritten identities stay separate.
- Every compiled fact retains the ordinary scan repository/commit identity,
  while optional receipt-validated binary-source/build provenance uses
  separate fields and may remain absent for an unbound input.
- Manifest-level compiled-input provenance is unconditional, including when no
  assembly is admitted. Its local bounded-input digest participates in `scanId`
  before fact IDs are derived; a distinct shareable summary binds its
  privacy-projected digest into its own artifact identity.
- Private compiled facts/indexes remain local-only in the first slice; a
  shareable derivative must be a distinct non-`CodeFact` privacy-projected
  summary rather than leaking or substituting required scan identity fields.
- Metadata-only facts use a safe assembly locator, `1..1` non-source sentinel,
  and versioned token-bearing properties. Ambient dependency resolution is
  forbidden; only declared, admitted, hashed dependencies may be read.
- Metadata tokens are module-local locations. MVID, path, timestamp, and display
  string alone cannot prove source identity or freshness.
- Bounded-input hashing uses a canonical pre-digest payload that excludes the
  digest and all downstream identities derived from it; the computed digest is
  attached before scan/fact IDs are derived.
- Normalized assembly/type/member/signature identities length-prefix every
  free-text component, preserving delimiters, the empty/global namespace, and
  nested declaring chains without ambiguous concatenation.
- Missing, stale, ambiguous, unbound, or mismatched inputs reduce compiled
  coverage and never erase or upgrade source-derived evidence. Timestamps alone
  do not establish staleness.
- Correctness, fixtures, and validation contracts are open-core. Managed
  private Windows execution is outside this implementation slice.

## Historical inputs

- PR #770 at the base commit is the current Web Forms terminal-reachability
  authority.
- The two retained VB/Web Forms investigation worktrees are preserved and are
  inventoried in
  `docs/history/WEBFORMS_VB_INVESTIGATION_BRANCHES_2026-09-19.md`. They will not
  be merged wholesale.
- The `dotnetperf` assessment is pinned to
  `db8c3359badfec620ccdc6df062b1756ef9607f8`. Its strongest dimensions are
  exception-region/branch rewriting and Cecil method-body behavior; its test
  count is not broad ECMA conformance and it lacks the required modern
  cross-language matrix.

## Validation state

Tasks 1-6 are implemented. The lane uses pinned Mono.Cecil `0.11.6` with
deferred reading and a resolver that rejects ambient resolution, then
independently compares normalized rows from `System.Reflection.Metadata`.
Disputed rows are withheld. The CLI accepts explicit primary, dependency, and
binding-receipt inputs plus validated admission limits; the manifest, `scanId`,
facts, SQLite index, Markdown report, and execution receipt retain the bounded
contract without exposing raw absolute paths.

The public portable fixture matrix lives under
`samples/compiled-dotnet-evidence/` and covers C#, VB.NET, and F#. Focused tests
exercise exact CLR identities, duplicate and unresolved dependencies,
provenance states, malformed/native/missing/over-budget inputs, reader
disagreement, deterministic bytes, privacy, all five scan artifacts, and
unchanged source evidence. Review hardening also covers iterative deeply nested
type inventory, filesystem-aware input and receipt-path deduplication, bounded
projection of overlong input locators, and explicit rejection of metadata-bearing
secondary modules. It also pins top-level receipt binding counts, rejects text
limits too small for a complete projected digest, converts excessive
metadata-signature nesting into an explicit partial-coverage gap, and keeps
compiled coverage separate from the source `analysisLevel`. Artifact overflow
retains only the configured number of per-input rows plus a deterministic
omitted-count/digest commitment. The local distribution workflow runs the same
focused tests on Windows, Ubuntu, and macOS.

Implementation commit: `a70ac803` (`feat: add bounded compiled metadata evidence lane`)

Portable fixture/test commit: `522e4325` (`test: add portable compiled metadata fixture matrix`)

Final bounds/provenance fix commits: `f28c4e93`, `7df78401`, and `5e17ee4a`.

Local macOS validation on 2026-09-20:

- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero warnings
  and zero errors.
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 1,982 passed,
  zero failed, zero skipped.
- focused `ManagedMetadataExtractorTests`: 22 passed, zero failed.
- two explicit admitted compiled-input CLI scans: 107 facts each, including 80
  compiled-rule facts; byte-identical `facts.ndjson`; all five required
  artifacts present; both output directories passed
  `scripts/validate-adapter-artifacts.py`; neither output contained a local
  absolute path.
- `scripts/check-private-paths.sh`: passed.
- `node scripts/kiro-review.mjs --self-test`: passed; implementation prompt
  dry-run completed with `Coverage: NotRun` as expected because it did not
  invoke the external Kiro reviewer.
- `git diff --check`: passed.

Task 7 is complete. Earlier PR #772 heads passed the portable matrix and package
smoke on Windows, Ubuntu, and macOS plus the .NET adapter, combined-adapter, and
private-path jobs. PR #772 was merged into `dev` as
`532fccfb0a7588ab64397f672ca6a8dddef8d083` on 2026-09-20. No reviewer was
manually retagged during the completed first-slice review loop.

Portable Windows success does not prove Windows PDB, legacy .NET Framework or
Web Forms build behavior, ILAsm/ILDAsm parity, the historical `dotnetperf`
corpus, or C++/CLI feasibility. The following remain deferred without implied
support: source-to-metadata reconciliation, PDB identity, operand-aware IL,
rewriting, legacy build execution, the historical corpus, and C++/CLI.

The later exact-head review added regression coverage for receipt-only scans
and oversized assembly-reference identities. Receipt-only activation now emits
the rule-backed `NoManagedInputDeclared` gap, and assembly-reference plus
dependency-resolution strings honor the configured compiled text limit.

## Task 8 exact source-to-metadata reconciliation

`dotnet.compiled.source-identity.v1` is active. When the compiled lane is
explicitly requested, the C# and Visual Basic semantic adapters retain internal
compiler-resolved source declaration identities without changing their normal
source facts. The reconciler emits one Tier1 edge only for one complete exact
metadata identity candidate backed by a validated `bound` receipt. Each edge
retains both endpoint identities, source and compiled fact IDs, rule and
extractor versions, bounded-input and generator digests, receipt-binding digest,
compiled provenance state, relationship proof, and the rule limitation.

Zero and multiple candidates, incomplete source identities, optional-parameter
state disagreement, and unacceptable compiled provenance remain Tier4 gaps
with no edge. Display strings, simple names, arity, path proximity, timestamps,
and metadata tokens never select a candidate. Compiler-generated declarations
remain separate except for Roslyn's specific associated property/event accessor
relationship. F# compiled identities remain available, but its `.fsproj` lane
emits `SourceMetadataReconciliationUnsupportedLanguage` and zero guessed joins.
Compiled coverage remains independent from source `analysisLevel`.

The v3 public fixture contract retains the stable reconciliation case IDs, exact
source and metadata identities, expected rule/tier/outcome/gaps, and non-claims.
The matrix covers namespaces, nested/generic declarations, complete overload
signatures, constructors, properties/indexers, events/accessors, C# ref and VB
ByRef shapes, optional parameters, explicit interfaces where representable,
scoped decimal signatures, constructed nested generic signatures with their
enclosing arguments, scoped non-primitive special types, non-generic nested
types inside constructed generic containers, same-looking cross-assembly/language
declarations, exact zero candidates, exact multiple candidates, unbound inputs,
class/struct/record primary constructors, C# ref fields, intrinsic
`System.TypedReference`, and the F# unsupported source lane. Source custom
modifiers fail closed. Optional-parameter mismatch gaps retain the
rejected metadata candidate's compiled provenance and receipt-binding digest.
Top-level C# statements are excluded from declaration collection, and
syntax-located declarations without a Roslyn symbol emit Tier3 observations
plus Tier4 incomplete gaps rather than claiming Tier1 semantic identity.

Local macOS Task 8 validation on 2026-09-20:

- focused `SourceMetadataReconciliationTests`: 19 passed, zero failed, zero
  skipped;
- existing `ManagedMetadataExtractorTests`: 22 passed, zero failed, zero
  skipped;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero warnings
  and zero errors;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,001 passed, zero failed,
  zero skipped;
- two bound C# fixture CLI scans: 371 facts each, 60 exact reconciliation edges,
  and two explicit incomplete source-identity gaps; `facts.ndjson`, report,
  reconciliation manifest summary, and normalized SQLite fact rows matched;
  both output directories contained all five required artifacts and passed
  `scripts/validate-adapter-artifacts.py`;
- `scripts/check-private-paths.sh`: passed;
- `node scripts/kiro-review.mjs --self-test`: passed; and
- `git diff --check`: passed.

The two retained C# gaps are the existing unmanaged function-pointer shapes;
compiled identities remain available, but source reconciliation intentionally
fails closed as `SourceFunctionPointerIdentityUnsupported` until a separately
documented complete Roslyn custom-calling-convention identity contract exists.
Task 8 makes no PDB/sequence-point, IL body/call, rewrite, `dotnetperf`/private
corpus, legacy Framework/Web Forms, or C++/CLI claim. PDB work was deferred
from that slice; Task 9 is documented below and Tasks 10-11 remain open.
Operational `scannedAt` and receipt durations remain wall-clock
diagnostics rather than deterministic evidence identifiers; deterministic
evidence payloads and normalized artifact content were compared instead.

Current-head review remediation adds reconciliation-only complete source
identities without changing ordinary source fact IDs; enclosing nested generic
arity and parameter ordinals; independent value/ref and generic-arity overload
joins; optional VB indexed-property parameter evidence in both metadata
readers; explicit unresolved declaration gaps; partial coverage when semantic
identity collection is unavailable; known rejected provenance on gaps; and
bounded, fully committed summary/supporting-ID overflow. Summary rows now carry
their evidence fact ID, file/line span, commit SHA, total join/gap counts, and
report-level truncation disclosure. The pinned C#/VB OSS smoke remains
explicitly deferred because it supplies no admitted compiled input and cannot
exercise this internal reconciliation-only lane; the full source-only suite
remains the acceptance guard for unchanged ordinary adapter facts.

The second exact-head review tightened three fail-closed boundaries. Reduced
semantic analysis now makes reconciliation partial even when every retained
candidate joins. Named signature types now include complete assembly-reference
scope in both source and Cecil/SRM metadata identities, so same-looking types
from different assemblies cannot compare equal. Roslyn `IErrorTypeSymbol`
values emit `SourceErrorTypeIdentityUnavailable` rather than a plausible
namespace/name identity. Focused regressions cover all three behaviors.

## Task 9 PDB identity and sequence points

Task 9 activates the explicit bounded PDB input, PDB identity,
sequence-point, and PDB gap rules. Portable PDBs bind to exactly one admitted
assembly only through the exact portable content GUID/stamp and PE CodeView
entry, with `bound` compiled receipt provenance required before any positive
fact. System.Reflection.Metadata and Mono.Cecil independently read the complete
portable method/sequence-point shape; disagreement withholds the input. PDB
documents and method rows remain distinct from metadata and source identities.

Positive facts preserve the PDB input, document, method, compiled method, and
metadata/PDB reconciliation supporting fact IDs as applicable, plus content
identity, both endpoint identities, matched assembly identity, compiled
receipt-binding digest, rule, tier, extractor version, generator and bounded
input SHA-256 values, provenance state, and limitation. Source documents join
only by one exact supported checksum candidate. Zero/multiple candidates,
unsupported checksums, missing/malformed/over-budget inputs, CodeView mismatch,
ambiguous assembly binding, unacceptable compiled provenance, and reader
disagreement remain explicit Tier4 gaps.

The v3 public fixture catalog adds six PDB cases. C# proves hidden,
multi-document, non-monotonic, async, iterator, lambda, and separate generated
member shapes; VB proves exact portable document/method evidence; F# retains
compiled PDB facts while emitting
`PdbSourceReconciliationUnsupportedLanguage` and zero guessed source joins.
Duplicate exact source bytes prove the multiple-checksum-candidate gap without
path selection. Focused CLI tests retain PDB provenance and endpoints across
the manifest, facts, SQLite index, Markdown report, and execution receipt and
compare deterministic repeat output.

Native Windows PDBs remain explicitly unsupported rather than trusting
Mono.Cecil as a sole oracle. Windows emits
`WindowsPdbIndependentReaderUnavailable`; non-Windows hosts emit
`WindowsPdbRequiresWindows`. The Windows CI lane builds real C# and VB native
PDBs and proves zero positive PDB facts for that bounded gap. Positive native
Windows PDB reading is deferred until an independent deterministic reader can
cross-check the result. Task 9 does not begin IL body/call extraction, rewrite
analysis, private or `dotnetperf` corpus execution, legacy Framework/Web Forms
build validation, or C++/CLI.

Final local macOS Task 9 validation on 2026-09-21:

- focused `PortablePdbExtractorTests`: 29 passed, zero failed, zero skipped;
- combined PDB, source/metadata reconciliation, managed metadata, and receipt
  contract filter: 89 passed, zero failed, zero skipped;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero warnings
  and zero errors;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,030 passed, zero failed,
  zero skipped;
- two bound C# CLI scans emitted all five required scan artifacts plus the
  execution receipt; `facts.ndjson` and `report.md` were byte-identical, PDB
  provenance and bounded endpoint summaries were identical, and SQLite retained
  exact source/target identities with supporting document/method fact IDs;
- `scripts/check-private-paths.sh`: passed;
- `node scripts/kiro-review.mjs --self-test`: passed; and
- `git diff --check`: passed.

The consolidated review correction enforces three subsystem-wide invariants:
resource admission occurs before retention or expensive work; positive evidence
requires a complete deterministic support chain with truthful coverage and
input commitments; and format/assembly binding is exact across primary and
dependency inputs. Sibling hardening adds streaming single-pass source checksum
indexes with explicit file/byte/work bounds, incremental SRM and Cecil work
accounting, exact native MSF classification, bounded expected-input receipts,
source-snapshot-bound endpoint summaries with direct file/line/commit context,
zero sequence-point facts without a one-candidate metadata-method edge, work
charges for every SRM/Cecil method-row inspection, and cancellation polling
through source checksum streaming.

An earlier exact-head review found two sibling violations of the same
admission/work-bound invariant. PDB assembly binding now consumes only exact
artifacts retained by the compiled evaluator's admitted prefix, so an omitted
byte-identical assembly cannot reenter matching or manufacture an ambiguous
candidate. Cecil type traversal now reuses the iterative managed-metadata
walker and charges every type before its methods, so deeply nested empty types
cannot overflow the stack or evade the PDB work budget. The regression matrix
covers the omitted duplicate, 10,000 nested empty types, and type-budget
exhaustion before any method visit.

The next settled three-finding review batch exposed two independent
post-admission invariants. First, exact method and document joins must be
linear in the bounded evidence set: compiled methods are indexed once by
assembly locator and MethodDef token, and PDB documents are indexed once by
row ID before sequence-point facts are emitted. Second, PDB input identity is
the normalized path under the checkout filesystem's case semantics, not the
raw option spelling. The regression matrix covers zero/one/multiple method
candidates, a same-token member in another assembly, multi-document
sequence-point support IDs, and both present and missing PDB aliases; an
aliased positive CLI scan completes without duplicate fact IDs.

The subsequent exact-head Codex batch exposed one shared resource-admission
invariant across two findings: compiled binding discovery must be cancelable
without retaining all admitted PE byte arrays. The consolidated correction
keeps only path, admitted digest, and CodeView identities, then boundedly
rereads and rehashes the unique matched assembly before Cecil comparison. A
changed, missing, oversized, or unreadable match fails closed with
`PdbCompiledArtifactChangedOrUnreadable`. The same cancellation polling applies
to PDB file reads. Regression matrix:

| Case | Expected evidence |
| --- | --- |
| Normal bound C#/VB/F# fixture | Existing exact PDB/metadata facts remain unchanged in shape. |
| Changed, missing, or oversized matched PE on reread | Verification returns no bytes; no positive PDB evidence may use it. |
| Cancellation before compiled binding admission | `OperationCanceledException`, not a PDB gap. |
| Cancellation after a bounded input read chunk | `OperationCanceledException` before the next chunk or positive evidence. |

Finding URLs: https://github.com/joefeser/tracemap/pull/774#discussion_r4062850503
(PE byte retention) and
https://github.com/joefeser/tracemap/pull/774#discussion_r4062850521
(cancellation during binding reads).

The next settled exact-head batch has three findings with two independent
correctness invariants. The input provenance digest must describe only its
bounded PDB/assembly admission inputs; source and method reconciliation may
change final coverage, but not the provenance object or its commitment.
`PdbEvidenceSummary` carries the final source-bound coverage and digest, and
the report displays that final coverage separately from input coverage. The
second invariant is linear, charged post-admission evidence work: the bounded
summary retains only its configured prefix and incrementally hashes omitted
entries in canonical order; independent reader shapes are compared as exact
duplicate-sensitive counts, charging each comparison rather than sorting.
The PDB regression matrix now includes:

| Case | Expected evidence |
| --- | --- |
| Same bound PE/PDB, exact then changed source bytes | Input provenance object and digest unchanged; source-bound summary digest changes and unmatched source emits a gap. |
| Bounded summary with omitted endpoints | Retained prefix only; incremental omitted digest equals canonical JSON array digest, including zero-omission case. |
| Same shapes in different order, duplicate mismatch | Order-independent exact agreement; duplicate mismatch disputes reader evidence. |
| Shape comparison exceeds remaining work units | `PdbInputTotalWorkLimitExceeded` before positive facts. |

Finding URLs: https://github.com/joefeser/tracemap/pull/774#discussion_r4063129385
(input provenance commitment),
https://github.com/joefeser/tracemap/pull/774#discussion_r4063129409
(summary allocation), and
https://github.com/joefeser/tracemap/pull/774#discussion_r4063129423
(shape comparison work).

The next exact-head batch exposed one shared candidate-completeness invariant:
an exact-one join must count every eligible source path and every matching
CodeView directory entry before accepting a positive edge. The checksum index
now uses the existing C#/VB inventory-kind classifiers; F# remains explicitly
unsupported for source reconciliation, so specialized source kinds cannot silently
disappear. CodeView identities retain duplicate entries; two matching entries
within one PE or matching entries across two admitted PEs are ambiguous. The
audit found no other candidate-thinning PDB path: normalized PDB path aliases
identify one physical input, supported checksum algorithms are capabilities
rather than candidate identities, and metadata/source candidate lists retain
multiplicity. No legacy parser or source adapter was added. Regression matrix:

| Case | Expected evidence |
| --- | --- |
| Existing C#/VB/F# source-kind matrix | All C#/VB inventory source kinds are checksum candidates; non-source markup/Razor are excluded; F# still has no guessed source join. |
| Two same-checksum specialized C# or VB files | Exact multiple-candidate gap with count 2 and zero selected source edge. |
| One, zero, or duplicate matching CodeView identities in one PE | Counts 1, 0, or 2 without collapsing identical entries. |
| Two admitted same-CodeView PEs | `AmbiguousPdbAssemblyMatch` and zero positive PDB facts. |

Finding URLs: https://github.com/joefeser/tracemap/pull/774#discussion_r4063521136
(source-kind candidate completeness) and
https://github.com/joefeser/tracemap/pull/774#discussion_r4063521146
(CodeView entry multiplicity).

PR #774 implementation-head CI at
`34ded8aef9b60b6f7db225d1c8af5df6b96ea159`
passed the .NET, JVM, Python, Swift, TypeScript, five-adapter combine, private
path, and package-smoke jobs on macOS, Ubuntu, and Windows. The Windows lane
used desktop Roslyn C# and VB compilers to produce real MSF PDBs, recognized the
complete native signature, emitted only the bounded unsupported-reader gap, and
produced zero positive native PDB facts. This is not a native-Windows support
claim. The Task 9 checkbox is complete. PR #774 then completed its authorized
exact-head ACK review terminal state and was merged into `dev` as
`ed2fdf1c028b034a9a6013908e8f5b8c29d900a7` on 2026-09-21 (merge commit
confirmed against `origin/dev`); the earlier "final current-head ACK pending"
note is closed and no longer describes repository state.

## Task 10 first slice: bounded operand-aware IL body and call evidence

Branch: `codex/il-body-call-evidence` from `origin/dev` at
`ed2fdf1c028b034a9a6013908e8f5b8c29d900a7`. Tracking #766.

The first Task 10 slice activates `dotnet.compiled.il-body.v1`,
`dotnet.compiled.il-call.v1`, and `dotnet.compiled.il-gap.v1` behind the
explicit `--il-body-evidence` flag with `--il-max-bodies`,
`--il-max-instructions-per-body`, `--il-max-locals-per-body`,
`--il-max-exception-regions-per-body`, `--il-max-text`, and `--il-max-work`
limits. The lane is inert without the flag: no IL facts, no
`ilBodyProvenance` manifest section, and no change to source, compiled
metadata, or PDB behavior.

The canonical body identity is
`<exact metadata method identity>|il-body:instructions:<n>:sha256:<digest>`
where the digest commits the full operand-aware encoding: opcodes with
resolved direct-call target identities and module-local tokens, branch and
switch target offsets, string-literal digests (never verbatim literals),
numeric constant bit patterns, variable indexes, raw non-call token operands,
ordered local signatures, exception-region boundaries with catch-type
identities and filter offsets, and max stack. The public C# fixture proves
that identical opcode streams with different member, string, constant, or
branch-target operands produce distinct identities, that `Twice(int)` and
`Twice(long)` share a body digest yet stay distinct identities, that an
identical trivial body in the C# and VB fixtures stays distinct, and that
call/callvirt/newobj/ldftn/MethodSpec/interface targets carry exact scoped
reference identities. F# and VB fixtures add minimal cross-language bodies.

Mono.Cecil is not the sole oracle: a System.Reflection.Metadata single-pass
raw-IL reader rebuilds the complete canonical encoding independently, and any
difference in assembly/module identity, method identity, body digests, call
sites, locals, regions, or max stack withholds the input behind
`IlReaderDisagreement`. Non-call token operands (field/signature tokens) are
committed by raw token only and their member identities are not resolved or
promoted in this slice. Bodyless methods (abstract, external, PInvoke) emit no
body fact as a structural observation. Manifest and execution receipt retain
`il-body-provenance.v1` with generator SHA-256, canonical bounded-input
SHA-256, effective limits, and per-input outcomes; the digest participates in
`scanId`; the report gains a bounded IL evidence section. A requested lane
with no admitted compiled input emits the `IlCompiledEvidenceUnavailable`
Tier4 gap, never a clean absence.

Explicitly deferred to later Task 10 slices: rewritten-member identity,
metadata-token retargeting, rewritten PDB offsets, ILAsm/ILDAsm parity, and
the extended ECMA-335/rewrite mutation matrix from #766. Task 11's legacy
Windows, `dotnetperf`, and C++/CLI lanes remain entirely separate. The Task 10
checkbox stays open until the public rewrite suite and its acceptance criteria
land.

Local macOS validation on 2026-09-21 (after review remediation `5c047abd`):

- focused `IlBodyEvidenceExtractorTests`: 29 passed, zero failed, zero skipped;
- neighbor suites: `ManagedMetadataExtractorTests` + `PortablePdbExtractorTests`
  + `SourceMetadataReconciliationTests`: 91 passed, zero failed, zero skipped;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero
  warnings and zero errors;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,080 passed, zero
  failed, zero skipped (one early concurrent run showed two transient
  failures that did not reproduce on the immediate clean rerun);
- three-language CLI scan (C# primary, VB and F# dependencies) with
  `--il-body-evidence`: `il-complete`, all five artifacts plus the execution
  receipt present, byte-identical `facts.ndjson` and `report.md` on repeat,
  154 IL body and 125 IL call rows in `facts.ndjson` and `index.sqlite`;
  `scripts/validate-adapter-artifacts.py` passed and no output contained a
  local absolute path or an IL string literal;
- `scripts/check-private-paths.sh`: passed;
- `node scripts/kiro-review.mjs --self-test`: passed; and
- `git diff --check`: passed.

Review remediation on 2026-09-21: the exact-head review batch (Codex plus the
Qodo single return) filed four findings on head `5e0e4464`; all are patched in
`5c047abd`. Switch jump-table targets are computed from the shared post-table
base with overflow-safe extent validation and per-target work charges; the raw
System.Reflection.Metadata reader now runs before Mono.Cecil materializes
operands; `--il-max-text` is enforced for user strings, target identities,
method identities, and body identities in both readers; and the `constrained.`
prefix now emits a cross-checked `constrainedtype` call observation whose
module-local token stays explicitly unclaimed because Mono.Cecil cannot
reproduce the raw TypeSpec token. A genuine five-target `switch` fixture,
hostile truncated and oversized switch-table tests, a text-limit gap test, and
a constrained-call assertion cover the remediation; CI is green on macOS,
Ubuntu, and Windows including the Windows cross-volume `--out` fix in
ScanOutputTransaction. All review threads are resolved and the stale Qodo
summary finding is dispositioned. The earlier ACK loop had stopped at
`CURRENT_HEAD_REQUIRED_REVIEW_MISSING` with `owner_decision_required`; the
owner subsequently issued the exact-head review authorization, and the
resulting findings were patched and merged. PR #775 was merged into `dev` as
`46b2baa125bed00ee9ac1964ce50febd55c3e68e` on 2026-09-21 (merge commit
confirmed against `origin/dev`). The Task 10 checkbox remains open for the
public rewrite suite.

## PR #775 owner-requested P1/P2 review follow-up

Reviewed head `1ae5564921852e0e780f68839b04fde525a5adca` on 2026-09-21.
Two P2 defects reproduced in six regression cases before patching:

- Valid `unaligned.` operands (alignments 1, 2, and 4) are boxed as unsigned
  bytes by Cecil. Casting every ShortInlineI operand to `sbyte` threw an
  uncaught `InvalidCastException` and aborted the entire scan. Both readers
  now distinguish signed `ldc.i4.s` constants from unsigned prefix bytes.
- `Encoding.Unicode` replaced unpaired surrogates with U+FFFD before hashing,
  assigning identical instruction/body identities to distinct string
  operands. Hashing now serializes exact little-endian UTF-16 code units.
  Regression pairs cover high surrogates, low surrogates, and U+FFFD.

Validation: all 41 focused IL tests and all 2,092 solution tests passed;
the solution build had zero warnings and errors. A three-language CLI smoke
scan against `samples/modern-sample` produced `il-complete`, 155 body facts,
and 126 call facts; adapter artifact validation passed. The private-path
guard, Kiro review self-test, and whitespace check passed. Local validation
is macOS only; hosted checks and exact-head review remain ACK's authority.
The Astra follow-up `2d20b4a3` (prefix operands and exact UTF-16 hashing)
landed before merge and is part of merge commit `46b2baa1`; those regression
guarantees must be preserved by later Task 10 slices. Task 10 remains open
for the deferred rewrite suite, and Task 11 is unchanged.

## Task 10 second slice: bounded before/after IL rewrite identity evidence

Branch: `codex/task10-rewrite-evidence` from `origin/dev` at
`46b2baa125bed00ee9ac1964ce50febd55c3e68e` (PR #775 merge). Tracking #766.

The second Task 10 slice activates `dotnet.compiled.il-rewrite.v1` and
`dotnet.compiled.il-rewrite-gap.v1` behind the explicit
`--il-rewrite-evidence` flag with ordinal `--il-rewrite-before` and
`--il-rewrite-after` inputs and `--il-max-rewrite-pairs`. The scanner never
performs or attributes a rewrite: both sides are operator-declared bounded
inputs, each admitted under the compiled file-size/text bounds with
privacy-projected external locators and raw SHA-256 commitments, and each
side must independently pass the complete first-slice dual-reader IL body
contract before any join. A missing, unreadable, oversized, malformed,
disputed, unsupported, or over-limit side withholds the whole pair behind a
Tier4 gap with `side`/`cause` properties and no partial edge set.

An edge is emitted only when the complete exact assembly-scoped method
identity text occurs exactly once on each side. The edge records both
assembly identities, both module-local tokens with `tokenRetargeted`, both
canonical body identities and digests, the relationship kind (`unchanged`,
`operand-only-change`, or `instruction-stream-change`), and
`opcodeSequencePreserved`. Call-site retargets (both tokens, both target
identities, both IL offsets, ordinal alignment) are recorded only when
instruction counts and opcode-name digests are exactly equal; the shared
opcode-name digest computed by both readers proves the alignment. One-side-only
membership emits bounded `IlRewriteMethodBeforeOnly`/`IlRewriteMethodAfterOnly`
gaps (eight retained identities per side plus an omitted-count digest
commitment), never guessed insertion/removal edges; duplicate identity on a
side emits `IlRewriteIdentityAmbiguous`; differing assembly identities emit
`IlRewriteAssemblyIdentityMismatch` with no joins; join work is charged to
the shared `--il-max-work` budget and exhaustion emits
`IlRewriteTotalWorkLimitExceeded`. Requesting the flag without pairs emits
`IlRewritePairUnavailable`; count mismatches emit
`IlRewritePairDeclarationInvalid`; the CLI additionally rejects unflagged or
unpaired declarations. The lane is inert without the flag: no rewrite facts,
no `ilRewriteProvenance` section, no known gaps, and unchanged source,
compiled-metadata, PDB, and IL body/call behavior (the focused suite pins
non-rewrite facts byte for byte against a baseline scan with normalized
derived fact IDs). The manifest, execution receipt, and report gain
`il-rewrite-provenance.v1` with generator SHA-256, canonical bounded-input
SHA-256, effective limits, and per-pair outcomes; the digest participates in
`scanId`; artifact visibility stays local-only and outputs never contain raw
absolute paths.

Mono.Cecil generates the deterministic synthetic mutations only inside the
public test suite: changed `ldc.i4` constant operand (same opcode), a member
inserted before existing MethodDef rows (token renumbering with identical
target identity), a rewired call operand to a different existing member,
duplicated complete method identity on the after side, renamed assembly
identity, corrupted after-side IL, and exhausted budgets. Identical
before/after inputs must prove every body `unchanged` with zero gaps. The
fixture catalog moves to `compiled-dotnet-fixture-cases.v5` with a new
`ilRewriteCases` section recording stable IDs, expected rules/tiers,
relationship kinds, gaps, and non-claims.

Local macOS validation on 2026-09-21:

- focused `IlRewriteEvidenceExtractorTests`: 23 passed, zero failed, zero
  skipped;
- focused `IlBodyEvidenceExtractorTests`: 41 passed, zero failed, zero
  skipped (opcode-name digest addition keeps all first-slice guarantees,
  including the Astra prefix-operand and UTF-16 regressions);
- combined compiled-lane filter (IL body, rewrite, managed metadata, PDB,
  source reconciliation): 155 passed, zero failed, zero skipped;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: zero warnings, zero
  errors;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,115 passed, zero
  failed, zero skipped;
- three-language CLI smoke (C# primary with mutated after side, VB and F#
  identical after sides, `--il-body-evidence --il-rewrite-evidence`):
  `rewrite-complete`, 155 rewrite edges (110 unchanged, 45
  operand-only-change), 51 call-site retargets showing Cecil round-trip token
  renumbering with identical target identities, zero gaps; all five artifacts
  plus `scan-receipt.json` present; repeat scan produced byte-identical
  `facts.ndjson` and `report.md` and an identical manifest except
  `scannedAt`; 206 `dotnet.compiled.il-rewrite*` rows in `index.sqlite`;
  `scripts/validate-adapter-artifacts.py` passed and no output contained a
  local absolute path;
- `scripts/check-private-paths.sh`: passed;
- `node scripts/kiro-review.mjs --self-test`: passed; and
- `git diff --check`: passed.

Explicitly deferred by this slice and still open for later Task 10 work:
rewritten PDB offsets and sequence-point validity, ILAsm/ILDAsm parity,
evaluation-stack-sensitive rewrites, netmodules, type forwarding, duplicate
assembly identities, insertion/removal relationship edges, and the extended
ECMA-335 mutation matrix from #766. Task 11's legacy Windows, `dotnetperf`,
and C++/CLI lanes remain separate. The Task 10 checkbox stays open until
#766's full public rewrite-suite acceptance is met.

Review remediation on 2026-09-21: the Qodo review of PR #776 head
`1d41d3af3dcac554b693db7106c40ab75b9c89ab` filed four findings; all are
patched. Join-phase budget exhaustion is now atomic (no partial edges,
membership deltas, or gap kinds survive), malformed declared paths fail
closed to `IlRewriteSideUnavailable` with cause
`IlRewriteSideDeclarationInvalid` and a privacy-projected locator instead of
aborting the scan, repeated identical pair declarations keep their own
ordinal outcomes without dedupe, and the bounded-input digest commits the
effective `CompiledInputLimits` admission policy. Four regression tests
cover the behaviors. Local re-validation: focused
`IlRewriteEvidenceExtractorTests` 27 passed; build zero warnings/errors.

Second review remediation on 2026-09-21: the Codex review of PR #776 filed
two P1 findings and one P3; all are patched. Omitted overflow pairs now
commit their declared privacy-projected locators to the synthetic outcome
and therefore the bounded-input digest, so scans omitting different declared
pairs never share a rewrite provenance digest or scan identity. The
relationship classifier no longer labels non-instruction structural body
changes (locals, exception regions, max stack, init-locals) as
`operand-only-change`: those are `body-structure-change`, combined operand
and structural changes are `operand-and-body-structure-change`, and the rule
catalog, fixture catalog (`CS-ILRW-STRUCTURE-009`), and docs document the
exact-kind contract. `tracemap scan --help` documents the four rewrite
options including the equal-length ordinal pair requirement. Three
regression tests cover the behaviors (30 focused rewrite tests total).

Third review remediation on 2026-09-21: the Codex re-review of PR #776 head
`49ee4db970985faf8698ccc29c1b6a379663a4f4` filed five P2 findings; all are
patched. Side-scoped failures now emit one Tier4 gap per failing side with
that side's own `side`, `cause`, and evidence locator, and the outcome
summary preserves the exact `side:cause` pairing. The omitted-membership
digest commits exactly the identities beyond the retained eight-entry prefix
so consumers can recompute it from reader-proven rows. Positive edges and
call retargets always keep the `managed-il-rewrite-v1` evidence location
kind even when the same pair carries gaps. Pair outcome labels are exact
(`unavailable`, `invalid`, `unsupported`, `membership-delta`) instead of a
generic malformed/limit fallback. The lane validates the reused body and
compiled limits identically to their owning extractors. Six regression tests
cover the behaviors (35 focused rewrite tests total).

Fourth review remediation on 2026-09-21: a follow-on Codex P2 on head
`80c0b4ba19b3fdd4c28e4944f9b1d4bb37856b2c` noted that after-only membership
gaps still used the before locator; one-side-only membership gaps are now
evidenced on the input that carries the identity, pinned by both the
inserted-member and omitted-suffix regressions.

Fifth review remediation on 2026-09-21: the Codex re-review of head
`950390728d6b6b59f16fed335de8e9b39ad434e9` filed one P1 and one P2; both are
patched. Admission side/cause mappings now participate in the canonical
bounded-input digest projection, so an in-repo locator that changes only from
missing to over-limit (identical gap kind, outcome, locator, and null raw
digest) still changes the provenance digest and scan identity. Blank declared
pair slots invalidate the whole declaration instead of being silently
dropped, which had shifted ordinal pairing. Two regressions pin the
behaviors (37 focused rewrite tests total).

Full independent PR #776 review on 2026-09-21, from exact head
`bff7413e50981d76d438a351c1411bb5ca59e1dc`, on local branch
`codex/pr776-full-review` (pushed back to `codex/task10-rewrite-evidence`):

- P1: rewrite sides bypassed the compiled-input shape preflight, admitting
  mixed-mode IL and reporting complete coverage for multi-module manifests.
  Both sides now pass the shared preflight before either body reader, with
  categorical unsupported gaps and no pair edges.
- P2: compiled metadata row/work limits were committed but not enforced on
  rewrite inputs. Preflight now enforces type/member row bounds and charges
  the compiled metadata work budget across both sides and every pair. Pair
  count remains governed by the explicit rewrite-pair limit.
- P2: rejected pair declarations committed counts but lost projected paths
  and blank positions. Their commitment now includes both ordered projected
  slot lists, preserving the fail-closed rejection without identity aliasing.
- P2: receipt scope hashing sorted each side independently, so different
  ordinal pairings shared an authorized-scope fingerprint. Ordered JSON
  framing now retains slot order, blanks, duplicates, and embedded newlines.
- P2: a file growing beyond its byte bound between the size precheck and
  bounded read could throw out of the scan. The admission boundary now
  translates that bounded-reader exception into the side's categorical gap.

Six new regression cases were run against the original head and all failed,
then passed with the fixes. A seventh regression pins the shared metadata
budget across sides and pairs. Focused body/rewrite/metadata/receipt tests
passed (125 tests before adding the seventh case). Existing Task 10 deferred
scope and checkbox remain unchanged; this review does not complete #766.

Validation: repeated C#/VB/F# CLI scans passed the artifact validator with
155 unchanged rewrite edges, zero rewrite gaps, matching SQLite/receipt
provenance, byte-identical facts/report, and manifests identical except
`scannedAt`. The private-path guard, Kiro self-test, and diff whitespace check
passed. Pinned public OSS source-adapter smokes remain deferred under the
compiled-lane guidance in `docs/VALIDATION.md`; the synthetic compiled and
full solution suites are the validation surfaces for these changes.

Final local validation: `dotnet test src/dotnet/TraceMap.sln --no-restore`
passed 2,136 tests, zero failed/skipped, with no compiler/analyzer warnings
or errors in the build/test output. The final ACK readback is recorded in
`.agent-control/pr-loop-handoffs/pr-776-full-review.json` in the review
worktree; local review and test results do not substitute for hosted-review
freshness or authorize a merge.

## Task 10 third slice: public messy .NET workspace regression (begin-work note)

Started 2026-09-21 on branch `codex/task10-messy-workspace-regression`
cut from `origin/dev` at `bdbc31cabd75a5021fd47c1ac3ea1db116183e01` (post #776,
post #777). PR #776 is merged into `dev`; this slice does not build on its
feature branch.

Scope decision: build the first bounded, public-safe "messy .NET workspace"
regression slice under `samples/messy-dotnet-workspace/` that reproduces the
failure shapes observed in real Web Forms/.NET scans using synthetic code
only — no private source, names, paths, or artifacts. A stable case catalog
(`case-catalog.json`, schema `messy-workspace-case-catalog.v1`) records the
eventual matrix with implemented/deferred status; deferred cases carry their
exact blocker or next-slice owner.

Implemented subset for this PR (all ordinary-CI portable):

- `MW-DEEP-CHAIN-D10-001`: 14-hop handler chain with a supported SQL terminal
  beyond depths 8 and 10; terminal inventory must stay complete with no false
  absence from depth truncation.
- `MW-CYCLE-001` and `MW-CYCLE-SELF-002`: 3-node call cycle plus self-cycle;
  traversal must terminate and surface cycle truncation honestly.
- `MW-SAME-NAME-TEN-001`: the same method names in ten classes in one file;
  identities must stay container-distinct and must not cross-join.
- `MW-MERGED-ROOTS-001`: two C# roots scanned separately, combined with
  labels, reviewed as one merged report; no invented cross-source joins.
- `MW-VB-PROJECTLESS-001`: loose projectless VB files (no `.vbproj`/`.sln`)
  scanned through the portable Roslyn syntax fallback. No blocker exists:
  `VisualBasicSyntaxExtractor` is deterministic on macOS/Linux CI and pinned
  by existing tests, so this case is implemented rather than deferred.
- `MW-FOLDER-SPREAD-001`: source spread across nested folders inside each
  root and across independently scanned roots.

Deferred cases and owners: overload ambiguity and receiver ambiguity (next
fixture slice; identity retention is already pinned by compiled-lane
`CS-OVERLOAD-001`/`VB-OVERLOAD-001` and receiver fixture tests), C#/VB/F#
cross-language boundaries (no F# source adapter exists; cross-language
identity needs the compiled lane), generated members (compiled-lane
`CS-GENERATED-005` covers identity labeling; traversal variant deferred), and
source → metadata → IL/PDB identity on messy roots (owned by the remaining
#766 ILAsm/rewritten-PDB matrix, explicitly not this slice).

Failure identification contract for the new tests: every assertion names the
catalog case ID and the pipeline stage — `extraction`, `combining`,
`reconciliation`, or `traversal`. Determinism contract: repeat scans of each
root must produce byte-identical `facts.ndjson`; the messy-workspace slice
adds no new derived machine-readable artifact (no new schema), so generator /
bounded-input hash pinning stays with the existing manifest provenance rather
than a new digest artifact.

Task 10's checkbox stays open regardless of this slice's outcome; #766's
remaining ILAsm/rewritten-PDB matrix and Task 11's private Windows/`dotnetperf`
lane remain out of scope.

Task 10 third slice delivered 2026-09-21 on
`codex/task10-messy-workspace-regression`:

- `samples/messy-dotnet-workspace/` with three synthetic roots
  (`root-alpha`, `root-beta`, `vb-projectless`), a README, and the stable
  case catalog `case-catalog.json` (`messy-workspace-case-catalog.v1`, 7
  implemented / 5 deferred cases with exact blockers).
- `MessyWorkspaceRegressionTests` (8 tests): catalog conformance, folder
  spread, deep chain beyond depth 10, cycles, ten same-name members, merged
  roots without invented joins, projectless VB, and repeat-scan
  byte-identical `facts.ndjson` per root. Every assertion names its case id
  and pipeline stage.

Two fixture-shape decisions recorded for future slices:

- The deep chain ends in an ADO.NET-style `ExecuteReader` call pattern over a
  stubbed `System.Data.Common.DbCommand` (the established stub-a-namespace
  fixture pattern). SQL string literals alone attach their
  `QueryPatternDetected` evidence to bare containing-method names; on
  semantic C# scans those bare names reconcile only when unambiguous, which
  the ten-same-name engines case deliberately pressures. The call-pattern
  terminal attaches with the exact compiler identity and is the battle-tested
  attachment path.
- The deep chain pins the documented retained-closure contract: at
  `--max-depth 10` a terminal at traversal distance 12 is inventoried with
  complete reachability and honest `depth` truncation, while at depth 8 it
  falls outside the depth-bounded retained symbol closure and the observation
  scopes its completeness claim to the retained graph (a longer 16-hop chain
  was verified to surface the same boundary at depths 8-12 before the fixture
  was fixed at ten steps; widening that closure or adding an explicit
  beyond-retained-closure gap kind is candidate follow-up scope, not a defect
  fix in this slice).

Validation on this branch: messy-workspace focused lane 8/8; neighboring
WebFormsModernizationPacket/Combine/projectless-VB/VisualBasicWebFormsComposition
suites 63/63; full `dotnet test src/dotnet/TraceMap.sln --no-restore`
2,144/2,144, zero failed/skipped; all four PowerShell Web Forms regression
scripts PASS; `samples/modern-sample` CLI scan passed
`validate-adapter-artifacts.py` plus its self-test; the private-path guard
and `git diff --check` passed; `dotnet build src/dotnet/TraceMap.sln` reports
0 warnings/0 errors; `node --test scripts/pr-review-loop-lane.test.mjs` 3/3.
The Task 10 checkbox remains open; #766's ILAsm/rewritten-PDB matrix and
Task 11's private Windows/`dotnetperf` lane are untouched.

Review remediation on 2026-09-22 (ACK 0.5.2 loop, Qodo + Codex exact-head
findings on `8f86147f`): the deep chain was lengthened from ten to twelve
call edges so the terminal at graph distance 14 is unambiguously beyond the
configured depth (inventory pinned at depths 12/16; retained-closure boundary
pinned at depth 10); the self-cycle got its own page handler and its own
traversal assertions under MW-CYCLE-SELF-002; same-name isolation now rejects
any cross-engine semantic edge and correlates each engine boundary to its own
terminal fact and class line range instead of a file-subset check; catalog
conformance now consumes expectedRuleIds against rules/rule-catalog.yml plus
expectedTiers/expectedGaps vocabularies; the commit-SHA assertion accepts
SHA-1 (40) and SHA-256 (64) hex; the scan helper is synchronous (CS1998); and
both fixture projects are registered in the test build graph via
`ReferenceOutputAssembly="false"` ProjectReferences (the compiled-evidence
precedent) with the deliberate `DbCommand` shadowing suppressed by a
documented fixture-local `NoWarn CS0436`.

Second review remediation on 2026-09-22 (Codex exact-head re-review of
`9b81303c`): the folder-spread test is synchronous (no async without await),
and the merged-roots rejection now validates complete (source label, caller,
callee) tuples for every Process call edge instead of callee presence alone.
All eleven review threads (five Qodo inline, four Codex inline, one Codex
follow-up pair) are settled with durable ACK dispositions at
`d11f35e5546ba74a7eab30d6a4115ed7a0740411`; the stale Qodo summary carries a
posted disposition. Final ACK readback: unresolved threads 0, actionable
findings 0, stale findings 0, pending/failed checks 0, merge state CLEAN;
decision `not_merge_ready` with `CURRENT_HEAD_REQUIRED_REVIEW_MISSING` — the
exact-head hosted-review freshness gate at `d11f35e5` is an owner decision
and local validation does not substitute for it. All CI checks pass on the
final head. No manual bot retagging, force-push, or merge was performed.


Full PR #778 review follow-up (2026-09-22, based on `1a196ea3`): two P2
regression-proof defects were confirmed and corrected; no new P1 was found.
The merged-roots test previously accepted a wrong caller within the same
namespace and replacement of `engine_10_queue` with `engine_09_queue` because
it checked broad namespace/table substrings. Temporary corruption of the
combined index reproduced both false passes together; after replacing those
checks with exact original-scan tuples, each corruption independently fails
at reconciliation. The injected mutations were removed from the final code.

Catalog validation previously checked vocabulary only, allowing declared rules,
tiers, and gaps to diverge from produced evidence. Each implemented case now
consumes its catalog expectations against actual scan, packet, or combined-edge
evidence. The merged-root expectation is corrected to Tier1 semantic callgraph
evidence: this case preserves separate-source identities and does not produce
`combined.paths.symbol-reconciliation.v1`. Its Process identity count is eleven.
The scanner/reducer behavior and open Task 10 scope remain unchanged.

Validation of the completed follow-up: focused suite 8/8; final full .NET suite
2,144/2,144, zero failed/skipped; solution build zero warnings/errors. The modern
sample CLI scan produced Level1SemanticAnalysis and passed artifact validation;
validator self-tests passed 7/7, lane tests 3/3, private-path guard and
`git diff --check` passed. Restoring the old merged-root catalog rule separately
fails the new produced-evidence assertion. The temporary catalog mutation was
also removed before final validation.

## Task 10 fourth slice: bounded rewrite PDB identity evidence

Branch: `codex/task10-ppdb-rewrite-identity` from `origin/dev` at
`381f93db88156d941efc22a9a39cb470083599f1` (PR #778 merge). Tracking #766.
The slice proves what happens to Portable PDB method and sequence-point
identities when an admitted method body is rewritten; it was inspected
against the existing source→metadata, PDB, IL-body, rewrite, and
messy-workspace contracts before any contract changed, and the two new rule
IDs (`dotnet.compiled.il-rewrite-pdb.v1`,
`dotnet.compiled.il-rewrite-pdb-gap.v1`) were documented with limitations in
`rules/rule-catalog.yml` before any fact was emitted.

`dotnet.compiled.il-rewrite-pdb.v1` activates behind
`--il-rewrite-pdb-evidence` (requires `--il-rewrite-evidence`) with ordinal
`--il-rewrite-pdb-before`/`--il-rewrite-pdb-after` declarations that must
align with the declared assembly pairs, plus
`--il-rewrite-pdb-max-{file-bytes,documents,methods,sequence-points,text,work}`
limits. Each PDB side binds only its own paired assembly through the exact
portable content GUID/stamp against the CodeView entries of the re-read,
re-hashed assembly bytes; duplicate entries, cross-side matches, and changed
or unreadable matched assemblies fail closed. Both sides independently
satisfy the standalone PDB dual-reader contract (SRM observations
cross-checked against Cecil shape counts), every PDB method row must map to
a dual-reader-proven body on its own side, and every sequence-point IL
offset must fall inside that body's proven extent
(`IlRewritePdbMethodRowInconsistent` otherwise). Positive
`ManagedIlRewritePdbObserved` facts record the original and rewritten
member identity, both body identities and operand-aware digests, both PDB
method identities, both content ids, per-side sequence-point digests, the
parent rewrite fact id, and the exact offset classification
(`sequence-point-offsets-unchanged` iff the ordered IL offset vectors are
equal). The classification compares IL offset vectors only and never claims
behavioral equivalence, source ownership, preserved debugging behavior, or
rewrite attribution. One-side-only debug information emits a bounded
`IlRewritePdbMethodDebugInformationAbsent` gap (8 retained identities plus an
omitted-suffix digest); neither-side methods emit nothing. The lane is inert
without the flag — a scan declaring the PDB lists without it keeps an
identical scan identity, rewrite digest, and fact bytes (pinned by test).

Mono.Cecil 0.11.6 generates the deterministic after sides inside the public
test suite by reading the compiler-produced
`samples/compiled-dotnet-evidence/csharp` fixture with portable symbols and
writing mutated assembly+PDB pairs; Cecil coordinates the written PDB content
id with the after assembly's CodeView entry (verified empirically and pinned
by the positive tests), so pairs bind by construction. The before side is
the genuine deterministic compiler pair, so positive evidence spans a real
cross-writer identity divergence. System.Reflection.Metadata independently
reads PDB observations, CodeView entries, and body extents; Cecil is never
the sole oracle. The fixture catalog moves to
`compiled-dotnet-fixture-cases.v6` with a 13-entry `ilRewritePdbCases`
section (11 implemented shapes, 2 deferred with exact prerequisites).
Notable identity finding pinned by the tests: an operand-only rewrite leaves
the PDB byte-identical (same content id as a pristine rewrite), and the
Cecil-written CodeView entry contains the local output path — the lane uses
GUID/stamp only and never emits the path.

ILAsm/ILDAsm parity was assessed on 2026-09-22 and deferred
(`ILRWPDB-ILASM-PARITY-012`): absent from PATH, the .NET SDK 10.0.201
installation, and the NuGet cache; Homebrew bottles mono 6.14.1 but it was
not installed, and no pinned ILAsm toolchain exists in ordinary CI.
Prerequisites: Windows SDK/Visual Studio `ilasm.exe`+`ildasm.exe` on the
Windows CI lane, or a pinned mono/dotnet-runtime ILAsm build on macOS/Linux,
plus an independent disassembly oracle. Embedded portable PDBs are likewise
deferred (`ILRWPDB-EMBEDDED-PORTABLE-013`). No ILAsm/ILDAsm or Windows PDB
parity claim is made from Cecil-based tests.

Local macOS validation on 2026-09-22:

- focused `IlRewritePdbEvidenceExtractorTests`: 23 passed, zero failed, zero
  skipped;
- combined compiled-lane filter (rewrite PDB, rewrite, PDB, IL body, managed
  metadata): 179 passed, zero failed, zero skipped (sibling fixture-catalog
  pins updated v5→v6 in the rewrite, PDB, IL body, and source-reconciliation
  suites);
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,167 passed, zero
  failed, zero skipped;
- clean full rebuild: zero warnings, zero errors;
- two repeat CLI scans (compiler before pair vs Cecil nop-insertion after
  pair): byte-identical `facts.ndjson` and `report.md`, manifest identical
  except `scannedAt`, 71 `dotnet.compiled.il-rewrite-pdb%` rows in
  `index.sqlite`, receipt carries `ilRewritePdbProvenance`, 90 joined
  methods, 71 relationships (70 offsets-unchanged, 1 offsets-changed);
  operand-only variant: 71/71 offsets-unchanged; mismatched after PDB:
  side-scoped `IlRewritePdbAssemblyBindingMismatch`;
- `scripts/validate-adapter-artifacts.py` passed on all scanned outputs; no
  output contained a local absolute path, a temp path, or an IL string
  literal;
- `scripts/check-private-paths.sh`, `node scripts/kiro-review.mjs
  --self-test`, and `git diff --check` passed;
- plain `samples/modern-sample` source scan unchanged
  (Level1SemanticAnalysis, no new provenance section).

Explicitly deferred and still open for Task 10: ILAsm/ILDAsm parity,
evaluation-stack-sensitive rewrites, netmodules, type forwarding, duplicate
assembly identities, insertion/removal relationship edges, embedded portable
PDBs, and the extended ECMA-335 mutation matrix from #766. Task 11's legacy
Windows, `dotnetperf`, and C++/CLI lanes remain separate. The Task 10
checkbox stays open until #766's full public rewrite-suite acceptance is
met.

Review remediation on 2026-09-22 (ACK 0.5.2 loop, Qodo + Codex findings,
patch authorized at exact head `06ed3f3c`): the paired-assembly
re-verification now reads under the compiled-input limit that admitted the
assembly instead of the PDB-side file bound (an admitted assembly can never
be falsely rejected by a smaller PDB limit; pinned by a padded-assembly
regression where the after dll exceeds its PDB by design); join-phase budget
exhaustion after successful side reads is now atomic — relationships and
debug deltas are discarded, the bound PDB content identities survive on the
outcome, and `IlRewritePdbTotalWorkLimitExceeded` fails closed instead of
aborting the scan (pinned by a consumed-units-derived regression; the
outcome now records deterministic `consumedWorkUnits`); the bounded-input
digest commits the parent rewrite provenance digest plus both paired
assembly raw hashes, so the same PDBs reused against differently rewritten
assemblies never share a provenance digest; admission limit and declaration
causes keep their specific gap kinds (`IlRewritePdbSideFileSizeLimitExceeded`,
`IlRewritePdbTextLimitExceeded`, `IlRewritePdbSideDeclarationInvalid`) instead
of collapsing to `IlRewritePdbSideUnavailable`; and the VALIDATION.md
inertness text now states the pin runs through the `ScanOptions` API because
the CLI rejects unflagged declarations. Three regression tests were added
(26 focused rewrite-PDB tests total; full suite 2,170). A separate CI-only
flake in the compiled-metadata CLI determinism test was traced to a
transient git spawn failure flipping `repoName` between identical scans;
`GitMetadataProvider` now retries a failed git invocation exactly once
(non-repository exits nonzero on both attempts and keeps its null result).

Final review remediation rounds on 2026-09-22: a Codex P1 at head
`202b26ca` showed that rejected PDB admission branches (malformed and
native-Windows formats, finalized text-limit) discarded the already-computed
raw SHA-256, so swapping an in-repo PDB's rejected bytes kept an identical
provenance digest and scan id; every branch that read the bytes now retains
the digest, pinned by a two-variant rejected-bytes regression (`baf55ae6`).
A Codex P2 at `baf55ae6` showed that a parent pair whose join phase was
atomically exhausted keeps both side artifacts with zero edges, which the
PDB lane mislabeled as an admitted, complete pair; the lane now detects the
parent join-exhaustion marker and emits `IlRewritePdbRewritePairUnavailable`
with the parent cause, pinned by a synthetic parent-exhaustion regression
(`627bdecd`, code head). All eight review threads (three Qodo inline, one
Qodo summary, four Codex inline) are settled with durable ACK dispositions.

Final ACK readback at code head
`627bdecd0b7d8879fd948398aceeb3ee9ac9757d`: unresolved threads 0,
actionable findings 0, stale findings 0, pending/failed checks 0, merge
state CLEAN, focused rewrite-PDB suite 28/28, full .NET suite 2,172/2,172
zero failed/skipped, zero build warnings; decision `not_merge_ready` with
`CURRENT_HEAD_REQUIRED_REVIEW_MISSING` — the exact-head hosted-review
freshness gate is an owner decision and local validation does not
substitute for it. This docs commit sits on top of the code head; do not
merge, force-push, or retag bots without the owner. (PR #779 was
subsequently merged into `dev` as `7be1f51c` on 2026-09-22.)

## Task 10 fifth slice: public ECMA-335 control-flow and exception-handling rewrite suite (begin-work note)

Branch: `codex/task10-rewrite-cflow-suite` from `origin/dev` at
`7be1f51c0360a7f80e3b7304aee9677fe3c92ddc` (PR #779 merge). Tracking #766.
Scope: the control-flow and exception-handling portion of #766's public
rewrite matrix — before/after rewrite edges for branch/`switch`/`leave`
operand retargets, nested exception-region rebinding and handler-kind
changes, max-stack-only header changes, and evaluation-stack-sensitive
instruction-stream rewrites, plus bounded malformed control-flow operands
(out-of-range branch delta, oversized `switch` table count) and an
exception-region limit gap. The before sides are the deterministic
compiler-produced fixture assembly; the after sides are deterministic
Mono.Cecil-generated mutations or bounded byte patches produced inside the
public test suite. Existing IL body, rewrite, and rewrite-PDB tests were
inspected first: the single-side identity lane already covers switch
reordering and dense jump tables (CS-IL-BRANCH-005/007) and locals/EH
digests (CS-IL-BODY-012), and the rewrite lane already covers constant,
call, token, locals, unchanged, ambiguous, malformed-opcode, disagreement,
and budget shapes — none of the before/after control-flow, exception-region,
max-stack, or evaluation-stack shapes below duplicate that coverage. A new
fixture assembly (`CompiledEvidence.CSharp.ControlFlow`) keeps sibling
pinned row counts stable. ILAsm/ILDAsm parity remains deferred with the
slice-4 prerequisites; ordinary CI must not depend on an unpinned
ILAsm/ILDAsm installation.

The slice landed as: a new
`samples/compiled-dotnet-evidence/csharp/CompiledEvidence.CSharp.ControlFlow.csproj`
building only `IlRewriteControlFlowShapes.cs` (`Deterministic=true`,
referenced build-only from the test project like the sibling fixtures); a
`compiled-dotnet-fixture-cases.v7` catalog with 11 new `ilRewriteCases`
entries (CS-ILRW-CFLOW-010..017, ILRW-CFLOW-HOSTILE-018/019,
ILRW-CFLOW-LIMIT-020), each with a stable ID, expected identity/outcome,
rule IDs, tier, relationship kind, gaps, and non-claims; a new
`IlRewriteControlFlowEvidenceExtractorTests` suite where every after side is
a deterministic Cecil mutation (branch/switch/leave retargets, nested
try-start rebinding, catch→fault kind change, dup/pop and constant/add
insertions) or a bounded single-byte patch (max-stack bump in the fat
header, out-of-range short-branch delta, oversized switch count), plus
catalog, determinism/privacy, generator/bounded-input digest, and CLI
artifact tests; v6→v7 pin updates in the five sibling fixture-catalog tests;
and rule-catalog limitation updates recording that the control-flow/
exception-handling matrix and evaluation-stack-sensitive rewrites are now
exercised while ILAsm parity and the remaining #766 matrix stay deferred.
No scanner/reducer code changed: classification falls out of the existing
operand-aware digests (branch targets as absolute offsets, switch target
vectors, exception-region digests, recorded max-stack) already proven by
the first slice. Notable pinned findings: a switch jump-table permutation is
operand-only because the fixed-size table keeps every instruction offset
stable; a max-stack-only byte patch proves the digest reads the fat-header
field rather than a writer's model; and the malformed switch-count patch
must target the count byte (one past the 0x45 opcode) — patching the opcode
byte itself decodes to an unsupported operand encoding instead of a
malformed body.

Local macOS validation on 2026-09-22:

- focused `IlRewriteControlFlowEvidenceExtractorTests`: 16 passed, zero
  failed, zero skipped;
- combined compiled-lane filter (rewrite PDB, rewrite, PDB, IL body,
  managed metadata, source/metadata reconciliation): 221 passed, zero
  failed, zero skipped;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,190 passed, zero
  failed, zero skipped;
- clean full rebuild: zero warnings, zero errors;
- two repeat CLI scans of a branch-retarget pair (compiler before side,
  Cecil after side): byte-identical `facts.ndjson` and `report.md`,
  manifest identical except `scannedAt`, 434 facts with 7
  `dotnet.compiled.il-rewrite.v1` rows (one `operand-only-change`, six
  `unchanged`), zero `dotnet.compiled.il-rewrite-gap%` rows, admitted pair
  under `rewrite-complete` coverage;
- `scripts/validate-adapter-artifacts.py` passed on both scanned outputs and
  no output contained a local absolute path or temp path;
- `scripts/check-private-paths.sh`, `node scripts/kiro-review.mjs
  --self-test`, and `git diff --check` passed;
- plain `samples/modern-sample` source scan unchanged
  (Level1SemanticAnalysis, 27 facts, null `ilRewriteProvenance`);
- the new tests run in the ordinary ubuntu `dotnet` CI lane with no
  ILAsm/ILDAsm dependency (byte patches index the PE array directly, so
  they are host-endianness independent); the windows/ubuntu/macos
  local-distribution matrix is unaffected.

ILAsm/ILDAsm parity was re-assessed on 2026-09-22 and stays deferred with
the slice-4 prerequisites (`ILRWPDB-ILASM-PARITY-012`): absent from PATH,
the .NET SDK 10.0.201 installation, and Homebrew (mono not installed); no
pinned toolchain exists in ordinary CI, and no ILAsm/ILDAsm parity claim is
made from Cecil-based or byte-patched tests.

Explicitly still open for Task 10 after this slice: ILAsm/ILDAsm parity,
member/type token, constant, string, signature, generics, custom modifiers,
function-pointer/`calli`, property/event-accessor rewrite shapes,
netmodules, type forwarding, duplicate assembly identities,
insertion/removal relationship edges, embedded portable PDBs, and the
remainder of the extended ECMA-335 mutation matrix from #766. Task 11's
legacy Windows, `dotnetperf`, and C++/CLI lanes remain separate. The Task
10 checkbox stays open until #766's full public rewrite-suite acceptance is
met.

Review remediation on 2026-09-22 (ACK 0.5.2 loop, PR #780, Qodo + Codex
findings, two patch rounds): the first round at code head `b27f0860`
(1) made `ControlFlowFixture` probe the fixture assembly's Debug then
Release output so `dotnet test -c Release` locates it (verified by running
the branch-retarget case under `-c Release`), (2) moved every scan output
and the CLI artifact tree under each test's disposable `TempDirectory` so
runs leave no unmanaged temp trees, and (3) fixed
`MutateStackReshapingInsertion` to insert a balanced `ldc.i4.1`+`add` pair
(Codex P2: the lone constant left an extra value on the evaluation stack
at `ret`, so the after body was stack-invalid while the catalog documents
a well-formed constant/add sequence). The second round at code head
`9129da30` (Codex P2) excluded `IlRewriteControlFlowShapes.cs` from
`CompiledEvidence.CSharp.csproj`'s default Compile glob so the
shared-directory source builds only in the ControlFlow project and the
original fixture assembly's identity is unaffected (Cecil readback of the
rebuilt `CompiledEvidence.CSharp.dll` shows the type absent, 14 top-level
types). After each round the focused suite (16/16), the full .NET suite
(2,190/2,190, zero failed/skipped), and a zero-warning rebuild were
rerun; all three threads and the stale Qodo summary carry durable ACK
settle/comment dispositions. Note: the pre-existing `TempOutput` leak and
Debug-only fixture-path patterns in the merged sibling suites predate this
PR and were left untouched as out of this patch's authorized scope.

Final ACK readback at code head
`9129da3074075bc0ccc5ff35980ad188b3a0d67f`: unresolved threads 0,
actionable findings 0, stale findings 0, pending/failed checks 0, merge
state CLEAN, decision `merge_ready` (`MERGE_READY_DEV`, `workerMayStop`
true) — including the ubuntu .NET adapter job (full suite), the
windows/ubuntu/macos package-smoke matrix, and the five-adapter combine,
all green on the final head. Per the slice protocol the worker stopped
without merging, force-pushing, or retagging bots; the merge decision
belongs to the owner. This docs commit sits on top of the code head.

## Task 10 sixth slice: public metadata operands and member shapes

Branch: `codex/task10-ecma335-rewrite-slice`, isolated from `origin/dev` at
`2766de6933d4d60bb632389eff24bc4bfc2313f0` after verifying PR #780
merged as that exact commit. Tracking #766. The worktree is separate from
the original checkout's unrelated TypeScript edits. Before implementation,
the IL body, rewrite, rewrite-PDB, and control-flow suites were inspected:
existing constant, UTF-16 string, direct call-token, branch/switch/leave,
exception-handler, max-stack, and stack-sensitive cases were retained
without new duplicates.

This slice adds deterministic public C# `MemberShapes` and `VarArgCall`
compiler fixtures plus bounded Mono.Cecil rewrites, catalog schema v8 and
case IDs 021-032. It covers InlineField and InlineTok member operands,
TypeSpec, MethodSpec, constructed generic types and methods, `calli`
StandAloneSig calling-convention changes, a compiler-produced vararg
MemberRef call with MethodDef parent and required-parameter boundary,
required and optional custom modifiers, property/event accessor metadata
and rewritten bodies, and explicit before-only/after-only method membership.
The same opcode stream can now differ by field, type, generic method, or
calli signature operand; even an unchanged field row number with a changed
full field signature changes the body digest. Hostile field/signature token row IDs and a
reserved calli signature convention withhold the whole pair as malformed.

`IlBodyEvidenceExtractor` still reads SRM before Cecil. It now validates
InlineField/InlineTok/InlineSig row kind and decodable signature in SRM,
binds full field/type/member operand identities alongside local row numbers,
cross-checks calli calling convention, this flags, return and parameter
types against Cecil, exposes the static calli signature in call-site and
retarget facts, and retains the required-parameter count for vararg
MemberRef call identities. The scanner does not rewrite, load, or execute
the assemblies and makes no equivalence or rewrite-authorship claim. Every
positive and gap fact retains the rule ID, tier, limitation, extractor
version, exact generator SHA-256, and privacy-projected bounded-input
SHA-256. The public catalog binds each stable fixture ID to its expected
CLR shape, outcome, rule, tier, limitations, and digest property names.

Validation on the local macOS host before review remediation: focused suite
14/14, combined compiled evidence suite 235/235, and the serial full solution
suite 2,204/2,204, all with zero failures or skips. A concurrent full run had one
unrelated docs-export test failure (2,203/2,204); that test passed alone and
the serial full rerun passed. Cross-platform CI results will be read from the
PR checks. A zero-warning, zero-error `dotnet build
src/dotnet/TraceMap.sln --no-restore -warnaserror` passed. Two repeat CLI
scans of the compiler fixture produced byte-identical
`facts.ndjson` and `report.md`, matching manifests apart from `scannedAt`,
494 facts with 19 rewrite rows and zero rewrite gaps. The exact generator
SHA-256 on the reviewed head was `29e142e3f84aec1c4eda370310a6e6f70c4ecd35d3d960e25be412f50f8cd605`
and the privacy-projected bounded-input SHA-256 was
`89261c462248d788283eacccee4c05fce2b8d4221461dd79f60e580caa70d855`
for that declared pair. Both artifact trees
passed `scripts/validate-adapter-artifacts.py`; no local absolute path
appeared in facts, report, or manifest. The private-path guard, Kiro
self-test, JSON parse, and `git diff --check` passed. The
`local-distribution-validation.yml` matrix now runs this focused suite on
Linux, macOS, and Windows with no ILAsm/ILDAsm dependency.

Remaining #766 coverage explicitly deferred to the following slice:
netmodules, type forwarding, duplicate assembly identities, embedded
portable PDBs, pinned ILAsm/ILDAsm parity, and the extended Windows matrix.
Task 10 remains unchecked: #766 also asks for a complete extended public
suite and safe runtime/ILAsm corroboration where available; the current
static evidence does not claim those acceptance items.

ACK review remediation for PR #781 at reviewed head `7f199c866f7fb49303b0618bc2d22733e127f535`:
Qodo and Codex independently identified the same canonical-identity defect.
`calli` StandAloneSig decoding retained vararg parameter types and calling
convention but omitted the required/optional sentinel boundary. The
existing function-pointer and vararg MemberRef formatters already retain
that boundary; the repair adds `required` to both Cecil and SRM calli
canonicalization, rejects inconsistent sentinel shapes, and adds public
case `CS-ILRW-CALLI-VARARG-BOUNDARY-032`. Its before/after pair keeps the
same StandAloneSig token, opcode, calling convention, and parameter types
while only the required count changes from one to zero; the independently
checked rewrite relationship becomes `operand-only-change` and the calli
retarget fact records both complete signatures. This is a static signature
observation, not a safe invocation or equivalence claim.

Post-repair local checks: focused suite 15/15; combined compiled evidence
suite 236/236; serial full solution 2,205/2,205, all with zero failures or
skips; zero-warning build; private-path guard, Kiro self-test,
artifact validation and byte-identical repeat scans all passed (494 facts,
19 rewrite rows, zero rewrite gaps; generator SHA-256
`4b3c8c00e95fef9f1a762809129ebba1e6c826a0f0c51d6e736adb6530f76d8c`,
privacy-projected bounded-input SHA-256
`bdfdbda438917fa46d691bddedda92fdd1a6527251fcbfc023edc680f0ca1ec5`).
The Linux, macOS, and Windows checks are read from PR #781 on the pushed
repair head.

## Task 10 public ECMA-335 integration continuation (in progress)

Branch: `codex/task10-public-ecma335-suite`, an isolated worktree created
from current `origin/dev` at `dbb4f10aef8201da7e046b428b7a5c4a2e5105c2`
after verifying that PR #781 was merged to `dev` with that exact commit.
The original checkout's unrelated TypeScript changes and the prior Task 10
worktree remain untouched. Scope is public synthetic #766 fixtures and
validation; Task 11's private `dotnetperf`, legacy Web Forms, and C++/CLI
work-machine runs remain separate.

The topology suite now pins `ILRW-TOPO-001`–`005`: netmodules, secondary
metadata modules, and exported-type forwarders produce named unsupported
gaps; duplicate complete member identities produce an ambiguity gap; equal
assembly identities in separately declared pair ordinals do not cross-join.
The forwarder preflight checks the ECMA-335 `ExportedType` forwarder flag
before admitting any rewrite side. Embedded portable PDB input is bounded
before decompression and requires exact assembly-byte identity, CodeView
content identity, SRM/Cecil shape agreement, and method/offset binding.
Missing or mismatched embedded debug evidence emits a typed Tier4 gap and
withholds the PDB relationship. `ILRWPDB-EMBEDDED-PORTABLE-013`–`015` bind
the public catalog to those positive and negative cases.

The one safe runtime corroboration invokes only a synthetic parameterless
constant method after an independently read SRM before/after IL comparison.
It is one observed fixture behavior, never the authority for a TraceMap
identity edge or a general equivalence claim. Ordinary CI has a bounded
topology/PDB/runtime subset across Linux, macOS, and Windows; the separate PR/manual
extended public workflow runs all rewrite suites and discovers Windows
ILAsm/ILDAsm candidates. See `docs/VALIDATION.md` for the requirement ledger
and exact lane commands. The macOS host has no installed ILAsm/ILDAsm, and
the Windows runner's actual executable paths and versions are unverified.
Therefore the `ILRWPDB-ILASM-PARITY-012` prerequisite remains open and Task
10's checkbox must stay unchecked pending pinned independent parity proof.

Local integration validation on macOS 2026-09-22: the combined compiled
matrix passed 229/229 with zero skips; the new topology/PDB/runtime subset
passed 12/12 after the final evidence-envelope assertions; a focused
topology rerun after pinning the complete method signature passed 5/5. The
full .NET solution passed 2,217/2,217 twice, with zero failed/skipped; the
second full run included the evidence-envelope assertions and preceded only
the focused topology assertion tightening. The final focused topology run
passed after that change. `dotnet build src/dotnet/TraceMap.sln --no-restore
-warnaserror` had zero warnings and errors. Two repeated CLI scans of the
same public compiler assembly/PDB pair produced 636 facts each, including 90
rewrite and 71 rewrite-PDB relationships, with zero rewrite gaps; their
`facts.ndjson` and `report.md` were byte-identical and their normalized
manifests matched after removing only `scannedAt`. Both artifact trees passed
`scripts/validate-adapter-artifacts.py`; `scripts/check-private-paths.sh`,
`node scripts/kiro-review.mjs --self-test`, JSON parsing, and
`git diff --check` passed. The scanned rewrite provenance recorded generator
SHA-256 `e879a1baed3b711dfe8e68aebf09d7cbd166463ea46868a10b98e29271300471`,
rewrite bounded-input SHA-256
`bbfa10ada888bf4cd179d35cb57064f02fa60df85497f220ca4adc608a063c77`,
and rewrite-PDB bounded-input SHA-256
`995704a6cb2ff3a6df0aabba15d75fc2f3660087f12ced0e57bda3aa098cb84a`.
These are local pre-commit validation identities; PR CI and ACK must be read
at the pushed head before any review-state claim.

PR #782 review follow-up on first pushed head
`17e5bf86fe2dff0046e112e01f2ef048c306594b`: ACK returned
`actionable_findings` / `UNRESOLVED_REVIEW_THREADS`, with four unresolved
threads, one actionable Qodo finding, and failed Windows checks. The Windows
extended and bounded jobs exposed two test-fixture file-lock problems: the
new fixture's recursive cleanup failed on Git's read-only object files, and
an older rewrite test used one Cecil file as input and output while it was
still open. The repair adopts the existing `TempDirectory` cleanup convention
and stages the second Cecil write at a distinct path. The review also found
that the receipt schema omitted `embedded-portable`, rewrite-PDB safe
locators could exceed their own stricter text limit, and exported-type rows
were not charged before the rewrite preflight traversal. Each has a bounded
code/schema correction and focused regression test. A Qodo locked-restore
claim is contradicted by the first-head CI logs: all three extended jobs
completed the restore step and entered test execution. This is evidence for
an ACK disposition, not a claim that a reviewer thread is resolved. The
post-repair exact-head CI and ACK decision remain to be recorded; Task 10
is still unchecked.

Post-review local repair validation: focused defect matrix 16/16; full
`dotnet test src/dotnet/TraceMap.sln --no-build --verbosity quiet --
RunConfiguration.MaxCpuCount=1` 2,219/2,219, zero failed/skipped;
zero-warning/error `dotnet build src/dotnet/TraceMap.sln --no-restore
-warnaserror`. Two repeated CLI scans of the same public assembly/PDB pair
each produced 636 facts (90 rewrite, 71 rewrite-PDB, zero rewrite gaps),
byte-identical facts/report and equal manifests after removing only
`scannedAt`; both artifact trees passed the validator. The repair extractor
generator SHA-256 was
`3bbcfc79268b9bcbd9c5c5ad5dcb9d376a3bc6b68157f0b2947490b571fc1869`,
rewrite bounded-input SHA-256
`9cf35f0477dc3a2e7e49fc6e4b3fbda8b0d15ca53f7a0d7951e17dac6028077d`,
and rewrite-PDB bounded-input SHA-256
`ec5b233a657c4fb79799e409b9c36c788030195113a3beef12d77879432741bb`.
Private-path guard, Kiro self-test, catalog/receipt JSON parsing, workflow
YAML parsing, and diff checks passed. These are local repair bytes with
pre-push scan commit `17e5bf86`; they are not a substitute for new-head CI
or ACK.

Current-head `f9aaf8cc` CI on 2026-09-22: the extended public suite passed
on Ubuntu and macOS but Windows reported 117/118, with the positive embedded
Portable PDB input admission case returning `unbound` rather than `admitted`.
The bounded package smoke passed on Ubuntu and macOS; its Windows job was
still running when this note was updated. Windows runner discovery found
`ildasm.exe` 4.8.3928.0 under the .NET Framework 4.8 and 4.8.1 SDK tools
(x86 and x64), but no `ilasm.exe` in the searched SDK, Visual Studio, or PATH
locations. The Windows failure remains a required public-suite defect to
diagnose; Task 10 remains unchecked. ACK on this head had zero unresolved
threads but stopped at `STALE_CODEX_REVIEW_RISKY_CHANGES`, requiring an exact
head fresh Codex review or owner override after CI is clean.

Diagnostic head `dcb1e875` reproduced the extended Windows 117/118 result.
The positive embedded-PDB test's compiled input was `admitted` but its
provenance was `unknown`, with `ManagedInputBindingIncomplete`; the PDB
extractor correctly emitted `PdbCompiledEvidenceUnacceptable`. The fixture
had used `GitMetadataProvider.Detect`, whose intentionally labeled `unknown`
fallback can produce a malformed synthetic binding receipt when Git process
discovery is transient under CI load. The fixture now obtains `rev-parse HEAD`
directly, requires a 40-hex SHA, and leaves the scanner's fail-closed behavior
intact. The seven embedded-PDB tests passed locally after this change;
cross-platform exact-head validation is pending.

Head `e508cff9` exposed the second part of that CI contention: Windows
extended validation again reported 117/118, now because the scan's bounded
Git probe could not establish a SHA and `ScanReceiptRecorder` refused to
create a receipt. The macOS ordinary package job separately reported 49/50
for an existing repeated bound-PDB scan whose two scan IDs differed.
`PortablePdbExtractorTests` and `IlRewriteEmbeddedPdbTests` now share a
non-parallel xUnit collection so these Git-sensitive identity assertions run
without competing test collections. The fixture continues to reject an
invalid SHA; scanner behavior remains fail-closed. Local validation after
the collection change: 57/57 focused PDB tests, 2,219/2,219 full solution
tests with zero skips, and a zero-warning/error solution build. Exact-head CI
and ACK remain pending; Task 10 stays unchecked.

Final head `a52d239d` ("Serialize Git-sensitive PDB identity tests") cleared
both flaky lanes: exact-head `public-mutation-matrix` passed on Linux, macOS,
and Windows (Windows 118/118) and all three `package-smoke` jobs passed, with
every other required check green. The owner merged PR #782 into `dev` as
`dc446ab560c7badf1f1518814685b33ccb9c375d` on 2026-09-22. Task 10 remains
unchecked: `ILRWPDB-ILASM-PARITY-012` still has no pinned `ilasm.exe`, and
the Windows runner discovery to date never searched the .NET Framework
runtime `Framework`/`Framework64` directories that ship `ILAsm.exe` with the
OS itself.

## Task 10 ILAsm/ILDAsm parity gate (begin-work note)

Branch: `codex/task10-ilasm-parity-gate`, an isolated worktree created from
`origin/dev` at `dc446ab560c7badf1f1518814685b33ccb9c375d` after verifying
GitHub reported PR #782 `MERGED` with that exact merge commit (second parent
`a52d239d`). Scope is the public Windows ILAsm/ILDAsm parity gate from #766:
tool discovery on the hosted Windows runner including the .NET Framework
`Framework`/`Framework64` runtime directories, Windows SDK, Visual Studio,
and PATH; a pinned-toolchain public parity matrix through
ILAsm → assembly → ILDAsm compared against TraceMap identities and gaps; and
reproducibility in the extended Windows CI lane. Mono.Cecil must not be the
independent parity oracle; ILDAsm text and ILAsm round-trip assembly are. The
private `dotnetperf` checkout, work-source scans, and Task 11's work-machine
lanes stay out of this PR. If the hosted runner truly lacks a usable ILAsm
toolchain even after runtime-directory discovery, the slice must instead
produce a precise work-machine command plus expected receipt for the missing
public parity cases and leave Task 10 unchecked without claiming parity.

Implementation on this branch: the missing prerequisite was the search scope,
not the tool — `ILAsm.exe` ships with the .NET Framework runtime under
`C:\Windows\Microsoft.NET\Framework[64]\v4.0.30319\`, which the PR #782
discovery never searched. Both the extended workflow's discovery step and the
in-test discovery now search the runtime directories first (Framework64, then
Framework), then the Windows SDK NETFX 4.8/4.8.1 Tools directories (x64
first), Windows Kits, Visual Studio, and PATH, recording the runner image
(`ImageOS`/`ImageVersion`/architecture), absolute paths, file versions,
product versions, and `/?` invocability; a hit without a file version or a
pinned tool that fails `/?` fails the case. The gate itself is
`IlAsmIldasmParityGateTests` (extended Windows lane only; non-Windows hosts
return without claiming anything, and ordinary CI filters never include it).
ILDAsm `/out= /nobar` text and ILAsm `/dll /nologo /output=` round trip are
the independent oracles, parsed by the test-local `IlDasmTextParser` whose
own platform-neutral tests (`IlDasmTextParserTests`) pin the parsing of
method headers, try/catch structure, switch tables, multi-line locals, and
`.line` directives. Six catalog cases were added as
`fixture-cases.json` schema v9 `ilasmParityCases`
(`ILASM-PARITY-TOOLS-001`, `CFLOW-002`, `EH-003`, `MEMBER-004`,
`MUTATE-005`, `PDB-006`), and `ILRWPDB-ILASM-PARITY-012` flipped from
deferred to implemented with an explicit `satisfiedBy` list. The
round-trip legs scan a bound compiled-input pair (binding receipt over a
temporary git fixture repository, `il-body` plus `il-rewrite` evidence) and
require: identical normalized ILDAsm text of original and reassembled
assembly; every joined method `unchanged` with `tokenRetargeted=false` and
zero gaps; and per-side instruction, local, exception-region, max-stack, and
call-offset counts equal between the ILDAsm observation and TraceMap facts.
The mutation leg requires the branch-retarget, handler-kind, and
stack-neutral-insertion classifications to be identical for the raw and
round-tripped after sides. The PDB leg requires an embedded portable PDB's
non-hidden sequence points to equal ILDAsm `.line` directives (adjacent
extracted PDB, `/pdbpath=` retry, and a precise work-machine reproduction
command plus expected receipt on total unavailability instead of a parity
claim). ILAsm outputs keep the fixture's assembly file name in their own
directories so the reassembled module identity joins the original regardless
of how ILAsm derives the module name. Rule-catalog limitations for
`il-body`, `il-rewrite`, and `il-rewrite-pdb` now state the exact proven
scope (public fixture matrix, pinned .NET Framework 4.8 ILAsm + Windows SDK
NETFX ILDAsm, ILDAsm as oracle, no general equivalence claim), and
`docs/VALIDATION.md` gained the parity-gate section with the updated #766
requirement row and discovery command.

Local macOS validation (the Windows legs return early by design):
zero-warning/error `dotnet build src/dotnet/TraceMap.sln --no-restore
-warnaserror`; focused `IlAsmIldasmParityGateTests` + `IlDasmTextParserTests`
12/12. The remaining checks for this slice are exact-head CI on all three
extended-lane operating systems (Windows is the load-bearing one) and the
ACK review; until the Windows extended lane passes with the pinned toolchain
recorded, no parity claim is final and Task 10 stays unchecked.

First pushed head `e9af9740` CI on 2026-09-22: the extended lane passed on
Ubuntu and macOS (125 tests each) and the Windows job confirmed the central
toolchain hypothesis — `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe`
exists (the in-test probes recorded exists=True and the toolchain test
passed), the runner image is `win25-vs2026`/`20260907.229.1`/AMD64, and the
workflow discovery additionally surfaced a legacy
`Framework\v2.0.50727\ilasm.exe` 2.0.50727.9157 (not pinned; the ordered
discovery takes Framework64 v4.0.30319 first). ILDAsm 4.8.3928.0 was found in
all four NETFX tool directories. Five Windows gate tests failed with three
distinct defects, all in the gate itself: (1) ILDAsm wraps long `.method`
headers across lines, so the parser must join header continuation lines until
the parameter-list parenthesis appears (the embedded PDB fixture's 76-char
single-line header parsed, the public fixtures' 80+ char headers did not);
(2) the mutation leg assembled into a `rt` subdirectory that was never
created, so ILAsm could not start ("The directory name is invalid");
(3) `/pdbpath` does not exist in ILDAsm 4.8.3928.0 — its captured usage text
documents `/LINENUM` ("Include references to original source lines") and
`/UTF8` instead, and the plain adjacent-PDB invocation without `/linenum`
emitted no `.line` directives. The repair joins wrapped headers (with the
offending text now included in every parser failure for future diagnosis),
creates output directories before invoking ILAsm, adds `/utf8` to every
ILDAsm invocation, and runs the PDB leg with the documented `/linenum`
switch; if that ILDAsm accepts the switch, disassembles the carrier, and
still emits no `.line` directives, the test records the typed
oracle-availability gap with the precise work-machine command and expected
receipt and makes no PDB parity claim (catalog case `ILASM-PARITY-PDB-006`
and `docs/VALIDATION.md` state this contract). Exact-head CI reruns on the
repair head remain pending; Task 10 stays unchecked.

Second CI round on the repair head: Ubuntu, macOS, and all ordinary lanes
passed; the Windows job failed only inside the gate with three new findings,
again all gate-side. (1) ILAsm re-emits assembly-level custom attributes in a
different metadata row order than the originals (observed:
`RefSafetySafetyAttribute` and `AssemblyCompanyAttribute` swapped), so
whole-file normalized-text equality is not the parity claim; the parser now
retains per-method exception-clause extents (kind plus try/handler
instruction-boundary ends), switch jump-table targets from continuation
lines, and the `// Code size` annotation, and the gate compares a canonical
per-method body text that is order-insensitive across members and strictly
order-sensitive inside bodies. (2) The mutation leg passed the disassembly
text where the IL file path was expected, producing ILAsm's "Input file name
exceeds 2047 characters"; the leg now passes the path. (3) The determinism
leg wrote scan outputs and binding receipts inside the scanned fixture
repository, so the second scan inventoried the first scan's artifacts;
outputs and receipts now live under a temp root outside the repository.
Local validation after the second repair: 14/14 focused parser+gate tests,
full solution suite green, zero-warning build. Third exact-head CI run
pending; Task 10 stays unchecked until the Windows extended lane passes and
the PDB oracle outcome (observed `.line` directives, or the recorded typed
gap) is known.

Third and fourth CI rounds pinned the remaining behavior. Round three left
only the member-shape leg failing: its canonical member-body comparison
held and the control-flow fixture round tripped fully `unchanged`, but
seven member-shape methods classified `operand-only-change`. Round four
confirmed the cause and the fix: ILAsm renumbers raw module-local
reference rows on re-emission (cross-assembly MemberRefs) while every
symbolic operand stays identical, and TraceMap's operand-aware digests
commit raw tokens by documented contract — so the member leg now asserts
exactly that classification (kinds limited to `unchanged` or
`operand-only-change`, opcode streams preserved, every call-retarget
identity equal). Round four also surfaced the constructor marker: TraceMap
encodes `.ctor`/`.cctor` under `constructor:`, not `method:`, so the gate's
body-fact matcher (and a name quotation strip in the parser) now mirror
that convention; the marker hypothesis was verified locally against real
facts before pushing. The extended lane now logs with detailed console
verbosity, and the run recorded the exact pinned toolchain: runner image
`win25-vs2026` `20260907.229.1` AMD64, `ilasm.exe` 4.8.9221.0
(`NET481REL1LAST_25H2`) at `Framework64\v4.0.30319`, and `ildasm.exe`
4.8.3928.0 (`NET48REL1`) at the NETFX 4.8.1 Tools x64 directory. The PDB
leg's definitive outcome also landed: that hosted ILDAsm accepted
`/linenum`, disassembled the carrier, and emitted no `.line` directives
for the adjacent extracted portable PDB, so the typed oracle-availability
gap branch executed with the work-machine command and expected receipt
recorded in the run log, and no PDB parity is claimed — Task 10's checkbox
stays open on exactly that one prerequisite. The fifth exact-head CI run
(the constructor-marker repair) is pending at this note's head.

Fifth exact-head CI run at `24100420` on 2026-09-23 is green everywhere:
`public-mutation-matrix` passed on Ubuntu (133 tests), macOS, and Windows
(133 tests including all eight gate tests and seven parser tests; run
https://github.com/joefeser/tracemap/actions/runs/35802803285/job/106996742841),
and every ordinary lane including all three `package-smoke` jobs passed.
The gate's public parity claims now stand proven on the pinned toolchain:
toolchain pinning with exact versions, control-flow and exception-region
round trip fully `unchanged` with count-level oracle agreement, the
member-shape round trip with symbolic operands preserved and renumbered
raw tokens classified exactly `operand-only-change` with
identity-preserving retargets, mutation-classification parity through the
independent round trip, and repeat-scan determinism. The one open
prerequisite is PDB sequence-point parity: the hosted ILDAsm 4.8.3928.0
observes no portable PDB through `/linenum`, so `ILASM-PARITY-PDB-006`
records the typed oracle-availability gap with the work-machine command
and expected receipt, no PDB parity is claimed, and Task 10's checkbox
stays open on exactly that gap pending a work-machine run. ACK review on
this head follows; Task 11 (#768/#769) remains separate.

First ACK review on the documentation head `3ee3b33c` returned
`actionable_findings` / `UNRESOLVED_REVIEW_THREADS` with five unresolved
threads (two Qodo maintainability findings, three Codex P1/P2 findings).
Five earlier findings against superseded heads (the uncreated rt
directory, the invalid /pdbpath flag, receipts inside the scanned repo,
the try/handler count equation) were already repaired by the CI-round
commits. The remediation commit addresses the five live ones: the parity
toolchain now records and requires non-empty file AND product versions;
the workflow's discovery step selects one ILAsm and one ILDAsm by the
documented order and hands their absolute paths to the test process via
`TRACEMAP_PARITY_ILASM`/`TRACEMAP_PARITY_ILDASM`, which the in-test
discovery consumes and re-validates before its own ordered fallback (a
full Visual Studio recursion in-test costs minutes on the runner, so the
broad search stays in the workflow); and `ILRWPDB-ILASM-PARITY-012` now
lists only the three proven satisfiedBy cases, drops the unexercised
`dotnet.compiled.il-rewrite-pdb.v1` rule claim, and adds the explicit
portable-PDB non-claim, with the catalog test pinning all of it. The
remaining Codex P1 (fail rather than record when the PDB oracle emits no
evidence) is settled by disposition: the owner's task instruction for
this slice defines exactly the recorded typed-gap fallback (precise
work-machine command, expected receipt, Task 10 left open, no parity
claim), and a permanently red extended lane would block the five proven
cases and every future src/dotnet PR. Local validation after the
remediation: 14/14 focused tests, full suite 2,233/2,233, zero-warning
build. Exact-head CI and the settlement rerun of ACK follow.

PR #783 follow-up on head `7e20b0c9`: the independent ILDAsm canonical
exception-region observation now commits the catch type identity and, for
filter clauses, the filter start offset as well as kind and extents. Parser
regressions pin both identities. The public mutation case's claim is
corrected to canonical method-body equality, not whole-file normalized
disassembly. The hosted `/linenum` no-directive outcome is represented in
the machine-readable fixture catalog as deferred/Tier4Unknown with
`IldasmPortablePdbLineOracleUnavailable`; the test verifies that record
before taking its no-oracle branch. This does not close Task 10 or claim
PDB parity. The parent `ILRWPDB-ILASM-PARITY-012` prerequisite is partial,
not implemented, until that PDB half is independently observed. The broader
Windows tool discovery already flows through the
workflow environment handoff on this head; no second discovery path was
added to the test.

The next exact-head review found two claim/documentation slips, not scanner
defects: `ILASM-PARITY-CFLOW-002` still said whole-file normalized IL text
where the gate compares canonical method bodies, and `docs/VALIDATION.md`
spelled the ILDAsm handoff variable without `_PARITY_`. Both are corrected
and covered by the fixture-catalog test. The prior head's complete CI matrix
was green, including the Windows parity job; this follow-up needs its own
exact-head checks and ACK decision.

### PR #783 independent ECMA review (2026-09-23)

Reviewed the full PR delta at `e3c0ff7ca5a7c4e6ef0114ed76c076878bff0abe`,
including the Windows workflow, independent parser, parity assertions,
fixture catalog, rule limitations and validation claims. Work branch:
`codex/review-pr783-ecma`, isolated from the existing implementation worktree.
The owner requested an independent review, without a PR review loop.

Four P2 defect families were repaired; no P1 defect was established:

1. **Incomplete canonical identity.** Method declarations were reduced to a
   short name and locals to a count. Changing parameter/return types, vararg
   calling convention, local types or local initialization could leave the
   independent comparison equal. Preserve full wrapped declarations and local
   signatures, including constraints/modifiers, and parse the method name
   without mistaking modifier/function-pointer parentheses for parameters.
2. **Dropped instruction evidence.** Wrapped non-switch operands were ignored,
   and four-digit offset patterns discarded instructions/targets above
   `0xffff`. Retain operand continuation text and complete offsets. Keep quoted
   whitespace significant; reject malformed line directives and incomplete
   switches instead of silently omitting observations.
3. **Incorrect EH boundaries.** A handler ending at the method boundary caused
   `Build()` to overwrite its already-observed try end with code size. Preserve
   resolved ends, pair sibling handlers with the same protected range, and
   distinguish PDB lexical-scope braces from EH braces. Unsupported offset-form
   clauses fail explicitly rather than producing guessed regions.
4. **Missing claimed modifier coverage.** The compiler fixture's non-virtual
   `in int` parameter did not exercise custom-modifier signatures. Construct
   explicit optional parameter and required return modifiers before the parity
   round trip; assert their presence in the independent ILDAsm declarations.
   Cecil checks fixture construction only, never parity.

Seven newly added regression cases were run against the original parser and
failed as expected (five signature/local mutations, a wrapped operand mutation,
and the terminal-handler boundary). The expanded matrix also covers generic
constraints, function pointers, quoted names, multiple handlers, lexical scopes,
large offsets, literal whitespace and malformed observations. The rule catalog
now states the portable-PDB oracle gap explicitly, matching the existing
partial/deferred fixture statuses. Task 10 remains unchecked on that gap.

Reference boundaries: ECMA-335 Partition II specifies method/local signatures
and exception regions; Microsoft's ILDAsm implementation emits wrapped operands,
lexical scopes and minimum-width (not fixed-width) hexadecimal offsets.
Sources: https://ecma-international.org/publications-and-standards/standards/ecma-335/
and https://github.com/dotnet/runtime/blob/main/src/coreclr/ildasm/dis.cpp.
No core scanner/reducer implementation changed. Windows parity must be checked
on the pushed repair head; local macOS early returns are not Windows proof.

Local validation: locked restore; solution build with `-warnaserror` (zero
warnings/errors); full .NET suite 2,253/2,253 passed; sample CLI scan emitted
101 facts and passed `validate-adapter-artifacts.py`; private-path guard,
fixture JSON parse, Kiro review self-test and `git diff --check` passed. The
final quoted-catch-identity regression and synthetic offset/code-size
corrections were checked by a focused rebuild after the full suite. Windows
parity cases return early on macOS and are excluded from any local parity
claim; the pushed-head Windows extended job is the required proof.
# Task 11 first bounded Windows validation lane (#768, 2026-09-22)

Branch `codex/task11-windows-validation` was created in a separate clean
worktree from `origin/dev` at exact base
`25e29a22896184379e1edfb11b46a839d3afcee0` (PR #783 merge). The origin
was verified as `joefeser/tracemap`; the original checkout and its untracked
`.vscode/` were left untouched. Scope is the first reviewable runner, public
ILAsm smoke, synthetic fail-closed guards, documentation, and C++/CLI
feasibility inventory for #768. Task 10 and #769 are excluded; the parent
Task 11 checkbox stays open.

The local historical checkout is clean but its HEAD is not the required
`db8c3359badfec620ccdc6df062b1756ef9607f8`, and that object is absent.
A noninteractive fetch using only the configured origin failed with `Host key
verification failed`; no credential or signing-material search was performed.
Private project build, representative tests, scan, provenance, and identity
edge assertions are blocked, not validated. The runner requires the pinned
commit and authorized remote attestation before any private build or scan.

The Windows host has Visual Studio 2022 Enterprise 17.10.35201.131, .NET SDK
10.0.303, Framework ILAsm 4.8.9221.0, and SDK ILDAsm 4.8.3928.0. The C++/CLI
component query and `cl.exe` discovery found no compiler. Public ILAsm can
represent custom modifiers and unusual pure managed metadata; tracking refs
and mixed-mode native transitions remain deferred or explicit unsupported
input. No PDB parity claim is made; Task 10 retains
`IldasmPortablePdbLineOracleUnavailable` after the prior three-version
`/linenum` attempt emitted zero `.line` directives.

Validation so far: the synthetic PowerShell guard suite passed wrong commit,
dirty checkout, missing corpus/tool/artifact, invalid provenance, and output
reuse/overlap cases. Locked .NET restore succeeded; the solution build passed
with zero warnings and zero errors. The full .NET suite was attempted on
Windows and stopped after over eleven minutes and widespread failures in
unrelated report, Web Forms, artifact, symlink, and Windows file-lock tests;
it is not green. The Python adapter-artifact test reported three Windows
temporary SQLite deletion errors. The Bash private-path script returned 0 but
its WSL Git worktree resolution emitted fatal path errors, so that invocation
is not treated as a valid privacy pass. `git diff --check` passed. Public
smoke remains to be run after committing a clean TraceMap head. The full-corpus
opt-in has not been exercised. Do not open a PR until the required gates pass.

After the clean commit `fe6f54e5`, the Windows `PublicSmoke` runner passed with
its local private receipt reporting `status=passed`; the synthetic guard suite
passed again. A real private preflight against the supplied local checkout
stopped at `CORPUS_COMMIT_MISMATCH` and wrote `status=blocked`, before any build,
test, or scan. The configured noninteractive fetch had already failed at host
key verification, so the pinned corpus remains unavailable. A changed-file
privacy sentinel over the branch diff passed, and `git diff --check` passed.
The extended Windows public workflow now invokes only the synthetic guards
and independent public ILAsm smoke; it has no private corpus dependency.
An additional public modern-sample CLI scan completed with 27 facts and all
five required artifacts; `validate-adapter-artifacts.py` accepted its output.
The full-corpus no-opt-in invocation stopped with
`FULL_CORPUS_OPT_IN_REQUIRED` before creating any output. These public checks
do not turn the blocked private stages or failed full .NET suite green.
The focused `IlDasmTextParserTests` .NET filter passed 26/26 on this Windows
host; it does not address Task 10's unavailable portable-PDB line oracle.
The `FullCorpus` guard now also requires a previously passed bounded receipt
for the same TraceMap and corpus commits, with generator and bounded-input
digests; the synthetic suite verifies missing opt-in and missing prior receipt
both stop before output creation.

## Task 11 Windows blocker triage (2026-09-23)

At the start of this continuation, `codex/task11-windows-validation` was clean
at `6c5017db63468c78e8e981b8b03a065d567c81c4`. A separate detached,
clean base worktree was created at
`25e29a22896184379e1edfb11b46a839d3afcee0`; the original checkout and
its untracked `.vscode/` remained untouched. No private corpus operation was
retried, and the pinned private stages remain blocked by the previously
recorded commit/SSH host-key condition.

The original full-suite invocation did not retain a durable complete console
log. A replay of `dotnet test src/dotnet/TraceMap.sln --no-build --no-restore
--logger 'trx;LogFileName=branch-full.trx'` retained exact failure names,
messages, and stack traces in a **local-only** triage report outside the repo.
It was interrupted after 62 recorded failing cases across 56 distinct methods
and a long period without completion; exit code 1, so no full-suite pass total
is claimed. A second run with test-collection parallelism disabled made no
case progress in five minutes and was stopped, also exit 1. The temporary
runner configuration was removed. Neither run is a green full-suite result.

Each of the 56 replay-failing methods was then invoked alone in this branch
and immediately at the untouched base with `dotnet test
src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore
--filter "FullyQualifiedName=<exact method>" --logger
'console;verbosity=minimal'`. The local-only report retains every exact
command, exit code, error, and stack trace. **53/56 methods exited 1 on both
commits with the same top error; 3/56 exited 0 on both.** There were zero
branch-only failures. The six rows of the evidence-metadata theory were also
selected individually with `DisplayName` filters: each exited 1 on branch and
base, one test per invocation. The two unsafe-path theory rows and the
`combined: True` framework row were separately reproduced on both commits.

| Isolated outcome | Exact representative failures | Classification |
| --- | --- | --- |
| 17 methods with `index.sqlite` or other file-in-use errors on both | `WebFormsAgentEvidenceHandoffTests.SetHandoffRejectsIndexWithInvalidEvidenceMetadata`; `WebFormsReportMemoryTests.Large_repetitive_fact_payload_does_not_scale_retained_graph_input` | Pre-existing Windows file-handle behavior |
| 3 methods with “A required privilege is not held by the client” on both | `AccessDesignEvidenceCompositionTests.Cli_requires_explicit_inputs_and_rejects_database_identity_mismatch_without_output`; both `ReverseImpactArtifactQueryTests` symlink cases | Windows symlink privilege unavailable |
| 12 methods with `release-review could not read --before/--after input` on both | `SqlValidationSummaryTests.Combined_release_review_preserves_context_matched_source_label`; several `ReleaseReviewTests` | Pre-existing Windows input/read failure |
| 16 assertion-mismatch methods on both | `LegacyDataEdmxSymbolCompositionTests.F17_scope_decoys_never_become_candidates` (`src\\Model.Designer.cs` versus `src/Model.Designer.cs`); `EvidenceDocsExportTests` cases | Pre-existing Windows behavior |
| 5 other methods failing on both | Includes `VaultExportTests.Vault_export_hidden_rejects_raw_unsafe_evidence_locations_without_echoing_values` (no expected exception) | Pre-existing Windows behavior |
| 3 methods passing alone on both after failing in suite | Both `ProjectlessVisualBasicWebFormsDiagnosticsTests` cases and `VisualBasicFoundationTests.Modern_vb_scan_is_deterministic_across_repeated_runs` | Suite interference |

Three additional named failures recoverable from the first interrupted run—
`ScanEngineTests.Scan_identity_changes_when_committed_source_bytes_change_without_changing_size_or_head`,
`VisualBasicValidationMatrixTests.Repeated_cli_scans_produce_byte_identical_facts`,
and `ScanExecutionReceiptTests.Source_only_scan_records_syntax_not_semantic_stage_coverage`—
each exited 0 alone on branch and base; classify them as suite interference.

`python scripts/test_validate_adapter_artifacts.py` exited 1 on branch **and
base**, with 7 tests run, 4 passing and 3 errors. Each of
`test_canonical_extractor_provenance_columns_are_not_null`,
`test_sqlite_fact_field_mismatch_fails`, and
`test_sqlite_properties_mismatch_fails` exited 1 when run alone on each commit.
Each stack ends in `tempfile.TemporaryDirectory.__exit__` /
`shutil.rmtree`, first raising `PermissionError: [WinError 32]` for open
`index.sqlite` and then `NotADirectoryError: [WinError 267]` during cleanup.
Those three unchanged base tests use `with sqlite3.connect(...)`, which
commits/rolls back but does not close the connection on Windows. The new
Task 11 runner never executes in those tests. This is pre-existing test
cleanup, not a branch regression; no broad retry, skip, or unrelated fix was
added.

Final focused checks on this branch: Task 11 synthetic guard script exit 0;
`IlDasmTextParserTests` 26 passed/0 failed; solution `dotnet build
--no-restore -warnaserror` exit 0 with 0 warnings/0 errors; Windows
`PublicSmoke` exit 0; Git Bash `scripts/check-private-paths.sh` exit 0 with
“Private path guard passed.” The real public sample scan and standalone
adapter-artifact validator had already passed in the prior slice. Full .NET
and Python test suites remain red for the base-equivalent reasons above.
No PR is opened and no private corpus validation is claimed.

## Task 11 bounded-admission guard follow-up

The first pushed runner allowed a broad `-BoundedPaths` glob such as `*`, which
could admit the entire corpus under a `Bounded` receipt. The bounded lane now
requires exact tracked files, rejects directories, globs, duplicate/missing or
reparse-point paths, and enforces 256 selected files / 64 MiB of selected
source bytes before any private build or scan. A full-corpus run also rejects
older bounded receipts without this selection record and rechecks the recorded
selection against the pinned clean corpus. The receipt retains the admitted
file and byte counts. The artifact rule-catalog check now compares exact rule
IDs instead of accepting a prefix substring. Synthetic regressions pin both
guards and the old-receipt rejection.

On macOS, the cross-platform synthetic helper cases and actual rule-catalog
parse passed; the Windows-only entry-point, full-corpus and output-path guards
remain for a Windows rerun. No private corpus run or full .NET suite is claimed
by this follow-up. Task 11 remains open and no PR has been opened.

The Windows rerun at `71675195` proved bounded selection and old-receipt
rejection, and `PublicSmoke` passed, but the synthetic artifact guard stopped
at `RULE_CATALOG_EMPTY`: the tiny synthetic catalog used CRLF while the exact
rule-ID matcher only accepted LF. The matcher now accepts either line ending,
and the regression writes CRLF explicitly on every host. Exact rule-ID checks
still require a Windows rerun before this follow-up is considered validated
there.

## PR #785 exact-head review repair (2026-09-23)

The current-head review found that the first bounded receipt could still
overstate its source and tool scope. The repair keeps the existing Task 11
lane and fails closed: the bounded selection must equal the scanner's complete
eligible inventory, scanner candidate enumeration and source file/byte counts
have hard limits, and any semantic input outside the declared set is rejected.
The runner rejects ignored source in a supposedly clean checkout, requires the
exact slice project in the selection, normalizes relative corpus and test
paths, records safe preflight failure receipts, and checks nonempty required
artifacts plus SQLite integrity/schema/commit/fact count. Reduced analysis or
explicit gaps yield a non-passing `partial` receipt. A full-corpus run must
reproduce the bounded run's exact CLI DLL digest and complete output-payload
digest before scanning.

Mac validation on this repair: Task 11 synthetic guards passed, the focused
exact-scope/SQLite validator suite passed 9/9, `dotnet build` passed with zero
warnings under `-warnaserror`, and the final full .NET suite passed 2,262/2,262.
The private-path guard, Kiro self-test, and `git diff --check` passed. The
Windows-only entry point and private corpus have not been rerun for this patch;
no private validation is claimed. The unrelated base-equivalent Windows
full-suite failures remain separately recorded above.

## Messy .NET workspace continuation (2026-09-23)

This section supersedes the *current-status* reading of the 2026-09-21
begin-work/deferred list and 7-implemented/5-deferred count above; those lines
remain as the historical first-slice record. PR #786 merged into `dev` as
`bb3e7ca806e037ce4c2a37be6c43ed724c4475cd`, pinning the overload and
receiver-dispatch identity cases. PR #787, branch
`codex/messy-dotnet-remaining-fixtures`, adds the remaining generated-member,
C#/VB/F# boundary, and source-to-metadata-to-IL/PDB cases. Its public catalog
now records **12 implemented, 0 deferred** cases across **five synthetic
roots**, backed by 12 `MessyWorkspaceRegressionTests` methods. These are
public fixture outcomes, not private-corpus or runtime-equivalence claims.

The #787 review repair makes catalog rule/tier/gap expectations executable
against each new case's produced evidence. The C# cross-language hop has two
explicit, mutually exclusive outcomes: Tier1 semantic call, or Tier3 syntax
call with a Tier4 compilation gap; VB-to-F# requires a Tier1 VB semantic call.
The F# compiled member requires Tier2 metadata evidence while F# source
ownership remains an explicit unsupported-language gap. The generated handler
requires exactly one source-to-metadata reconciliation edge and one IL body
and PDB method on its compiled identity. The fixture does not claim that the
entire three-language source path is traversable or that compiled evidence
proves execution.

Local review-repair validation: focused messy-workspace suite 12/12, full
`dotnet test src/dotnet/TraceMap.sln --no-restore` 2,266/2,266, and the
private-path guard passed. Task 10's remaining public #766 matrix and Task
11's Windows/private validation remain separate; neither checkbox is closed
by these synthetic fixtures.

## Task 11 buildable-fix profile preparation (2026-09-23)

This branch starts at `origin/dev` merge `308d76532059bb67a9d73a7e57c028c02e01ecb4`
(PR #787). Task 10's public ECMA-335 work still lacks an independent ILDAsm
portable-PDB `.line` oracle (`IldasmPortablePdbLineOracleUnavailable`) and the
remaining #766 acceptance matrix; the 12/12 messy-workspace fixtures do not
close it. Task 11 is a separate Windows/private lane. Neither Task 10 nor Task
11 is checked complete.

The Task 11 runner now has two explicit corpus profiles. The default
`HistoricalMaster` retains #769's exact commit and 256-file admission limit.
`BuildableFix` pins the distinct buildable-fix commit and admits at most 427
complete eligible files. Both retain 64 MiB selected bytes and 4,096 candidate
entries. Receipt schema 2 binds the exact runner SHA-256, profile, commit,
effective limits, exact file selection, and candidate counts before and after
representative tests. A
cross-profile or older bounded receipt cannot authorize a full-corpus run.
The runner still rejects ignored non-generated inputs and exact selections
that are not tracked; the scanner still requires complete inventory equality.

A separate clean detached Windows checkout of the fix commit was inventoried
before building: 427 eligible tracked files, 19,241,511 bytes, 645 candidate
entries, no ignored or untracked entries. The earlier build checkout's 858
eligible files included ignored package/source material, so it is not the
bounded input. The fresh checkout's independent Debug solution build did not
reproduce: a restore-disabled MSBuild attempt exited 1 with 3 warnings and 49
errors because the pinned SQLite assembly and PostSharp 2 targets package were
not present. The build checkout has those version-named packages, but the
separate clean checkout does not; its ignored x64 SQLite interop copy is also
absent. Package and native-copy provenance sufficient for a clean, independent
reproduction has not yet been established. The documented build command first
attempted legacy NuGet restore and was stopped before any package directory
appeared. No private bounded
receipt, representative test, TraceMap scan, or FullCorpus result is claimed.
The public profile/guard PR precedes any private Windows receipt attempt.

Public local checks on this branch: Task 11 synthetic guards passed, including
427-file admission, 428-file rejection, candidate-entry limiting, generated
input rejection, and cross-profile receipt rejection; focused exact-source
scope tests passed 7/7; locked restore and solution build under `-warnaserror`
passed with zero warnings/errors; the private-path guard, Kiro self-test, and
`git diff --check` passed. `PublicSmoke` needs a clean committed TraceMap head
and passed with a private local receipt at code commit `7d4c748d97bafa00ef936f14ca3a0afd130911b9`.
The PR head will receive a fresh exact-head smoke run. No full .NET suite or
private corpus scan is claimed by these checks.

## Compound projectless VB Web Forms chains (2026-09-23)

Branch `codex/webforms-page-chain-repro` starts from `origin/dev` at
`9c136d6b` after PR #788 merged. Issue #789 records the unresolved private
pages 2, 3, and 11 acceptance failure without publishing private artifacts.
The new public-safe `vb-compound-pages` fixture has five same-name receiver
methods in one file, two further call levels, and two/zero/one supported SQL
terminals by page. Before the fix, the scan retained downstream VB call and
SQL facts, and the unbounded graph retained receiver bridges, but the bounded
single-index packet reader admitted only its first wave of projectless VB
method bodies. It therefore reported zero terminals for the page-two analogue
even with a large display depth. The reader now admits newly discovered method
bodies until no new syntax call names remain, subject to explicit input work,
frontier, fact, and text limits; an interrupted closure is marked partial.
Regression tests pin exact same-name bridges, depth-8/depth-10 inventory,
a two-root merged-index packet, no-terminal page-three scope, and a deliberately
exhausted work limit. Private page improvement remains unverified pending a
fresh merged-index rerun; Task 10 and Task 11 remain open.

Public local validation: focused compound regression passed, full .NET suite
2,269/2,269 passed, zero-warning solution build under `-warnaserror` passed,
and the focused page traversal, depth, completed-depth-summary, and targeted
depth-comparison PowerShell tests passed. JSON parsing, `git diff --check`,
and the private-path guard passed. No private work-corpus scan was performed.
