# Database Operation Call-Patterns v0 Design

## Fact contract

Add `DatabaseOperationCandidate` under
`database.operation.call-pattern.v1`. Every row is a static call candidate, not
a runtime fact.

Allowlisted properties:

- `frameworkFamily`
- `operationKind`
- `methodName`
- `entityType`
- `tableName`
- `routineName`
- `sqlOperationName`
- `targetIdentityStatus`
- `coverageLabel`
- `limitations`

The extractor never stores SQL or command text on this fact.

## Classification

- EF/EF Core: `insert-candidate`, `update-candidate`,
  `delete-candidate`, `select-candidate`, `save-boundary`,
  `transaction-begin`, `transaction-commit`, and
  `transaction-rollback`.
- Dapper: `select-candidate` for query calls; execute calls use bounded
  constant SQL shape when available, otherwise `execute-candidate` plus a
  target gap.
- ADO.NET/Npgsql: reader/scalar/non-query candidates. Only inline constant
  command construction is projected to SQL shape in v0.
- `CALL`/`EXEC`/`EXECUTE` shapes become stored-routine-call candidates with
  bounded routine identity when the existing shape parser supplies it;
  otherwise the target remains a gap.

Recognizable operation syntax without semantic binding emits Tier4 gap evidence
only. Name resemblance never becomes a Tier1 operation.

When no semantic model exists for a C# file, the integration syntax fallback
emits name-only Tier4 operation gaps. Files with a semantic model do not also
receive those fallback gaps. Compiler-resolved EF candidates must trace to an
allowlisted framework declaration or override chain; merely deriving an
application class from `DbContext` is insufficient.

Constant SQL matching retains safe one- or two-part table identity before
composition. If qualification is unsafe or cannot be retained, the operation
target remains unavailable rather than being matched by a stripped table name.

## Design-review composition

1. Existing PostgreSQL declarations establish table groups.
2. Existing explicit EF table mappings establish entity-to-table candidates.
3. Operation candidates with a table identity use the existing exact
   schema/table or unique schema-unspecified same-source match.
4. Entity-scoped candidates use exactly one bounded entity-to-table match.
5. Save/transaction boundaries are global evidence.
6. Zero or multiple matches produce gaps.
7. A truncated or reduced route-path search produces coverage gaps; it never
   becomes evidence that no route reaches an operation.

## Limitations

Static call evidence does not prove the call executes, reaches a database,
targets the configured provider, commits, affects rows, or succeeds. Save calls
do not identify which tracked changes are persisted. Transaction calls do not
prove scope, isolation, commit, rollback, or atomicity. SQL-shape correlation
does not prove generated SQL or runtime parameter values.
