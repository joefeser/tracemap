# Requirements

## Purpose

Turn the remaining representative Access expression and binding gaps into either deterministic lineage or precise, conversation-ready classifications. Zero gaps are not required; every retained gap must explain what evidence is missing and what human confirmation could add.

## Requirements

1. The implementation shall account for all 32 `AccessBindingExpressionPartial` and 98 `AccessBindingTargetUnresolved` representative cases by deterministic root-cause class.
2. Controls whose owning surface uses a crosstab with no static output catalog shall preserve a surface-declared output candidate without claiming the crosstab emitted that column at runtime.
3. Controls behind bounded inline SQL record or row sources shall reuse static SQL projection where possible and preserve unresolved output candidates separately from database field conclusions.
4. Domain expressions shall distinguish a resolved domain object with an incomplete field catalog from an unknown domain object or dynamic expression.
5. Ambiguous, stale, misspelled, externally supplied, row-derived, or dynamically constructed references shall remain explicit rule-backed gaps.
6. A private remainder register shall retain the owning surface/control identity, binding role, known lineage, root-cause classification, and smallest useful human question.
7. Human annotations shall remain separate from deterministic TraceMap facts and shall not silently upgrade evidence tiers.
8. Standard facts and public artifacts shall not contain raw expressions, customer identifiers, source values, or private paths.
9. Results shall be deterministic across input ordering and repeated representative runs.

## Non-claims

- No query, expression, form, report, macro, or VBA execution.
- No row-derived discovery of crosstab columns.
- No proof of runtime reachability, branch selection, mutation success, or data correctness.
- No claim that a surface-declared output candidate is emitted by its source query.
- No widening of the Access COM boundary.
