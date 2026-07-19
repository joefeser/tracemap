# Microsoft Access Adapter v0 Runway Implementation State

Status: ready for implementation; implementation not started

Spec branch: `codex/microsoft-access-adapter-runway`

Implementation branch: not created

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

This is feasibility evidence, not production adapter validation. The prototype
is intentionally not committed as product code because it does not yet satisfy
the full Git/artifact/rule/test contracts in this spec.

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

## Important Safety Unknowns to Resolve During Phase 0

- Confirm the exact Access automation-security property and value suppress
  `AutoExec`, startup forms/functions, embedded macros, and event procedures on
  every supported Access version.
- Confirm catalog enumeration APIs do not implicitly refresh linked tables or
  materialize pass-through/action query results.
- Confirm working-copy ACL and cleanup behavior when Access leaves lock files or
  a COM call fails.
- Choose explicit object/string/module/time limits and test their gap behavior.
- Choose aggregate fact/gap/projection/artifact byte limits and the worker
  heartbeat/total timeout defaults before coding.
- Choose the bounded IPC mechanism and record its framing, heartbeat,
  authentication/scan-token, protected-material, and platform-neutral testing
  rationale before worker implementation.
- Treat 3600 seconds as the v0 timeout ceiling; later VBA/form phases may revise
  it only with new limit and canary evidence.
- Prove owned-PID cleanup still works when Job Object assignment fails.
- Treat a modal repair/trust prompt as a worker hang; macro suppression alone is
  not evidence that modal prompts cannot occur.
- Include explicit worker-IPC protected-material tests and a Git LFS pointer-only
  rejection fixture.
- Confirm Git clean-input verification handles Git LFS-backed Access files
  without attributing pointer bytes to database content.
- Decide whether `.mdb` support is enabled in the first slice or remains a
  provider-dependent capability with a gap.

## Validation for This Spec Branch

Required:

```bash
./scripts/check-private-paths.sh
git diff --check
```

No .NET product source or rule catalog changed in this spec-only branch, so a
full solution build/test is not required unless review changes code outside the
spec directory.

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

## Validation for the Future Implementation Branch

At minimum:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
powershell -File scripts/access-validation/New-SyntheticAccessFixture.ps1 <args>
powershell -File scripts/access-validation/Invoke-AccessSmoke.ps1 <args>
./scripts/check-private-paths.sh
git diff --check
```

Also run Access-index export/combine/report smokes and relevant pinned checks
from `docs/VALIDATION.md`. Record exact commands, Access version, Windows
architecture, fixture hash/label, capability results, canary results, and
deferred coverage without committing machine paths or raw fixture contents.

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
