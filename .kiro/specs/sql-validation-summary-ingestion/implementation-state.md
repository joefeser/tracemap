# SQL Validation Summary Ingestion Implementation State

Status: implementation-complete-awaiting-review
Branch: `codex/sql-validation-summary-ingestion`
Base: `dev` at `721e79a746883ec4afa06e653d3cc1aae0c64b14`
Issue: [#508](https://github.com/joefeser/tracemap/issues/508)

## Scope decision

Implement the first complete offline ingestion slice: strict schema/policy
validation, repeated CLI inputs, SQL runbook composition, release-review
composition, safe structured gaps, synthetic tests, and documentation.

No database connections, SQL execution, raw output ingestion, operator prose,
DBA attestation, Access extraction, site work, or runtime safety conclusions.

## Key decisions

- Require explicit `--sql-validation-as-of` as the deterministic freshness
  instant; this permits post-scan validation without consulting wall-clock time.
- Match repository, commit, and full categorical context before accepting an
  observation.
- Support repeated inputs so duplicates and conflicts have defined behavior.
- Keep accepted observations separate from static facts and their tiers.
- Treat every rejected or ambiguous artifact as a rule-backed gap.

## Validation

- Focused SQL validation/runbook/release-review/Access regression suite: 58
  passed, 0 failed.
- Full .NET solution suite: 844 passed, 0 failed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: succeeded with only the
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisories.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- JSON schema parse: passed.
- Repository-wide `dotnet format --verify-no-changes` remains unsuitable as a
  gate because it reports pre-existing whitespace findings in unrelated files;
  no formatting mutation was applied.

## Deferred

- Cryptographic signatures and external trust stores.
- Live validator execution or database connectivity.
- Additional engines, validator versions, and assertion vocabularies.
- Public site rendering of observed validation evidence.
