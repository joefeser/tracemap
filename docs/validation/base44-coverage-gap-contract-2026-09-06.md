# Base44 Producer Coverage-Gap Contract Validation

Date: 2026-09-06

This receipt validates the TraceMap producer portion of 88mphServer issue
`#622`. It does not close that host issue: the host must still type and compose
its cross-producer comparison gaps. In particular, the historical ShopGenie
set of 15 `function.canonical_only`, one `sdk_primitive.canonical_only`, and one
`http_target.canonical_only` gaps is host-generated and is not represented as
producer-owned `coverage.gaps`.

## Bound identities

- TraceMap base commit: `8df82f1c13239ebe985fa9fa71141e14da822c3f`
- Source repository: `BigRiverMachine/ShopGenie`
- Source commit: exact `origin/main`
  `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Packet schema: `tracemap.base44.static-evidence.v1` (additive fields)
- Coverage-gap schema: `tracemap.base44.coverage-gap.v1`
- Extractor version: `base44-evidence/0.7.0`

Every producer-owned Tier-4 Base44 fact now has exactly one source-bound gap
with a stable full-SHA-256 `gapId`, a required non-null `factId`, rule and tier,
a declared surface, and one category from the closed set `entity`, `function`,
`auth`, `http-integration`, `storage`, `provider`, or `unknown`. The producer
always emits `coverage.gaps`, including an empty array. Manifest-only
`coverage.knownGaps` and host-generated comparison gaps remain separate so the
producer cannot erase or falsely classify them.

## Canonical ShopGenie replay

The pre-change replay produced 4,261 facts and 90 Tier-4 facts. Its packet
SHA-256 was
`fbd4a06f83c438269ca09215ce4c465d5f04b50152a610a7edc28130e4e21b80`
and its facts SHA-256 was
`fcd5ce552722c86168108f0a068cbf9af48b6899f213f765be3164068bf33ff6`.

Two post-change replays both produced the same 4,261 facts and the same 1,371
entity-operation denominator. They emitted 90 producer gaps: 89 `entity` and
one `http-integration`. The two ordered gap arrays were byte-identical with
SHA-256
`37a7b71dc7d883d0cbeb7c992a6885af78d2780a57ff1c1c3876ae3863dd7433`.
The two `facts.ndjson` files were byte-identical with SHA-256
`33e9048c070a0df9593d022c6512817a72d78eeb4412b94a287c9392eae6d638`.
Packet digests differed, as expected, because packet metadata binds each run's
scan ID, timestamp, and artifact metadata:

- replay A: `24b6b8091b3b3a4033d8473da633adb70a7d6196bda513f60b779f4aecc5ff7a`
- replay B: `a5cea0f4c8302ca9eb40cd715c4a63b2292268883c8a113787491442bc94bf0c`

The entity-operation denominator and fact-type counts did not change in this
contract-only slice. The facts digest changed because the extractor version is
part of fact evidence identity.

## Adversarial checks

The focused suite proves that a mixed source fixture produces distinct
`entity`, `function`, `http-integration`, and fail-closed `unknown` gaps; that
all and only Tier-4 facts appear exactly once; and that unchanged source
replays produce the same ordered gap identities. It also rejects malformed and
duplicate gap IDs, duplicate fact IDs, missing rows, extra rows, and attempts
to reclassify an unknown fact. A non-Base44 fixture proves the new producer
still emits the mandatory empty array.

## Commands and results

```bash
npm ci --prefix src/typescript
npm run check --prefix src/typescript
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test installDist
python3 -m venv /tmp/tracemap-python-622-venv
/tmp/tracemap-python-622-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-622-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-622-venv/bin/python ./scripts/smoke-python-endpoints.sh
python3 scripts/test_validate_adapter_artifacts.py
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock ./scripts/smoke-open-source-repos.sh
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

Results: 158/158 TypeScript tests, 1,732/1,732 .NET tests, JVM tests and
distribution install, 64/64 Python tests and endpoint smoke, and 7/7 artifact
validator tests passed. The private-path and diff guards passed. The pinned
public smokes emitted 14,060 facts for `scip-typescript` at
`891eb4293709a6a587bf4468dfa1b45a85182fd9` and 11,186 facts for
`axios-npm-lock` at `84a9f3b9a4f3244b8c8e818f557d64c7b964fb25`; both retained their explicit
reduced-coverage gaps.

## Remaining boundary

This contract makes producer uncertainty composable; it does not turn any gap
into passing evidence. The 90 producer gaps remain blockers until stronger
source evidence closes them. The host must independently retain and type its
17 cross-producer gaps and prove exact-once composition across both sets.
