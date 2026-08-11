# Framework Migration Evidence v0 Design

## Boundary And Ownership

This runway adds a framework-migration evidence family rather than widening
`database.postgres.schema-migration.v1`. The existing PostgreSQL rule remains
the authority for checked-in SQL DDL and marked snapshots. The new family owns
compiler-resolved migration-code declarations, operations, and gaps:

- `database.framework-migration.declaration.v1`
- `database.framework-migration.operation.v1`
- `database.framework-migration.gap.v1`

Proposed fact types are `FrameworkMigrationDeclared` and
`FrameworkMigrationOperationCandidate`; coverage failures continue to use
`AnalysisGap`. These are static candidates, not executed migrations.
Declarations and operations use `Tier1Semantic`; gaps use `Tier4Unknown`.
The producer remains part of `csharp-semantic`, advanced from `0.16.0` to
`0.17.0` when implementation lands. The syntax-only gap is emitted by that
same extractor version.

## Semantic Admission

The producer extends the existing Roslyn semantic pass. A migration declaration
requires a canonical base-type chain ending at
`Microsoft.EntityFrameworkCore.Migrations.Migration` whose containing assembly
has exact name `Microsoft.EntityFrameworkCore.Relational` and is a metadata
reference rather than scanned source. Its public-key token must equal
`adb9793829ddae60`; missing or different token produces
`FrameworkAssemblyIdentityUnavailable` and no declaration or operation. An
operation requires
a compiler-resolved method whose original or reduced declaration is owned by
`Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder` from that same
allowlisted metadata assembly.

Application methods, wrappers, source-declared framework lookalikes, extension
methods in other assemblies, and unresolved symbols do not qualify. Test
fixtures use a pinned test-only `Microsoft.EntityFrameworkCore.Relational`
package reference for positive metadata and compile identical scanned-source
and unsigned same-name metadata types as negative fixtures. TraceMap product
projects do not acquire an EF Core runtime dependency.

Syntax fallback may emit a
categorical `SemanticBindingUnavailable` gap only when the containing syntax is
bounded enough to identify a possible migration scope; it never invents a
framework or provider fact.

## Closed Operation Vocabulary

The first producer slice supports these EF Core operation categories when their
required identity arguments are constant and bounded:

- `create-table`
- `add-column`
- `alter-column`
- `drop-table`
- `drop-column`
- `rename-table`
- `rename-column`
- `create-index`
- `drop-index`
- `add-foreign-key`
- `drop-foreign-key`

`Sql`, `InsertData`, `UpdateData`, `DeleteData`, generic `Operation`, provider
annotations, computed/default expressions, and arbitrary custom operations are
not projected in v0. They emit safe classifications such as
`RawSqlMigrationOperationUnavailable`, `DataMigrationOperationUnavailable`, or
`UnsupportedMigrationOperation` without retaining argument values or digests.

The identity map is closed:

| Kind | Required constant identity | Optional constant identity | Structured support shape |
| --- | --- | --- | --- |
| `create-table` | `name` | `schema` | `columns` and `constraints` lambdas are not identities; emit `NestedTableShapeUnavailable` |
| `add-column`, `alter-column` | `name`, `table` | `schema` | default/computed arguments emit protected gaps |
| `drop-table` | `name` | `schema` | none |
| `drop-column` | `name`, `table` | `schema` | none |
| `rename-table` | `name`, `newName` | `schema`, `newSchema` | none |
| `rename-column` | `name`, `newName`, `table` | `schema` | none |
| `create-index` | `name`, `table` | `schema` | constant scalar/array `column` or `columns` may be supporting identity; other shapes emit `IndexColumnShapeUnavailable` |
| `drop-index` | `name` | `table`, `schema` | none |
| `add-foreign-key` | `name`, `table`, `principalTable` | `schema`, `principalSchema` | constant scalar/array `column(s)` and `principalColumn(s)` may be supporting identity; other shapes emit `ForeignKeyColumnShapeUnavailable` |
| `drop-foreign-key` | `name`, `table` | `schema` | none |

An operation fact requires all required identity fields. Optional fields stay
absent when unspecified. Constant arrays are admitted only when every element
has a safe Roslyn constant value; partial arrays are gapped as a whole.
The `rename-table` rule deliberately requires constant `newName` even though EF
Core's API permits it to be omitted; an omitted target is not useful bounded
rename evidence and therefore becomes an identity gap.

`Annotation` is a gap-only recognition path: the method must bind to the
allowlisted EF Core metadata `OperationBuilder<T>` family and its receiver
chain must resolve directly to an otherwise admitted migration operation.
Annotation names and values are not retained. Unrelated `Annotation` calls do
not qualify.

Gap records use property `gapKind`. Cardinality is one gap per migration type,
per categorical `gapKind`, with a deterministic `occurrenceCount`; individual
protected invocations do not produce one retained row each. The gap evidence
span covers the migration declaration rather than protected invocation text.

## Identity And Direction

Every declaration and operation carries canonical symbol-role properties for
the migration type and containing method. Direction is `up` or `down` only
when Roslyn proves the containing method overrides the corresponding EF Core
`Migration` member. Helper methods and non-overrides use `unknown` and a gap;
method names alone do not establish direction.

Only an invocation whose directly containing ordinary method symbol overrides
EF Core `Up` or `Down` receives that direction. Invocations inside local
functions, anonymous functions, or helper methods use `unknown` plus
`MigrationDirectionUnavailable`; the producer does not walk outward or infer
direction from a caller.

Object identity is built only from constant arguments bound to allowlisted
parameter symbols. The contract distinguishes table, column, index,
constraint, and extension identity. A missing schema stays explicitly
unspecified; it is not defaulted to `public`. Dynamic or unsafe identifiers are
omitted and produce a gap. Fact IDs include canonical owner, operation,
direction, source coordinates, and a deterministic invocation ordinal within
the containing method so same-named migrations and identical shapes on one line
cannot collide.

## Provider Scope

Provider scope is closed to `unknown` in v0.

An Npgsql package reference is useful project provenance but does not prove the
runtime provider for a migration operation. `ActiveProvider` conditionals are
not interpreted in v0 because doing so would require branch-feasibility and
runtime-provider claims. Generic operations therefore remain useful framework
evidence without being relabeled PostgreSQL. `postgresql-explicit` is deferred
until a follow-up pins a real Npgsql package version, metadata assembly, and
fully qualified callable signatures in an independent fixture.

## Evidence And Safety

Facts preserve the normal repository/commit-bound provenance and safe line
spans. For protected operations (`Sql`, seed/data, annotation, default, and
computed expressions), evidence must be derived from categorical structure
only or omit the snippet hash. It must not hash the full invocation text,
because a digest of credential-bearing SQL or private source values would still
cross the protected-data boundary.

The producer stores no invocation text, raw SQL, seed values, annotation
values, arbitrary expressions, connection material, local absolute paths, or
private infrastructure identity. Rule limitations explicitly state that static
migration code does not prove execution, ordering, rollback, provider choice,
database state, or safety.

## Consumer Composition

The first implementation PR is producer-focused, not consumer-free. Existing
fact storage and basic fact counts may persist the new rows without a schema
change, but every generic consumer is audited. In particular, the contract
delta reducer explicitly excludes `FrameworkMigrationDeclared`,
`FrameworkMigrationOperationCandidate`, and framework-migration gaps until a
later contract can preserve upstream evidence and limitations. The exclusion
is an early return at the top of `MatchFact`, keyed on the two fact types and on
rule ID `database.framework-migration.gap.v1` because gaps share the generic
`AnalysisGap` fact type. `IsSqlFact` and `IsPostgresSchemaFact` remain
unchanged. Regression tests prove table, schema, column, migration, and gap
properties cannot participate in type/member/SQL matching or downgrade a
finding to `UnknownAnalysisGap`.

Other generic consumers are audited explicitly: NDJSON and SQLite persist the
safe rows; `report.md` may count fact types without rendering properties;
snapshot diff may report added/removed fact identities without interpreting
application; static HTML and vault/evidence exports must either preserve the
rule/tier/coverage/limitations or omit the new rows with a gap. Feature-specific
design-review and release-review interpretation stays in PR 2.

A later bounded consumer PR may:

1. display generic framework migration declarations and operations as
   application-side evidence;
2. retain upstream rule, tier, span, commit, extractor, coverage, support IDs,
   and limitations;
3. attach only operations admitted by a later provider-explicit contract with
   one exact bounded object identity to a same-source PostgreSQL group; and
4. emit unlinked, ambiguous, provider-unknown, dynamic-identity, and reduced-
   coverage gaps rather than selecting by short display name.

Release review and database design review must preserve non-claims and may not
describe an `Up` method as applied or a `Down` method as a proven rollback.
Schema-unspecified identity cannot correlate to a `public` schema group by
default; it remains unlinked with a gap.

## Compatibility

The new facts use existing `facts.ndjson`, SQLite generic fact/property, and
report provenance contracts. No existing fact meaning changes. Adding the rule
and fact types requires extractor-version, catalog, validation, and downstream
allowlist review, but not an index schema migration.

## Limitations

- Helper methods, wrapper APIs, custom operation types, reflection, compiled
  migrations, bundles, scaffolding metadata, and design-time service behavior
  remain unavailable.
- Constant source arguments prove only checked-in call shape, not generated SQL
  or runtime values. A constant may be folded from `const` or `nameof` rather
  than appearing as a literal at the call site.
- `Up` and `Down` describe declared override location, not execution order,
  reversibility, symmetry, or success.
- Exact source identity does not prove correspondence with a live database or
  migration history.

## API References

- [EF Core `Migration`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migration)
- [EF Core `MigrationBuilder`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migrationbuilder)
- [Npgsql migration extensions](https://www.npgsql.org/efcore/api/Microsoft.EntityFrameworkCore.NpgsqlMigrationBuilderExtensions.html)
