# Requirements

## Purpose

Record the source and naming provenance of statically recoverable Access query
outputs so report expressions such as `DCount("[1]", "[qrptSAsByWeek]", ...)`
can be compared with crosstab declarations without pretending that `[1]` is an
alias for `W1` or that the query executed.

## Requirements

1. The bounded Access SQL projector SHALL classify a recoverable output as an
   explicit `AS` alias, Access colon alias, direct field projection, or literal
   crosstab pivot column.
2. Output facts SHALL preserve role-scoped hashes for the source expression and
   separate source-field candidates for aggregate/output expressions and the
   pivot expression where those fields resolve uniquely.
3. A literal crosstab column SHALL be labeled `pivot-literal`; its name SHALL
   remain a declared column candidate and SHALL NOT be normalized to another
   output name.
4. Existing `AccessQueryOutputDeclared` and
   `AccessQueryOutputSourceCandidate` facts SHALL remain the composition seam;
   no parallel report-only fact model SHALL be introduced.
5. Dynamic pivots, malformed projections, unsupported expressions, duplicate
   names, and unresolved fields SHALL remain partial or emit existing query
   gaps.
6. The implementation SHALL not execute queries, open recordsets, read rows,
   render reports, inspect runtime column availability, or widen the Access COM
   boundary.
7. Raw SQL, source expressions, literal values, connection material, and local
   paths SHALL not be persisted in the new fields; only role-scoped hashes and
   safe stable keys may be emitted.
8. Focused tests SHALL prove alias classification, pivot-expression source
   linkage, deterministic hashes, fact preservation, and the non-equivalence
   boundary.
