# ShopGenie SDK callsite identity

Date: 2026-09-07

This receipt covers extractor `base44-evidence/0.10.0`. It adds a closed,
source-derived SDK identity to each entity operation and its payload/query
fact. It does not change any payload classification, close a runtime
obligation, or claim runtime compatibility.

## Bound inputs

- ShopGenie repository: `<exact-ShopGenie-origin-main-checkout>`
- ShopGenie commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Frontend package manifest SHA-256:
  `2305ce49599edb5ffe2bf9f2996f5b4f55a461c91a63a67be525a54302369dba`
- Frontend package lock SHA-256:
  `827eb62e9ff66c107e3793bc35f570d5dc4fb8cb2f463442d03813845ed3238e`

The checkout was detached at the exact `origin/main` commit and had no local
changes before either scan.

## Exact result

- Facts: 4,261
- Entity operations: 1,371
- Payloads: 538
- Queries: 833
- Frontend package SDK 0.8.5 operations: 1,289
- Function-runtime SDK 0.8.4 operations: 82
- SDK identity gaps: 0
- Frontend source-import roots: exactly `src/api/base44Client.js`
- Function source-import roots: 13 exact function files
- Producer coverage gaps: 83 (82 entity, one HTTP integration)

Every function-runtime identity has exactly one evidence entry. Every frontend
identity has exactly three entries: the package lock, package manifest, and
direct SDK import source. Intermediary traversal files are absent.

The complete payload state distribution is unchanged from extractor 0.9.0:

| State | Rows |
| --- | ---: |
| `complete|object|source-bounded|none` | 456 |
| `partial|object|source-bounded|obligation` | 37 |
| `partial|object|unresolved|none` | 23 |
| `partial|unknown|unresolved|none` | 3 |
| `unresolved|unknown|unresolved|none` | 19 |

An exact comparison of operation-bound payload state, fields, spreads, gaps,
and runtime obligations between 0.9.0 and 0.10.0 was byte-identical.

## Deterministic replay

- Packet A: `/tmp/tracemap-shopgenie-sdkid-v010-a.zirCdo`
- Packet B: `/tmp/tracemap-shopgenie-sdkid-v010-b.4oEETo`
- byte-identical `facts.ndjson` SHA-256:
  `e538ea70c2d915e3c720fc015d790ac5cc6287c846ee7e8e94878d215926ecb1`
- ordered `coverage.gaps` JSON SHA-256:
  `0e8a8517cc5566f8496b4159113c4fa9afc22fa7ac61248312ab07e52fab3d91`
- packet A SHA-256:
  `236ffb58bae3142d5bcc4d99b2aa72d12aa5c6513738de378cd2bd4f8a554bc5`
- packet B SHA-256:
  `d3dae1f528f27281c140376c1d55fb7df4bf29d3f397148b465c5ec20e5e01f8`

Packet hashes differ only in run metadata. Facts and coverage gaps are
deterministic.

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

The TypeScript suite passed 182/182 tests. Artifact validation passed 7/7;
both selected open-source smoke scans and the private-path guard passed.
Adversarial cases cover mixed 0.8.4/0.8.5 roots, an indirect helper with exact
minimal evidence, ambiguous roots, missing package authority, valid-looking
but false source digests, shape/operation disagreement, and branch-loss
attempts that replace a typed ambiguity with an asserted identity.
