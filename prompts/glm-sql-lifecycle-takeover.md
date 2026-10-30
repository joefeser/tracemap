# GLM takeover: bounded compiled SQL lifecycle, whole-protocol implementation

IMPLEMENTATION_COMMIT: `9b841e50e9ec9e59c459678d2590842a2326ced6`

This file is both the current state and the implementation assignment. Read
AGENTS.md and .kiro/specs/sql-compiled-wrapper-evidence/implementation-state.md.
Establish state from Git/files. Preserve dirty work; do not reset/clean/archive
other checkouts, change ACK, merge, open a PR or use employer material in fixtures.
The user authorized implementation and push on codex/sql-compiled-wrapper-evidence.
No agent delegation or private application/SQL execution is authorized.

## Current locations and authority

- Public repository: joefeser/tracemap; branch codex/sql-compiled-wrapper-evidence.
- Active isolated clone: /tmp/tracemap-xml-catalog-20261030 (may resolve /private/tmp).
- Original /Users/josephfeser/.codex/worktrees/sqlserver-public-acceptance/tracemap
  was removed/archived during testing. Do not recreate it or guess its dirty state.
- Primary /Users/josephfeser/src/gh-joe/tracemap is on another branch and has
  untracked docs/reports/. Preserve it. The isolated clone's origin is that local
  repository; pushing there alone IS NOT a GitHub push. Verify GitHub origin and
  push only the task branch, with no force. Do not change another checkout's branch.
- Resolve exact current task-branch SHA live; this file's commit is docs-only after
  the implementation it describes. Do not trust a previous completion claim.

## Objective and observed state

Trace an ArrayList of inspected custom SQL parameter objects, compiled wrapper
construction/attachment of real SqlParameters, adapter Fill and returned DataSet,
while preserving per-allocation/per-execution identity and explicit unknowns.
Two private inner roots are selected on the work machine. Private inputs stay
there. Public synthetic fixtures and compiled metadata are the implementation lane.

The public guard bounds resource use. Broad source loading previously ran before
IL. --compiled-only excludes ALL source inventory/MSBuild/semantic/syntax work,
retains Git + explicit compiled admission, marks source
coverage reduced with SourceAnalysisExcludedByCompiledOnly and build NotRun.
An empty source digest does not bind the source tree. Do not omit --repo or add
framework DLLs to make a scan enter unrelated bodies.

Implemented: exact ArrayList operations and inspected custom fields/properties;
bounded enumeration/heap/depth/work; entry root selection/slicing; base constructor
ownership; opt-in opaque scalar initialization and inspected scalar transforms;
exact System.Web trace boundary, computed string/stack-name handling; inspected
InfoMessage delegate/subscription, private naming list, post-Fill naming and
allocation identity; signed framework-2/4 profiles; exact ordinal XML attribute /
InnerXml callback signatures; bounded ceq for concrete scalar/null/fresh/alias
operands. Unknown inputs and potentially aliasing views refuse, with exact site.

Newest change: exact signed SqlConnection/DbConnection get_State recognition,
ONLY inside InfoMessage opt-in, returns connection-state-unbound tied to its
connection identity. ConnectionStateReads receipts record external observation,
never an enum constant or transition. A state-based comparison/branch still
REFUSES. This is a diagnostic/typed-unknown fix, NOT closure of the runtime-state
blocker. Policy: IL 0.2.26, evidence v27, InfoMessage /4. No private candidate
has yet been proven. Do not set state=Open after Open and call that an observed
getter value. Microsoft documents State as a runtime connection-state observation:
https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.state

## Why the previous approach failed

Interpreter exits on the first refusal. We patched one visible failure, then used
another private scan to reveal the next. Happy-path public fixtures did not cover
the wrapper's entire control protocol. Callback-only inventory found its two XML
signatures but omitted surrounding lifecycle State/control behavior. Do not repeat
this. Do not create another one-getter/opcode fix plus private rescan cycle.

## Required next work (one bounded batch)

1. Obtain/reconcile the complete capability map using the companion
   prompts/inspect-sql-lifecycle-whole-protocol.md on existing work-machine evidence.
   Until that map exists, build public controls/tooling independently; do not guess
   the private method's behavior. Inventory is not proof of runtime reachability.
2. Write ONE representative independently authored C# and VB wrapper fixture that
   covers the whole map: constructors/config boundary, State/null/enum guards,
   Open/Close/finally, custom parameter objects, real parameter attachment, Fill,
   event callback/naming and returned allocation. Add negative controls up front.
3. Design state-dependent control soundly. Runtime getter reads need symbolic
   origins and constraints or bounded path alternatives, not assumed enum values.
   Treat separate reads independently unless stability is proven. If paths fork,
   bound path count, instruction/work budget and heap; preserve allocation identity
   before forks and execution identity after forks. Do not silently select a path,
   merge incompatible parameter attachments, erase exception paths, or relabel
   partial branches as unconditional entry evidence. Keep unsupported cases explicit.
   Any new conditional boundary/schema/policy must be intentional and documented.
4. Inventory and cover adjacent missing opcode/signature families as indicated by
   that same map, including base/override owner encodings and exact signing/version.
   Retain all current bounds/refusals. No general SQL parser, LINQ interpreter,
   dynamic DataTable.Select, live SQL, deployment or private acceptance claim.
5. Run focused tests immediately, fix concrete failures, then required shared-core
   full Debug -warnaserror on an immutable test copy/head. Do not edit, rebuild,
   commit, delete or archive that copy while its suite runs. Do not start duplicate
   full runners. Record failures/cancellations separately. Update spec tasks/state.
6. Push implementation, tests, docs and one pinned guarded validation prompt after
   the public gate. Keep --compiled-only and existing four boundary flags; use
   Windows guard self-test and 60s/2048MiB job limits. No automatic private retry.

## Relevant files

src/dotnet/TraceMap.Core/IlSqlLifecycleExtractor.cs: Value/heap, ceq, branches,
Invoke/Api, actual opcodeFailure invocation path and entry identity.
IlSqlLifecycleApis.cs / IlSqlObjectPathExtractor.cs: exact default catalogs;
legacy profile lookup is confined to InfoMessage opt-in.
IlSqlInfoMessageBoundary.cs: exact catalogs/profiles, callback/naming inspection,
external state reads, profile/identity/escape limits. No callback execution.
IlSqlLifecycleSlicing.cs / IlBodyEvidenceExtractor.cs: selected compiled closure,
dual-reader retention, global budgets, materialization and versions.
src/dotnet/tests/TraceMap.Tests/IlSqlInfoMessageTests.cs and
IlSqlLifecycleExtractorTests.cs: normal/retargeted C#/VB bodies and native CLI.
samples/sql-compiled-wrapper-evidence/{csharp,vb}/end-to-end/InfoMessage.*:
public synthetic only. FRAMEWORK-PROFILES.md explains temporary metadata retargeting.
scripts/Invoke-BoundedScan.ps1 and Test-BoundedScanGuard.ps1: Windows job cap and
sampled working-set guard, child exit zero is not completion proof.

## Validation evidence and limits

- Clean XML head ede44baf201e717bf25b4e65c3a5c0f37c512ae4 full Debug: 5668 passed,
  3 existing skips, 0 failed, 43.8867 minutes. /tmp/tracemap-xml-full-debug.log.
- Equality head ed4c603a2fdff96d119597407a1d65e8568bfe9b affected: 211 passed.
  /tmp/tracemap-ceq-regression.log. Its full run was NOT green; one recorded failure
  is WebFormsReviewExecutionTestsRelocation.Cancelled_relocation_does_not_create_a_completed_copy_or_delete_source_proof.
  It was canceled when work continued in the same test copy. Do not claim it passed.
- Latest state-recognition affected suite: 215 passed, 0 failed/skipped, 28 seconds,
  Debug -warnaserror. /tmp/tracemap-state-regression.log; no private entry claim.
  Full Debug for this newest change remains a required takeover gate, not running.
- The ongoing ECMA-335 effort can supply generic operand/stack/CFG correctness;
  it does not establish framework call semantics or remove these policy gates.
  Keep encoded evidence and conditional lifecycle interpretation distinct.
- First state-control test iteration: 35 passed, 2 failed because the negative
  mutation assumed SqlConnection as C# dispatch owner; corrected to mutate the
  actually observed signed getter identity (base/override dispatch differs).
- Some prior worktree-based broad runs failed after the checkout disappeared;
  those do not count as passes. Native dotnet SIGSEGV retry is separately recorded.

Deliver a concrete capability map, tested whole-protocol fixture/implementation,
remaining gaps and exact pushed head. If blocked by missing private metadata,
state precisely what is missing in ONE bounded request; do not ask for source,
email or a sequence of per-opcode photos. No claim that the whole SQL effort is done.
