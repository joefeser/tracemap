# Report-Specific Pass

## Measured starting point

The same-snapshot regeneration leaves 93 report binding remainders. They are concentrated rather than random:

| Family | Cases | Next deterministic pass |
| --- | ---: | --- |
| Crosstab output candidates | 52 | Reconcile declared pivot/static columns with report control sources while retaining generated-column limitations. |
| Expression partial | 14 | Classify remaining identifier roles and preserve unresolved hashes when no unique catalog match exists. |
| Domain field catalog incomplete | 12 | Recover the bounded domain-query output catalog and compare selected/criteria roles independently. |
| Inline SQL output unmatched | 6 | Compare report controls with the static SELECT output catalog; do not infer aliases. |
| Record source ambiguous | 5 | Resolve only when one declared table/query identity is uniquely supported. |
| Expression function unresolved | 2 | Compare against the protected VBA procedure catalog; retain ambiguity or missing-procedure gaps. |
| Inline SQL projection partial | 1 | Identify the unsupported projection shape and add a bounded parser fixture if generalizable. |
| Target ambiguous | 1 | Preserve the ambiguity unless one candidate can be excluded by scoped evidence. |

## Sequence

1. Handle the 12 domain-catalog cases as one query-family cluster.
2. Handle the seven inline-SQL cases as one report-family cluster.
3. Reconcile the two unresolved functions against the protected procedure catalog.
4. Treat the 52 crosstab candidates as a separate generated-column runway; do not force them into ordinary field bindings.
5. Leave ambiguous record sources and targets as explicit owner questions unless deterministic scope removes a candidate.

## Boundaries

- A declared report binding does not prove that the report ran or rendered.
- A crosstab candidate does not prove the runtime-generated column set.
- A DLookup query and selected-field reference do not prove a returned value.
- No ambiguous candidate is selected by naming similarity alone.
