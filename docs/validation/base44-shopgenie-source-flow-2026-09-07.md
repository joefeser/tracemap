# ShopGenie Base44 source-flow evidence — 2026-09-07

## Authority

- Repository: `<external-sample-repos>/<private-client-app>`
- Exact source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256: `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Extractor: `base44-evidence/0.14.0`
- TraceMap implementation commit: `39cf17175d05ae5725af808f4815776273f6bfe0`
- TraceMap implementation tree: `41be893b0830c0400c7a9f427e8637242dbc7362`

The executable source is authority. No prose schema, fixture, reverse-engineer
artifact, host reconciliation, or customer-specific filename supplies an
entity, field, selector, reachability, payload, or SDK-identity fact.

## Independent static result

The canonical scan emits 5,631 Base44 facts, including exactly 1,797 executable
`Base44EntityOperation` facts and 46 separate source-bound dormant callsite
dispositions. The operation key set is stable across deterministic replays.
These are raw source facts, not a packaged-runtime or compatibility claim.

Both final replays produced a byte-identical `base44-evidence.json` with SHA-256
`be339501a5ad8106ff07b8ec186f2f33ec707fa6cafb887c9e927552aa0ee212`
and byte-identical `facts.ndjson` with SHA-256
`ea07f6d5076283b7bd1081882effbee4a97d2d79ae9ee195597c486e9d84fef7`.

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

Five dormant callsites retain a dynamic entity-selector gap on their exact raw
SDK primitive. Their source-proven dormant dispositions do not duplicate that
selector gap. Together with the 46 runtime obligations and two source defects,
the entity gap denominator is therefore exactly 53. One unrelated dynamic
HTTP-target fact brings the complete packet gap count to 54; it must survive
composed release gating exactly once and is not an entity gap.

## Closed source-flow rules

Versions 0.13.0 and 0.14.0 add bounded inference for correlated relationship maps,
terminating branch predicates, controlled selector state, finite local/callback
caller graphs, inline and named JSX callbacks, React Query mutation arguments,
and React dependency-array references. JSX attribute names are not mistaken
for value references. A directly constructed object remains source-proven as
an outer object even when a spread's fields are opaque; the property set stays
open and retains the Docker obligation.

The final pass also fails closed on exported mutation handles, unresolved local
JavaScript/TypeScript and configured path-alias edges, array-intrinsic mutation,
and mutations after terminating control flow. Explicit stylesheet imports stay
outside the executable source graph and remain subject to the separate build
and package gates. All ten finite computed JobCost payloads at
`src/pages/JobCostAnalysis.jsx:397-433` are complete without an app-specific
rule.

The array-intrinsic proof also fails closed for indirect, aliased, global-member,
or function-constructor access to `eval`/`Function`, escaped global/intrinsic
aliases, stateful non-string guard values, and direct or aliased mutation of the
`RegExp`, `String`, or `Function` prototype authorities used by the bounded
evaluator proof. The exact guarded ShopGenie arithmetic path remains admitted;
ordinary object members named `eval` and ordinary conditional `window` aliases
do not poison the proof.

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
and 2,224 semantic field aggregates. Of the semantic aggregates, 1,664 retain
`valueType=unknown` or unknown semantic presence. They are honest blockers to
typed-column promotion rather than inferred types. All 118 collapsed
alternatives remain recoverable through
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

The hardened implementation passed 235/235 TypeScript tests, the accumulated
21-case adversarial corpus, and 7/7 adapter-validator tests. Both immutable-git-
archive canonical replays passed artifact validation and reproduced the exact
packet/facts hashes above, 1,797 unique executable operation identities, 46
unique dormant identities, and an empty active/dormant intersection.
