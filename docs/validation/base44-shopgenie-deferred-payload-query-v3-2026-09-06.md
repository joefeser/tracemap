# ShopGenie deferred payload and query-v3 validation

Date: 2026-09-06

This receipt covers the second bounded TraceMap producer slice for host issue
`#622`, built after local producer-gap commit
`a7335d3e515fd3d6f9300673aadc626b5e188d33`. It classifies source-bounded
open-object payloads without inventing their fields and adds conditional filter
presence that query v2 could not express. It does not publish the Stage 6
denominator, close runtime obligations, or make a compatibility/release claim.

## Identities

- TraceMap branch: `codex/shopgenie-m1-completion`
- TraceMap exact base: `8df82f1c13239ebe985fa9fa71141e14da822c3f`
- Source repository: `BigRiverMachine/ShopGenie`
- Source commit: exact `origin/main`
  `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Extractor: `base44-evidence/0.8.0`
- Payload shape: `2`
- New query descriptor: `88mph.entity-query.v3`

## Exact producer delta

Both the pre-slice and post-slice scans contain 4,261 facts, including exactly
1,371 entity-operation facts, 538 payload facts, and 833 query facts. The
declared-surface denominator therefore changes by **0**.

The pre-slice 0.7 packet had 90 producer-owned Tier-4 gaps. The final 0.8
packet has 89: 88 `entity` and one `http-integration`. The only Tier-4 row that
moved to static completeness is the single filter-construction row. Thirty-
three open-object payloads remain Tier4/partial blockers but now carry precise
runtime-deferred evidence instead of an undifferentiated initializer gap.

| Payload/query reason | Before | After |
| --- | ---: | ---: |
| `spread:binding-initializer-unresolved` | 37 | 5 |
| `binding-initializer-unresolved` | 19 | 19 |
| `spread:spread:binding-initializer-unresolved` | 1 | 1 |
| `destructured-binding-unresolved` | 3 | 2 |
| `spread:destructured-binding-unresolved` | 1 | 1 |
| `query:filter_construction_unresolved` | 1 | 0 |
| `runtime-deferred-object-fields` | 0 | 33 |

The 19 direct opaque parameters retain `outerKind=unknown` and
`referenceAccounting=unresolved`; no type or fields were guessed. Five spread
initializer cases and the remaining destructured/double-spread cases retain
their original blocker because their outer kind or reference graph is not
source-proven, or because mutation/escape/cross-scope evidence conflicts.

The 33 deferred rows are exactly object/source-bounded/partial/Tier4 and carry
only the obligation
`entity-open-object-fields:docker-write-readback-cleanup`. The host may admit
that obligation only with the exact SDK open-object transport and later
isolated local-Docker write/readback/tenant/cleanup proof.

One real example is `OutsideServices.jsx:152` (`OutsideService.create`). The
object-rest projection statically removes `priceBreaks`, retains six observed
field occurrences, and emits these exact contract properties:

```json
{
  "shapeVersion": "2",
  "outerKind": "object",
  "referenceAccounting": "source-bounded",
  "completeness": "partial",
  "analysisGapsJson": "[\"runtime-deferred-object-fields\"]",
  "runtimeObligationsJson": "[\"entity-open-object-fields:docker-write-readback-cleanup\"]"
}
```

## Query-v3 result

`QuickToolingPOSheet.jsx:67` now emits one complete
`88mph.entity-query.v3` descriptor. Its entries are
`tooling_item_id=always`, `organization_id=always`, and
`print_id=conditional`; the latter is bound to the exact conditional assignment
span. Its fact ID is `fact-b66f9bb00c7764764643`, normalized
`querySemanticsJson` SHA-256 is
`4efae033e7473b3af2dbdfdce525b16ce8aecc324364ec92ada7135b8be50772`,
and independent `fieldsJson` SHA-256 is
`36f3f708a28d3bbee7de4aa068aaa97a40b3da2ac6abbf57a8b77fa9a2da6ff8`.

The descriptor stays host-blocked until the host explicitly validates v3 and
normalizes both independent producers. Direct object literals continue to emit
v2. Mutation, aliasing, compound/repeated writes, deletion, method calls,
nested captures, computed fields, and lost conditional branches remain typed
unresolved evidence.

## Deterministic final replay

Two final scans produced byte-identical `facts.ndjson` files:

- facts SHA-256:
  `baa0586fd832e7158e0f07e20ab7459445d6e38191e311e3c460f9abf9c68da5`
- ordered `coverage.gaps` SHA-256:
  `8b7a91e7335e9a03d2f2169dd199fe34e70f7f3e04aafd5c3372983b629a5c72`
- packet A file SHA-256:
  `aa2e135214c1b87384c0d65b9d78088f06136c925a1099daf3b56eb0114232a2`
- packet B file SHA-256:
  `43cb9d9c9ea168d2a8ff0bac44fd99ee321c396f40c0f445809870e237080b75`

Packet files differ only because run metadata binds each scan ID/timestamp and
artifact metadata; normalized facts and gap identities are deterministic.

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
  --repo <canonical-shopgenie-origin-main-checkout> \
  --out <disposable-output> \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label canonical-shopgenie-origin-main-syntax \
  --no-semantic
python3 scripts/validate-adapter-artifacts.py <disposable-output>
```

Adversarial coverage verifies conditional-versus-always branch identity,
branch-loss descriptor changes, nested capture/escape/mutation rejection,
unsupported destructuring, exact object-rest exclusion, deferred-object
tampering, orphaned/invalid contract fields, and deterministic replay. The
host's historical 17 cross-producer surface gaps remain separate and are not
claimed closed by this producer work.
