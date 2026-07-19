# Microsoft Access Adapter v0 Runway Implementation State

Status: Phase 0 through Phase 6 implemented; PR #487 review fixes validated and ACK rerun pending

Spec branch: `codex/microsoft-access-adapter-runway`

Implementation branch: `codex/microsoft-access-adapter-v0-foundation`

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

Results: 23/23 focused Access tests passed; solution build passed with the
pre-existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory; 786/786 solution tests
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

One suggestion to skip `index.sqlite` during protected-marker scanning was not
implemented: SQLite is a required artifact and excluding it would weaken the
secret-safety proof. The synthetic artifact is bounded, and the full Windows
smoke scanned it successfully with zero planted-marker matches.

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
