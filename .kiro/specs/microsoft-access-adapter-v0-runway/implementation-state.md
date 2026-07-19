# Microsoft Access Adapter v0 Runway Implementation State

Status: Phase 0 through Phase 6 ACK merge-ready on PR #487; Phase 7 projection pipeline in progress pending Windows COM capability evidence

Spec branch: `codex/microsoft-access-adapter-runway`

Implementation branch: `codex/microsoft-access-adapter-v0-foundation`

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
adapter now satisfies the Phase 0 through Phase 6 Git, artifact, rule, safety,
and validation contracts recorded below; forms/reports, VBA, and macros remain
later slices.

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

Current Phase 7 validation:

- 30/30 focused Access foundation/UI tests pass;
- 793/793 full solution tests pass;
- solution build passes with the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- private-path guard and `git diff --check` pass.

Still required before Phase 7 PR readiness:

- sanitized Windows capability result on issue #488;
- approved non-invoking COM/design source for control and binding metadata;
- fixture generator extension and complete product-reader wiring;
- hostile canary, marker, determinism, concurrent-scan, export/combine/report,
  and full solution validation on the wired implementation.

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
- standard-artifact and combined-index tests proving rule/line provenance
  survives while planted VBA comments, SQL, paths, literals, and command bodies
  remain absent.

Phase 8 platform-neutral validation:

- 3/3 focused VBA projection tests pass;
- 33/33 combined Access foundation/UI/VBA tests pass;
- 796/796 full solution tests pass;
- solution build passes with the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- changed-file whitespace verification passes. The repository-wide formatter
  still reports pre-existing whitespace findings outside this slice.

Still required before Phase 8 PR readiness: Phase 7 completion, fixture task
8.0, an approved Windows-local non-invoking VBA source capability, product
reader/worker wiring for task 8.2, Windows hostile-canary and protected-marker
validation, and the focused PR/ACK sequence. No product COM/VBProject reader or
worker IPC source field has been added on this branch.

The Phase 8 synthetic generator now includes one form event procedure with an
observable first-statement canary plus local-call, literal navigation,
DAO/query/table, domain-function, variable target, `Eval`, `Run`, COM/external
process, comment, literal, fake SQL, and fake path shapes. The smoke marker set
was extended accordingly. This does not complete task 8.0 yet: PowerShell is
not installed on the macOS host, and the generator change remains unparsed and
unexecuted until the isolated Windows + Access run from issue #489. No extractor
or worker API was changed by this fixture-only commit.

Windows capability gate: issue #489 contains a separate local-only prompt and
sanitized result contract for `CurrentProject.AllModules`, already-loaded module
inventory, and read-only VBProject/CodeModule access under the machine's existing
trust policy. The prompt forbids trust-policy changes, module open/export/edit,
execution, protected-source logging/IPC, and retrying around access denial. An
unavailable result maps to `AccessVbaProjectUnavailable` rather than weakening the
boundary.

## Phase 9 Platform-Neutral Work

Branch: `codex/microsoft-access-adapter-v0-macro-reporting-stacked`, stacked on the
pushed Phase 8 fixture head `4919be5f`.

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
  ignored fact family from becoming an absence/change conclusion.

Macro bodies remain entirely outside the raw/projection models. The projector
accepts only a name and catalog kind; it has no field through which a command,
argument, condition, expression, or body can enter worker IPC or artifacts.

Current Phase 9 platform-neutral validation:

- 3/3 focused macro/reporting tests pass;
- 36/36 combined Access foundation/UI/VBA/macro tests pass;
- 799/799 full solution tests pass;
- solution build passes with only the pre-existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory;
- changed-file whitespace verification, private-path guard, and
  `git diff --check` pass.

Still required before Phase 9 PR readiness: fixture task 9.0, a Windows-local
non-invoking macro collection result and product-reader wiring for 9.1, the
representative local-only workflow in 9.5, and completion/merge sequencing for
the foundation, Phase 7, and Phase 8 dependencies. No Phase 9 PR should open
against `dev` while those stacked dependencies remain unmerged.

Windows capability gate: issue #490 contains the local-only macro inventory
prompt and sanitized result contract. It tests only non-invoking catalog and
startup-presence properties, forbids command/action/body access even if exposed,
and maps unavailable named/embedded/data/startup categories to rule-backed gaps
instead of weakening the fixed threat decision.

The Phase 9 generator layer adds a second form button classified as
`[Embedded Macro]` with a protected caption marker, but deliberately supplies
no body. Named and data macro fixture creation remains deferred to the Windows
probe because no documented, bounded generator path was established locally.
Task 9.0 therefore remains unchecked. PowerShell is not installed on the macOS
host (`brew info powershell` reports “Not installed”), so script parsing and
Access behavior must be validated in issue #490 before this fixture claim can
advance.

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
- Route-flow, property-flow, release-review, vault, and site composition.
- Public promotion or customer claims.
