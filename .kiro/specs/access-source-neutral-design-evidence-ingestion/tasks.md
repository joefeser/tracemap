# Source-Neutral Access Design-Evidence Ingestion Tasks

## Specification slice (#550)

- [x] Verify issues #549 through #552 against active TraceMap specifications
      and completed Windows validation issues #488 through #491.
- [x] Inventory existing pure UI, VBA, event, navigation, and macro projectors.
- [x] Inventory fact contracts, rules, tiers, limits, tests, downstream
      consumers, and intentionally disconnected production paths.
- [x] Define the versioned manifest and strict NDJSON record vocabulary.
- [x] Define repository, commit, base-scan, database-copy, producer, bundle,
      record, coordinate, and completeness provenance.
- [x] Define hash-only identity disclosure and the narrow future projector/fact
      contract changes it requires.
- [x] Define transient protected input and standard-artifact persistence
      boundaries.
- [x] Define deterministic identity, ordering, duplicate, mismatch, and limit
      behavior.
- [x] Define tiers, coverage labels, rule reuse, required gaps, and non-claims.
- [x] Compare owner-controlled export mechanisms without selecting or
      implementing one.
- [x] Define Mac-only synthetic validation and leak-test requirements.
- [x] Record dependencies on #551 and #552 without specifying their flow or
      copy/clone implementations.
- [ ] Complete independent adversarial specification review and disposition all
      P1/P2 findings.
- [ ] Obtain owner decisions recorded in `implementation-state.md`.
- [ ] Approve or revise the specification for implementation.

## Future implementation — not authorized by this PR

- [ ] Add strict manifest/NDJSON models and bounded streaming validation.
- [ ] Add explicit CLI input and bind it to the matched base Access scan.
- [ ] Add hash-only identity disclosure for source-neutral projection.
- [ ] Add validated optional source-coordinate provenance.
- [ ] Normalize supported records into existing pure projectors.
- [ ] Add ingestion provenance rules/gaps and update existing rule-catalog
      possible tiers where required.
- [ ] Preserve provenance through facts, indexes, combine, reports, evidence
      docs, vault, release review, and local review bundle.
- [ ] Add synthetic determinism, mismatch, gap, cap, and protected-marker tests.
- [ ] Run focused/full Mac validation.
- [ ] Propose a separate threat-reviewed Windows exporter only if the owner
      selects a candidate mechanism.
