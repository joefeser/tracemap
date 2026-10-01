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
