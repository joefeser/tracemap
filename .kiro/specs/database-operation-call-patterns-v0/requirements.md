# Database Operation Call-Patterns v0 Requirements

## Goal

Emit deterministic static candidates for application database operations and
compose bounded table/entity-linked candidates into database design review.

## Requirements

1. Compiler-resolved EF/EF Core calls shall classify explicit add, update,
   remove, raw-SQL, save, and transaction call patterns.
2. Compiler-resolved Dapper calls shall classify query and execute candidates;
   constant SQL shall contribute only bounded operation/table shape metadata.
3. Compiler-resolved ADO.NET/Npgsql command execution shall classify reader,
   scalar, and non-query candidates; inline constant command text may contribute
   bounded SQL shape metadata.
4. Each candidate shall preserve framework family, operation kind, containing
   symbol, file span, evidence tier, commit SHA, extractor version, rule ID,
   coverage label, and limitations.
5. Dynamic SQL/command text, unresolved target identity, and recognizable
   operation calls without semantic binding shall remain explicit rule-backed
   gaps rather than guessed table operations.
6. Database design review shall link candidates to PostgreSQL table groups only
   through a bounded same-source table identity or one bounded EF
   entity-to-table mapping.
7. Unlinked or ambiguous table/entity candidates shall remain packet gaps.
8. Transaction and save-boundary candidates may be retained as global
   application-operation evidence without claiming a table mutation.
9. No output shall render raw SQL, command text, parameter values, connection
   material, credentials, local paths, or arbitrary expressions.

## Non-goals

- SQL execution, application startup, migrations, or database connectivity.
- Proof of runtime reads, writes, transactions, stored-procedure execution, or
  branch reachability.
- Complete interprocedural repository/DI/data-flow reconstruction.
- Convention-derived EF identities or generated SQL.
- General LINQ classification where the compiler cannot prove a database
  framework receiver.
- Full command-variable or parameter-value flow in v0.
