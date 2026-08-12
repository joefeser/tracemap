# Framework Migration Evidence v0 Implementation State

Status: producer-composition-and-generic-consumer-audit-implemented

Branch: `codex/framework-migration-export-audit-v0`

Base: fresh `origin/dev` at
`b205d60a3271b0c9280249e46939f98599b509b5`

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
types, `framework-migration/0.1.0`, and
`framework-migration-syntax-fallback/0.1.0`. Admission requires Roslyn resolution to the
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
bounded syntax fallback protects named and supported positional current/old
default/computed arguments and makes any fallback gap reduce the manifest's
analysis coverage instead of leaving a contradictory full analysis. Migration
coverage and build outcome remain separate: a clean MSBuild/Roslyn build stays
`Succeeded` while `analysisLevel` and categorical `knownGaps` state the reduced
migration coverage.

The generic contract-delta reducer returns before matching either new fact type
or the framework gap rule. `IsSqlFact` and `IsPostgresSchemaFact` remain
unchanged.

## Bounded Composition

PR 2 admits the three framework-migration rules into database design review and
release review through closed fact/property allowlists. Declarations are static
evidence; operations are review-recommended application-side evidence. Both
reports preserve the upstream rule, tier, commit, file span, extractor,
coverage, supporting fact ID, and limitations. Upstream categorical gaps remain
rule-backed gaps, and database design review adds a separate rule-backed
`FrameworkMigrationProviderUnknown` gap for every generic operation.

No framework operation is attached to a PostgreSQL table or schema in v0.
Provider-explicit correlation remains unchecked and deferred until a separately
specified, pinned provider contract exists. The reports make no claim about
application, ordering, rollback, generated SQL, runtime provider, compatibility,
database state, safety, or approval.

## Generic Consumer Audit

The generic Markdown report already counts framework migration fact types
without rendering their protected properties or symbols. The static HTML
explorer now preserves framework facts only when their exact rule, fact type,
coverage label, limitation, tier, commit, file span, extractor version, and
supporting fact identity are compatible with the closed producer contract.
Rows with missing or unexpected framework coverage/limitations are omitted
with `explorer.input.framework-migration-metadata-unavailable.v1`.

Snapshot diff, vault export, and evidence-docs export do not yet have dedicated
framework migration semantic projections. They now detect the rule family,
omit it from generic surface projections, preserve bounded supporting fact
IDs, and emit explicit Tier4 omission gaps. They do not silently drop the
facts or repackage them as ordinary dependency-surface claims. A future
snapshot comparison may add migration identity and ordering semantics only
under a separately specified contract.

## Validation

Generic consumer audit validation on
`codex/framework-migration-export-audit-v0`:

- Focused framework migration consumer audit tests: 5/5 passed.
- Focused neighboring consumer and rule-catalog tests: 169/169 passed.
- Full .NET solution build: passed with 0 warnings and 0 errors.
- Full .NET solution test: 1,442/1,442 passed.
- Static HTML valid/invalid metadata projection, Markdown count-only output,
  single/combined snapshot diff gaps, vault omission gaps, and evidence-docs
  omission gaps are covered with protected-value assertions.

Bounded composition validation on `codex/framework-migration-composition-v0`:

- Focused producer, database design-review, release-review, and composition
  tests: 74/74 passed, including framework-only single and combined indexes.
- Full .NET solution build: passed with 0 warnings and 0 errors.
- Full .NET solution test: 1,437/1,437 passed.
- Changed-file `dotnet format`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Producer implementation validation at the current worktree:

- Focused framework-migration tests: 31/31 passed; focused scan/reducer and
  analyzer-capability tests: 35/35 passed.
- Full .NET solution build: passed with 0 warnings and 0 errors.
- Full .NET solution test: 1,434/1,434 passed.
- Checked-in synthetic CLI scan: `Succeeded` /
  `Level1SemanticAnalysisReduced`; all five artifacts present; NDJSON and
  SQLite contained one declaration, four operations, and one categorical raw-
  SQL gap. The manifest names that migration gap categorically without
  misreporting a build failure. The protected SQL sentinel was absent from
  NDJSON, SQLite, report, and analyzer log.
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
