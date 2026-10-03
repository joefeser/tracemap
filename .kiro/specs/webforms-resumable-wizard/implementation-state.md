# Implementation state

## Completion audit — 2026-10-01

Implementation #799 is owner-merged into dev as `98ce227f`; its complete tree is
identical to tested head `2f6975f5`. Final CI: .NET 3,138 passed/one platform skip,
zero build warnings; Windows corpus 140 passed/zero skips, both ASP.NET publish
cases and three report checks. All distribution/mutation/adapter checks passed.
Both final budget findings were settled; ACK's fallback-attempt ceiling was not
merge approval. The owner subsequently merged the PR.

`acceptance-audit.md` maps all nine requirements to inspected implementation and
test assertions, records limits and the unresolved historical Windows failure,
and separates v1 completion from recommended customer/operator follow-up.
Closeout branch: `codex/webforms-wizard-acceptance-audit`, docs only, based on the
merged dev revision. Earlier entries below are historical and superseded where
they say final-head validation/delivery is pending.

Branch: codex/webforms-resumable-wizard, based on #798 head
4f231dc31e075dc3ad35ee84808893242a7c68ca. The dependency branch is unchanged.
Owner has requested Codex review on that head; do not duplicate the request.

Existing native configuration/preflight/preparation/start APIs live in
TraceMap.Cli. The wizard must adapt to them, not bypass receipts, source-commit
attestation or output ownership. UI-independent selection validation starts in
TraceMap.Core; terminal orchestration and native configuration conversion belong
outside that core helper. No customer code is executed during discovery.

First foundation implemented: WebFormsWizardForms in TraceMap.Core, with
deterministic .aspx discovery, web-root-relative slash normalization, absolute
in-root selection, comments, duplicate/escape/missing/empty errors, link refusal
inside the selected root, and explicit inventory/text limits. System aliases
above the chosen root (e.g. macOS /var) do not invalidate its child selection.
Template generation is bounded and rejects filenames that the line format
cannot faithfully represent. This helper writes nothing and does not establish
source/publication provenance or eliminate filesystem races before execution.

Validation: 10 focused tests passed, no skips, with retained TRX at
`/tmp/tracemap-wizard-forms-foundation`. Diff and private-path guards passed.
The dependency branch remains at 4f231dc3. No PR update or duplicate review
request was made.

State foundation: WebFormsWizardStore owns a new configuration root, uses an
exclusive file lease, and stores versioned local-only root/project envelopes
with the Core assembly generator hash and bounded configuration hash. Resume
checks envelope structure and project-byte references; changed project files
stop that project without preventing another valid project's inspection.
Strict loading rejects duplicate/unknown properties and bounded-size violations.
Physical path overlap checks keep configuration outside declared inputs.
Returned root snapshots do not expose mutable internal references.

Persistence is atomic per file, not a multi-file transaction. A crash between
project and root replacement fails closed with PROJECT_CHANGED. Explicit repair
is not implemented yet. Hashes detect changes, not authorship; these envelopes
are not source/build/publication receipts. Saved steps are only cursors and do
not establish that any source/build or publication is valid. Concurrent external
filesystem replacement races are not eliminated by the advisory lease.

Validation: 24 focused wizard tests passed, zero skips, retained TRX under
`/tmp/tracemap-wizard-state-foundation`. Full suite and CLI replay remain pending.
Next: target classification, terminal coordinator/subset pause, repair and
native preflight/execution integration. No wizard PR has been opened or pushed.

Target/selection increment: WebFormsWizardTarget performs bounded static XML and
solution-entry inspection without MSBuild evaluation. Folder inputs containing
an immediate C#/VB project require that explicit project; solution inputs require
a selected local web root matching exactly one entry. Web roots require web.config
and discoverable forms. URL/foreign-drive/outside-solution entries are not admitted
by this v1 classifier. Project references are not independently registered.
Tool families are recommendations only; SDK modern targets are not claimed to
support Web Forms, and conditional/imported properties remain unevaluated.
Actual executable/toolchain checks and build-result validation remain pending.

WebFormsWizardSelection creates a deterministic editable forms.txt for missing or
blank selected-mode input, pauses without advancing the cursor, then validates
the edited list and advances to build. All-mode advances without an edit file.
Comments-only input fails rather than becoming all. Resuming later steps checks
the selected files again. This shared transition has no terminal adapter yet.
Selection template replacement is per-file atomic, not a transaction with state;
filesystem replacement races by non-cooperating processes remain a limitation.

Target tests caught and fixed a framework classifier bug that initially mistook
net10.0 for legacy net10. Focused wizard validation now covers 35 cases; final
TRX is retained under /tmp/tracemap-wizard-selection-foundation. Full suite, CLI
integration, Windows replay, repair, publication and execution remain pending.

Terminal setup adapter: `webforms-review wizard [--root <folder>] [--continue]
[--add-project]` now routes through WebFormsWizardCommand. TextReader injection
allows complete local prompt replay without changing Console.In. An existing
folder requires an explicit continue/new choice; duplicate flags, implicit add,
unknown projects and malformed config fail without resetting prior state.
Multiple saved projects require explicit selection; only --add-project creates
another roster entry. Subset continuation consumes saved answers and doesn't
repeat ID/path/mode prompts. EOF is a typed error; any already-saved project is
retained. Setup currently pauses at build with exit 2, explicitly stating that
no build/scan/application was executed. It is not a completed wizard yet.

Validation: 40 focused wizard cases passed with zero skips, retained TRX under
/tmp/tracemap-wizard-terminal-foundation. Five terminal tests exercise prompt
replay, restart, add-project preservation, existing-root/EOF behavior, invalid
flags and CLI help dispatch. Next implementation must advance the build cursor:
explicit toolchain/consent or guided manual projectless publication, followed by
publication inventory/native config, repair and immutable report execution.

Build increment: shared WebFormsWizardBuild creates a bounded executable/target
hash preview, requires explicit consent before even probing the executable,
validates a successful numeric version result, then executes the exact argument
list. The terminal requires an absolute trusted tool path and the word `build`,
shows both commands and working directory, and warns about arbitrary MSBuild
tasks/restoration/source writes. A Windows MSBuild requirement cannot be bypassed
on non-Windows. The process adapter uses no shell, bounds stdout/stderr to 64 KiB
each, uses a 30-minute timeout, and kills the process tree on cancellation/failure.
Private command output is displayed to the operator, not published as a report.

Only successful probe/build plus unchanged tool/target hashes advance to
publication. Failed/declined builds leave the build cursor; a local-only nested
build evidence record retains hashes inside the provenance-wrapped project
config. Resume rechecks those retained tool/target hashes. These are command
observations, NOT complete source/dependency snapshots or binary/source binding.
Existing native publication/provenance admission remains mandatory later.
Projectless `ready` declares external compilation and advances without inventing
build evidence; EOF/later safely pauses. Publication validation is next.

Focused tests: 49 passed, no skips, at /tmp/tracemap-wizard-build-foundation,
including actual installed dotnet --version execution, injected build success/
probe failure/build failure/declined consent/input mutation, terminal consent
ordering and manual-publication distinction. No customer project was built.
Full fixture builds, publication integration, explicit repair and report execution
remain outstanding. No wizard PR or push yet.

Publication increment: WebFormsWizardPublication validates site-root/bin shape,
web.config XML, and projectless PrecompiledApp.config. PE metadata is inspected
without loading/executing assemblies. Primary assemblies must be in publication
bin; explicit external managed dependencies are accepted. Duplicate selections
fail. This is structural evidence, not native build/source-binding admission.

Local input snapshots retain selected DLLs, publication metadata/maps and bounded
website source/config files, excluding bin/obj/.git. Resume detects content and
relevant inventory changes. Limits are 64 MiB/file, 2 GiB selected hash input,
10,000 source files, 100,000 traversed source entries, plus the existing 1 MiB
configuration envelope limit. Source membership uses explicit relevant extensions,
not every arbitrary file and not imported projects outside the website root.

Terminal publication selection checkpoints before dependencies. Completing the
dependency prompt advances to configuration, not ready/completed. Native config
is next: external DLLs need owned staging because native preflight requires
publication-root-relative inputs. Do not weaken native containment or write into
customer publication folders. Preserve hashes and native receipts/attestation
requirements. Filesystem races remain outside build-authenticity claims.

Validation: 59 focused cases passed, zero skips, retained TRX under
/tmp/tracemap-wizard-publication-foundation. Cases cover root-vs-bin, missing
compilation marker, external dependencies, duplicates, changed DLL/source/config,
new source/map detection and terminal dependency pause/resume. Fixtures copy local
test assemblies; no private customer binaries were used. Full suite/Windows
acceptance, native adapter, explicit repair and execution remain outstanding.

Native adapter increment: WebFormsWizardNative stages selected publication files
under a unique project-owned publication folder, streaming and verifying original
hashes. External dependencies get hash-scoped paths under dependencies, preserving
native input containment. It creates native.config.json without overwriting an
existing file, validates native preflight, then records its hash and staged-input
hashes in project state. Resume rechecks original inputs, staged inputs, native
config bytes and current Git commit. Failed/interrupted attempts remain for
explicit repair; no implicit cleanup or reset is performed.

Native configuration gains an optional WizardProvenance field (omitted when null)
with exact CLI generator and bounded config/input hashes. PreparationProvenance
is not repurposed; binding receipts stay empty until separate explicit attestation.
The wizard requires a source commit and remote but does not invent a commit or
attest on the operator's behalf. Existing preparation still validates committed
source membership and assembly/source-binding gates before execution.

Solution input builds still use the selected solution target, but generated scan
configuration explicitly selects only the website project with web-root-relative
source paths. Other solution projects are not silently registered. This keeps
published page-map paths relative to the actual web root. The original solution
target remains part of the wizard snapshot even if outside that web root.

Terminal `prepare` at configuration creates this native config; EOF/later pauses.
The ready cursor means configuration/preflight ready, not completed analysis.
No source attestation, native preparation or scan/report execution is wired yet.
Expanded wizard plus existing preflight/preparation regression: 168 passed,
zero failures/skips, no build warnings in the final run; retained TRX at
/tmp/tracemap-wizard-native-foundation. Source/publication fixtures stayed
unchanged; external DLL staging and modified native config/staged-input rejection
are covered. Full suite, Windows acceptance, explicit repair and end-to-end
wizard execution remain pending.

Execution increment: WebFormsWizardExecution calls native start only after an
exact source-commit attestation and preserves a unique runs/<project>-<guid>
reference before dispatch. Native start owns preparation/preflight/scan/reports;
the wizard never launches the website. Nonzero return and interruption retain
the attempt as failed. Resume requires the pinned native run manifest; an attempt
that failed before that manifest requires explicit repair, not silent restart.
Completed continue performs read-only native status verification, not a new scan.

Completion requires native status reports-completed-review-only, verified retained
artifacts, the attested commit and a workbench locator, followed by fresh wizard
input checks. A zero process return alone cannot mark complete. The running cursor
is explicitly not a live-process assertion. Native run locks remain authoritative.
The terminal separately prompts for full commit attestation or pinned resume.

Validation: all 68 wizard tests passed with zero skips, including the negative
status case, under /tmp/tracemap-wizard-execution-foundation. The real local
fixture test ran native preparation, scan and reports, then verified completed
continue preserved project bytes and one run folder. Existing public compiled
sample DLLs were used; no customer application was executed. Explicit repairs,
full terminal fixture replay, broader regression/Windows checks and PR remain.

Repair increment: --continue --repair-project <id> previews a registered-project
setup restart, requires explicit replacement input/mode and ID confirmation, and
checks the observed project hash has not changed before applying it. Corrupt or
missing project files can be replaced without parsing/trusting their contents;
root metadata must still validate. Declined/stale previews do not modify state.
Repair archives original project bytes and form selection under project-history
with a provenance-wrapped repair record, clears only that project's build/native/
attestation cursor, and preserves all prior native configs, staged files and run
paths IN PLACE. Moving those files would invalidate prior native manifests.
Subsequent native generation therefore uses a versioned native-<id>.config.json
while preserving the original native.config.json. Other project bytes are pinned
by regression tests. Repair is per-file atomic, not a transaction across files.

73 focused wizard cases passed before the final added check that actual completed
native reports remain verifiable after repair. Full .NET test project is running
in process session 69611 with TRX directory /tmp/tracemap-wizard-full-suite;
do not restart it while live. A final focused rerun is still needed after the
latest test assertion. Native workflow documentation and rule catalog now describe
commands, private artifacts, pause/error codes, repair boundaries and limitations.

Latest validation: the broad .NET run completed with 3,096 passed, one skipped,
zero failed (TRX /tmp/tracemap-wizard-full-suite). This run preceded the final
fresh-root ordering fix and acceptance additions. The subsequent focused run
passed all 76 wizard cases with zero skips (TRX /tmp/tracemap-wizard-latest).
Fresh terminal setup now validates configuration/source overlap before creating
the configuration root. Added real consented SDK fixture compilation, complete
subset/restart/two-project terminal replay, and retained-report verification after
repair. These are public local static fixtures, not customer runtime acceptance.

The existing scripts/wlocal.ps1 corpus now includes every wizard test and requires
the three principal real-process/end-to-end cases in its admitted test receipt.
Its bounded source roster includes wizard implementation and test files. Replay
is underway at /tmp/tracemap-wizard-local-replay-20261001. Dependency PR #798 is
still open and advanced from 4f231dc3 to 1653f0de (retained report recovery budget
fix); fetched and inspected, integration into this branch is still pending.

One-command replay completed: 108 passed, one Windows-only skip; all three
PowerShell chain/SQL-ledger/cap checks passed. The wrapper guard tests also passed.
Receipt: /tmp/tracemap-wizard-local-replay-20261001/validation.local.json.
Windows ASP.NET publication remains explicitly unverified here; use
scripts/wlocal.ps1 -RequireWindowsPublish on the authorized Windows machine.

Integrated dependency 1653f0de into this branch through merge 39878a09; #798's
branch was not modified. Integrated wizard/native execution tests: 201 passed,
zero failed/skipped, TRX /tmp/tracemap-wizard-integrated. Current local source
has no compiler warnings in this validation. Scoped stacked PR and full final-head
regression remain pending. No customer site or SQL was executed.

PR #799 is open against dev at 67ac3f81. #798 merged as 83f09c9f during PR
creation, so a stacked base is no longer needed. Final full local suite remains
live in session 92830; ACK review process remains live in session 20257.

Windows corpus run 36901217294 exposed four subset/restart failures: MoveFileEx
cannot replace forms.txt while the prior write-capable destination handle is open.
All failures trace to the same template-generation path. The local repair uses a
no-overwrite move for missing selections and File.Replace with read/delete sharing
for an existing blank selection, while denying in-place writers and rechecking
blank content before replacement. A new regression keeps a delete-sharing reader
open and proves the original handle retains its old contents with no staging file
left behind. Nonblank selections remain untouched. Windows revalidation is still
required, not inferred from macOS.

Separate artifact outputs avoided altering the live full-suite runtime. Focused
selection tests: 4 passed. All wizard tests with the repair: 77 passed, zero skips,
TRX /tmp/tracemap-wizard-windows-fix-all. This repair has not yet been pushed while
the required review batch settles. The prior Windows run is failed, not admitted.

Full-suite session 92830 completed for pre-repair head 67ac3f81: 3,107 passed,
one Windows-only skip, zero failures, TRX /tmp/tracemap-wizard-final-full.
Do not attribute that full result to the later Windows repair. The repair has
77 passing wizard tests with isolated artifacts. ACK status with the unchanged
repo batching policy and explicit local validation evidence now returns
LOCAL_FIX_BATCH_READY_TO_PUSH. Qodo returned findings, but the normal gate still
reports CHECKS_PENDING (Windows package smoke pending; public-corpus failed),
not patch-review authority. Publish the validated CI repair, then rerun the gate;
review findings still require their authorized brief and disposition.

Repair published at 573d7ab0. Windows public-corpus run 36903234553 completed
successfully on that exact head: 111 passed, zero skipped/failed, including both
ASP.NET publication cases. Chain comparison, SQL ledger and cap shortcut checks
also passed. This supersedes the failed pre-repair Windows result, but establishes
only public synthetic static acceptance, not private/customer runtime coverage.
Local repaired wlocal replay: 109 passed, one platform skip; all three report
script checks passed, receipt /tmp/tracemap-wizard-repaired-replay-20261001.
The built CLI wizard --help entrypoint also exited zero with the expected options.
Full final-head CI/review disposition and final completion audit remain pending.

Final selection audit found that a UTF-8 BOM-only blank file was not recognized
as blank before parsing. Both initial classification and the under-lease recheck
now ignore a leading BOM consistently with the existing forms parser. Empty,
whitespace, BOM-only and BOM-plus-whitespace cases are pinned by a theory;
comments-only input remains an error and is preserved. All 80 wizard tests passed
without compiler warnings in /tmp/tracemap-wizard-bom-blank-clean. This follow-up
is local pending the next review-authorized push batch, not part of the 573d7ab0
Windows evidence.

CI .NET adapter run 36903234570 on published 573d7ab0 passed the full suite:
3,108 passed, one platform skip, zero failures. ACK's original process completed
with WAIT_TIMEOUT_CHECKS, with no failures and only Windows package-smoke still
running. Direct job state confirms forward progress through metadata/PDB/IL tests
and native Windows PDB fixtures; it is currently validating Web Forms publish
proof. Continue monitoring run 36903234523, then resume the review gate after its
terminal result. Do not treat the bounded ACK wait timeout as a stopped CI job.

Windows package-smoke completed successfully; all checks on 573d7ab0 are now
green. Resumed the normal ACK gate in session 29473 after the prior process's
terminal timeout. Do not restart the previous completed session 20257.

Selection-bound audit: generated templates previously checked characters while
resume bounded UTF-8 bytes. Template and parser now share the same character/byte
validation; resume names the byte limit explicitly. Regression covers a valid
Unicode page and an oversized multibyte comment below the character ceiling.
All 81 wizard tests passed without warnings in /tmp/tracemap-wizard-selection-bounds.
This and the BOM correction remain local for the next consolidated push.

## Settled review batch, 2026-10-01

ACK session 29473 completed with PATCH_AUTHORIZED on 573d7ab0: six threads,
five Qodo and one current-head Codex. No duplicate reviewer request was posted.
The findings are mixed, not one shared defect. The repair audit covers selected
build scope/input identity, bounded process output, source startup inventory,
duplicate dependency byte identity, and persisted completion versus output I/O.

- Include Global.asax in the bounded source roster; edit/add/delete regressions.
- Build the selected website project rather than the entire solution, retaining
  and rechecking both solution and selected-project hashes; project-defined
  references/tasks may still build dependencies. Use minimal build verbosity.
- Drain/hash complete decoded output streams with bounded 65,536-character tails;
  verbose output no longer kills the build. Real long-output success/failure
  commands pin exit-code handling. Hash encoding is UTF-16LE code units.
- Deduplicate byte-identical dependencies for native admission while retaining
  every original selection for source-path/hash revalidation.
- Update the local completed cursor only after its successful persisted save,
  so later output failures cannot overwrite it with failed.
- The page-map finding is non-actionable for the wizard path: its attested start
  calls native preparation, writes publication receipts and uses prepared config
  before scanning. Real fresh execution now asserts bound publish provenance,
  one consumed page and a publish-receipt input. No admission bypass was added.

Initial repair runs exposed test-fixture errors (missing solution header, config
overlap, macOS canonical path comparison), corrected without weakening guards.
The first full replay passed 122 tests with one platform skip, then its wrapper
correctly rejected a renamed acceptance-test identity. The original named Fact
is restored and output-failure coverage is a separate Fact sharing the helper.
The complete wrapper replay is being rerun; final-head CI/review remain pending.

Consolidated replay completed successfully: 122 passed, one Windows-only skip,
zero failures; all three PowerShell chain/SQL/cap report checks passed. Receipt:
/tmp/tracemap-wizard-consolidated-final-20261001/validation.local.json.
Private-path guard and diff whitespace checks passed; no compiler warnings were
emitted. This is local public synthetic evidence, not Windows final-head proof.

Acceptance audit: contracts 1-7 have shared-service and terminal/native tests;
contract 8 has the fresh/restart/repair/two-project and real native replay plus
bounded-build regressions; contract 9 has workflow/rule/backlog documentation and
PR799 against dev after dependency merge. Remaining delivery gates are the full
suite, Windows final-head corpus and review dispositions/readiness on the repaired
head. Do not mark those gates complete using previous-head CI.

Second settled batch: ACK session 14046 terminated PATCH_AUTHORIZED on fdc27ecd
with two current-head Codex findings (4159126233 and 4159126240). Windows corpus
run36907795758 passed on that head: 124 passed, zero skips/failures, both authentic
publication cases and all three report scripts. Local full-suite session53652
remains live; its runtime has not been rebuilt under the running tests.

Repairs are independent: subset parsing now calls the bounded inventory validator
so an unselected case-colliding sibling cannot evade publication identity checks;
version and build process observations use the same full-stream hash composition.
Regression matrix includes file and directory case collisions and independent
discarded stdout/stderr version-prefix differences. Case-insensitive hosts assert
their representable single-file behavior; Linux CI must exercise actual collision
rejection. Separate artifact-path validation passed 27 focused forms/build tests
without compiler warnings at /tmp/tracemap-wizard-review2-results/review2.trx.
Full updated replay, push, individual settlement and final-head gates remain open.

The separate artifacts layout is valid for focused wizard tests but not the legacy
corpus: those tests derive build configuration from the runtime parent directory
and looked for bin/TraceMap.Tests instead of bin/Debug. That attempted replay is
not acceptance evidence (95 passed, 31 failed, one platform skip). All three
independent PowerShell report checks passed. Do not weaken fixture checks to admit
the alternate layout.

Local pre-fix full-suite session53652 was explicitly canceled as superseded, with
both owned process IDs verified exited, to release the standard runtime for the
updated replay. It has no passing local full-suite claim. Separately, authoritative
CI run36907795847 on fdc27ecd completed successfully: 3,121 passed, one platform
skip, zero failures, zero build warnings. These results do not cover the two new
local fixes. Standard wlocal replay is running in session99124 with output under
/tmp/tracemap-wizard-review2-standard-20261001.

Standard replay succeeded: 126 passed, one Windows-only skip, zero failures, all
three report-script checks passed. Receipt:
/tmp/tracemap-wizard-review2-standard-20261001/validation.local.json. This supersedes
the invalid alternate-layout attempt for local corpus validation. The repaired
head still requires Linux case-sensitive collision and Windows/full CI checks.

## Windows failure diagnostic follow-up

Owner explicitly authorized Windows repair, failure diagnostics and continuation
of the review loop. Head83b39433 Windows run36909987115 failed one real wizard
pipeline test (127 passed); its temporary run was deleted by test cleanup. The
generic execution error did not identify the underlying exception. No root cause
or fix is yet claimed. Add closed phase/category/HRESULT console diagnostics,
with no raw exception messages, paths or SQL, and retain public synthetic wizard
runs under the existing corpus artifact root. Seven focused execution/privacy
tests passed locally. A diagnostic Windows replay is required before choosing a
root-cause repair; failed checks and three review threads remain open.

The first diagnostic local corpus attempt passed 126 tests (one platform skip)
and all three PowerShell checks, but its final input-stability receipt correctly
rejected an overlapping test-retention edit. It is not admitted replay evidence.
The final retention design copies failed synthetic fixtures after execution,
preserving original temp-path conditions; copied manifests are diagnostic only,
not relocated resume authority. Rerun against stable source before publication.

Stable replay completed successfully at
/tmp/tracemap-wizard-diagnostics-final-20261001/validation.local.json:
126 passed, one Windows-only skip, all three report checks passed. Final focused
execution/privacy tests: seven passed. Previous-head Linux CI run36909987091
also passed 3,125 tests with one platform skip and zero warnings; this does not
certify the diagnostic patch. Windows diagnosis remains pending.

## Third settled review batch

ACK run1790881834560 authorizes three thread dispositions on diagnostic head018f56ad.
The findings are independent: native page-map accounting, native generator
identity, and truncated project repair. Exact reviewed-head inspection shows
`IsPublishInventoryRole` already includes `page-map`; no budget expansion is
needed. Added 127-map and 1,023-map real wizard/preflight regressions, also checking
legacy unsplit budgets still reject excess inputs. The initial large-case test
expected the count gate, but the stricter legacy list-size gate correctly rejects
first; that expected diagnostic was corrected without weakening production bounds.

Native generator identity now commits to both implementing CLI and Core hashes;
tests vary each implementation independently and verify path-independent identity.
Repair-only bounded reads accept empty originals for preview/archive, while
ordinary resume remains fail-closed. Regression matrix covers empty/nonempty
corruption, declined confirmation, changed preview, isolated preservation and
the terminal repair/resume flow. Validation and settlement pending.

Focused third-batch validation passed 17 tests; stable wlocal replay passed 133
tests, one Windows-only skip, and all three report-script checks. Receipt:
/tmp/tracemap-wizard-review3-20261001/validation.local.json. The named real native
pipeline acceptance test now runs three independent fresh attempts, failing on
the first failure (no retry-to-green). Diagnostic head018f56ad Windows run36912333153
passed 128 tests, both authentic publication cases, and all report checks. Thus
the prior Windows failure is intermittent and remains unexplained; no root-cause
fix is claimed. New diagnostics and postmortem copies will support the next
reproduction. Final-head Windows/full validation and finding settlement remain.

## Fourth review batch: admission and downstream count budgets

ACK run1790885828742 released two exact-eb290ea6 P2 findings. Shared invariant:
wizard-admitted component counts must fit retained validation and native follow-on
input counts. Native hard cap remains 256; wizard selects that explicit budget
and rejects combined primary/dependency counts above 252 before persistence,
reserving configuration, selected project and two generated receipts. Retained
count derives from existing component maxima (11,155), including an external
solution input. Independent byte/config serialization/work bounds are unchanged.

Regressions cover 128 primaries, 252 combined distinct-hash assemblies with
receipt/project headroom, over-limit rejection without state advance, exact
retained-count boundary and one-over rejection. Initial focused matrix passed
26 tests. Expanded stable corpus replay passed 138 tests, one Windows-only skip
and all three report-script checks; receipt:
`/tmp/tracemap-wizard-budget4-final-20261001/validation.local.json`.
Project-backed and projectless maximum assembly selections both pass native
preflight/resume; receipt headroom is asserted, not a full 252-assembly scan claim.
The first expanded run exposed a noncanonical external-solution path in the test
fixture; corrected the fixture without weakening inventory identity checks.
Prior eb290ea6 CI passed Windows 135 and full .NET 3,133 with one platform skip;
those results do not validate this new patch.

## Main promotion PR #801: settled head findings (2026-10-02)

Repair branch: `codex/promote-dev-to-main-f0aaf582f778`, based on reviewed head
`f0aaf582f7787d62e8f3dc655c53363c9c05b0c2`. ACK run1790987754602
authorized the six settled findings. This repair does not update dev or merge
main; reconcile its fixing commit into dev separately after promotion review.

Single-receipt publish admission now shares a host path comparer for duplicate
checks and artifact lookup, and parses only captured bounded hash-verified map
bytes. Extractor version advances to 0.1.2. Focused pipeline page-list writes
reject linked targets/ancestors and replace the leaf atomically (including a
pre-existing hardlink); the root must remain operator-controlled against
concurrent ancestor replacement. A trailing IL opcode prefix emits the typed
malformed-body gap. Wizard discovery excludes build/control directory names
case-insensitively. Independent VB restore retains the explicit request even
when C# projects are present; shared solution restore may run redundantly.

Validation: final isolated full .NET suite passed 3,150 tests, with one
Windows-only skip and zero failures; TRX is
`/tmp/tracemap-pr801-final-tests/pr801-final.trx`. Focused follow-up passed 80
tests. Both focused pipeline PowerShell contract and output-safety checks passed.
The final build had zero warnings/errors. The VB sample scan produced 222 facts
with semantic coverage. Pinned Community.VisualBasic smoke at
`20d2a51dfc9f342848ad134952ceaa8d79302559` produced the expected 110,726 facts
with reduced semantic coverage and FailedOrPartial build status; this is not
a clean customer build claim. Smoke artifacts are under
`/tmp/tracemap-pr801-oss-smoke/community-visual-basic`.

An earlier full-suite attempt overlapped a smoke rebuild: runtime-hash replay
correctly rejected changed binaries, and a legacy-data assertion also failed
(its cause is not established). That run was cancelled, not counted as passing.
The legacy test passed in isolation, then the entire unchanged-binary suite
passed. Windows-specific identity and output-link checks are wired into the
three-platform distribution jobs; repaired-head CI and fresh review remain
required. No customer application or SQL was executed.
