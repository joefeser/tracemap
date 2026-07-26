# Access Local Review Bundle Implementation State

Status: implementation complete; Windows smoke and PR workflow pending

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
  stopped at implementation start;
- Windows validation will use the isolated VM only, not broaden sharing or
  networking policy.

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
- Isolated Parallels smoke: pending exact committed head.

## Deferred

- richer UI/VBA/macro extraction or identity;
- password/encrypted inputs and effective permissions;
- runtime/data analysis;
- public promotion or customer-data upload;
- dedicated Access route/property-flow projection.
