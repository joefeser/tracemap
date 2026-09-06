# Base44 runtime-deferred query replay

This candidate replay validates `base44-evidence/0.5.0` and
`88mph.entity-query.v2` against canonical ShopGenie `origin/main` commit
`6753cfa6264b04c0d7dbfcde942689ee32d2c06b`, accepted source/tree SHA-256
`a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`.

## Result

- 4,261 Base44 facts and 1,371 entity-operation keys are preserved.
- All 734 query facts carry v2 descriptors.
- 733 descriptors are complete; one root-bound filter remains unresolved with
  `query:filter_construction_unresolved`.
- The 650 former `query:implicit_predicate_type_unresolved` rows are now
  complete runtime-deferred references.
- Independent comparison with the reverse-engineer candidate found 734/734
  matching query keys and zero descriptor differences.
- A second TraceMap run produced byte-identical `facts.ndjson`; per-run scan
  manifest and SQLite identities intentionally differ.
- Candidate `facts.ndjson` SHA-256:
  `9402ae4c5993f4664a522175acb68718939026e83be6edcec29b88650402b855`.

## Validation

- TypeScript build and 122 tests passed.
- The unchanged .NET reader/build surface passed all 1,732 tests.
- Seven adapter-artifact validator tests and the private-path guard passed.
- Pinned TypeScript OSS smokes completed with unchanged measured outputs:
  scip-typescript emitted 14,060 facts and 33 explicitly reduced-coverage
  gaps; axios-npm-lock emitted 11,186 facts and 56 explicitly reduced-coverage
  gaps.

## Boundary

The extractor records only reference spans, not expression text, runtime
values, inferred types, or customer payloads. Root bindings, spreads,
computed/duplicate keys, logical composition, unsupported expressions,
dynamic controls, unsupported operators, and traversal overflow remain typed
gaps.

This is candidate static evidence. It does not admit the producer, change the
host gate, prove backend operator compatibility, publish a denominator, or
authorize deployment.
