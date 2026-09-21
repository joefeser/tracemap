# Compiled .NET Evidence Foundation Implementation State

Status: Task 8 exact source-to-metadata reconciliation merged in PR #773; Task 9 implemented in PR #774 with local and cross-platform acceptance green, final exact-head ACK pending

Branch: `codex/pdb-sequence-point-evidence`

Base: `origin/dev` at `7dc943f2f9d5de82b0963e3e1b8aa9196116b51c`

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

- focused `PortablePdbExtractorTests`: 27 passed, zero failed, zero skipped;
- combined PDB, source/metadata reconciliation, managed metadata, and receipt
  contract filter: 87 passed, zero failed, zero skipped;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero warnings
  and zero errors;
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 2,028 passed, zero failed,
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

The final current-head review found two sibling violations of the same
admission/work-bound invariant. PDB assembly binding now consumes only exact
artifacts retained by the compiled evaluator's admitted prefix, so an omitted
byte-identical assembly cannot reenter matching or manufacture an ambiguous
candidate. Cecil type traversal now reuses the iterative managed-metadata
walker and charges every type before its methods, so deeply nested empty types
cannot overflow the stack or evade the PDB work budget. The regression matrix
covers the omitted duplicate, 10,000 nested empty types, and type-budget
exhaustion before any method visit.

PR #774 implementation-head CI at
`34ded8aef9b60b6f7db225d1c8af5df6b96ea159`
passed the .NET, JVM, Python, Swift, TypeScript, five-adapter combine, private
path, and package-smoke jobs on macOS, Ubuntu, and Windows. The Windows lane
used desktop Roslyn C# and VB compilers to produce real MSF PDBs, recognized the
complete native signature, emitted only the bounded unsupported-reader gap, and
produced zero positive native PDB facts. This is not a native-Windows support
claim. The Task 9 checkbox is complete; final current-head ACK remains the PR
terminal gate, and the PR must not be merged by this task.
