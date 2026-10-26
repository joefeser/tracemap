# Design

## Contracts (all new, versioned, additive)

| Contract | Purpose |
| --- | --- |
| `sqlserver-reader/1` | Fact property `sqlServerReaderState` on every ExecuteReader-family DatabaseOperationCandidate. Independent of `sqlServerParameterState`. |
| `sqlserver-result-declarations/1` | Authored public JSON, origin `independent-synthetic-result-declarations`. |
| `sqlserver-result-schema/1` | Admitted JSON-origin export; extractor `sqlserver-synthetic-result-declarations/1`, rule `database.sqlserver.synthetic-result-shape.v1`. |
| `sqlserver-ddl-result-input/1` | Opt-in DDL manifest (same fields as `sqlserver-ddl-input/1`, distinct version). |
| `sqlserver-ddl-result-declarations/1` + `sqlserver-ddl-result-schema/1` | DDL-origin projection `public-synthetic-tsql-ddl-result`; extractor `sqlserver-ddl-result-shapes/1`, rule `database.sqlserver.ddl-result-shape.v1`. |
| `sqlserver-result-bindings/1` | Exact fact ID, two-part command text, four-part identity and `readerEvidenceSha256` (reader-state digest, not the parameter `CallDigest`). |
| `sqlserver-result-comparison/1` | Rows under `database.sqlserver.result-column-comparison.v1`. |
| `sqlserver-ddl-result-comparison/1` | Wrapper binding admitted DDL result evidence and the inner report. |

Old exports, parameter receipts and `sqlserver-bindings/1` fail admission in the
new routes (version/origin checks); the new exports fail the old routes.

## Source extraction

`SqlServerReaderColumns` (Core, IOperation-based, so C# and VB share it). Supported:
receiver is a local declared from `new SqlCommand(...)` (signed System.Data.SqlClient
or Microsoft.Data.SqlClient, no initializer); constant CommandText (constructor or
assignment) and constant CommandType; `ExecuteReader()` with no arguments, directly
the initializer of a `SqlDataReader` local (plain, `using` statement or using
declaration). Every reference to the command and reader local in the whole method
is classified; any unclassified reference is an alias/escape gap. Reader members:
indexer with constant string/int, one-int-argument `Get*`/`IsDBNull` getters,
`GetOrdinal(string)`, one `Read()` that is the condition of an `if`/top-tested
`while`, `Close`/`Dispose`. `NextResult`, `RecordsAffected`, `FieldCount`, async
members and others make the reader unknown. Method-wide control flow other than
using blocks and Read()-conditioned if/while is unknown. Ordinal locals must be
declared directly from the same reader's `GetOrdinal(constant)` and never written
again; otherwise that access is `dynamic-ordinal`. Bounds: 20,000 operations,
256 accesses. Extractor versions: C# 0.22.12, VB 0.10.15.

## Admission and comparison

JSON: strict reader (`SqlServerSchemaEvidence.Read`), regenerate-and-compare
admission, exact expected repo/revision. Shape: identity, file/span, `complete` or
`unknown` with reason, ordered columns (`ordinal` = position, ASCII identifier
names, optional visible `declaredType`). Duplicates are admissible (they are legal
SQL) but comparisons on them remain unknown.

DDL: `SqlServerDdlEvidence.ProduceCore` runs the full existing parameter admission
(same parser thread, bounds, SQLCMD guard, digest and identity checks) and hands
each parsed procedure to the result analyser. Body = exactly one statement, or one
BEGIN/END block holding exactly one statement; it must be a SelectStatement whose
QuerySpecification has only aliased literal / CAST(literal AS builtin) elements and
no FROM/WHERE/GROUP/HAVING/ORDER/TOP/OFFSET/DISTINCT/FOR/INTO/CTE/hints. Other
bodies produce `unknown` shapes with explicit Tier4 unsupported records. No body
text, expressions or literal values are stored; CAST type spelling is kept as a
visible uncompared facet.

Comparison emits one row per bound access, and one call-level unknown row for any
reader without comparable accesses. Shared reason precedence mirrors the parameter
route. Report always `partial` / `no-aggregate-clean-verdict`; bounded input hash
covers only the admitted export, bindings and reader projections.

## CLI

`tracemap sqlserver-result-schema produce|compare` and
`tracemap sqlserver-ddl-result produce|compare`, mirroring the existing option
names. Outputs `result-column-comparison.json` + `report.md` via the existing
`PublishNewDirectory` (now parameterized with its required file names) and
`PublishNewFile`. DDL sources reuse the existing bounded, link-refusing reader.
