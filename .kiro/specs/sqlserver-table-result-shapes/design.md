# Design — table-backed DDL result shapes

## Contracts (new, versioned, additive)

| Contract | Purpose |
| --- | --- |
| `sqlserver-ddl-table-result-input/1` | Manifest; same fields and file/identity policy as `sqlserver-ddl-input/1`. |
| `sqlserver-ddl-table-result-schema/1` | Export: input, parser/generator/input SHA-256, `tables`, `declarations`, `projections`, `tableUnsupported`, `unsupported`. Extractor `sqlserver-ddl-table-result-shapes/1`, rule `database.sqlserver.ddl-table-result-shape.v1`, Tier2 complete / Tier4 unsupported. |
| `sqlserver-ddl-table-result-declarations/1` | Shape projection, origin `public-synthetic-tsql-ddl-table-result`; consumed by the unchanged comparison engine. |
| `sqlserver-ddl-table-result-comparison/1` | Wrapper binding admitted evidence and inner `sqlserver-ddl-table-result-column-comparison/1`. |

CLI: `tracemap sqlserver-ddl-table-result produce|compare`, same options as `sqlserver-ddl-result`.
Bindings stay `sqlserver-result-bindings/1`; source facts stay `sqlserver-reader/1` (no
extractor change or version bump).

## Admission core reuse

`SqlServerDdlEvidence.ProduceCore` gains an optional table visitor. When it is null (both
existing routes) `CREATE TABLE` still rejects with `top-level-statement-unsupported`. When set
(only the new route): two-part identifier-vocabulary name required (else whole-input
`explicit-bounded-schema-required`, so `#temp`, one- and three-part names reject), identity
added to the shared case-insensitive procedure/table set (`ambiguous-object`), at most 256
tables. Every other whole-input guard (strict UTF-8, digests, SQLCMD, size/token/complexity
bounds, 64 MiB parser thread, parameter vocabulary, malformed SQL, ALTER TABLE / CREATE VIEW /
other statements) is unchanged. Bodies and tables are analysed after all files are read.

## Accepted table grammar

`CREATE TABLE schema.name ( column [, column | table-constraint]* )` where

- column: identifier-vocabulary name, built-in `SqlDataTypeReference` with a one-part name
  and at most two integer/MAX facets, optional `NULL`/`NOT NULL`, optional `IDENTITY(...)`,
  optional column `PRIMARY KEY`/`UNIQUE` without index options or filegroup;
- table-constraint: `PRIMARY KEY`/`UNIQUE` without index options or filegroup.

Anything else makes the table `unknown` (no columns, Tier4 record): table options/filegroups/
TEXTIMAGE/FILESTREAM/node/edge/file tables/inline indexes/periods (`table-option-unsupported`),
CHECK/FOREIGN KEY/DEFAULT table constraints (`table-constraint-unsupported`), computed columns,
DEFAULT, COLLATE, alias/CLR/xml types, column CHECK/FK, ROWGUIDCOL/SPARSE/FILESTREAM/inline
index/GENERATED/HIDDEN/encryption/masking (`computed-column-unsupported`,
`column-default-unsupported`, `column-collation-unsupported`, `column-type-unsupported`,
`column-constraint-unsupported`, `column-option-unsupported`), non-vocabulary names,
duplicate or case-only duplicate column names, more than 1,024 columns.

## Accepted procedure grammar

Body unwrap and statement checks are the existing ones. Constant SELECTs without FROM keep the
existing analysis. With FROM, in this order (first failure wins, shape unknown, no columns):

1. CTE → `cte-unsupported`; INTO/ON/COMPUTE/hints → `select-clause-unsupported`; UNION etc.
   → `set-operation-unsupported`; non-plain query → `query-expression-unsupported`; `*`/`t.*`
   → `select-star-unsupported`; assignment → `select-assignment-unsupported`.
2. WHERE/GROUP/HAVING/ORDER/TOP/OFFSET/FOR/WINDOW/DISTINCT → `select-clause-unsupported`.
3. FROM: more than one source → `multiple-table-sources-unsupported`; joins/APPLY →
   `join-unsupported`; derived tables → `subquery-unsupported`; table variables →
   `table-variable-unsupported`; functions → `table-valued-function-unsupported`; other
   references → `table-reference-unsupported`; hints → `table-hint-unsupported`; sampling,
   temporal or graph paths → `table-reference-unsupported`.
4. Name: `#`/`##` → `temporary-table-unsupported`; server or database part →
   `cross-database-or-server-reference-unsupported`; no schema → `table-reference-schema-required`;
   non-vocabulary names/alias → `table-reference-outside-identifier-vocabulary`.
5. Resolution in the admitted table set: exact ordinal schema+name → table; case-only match →
   `table-reference-case-policy-unknown`; none (including views and procedures) →
   `table-dependency-missing`; unknown table → `table-definition-unsupported`.
6. Each element: `SelectScalarExpression` whose expression is a regular
   `ColumnReferenceExpression` without COLLATE (scalar subquery → `subquery-unsupported`,
   others → `non-column-projection-unsupported`). Output alias optional (`AS x`, `x = col`,
   `col x`); string alias → `string-alias-unsupported`; non-vocabulary alias →
   `alias-outside-identifier-vocabulary`. Qualifier: none, or exactly the declared alias.
   Qualifier without declared alias → `column-qualifier-without-alias-unsupported`; case-only
   alias difference → `column-qualifier-case-policy-unknown`; other → `column-qualifier-unbound`;
   three or more parts → `column-qualifier-unsupported`. Column: exact ordinal match; case-only
   → `column-case-policy-unknown`; absent → `column-dependency-missing`.
7. Output names (alias or column name) unique case-insensitively, else
   `duplicate-or-case-only-output-name-unsupported`; 1–1,024 columns (`column-limit`).

A complete shape lists output names in projection order, the select element line, and the
table column's declared type spelling as a visible uncompared facet; `projections` records
procedure, table identity and per-output source column/line. A `column-type-facets-not-compared`
Tier4 record accompanies every complete shape. No body text, alias of the table or literal
value is stored.

## Comparison and outputs

`SqlServerResultEvidence.CompareAdmitted` is reused unchanged (validation is parameterized by
version/origin only). The wrapper binds the admitted export and inner report; inner bounded
input = digest(export, inner input hash), outer = digest(export, comparison). Markdown lists
tables, shapes, projections and unsupported records before the shared comparison table.

## Tests and smoke

`SqlServerDdlTableResultEvidenceTests` (grammar positives/negatives, whole-input refusals,
version/origin/substitution, native fixture replay, CLI). New smoke
`scripts/sqlserver-table-result-evidence/smoke.py` reuses the result smoke helpers without
changing that smoke's bytes, so retained receipts and denominators stay comparable.
