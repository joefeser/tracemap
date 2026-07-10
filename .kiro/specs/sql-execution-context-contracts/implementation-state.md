# SQL Execution Context Contracts Implementation State

Status: implemented-pending-pr-review
Implementation branch: `codex/sql-execution-context-contracts-impl`
Target base: `dev`
Base SHA: `ed6a83cca428c0b4397b3292880487ca97938b66`
Public claim level: hidden

## Implemented Scope

- Added the strict JSON `sql-execution-context/v1` companion contract at
  `<script>.sql.tracemap-sql-context.json` plus a bounded single-line comment
  directive grammar.
- Added deterministic PostgreSQL statement boundaries that ignore comments,
  quoted values, quoted identifiers, and dollar-quoted bodies for structural
  classification and hashing.
- Added categorical context facts for declarations and syntax candidates,
  covering extension, FDW server, user mapping, schema import, foreign table,
  permission, publication, subscription, `pg_cron`, validation, destructive,
  and unknown steps.
- Added rule-backed gaps for invalid sidecars/directives, declaration conflicts,
  unknown steps, missing context, unreadable inputs, and reduced analysis.
- Added ordered SQL context reporting with explicit transition checkpoints,
  manual-client verification guidance, stop/review codes, and static-only
  limitations.
- Reused the generic NDJSON and SQLite fact storage paths so rule IDs, tiers,
  repo-relative spans, commit SHA, extractor version, coverage, and limitations
  survive without a schema migration.
- Added public-safe fixtures, focused extractor/CLI/SQLite/report/rule tests,
  deterministic repeated-scan checks, and planted sentinel leak checks.

## Contract Decisions

- JSON was selected for v1 because TraceMap already has a deterministic
  `System.Text.Json` dependency and no YAML parser dependency in the scanner.
- Steps use positive statement ordinals. Explicit stable step IDs remain a
  possible future contract version because ordinals are edit-sensitive.
- Sidecars take precedence over bounded directives; disagreements emit gaps.
- Validation syntax establishes validation execution mode but does not infer a
  concrete live database role.
- Context-sensitive syntax with unknown server/database roles remains useful
  but has reduced coverage and an explicit missing-evidence gap.
- Context statement hashes are derived after removing value-bearing and comment
  contents. Raw SQL, directive bodies, sidecar values, connection material,
  scheduled command bodies, and infrastructure identities are not stored on
  context facts.

## Validation

Passed on this branch:

- `dotnet build src/dotnet/TraceMap.sln` — zero warnings/errors.
- `dotnet test src/dotnet/TraceMap.sln` — 704 passed.
- Focused `SqlExecutionContextExtractorTests` — 7 passed.
- Two CLI scans of `samples/sql-execution-context` produced byte-identical
  `facts.ndjson` and `report.md`.
- SQLite inspection confirmed context declaration, candidate, and gap rows with
  rule/tier/span provenance.
- Planted credential, hostname, scheduled-command, raw-SQL, and absolute-path
  sentinels were absent from facts, SQLite properties, report, and analyzer log.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Limitations and Follow-Ups

- This implementation does not connect to a database, execute SQL, observe the
  active pgAdmin/client tab, validate permissions, or certify safety.
- PostgreSQL procedural bodies, dynamic identifiers, and unsupported statement
  families remain unknown/reduced context.
- The secret-safety follow-up owns stronger cross-output handling of
  credential-bearing SQL steps; this slice only removes value-bearing content
  from its own context facts.
- Archive-link and permission specs will refine mechanism-specific direction and
  prerequisite semantics without upgrading static context to runtime proof.

## Related Issues

- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)
