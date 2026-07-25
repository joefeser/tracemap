# EF Core Mapping v0 Implementation State

Status: implemented; ACK merge-ready handoff

Branch: `codex/ef-core-mapping-v0`

Base: `origin/dev` at `07899292b0a66aa164137bebc747fe4df7f1012c`

Issue: [#436](https://github.com/joefeser/tracemap/issues/436)

PR: [#535](https://github.com/joefeser/tracemap/pull/535)

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
  tests — passed. Post-review extractor/design-review coverage passed 13/13.
- `dotnet build src/dotnet/TraceMap.sln --no-restore` — passed with the
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build` — passed
  902/902 after one unchanged rerun. The first post-review run had one
  unrelated transient failure in
  `BuildEnvironmentDiagnosticTests.Cli_restore_failure_artifacts_are_sanitized`;
  that test passed immediately in isolation and the unchanged full rerun
  passed.
- Documented PostgreSQL sample `scan` → `combine` →
  `database-design-review` smoke — passed twice with byte-identical Markdown
  and JSON; four tables and explicit partial coverage/gaps.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Review fixes

ACK authorized six current-head Codex/Qodo threads. The follow-up resolves
fluent arguments against Roslyn parameters (including reordered named
arguments), evaluates constant attribute schema expressions, emits a Tier4 gap
for recognizable fluent chains without semantic binding, removes null-forgiving
semantic failure outputs, and preserves bounded generic/nested entity type
metadata. Focused regression coverage was added for each behavior.

After the review-fix commit, ACK returned `merge_ready / NONE / merge_ready`
with clean checks and merge state, zero unresolved threads, and zero actionable,
held, or stale findings. Codex and Qodo both returned under the configured
trusted-review quorum.

## Deferred

- EF keys, indexes, relationships, owned types, and converters.
- Application database-operation call patterns tracked by #437.
- Runtime configuration, compiled models, provider conventions, generated SQL,
  migration execution, and database connectivity.
- Deeper PostgreSQL DDL/framework migration extraction tracked by #531.
