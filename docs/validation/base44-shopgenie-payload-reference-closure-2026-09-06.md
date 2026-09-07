# ShopGenie payload outer-kind and finite reference closure

Date: 2026-09-06

This receipt covers the `base44-evidence/0.9.0` follow-up to the source-bound
payload-shape work. It fixes the invalid `completeness=complete` plus
`outerKind=unknown` combination and follows only finite local array
construction paths. It does not infer object kind from an SDK payload position
or from a parameter name, and it does not claim cross-component callbacks or
unused mutation hooks resolved.

## Bound inputs

- TraceMap base: `0e09393d36c0d46e501e0ae32270f5946a84ecde`
- ShopGenie commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Historical producer join:
  `/tmp/shopgenie-m1-final-producer-join.json`
- Historical join self-hash:
  `acc014a6649528e5ae57e9c693b092359030dc71859b82aa3dfc0fbcfe5ab3ef`
- Historical TraceMap packet:
  `/tmp/tracemap-shopgenie-622-final-a.vIF7he/base44-evidence.json`
- Reverse packet used by that join:
  `/private/tmp/shopgenie-m1-final-2b37357-a/analysis.json`

## Exact delta

The denominator remains exactly 1,371 entity operations, including 538
mutation payloads. Total facts remain 4,261.

All 50 historical `complete|unknown|source-bounded|none` rows are now
`complete|object|source-bounded|none`, derived from executable branches or
React Query callsites. No `complete + unknown` row remains.

Of the historical 28 reverse-only obligation rows:

- five finite mutation-array `push -> hook -> map -> entity create` paths moved
  from unresolved to statically complete object evidence;
- one finite local `charges.map` path moved from partial to statically complete
  object evidence;
- two source-proven outer object rows moved from partial/unknown to
  partial/object/source-bounded with the existing Docker obligation;
- 20 remain blocked: 13 have no source-derived outer object beyond a forwarded
  callback parameter, six mutation hooks have no `.mutate*` callsite, and one
  combines an unresolved caller with computed-property and binding-escape
  evidence.

The complete payload state distribution is:

| State | Rows |
| --- | ---: |
| `complete|object|source-bounded|none` | 456 |
| `partial|object|source-bounded|obligation` | 37 |
| `unresolved|unknown|unresolved|none` | 19 |
| `partial|object|unresolved|none` | 23 |
| `partial|unknown|unresolved|none` | 3 |

Producer Tier-4 gaps changed from 89 to 83: 82 `entity` and one
`http-integration`. Thirty-seven rows retain the exact runtime obligation
`entity-open-object-fields:docker-write-readback-cleanup`; they remain
Tier4/partial, not static success.

## Deterministic replay

- Packet A: `/tmp/tracemap-shopgenie-622-v09-final-a.2MF4OQ`
- Packet B: `/tmp/tracemap-shopgenie-622-v09-final-b.Xp5QmN`
- byte-identical `facts.ndjson` SHA-256:
  `e4f34fd503e65a00cced27d22293db6933059951880b3bf14e77f8ecd219367f`
- ordered `coverage.gaps` JSON SHA-256:
  `f0045622e4f03da929668f4413013225c15bdb3215e817d4c4968094c74a4633`
- packet A SHA-256:
  `9a3212db6dd592a88a644fc9c32993e2791ad0f7fde4fcba3f97d8661beb247d`
- packet B SHA-256:
  `9c8c2472a51bed4d5d02ec933f72cdb986bba0b0906456460847df513006d52c`

Packet file hashes differ only because run identity and timestamp metadata are
different. Facts and coverage-gap identities are deterministic.

## Verification

```bash
npm run check --prefix src/typescript
python3 scripts/test_validate_adapter_artifacts.py
TRACEMAP_SKIP_BUILD=1 \
  TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock \
  ./scripts/smoke-open-source-repos.sh
./scripts/check-private-paths.sh
git diff --check

node src/typescript/dist/src/cli.js base44-evidence \
  --repo <exact-ShopGenie-origin-main-checkout> \
  --out <disposable-output> \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label canonical-shopgenie-origin-main-syntax \
  --no-semantic
python3 scripts/validate-adapter-artifacts.py <disposable-output>
```

Adversarial tests cover conflicting object/array callsites, open and spread
array pushes, array escape, map-parameter escape, packet tampering to restore
the forbidden complete/unknown state, and deterministic positive replay.
