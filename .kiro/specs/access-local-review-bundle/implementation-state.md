# Access Local Review Bundle Implementation State

Status: implementation and exact-head Windows smoke complete; PR workflow
pending

Branch: `codex/access-local-review-bundle`

Base: `origin/dev` at `580e0c876477c4d0d0f850c1e345c5346f6da59e`

Public claim level: hidden

## Scope decision

Build a one-command local review bundle over already-shipped Access scan
artifacts. Reuse the Access-only release-review composition and static HTML
explorer. Add no extractor, COM, projector, worker, fixture, or fact-schema
change.

The bundle is intended to help a database owner and developer understand
bounded static schema, relationship, saved-query, external-boundary, and
count-only capability evidence before an in-person review.

## Established safety boundary

- no rows, recordsets, queries, links, macros, or VBA execute;
- no form/report open, render, export, invocation, or item identity read;
- no VBA source, macro body, caption, expression, raw SQL, connection,
  credential, private infrastructure name, or local path output;
- UI/VBA/macro identities remain unavailable with explicit gaps;
- existing Windows canaries and representative authorization remain
  authoritative.

## Local environment

- macOS host with .NET implementation and platform-neutral tests;
- Parallels is installed;
- `Windows 11` is running;
- the previously validated `Windows 11 - Access Isolated` VM is present and
  was started for validation;
- host-command execution had not yet been exercised in the isolated Parallels
  VM, so the user explicitly moved the synthetic run to the established
  Windows Phase 9.5 environment rather than weakening isolation;
- later `access-parallels-source-runner` validation proved Parallels Tools
  guest command execution works with networking still disabled and without
  installing Codex inside the VM.

## Validation

- Focused Access bundle, macro-reporting, and explorer tests: 36/36 passed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with the
  repository's existing `NU1903` SQLite package advisories.
- First full solution test: 911/912 passed; the unrelated
  `BuildEnvironmentDiagnosticTests.Cli_restore_failure_artifacts_are_sanitized`
  failed its synthetic restore classification.
- Exact isolated rerun of that test: 1/1 passed without a code change.
- Unchanged full solution rerun: 912/912 passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- PowerShell 7.6.4 was installed from Homebrew after review changed both
  Windows harnesses; both scripts pass parser-only syntax validation locally.
- Exact-head Windows synthetic smoke at `9cdae53b`: completed.
- Both changed PowerShell scripts passed syntax parsing.
- Phase 9 consumer contracts and the local-review-bundle contract passed.
- The retained bundle had the expected manifest, relative contained paths,
  verified file sizes and SHA-256 hashes, available Access evidence, and
  positive finding/gap counts.
- Determinism, generation/extraction canaries, protected-output suppression,
  and baseline fixture integrity passed.
- Access and worker processes exited, networking was restored, raw scratch was
  removed, only the sanitized local review bundle was retained, and the
  exact-head worktree remained clean.
- The Windows run edited, committed, pushed, and posted nothing.

## Review hardening

ACK authorized one consolidated patch for the six exact-head review threads.
The patch:

- verifies manifest, NDJSON, and SQLite scan identity plus exact fact-ID
  inventory before composing either consumer;
- resolves existing symlink/junction/reparse ancestors on every platform before
  overlap checks while rejecting a selected path that is itself a reparse point;
- preserves or restores the previous generated bundle on publication failure,
  retaining the backup when a concurrent collision prevents restoration;
- distinguishes rooted machine-local paths from valid repository-relative
  `private` and `home` path segments;
- converts unexpected composition/filesystem failures to categorical
  path-safe diagnostics;
- accepts both `available` and valid `truncated` Access evidence in the
  synthetic and representative harness contracts.

Post-patch validation:

- focused Access bundle, macro-reporting, and explorer tests: 40/40 passed;
- focused Access bundle tests after cross-platform ancestor resolution:
  10/10 passed;
- both PowerShell harnesses passed syntax parsing;
- solution build: passed with the unchanged SQLite package advisories;
- full solution tests after the final review fix: 917/917 passed;
- private-path guard: passed;
- `git diff --check`: passed.

The successful Windows smoke remains authoritative for the unchanged Access
reader/extractor boundary. A second Access run is deferred because the review
patch changes only read-side artifact consistency, output publication safety,
categorical diagnostics, and acceptance of an already-supported `truncated`
status; it adds no COM read, projector fact, fixture behavior, or execution
surface.

## Issue #563 finding-cap hardening

Branch: `codex/issue-563-access-hardening`

- `access-review create` now accepts `--max-findings <1-10000>`.
- The default is 1,000 so a representative medium scan is not silently bounded
  by the general release-review default of 100.
- Release-review ordering remains deterministic. If the selected cap is
  exceeded, the Access section and packet remain `truncated`, the omitted count
  is retained, and a `TruncatedByLimit` gap is emitted.
- This remains read-side composition only. It does not open Access, add COM
  reads, or change count-only UI/VBA/macro acquisition.
- Synthetic bundle tests cover deterministic custom-cap output, explicit
  truncation, and invalid-bound rejection.

## Deferred

- richer UI/VBA/macro extraction or identity;
- password/encrypted inputs and effective permissions;
- runtime/data analysis;
- public promotion or customer-data upload;
- dedicated Access route/property-flow projection.
