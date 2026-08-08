# Requirements

## Goal

Improve Microsoft Access DLookup-family lineage by reconciling protected static domain, return-field, criteria-field, and owning-control references against already-acquired deterministic catalogs.

## Requirements

1. A DLookup-family domain must resolve to exactly one declared query or table before any field reconciliation occurs.
2. A return field must resolve only to a declared field/output of that domain; dependency fields must not be treated as returned outputs.
3. A criteria field may resolve to a declared domain field/output or, when absent there, to exactly one field exposed by a direct declared query dependency.
4. Duplicate dependency-scoped names, absent names, dynamic domains, and unsupported expressions must remain explicit partial gaps.
5. Reconciliation must reuse existing protected design expressions and base-scan facts; it must not read rows, execute queries, widen Access COM, or persist raw expressions.
6. Static lineage coverage must remain separate from runtime-value coverage.
7. Output must remain deterministic and preserve the existing rule ID, evidence tier, provenance, commit SHA, extractor version, limitations, and supporting fact semantics.

## Non-claims

- No DLookup execution or result.
- No predicate truth or selected-row proof.
- No runtime reachability or branch execution.
- No database correctness, rebuild completeness, or production state.
