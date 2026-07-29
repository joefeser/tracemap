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
  - 5 passed
- Focused SQL-project, database-design-review, release-review, and inventory suite
  - 45 passed
- `dotnet build src/dotnet/TraceMap.sln --no-restore`
  - passed with the repository's existing `NU1903` SQLite package advisory
- `dotnet test src/dotnet/TraceMap.sln --no-restore`
  - 930 passed
- checked-in `samples/sql-project-refactor` CLI scan
  - 1 linked log and 2 supported operations emitted
  - raw operation keys absent from generated output
- `./scripts/smoke-combined-paths.sh`
  - passed after `npm ci` restored the fresh worktree's pinned TypeScript tools
- `./scripts/check-private-paths.sh`
  - passed
- `git diff --check`
  - passed

## Deferred

- MSBuild property/glob evaluation.
- `.dacpac` or `refactor.xml` package inspection.
- Deployment-script and target `[dbo].[__RefactorLog]` comparison.
- Richer SQL Server DDL extraction and cross-artifact old/new identity mapping.
- Any operational, compatibility, safety, approval, or applied-state conclusion.
