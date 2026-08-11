# Framework Migration Evidence v0 Implementation State

Status: specified; implementation not started

Branch: `codex/postgres-framework-migration-v0`

Base: fresh `origin/dev` at
`42a1556bf84a2725626ec40cae72f36055095236`

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

## Validation

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
