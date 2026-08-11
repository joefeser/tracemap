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
allowlisted metadata assembly. The invocation's enclosing type must separately
pass the migration-declaration admission rule. A helper in an ordinary type
that accepts a genuine `MigrationBuilder` cannot emit an operation or gap under
this rule family.

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

| Kind | Required API parameter to output property | Optional API parameter to output property | Structured support shape |
| --- | --- | --- | --- |
| `create-table` | `name` → `tableName` | `schema` → `schemaName` | `columns` and `constraints` lambdas are not identities; emit `NestedTableShapeUnavailable` |
| `add-column`, `alter-column` | `name` → `columnName`; `table` → `tableName` | `schema` → `schemaName` | default/computed arguments emit protected gaps |
| `drop-table` | `name` → `tableName` | `schema` → `schemaName` | none |
| `drop-column` | `name` → `columnName`; `table` → `tableName` | `schema` → `schemaName` | none |
| `rename-table` | `name` → `tableName`; `newName` → `newTableName` | `schema` → `schemaName`; `newSchema` → `newSchemaName` | none |
| `rename-column` | `name` → `columnName`; `newName` → `newColumnName`; `table` → `tableName` | `schema` → `schemaName` | none |
| `create-index` | `name` → `indexName`; `table` → `tableName` | `schema` → `schemaName` | constant scalar/array `column` or `columns` → `columnNames`; other shapes emit `IndexColumnShapeUnavailable` |
| `drop-index` | `name` → `indexName` | `table` → `tableName`; `schema` → `schemaName` | none |
| `add-foreign-key` | `name` → `constraintName`; `table` → `tableName`; `principalTable` → `principalTableName` | `schema` → `schemaName`; `principalSchema` → `principalSchemaName` | constant scalar/array `column(s)` → `columnNames` and `principalColumn(s)` → `principalColumnNames`; other shapes emit `ForeignKeyColumnShapeUnavailable` |
| `drop-foreign-key` | `name` → `constraintName`; `table` → `tableName` | `schema` → `schemaName` | none |

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

## Closed Fact Property Schema

Properties are ordinal, case-sensitive keys with invariant-culture values.
Extractor version, rule ID, evidence tier, repository-relative span, and commit
SHA remain first-class `Fact`/`EvidenceSpan` fields and are not duplicated as
properties. No producer may add an unlisted property without a versioned spec
change.

All facts take `ScanId`, `Repo`, and `CommitSha` from the scan manifest;
`ProjectPath` is the normalized repository-relative project path; and evidence
uses the normalized repository-relative source file plus exact 1-based line
span. The remaining top-level `CodeFact` identity fields are closed:

| Fact | `SourceSymbol` | `TargetSymbol` | `ContractElement` |
| --- | --- | --- | --- |
| `FrameworkMigrationDeclared` | null | fully qualified admitted migration type display name | `framework-migration` |
| `FrameworkMigrationOperationCandidate` | fully qualified directly containing method display name | fully qualified resolved EF Core method display name | exact `operationKind` |
| framework-migration `AnalysisGap` | directly containing method display name when `sourceSymbolId` is available; otherwise null | null | exact `gapKind` |

These values, including required nulls, participate unchanged in
`FactFactory.CreateFactId`. Canonical IDs remain in properties; display names
in top-level symbol fields are not identity substitutes.

`FrameworkMigrationDeclared` requires:

| Property | Closed value or format |
| --- | --- |
| `declarationKind` | `framework-migration` |
| `frameworkFamily` | `ef-core` |
| `providerScope` | `unknown` |
| `migrationTypeName` | fully qualified Roslyn display name |
| `targetSymbolId` | canonical migration type ID from `AddSymbolProperties` |
| `targetSymbolKind` | `NamedType` |
| `targetAssemblyIdentity` | canonical containing-assembly identity |
| `coverageLabel` | `bounded-static-migration` |
| `limitations` | `Static framework migration declaration only; execution, ordering, provider selection, generated SQL, database state, rollback, and safety are not proven.` |

`FrameworkMigrationOperationCandidate` requires the following common keys;
the identity keys permitted for each `operationKind` are defined by the closed
identity map above.

| Property | Closed value or format |
| --- | --- |
| `frameworkFamily` | `ef-core` |
| `providerScope` | `unknown` |
| `migrationTypeSymbolId` | canonical admitted migration type ID |
| `sourceSymbolId` | canonical directly containing method ID |
| `sourceSymbolKind` | `Method` |
| `sourceAssemblyIdentity` | canonical containing-assembly identity |
| `targetSymbolId` | canonical resolved EF Core method ID |
| `targetSymbolKind` | `Method` |
| `targetAssemblyIdentity` | canonical EF Core relational assembly identity |
| `direction` | `up`, `down`, or `unknown` |
| `operationKind` | one closed operation kind listed above |
| `objectKind` | `table`, `column`, `index`, or `foreign-key` according to operation kind |
| `invocationOrdinal` | 1-based decimal ordinal by invocation `SpanStart` within the directly containing method; no leading zeros |
| `coverageLabel` | `bounded-static-migration` |
| `limitations` | `Static framework migration operation candidate only; execution, ordering, provider selection, generated SQL, database state, rollback, reversibility, and safety are not proven.` |

`objectKind` is `table` for create/drop/rename-table, `column` for
add/alter/drop/rename-column, `index` for create/drop-index, and `foreign-key`
for add/drop-foreign-key.

The only optional operation identity keys are `schemaName`, `tableName`,
`newSchemaName`, `newTableName`, `columnName`, `newColumnName`, `indexName`,
`constraintName`, `principalSchemaName`, `principalTableName`, `columnNames`,
and `principalColumnNames`. The per-kind table determines which are required or
allowed. Multi-column values are canonical JSON string arrays preserving source
order; no delimited ad hoc encoding is allowed.

Framework-migration `AnalysisGap` properties are closed to:

| Property | Closed value or format |
| --- | --- |
| `gapKind` | one value from the gap vocabulary below |
| `frameworkFamily` | `ef-core` |
| `providerScope` | `unknown` |
| `migrationTypeSymbolId` | canonical admitted migration type ID when semantic admission succeeded; otherwise absent |
| `sourceSymbolId` | canonical directly containing method ID when available; otherwise absent |
| `sourceSymbolKind` | `Method` when `sourceSymbolId` is present; otherwise absent |
| `sourceAssemblyIdentity` | canonical source assembly identity when `sourceSymbolId` is present; otherwise absent |
| `operationKind` | recognized closed operation kind when available; otherwise absent |
| `direction` | `up`, `down`, or `unknown` when available; otherwise absent |
| `occurrenceCount` | positive invariant decimal with no leading zeros |
| `coverageLabel` | `reduced-static-migration` |
| `limitations` | `Static framework migration coverage is reduced; omitted protected content and runtime behavior were not analyzed.` |

The v0 `gapKind` vocabulary is:

- `FrameworkAssemblyIdentityUnavailable`
- `SemanticBindingUnavailable`
- `MigrationDirectionUnavailable`
- `DynamicIdentityUnavailable`
- `MissingRequiredIdentity`
- `NestedTableShapeUnavailable`
- `IndexColumnShapeUnavailable`
- `ForeignKeyColumnShapeUnavailable`
- `RawSqlMigrationOperationUnavailable`
- `DataMigrationOperationUnavailable`
- `AnnotationMigrationOperationUnavailable`
- `DefaultOrComputedExpressionUnavailable`
- `UnsupportedMigrationOperation`

Gap aggregation keys are migration type identity (or repository-relative file
and bounded possible-migration declaration span when semantic identity is
unavailable), `gapKind`, containing method identity or `<none>`, recognized
`operationKind` or `<none>`, and `direction` or `<none>`. Thus unlike operations
or directions never collapse into one row, while repeated equivalent gaps in
one method aggregate into `occurrenceCount`. Gap properties never include
protected argument text, a protected-value digest, a local absolute path, or
arbitrary exception text.

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
