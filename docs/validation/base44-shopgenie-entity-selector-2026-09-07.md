# ShopGenie entity selector coverage

Date: 2026-09-07

This receipt covers extractor `base44-evidence/0.12.0`, derived independently
from executable ShopGenie source before producer comparison. Reverse-engineer
output was not used as extraction authority.

## Bound inputs

- TraceMap predecessor: `2257a24fc232e93f496484f5f3d4a4a2305f7ef8`
- ShopGenie repository: `<exact-ShopGenie-origin-main-checkout>`
- ShopGenie commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`

## Exact producer result

The extractor now observes 1,409 unique entity SDK source calls. The operation
denominator is 1,720 rows because finite computed selectors create one row for
each source-proven entity candidate:

| Selector kind | Source calls | Operation rows |
| --- | ---: | ---: |
| Static member | 1,371 | 1,371 |
| Finite source domain | 15 | 326 |
| Unresolved computed selector | 23 | 23 |
| Total | 1,409 | 1,720 |

The previous 1,371 denominator omitted all 38 computed/aliased source calls.
This slice exposes them instead of interpreting absence as compatibility.

The packet contains 5,308 facts: 542 payloads and 1,178 queries. It contains
176 producer-owned coverage gaps: 175 entity and one HTTP/integration. The
increase from 69 gaps is an honest consequence of exposing previously missing
surfaces and retaining incomplete shapes; it is not a readiness improvement.

All 38 prior runtime-deferred open-object obligations remain present. The 23
unresolved selector rows include dormant helpers with no executable callers,
React/state-mediated blocker collections, mutable entity-client aliases, and
one user-selectable dynamic-import client. No literal subset is admitted when
another source branch is runtime-open.

The finite domains come from executable arrays/maps and their loop bindings,
closed helper callsites, the exact relationship map and its finite callers,
and a terminating one-entity Set allowlist. Query field correlation remains a
separate obligation: 22 relationship-map filter rows retain
`dynamic-computed-property` and `query:field_or_composition_unresolved` rather
than treating a finite entity selector as a complete query shape.

## Deterministic replay

- Packet A: `/tmp/tracemap-shopgenie-selector-v012-final2-a.Bhocdb`
- Packet B: `/tmp/tracemap-shopgenie-selector-v012-final2-b.9VDBtv`
- byte-identical `facts.ndjson` SHA-256:
  `8c9d2887b807ce6a7d3bd174bf9ba304390f7a49f2d0672a0b54c03569a37694`
- ordered `coverage.gaps` JSON SHA-256:
  `4486838ad957ef145f486cc6d6292c4a37eebc778d6b1bf49976fb2a14e2ec45`
- packet A SHA-256:
  `66f41d21db4b5e95c7eb960cc56dd84fec6f30b2e3a5ed5a9d611fd1816afdfa`
- packet B SHA-256:
  `0894ae75a6d2000a5fe048103a286b54e5659b9b110ea2ec5408e32175e97b19`

Packet hashes differ only in run metadata. Facts and coverage gaps are
deterministic.

## Verification

```bash
npm run check --prefix src/typescript
python3 scripts/test_validate_adapter_artifacts.py
python3 scripts/validate-adapter-artifacts.py \
  /tmp/tracemap-shopgenie-selector-v012-final2-a.Bhocdb
python3 scripts/validate-adapter-artifacts.py \
  /tmp/tracemap-shopgenie-selector-v012-final2-b.9VDBtv
./scripts/check-private-paths.sh
git diff --check
```

The TypeScript suite passed 188/188. Adversarial cases cover mutated and
escaped arrays, runtime-open caller branches, non-terminating and mutated Set
guards, duplicate candidates, forged source authority, and deterministic
finite arrays/maps/caller arguments.
