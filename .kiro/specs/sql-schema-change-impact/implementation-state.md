# SQL Schema Change Impact Implementation State

Status: PR 3 implemented

## Branch

`codex/sql-schema-combined-surfaces`

## Current State

Implemented SQL/schema impact v1 in `dev`. This branch adds the deferred PR 3 precision slice for combined `sql-query` and `sql-persistence` matching.

## Shipped Scope

- Added `tracemap reduce --sql-schema-delta <delta.json>` as a mutually exclusive alternative to `--contract-delta`.
- Added strict `sql-schema-delta.v1` input validation for required IDs, duplicate IDs, allowed SQL kinds, allowed change types, unknown versions, empty changes, and unsafe selector-only references.
- Normalized SQL/schema changes into existing contract-delta reducer selectors instead of adding a separate impact engine.
- Matched single-index SQL evidence for table, column, query shape hash, text hash, SQL resource, and persistence mapping facts.
- Accepted combined indexes through the same reducer path while preserving source labels, commits, evidence tiers, rule IDs, and explicit context-unavailable gaps.
- Added SQL-specific report types: `SqlSchemaChangeImpactSingleV1` and `SqlSchemaChangeImpactCombinedV1`.
- Added SQL output names for directory targets: `sql-impact-report.md` and `sql-impact-report.json`; directory `--format json` emits JSON only.
- Added SQL selectors: `--table`, `--column`, and `--query-shape`.
- Preserved contract-delta output names and behavior for existing `--contract-delta` users.
- Reused the shared combined surface projection for combined SQL/schema matching so `sql-query` and `sql-persistence` evidence uses the same safe table, column, shape-hash, text-hash, source-kind, source label, source index ID, scan ID, commit SHA, rule ID, and evidence-tier projection as combined dependency reporting.
- Kept query evidence distinct from mapping/persistence evidence; query selectors do not match mapping-only `sql-persistence` rows.
- Added deterministic combined SQL/schema tests for projected query and persistence surfaces, source labels, scan IDs, safe metadata, and mapping-only no-overclaiming.

## Scope Decisions

- Combined SQL/schema mode now uses projected `sql-query` and `sql-persistence` surface facts where available. Non-SQL reducer behavior and single-index SQL/schema matching continue to use the existing fact reader.
- Path and reverse context remain bounded/deferred through existing reducer behavior: unsupported or unstable SQL selectors emit `PathContextUnavailable` and `ReverseContextUnavailable` rather than inventing traversal.
- No `sql.schema.*` rules were added because the existing `contract.delta.input.v2`, `contract.delta.impact.v2`, and `contract.delta.context.v2` rules cover this adapter slice.
- `old`, `new`, and metadata objects are not yet projected into the SQL report model; v1 matching uses safe `reference` fields only.

## Seams Checked

- Existing contract-delta v2 parser, reducer classifications, report writer, and output path handling in `TraceMap.Reduction`.
- Existing CLI option parsing and `reduce` command wiring in `TraceMap.Cli`.
- Existing SQL fact shapes for `QueryPatternDetected`, `SqlTextUsed`, `SqlFileDeclared`, and `DatabaseColumnMapping`.
- Existing combined-index fact reader and source provenance fields.
- Shared combined SQL surface projection in `TraceMap.Core` and `TraceMap.Reporting`.
- Existing rule catalog entries for contract delta input, impact, and context rules.

## Validation

- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter SqlSchemaChangeImpactTests`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

## Follow-Ups

- Add real bounded path/reverse traversal from stable SQL surface identities.
- Expand SQL/schema tests to cover every kind independently, including schema and persistence-surface deltas.
- Project safe `old`, `new`, and metadata fields where useful without rendering unsafe values.
- Add cap/truncation, duplicate/volatile identity, unlinked-surface, and reduced-coverage SQL-specific regression tests.
