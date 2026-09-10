# Implementation state

## Handler-rooted HTML and anonymous review packet (2026-09-10)

The one-case source review now renders private HTML organized as trigger,
handler root, nested retained callees, and anchored evidence excerpts. Repeated
caller/callee edges collapse into one tree entry while retaining links to every
call-site witness; cycles and shared callees are references rather than recursive
duplication. A separately generated shareable HTML/JSON pair contains only
report-local aliases, structural classifications, edge counts, public rule IDs,
evidence tiers, closed conclusions, and limitations. It excludes source text,
paths, symbols, fact IDs, SQL, URLs, configuration, and commit identity. The
shareable HTML renders an alias-only Mermaid graph when its browser module is
available; private HTML loads no remote script.

Validation: focused code-path review tests 2/2; full .NET suite 1789/1789;
PowerShell parse, scoped formatting, private-path guard, and diff checks passed.

Field dogfood confirmed the alias graph and repeated-call collapse rendered in
the work browser. The private report now embeds that shareable graph in a
sandboxed script-only frame and provides a private alias-to-symbol/evidence
legend outside the frame. The remote Mermaid module can access only the
anonymous child document, not the source-bearing parent. Bounded trigger source
is duplicated inline before the handler-rooted path so a reviewer can read down
without an initial navigation jump; the full anchored evidence remains below.
Validation after this layout refinement: focused tests 2/2 and full .NET suite
1789/1789; scoped formatting, private-path guard, and diff checks passed.

The private review now uses native disclosure regions. Trigger, retained call
path, and human verdict start open; the Mermaid graph and detailed evidence
start closed. Fragment navigation opens any enclosing disclosure automatically,
so evidence links remain usable without forcing the long sections open at load.
Validation for the disclosure refinement: focused review tests 2/2, formatting,
privacy, and diff checks passed. The full suite passed 1788/1789; the unrelated
global-activity isolation assertion observed a concurrent scan, then passed 1/1
when rerun alone.

## Batch local handler review (2026-09-10)

Field triage after Fill terminal projection still has seven terminal-free chains
on three pages, while the traversal-limited group fell from 51 to 50. The user
requested an end to repeated single-question diagnostics. Added
`New-FocusedWebFormsBatchInspection.ps1`: one command reuses the existing settings,
builds the diagnostic helper, and writes a private Markdown review plus JSON for
every selected handler. Windows opens the Markdown in Notepad. No rescan is needed.

The existing raw exact semantic closure is shared across all handlers; one extra
bounded query retrieves call sites, selected binding/handler locations, and exact
declaration witnesses. Each case includes all direct calls, stopping locations,
the retained closure, provenance, explicit bounds, and an unreviewed result slot.
Private UI/database classifications require local source review. Reports use a
separate filename prefix so database audits cannot accidentally select batch JSON.
The location query shares remaining row/text budgets and has a 20k-record ceiling.
Existing input and per-handler traversal limits remain in force.

Validation: full .NET suite 1786/1786; multi-handler/sibling/location/privacy and
bounded-case regressions passed; actual PowerShell command created both artifacts
from a synthetic SQLite index. Modern sample CLI scan, scoped formatting,
private-path guard, and diff checks passed. Windows Notepad launch awaits work
machine use. Existing PropertyMappingTests nullable warning remains unrelated.

## Receiver and property assignment extraction (2026-09-09)

Incremented semantic extractor to 0.21.0. Ordinary explicit member invocations
retain receiver symbol/type/identity; property access retains receiver identity
and direct simple assignment RHS symbol/identity. No raw values or snippets added.
Database census consumes this metadata and remains compatible with older indexes.
Real System.Data.Common framework compilation is scanned and persisted through
SQLite before audit assertions, including typed parameter display, inherited
Fill, a StoredProcedure assignment, a read, and a later Text reassignment. The
census deliberately does not infer value-at-Fill from assignment occurrence.
Full provider-specific SQL command object flow and private Windows scan remain
operator validation; the test uses real abstract framework parameter types.
Pinned public OSS smoke is deferred for this slice; full .NET suite and the
compiled framework scan/storage fixture provide local validation.
Validation: full solution 1784/1784; modern-sample CLI scan completed with semantic
analysis; formatting, private-path guard, and diff checks passed. Existing
PropertyMappingTests nullable warning remains unrelated.


## Database evidence linkage diagnostic (2026-09-09)

The exact-caller census now privately compares command construction `assignedTo`
metadata with adapter-constructor `argumentSymbol` metadata and reports only the
match count. It explicitly distinguishes unretained Fill receiver identity and
CommandType assigned-value metadata from absent source. Same-method local-name
matching supports command-to-adapter flow but is not claimed as object identity;
adapter-to-Fill linkage remains unestablished by the retained fact shapes.
Validation: focused audits 17/17; full solution 1783/1783; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.


## Qualified framework display symbols (2026-09-09)

Accept one leading `global::` for database census framework classification only.
Keep the original exact caller identity in SQLite queries. Regression coverage
includes qualified Fill signatures with parameter names, qualified constructors
and properties, fake framework names, and exclusion of unqualified caller rows.
No application scan, source changes, or inspection regeneration required.
Validation: focused audits 17/17; full solution 1783/1783; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.


## Distinct Fill audit failures (2026-09-09)

Field returned RawAuditFillCallerUnavailable, which conflated inspection-hop
recognition with missing exact-caller index evidence. Split these into closed
codes for no recognized hop, missing caller identity, multiple callers, and
missing index witness. Emit safe hop counts and post-provenance caller counts
before failure. Wrapper prints allowlisted generated filename, UTC timestamp,
and content hash, never a private path/custom filename. No type matching or
source assumptions broadened; this diagnoses the actual cause on the next run.
Validation: focused audits 15/15; full solution 1781/1781; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.

## Database evidence census (2026-09-09)

Operator confirmed command construction with SQL/connection, StoredProcedure
assignment, parameters, adapter(command), and Fill in the source. Added a
read-only original-index census scoped to the exact framework Fill caller from
the newest local inspection. Reports closed fact and semantic metadata counts
only; never SQL, parameters, symbols, paths, or arbitrary property values.
Does not assert object linkage from cooccurrence or stored-procedure mode from
a property reference. Validates snapshot; caps 10k rows/8MiB text. Wrapper uses
existing path settings, requires no new method hint, and does not regenerate
the local inspection or application report.
Validation: full solution 1780/1780 before the additional missing-Fill regression;
final focused audits 15/15, including inherited CommandType references and
missing Fill rejection. PowerShell/helper smoke, formatting, privacy guard,
and diff checks passed. No conclusion about command/adapter identity yet.

## Method-starting local inspection (2026-09-09)

Added New-FocusedWebFormsMethodInspection.ps1 with a local interactive method
hint. Does not commit the private method name. Resolves one exact semantic
call/invocation signature (short or qualified name); absent/ambiguous matches
fail closed. Reuses the bounded raw closure and local call-site report, without
requiring hints for intermediate abstraction layers. Report supplies snapshot
identity, not an inferred page/event association. Fill-named stopping symbols
are preferred for the sample path, but this is explicitly not SQL evidence.
All direct sibling calls remain visible. No application scan or execution.
Validation: focused audit 12/12, full solution 1778/1778, and method-wrapper
PowerShell/helper smoke passed. Formatting, private-path guard, and diff checks
passed. The work index must still be checked for the operator's method match.

## Whole-handler direct-call inspection (2026-09-09)

Operator inspection showed the selected stopping branch only toggles panel
visibility; later operations are sibling calls in the parent event handler.
This is not evidence of a tracing defect. Extended the local inspection with
all retained exact Tier1 direct call sites and a separate downstream stopping
summary per callee. CallEdge/MethodInvoked witnesses at the same target/span
are grouped; repeated sites are retained. Source order is not execution order.
The previous one-path section remains compatible but is explicitly one sample
branch. The form list, application index and sanitized console stay unchanged
except a safe direct-site count. Bounds: 2,000 direct witness rows, 500 symbols
and 10k edges per cached callee summary; limits are not silently hidden.
Validation: focused audit tests 9/9, full solution 1775/1775, and local inspection
PowerShell/helper smoke passed. Formatting, private-path guard, and diff checks
passed. Field source comparison is still required; no extraction defect claimed.

## Local-only source inspection (2026-09-09)

User requested concrete source locations after the raw audit found seven
handlers, 86 selected facts, 18 exact symbols, and no invocation-source without
a semantic call source witness. Added an opt-in local inspection JSON via
New-FocusedWebFormsLocalInspection.ps1. It selects one unbounded raw stopping
sample, reconstructs its BFS parent chain, and queries retained fact locations.
The final hop is labeled a call site, never a callee definition. A local Go To
Definition check and source-commit verification are required. Private symbols
and paths are stored only in this explicitly requested file, not console output.
Existing raw summary behavior is unchanged unless inspection is requested.
Output is create-new, uses the existing path configuration, and does not edit
the form list, read source text, execute the application, or regenerate reports.
Validation: full solution 1774/1774; final focused audit tests 8/8; new PowerShell
wrapper/helper smoke passed. Formatting, private-path guard, and diff checks
passed. Source lookup on the work checkout remains the operator's next step.

## Raw audit text-limit fix (2026-09-09)

Field run failed with RawAuditTextLimit because the helper materialized all raw
call/invocation/declaration symbol text before traversal. Replaced that preload
with parameterized exact-symbol frontier reads batched across all handlers.
Each symbol is loaded once; declarations select by target and call witnesses by
source. At most twelve fact queries (eleven depth layers plus a final witness
read for admitted work-bounded symbols). No limits were raised. Index remains
read-only; no source index is added or original scan/report regenerated.
Regressions cover unrelated oversized text/row exclusion and continued rejection
of oversized selected evidence, alongside snapshot, privacy, and traversal tests.
Validation: focused audit tests 6/6; full solution 1772/1772; PowerShell helper
smoke against a retained SQLite fixture passed. Formatting, private-path guard,
and diff checks passed. Work-machine performance is still a field check.

## Independent raw-index audit (2026-09-09)

Correction: compacted symbol witnesses cannot rule out extraction/attachment
problems. The packet does not retain exact unresolved leaf IDs. Added
`Test-FocusedWebFormsRawEvidence.ps1` and a separate .NET helper to audit exact
semantic call/invocation closure from priority handlers against the original
read-only index, before compaction. This is not report-leaf reconstruction.
The helper validates scan/commit, enforces input and traversal bounds, and emits
closed counts only. No form-list edits, application scans, or report generation.
See README for limits and diagnostic rule limitations.

Validation: full solution 1769/1769 (before the additional traversal-bound test),
then focused audit tests 4/4 including that test. PowerShell/helper end-to-end
SQLite fixture passed; the index-byte preservation and syntax-isolation checks
passed. Private-path guard and diff check passed. No work-machine index or
application source was available locally, so field behavior remains unverified.

## Exact canonical leaf source availability (2026-09-09)

The field call-evidence diagnostic classified all seven terminal-coverage-review
chains as having no exact source-owned call-shaped evidence in the compacted
reader. This cannot rule out original-index call facts or attachment problems.
Added a second closed leaf diagnostic that independently records exact method
declaration evidence and exact source-owned body-operation evidence. The selected
bounded reader admits `MethodDeclared` rows only by exact target-symbol equality
and retains those witnesses even when the symbol node already exists. Simple-name
syntax declarations are not reconciled to qualified canonical methods. The four
canonical states distinguish declaration+body, declaration only, body only, and
neither; noncanonical leaves are not applicable. The last state deliberately
cannot distinguish external, generated, excluded, empty, or unavailable bodies.
No identities, paths, or source are published and no traversal/join changed.

Validation: focused packet/path tests 68/68, full .NET solution 1766/1766, and
the synthetic PowerShell summary test passed. Targeted formatting verification,
private-path guard, and diff check passed before commit.

## Exact canonical leaf call-evidence classification (2026-09-09)

Field leaf/reconciliation evidence showed seven terminal-free chains containing
both canonical Tier1 method leaves and deliberately isolated Tier3 projected
targets. Rule IDs on a leaf can originate from evidence that created the node and
therefore do not prove that the leaf method owns an outgoing invocation. Added a
bounded closed `LeafCallEvidenceStates` set that compares exact canonical source
node IDs with retained `MethodInvoked`/`CallEdge` facts and actual outgoing graph
edges. It distinguishes missing call-shaped evidence, an invocation without its
paired call fact, a call fact without its graph edge, path-local cycle rejection,
dispatch cross-hop rejection, and defensive retained-but-untraversed edges.
Noncanonical leaves are marked not applicable. No raw identity is emitted and no
name-based reconciliation or traversal bound changed. The actionable-gap script
prints the new aggregate field and remains compatible with older packets.

Validation: focused packet/path tests 68/68, full .NET solution 1766/1766, and
the synthetic PowerShell actionable-gap test passed. Targeted formatting
verification, private-path guard, and diff check passed before commit.

## Per-chain truncation reason retention (2026-09-09)

Field evidence showed identical depth-8/10 terminal-free results: all resolved
truncated chains already had joined downstream edges and nonzero handler-owned
call evidence. Added a closed, sorted `TruncationReasons` set to internal root
observations and an additive initialized property on the public Web Forms
observation, preserving its existing constructor and older JSON readability.
Search marks the applicable root for `depth`/`cycle` and all affected pending
roots for `frontier`/`path`/`work`; merged roots take a deterministic union.
Packet JSON and Markdown expose the set under the existing rule and limitations.
The read-only triage prints direct per-chain reason counts; old packets print
`not-retained`. Reasons describe static bounds, can coexist, and are neither
runtime conditions nor exclusive causal claims.

Validation: focused packet/path tests 67/67, full .NET solution 1765/1765,
and the synthetic PowerShell retained-triage test passed. Formatting verification
passed for both changed C# files. The whole-solution formatting check remains
noisy in unrelated pre-existing files and was not used to rewrite them. Private
path guard and diff check passed before commit.

Field handoff: a separate run-and-triage wrapper calls the locally configured
page-list runner and then passes the one freshly written JSON to read-only
triage. Missing or ambiguous fresh JSON fails closed; no prior report is
selected. Keeping orchestration separate avoids modifying the tracked file in
which the operator keeps the local 43-form list.

## Retained observation-state output (2026-09-09)

Field comparison at depths 8 and 10 found the same terminal-free page buckets;
all exact-node truncation-reason associations remained `not-established`.
Extended the existing read-only triage to print whitelisted `stopState` and
`callEvidenceState` counts plus aggregate retained handler-owned call evidence
per alias. This uses packet fields already present in the completed JSON and
does not launch another traversal. Synthetic tests cover each useful state,
numeric aggregation, unknown-state withholding, and existing privacy bounds.

## Retained reason comparison (2026-09-09)

Added `Compare-CompletedWebFormsPageTriage.ps1` on the existing restricted-run
branch. Reads depth-8/10 sequentially, buffers small output until source and
selection equality checks pass. Extended triage with closed truncation reasons
associated only through exact retained path-node IDs. No causal attribution or
new traversal. Missing associations stay `not-established`; shared-node matches
are not exclusive per-page stopping reasons. Synthetic tests pass for linked
depth reason, unrelated-node exclusion, missing associations, matched pairs,
source mismatch rejection, optional fields and private-string suppression.
Scripts-only change; Windows field run pending. No .NET changes or full suite
rerun for this follow-up.

## Retained page triage (2026-09-09)

On `codex/restricted-webforms-run-evidence-20260902`, added
`Triage-CompletedWebFormsPages.ps1` after the user confirmed the completed run
used ff4059. Reads existing depth-8 only; no execution or traversal path.
Separates terminal, missing handler, truncated observation, downstream without
terminal, zero downstream edge, and unavailable observation buckets. Includes
no-retained-event pages. Focus aliases page-004/page-026 link gaps by exact
binding supporting ID only, without treating links as proof of cause.
Synthetic PowerShell tests passed for discovery, absent/null fields, buckets,
exact links, unrelated-gap exclusion, and private-string suppression.
No .NET code changed in this follow-up; Windows field execution remains pending.

## Selected-handler graph admission follow-up (2026-09-08)

The 43-page work-machine report matched all requested forms but returned 191
globally downgraded event chains, no downstream boundaries, and a truncated
packet after the repository-wide graph input ceiling was reached. That output
is retained as regression evidence; it is not an application-level absence
claim.

The bounded single-index reader now uses the packet's exact selected handler
fact IDs to seed a deterministic symbol closure through call, object-creation,
parameter-forward, and symbol-relationship edges up to the existing
depth/frontier bounds. It reads only facts and normalized edges connected to
that selected closure, plus exact supporting fact IDs. Unselected callers
cannot consume the selected page budget. All existing fact/edge/text limits
remain fail-closed, and an
over-limit selected neighborhood still suppresses path classifications and
emits `GraphInputLimitReached` through the packet's existing rule-backed gap.

Synthetic coverage verifies that 100 unrelated call-shaped facts no longer
hide a selected handler-to-database path under a deliberately small admission
budget. Oversized payload evidence inside the selected neighborhood still
fails before JSON allocation. Existing full-reader compaction behavior remains
unchanged when no explicit handler selection is supplied.

The first restricted rerun after this change had no input-admission, traversal,
event-chain, or boundary limit classification. It retained exactly one
`WebFormsModernizationGapLimitReached`, so its remaining `truncated: true` was
the 1,000-row output gap cap. The committed page-list wrapper now passes
`--max-gaps 5000` and reports any remaining `LimitReached` or
`TruncatedByLimit` classifications directly after publication. This changes
report retention only; it does not widen graph traversal, scan the repository,
or alter application evidence.

The completed non-truncated report exposed overlapping human-readable states:
a handler-unavailable chain also has no terminal, so raw text counts cannot be
added. `Summarize-FocusedWebFormsPageList.ps1` now selects the newest retained
page-list JSON and emits three mutually exclusive chain counts, exclusive page
categories with safe aliases, and terminal-kind totals. The script is read-only
and excludes private paths, symbols, SQL, and source content from its output.

The complete 43-page result contains 191 exclusive chains: 41 without a
resolved handler, 143 with a resolved handler but no terminal, and 7 with a
static SQL-query terminal. Seven pages have at least one SQL terminal, 35 have
resolved handlers but no terminal, and one has no static event binding. Added a
second zero-argument, JSON-only triage script to split the 143-chain population
by defensible stop state and aggregate its public evidence metadata. No raw
application identifier is emitted.

## Retained-index page-list report (2026-09-06)

Rebased this diagnostic branch cleanly onto `origin/dev` at `af72e8b9`, which
contains the merged Base44 work through PR #719. Added optional
`webforms-modernization --surface-list <file>` filtering and the committed
`Invoke-FocusedWebFormsPageListReport.ps1` wrapper for the work-machine run.
The input is a line list or first-column CSV; repo-relative paths match exactly
and filename-only values must be unique. Output retains deterministic page
aliases and hashes rather than copying raw list values. Matched entries restrict
page/event traversal; unmatched and ambiguous entries emit packet-rule gaps.
The Markdown coverage table reports only static event-chain and downstream
boundary evidence plus the first unresolved state. It makes no runtime,
rendering, branch, binding, SQL-execution, or whole-application claim.

On 2026-09-08, added `Run-FocusedWebFormsPageList.ps1` as the human-operated
entry point. Its top edit block contains the retained-index path, output root,
and a here-string accepting one unquoted `.aspx` path per line. It creates the
temporary list and timestamped output internally, then delegates to the tested
parameterized wrapper. This avoids chat copy/paste and repeated command editing.

## Field ownership result and next handoff (2026-09-03)

Operator screenshots report ownership-verified for the same event on 0.7.1:
one projection, six supporting facts and two edges resolved, zero mismatches
or missing IDs. Both prior unrelated edges remain indexed but are absent from
this projection; the legitimate direct edge remains. Syntax support stays Tier3.
This closes the field check for the selected event only. No runtime or broad
coverage claim follows.

Added claude-database-backed-event-trace.prompt.md for an operator-selected
different event using the existing index, with the original bounds and privacy
constraints. No new scan required. Future work-machine handoffs must be pushed
as repo prompt files and linked in README because the computers are separate.
Documentation-only change; validate privacy and diff checks, not a new code-test run.

## Retained projection verification handoff (2026-09-03)

Field screenshots report scanner 963392f4, extractor 0.7.1, zero workspace
diagnostics and the retained three-hop semantic path to an HTTP boundary.
The report says no projection was found for the binding; that is not sufficient
to verify ownership isolation because projection lookup uses handler identity
and exact supporting fact IDs, not direct bindingFactId. Added a bounded,
read-only prompt for that lookup and support validation. README makes clear no
new scan is needed. Field ownership verification remains pending; omitted
branches remain unverified. Documentation-only change; privacy and diff checks
are the relevant validation, with no new code-test claim.

## Handler ownership correction (2026-09-03)

On the current diagnostic branch, `legacy-webforms/0.7.1` fixes direct event-flow
support accepting same-name members from unrelated files/types. Admission now
requires the resolved handler file and contained line span. Tier1 support also
requires its canonical source symbol ID (including assembly identity). Logic
signals share admission and select their syntax method by the resolved span.
Syntax name/span evidence remains lower-tier; overlapping same-line declarations
are an explicit limitation, not compiler-resolved ownership.

Synthetic regressions cover same-name different-file/type edges, a same-span
different-assembly semantic identity, retained own-handler support under reduced
coverage, syntax fallback, and reversed-input determinism. No private source is
used. Validation: full .NET suite 1760/1760; focused extractor suite 45/45;
synthetic non-compiling CLI smoke 65 facts with reduced coverage; changed-file
format verification, private-path guard, and diff checks. Existing unrelated
PropertyMappingTests nullable warning remains. README and one-page prompt now
require a fresh 0.7.1 index and the same selected event; no PR or merge requested.

Original implementation branch: `codex/webforms-bounded-report-memory`
Current diagnostic-debug branch: `codex/restricted-webforms-run-evidence-20260902`
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
reported 932,070 Tier1 facts and 10,588 occurrences of
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset`. Code
inspection subsequently established that this projection can conflate ordinary
`CompilationDiagnostic` rows with genuine workspace failures for legacy
projects. It is not evidence of 10,588 proven toolset failures. The bounded
count-only queries, local-inspection boundary, and synthetic reproducer are
recorded in [`README.md`](README.md). The on-device count-only follow-up found
only one retained `WorkspaceDiagnostic`, one scan-scope gap, and no retained
`CompilationDiagnostic` rows under `csharp.semantic.workspace.v1`; the 10,588
projected rows therefore remain origin-indeterminate and demonstrate missing
diagnostic lineage rather than a proven environmental root cause.

Implementation and runbook are committed locally; pushing/opening a PR and
repairing ACK setup remain separate next steps. No merge readiness is claimed.
Use the README's large-index/OOM link for the private Windows rerun after the
branch is made available there. Keep the successful 300-surface packet and the
original scan index, select a new output directory, and compare counts/gaps and
memory. A deterministic input-limit gap is a truthful partial result, not proof
that every requested event chain was analyzed. Full private-index completion
and any subsequent root-specific lazy-loading design remain unverified here.

## Diagnostic-lineage debug patch

At the operator's direction, the diagnostic projection correction is being
debugged on `codex/restricted-webforms-run-evidence-20260902` alongside the
sanitized field notes. After synthetic and restricted validation settle the
behavior, the code/test/rule/script changes should be cherry-picked onto a fresh
branch from `origin/dev`; the field documentation need not be included in that
product PR.

The patch separates ordinary compiler diagnostics from workspace admission,
adds closed origin lineage and safe diagnostic IDs to projected environment
facts, limits legacy-prerequisite corroboration to genuine workspace/load
origins, deterministically aggregates exactly equivalent projections, and makes
the PowerShell readback report unknown lineage explicitly for pre-fix indexes.
No raw native diagnostic message or private identifier is added.

Validation on 2026-09-02:

- full .NET solution: **1,737/1,737 passed**;
- focused diagnostic/snapshot suite: **32/32 passed** before the final safe-ID
  extraction case, followed by a green full solution run containing that case;
- `Export-FocusedWebFormsWorkspaceSummary.Tests.ps1`: passed, including legacy
  unknown-lineage, compiler-origin, workspace/load-origin, static-origin, and
  occurrence-count cases;
- changed-file `dotnet format --verify-no-changes`: passed;
- private-path guard and `git diff --check`: passed;
- synthetic CLI classifier fixture: 37 facts, truthful
  `Level1SemanticAnalysisReduced`; `CS0103` remained a compiler-origin
  `AnalysisGap`, no legacy-toolset prerequisite projection was emitted, and
  Web Forms page/control/event/handler evidence remained present.

The repository-wide formatter still reports unrelated pre-existing formatting
violations outside this change, so validation is scoped to all changed C# files.
No non-.NET adapter changed; pinned language-adapter smokes are deferred.

### Projection-boundary follow-up

The restricted post-fix review verified the lineage patch and exposed a second,
independent projection defect: 10,609 `PropertyMappingShapeUnsupported` and 24
`PropertyMappingTruncated` occurrences were duplicated as unknown
`BuildEnvironmentDiagnostic` workspace failures. Two genuine workspace-callback
occurrences remained. The legacy-prerequisite count was zero.

Commit `08ec7348` bounds environment projection to the closed admitted gap kinds,
preserves the original property-mapping gaps, and bumps the build-environment
extractor to `0.5.0`. Validation: focused build-environment tests **22/22** and
full .NET solution **1,738/1,738** passed; changed-file format verification,
private-path guard, and `git diff --check` passed. The full suite retains one
pre-existing nullable warning in `PropertyMappingTests.cs:560`.

Restricted validation completed on TraceMap head `a3de925b`: provenance and
extractor versions passed; unknown-origin and legacy-prerequisite counts were
zero; independent property-mapping gaps remained in their own rule family; and
exactly two sanitized `WorkspaceDiagnostic` callback occurrences remained. No
safe diagnostic ID was present. The next investigation is local-only native
callback classification, not another inference from shareable artifacts.

### COM-reference task-host follow-up

Local-only inspection classified both remaining callbacks as the same bounded
COM-reference task-host failure: kind `Failure`, no safe diagnostic ID, aggregate
occurrence count 2. A separate Visual Studio build of the selected solution
succeeded, so the evidence does not support a broken-solution claim.

The follow-up adds `MSBuildTaskHostIncompatible` classification and a bounded
workspace admission fallback for projects declaring `COMReference` or
`COMFileReference`. The fallback temporarily overrides only the two COM
reference resolution targets, keeps independent semantic extraction available,
and emits `ComReferenceResolutionSkipped` so COM-defined symbols remain an
explicit Tier 4 limitation. Project-defined `CustomAfterMicrosoftCommonTargets`
hooks are never replaced; those projects receive
`ComReferenceResolutionFallbackUnavailable` and retain normal workspace
behavior. Extractor versions advance to `build-environment/0.6.0` and
`csharp-semantic/0.20.0`.

### Post-COM coverage handoff (2026-09-03)

The field summary at `ad8fdd98` reports zero workspace/uncategorized diagnostics
and 932,070 Tier1 facts, with reduced coverage retained. The accuracy report's
workspace-repair priority was still triggered by static legacy markers. The
summary now admits only non-informational workspace-rule diagnostics for that
decision; generic unknown failures request classification and COM host failures
receive task-host-specific guidance. No scanner or evidence rule changed.

Added `claude-retained-coverage-triage.prompt.md` as the current handoff, linked
at the top of README. It selects and verifies a retained run, inspects at most
five samples in each of four gap kinds read-only, and returns only closed
aggregate categories. It forbids scan/rebuild, source changes and BRD work.

Validation: accuracy-summary tests (including fourteen priority cases), evidence-
summary tests, workspace-summary tests and review-launcher tests all passed.
Private-path guard and diff whitespace checks passed. The .NET suite was not
rerun for this PowerShell/documentation-only change; the preceding scanner fix
passed 1,742/1,742 .NET tests.

### Bounded post-triage extraction follow-up

Same diagnostic branch; no PR or merge. `legacy-webforms/0.7.0` changes only
markup type-name casing (namespace and project ownership unchanged), positive
postback branch candidates, and client/non-identifier event gap classification.
Exact tag matches do not override case-collision ambiguity. Negative-branch
identity/limitations and client-script negative-branch attribution are retained.
DLL metadata support, compound conditions, boolean comparisons and arbitrary
receiver inference remain out of scope. Synthetic non-compiling Framework 4.5
fixtures cover these boundaries; the restricted sample observations are not
treated as a population-wide guarantee of gap reduction.

Validation: focused extractor/coverage tests 59/59; full .NET suite 1,758/1,758;
legacy-codebase validation Python tests 13/13. A synthetic non-compiling Framework
4.5 CLI scan emitted 65 facts with reduced coverage; its persisted SQLite facts
include canonical control-type composition, both postback polarities and the
client-attribute gap. Changed-file formatting, private-path guard and diff checks
passed. The existing nullable warning in PropertyMappingTests remains unrelated.

### One-page trace handoff

Added `claude-single-page-trace.prompt.md` and made it the first README handoff.
This is documentation-only: actual execution needs the work computer's retained
index and private source. Selection is fixed before traversal to avoid selecting
only a successful example. Bounds are six hops, fifty symbols, one hundred
edges and ten terminal candidates. Handler joins use binding IDs; downstream
joins preserve canonical symbol/assembly identity and explicit support. Manual
source observations cannot fill missing extracted edges. No new scan or BRD.

### Per-handler traversal observation follow-up

The branch was rebased without conflicts onto `origin/dev` at `b025a6d3`; the
focused Web Forms packet baseline passed after that rebase. The non-truncated
43-page field report then showed 191 event chains: 41 handler-unavailable, 143
handler-resolved without a terminal, and 7 SQL-terminal chains. All 143 resolved
nonterminal chains lacked a retained legacy path, so the prior packet could not
distinguish a handler with no observed downstream edge from a traversed path that
stopped before a supported terminal.

The bounded path search now retains per-start observations during the existing
single traversal: reached-node count, traversed-edge count, downstream-edge count,
terminal-path count, and traversal truncation. The synthetic
`legacy-root-selection` edge is excluded from downstream counts. Web Forms event
chains expose only those counts, the public `legacy.flow.static-traversal.v1`
rule, a closed stop state, and explicit non-runtime limitations; no private
symbols, paths, SQL, or source content are added. The zero-argument triage script
uses the new field and safely falls back to `traversal-observation-unavailable`
for older packets. Synthetic tests cover a non-compiling Web Forms scan plus
separate no-edge, nonterminal-traversal, and SQL-terminal paths.

Validation after the rebase: focused packet and bounded-memory tests 28/28;
duplicate-start route-flow compatibility regression passed; full .NET solution
1,763/1,763; PowerShell parser clean. Changed-file formatting, private-path guard,
and diff checks are recorded after their final rerun.

### Handler-owned call-evidence diagnostic follow-up

Field evidence established 143 resolved nonterminal chains with zero observed
downstream edges. The packet now reports whether each exact handler flow
projection retained one or more supporting call-edge fact IDs. Closed states
separate retained-but-unjoined handler call evidence from no retained
handler-owned call evidence. The diagnostic does not synthesize edges or widen
identity matching. The alias-only triage script reports state, chain/page counts,
and aggregate handler-owned call-edge counts.

Validation: focused packet tests 15/15; full .NET solution 1,763/1,763;
changed-file format verification and PowerShell parser clean. Private-path and
diff checks pass.

### Exact handler-call support bridge

The field split found 137/143 resolved nonterminal chains with 3,032 exact
handler-owned call-edge references. The bounded reader now seeds canonical
handler display identities directly from the selected handler facts and admits
the projection's exact supported call facts. Only
compiler-resolved call targets seed further target-symbol closure. Graph
construction re-anchors a call only when one exact Web Forms handler is in the
projection support set, the projection source equals its canonical handler
identity, and the call fact ID occurs in both support lists. The bridge is a
`webforms-handler-call-support-projection` under
`legacy.flow.static-traversal.v1`, remains review-tier projection evidence, and
does not join unrelated same-named calls. Packet path matching now recognizes
both canonical handler display identity and symbol ID. Syntax-only call targets
remain isolated candidates and cannot create global simple-name joins.

Validation: focused Web Forms packet and bounded-memory tests 29/29; full .NET
solution 1,764/1,764; changed-file format verification clean. The synthetic
reduced-analysis index proves an exact supported canonical call can continue to
an SQL terminal while excluding an unsupported same-named call; a separate
fixture proves syntax-only support stops at one isolated projected candidate.
## Partial triage handoff

The work-machine report after de298671 matched 43 pages and retained 191 event
chains, 7 boundaries, and 82 TruncatedByLimit gaps. The packet drops the underlying
path-gap reason, so no specific limit or cycle cause is established. Triage now
accepts this retained JSON with partial status, retained-only count scope, and an
explicit no-absence warning. No traversal limits or scanner behavior changed.
PowerShell synthetic empty-chain packets tested with truncated true and false;
both produced the expected status and unavailable-reason output. Full .NET tests
not rerun for this script/documentation-only change.
## Closed truncation-reason reporting

Added optional gap.truncationReason for TruncatedByLimit, accepting only depth,
frontier, path, and cycle from the existing path gap. Null is omitted for other
gaps or unknown reasons. Both page-list reporting and retained triage print
reason counts; old packets and unexpected values become unavailable. No traversal
limits, cycle handling, or evidence classification changed. The field result shows
137 previously unjoined chains now observe downstream edges; among 143 unresolved
terminal chains, 130 are truncated, 7 have nonterminal traversal, and 6 have no
retained handler-owned call evidence. These are retained-report observations only.

The synthetic handler-to-terminal fixture verifies depth truncation survives JSON
publication (depth 3; depth 1 does not admit the same fixture graph). PowerShell
checks cover all four reasons plus absent/unknown values without leaking the
unexpected value. Formatting and private-path guard passed.
Validation: focused packet tests 16/16 and full .NET solution tests 1764/1764
passed, including the synthetic scan/index/packet integration test.
## Legacy frontier scheduling

Field report after 236a942d identifies 81 cycle gaps and one frontier gap. The
frontier is a global pending-path queue, not a depth limit. Legacy reports now
schedule branches depth-first in existing deterministic edge order to reduce
breadth-wide pending expansion. Non-legacy searches retain breadth-first order.
No global node deduplication, limits increase, or suppression of cycle gaps was
introduced. Cycles remain partial; path-limited legacy result subsets can change
because traversal is not shortest-first. Private graph performance is not proven
locally and requires regenerating the report from the retained index at work.

Synthetic regression preserves 64 reconvergent routes under frontier 24, checks
repeat determinism, checks true depth/frontier/path truncation, and preserves all
64 terminal routes when a cycle is present. Existing scan-to-packet integration
tests remain part of validation.
Validation: full solution 1765/1765 passed; focused packet/traversal 17/17
passed. The final added breadth-first comparison assertion also passed: ordinary
breadth-first hits frontier 24 on the same fixture. Formatting, private-path
guard, and diff check passed. Existing nullable warning in PropertyMappingTests
is unchanged.
## Bounded depth comparison handoff

Added Compare-FocusedWebFormsDepth.ps1, reusing the user's configured runner
without re-entering forms. Sequential depths 8/10/12 reuse the retained index,
with other caps unchanged and separate output directories. Summary counts exact
boundary kind/target/evidence tuples, page aliases, gains/losses and closed limit
reasons; source and page-selection mismatch fails comparison. It does not equate
boundary records with runtime operations or treat missing evidence as absence.
Field result after depth-first scheduling: frontier gap cleared, 931 boundary
records, 1031 chains, 59 resolved-handler chains without terminal, 349 cycle and
394 depth gaps. Work-machine depth comparison is pending.

Validation: synthetic PowerShell test covers duplicate tuple counting, gains and
losses, page counts, gap reasons, private identity non-disclosure, provenance
rejection and CLI depth forwarding. All changed runner scripts parse; private-path
guard and diff check passed. No .NET code changed; full suite not rerun (previous
code commit passed 1765/1765). Test entry: scripts/tests/Test-FocusedWebFormsDepth.ps1.
## Read-only completed depth summary

Follow-up: field failure at line 59 was the mandatory GetProperty call for
terminalKind. Production serialization omits null fields. The reader now treats
omitted or explicit-null handlerFactId/terminalKind as unavailable; other required
schema fields remain required. Regression covers omitted, null, and populated
optional fields. PowerShell regression, private-path guard and diff check passed.

The depth comparison consumed unacceptable resources on the work machine; depth 8
and 10 completed and remain local (66 and 84 MB), while no depth-12 completion is
established. New Summarize-CompletedWebFormsDepths.ps1 only reads those files.
It discovers a shared comparison folder, processes one bounded JSON document at
a time, keeps compact terminal identity sets, validates provenance/selection, and
prints counts plus gained/lost page aliases. No code path launches another script
or process; no outputs are changed. Hard input cap 128 MiB per report; boundary and
chain count caps 10,000, selection cap 1,000. This does not fix traversal resource
safety; deeper traversal must remain paused pending that separate change.

Validation: PowerShell synthetic fixture verifies auto-discovery, duplicate terminal
identity handling, gains, null handlers, unknown reasons, private identity omission
and mismatched provenance rejection. No .NET changes; full .NET suite not rerun.
## Resource safety and targeted baseline triage

Depth 8/10 read-only comparison confirmed 153/161 distinct terminal evidence tuples,
29 pages with terminals in both, 8 gained terminal tuples and none lost. Unavailable
handlers remain 41 chains; resolved without terminal remains 59. No further deeper
run is justified. Automatic comparison and wrapper depth >8 now fail closed.

Implemented Search work budget (100,000 state/edge operations per invocation),
typed work truncation, and preservation of terminal-free search gaps. Pending roots
are marked truncated. Wrapper directly supervises the built report DLL rather than
a dotnet-run parent: 300-second timeout, sampled 4-GiB working/private memory,
2-GiB managed-heap limit, kill owned tree on failure/cancel. These limits do not
prove whole-process-tree memory containment; build child memory is not aggregated.
No private 50-GiB workload reproduced here. Windows watchdog validation remains
pending; no user rerun requested. Only the read-only -Details handoff is recommended.

Synthetic work tests cover bounded terminal-free/reconvergent/cyclic paths and
deterministic exhaustion; process tests cover success, nonzero exit, timeout and
sampled memory termination. Read-only tests cover targeted alias handler counts.
Final validation: full .NET solution 1765/1765; focused packet/traversal 17/17;
all three PowerShell regression scripts passed. Formatting verification, PowerShell
parsing, private-path guard and diff check passed. Existing CS8602 warning at
PropertyMappingTests.cs:560 remains unchanged.

## Fair bounded handler scheduling

The post-rebase 43-page field run retained 262 event chains but only 96 downstream
boundaries. Its closed truncation evidence contained 64 cycle, 50 depth, and one
global work exhaustion; per-chain observations showed later handler roots inheriting
`work`. This establishes deterministic root starvation under the single 100,000-unit
depth-first queue, not absence of downstream behavior.

Legacy traversal now retains the same global work/frontier/depth/path ceilings but
rotates a root after the smaller of 64 work units or its equal ceiling share. A
partially expanded high-fan-out state resumes at its deterministic edge cursor.
Ordinary breadth-first paths are unchanged. `webforms-modernization` now exposes
`--max-traversal-work` (default 100000) and packet construction forwards it to the
shared traversal. Fairness does not guarantee completion, runtime reachability,
successful binding, or a terminal for every handler.

The synthetic regression places the noisy handler first, caps global work at 40,
and proves a later cheap handler still retains its SQL terminal while the noisy
root receives explicit `work` truncation. A repeated packet is byte-equivalent by
serialized model comparison.

Validation: focused Web Forms packet and combined traversal tests 68/68; full .NET
solution 1766/1766; scoped formatting, private-path guard, and diff check passed.
The existing nullable warning in PropertyMappingTests remains unchanged.

## Batch terminal-free evidence conclusions

The local batch inspection now separates exact allowlisted framework UI/control
endpoints from other unresolved leaves. JSON, private Markdown, and privacy-safe
console lines report the observed endpoint count, unresolved leaf count, and a
deterministic evidence conclusion. The conclusion remains
`no-supported-backend-terminal-observed`; it does not claim backend absence,
runtime behavior, or complete source coverage. Human review remains `unreviewed`.

Validation: focused raw-audit tests 14/14; full .NET solution 1787/1787;
scoped formatting and diff checks passed. The existing nullable warning in
`PropertyMappingTests.cs:560` remains unchanged.

## Local working-tree code-path review prototype

Added a one-case, local-only Markdown review over the batch inspection. It reads
bounded excerpts directly from a supplied source root without requiring Git,
renders retained call edges and evidence locations, and provides a human verdict
checklist. The report explicitly labels source mode as `working-tree` and does not
claim equality with the scan commit. A missing exact declaration may receive a
definition navigation candidate only when a syntax parse finds one unique method
name within already witnessed files; that candidate is not promoted to evidence.
Console output is counts-only and contains no private paths, symbols, or source.

Validation: code-path and raw-audit tests 16/16; full .NET solution 1789/1789;
Release diagnostic helper build, PowerShell parse, scoped formatting, and diff
checks passed. The existing nullable warning in `PropertyMappingTests.cs:560`
remains unchanged.

## Compiler-resolved DataAdapter Fill terminal projection

The fresh field scan retained the exact public framework Fill target plus receiver
identity, while the database census independently confirmed same-method command,
adapter, CommandType assignment, and Fill shapes. The bounded page reporter still
left that call as a nonterminal because `MethodInvoked` was treated only as compact
symbol metadata and the shared surface projection did not recognize framework Fill.

The shared surface projection now maps only Tier1 `csharp.semantic.methodinvocation.v1`
facts whose exact target is a supported public `DbDataAdapter`/`SqlDataAdapter.Fill`
signature and whose receiver symbol is retained to an `sql-query` surface subtype
`data-adapter-fill`. The compact single-index reader preserves properties for that
narrow fact shape. Same-name application methods, lower-tier facts, and missing
receiver evidence remain nonterminals. Tests cover positive/negative projection,
real framework extraction into the projection, and a complete Web Forms handler
path reaching the new SQL terminal. This remains static boundary evidence only;
command value flow, execution, success, returned rows, and branch feasibility are
not claimed.

Validation: full .NET solution 1785/1785; focused projection, real-framework
extraction, and handler-to-Fill terminal tests 3/3; modern sample CLI scan passed;
scoped formatting and diff checks passed. Repository-wide formatting remains
blocked by pre-existing whitespace findings outside this change. The existing
nullable warning in `PropertyMappingTests.cs:560` remains unchanged.

### Mermaid 11 report compatibility

The anonymous call-path graph now emits conservative Mermaid 11 flowchart
syntax: pipe-delimited edge labels are unquoted, node navigation uses the
explicit `click ... href` form, and the browser module is pinned to 11.17.2.
This removes permissive-parser and floating-version dependencies from field
reports that displayed Mermaid's syntax-error fallback only in the iframe. The
private iframe also loads the anonymous document at its root rather than during
fragment navigation, matching the field-confirmed standalone rendering path
while preserving the script-only sandbox. The report can be regenerated from
the retained local inspection; no repository rescan is required.

The first field rerun still rendered standalone but failed only inside Chrome
and Edge iframes. Mermaid navigation directives are therefore no longer emitted
as diagram grammar. The anonymous document renders a navigation-free graph with
strict security, then attaches alias-only node navigation after rendering; plain
HTML alias links remain as a no-script fallback. The remote module remains
confined to the anonymous script-only sandbox.

The second field rerun showed that browser-specific iframe rendering remained
unreliable even with navigation removed. The private report no longer embeds an
iframe or loads Mermaid at all. It renders the same alias-only nodes and retained
edges as deterministic inline SVG with evidence links. The separately shareable
HTML remains Mermaid-based because standalone rendering was field-confirmed.
Private graph rendering now has no CDN, script, sandbox, or browser-origin
dependency.

The private report trigger context is independently configurable from zero to
100 lines on each side of the exact retained binding span, defaulting to 12 and
capped at 256 displayed lines. This supports unusually tall Web Forms control
declarations without widening downstream evidence excerpts or changing the
underlying evidence span.

Added `New-FocusedWebFormsCodePathReviewSet.ps1` for the post-dogfood workflow.
It selects all retained batch-inspection cases by default, builds the diagnostic
helper once, and places each private/shareable report triplet in one timestamped
private subfolder. A root `review-queue.md` contains case/evidence IDs, relative
private and anonymous report paths, an allowlisted human-verdict column, and a
free-form comment column for local AI or workflow ingestion. Field review made
the entry-point convention explicit: the queue is named `index.md` in the root
of its timestamped report-set folder. Later runs use new folders and cannot
overwrite edited decisions. Review decisions remain metadata and do not become
scanner evidence automatically. A PowerShell regression covers multi-case
selection, index placement, relative paths, trigger context, queue fields, and
artifact counts.

The review-set folder also contains a private `index.html` for browser-first
navigation. It links every private and anonymous case report in a new tab and
links the editable `index.md`. Batch-generated private reports contain bounded,
validated `index.html` return links at both the top and bottom. Anonymous reports
do not link into the private review set. Tests cover link validation, return-link
placement, index generation, new-tab behavior, and the Markdown queue.

The first field run of the indexed set failed before case generation because the
helper could not reopen the selected inspection path. The batch launcher now
copies the already validated input to `inspection.snapshot.json` inside the new
private set and passes its explicit argument array to every helper invocation.
This also freezes the provenance input for the set. Single-case selection remains
an array, and regression mocks require the snapshot to exist at the helper
boundary. Validation included the two-case mocked PowerShell regression and a
real one-case launcher-to-helper run that produced both indexes, the private and
shareable reports, and the inspection snapshot.

The private HTML and Markdown indexes now group cases by retained event-binding
file path, falling back to the handler file and then `surfaceId` only when a more
useful path is unavailable. They report distinct item count separately from
handler-case count and show the private handler identity on each row. Evidence
case IDs do not wrap in the HTML table. This prevents seven handler cases across
three pages from reading as either seven pages or one unexplained batch.

Field acceptance requested contiguous case numbering inside those file groups.
Batch inspection now sorts candidate handlers by retained binding file path,
then handler identity and fact ID, before assigning local case IDs. The index no
longer has to display groups such as 001, 002, 005 followed by 003. A focused
regression deliberately interleaves handler fact ordering across file paths and
proves the emitted case IDs follow the file grouping.

The accepted workflow is packaged in `scripts/webforms-review/README.md`. Public
entry scripts remain at their already dogfooded root paths instead of being moved
behind compatibility wrappers. The guide separates the normal two-command batch
and review-set path from optional diagnostic tools, inventories every set artifact,
and repeats the private/shareable and static-evidence boundaries.

### Post-fairness field result and actionable summary

The work-machine rerun retained 466 event chains and 361 downstream boundaries.
The exclusive summary found 361 terminal-resolved chains across 25 of 43 pages:
275 SQL-query, 70 SQL-persistence, and 16 HTTP-client terminals. The remaining
chains are 41 handler-unavailable and 64 handler-resolved without a terminal.
Among the latter, retained observations separate 51 bounded-traversal truncations,
six chains with no observed downstream edge, and seven chains across three pages
with downstream evidence but no supported terminal. These are static retained
counts, not runtime reachability or absence claims.

Added `Summarize-FocusedWebFormsActionableGaps.ps1`, a zero-argument, read-only,
128-MiB-bounded handoff. It reports aliases and aggregate allowlisted rule, tier,
coverage, and linked-gap metadata for handler resolution, missing handler-owned
call evidence, and terminal-coverage review. It keeps traversal-truncated chains
separate and deferred. Synthetic validation covers all buckets, linked gaps,
priority ordering, and private identity non-disclosure.

## Terminal-free leaf and frontier diagnostics

Field actionable triage reduced the highest-value unknown set to seven chains on
three page aliases with joined downstream edges but no supported terminal. Existing
packet observations retained only counts and therefore could not identify which
closed evidence shapes exhausted or bounded those traversals.

Per-root traversal observations now retain deterministic, sorted sets of exhausted
leaf node kinds, leaf surface kinds, leaf rule IDs, bounded frontier node/surface
kinds and rule IDs, plus traversed downstream edge kinds and rule IDs. Each set is
capped at 32 distinct values with an explicit `diagnosticShapesTruncated` marker.
Synthetic root-selection edges are excluded from downstream shape metadata. The
packet and Markdown carry only these public aggregate classifications; identities,
names, paths, source text, SQL, and terminal targets are not added. The actionable
summary prints the new fields by existing alias-only bucket.

Validation: focused packet/traversal tests 68/68; full .NET solution 1766/1766;
PowerShell actionable-summary regression, scoped formatting, private-path guard,
and diff check passed. The pre-existing nullable warning in
`PropertyMappingTests.cs:560` remains unchanged.

The first field run with leaf shapes showed all seven terminal-coverage chains
exhausting both `Method` and `SymbolCandidate` leaves, with no frontier and no
surface kind. The candidate leaves are created only for exact handler-owned
syntax-tier call support; their target is intentionally hashed and isolated rather
than globally reconciled by a simple method name. This is not evidence of a missed
safe reconciliation candidate. Added leaf evidence-tier and closed reconciliation
states so the next packet distinguishes intentional syntax isolation from canonical
symbol exhaustion without retaining the private target identity. Matching behavior
remains fail closed.

## PR 723 head-review hardening

Head-review remediation preserves the field workflow while closing its remaining
boundedness and evidence-labeling gaps. Surface lists are streamed under byte,
row, and entry ceilings, comma-bearing list paths stay verbatim, and requests
that cannot be resolved after a truncated fact snapshot are labeled
`unavailable` rather than `unmatched`. Selected-symbol closure now applies its
frontier limit in SQL and while reading, and path work limits are validated even
when no root matches.

Private code-path reports require explicit `-IncludeRawSource` opt-in before
serializing excerpts. Physical link resolution prevents source-root escapes,
filesystem roots remain valid, and witnessed C# files are parsed once under a
bounded definition-candidate work limit. COM fallback discovery now includes
repository-contained literal imports and `Directory.Build.props/targets`, while
preserving imported custom-after-target settings. Database Fill audits require a
complete selected method symbol and an exact normalized Tier1 caller witness.

Validation: affected focused tests 112/112; full .NET solution 1799/1799;
PowerShell review-set regression and diff check passed. The pre-existing nullable
warning in `PropertyMappingTests.cs:560` remains unchanged.
