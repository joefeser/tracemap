# Implementation state

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
