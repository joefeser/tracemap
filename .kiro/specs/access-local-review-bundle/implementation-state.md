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
- host-command execution remained unavailable by design in the isolated
  Parallels VM;
- because Codex was not installed inside that VM, the user explicitly moved
  the synthetic run to the established Windows Phase 9.5 environment rather
  than weakening Parallels isolation.

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
- PowerShell is not installed on the macOS host; Homebrew discovery found the
  available formula. Syntax and product execution remain assigned to the
  isolated Windows smoke rather than installing a second PowerShell runtime.
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

## Deferred

- richer UI/VBA/macro extraction or identity;
- password/encrypted inputs and effective permissions;
- runtime/data analysis;
- public promotion or customer-data upload;
- dedicated Access route/property-flow projection.
