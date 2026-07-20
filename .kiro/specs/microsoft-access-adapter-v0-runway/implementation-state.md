# Microsoft Access Adapter v0 Runway Implementation State

Status: Phase 0 through Phase 9.5 implementation, synthetic Windows validation,
and representative local-only validation are complete; ready for Phase 9 PR

Spec branch: `codex/microsoft-access-adapter-runway`

Implementation branch: `codex/microsoft-access-adapter-v0-macro-reporting-stacked`

Phase 7 branch: `codex/microsoft-access-adapter-v0-ui-bindings`

Public claim level: hidden

## Why This Runway Exists

A local feasibility lab established that installed Microsoft Access 16 on
Windows 11 ARM under a Mac-hosted VM can expose deterministic design metadata
through COM without reading rows or executing saved queries. The lab used a
synthetic zero-row `.accdb` containing:

- four local tables;
- two declared relationships;
- select, parameter, action, and pass-through saved queries;
- one bound form and one bound report;
- a form VBA module with direct Access/DAO call shapes;
- planted fake connection/query secret markers.

The prototype exported schema, query, form/report, and VBA metadata twice and
produced byte-identical outputs. A second run in a network-disabled isolated VM
through a read-only input share produced the same output. The read-only write
canary was rejected, and searches found no planted raw connection string,
credential, private server, query SQL, or local absolute path in the evidence
bundle.

That feasibility evidence informed the foundation implementation. The product
adapter now satisfies the Phase 0 through Phase 9 Git, artifact, rule, safety,
count-only UI/VBA/macro, consumer-gap, and synthetic validation contracts
recorded below. Representative validation remains pending; UI/VBA/macro
identities and bodies remain explicitly unavailable rather than inferred.

## Recorded Decisions

- Build a Windows-local Access adapter rather than attempting to parse the
  proprietary binary file format on macOS.
- Use installed Access/DAO COM automation without Office primary interop
  assembly dependencies; keep platform checks at runtime so the solution still
  builds on macOS/Linux.
- Require the selected database to be a clean tracked file at a concrete Git
  commit. Do not create an alternate provenance exception for standalone files.
- Document a local restricted Git evidence workspace as the workflow for a
  standalone customer database.
- Open only a verified private working copy, never the selected original.
- Force-disable automation macros before open and prove startup suppression with
  hostile canaries before shipping.
- Produce the normal TraceMap artifact set directly; do not standardize the
  feasibility prototype's intermediate JSON as a second public schema.
- Use the owning binary database path at span `1:1` for catalog objects. VBA may
  use real CodeModule line spans while retaining the database as evidence path.
- Reuse shared legacy-data/query facts only when their semantics fit; use
  Access-specific facts for forms, reports, controls, VBA, events, navigation,
  macros, external links, and Access query declarations.
- Treat all v0 output as reduced coverage and hidden public claim level.
- Keep macro command bodies deferred. Names/categories and body-omitted gaps are
  sufficient until a separate safety review proves more is safe.
- Use a supervising CLI plus a private COM worker. The worker reports the exact
  Access PID it created; the supervisor enforces heartbeat and total timeouts and
  may terminate only that validated owned process, never every Access process by
  name.
- Attribute v0 scans to checked-out `HEAD`. Historical revisions require an
  explicit checkout; arbitrary commit selection is deferred.
- Use the existing `Level1SemanticAnalysisReduced` compatibility label because
  no shared structural-reduced label exists, while keeping every Access fact at
  its true Tier2/Tier3/Tier4 evidence tier.
- Prefer the existing `LegacyDataSafeValues` contract or a shared equivalent;
  record any Access-specific secret-bearing identifier heuristics before
  implementation emits clear object names.
- Keep `ScanManifest` within the existing shared contract. Preserve database
  path/hash, Access version, provider capabilities, `rowDataRead=false`, and
  `executionPerformed=false` in rule-backed inventory/capability facts.
- Treat forms/reports/controls, VBA/event procedures, and macro inventory fixture
  expansion as Phase 7/8/9 work respectively; the first-slice fixture requires
  only schema/query/external metadata plus startup and execution canaries.

## First Implementation Slice

Implement Phase 0 through Phase 6 only:

- CLI, Git provenance, clean tracked input, and capability probes;
- controlled-copy/COM lifecycle and non-execution canaries;
- standard artifacts, rules, facts, deterministic identity, and gaps;
- schema, keys/indexes, relationships, saved-query shape/hash/dependencies, and
  external-boundary hashes;
- synthetic zero-row generator, secret-leak regression tests, downstream
  compatibility, and full solution validation.

Do not add forms/reports, VBA, macro bodies, composition reports, or site claims
to the first implementation PR.

## Phase 0 Implementation Decisions

- Runtime/project layout: .NET 10 library `TraceMap.Access` plus dedicated
  executable `TraceMap.Access.Cli` with assembly name `tracemap-access`.
- IPC: the public CLI supervises a private `worker` mode of the same executable.
  The worker writes bounded newline-delimited JSON frames to redirected stdout;
  every frame carries a random scan token and is either `hello`, `heartbeat`,
  `result`, or a safe classification-only `failure`. Raw SQL, connection/source
  values, VBA, macro bodies, database bytes, and absolute paths are prohibited
  from frames. Platform-neutral tests deserialize every frame and scan the wire
  text for planted protected markers.
- Watchdog: 600-second v0 default, configurable from 30 through 3600 seconds;
  five-second worker heartbeat and 30-second idle threshold. The worker creates
  a Windows kill-on-close Job Object where available and reports the exact Access
  PID derived from `Application.Hwnd`. The supervisor validates PID/session/start
  time and never kills by process name. Owned-PID termination is the fallback.
- COM: runtime activation through `Access.Application` without Office PIAs.
  Set `AutomationSecurity=3` and `Visible=false` before opening only the verified
  private copy. No recordset, query execution, field-result enumeration, link
  refresh, object invocation, or macro/VBA execution API belongs in the reader.
- Safe values: make the existing `LegacyDataSafeValues` helper public for shared
  identifier policy; use explicit role-prefixed full SHA-256 for connection,
  external-source, query-text, and protected-value hashing.
- Limits: database bytes 2 GiB; 10,000 objects per collection; 10,000 child
  members per object; 1 MiB per ordinary COM string; 4 MiB query SQL inspected
  in memory; 100,000 facts; 10,000 gaps; 64 MiB safe IPC projection; 512 MiB
  final artifact set. Limit hits are gaps or hard failures as specified.
- Provenance: v0 scans checked-out `HEAD`; tracked/clean status is required.
  Git LFS inputs require a materialized file whose SHA-256 matches the `oid`
  recorded in the HEAD pointer. Pointer-only or unverifiable inputs fail.
- `.mdb`: accepted only when the installed Access/DAO version opens the same
  controlled-copy path. Provider rejection remains a bounded capability failure.
- Windows integration environment: locally installed Access 16.0 64-bit on
  Windows 11 ARM in the isolated, network-disabled Access analysis VM. No
  customer database or private output is used for validation.

## Foundation Implementation Outcome

- Added platform-neutral `TraceMap.Access` and `TraceMap.Access.Cli` .NET 10
  projects. The `tracemap-access` CLI probes Windows and installed Access only
  for `scan`; help and version do not initialize COM.
- Input validation requires a clean tracked checked-out `HEAD`, concrete commit
  SHA, materialized Git LFS bytes, a repository-relative `.accdb` or `.mdb`, and
  a non-destructive output path. Traversal, absolute paths, reparse escapes,
  untracked/dirty bytes (including `assume-unchanged` mismatches), and
  output/input ancestry collisions fail closed. Output mapping preserves the
  requested location when `--repo` names a Git subdirectory.
- A restrictive-ACL working directory receives a stream-copied, hash-verified
  database without alternate data streams. Only the copy is opened and cleanup
  failure becomes a path-free gap.
- The worker sets Access `AutomationSecurity=3` and `Visible=false` before open.
  Its catalog API does not expose recordset, query execution, refresh, macro,
  form/report invocation, or VBA invocation operations.
- The authenticated NDJSON worker protocol carries only bounded safe projections
  and classification tokens. Heartbeat/total timeouts, process-session/start
  validation, kill-on-close Job Object containment, and a revalidated owned-PID
  fallback cover worker crashes, COM hangs, modal prompts, close/quit hangs, Job
  assignment failure, and PID reuse without name-wide termination.
- Standard artifacts publish atomically. Repeated identical catalog failures are
  deduplicated by deterministic fact ID before SQLite insertion; SQLite pools are
  cleared before the Windows staging-directory rename. SQLite failures expose
  only bounded numeric error codes.
- Schema extraction covers local tables, fields, required flags, Access type
  families, declared sizes, indexes, and simple/composite relationships. System
  objects and system relationships are omitted and counted rather than projected
  as ambiguous user schema.
- Saved queries persist only kind, SQL hash/length, parameter descriptors, safe
  local dependency candidates, and complete/partial/unknown coverage. Action,
  DDL, crosstab, union, unsupported, dynamic, malformed, and pass-through shapes
  remain non-executed and partial/unknown as appropriate.
- Linked-table and pass-through boundaries preserve only provider family and
  role-separated hashes. Raw SQL, connection strings, credentials, hosts, DSNs,
  URLs, UNC/drive paths, private names, command bodies, and local absolute paths
  are prohibited from protocol frames and artifacts.
- Provider-compatible `.mdb` is enabled. An incompatible or malformed `.mdb`
  either fails with a bounded capability classification or produces gap-only
  artifacts with no unsupported storage claims.

## Phase 7 Implementation State

The Phase 7 branch is stacked on approved foundation head
`1b2596b54350877b6b55ebb9ee8f9dffb854589b` while PR #487 awaits explicit
owner merge authorization. It will not open a PR against `dev` until #487 is
merged, because doing so would repeat the entire foundation diff.

Implemented platform-neutral pieces:

- cataloged `legacy.access.ui-surface.v1` and `legacy.access.binding.v1` with
  form, report, control, binding, gap, tier, limitation, and non-claim
  contracts;
- added safe serialized projections and fact emission for forms, reports,
  controls, direct record/control/row-source bindings, bounded expression
  candidates, and event categories;
- kept all raw form/report/control descriptors in worker-internal types only;
  IPC/facts retain safe identities, role-separated hashes, lengths, types,
  stable target keys, coverage labels, and gaps;
- added deterministic direct-object/direct-field matching, ambiguity and
  unresolved gaps, quote-masked bracket candidate parsing, protected event
  classification, stable control ordering, and design hashes that do not
  contain raw design text;
- closed a pre-PR contract audit gap for Requirement 6.3: surface filters and
  ordering expressions plus control validation expressions now use the same
  worker-internal-only raw boundary and safe projection as other bindings. Facts
  retain only the expression role, length, role-separated hash, safe resolved
  field keys, partial coverage, and rule-backed gaps; planted expression text is
  absent from worker projection, standard artifacts, and combined artifacts.
  The COM reader does not fetch these properties until issue #488 proves an
  approved non-loading source;
- added a bounded worker-internal Access text-design parser that stops before
  code-behind, ignores captions/labels/values, balances unsupported property
  blocks, and gaps malformed or oversized designs. The parser is not connected
  to an Access export method yet.
- wired the documented unloaded `CurrentProject.AllForms`/`AllReports`
  inventory path. It emits surface facts with `inventory-only` coverage and an
  `AccessFormReportCoverageUnavailable` gap instead of treating unavailable
  control metadata as a complete zero-control design. Allowlisted property
  reads are discarded and gapped if they cause `IsLoaded` to change.
- extended the disposable Phase 7 generator source with two saved forms, one
  report, eight representative controls, direct/row/calculated binding shapes,
  protected captions/expressions, and an event canary. This generator change is
  not considered validated until it runs inside Windows + Access.
- added an artifact/downstream regression proving form, control, binding, and
  rule provenance survive standard SQLite persistence and combined-index
  import while planted control/expression/event values remain absent from all
  standard and combined artifacts.

Windows-only capability gate: issue #488 contains the local-only, no-upload
probe and sanitized result format. The isolated VM deliberately disables host
command execution, so the prompt is intended for a Codex session running on
the Windows machine. Microsoft documentation confirms `AllForms`/`AllReports`
enumerate saved surfaces while the `Forms` collection contains only loaded
forms. `SaveAsText` is documented to export all object properties and
definitions, which can include protected design text and code; it remains
unapproved until a separate threat/canary decision proves an acceptable
worker-local handling path. No `OpenForm`, `OpenReport`, rendering, invocation,
recordset, query, macro, or VBA execution API has been added.

The first physical-Windows attempt stopped correctly with
`phase7-probe-boundary-violation`. Its classification-only postmortem identified
`fixture-generator-visible-or-design-open` during `fixture-generation`; no
extraction probe ran. This exposed an orchestration ambiguity, not evidence that
the reader loaded a surface. The corrected rerun contract separates two stages:

- fixture generation may create, open in design mode, configure, save, and close
  disposable synthetic forms/reports/controls in a dedicated generator Access
  instance while keeping the application UI hidden and executing no event,
  macro, VBA, query, row, link, or external action;
- extraction starts only after that instance exits and a baseline fixture hash
  is recorded. A fresh macro-disabled hidden Access instance opens only the
  private verified copy, and surface loading, design opening, rendering, export,
  invocation, or visible UI remains a hard boundary.

The rerun harness must preserve three independent sanitized outcomes rather
than collapsing them into a single boundary token: canary state after generation
and after extraction, protected-marker counts in prohibited sinks, and baseline
fixture unchanged after extraction. Fixture bytes, generator source, and the
private marker declaration are expected marker carriers and are excluded from
the prohibited-sink count; stdout, stderr, IPC, logs, TraceMap artifacts, and
unexpected scratch remnants are not. Issue #488 carries the corrected prompt
and fresh explicit rerun authorization. Phases 8 and 9 remain paused unless the
corrected Phase 7 rerun completes without a boundary.

The corrected rerun reached extraction and stopped with
`surface-loaded-during-metadata-read`. Both generation and extraction canaries
remained false, prohibited sinks contained zero protected-marker matches, the
baseline fixture was unchanged, Access exited, and cleanup completed. This is
evidence that the probed unloaded-surface property path is outside the v0 safety
boundary; it is not evidence that forms/reports can be safely opened for richer
metadata.

The next narrowed probe tested only collection enumeration, `IsLoaded`, and
transient `Name`. Reading `Name` caused the surface to load, so it stopped at the
direct-catalog boundary. Generation/direct-catalog canaries remained false,
prohibited sinks contained zero marker matches, the fixture was unchanged,
Access exited, cleanup completed, and no product scan ran. This rules out item
identity enumeration itself; it is not safe to "just read names" and then gap
richer properties.

The product reader therefore never indexes an `AllForms`/`AllReports` item. It
reads bounded collection counts only, persists those counts on the database
metadata fact, emits `AccessFormReportCoverageUnavailable` when a nonempty
catalog cannot be identified, and emits zero form/report/control/binding/event
facts. The platform-neutral projector remains covered for future approved safe
inputs but has no production COM source in v0. Tests use a collection whose
indexer throws and prove it is never called. Any future identity, module/bound
state, control, binding, event, or design projection requires a new threat review
and a different proven non-loading source.

Current Phase 7 validation:

- 9/9 focused UI projection tests pass;
- 796/796 full solution tests pass;
- solution build passes with the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- format verification passes for the changed C# files; the solution-wide
  formatter continues to report pre-existing whitespace findings outside this
  PR's changed files;
- private-path guard and `git diff --check` pass.

Phase 7 PR readiness and deferred work:

- no implementation or validation work remains for the count-only Phase 7 slice;
- surface identity, design metadata, controls, direct bindings, and event
  classification remain unchecked tasks 7.2 through 7.4 because Windows evidence
  proved that even indexing a catalog item and reading `Name` can load a surface.
  Requirement 6.5 is satisfied by the rule-backed coverage gap. Those richer
  tasks require a new threat review and a different proven non-loading source.

Final Phase 7 Windows validation used the exact pushed head
`bf764a1568f938ffd63720d4f818b50b2baa3bde`. The count-only product reader
observed form and report counts, emitted zero surface/control/binding/UI-event
identity facts, and preserved the expected coverage gaps through the standard
artifacts and downstream export/combine/report path. Sequential and established
concurrent outputs were deterministic. Generation and extraction canaries
remained false, every surface remained unloaded after the product scan, the
protected-output marker count was zero, and the baseline fixture was unchanged.
Access and worker processes exited, networking was restored, cleanup completed,
and the Windows reference worktree remained clean. The sanitized evidence is
recorded on issue #488 in comment `5016324776`.

PR #492 review hardening preserves a successful form or report count when only
the other catalog is unavailable, marks malformed design text projections
partial rather than complete, uses non-overflowing design size counters, and
does not misclassify period-qualified expressions as direct Access identifiers.
Focused regression tests cover each corrected path.

## Phase 8 Platform-Neutral Work

Branch: `codex/microsoft-access-adapter-v0-vba-flow`, stacked on the pushed
Phase 7 head `ad8a36eec5184a02a68e869ed06fcbecb1b01967`.

Phase 8 begins as a platform-neutral projection slice only. It may add the
cataloged VBA/event/navigation fact contracts, bounded source tokenizer, safe
hash/identity projection, exact same-module event mapping, gaps, and leak tests.
It must not connect any COM/VBProject reader until a separate Windows-local
capability probe proves that source can be obtained from the private copy while
macro security is forced off, without invocation, export, trust-policy changes,
or protected material crossing worker IPC. Raw VBA may exist only transiently
inside that future isolated worker; parser inputs in tests are synthetic.

The parser will treat VBA as Tier3 textual evidence, preserve real module line
coordinates, mask comments and ordinary string contents before call matching,
retain literal targets only through role-specific safe identity projection, and
emit dynamic-boundary gaps for variable/concatenated/`Eval`/`Run`/callback or
otherwise unsupported dispatch. It will not claim runtime dispatch, branch
feasibility, query or row access, navigation, external process execution, or
production use.

Implemented on this branch:

- `legacy.access.vba.v1` and `legacy.access.event-binding.v1`, with cataloged
  limitations and the module, procedure, navigation/call, event-binding, and
  gap fact vocabulary;
- bounded module, procedure, and call projection contracts with role-separated
  identities and module-source/dynamic-expression hashes only;
- a two-pass VBA projector that preserves real module line coordinates, masks
  apostrophe/`Rem` comments and ordinary string contents, recognizes explicit
  same-module calls and allowlisted `DoCmd`, DAO collection, `OpenRecordset`,
  and domain-function literal target shapes, and gaps dynamic or unresolved
  targets rather than guessing;
- exact same-module event-procedure mapping with missing/ambiguity gaps;
- call-limit handling proven by one-call-over-cap lookahead: an exact-cap module
  remains complete, while a genuinely omitted call emits one scoped
  `AccessVbaCallLimitReached` gap without retaining a dangling projection or
  target gap for the omitted call;
- standard-artifact and combined-index tests proving rule/line provenance
  survives while planted VBA comments, SQL, paths, literals, and command bodies
  remain absent.

Phase 8 platform-neutral validation:

- 8/8 focused VBA projection/inventory tests pass;
- 42/42 combined Access foundation/UI/VBA tests pass;
- 804/804 full solution tests pass;
- solution build passes with the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- seven artifact-validator tests, changed-file whitespace verification,
  private-path guard, and `git diff --check` pass. The repository-wide formatter
  still reports pre-existing whitespace findings outside this slice.

Still required before Phase 8 completion: the focused PR/ACK sequence. No
product COM/VBProject source reader or worker IPC source field exists on this
branch.

The Phase 8 synthetic generator now includes one form event procedure with an
observable first-statement canary plus local-call, literal navigation,
DAO/query/table, domain-function, variable target, `Eval`, `Run`, COM/external
process, comment, literal, fake SQL, and fake path shapes. The smoke marker set
was extended accordingly. The isolated Windows + Access run from issue #489
generated the fixture safely and completed task 8.0. No extractor or worker API
was changed by the fixture-only commit.

The proposed live VBE capability harness was blocked by OpenAI's safety system
before execution; Windows did not reject the APIs and no VBE attempt occurred.
Phase 8 v0 therefore no longer seeks or accepts live VBE evidence. The product
reader is restricted to bounded `CurrentProject.AllModules.Count` plus
`Application.Modules.Count` before and after as a safety canary. It never
accesses `Application.VBE`, `ActiveVBProject`, `VBComponents`, component names,
or source lines. It persists only the catalog count, canary outcome, and
`count-observed-source-unavailable` coverage, emits zero VBA module/procedure/
call/event-binding facts, and records rule-backed `AccessVbaProjectUnavailable`.
Any richer VBA extraction is deferred to a separately security-reviewed
execution mechanism and cannot be routed through another model to evade the
boundary.

Phase 8 Windows validation completed successfully at exact head `f332e3b1`.
The sanitized result is recorded on
[issue #489](https://github.com/joefeser/tracemap/issues/489#issuecomment-5016719619).
The offline product smoke passed sequential/concurrent determinism, standard
artifact validation, count-only VBA coverage, zero VBA identity/flow facts, the
expected rule-backed unavailable gap, false generation and extraction canaries,
zero protected-output matches, unchanged baseline fixture, clean Access/worker
exit, restored networking, complete cleanup, and clean reference worktree.
Phase 9 and issue #491 were not started.

The platform-neutral projector was also exercised directly on Windows at the
pre-count-boundary head `3fbe3aea`: all four then-existing
`AccessVbaProjectionTests` and all nine `AccessUiProjectionTests` passed after
correcting a test-only SQLite pooled-connection lock. Commit `c5aad146` clears
SQLite pools before the protected-marker byte scan and opens a fresh connection
for the subsequent fact query; it changes no product, COM, projector, rule, or
evidence behavior. That fix was cherry-picked into this branch and the current
eight focused VBA tests, 42 Access tests, full 804-test solution, build, artifact
validator, private-path guard, and diff checks all pass on macOS.

PR #493 review follow-up filters literal catalog candidates through the target
families implied by typed Access APIs and treats `:` as a VBA statement
terminator during argument parsing. The regression test proves a colon-separated
literal `OpenForm` remains complete while a query-only catalog match for
`OpenForm` stays partial with an explicit unresolved gap.

## Phase 9 Platform-Neutral Work

Branch: `codex/microsoft-access-adapter-v0-macro-reporting-stacked`, synchronized
with Phase 8 merge commit `71cfd901` through branch merge `99fdbb14`.

Before the next PR loop, the repo-local ACK lane adopted the one-pass Qodo
posture enabled by agent-control-kit PR #281: Qodo remains required and
`explicit_only`, but `waitUntilReturnedBeforeProcessing` is false; exact-head
required Codex, fast quorum, and every mechanical/risky-file gate remain intact.
The source checkout was fast-forwarded and built at `d4eeead`, the lane passed
ACK `doctor`, the consumer config regression passed 2/2, and the three focused
ACK #281 behavior tests passed. Future TraceMap loops must invoke that built
`dist/cli.js` (or a verified descendant), not installed stable build `eeb217a`.

The Phase 9 threat decision is to inventory macro declarations and startup/data
macro categories only. No macro command semantic or body inspection is approved
for v0: command names, arguments, conditions, expressions, embedded macro text,
and action bodies remain protected and omitted. Any future command-level
projection requires a new threat review, rule contract, Windows canary, and
artifact/IPC leak proof; this branch must not infer semantics from names.

Platform-neutral scope may add safe/hash macro identities, kind/startup-role
classifications, rule-backed body-omission gaps, Access-specific report counts
and non-claims, deterministic evidence-doc family routing at hidden claim level,
and explicit unsupported-consumer gaps where release-review cannot yet present
Access facts. Product COM macro inventory remains gated on a Windows-local
non-invoking collection probe, and no macro execution/export API is allowed.

Implemented platform-neutral Phase 9 pieces:

- `legacy.access.macro-gap.v1`, `AccessMacroDeclared`, safe/hash macro identity,
  normalized named/UI/data/embedded/unknown categories, exact `AutoExec`
  classification, and one protected-body omission gap per observed category;
- exact startup classification is restricted to an ownerless named `AutoExec`;
  embedded/data macros with the same safe name remain non-startup inventory.
  Optional safe owner stable keys and occurrence ordinals now preserve distinct
  embedded instances and emit source-to-macro fact linkage. An invalid owner
  channel is discarded with `AccessMacroOwnerUnavailable` rather than crossing
  worker IPC;
- a human Access design-evidence summary with hidden public claim level,
  category counts, rule/tier counts, gaps, and repeated non-claims;
- evidence-doc routing of Access facts to the legacy family while preserving
  hidden claim level, citations, rule IDs, tiers, spans, commit SHA, extractor
  provenance, limitations, and supporting IDs;
- vault verification proving the Access macro rule survives combined-index
  graph projection at hidden claim level;
- a structured packet-level `ReleaseReviewGap` named
  `AccessEvidenceConsumerUnsupported`, using `release.review.section.v1` and
  `ReleaseReviewClassifications.PartialAnalysis`, whenever Access evidence is
  present but no dedicated Access comparison section exists. This prevents an
  ignored fact family from becoming an absence/change conclusion. Single and
  combined index paths are both covered; their queries use fixed SQL statement
  shapes rather than formatted table/column identifiers, and combined
  supporting IDs retain the source-qualified `combined_fact_id`.

Macro bodies remain entirely outside the raw/projection models. The projector
accepts only a name and catalog kind; it has no field through which a command,
argument, condition, expression, or body can enter worker IPC or artifacts.

Final Phase 9 validation:

- 9/9 focused macro/reporting tests pass;
- 815/815 full solution tests pass across the final branch history;
- solution build passes with only the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- seven artifact-validator tests, changed-file formatting verification, the
  two-test ACK lane regression, private-path guard, and `git diff --check` pass.
- issue #490 synthetic product/consumer validation and issue #491 representative
  local-only validation both completed with their separate confirmed cleanup
  acknowledgments.

Phase 9 is ready for PR review. The foundation and Phases 7/8 are merged into
`dev`.

Issue #491 records the completed final task 9.5 workflow and its Windows-local
result. It was sequenced after issues #488 through #490 and their reviewed
product wiring. The workflow used an authorized representative database only
inside a disposable no-remote local Git repository, reported labels/counts/
coverage/rule IDs/gaps and boolean safety outcomes only, and deleted the
database, local commit history, outputs, scratch state, checkpoint family, and
sanitized temporary result after confirming the issue comment.

The earlier broad Windows capability prompt on issue #490 is obsolete because
it authorized macro catalog item names and startup properties. Phase 9 now uses
a conservative documented product boundary: bounded
`CurrentProject.AllMacros.Count`. It never indexes the collection and never
reads names, `IsLoaded`, startup properties, macro actions, arguments,
conditions, expressions, XML/text, or bodies. Product output contains only the
named catalog count, explicit coverage, zero macro identity/body
facts, and rule-backed named/embedded/data/startup/body gaps. Issue #490 requires
a fresh exact-head product-only authorization; issue #491 remained paused at
that point until it succeeded.

The authorized Windows product smoke at exact head `42cff969b231d88daab61938bc6c209e57226217`
stopped at the count-only boundary and posted its sanitized result on issue
#490. The run retained no unsafe detail: fixture-generation and extraction
canaries stayed false, protected-output matches were zero, the baseline fixture
was unchanged, processes exited, networking was restored, scratch was removed,
and the worktree stayed clean. However, no named-macro count was observed, the
loaded-macro canary was not established, the success coverage label was absent,
and downstream preservation was not demonstrated. The deterministic runs did
emit zero macro identity facts and the required rule-backed gaps. This was a
safe boundary result, not a successful product validation. At that point task
9.0 remained open, task 9.5 and issue #491 remained paused, and no further
Windows rerun was authorized until the product/count boundary could be
investigated without expanding the approved COM reads.

Post-run review found that Microsoft documents `CurrentProject.AllMacros` but
does not document the `Application.Macros` collection used by the original
loaded-state canary. The fake product test had modeled that unsupported member.
The branch therefore removes that read, keeps loaded-macro state explicitly
unavailable under `legacy.access.macro-gap.v1`, and retains only the documented
bounded catalog count. A future Windows rerun must validate this corrected
contract at a new exact head; it must not revive or substitute an item-level
loaded-state probe.

The corrected Windows run at `c1761a52f6f607dd2c3ef7b56744afeca7b7d34b`
validated the product boundary: the documented count was observed, the removed
loaded-state field stayed absent, coverage and required rule-backed gaps were
correct, identity facts stayed zero, outputs were deterministic, and every
safety/cleanup assertion passed. The run stopped only at downstream validation.
Local contract tests then identified two concrete composition defects: the
Access human report omitted the observed count/coverage, and evidence-doc export
mistakenly routed the Access database metadata fact through the generic legacy
data descriptor projector, which discarded its safe Access metadata. The branch
now renders the count/coverage in the Access report, keeps those fields in the
hidden legacy evidence-doc chunk, and proves combined-index property retention.
Vault continues to preserve the Access rule and limitations, while release-review
continues to emit its documented `AccessEvidenceConsumerUnsupported` structured
gap; neither consumer is permitted to invent a dedicated Access comparison.
Issue #491 remains paused until this corrected consumer-specific contract passes
a fresh exact-head Windows validation.

The first consumer-specific rerun stopped conservatively at `safety-check`
because its outer Windows orchestration deleted the only sanitized summary with
the disposable artifacts. No product or consumer failure was established; the
posted false fields were unavailable placeholders. The smoke harness now accepts
an out-of-root Phase 9 checkpoint and atomically records only closed status,
stage, booleans, and the protected-output match count after each gate. It also
runs the product, report, combine-backed evidence docs, vault, and release-review
checks directly. Database hashes, names, paths, exception text, and protected
values never enter the checkpoint. Raw smoke state may be cleaned first; the
checkpoint remains until its sanitized issue comment is confirmed and is then
deleted.

The first checkpointed run stopped at generation. Because the checkpoint did
not yet include a closed failure reason, it could not distinguish an Access
generator failure from the harness deleting tools staged under its disposable
root. The harness now preflights that its own script, generator, and both CLI
executables exist outside the smoke root before deletion, and it checkpoints a
closed generation failure classification without exception text. This is an
orchestration correction; it does not widen product extraction or authorize a
new Access API. The corrected harness contract passed 8 focused reporting tests,
the 814-test solution, a clean solution build, seven artifact-validator tests,
the private-path guard, and `git diff --check` on macOS. PowerShell parsing and
the closed failure classification remain exact-head Windows gates.

The rerun at `b2c8c7e5ee37ff47425fb702b46d473ffe6e17f1` again stopped with
the initial generation checkpoint (`failureClassification=none`). That outcome
proves only that the coordinator did not persist a later gate; it cannot
distinguish in-process host termination from failure of the mutable latest-file
replacement. It does not establish a product or extraction failure. Fixture
generation now runs in an output-suppressed child PowerShell process. Every
checkpoint update also creates an immutable sequenced snapshot before a
best-effort latest-file replacement. The surviving coordinator records a closed
classification for generation plus every fixture-provenance operation before
the product scan. This remains an orchestration-only change with no new Access
read or extraction permission.

The Windows run at `ca1b5aa680e4355fd57fd430e5e4d8dabd760f62`
advanced through immutable checkpoint sequence 10. Product, report, combine,
evidence-doc, determinism, canary,
baseline, protected-output, process, cleanup, and worktree gates passed; only
vault validation stopped. Local reproduction showed that the prior vault test
depended on platform-neutral `AccessMacroDeclared` projector fixtures, while the
shipped count-only reader correctly emits zero macro identity facts. Vault now
records a structured `AccessEvidenceConsumerUnsupported` gap under
`vault-export.gap.access-evidence-consumer-unsupported.v1` with bounded
supporting combined fact IDs. It does not invent Access identities or flows.

The corrected Windows run at `4d834ab12490354f69f4a8b90f3c1467dd54570c`
completed through immutable checkpoint sequence 15. Product, report, combine,
evidence-doc, vault, release-review, determinism, canary, baseline,
protected-output, process, network, cleanup, and worktree gates all passed. The
primary result and its separate durable cleanup acknowledgment are recorded on
issue #490. The run observed the documented named-macro count, emitted zero
macro identities, and safely generated the approved embedded event marker with
no body; task 9.0 is complete. Named/data macro creation and every macro body or
command read remain deferred because no bounded documented generator/extractor
path was approved.

The first issue #491 attempt recorded explicit authorization for an `.mdb` but
stopped at `input-authorization` before copying the database or launching
Access. Repository review found that task 9.5 had only prose authorization and
no committed executable representative workflow. The branch now includes
`Invoke-AccessRepresentativeSmoke.ps1`: it requires an explicit authorization
switch, streams the input to a generic path in a disposable no-remote Git
repository, runs sequential/concurrent product scans and all downstream
consumers, enforces count-only/non-execution facts, searches outputs for the
original path/name, and writes immutable sanitized checkpoints containing only
the authorized result fields. It also actively monitors every scan for a visible
Access surface and validates manifest/fact commit, rule, tier, and extractor
provenance. The harness contract passed 9 focused tests, the full solution test
run, a zero-error solution build, seven artifact-validator tests, the
private-path guard, and `git diff --check` on macOS. The known SQLite package
advisory warnings remain unchanged.

The exact-head Windows run at
`f3103555baf81995c3ca97c1e2ce3e1d1f9a9de8` completed task 9.5. The primary
issue #491 result records an explicitly authorized `.mdb`, an unchanged
original, a disposable no-remote repository, all standard artifacts, sequential
and concurrent determinism, structural/query/external-boundary evidence, and
count-only form/report/VBA/macro metadata. It records zero UI, VBA-flow, and
macro identity facts; `rowDataRead=false`; `executionPerformed=false`; every
report/combine/docs/vault/release-review contract passing; no visible Access
surface; and zero protected-output matches. The separate confirmed cleanup
acknowledgment records deleted checkpoints and sanitized results, restored
networking, exited processes, and a clean exact-head worktree. These results do
not claim row contents, execution, runtime reachability, production state,
release approval, or that the database or any change is safe.

PR #495 review hardening makes the positive nullable macro-count predicate
explicit, rejects missing or malformed sequential/concurrent job exit results,
checks the emitted `AccessNavigationCandidate` and
`AccessEventBindingCandidate` fact types in the zero-flow canary, disposes both
copy streams across partial-open failures, refuses existing or filesystem-root
representative scratch paths instead of recursively deleting caller-owned
contents, and simplifies the synthetic marker-present condition. The review
delta adds no COM read and does not widen extraction. The successful issue #491
representative result remains pinned to `f3103555`; another representative run
is deferred because the reviewed changes affect harness failure detection,
filesystem safety, and an equivalent positive-count expression rather than the
approved Access read boundary. The post-review head passes 9 focused tests, the
full solution build/test, seven artifact-validator tests, the two-test ACK lane
regression, the private-path guard, and `git diff --check`.

Fresh Codex review on PR #495 required two additional correctness fixes. Every
post-generation synthetic Phase 9 failure now records a closed classification
derived from its checkpoint stage before throwing, so the highest immutable
checkpoint cannot retain `failureClassification=none` after a real gate
failure. The Access design summary now excludes `AnalysisGap` rows from its
rule-based evidence counts; coverage gaps remain reported separately and can no
longer inflate UI/VBA evidence totals.

The Phase 9 generator layer adds a second form button classified as
`[Embedded Macro]` with a protected caption marker, but deliberately supplies
no body. Named and data macro fixture creation remains deferred to the Windows
probe because no documented, bounded generator path was established locally.
Task 9.0 is complete at the approved count-only/embedded-marker boundary.
PowerShell is not installed on the macOS host (`brew info powershell` reports
“Not installed”); syntax and Access behavior were therefore validated by the
successful exact-head issue #491 Windows run.

## Foundation Validation

Platform-neutral validation on the implementation branch:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~AccessFoundationTests --no-restore
dotnet build src/dotnet/TraceMap.sln --no-restore
dotnet test src/dotnet/TraceMap.sln --no-build --no-restore
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
git diff --check
```

Results: 24/24 focused Access tests passed; solution build passed with the
pre-existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory; 787/787 solution tests
passed; seven artifact-validator tests passed; private-path and diff checks
passed. No shared artifact reader changed, so additional pinned adapter smokes
were not required beyond the standard artifact validator and the downstream
Access export/combine/report smoke.

Windows validation used Access 16.0 64-bit on Windows 11 ARM in the isolated,
network-disabled VM with a read-only scoped input share and read-write scoped
output share. The committed PowerShell harness generated a disposable zero-row
fixture and completed:

- two sequential scans with byte-identical `facts.ndjson`, `report.md`, and
  `logs/analyzer.log`;
- two concurrent scans with identical facts, distinct private working copies,
  and unchanged original hash;
- 63 facts for the synthetic `.accdb`, including exactly two declared
  relationship facts and no system-schema ambiguity;
- a successful real Access 2002-2003 `.mdb` scan with 15 facts;
- a malformed `.mdb` gap-only scan with 14 unique facts, three gaps, zero
  storage-object claims, and all five standard artifacts;
- zero startup-canary firings and zero planted protected markers in artifacts;
- successful Access-index JSON export, combine, and combined markdown report.

After review hardening, the self-contained Windows binary and full committed
harness were rerun. The fixture included an indexed field with a redacted raw
name; index membership remained present while that raw name remained absent
from every text and SQLite artifact. The run again passed sequential and
concurrent determinism, real and malformed `.mdb`, canary, protected-marker,
export, combine, and report checks, producing 63 facts and exactly two declared
relationship facts. The isolated VM was then stopped.

## PR #487 Review Fixes

ACK released 12 current-head review threads. The verified fixes:

- reject `.git` and actual Git-directory outputs, require a new output path,
  and preserve existing caller-owned directories in both validation and atomic
  publication;
- preserve exact Git-timeout classifications even if process cleanup races;
- count worker frames as UTF-8 bytes plus an explicit LF delimiter;
- preserve specific bounded COM read classifications and use pointer-width-safe
  Access window-handle conversion;
- retain raw field names only in worker-local lookup maps so redacted field
  names still resolve index/relationship membership without entering IPC or
  artifacts;
- scope index identities to the owning table;
- preserve every gap when the list exactly equals the ceiling and add a limit
  gap only when evidence is actually truncated;
- remove redundant write-through mode while retaining a forced flush and hash
  verification for the private copy.

The marker-scan review suggested skipping `index.sqlite` to avoid loading binary
artifacts into memory. SQLite is a required artifact, so excluding it would
weaken the secret-safety proof. Instead, the harness now scans every artifact
with a bounded 64 KiB rolling UTF-8 window. The full Windows smoke reran with
that implementation and found zero planted markers.

Fresh Codex review on head `7b001f1873ad26120b7ef02d31671201bc5d0e2c`
identified two saved-query projection gaps. The follow-up marks parsed object
references that cannot be resolved to a known local object as partial instead
of silently claiming complete coverage, recognizes comma-separated Access
`FROM` sources, and recognizes parenthesized join sources. Focused tests cover
all three shapes without persisting raw query text.

## Spec Review

The repository Kiro review wrapper completed a full spec review and one
same-session re-review with model selection `auto`.

- Initial review found two low blocking documentation gaps: the missing review
  prompt and missing concrete COM timeout/owned-process strategy.
- Patches added the review contract; supervisor/worker watchdog; exact owned
  Access PID rules; timeout/heartbeat/Job Object fallback; HEAD and Git LFS
  provenance; coverage-label rationale; `.mdb`, aggregate-limit, collision, and
  concurrency tests; and the safe-value implementation gate.
- Re-review reported full coverage, no blocking issues, and ready to merge.
- Remaining implementation-time notes are recorded above rather than treated as
  completed product behavior.

PR review-loop follow-up on PR #485 patched four current-head findings:

- kept Access-specific provenance/capabilities in rule-backed facts rather than
  unsupported adapter-specific `ScanManifest` fields;
- split the synthetic fixture contract so forms/reports, VBA, and macro shapes
  enter only their Phase 7/8/9 implementation PRs;
- required segment-based relative-path validation instead of slash-substring
  matching;
- required future release-review gaps to use structured `ReleaseReviewGap`
  entries and statuses to use `ReleaseReviewStatuses` only.

## Deferred Work

- Form/report/control and binding extraction.
- VBA declaration/call/event/navigation evidence.
- Macro body semantics and data macros.
- Password/encrypted file support.
- Effective permission evidence.
- Runtime execution and row/data analysis, which are outside TraceMap's static
  evidence model.
- Dedicated Access route-flow, property-flow, vault, and site projections;
  vault and release-review currently preserve explicit unsupported-consumer
  gaps instead of inventing Access comparisons.
- Public promotion or customer claims.
