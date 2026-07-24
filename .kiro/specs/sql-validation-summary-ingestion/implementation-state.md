# SQL Validation Summary Ingestion Implementation State

Status: merged-to-dev
Branch: `codex/sql-validation-summary-ingestion`
Base: `dev` at `721e79a746883ec4afa06e653d3cc1aae0c64b14`
Issue: [#508](https://github.com/joefeser/tracemap/issues/508)
PR: [#514](https://github.com/joefeser/tracemap/pull/514)
Merge commit: `43c422e836c4885ef1521c12275e9ee4ec7519d7`

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

- Focused SQL validation/runbook/release-review/Access regression suite: 59
  passed, 0 failed.
- Full .NET solution suite: 845 passed, 0 failed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: succeeded with only the
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisories.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- JSON schema parse: passed.
- Repository-wide `dotnet format --verify-no-changes` remains unsuitable as a
  gate because it reports pre-existing whitespace findings in unrelated files;
  no formatting mutation was applied.

## Review fixes

ACK-authorized PR review findings were addressed together:

- normalized the observation catalog tier to `Tier4Unknown` while retaining
  explicit observed-validation/static-tier-not-applicable metadata;
- added a deterministic safe artifact placeholder span to observation findings;
- preserved distinct non-leaking gaps for multiple malformed inputs and
  multiple assertion conflicts;
- treated repeated assertion identities from distinct artifacts as conflicts
  regardless of whether their status strings agree.

During the later `dev`-to-`main` promotion review, additional exact-head fixes:

- require observation expiry to be strictly later than observation time;
- disambiguate same-repository/same-commit sources by categorical target
  context before emitting `AmbiguousSource`; and
- keep rejected observed-validation gaps visible without counting them as
  static-analysis gaps or reducing otherwise complete static coverage;
- replace unsafe rejected artifact IDs with the fixed `unidentified` identity
  before constructing gap IDs or metadata; and
- preserve the context-matched combined source label in release-review
  observation findings.

Promotion-focused validation: 18 SQL validation-summary tests and 874 full
solution tests passed; the private-path guard and `git diff --check` also
passed.

## Deferred

- Cryptographic signatures and external trust stores.
- Live validator execution or database connectivity.
- Additional engines, validator versions, and assertion vocabularies.
- Public site rendering of observed validation evidence.
