# Design

## Existing seam

`AccessExpressionProjector` already separates domain return fields from criteria fields. `AccessDesignEvidenceComposer` already has the base query catalog, crosstab lineage facts, and protected design expressions at composition time.

## Completion behavior

- Build a per-query set of declared static pivot-column hashes from `AccessQueryCrosstabLineageCandidate` facts whose query kind is consistently `crosstab`.
- During protected expression reconciliation, compare a static selected identifier using the existing `access-query-pivot-column` role hash.
- On one exact match, add a hash-only `query-pivot-column-candidate` identity to the domain return scope. The projector recognizes that identity as partial evidence and emits `AccessBindingDomainCrosstabPivotCandidate`.
- For a numeric requested identifier, compare `W<identifier>` against both the declared pivot hash and the unique output catalog. Preserve a hash-only mismatch candidate and emit `AccessBindingDomainCrosstabPivotPrefixMismatch`; do not equate the names.
- If normal return-field resolution fails, inspect the already-bounded criteria/dependency scope only to classify the absence. Emit `AccessBindingDomainSelectedFieldDependencyOnly` for one candidate or `AccessBindingDomainSelectedFieldDependencyAmbiguous` for multiple candidates. Do not promote either to a selected output identity.

## Full-register follow-up

- Derive a set of table identities whose base field catalogs are present and have no field-scoped acquisition gaps. Only a unique table wildcard in that set can complete an inline record-source binding. Keep query wildcards and any incomplete catalog partial; do not fabricate projection order.
- Remove Access predicate operators such as `IN` and `EXISTS` from the custom-function set and retain them as operator hashes. Continue resolving every nonliteral operand normally.
- Keep private owner dispositions in the review workbook. They affect rebuild scope, not source evidence or scanner output.

## Review hardening

- Replace an ordinary declared output in the working reconciliation catalog with a hash-only candidate when the same identifier is a statically declared crosstab pivot heading. This keeps exact pivot lookups partial instead of allowing the ordinary output identity to imply materialization.
- Require both the static `W<identifier>` pivot hash and one declared query-output name-hash match before emitting a prefix-mismatch candidate. Synthetic design fields cannot satisfy this proof.
- Classify each domain lookup against a per-call selected-field set, then aggregate outputs. A truly unmatched or dependency-only/ambiguous return gap takes precedence over a pivot candidate from another lookup.
- Treat truncated or malformed field catalogs as incomplete before completing a table wildcard. Preserve bracketed field names that collide with predicate keywords, and keep malformed `IN`/`EXISTS` operands partial.
- Preserve `expressionGapClassification` in release-review binding metadata so dependency-only and dependency-ambiguous evidence remains distinguishable downstream.

## Output-source alias follow-up

- Reuse the bounded SELECT projection parser against SQL already acquired by the Access reader. A direct field projection with a static alias and exactly one resolved source field may supplement missing DAO `SourceTable`/`SourceField` metadata; this performs no additional COM read and opens no recordset.
- Emit the recovered source as the existing `AccessQueryOutputSourceCandidate` fact. If DAO and static candidates disagree, preserve all candidates and partial coverage.
- At design composition time, use one output-to-source candidate to resolve a criteria identifier to its source field. If a DLookup return names that source while the query declares a different output alias, emit a hash-only alias-mismatch candidate and `AccessBindingDomainSelectedFieldAliasMismatch` rather than promoting it to the declared output.
- Multiple matching outputs or multiple sources remain ambiguous. The twelve numeric-versus-`W` crosstab cases remain source-design contradictions and are not affected by alias reconciliation.

## Non-claims

This slice does not prove query execution, pivot-column availability, returned values, predicate outcomes, runtime reachability, business intent, or correctness. It adds no COM reads and does not retain raw SQL/expressions.
