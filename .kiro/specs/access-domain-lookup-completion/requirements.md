# Requirements

## Goal

Complete the bounded static classification of the remaining Microsoft Access domain-lookup bindings without executing queries or inventing field identities.

## Requirements

1. TraceMap shall recognize a static domain return identifier as a crosstab pivot-heading candidate only when the resolved domain is a crosstab query and its existing lineage fact contains the exact role-separated pivot-column hash.
2. TraceMap shall distinguish a numeric return identifier from a uniquely declared `W`-prefixed crosstab heading/output as a prefix mismatch, not silently treat the two names as equivalent.
3. A pivot-heading or prefix-mismatch candidate shall remain partial and shall not claim that Access produced the requested column, returned a value, or executed the query.
4. When a static domain return identifier is absent from the domain output catalog but appears only in the criteria/dependency scope, TraceMap shall distinguish a unique dependency-only candidate from an ambiguous dependency-only candidate.
5. Criteria identifiers with multiple candidates shall remain explicitly ambiguous.
6. Dynamic, malformed, unresolved, and unsupported domain expressions shall retain precise gaps.
7. Raw expressions, SQL, field names that fail disclosure policy, values, and row data shall not be added to standard artifacts.
8. Outputs shall remain deterministic and use `legacy.access.binding.v1` with its documented limitations.
9. A single-table wildcard record source shall be statically complete only when the dependency is unique and the immutable base scan proves a field catalog with no field-level acquisition gap.
10. Access predicate keyword calls such as `IN (...)` shall not be classified as unresolved custom functions; referenced fields must still resolve independently.
11. Owner rebuild dispositions shall remain separate from deterministic scanner facts and shall not modify the source database.
