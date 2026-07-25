# EF Core Mapping v0 Implementation State

Status: implementation complete; PR pending

Branch: `codex/ef-core-mapping-v0`

Base: `origin/dev` at `07899292b0a66aa164137bebc747fe4df7f1012c`

Issue: [#436](https://github.com/joefeser/tracemap/issues/436)

## Scope decision

Extend the already-shipped semantic EF and `DatabaseColumnMapping` evidence
rather than creating a parallel ORM subsystem. This v0 covers explicit
entity/table and property/column mappings from resolved annotations and
constant fluent chains, then composes them into the existing combined-index
database design-review packet.

Dynamic configuration and assembly scanning are gaps. Convention-only names,
keys, indexes, relationships, owned types, converters, operation-call
patterns, runtime model reconstruction, generated SQL, and database execution
are deferred.

## Validation

- Focused extractor, database design-review, release-review, and rule-catalog
  tests — passed 46/46.
- `dotnet build src/dotnet/TraceMap.sln --no-restore` — passed with the
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build` — passed
  901/901.
- Documented PostgreSQL sample `scan` → `combine` →
  `database-design-review` smoke — passed twice with byte-identical Markdown
  and JSON; four tables and explicit partial coverage/gaps.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Deferred

- EF keys, indexes, relationships, owned types, and converters.
- Application database-operation call patterns tracked by #437.
- Runtime configuration, compiled models, provider conventions, generated SQL,
  migration execution, and database connectivity.
- Deeper PostgreSQL DDL/framework migration extraction tracked by #531.
