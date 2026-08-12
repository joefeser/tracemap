# Framework Migration Evidence v0 Implementation State

Status: producer-implemented

Branch: `codex/framework-migration-producer-v0`

Base: fresh `origin/dev` at
`1937def3647ae090ffc838d2135d06c134f7c6cb`

Issue: [#531](https://github.com/joefeser/tracemap/issues/531)

## Reconciliation

The issue's raw-DDL candidates are already covered by the implemented
PostgreSQL migration specs for quoted-safe gaps, constraints/indexes,
enums/routines, destructive changes, and marked snapshots. EF model mapping is
owned by #436 and application operation calls by #437. This spec owns only the
remaining framework migration-code boundary.

## Scope Decision

Ordinary EF Core `MigrationBuilder` calls are useful static framework evidence
but do not prove PostgreSQL provider selection. The contract therefore keeps
all v0 operations provider-unknown and defers `postgresql-explicit` until a
pinned real Npgsql package/signature fixture exists. Framework admission also
requires allowlisted metadata assembly identity; namespace/type resemblance is
insufficient. The first implementation PR is producer-focused and includes an
explicit reducer exclusion; feature-specific composition follows separately.

Protected raw SQL, seed/data, annotation, default, and computed arguments are
categorical gaps and must not be retained or hashed. No migration execution,
ordering, rollback, generated-SQL, database-state, or safety claim is added.

## Producer Implementation

PR 1 adds the three versioned framework-migration rules, the two closed fact
types, and `csharp-semantic/0.17.0`. Admission requires Roslyn resolution to the
strong-named `Microsoft.EntityFrameworkCore.Relational` metadata assembly with
public-key token `adb9793829ddae60`; source and unsigned metadata lookalikes are
rejected with categorical gaps.

The producer emits the closed eleven-operation vocabulary with constant object
identity, canonical migration/method roles, deterministic invocation ordinals,
direction proven only from real `Up`/`Down` overrides, provider scope
`unknown`, and fixed limitations. Protected SQL, data, annotations,
default/computed expressions, and nested table shapes are gap-only. Their
source text and digests are omitted. A semantic-to-syntax protected-span seam
also prevents proven protected migration content from being reprojected by the
generic C# syntax, integration, SQL text/shape, and later legacy passes. The
bounded syntax fallback protects named and supported positional
default/computed arguments and makes any fallback gap reduce the manifest's
coverage instead of leaving a contradictory successful/full scan.

The generic contract-delta reducer returns before matching either new fact type
or the framework gap rule. `IsSqlFact` and `IsPostgresSchemaFact` remain
unchanged. PR 2 composition into database design-review and release-review is
still deferred.

## Validation

Producer implementation validation at the current worktree:

- Focused framework-migration and reducer isolation tests: 27/27 passed.
- Full .NET solution build: passed with 0 warnings and 0 errors.
- Full .NET solution test: 1,430/1,430 passed.
- Checked-in synthetic CLI scan: `FailedOrPartial` /
  `Level1SemanticAnalysisReduced`; all five artifacts present; NDJSON and
  SQLite contained one declaration, four operations, and one categorical raw-
  SQL gap. The protected SQL sentinel was absent from NDJSON, SQLite, report,
  and analyzer log.
- Changed-file `dotnet format --verify-no-changes`: passed. Repository-wide
  format verification remains noisy from pre-existing formatting findings in
  unrelated files.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

- Confirmed no existing active spec owns framework migration extraction.
- Confirmed all five spec files are private-path free and diff-clean.
- Ran a fresh read-only Kiro `claude-opus-5` spec review. It found six blocking
  contract issues: rule/tier ownership, forgeable admission, unverified Npgsql
  scope, incomplete identity shapes, unreachable annotation gaps, and an
  existing reducer consumer. All six are addressed in the current spec text.
- Verified against the official EF Core API reference that `Migration` and
  `MigrationBuilder` are defined by `Microsoft.EntityFrameworkCore.Relational`.
  The official Npgsql API reference exposes provider migration extensions from
  `Npgsql.EntityFrameworkCore.PostgreSQL`, but labels that surface internal and
  compatibility-unstable; provider-explicit support therefore remains deferred
  until a versioned signature fixture is approved.
- Ran a fresh Kiro re-review after the first corrections. It found two remaining
  blockers: generic `AnalysisGap` reducer matching required a gap-rule early
  return, and unsigned same-name metadata assemblies could still forge
  admission. The contract now requires the EF Core public-key token and a
  rule-aware reducer gate. Its gap-cardinality, generic-consumer, and
  invocation-ordinal recommendations are also incorporated.
- Both Kiro passes reported reduced tool coverage because their sandbox denied
  shell/network access. Repository checks and primary API-reference verification
  were run independently outside the advisory review.
- Hosted review on PR #642 required an admitted migration owner for every
  operation and identified two repository-vocabulary inconsistencies. The spec
  now rejects genuine `MigrationBuilder` calls from ordinary helper types, uses
  exact `Tier4Unknown`, and uses the documented `spec-ready` status token.
- Exact-head Codex follow-up required a closed fact property schema. The design
  now fixes declaration, operation, and gap keys; symbol roles; coverage and
  limitation values; identity encodings; invocation ordinals; aggregation
  keys; and the complete v0 `gapKind` vocabulary.
- A second exact-head follow-up closed the separate top-level `CodeFact`
  identity inputs and prevented gap aggregation from collapsing different
  methods, operation kinds, or directions into one misleading row.
- Implementation and product tests are intentionally deferred until PR 1.

## Owner Decisions Resolved By This Spec

- Do not infer PostgreSQL from Npgsql package presence alone; defer explicit
  provider scope in v0.
- Do not merge generic EF migration evidence into the existing PostgreSQL DDL
  rule family.
- Do not parse or hash raw migration SQL or data-bearing arguments.
- Reject source-declared framework lookalikes by requiring metadata assembly
  identity.
- Audit and exclude the new facts from the generic reducer before adding
  design-review/release-review composition.
