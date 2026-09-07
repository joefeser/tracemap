# ShopGenie Base44 source-flow evidence — 2026-09-07

## Authority

- Repository: `/Users/josephfeser/src/BigRiverMachine/ShopGenie`
- Exact source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256: `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Extractor: `base44-evidence/0.14.0`

The executable source is authority. No prose schema, fixture, reverse-engineer
artifact, host reconciliation, or customer-specific filename supplies an
entity, field, selector, reachability, payload, or SDK-identity fact.

## Independent static result

The canonical scan emits 5,585 Base44 facts, including exactly 1,797 executable
`Base44EntityOperation` facts and 46 separate source-bound dormant callsite
dispositions. The operation key set is stable across deterministic replays.
These are raw source facts, not a packaged-runtime or compatibility claim.

Both final replays produced a byte-identical `base44-evidence.json` with SHA-256
`63db70276ec8d47a4f654ec121f441109a30df542103fe0247e2731646f71573`
and byte-identical `facts.ndjson` with SHA-256
`2b5e991f59e849f3c4512c608d2a3144498077d9e46c6fa999825e32f48eb4a9`.

Forty-six mutation payload rows remain intentionally partial under the exact
runtime obligation
`entity-open-object-fields:docker-write-readback-cleanup`. Each has a
source-proven outer object and finite reference accounting, while its runtime
fields remain opaque. The obligation is not static completeness and cannot be
accepted without exact SDK open-object transport plus isolated Docker
write/readback/cleanup proof.

Two executable rows remain hard entity blockers because source proves a
one-argument parent callback is invoked by its child with `(id, data)`:

- `src/pages/Material.jsx:474` — `Vendor.update`; `handleUpdateVendor(vendorData)`
  is passed to `VendorViewSheet`, whose save path invokes
  `onUpdate(vendor.id, dataToSubmit)`. The SDK payload therefore receives the
  ID argument, not the submitted object.
- `src/pages/OutsideServices.jsx:246` — `ServiceProvider.update`;
  `handleUpdateServiceProvider(providerData)` is passed to
  `ServiceProviderViewSheet`, whose save path invokes
  `onUpdate(provider.id, dataToSubmit)`. The SDK payload therefore receives the
  ID argument, not the submitted object.

One unrelated dynamic HTTP-target fact remains an `http-integration` gap. It
must survive composed release gating exactly once and is not an entity gap.

## Closed source-flow rules

Versions 0.13.0 and 0.14.0 add bounded inference for correlated relationship maps,
terminating branch predicates, controlled selector state, finite local/callback
caller graphs, inline and named JSX callbacks, React Query mutation arguments,
and React dependency-array references. JSX attribute names are not mistaken
for value references. A directly constructed object remains source-proven as
an outer object even when a spread's fields are opaque; the property set stays
open and retains the Docker obligation.

The extractor excludes a call only through the closed callsite-disposition
contract. Path names such as `test`, `smoke`, or `diagnostic` never establish
dormancy. The raw 1,797-row denominator therefore deliberately includes rooted
diagnostic/test-route source until an independently reviewed packaged-runtime
reachability authority composes a narrower active denominator.

## Semantic projection and remaining promotion blocker

Payload `fieldsJson` continues to record every syntactic `expressionType`,
presence, and source derivation without collapsing alternatives. Shape v3 adds
`semanticFieldsJson`, a unique-by-name projection with closed semantic
presence, value type, explicit-null state, and exact contributing provenance.
The exact replay contains 563 payload-v3 facts, 2,342 syntactic observations,
and 2,224 semantic field aggregates. Of the semantic aggregates, 1,663 retain
`valueType=unknown`; they are honest blockers to typed-column promotion rather
than inferred types. All 118 collapsed alternatives remain recoverable through
the semantic projection's provenance arrays.
Host schema promotion remains blocked for any unknown/conflict and until both
independent producers agree across the required executable mutation callsites.
Explicit null and conditional presence remain distinct.

## Verification

```bash
npm test --prefix src/typescript -- Base44Evidence.test.ts
npm run check --prefix src/typescript
python3 scripts/validate-adapter-artifacts.py <scan-a>
python3 scripts/validate-adapter-artifacts.py <scan-b>
python3 scripts/test_validate_adapter_artifacts.py
cmp <scan-a>/base44-evidence.json <scan-b>/base44-evidence.json
./scripts/check-private-paths.sh
git diff --check
```

The portable packet binds only deterministic `facts.ndjson`, `report.md`, and
`logs/analyzer.log`. Operational `scan-manifest.json` and `index.sqlite` remain
required outputs validated independently; their timestamp-bearing bytes cannot
perturb the portable packet digest.
