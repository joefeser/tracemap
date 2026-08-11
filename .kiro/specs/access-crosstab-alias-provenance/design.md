# Design

## Existing seam

`AccessComReader` already reads bounded QueryDef text and projects crosstab
outputs into `AccessQueryOutputFieldProjection`. `AccessFactBuilder` emits the
existing `AccessQueryOutputDeclared` and `AccessQueryOutputSourceCandidate`
facts under `legacy.access.query.v1`. This slice extends those records rather
than adding a report-specific contract.

## Provenance fields

Each output may carry:

- `outputKind`: `row-heading` or `static-pivot` for crosstab catalog entries;
- `aliasKind`: `explicit-as`, `access-colon`, `direct-field`, `pivot-literal`,
  or `unknown`;
- `sourceExpressionHash`: a role-scoped hash of the masked source expression
  (or the crosstab pivot expression for a static pivot output);
- `pivotSourceFieldStableKeys`: fields resolved from the pivot expression,
  separate from fields used by the aggregate/value expression.

The crosstab lineage projection also records aggregate and pivot source-field
candidate lists. This makes `pivot literal [1] -> pivot expression ->
WeekNumber` inspectable while leaving the report's `W1` control as an
independent output identity.

## Coverage and safety

Coverage remains partial for dynamic or ambiguous shapes. Hashes are not
reversible source storage and are role-separated from output-name and pivot
literal hashes. No output-name similarity or ordinal adjacency creates an
alias equivalence. Runtime query columns, report execution, row values, and
Access UI state remain unproven.

## Deferred work

The private Windows evidence still needs a same-snapshot rerun if exact DAO
QueryDef properties are required. Dynamic pivot behavior, DCount/query
reconciliation, and report layout reconstruction remain separate follow-ups.
