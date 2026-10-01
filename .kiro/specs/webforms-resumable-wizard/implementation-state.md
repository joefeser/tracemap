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
