# Design

## Baseline census

The retained same-snapshot representative packet contains 32 partial expressions and 98 unresolved binding targets after PR #578.

- 55 unresolved targets are controls on surfaces backed by partial crosstab queries with no declared output catalog.
- 30 unresolved targets are concentrated behind expression/inline record sources.
- Five are behind ambiguous record-source identifiers, three have no record source, and the small remainder use query sources without an output catalog.
- Nineteen partial expressions are domain lookups whose domain object resolves but whose selected and criteria field catalogs are incomplete.
- The remaining partial expressions are bounded row-source, record-source, control-source, and filter shapes.

These counts are private representative evidence, not universal product guarantees.

## Resolution model

Add specific classifications instead of converting weak candidates into complete field bindings:

1. `surface-declared-crosstab-output-candidate` records that a control names an expected output beneath a known crosstab source. It remains partial and does not imply that row-derived pivot output exists.
2. `inline-source-output-candidate` records a bounded output named by an inline SQL source when static projection can establish its scope.
3. `domain-field-catalog-incomplete` records a resolved domain object whose selected or criteria identifier cannot be matched to an available output catalog.
4. Existing ambiguity, dynamic, missing-record-source, and unsupported classifications remain explicit.

Stable candidate identities derive from the database identity, owning source stable key or protected source hash, role, and normalized identifier. They never derive from input ordering.

## Private review register

Generate a private machine-readable register outside the repository from the enriched packet. Each row contains a stable case ID, actual private surface/control name where available, binding role, deterministic evidence class, known source/query candidates, gap reason, and a bounded human question. The register is a review projection, not a standard scan fact and not public evidence.

Human answers may be stored as separate annotations with author, timestamp, and disposition. They do not alter scanner facts unless later corroborated by deterministic source evidence.

## Limitations

Static surface declarations can identify expected crosstab outputs but cannot prove generated columns without row evidence. Inline SQL parsing remains bounded. Dynamic SQL, aliases constructed at runtime, and stale identifiers remain gaps.
