# Evidence admission debugging and regression ledger

Recorded 2026-10-15 on `codex/evidence-admission-hardening`. This ledger is a
public-safe reconstruction from branch history and sanitized operator readbacks,
not a publication of private artifacts. Operator readbacks are not independently
replayed acceptance on this machine. No private names, paths, binaries, SQL,
screenshots, or artifact hashes belong in this ledger or its fixtures.

## Debugging lessons that must survive review

| Boundary | Lesson / failure mode | Existing regression anchor |
| --- | --- | --- |
| Compilation scope | Publish inventory is not the semantic compilation roster. App_Code facts do not prove page code-behind was admitted. Partial compilation remains partial. | `ExplicitVisualBasicContextTests`; hash/commit/source/reference refusal matrix |
| Registration | Namespace-only App_Code control registrations can be candidates, not proven runtime instances. | `AppCodeRegistrationMatrixTests`; semantic registration matrix in `ExplicitVisualBasicContextTests` |
| Source body versus graph | A declaration-only span does not mean no body or outgoing calls. Inspect retained facts and graph separately. | `Syntax_page_reaches_semantic_database_body_only_through_unique_bridge`; nested/inherited/wrapper cases |
| Source database terminals | A retained database operation may belong to an unrelated method. Counts do not establish handler reachability. | source database audit matrix in `ExplicitVisualBasicContextTests`; `WebFormsDatabaseEvidenceAuditTests` |
| Query scope | A database-filtered query says nothing about absent HTTP operations. Compiled-only prewalk cannot consume source bridges. | `CombinedDependencyPathTests`; explicit-reference compiled-only negative |
| IL observation | IL calls are `ManagedIlCallObserved`, not source `CallEdge`. Command/value-analysis gaps do not erase observed calls. | `IlBodyEvidenceExtractorTests.Unbound_provenance_still_records_operand_aware_bodies`; Python retained-IL helper tests |
| Owner identity | Graph nodes may be represented by a body/property fact instead of the declaration fact. Join exact source-index and symbol identity, not representative fact ID. | `scripts/validation/test_inspect_retained_il_body.py` |
| Overloads | A same-named wrapper body may be the wrong overload. Compare exact metadata identity and bytes before interpreting its calls. | `Explicit_reference_bytes_connect_external_wrapper_without_promoting_provenance`; helper source-witness checks |
| Roslyn retargeting | Reconstructed parameter assembly versions differed from raw metadata. Decode the resolved token from captured reference bytes. | `retargeted-reference` case, commit `e33e59cb` |
| Bounded projection | Extracted witnesses were dropped by compact readers. Ordinary-reader success does not establish bounded-reader success. | both readers in explicit-reference matrix; commit `b52a0c51` |
| Admission | Exact reference bytes permit a separately tiered candidate bridge, not original-build provenance promotion. Wrong hash, wrong identity, missing proof, duplicates, virtual dispatch must not bridge. | eight-case explicit-reference matrix; commit `4d4d4377` |
| External API terminals | An observed external API operand need not have an admitted method body. Do not fabricate framework implementation evidence. | `FrameworkSignatureCandidateTests`; legacy SQL fixture |
| Enumeration | Retained routes survive truncation. Variant cap is not a complete inventory; grouping removes no evidence. Terminal tier does not upgrade the route. | `GroupedCompiledPathHandoffTests`; commit `1dc37d3e` |
| Tool/runtime receipts | Wrong bundle directory and changed runtime bytes are workflow problems, not graph results. Never rewrite sealed checkpoints to bypass identity checks. | `WebFormsReviewExecutionTests`; runtime inventory/retained-tool tests |
| Earlier integration | Compiled-only prewalk, pre-4.0 runtime-floor evidence, SQL regex initialization, Windows path/hash boundaries need their existing focused gates retained. | branch commits `28177893`, `575f5c57`, `c6f9f2ad`, `bc337541` |

## Current application-independent HTTP findings

H1: operator readback verified event binding → handler → HTTP caller/terminal
with exact semantic joins. H2/H3: later readback found a retained user-control
event → constructor → profile service → HTTP chain, including a separate token
request. Host-page containment is a different relationship. Alternate property
callers remain uninvestigated; markup data binding is a hypothesis, not a proven
extractor defect. H4: calls from a `CreateChildControls` override through helpers
to HTTP are retained; framework entry is a separate modeling question.

Do not invent `Page_Load → CreateChildControls` or page → click-handler calls
from markup containment. A real override relationship, a framework entry
candidate, a markup reference, and an actual source call are different evidence.
Neither a method name nor a page-level warning establishes or invalidates a path.

The supplied illustrative code needs repair before use as a fixture: its token
helper is described as called but is omitted from the example caller body; its
event fields and framework scaffolding are incomplete. Synthetic fixtures must
contain the actual calls they assert. The final readback also reused H3 for a
constructor branch; retain operation identities rather than trusting these labels.

## Required new synthetic matrix

- Explicit event → helper → HTTP and event → constructor → HTTP/token chains.
- Explicit page-method → control-method calls versus markup-only containment.
- Explicit property access versus declarative-only property references.
- Lifecycle override → helper → HTTP versus inert override and same-name non-override.
- No cross-control event joins, no fabricated framework dispatch, no fake HTTP
  provider promotion, no transfer of the terminal's tier to weaker edges.
- Scan → SQLite → combine → ordinary AND bounded HTTP path queries; deterministic
  repeated output and exact symbol joins, not only counts.

## Scope decisions

Database call tracing is sufficient for the current exercise. Schema, stored
procedure contracts, populated DataSet/DataTable assumptions and source-inferred
column models are not runtime or schema verification. Optional SQL Server/schema
fixtures remain deferred under #870; the v4 wording was not a release commitment.
#861 and #864 remain separate broader workstreams, not closed by this ledger.

Private readbacks established two retained static database read routes with
incomplete variant enumeration; source-assisted write analysis was a separate
layer. Do not relabel those read routes as proven writes. No database/HTTP target
execution, full-site completeness, production use, or historical-build claim.

## Reproductions discovered while implementing this ledger

1. An explicit read of a source-defined read-only VB property retained
   `PropertyAccessed` but no caller-to-getter edge; the getter's constructor/HTTP
   body was disconnected. Fixed with `vb.semantic.readonly-getter-call.v1`,
   extractor 0.10.4. Writable/default/indexed/virtual/external getters, ByRef,
   NameOf and deferred anonymous functions are deliberately excluded. This does
   not prove the private unresolved callers used explicit property reads.
2. Real System.Web fixture: an inert override calling MyBase acquired a sibling
   override's HTTP route through an override-candidate hop. Fixed by retaining
   explicit nonvirtual dispatch evidence for MyBase/MyClass and honoring it in
   ordinary/bounded paths and terminal-witness prewalk, algorithm 1.8. Mixed
   virtual/nonvirtual calls to the same target must remain distinct.
3. Fixture-development failures: initial runtime reference closure was incomplete;
   the compilation gate caught it. HTTP path surfaces also include a legacy
   request-creation binding, separate from outbound send operation facts. Tests
   count the two operation terminals using their evidence, not all HTTP surfaces.
4. A partial compilation can retain a property operation for an invalid write to
   a read-only property. The new getter rule explicitly rejects assignment targets
   and invalid operation contexts; a failing-then-fixed negative pins this case.

### Remaining modeling work (not silently fixed by these changes)

- Host-page/control containment and declarative property access need their own
  evidence-bearing relationships. Unknown private markup remains unknown.
- Framework lifecycle candidate roots may be useful, but must not be emitted as
  ordinary page-to-override CallEdges. The real framework sample now protects
  the existing override-body evidence and the absence of a fabricated page call.
- Writable/accessor identity separation, virtual/default-property invocation and
  unqualified property reads are outside the new explicit read-only getter rule.
- Nonvirtual dispatch evidence added here is VB MyBase/MyClass-specific; this
  change is not a claim of equivalent C# base-call coverage.

## Validation gate

Run the new HTTP matrix, adjacent HTTP/VB/WebForms suites, explicit-reference and
source-metadata reconciliation suites, combined paths and grouped handoff suites,
and Python helper tests. Run the full .NET suite and a public CLI smoke; retain
failures and distinguish environmental failures from passes. Follow
`docs/VALIDATION.md`; pinned OSS/Windows/private runs are separate gates and must
be explicitly deferred when unavailable. No PR should drop a negative control
or widen evidence admission merely to make an expected path appear.

### Implementation validation receipt (2026-10-15)

- Production/regression commit `10b65897`: 17 new tests passed; 43 Python
  diagnostic tests passed; public net48 sample build had zero warnings/errors.
- Frozen full .NET run: 4,333 passed, one Windows-only test skipped, one stale
  algorithm-version assertion failed (expected 1.3, actual 1.8), 27m12s.
- Test-only correction `39e38e2d`: affected native workflow and new HTTP matrix
  passed 18/18 in 46s. No production code or path assertions changed after the
  full run. This is not represented as a second clean full-suite run.
- CLI scan at clean `39e38e2d` succeeded: 107 facts, Level1SemanticAnalysis,
  BuildStatus Succeeded, all required outputs present.
- Private-path guard and diff whitespace checks passed. Private application
  inputs are not part of this corpus. Pinned OSS, Windows/private scans and
  non-.NET adapter suites remain explicitly deferred, not inferred passes.
- Before a PR, preserve this ledger and all negative controls, check version
  changes, and rerun CI on the final review head. Do not convert the deferred
  modeling questions above into asserted semantic paths.
