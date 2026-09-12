# ShopGenie Base44 UI input semantics validation — 2026-09-12

This receipt validates `base44-evidence/0.15.0` and
`88mph.base44-ui-input-semantics.v1` against the current ShopGenie
`origin/main` authority, not an export or helper directory.

## Source authority

- repository: `BigRiverMachine/ShopGenie`
- commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- detached scan root:
  `/private/tmp/shopgenie-ui-input-origin-main-20260912-refresh2.lvEjrd/source`

## Results

Two independent scans emitted 6,865 Base44 facts each and were byte-identical:

- `base44-evidence.json` SHA-256:
  `5ec250f94ca48d2cb01e03f4172d23dd4faf9944ef7ba74b4aebe18fbbe37f35`
- `facts.ndjson` SHA-256:
  `da38af5d0325923a05f70950793f677542b259620eb4a689a9895a63247cf2ac`
- refreshed output path:
  `/private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V`
- comparison output path:
  `/private/tmp/tracemap-shopgenie-ui-semantics-refresh2-b.5PXEDm`
- UI semantics facts: 1,232
- proven payload correlations: 722
- partial controls with explicit unresolved correlation: 493
- unresolved controls without a field/value binding: 17
- multi-target correlations silently selected: 0

The current source emits high-confidence decimal evidence for all three
`parseFloat(row.cost)` operations into `MaterialItemPriceBreak.price`. The
wrapped `Input` bound to `row.cost` also correlates to the create/update
operations as decimal evidence. Other price-break evidence includes decimal
classes for `PurchasedPartPriceBreak.cost`,
`OutsideServicePriceBreak.cost_per_unit`, and
`ToolingItemPriceBreak.cost_per_unit`. Quantity/minimum-quantity fields retain
all observed integer, number, and decimal classes; none is narrowed.

The UI value-class census is: 53 boolean, 14 date-string, 262 decimal, 105
integer, 155 number, 1 object, 590 string, and 52 unknown facts. Every
descriptor retains `storageAuthority=widening-only-never-narrowing`.

The 510 uncorrelated controls use
`unresolvedCorrelationReason=no-proven-submitted-payload-correlation`. Most are
forms whose Base44 mutation handler is passed across a component/module
boundary. Their control/binding evidence remains available at low confidence,
but no entity or payload field is selected. This is the principal remaining
correlation limitation.

## Commands

This receipt followed `docs/VALIDATION.md` for a TypeScript/Base44 adapter-only
change. The applicable pinned matrix was:

- TypeScript adapter contract and unit matrix:
  `npm run check --prefix src/typescript` — passed, including
  `Base44UiInputSemantics.test.ts`.
- Artifact conformance:
  `python3 scripts/validate-adapter-artifacts.py
  /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V` and
  `python3 scripts/test_validate_adapter_artifacts.py` — passed.
- Private-path and diff hygiene:
  `./scripts/check-private-paths.sh` and `git diff --check` — passed.
- Deterministic source-bound ShopGenie scan:
  two scans of the same `origin/main` commit produced byte-identical
  `base44-evidence.json` and `facts.ndjson` — passed.
- Host intake compatibility:
  coordinated in 88mphServer PR #634, which allowlists
  `base44-evidence/0.15.0` and `base44.ui-input-semantics.v1` and validates
  the UI descriptor before host consumption.

Deferred checks:

- JVM and Python adapter suites were not run because no JVM/Python scanner,
  shared index, relationship-query, or cross-language output path changed in
  this PR.
- Public OSS smoke was not run because the change is scoped to the Base44
  TypeScript producer and is covered here by the private ShopGenie
  source-bound replay plus artifact validation.

The refreshed run also includes regression coverage for lexical shadowing and
dynamic input-type safety: nested helper parameters and locally shadowed cast
functions no longer produce proven UI correlations, and native
`<input type={...}>` controls remain partial/unknown instead of defaulting to
text evidence.

```bash
npm run check --prefix src/typescript
dotnet test src/dotnet/TraceMap.sln
python3 scripts/validate-adapter-artifacts.py \
  /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
git diff --check

node src/typescript/dist/src/cli.js base44-evidence \
  --repo /private/tmp/shopgenie-ui-input-origin-main-20260912-refresh2.lvEjrd/source \
  --out /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label shopgenie-origin-main-ui-input-semantics

cmp /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V/base44-evidence.json \
  /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-b.5PXEDm/base44-evidence.json
cmp /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-a.9uff6V/facts.ndjson \
  /private/tmp/tracemap-shopgenie-ui-semantics-refresh2-b.5PXEDm/facts.ndjson
```

Results: TypeScript 257/257 passed; .NET 1,928/1,928 passed; artifact validation,
validator unit tests, private-path guard, diff check, and both deterministic
comparisons passed.

The refreshed fixture also covers these adversarial safety regressions:
controls with no submitted value binding remain partial, trailing JSX spreads
invalidate control proof, trailing payload spreads invalidate overwritten
payload fields, escaped const payload roots remain unresolved, hoisted local cast
functions shadow global casts, truthy `required` JSX attributes stay truthy, and
`src/__mocks__` is excluded from UI source authority.
