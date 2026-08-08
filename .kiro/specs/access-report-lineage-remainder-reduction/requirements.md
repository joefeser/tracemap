# Requirements

## Purpose

Reduce the remaining Microsoft Access report-lineage gaps by composing statically declared crosstab outputs into existing report and expression binding evidence. Preserve an explicit boundary between declared output identities and runtime query/report behavior.

## Requirements

1. The Access query projector SHALL inventory unique crosstab row-heading output names, aggregate aliases, and literal `PIVOT ... IN (...)` output names from bounded static SQL.
2. Every projected crosstab output SHALL retain a stable identity, ordinal, source-field candidates where uniquely resolved, coverage, rule ID, evidence tier, commit SHA, extractor version, and limitations.
3. The design-evidence composer SHALL reuse those query-output facts when resolving report control bindings and domain-expression selected/criteria fields.
4. Dynamic pivot values, duplicate output names, unresolved fields, unsupported functions, and malformed shapes SHALL remain partial or explicit gaps.
5. The implementation SHALL NOT execute a query, read rows, render a report, open a recordset, infer runtime column availability, or claim a report ran.
6. Extractor and composer versions SHALL change when this projection behavior changes.
7. Focused tests SHALL cover row headings, aggregate aliases, static pivot outputs, downstream report binding, deterministic ordering, duplicates, dynamic pivots, and non-claims.
8. The same immutable private Access snapshot used for the prior remainder register SHALL be regenerated and measured before completion.
9. The report-specific follow-up SHALL classify all remaining cases and identify which require product work, owner disposition, or stronger evidence.

