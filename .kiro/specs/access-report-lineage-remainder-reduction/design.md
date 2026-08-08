# Design

## Static crosstab output catalog

`AccessQueryProjector` derives a bounded output catalog from the already-read QueryDef SQL:

- `SELECT` projections contribute row-heading output names and source fields.
- an explicit `TRANSFORM ... AS alias` contributes the aggregate alias.
- literal values in `PIVOT ... IN (...)` contribute statically declared pivot output names.

Only unique output names are emitted. The catalog retains declaration order and does not evaluate expressions. Dynamic pivot values have no invented output identities.

## Fact composition

`AccessComReader` projects catalog entries as existing `AccessQueryOutputDeclared` facts. Direct report controls and domain expressions therefore reuse the same query-output lookup already used for ordinary SELECT queries. Source-field candidates use `AccessQueryOutputSourceCandidate`; unresolved source lineage remains partial.

This avoids a crosstab-specific report-binding data model and keeps the seam source-neutral after the Windows scan.

## Coverage

An output name may be statically declared even when its runtime value is not known. Binding coverage describes the declared target identity. Query-output coverage describes source-lineage completeness. Existing crosstab and query gaps continue to represent unsupported or dynamic query shape.

## Non-claims

- no query or report execution;
- no row access or returned-value proof;
- no proof that a dynamic pivot generated a column;
- no proof that a report rendered or was used;
- no business-intent or migration-approval conclusion.

