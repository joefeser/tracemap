# ShopGenie payload source closure

Date: 2026-09-07

This receipt covers extractor `base44-evidence/0.11.0`. The pass was derived
independently from executable ShopGenie source. Reverse-engineer and host
outputs were reserved for comparison after the producer artifacts existed.

## Bound inputs

- TraceMap predecessor: `2cc57646d602d3c50d00944c2ce0e668d1ad9370`
- ShopGenie repository: `<exact-ShopGenie-origin-main-checkout>`
- ShopGenie commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`

## Exact producer delta

The declared entity-operation denominator remains 1,371. Total facts remain
4,261: 538 payloads and 833 queries. Per-operation SDK identity remains 1,289
frontend 0.8.5 operations and 82 function-runtime 0.8.4 operations, with zero
SDK identity gaps.

| Measure | 0.10.0 | 0.11.0 | Delta |
| --- | ---: | ---: | ---: |
| Complete payloads | 456 | 470 | +14 |
| Runtime-deferred payloads | 37 | 38 | +1 |
| Non-deferred incomplete payloads | 45 | 30 | -15 |
| Entity coverage gaps | 82 | 68 | -14 |
| HTTP coverage gaps | 1 | 1 | 0 |
| Total producer coverage gaps | 83 | 69 | -14 |

The 15 corrected rows are:

- 14 source-proven static completions: PrintLabor update, Quote update, Print
  update, DevelopmentStory create/update, FixtureBin update, JobCostAnalysis
  create, Material update, ServiceProvider update, Organization update, Task
  update, ToolingType update, Supplier update, and Vendor update; and
- one MaterialItem create row that now proves a finite outer object while
  retaining `runtime-deferred-object-fields` and the Docker
  write/readback/cleanup obligation. It is not counted as statically complete.

The payload state distribution is:

| State | Rows |
| --- | ---: |
| `complete|object|source-bounded|none` | 470 |
| `partial|object|source-bounded|obligation` | 38 |
| `partial|object|unresolved|none` | 10 |
| `partial|unknown|unresolved|none` | 1 |
| `unresolved|unknown|unresolved|none` | 19 |

## Residual blockers

All 30 non-deferred incomplete rows remain source-bound blockers:

- 14 `binding-initializer-unresolved`: payloads arrive through a
  cross-component form/view callback and the payload v2 fact has no independent
  cross-file field-provenance contract;
- 10 `dynamic-computed-property`: JobCost actual create/update paths reach the
  computed field through child-component callbacks plus React refs and
  debounced rest forwarding, so the complete caller domain is not locally
  source-bounded; and
- six `mutation-hook-callsite-missing`: the declared update hooks have no
  `.mutate` or `.mutateAsync` callsite in accepted source. UI state reads such
  as `.isPending` do not create a payload.

No gap was suppressed for a missing branch, escaping callback, unknown
argument, cross-component handoff, ref-mediated call, or unused hook.

## Deterministic replay

- Packet A: `/tmp/tracemap-shopgenie-closure-v011-a.enly5U`
- Packet B: `/tmp/tracemap-shopgenie-closure-v011-b.HX3OBc`
- byte-identical `facts.ndjson` SHA-256:
  `e7abb9dcd5860aa57272b4d34c3691b2483ddf648cb7f6a4a619b6c041438b62`
- ordered `coverage.gaps` JSON SHA-256:
  `bf917df1e9da96b7a6cfe67cf73ba2fce951b0c4e1003d28530974afd8a94acc`
- packet A SHA-256:
  `3db1587b81a45469157754602f8227c3291fd252e5a9dc23e660e559505a747f`
- packet B SHA-256:
  `310075c1e1b8dde4c13d8c6aa625b81b3c5a7916a187d5090d15753a2755ae8b`

Packet hashes differ only in run metadata. Facts and coverage gaps are
deterministic.

## Verification

```bash
npm run check --prefix src/typescript
python3 scripts/test_validate_adapter_artifacts.py
python3 scripts/validate-adapter-artifacts.py \
  /tmp/tracemap-shopgenie-closure-v011-a.enly5U
python3 scripts/validate-adapter-artifacts.py \
  /tmp/tracemap-shopgenie-closure-v011-b.HX3OBc
TRACEMAP_SKIP_BUILD=1 \
  TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock \
  ./scripts/smoke-open-source-repos.sh
./scripts/check-private-paths.sh
git diff --check
```

The TypeScript suite passed 185/185 tests. New adversarial coverage requires
exact React/Lodash imports, rejects shadowed hooks and wrappers, retains gaps
for runtime-open and escaped computed-key callers, rejects unsafe React setter
flows, distinguishes mutually exclusive from sequential branches, and keeps
unused mutation hooks blocked.
