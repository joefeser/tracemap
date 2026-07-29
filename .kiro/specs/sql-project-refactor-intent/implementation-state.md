# SQL Project Refactor Intent Implementation State

- Status: implementation complete; ready for PR review
- Branch: `codex/sql-project-refactor-intent`
- Base: `origin/dev`
- Base SHA: `1035e85ceba55fcd2a9dbfc25ede249465404a7a`

## Scope decision

The slice will add a bounded static reader for literal `.sqlproj`
`RefactorLog Include` entries and supported `.refactorlog` operations. Database
design review will treat the results as global SQL Server refactor intent rather
than PostgreSQL declarations. Release review will reuse the existing SQL evidence
input path with a small rule/projection extension.

No new SQL extraction, SQL project build, `.dacpac` inspection, deployment
generation, database connection, or runtime validation is required.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter SqlProjectRefactorTests`
  - 8 passed
- Focused SQL-project, SQL-runbook, database-design-review, release-review, and CLI suite
  - 66 passed
- `dotnet build src/dotnet/TraceMap.sln --no-restore`
  - passed with the repository's existing `NU1903` SQLite package advisory
- `dotnet test src/dotnet/TraceMap.sln --no-restore`
  - 933 passed
- checked-in `samples/sql-project-refactor` CLI scan
  - 1 linked log and 2 supported operations emitted
  - raw operation keys absent from generated output
- `./scripts/smoke-combined-paths.sh`
  - passed after `npm ci` restored the fresh worktree's pinned TypeScript tools
- `./scripts/check-private-paths.sh`
  - passed
- `git diff --check`
  - passed

## Review fixes

- Read generated SSDT operation fields from `Property` `Name`/`Value`
  attributes and operation keys from the `Operation` attribute, while rejecting
  conflicting duplicate values.
- Reject item/metadata/property expressions, include lists, conditions,
  traversal, and symlink/reparse-point targets at the project-link boundary.
- Honor explicit `.sqlproj` `--project` scope and suppress unrelated standalone
  log gaps during project-scoped scans.
- Project refactor operations and gaps into the standalone SQL runbook packet
  without duplicating the explicit release-review projection.

## Deferred

- MSBuild property/glob evaluation.
- `.dacpac` or `refactor.xml` package inspection.
- Deployment-script and target `[dbo].[__RefactorLog]` comparison.
- Richer SQL Server DDL extraction and cross-artifact old/new identity mapping.
- Any operational, compatibility, safety, approval, or applied-state conclusion.
