# Design

## Existing seam

`AccessExpressionProjector` already separates domain query, selected-field, criteria-field, control, and external-reference evidence. `AccessDesignEvidenceComposer` already builds clear-name and role-hash field catalogs from the immutable base scan and protected design bundle.

## Reconciliation

1. Discover only syntactically static DLookup-family domain and field names from protected expressions.
2. Reveal an existing hash-only domain output name in memory only when the domain and role-separated output hash each match exactly once and no design-catalog field already owns that clear name.
3. Build a criteria-only field scope per query from:
   - its own declared outputs; and
   - fields/outputs of its direct declared query/table dependencies, only for names absent from its own outputs.
4. Keep return-field resolution on the original domain output scope. Pass the wider scope only to criteria-field resolution.
5. Emit precise partial classifications for unmatched return fields, unmatched criteria fields, and ambiguous criteria fields.

Direct output names take precedence over dependency-scoped names. Dependency candidates are sorted and de-duplicated. A dependency name with more than one stable-key candidate is not selected.

## Privacy and determinism

Raw expressions remain transient protected input. Persisted output contains stable keys, role-separated hashes, lengths, classifications, coverage, and provenance only. All maps and emitted candidates use ordinal deterministic ordering.

## Limitations

Dependency-scoped criteria matching is static name evidence. It does not prove Access accepted or evaluated the predicate. Dynamic SQL/domain construction, aliases unavailable from acquired metadata, and duplicate names remain partial.
