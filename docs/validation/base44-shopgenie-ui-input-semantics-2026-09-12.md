# ShopGenie Base44 UI input semantics validation — 2026-09-12

This receipt validates `base44-evidence/0.15.0` and
`88mph.base44-ui-input-semantics.v1` against the current ShopGenie
`origin/main` authority, not an export or helper directory.

## Source authority

- repository: `BigRiverMachine/ShopGenie`
- commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- detached scan root: `/private/tmp/shopgenie-ui-input-origin-main-20260912`

## Results

Two independent scans emitted 6,876 Base44 facts each and were byte-identical:

- `base44-evidence.json` SHA-256:
  `786ffcc3424fcdb88afae7df4a364a9358a4b5103a4edc8a6eb6af1147dad295`
- `facts.ndjson` SHA-256:
  `347109e0a9bd86f6d4bdeff06d7c40d417cec6df274c324c06d8fdb3ba25d43e`
- UI semantics facts: 1,245
- proven payload correlations: 745
- partial controls with explicit unresolved correlation: 490
- unresolved controls without a field/value binding: 10
- multi-target correlations silently selected: 0

The current source emits high-confidence decimal evidence for all three
`parseFloat(row.cost)` operations into `MaterialItemPriceBreak.price`. The
wrapped `Input` bound to `row.cost` also correlates to the create/update
operations as decimal evidence. Other price-break evidence includes decimal
classes for `PurchasedPartPriceBreak.cost`,
`OutsideServicePriceBreak.cost_per_unit`, and
`ToolingItemPriceBreak.cost_per_unit`. Quantity/minimum-quantity fields retain
all observed integer, number, and decimal classes; none is narrowed.

The UI value-class census is: 53 boolean, 14 date-string, 278 decimal, 105
integer, 152 number, 1 object, 591 string, and 51 unknown facts. Every
descriptor retains `storageAuthority=widening-only-never-narrowing`.

The 500 uncorrelated controls use
`unresolvedCorrelationReason=no-proven-submitted-payload-correlation`. Most are
forms whose Base44 mutation handler is passed across a component/module
boundary. Their control/binding evidence remains available at low confidence,
but no entity or payload field is selected. This is the principal remaining
correlation limitation.

## Commands

```bash
npm run check --prefix src/typescript
dotnet test src/dotnet/TraceMap.sln
python3 scripts/validate-adapter-artifacts.py \
  /private/tmp/tracemap-shopgenie-ui-semantics-e
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
git diff --check

node src/typescript/dist/src/cli.js base44-evidence \
  --repo /private/tmp/shopgenie-ui-input-origin-main-20260912 \
  --out /private/tmp/tracemap-shopgenie-ui-semantics-e \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label shopgenie-origin-main-ui-input-semantics

cmp /private/tmp/tracemap-shopgenie-ui-semantics-e/base44-evidence.json \
  /private/tmp/tracemap-shopgenie-ui-semantics-f/base44-evidence.json
cmp /private/tmp/tracemap-shopgenie-ui-semantics-e/facts.ndjson \
  /private/tmp/tracemap-shopgenie-ui-semantics-f/facts.ndjson
```

Results: TypeScript 257/257 passed; .NET 1,928/1,928 passed; artifact validation,
validator unit tests, private-path guard, diff check, and both deterministic
comparisons passed.

JVM and Python adapter suites were not run because this change is confined to
the TypeScript/Base44 producer and its shared artifact contract.
