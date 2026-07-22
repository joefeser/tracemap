# Access Design-Review Composition Design

## Decision

Add a dedicated `AccessEvidence` section to the existing release-review
document. This is a read-side composition over already persisted facts, not a
new Access report command and not an extractor change.

## Read model

`ReleaseReviewReporter` reads Access rows from `facts` or `combined_facts` with
their manifest/source row. It reconstructs only the existing `CodeFact`
provenance needed by the projection and groups rows by safe source label.

The read gate accepts only cataloged `legacy.access.*` rules. A row must retain
extractor ID and version for the section to become `available`.

## Projection

The after-snapshot section emits bounded findings for:

- database inventory and count-only UI/VBA/macro coverage;
- tables, fields, indexes, and declared relationships;
- saved-query categories and static dependency candidates;
- external-boundary categories.

Metadata is an explicit allowlist. Role-scoped `access-*` stable design keys may
be rendered so tables, fields, relationships, and query dependencies remain
correlatable without names. SQL/query hashes, external source hashes, raw names,
and generic fact property bags are not rendered.
`AnalysisGap` rows become structured `ReleaseReviewGap` entries with upstream
rule/tier/span/provenance and safe classifications.

Item-level UI/VBA/event/navigation/macro facts are not projected as supported
design findings. If present, the section records a bounded unsupported-evidence
gap because the shipped product reader is count-only for those capabilities.

## Status behavior

- `not_requested`: `access-evidence` is outside the selected scope.
- `deferred`: no compatible Access evidence is present in the after snapshot.
- `unavailable`: Access rows exist but required extractor provenance is absent.
- `available`: compatible findings and/or structured gaps are projected.
- `truncated`: the shared release-review finding/gap caps omit rows.

## Compatibility

The release-review JSON version advances from `1.0` to `1.1` because it adds a
first-class section. Existing fields and status vocabularies remain unchanged.
Review-priority scoring consumes the new section through the same finding and
gap contracts as every other release-review section.

## Non-claims

The composition does not read an Access database, execute a query, inspect row
values, open/render UI, read VBA source, inspect macro bodies, establish runtime
reachability, prove permissions/connectivity, approve a release, or certify a
migration as safe.
