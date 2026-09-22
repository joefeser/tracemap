# Compiled .NET Evidence Foundation Implementation State

Status: Tasks 1-9 are merged into `dev`; PR #774 completed its authorized exact-head ACK review and was merged into `dev` as `ed2fdf1c028b034a9a6013908e8f5b8c29d900a7` on 2026-09-21. The first Task 10 slice (bounded operand-aware IL body/call evidence, PR #775) is also merged into `dev` as `46b2baa125bed00ee9ac1964ce50febd55c3e68e` on 2026-09-21, including the owner-requested P1/P2 follow-up `2d20b4a3` (prefix-operand signed/unsigned distinction and exact UTF-16 literal hashing). PR #779 (slice 4, bounded rewrite PDB identity evidence) merged into `dev` as `7be1f51c0360a7f80e3b7304aee9677fe3c92ddc` on 2026-09-22. The remaining Task 10 public rewrite suite from #766 and Task 11 remain open.

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
