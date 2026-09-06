# Base44 runtime-deferred query replay

This candidate replay validates `base44-evidence/0.5.0` and
`88mph.entity-query.v2` against canonical ShopGenie `origin/main` commit
`6753cfa6264b04c0d7dbfcde942689ee32d2c06b`, accepted source/tree SHA-256
`a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`.

## Result

- 4,261 Base44 facts and 1,371 entity-operation keys are preserved.
- All 734 query facts carry v2 descriptors.
- 733 descriptors are complete; one root-bound filter remains unresolved with
  `query:filter_construction_unresolved`.
- The 650 former `query:implicit_predicate_type_unresolved` rows are now
  complete runtime-deferred references.
- Independent comparison with the reverse-engineer candidate found 734/734
  matching query keys and zero descriptor differences.
- A second TraceMap run produced byte-identical `facts.ndjson`; per-run scan
  manifest and SQLite identities intentionally differ.
- Candidate `facts.ndjson` SHA-256:
  `eade638627565e3d8cf6027fe2e80de6efc9c79e444c0057e5e3f858eb6b3be3`.
- Synthetic regressions prove nested executable access chains such as
  `helper().id`, `values[makeKey()]`, optional-call receivers, and executable
  nested indexes remain unresolved. Direct identifier, property, `this`, and
  literal/admitted-reference element chains remain complete.

## Validation matrix

The applicable procedure in [`docs/VALIDATION.md`](../VALIDATION.md) was
followed after the review correction. The retained command/result record is:

- `npm run check --prefix src/typescript` — build and 126/126 tests passed.
- `dotnet build src/dotnet/TraceMap.sln` — passed as part of the public demo;
  `dotnet test src/dotnet/TraceMap.sln --verbosity minimal` — 1,732/1,732.
- `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
  gradle -p src/jvm test` — passed.
- isolated Python editable install and `pytest src/python/tests` — 64/64;
  `smoke-python-endpoints.sh` — passed with its expected reduced-evidence
  labels.
- `python3 scripts/test_validate_adapter_artifacts.py` — 7/7;
  `validate-adapter-artifacts.py` also passed against both exact canonical
  ShopGenie outputs.
- `./scripts/check-private-paths.sh` and `git diff --check` — passed.
- `./scripts/demo-public.sh /tmp/tracemap-demo-pr720` — passed all six scans,
  both combined indexes, reports, paths, reverse, portfolio, diff, impact, and
  release-review smokes.
- two exact canonical ShopGenie scans — byte-identical facts at the SHA-256
  above; both emitted 4,261 Base44 facts and retained all 1,371 operations.
- independent producer comparison — 734/734 keys present in each producer and
  zero descriptor differences.

The pinned OSS command was:

```bash
TRACEMAP_SKIP_BUILD=1 \
TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock \
  scripts/smoke-open-source-repos.sh \
  /tmp/tracemap-oss-cache \
  /tmp/tracemap-oss-smoke-pr720-postreview
```

Its retained result excerpt was:

```text
scip-typescript commit=891eb4293709a6a587bf4468dfa1b45a85182fd9 facts=14060 analysis_gaps=33
axios-npm-lock commit=84a9f3b9a4f3244b8c8e818f557d64c7b964fb25 facts=11186 analysis_gaps=56
OSS smoke complete
```

`swift test --package-path src/swift` was attempted but cannot complete in the
installed Command Line Tools environment because `XCTest` is unavailable. No
Swift code changed; the repository Swift CI lane with a complete toolchain
remains required. No other validation step was deferred.

## Boundary

The extractor records only reference spans, not expression text, runtime
values, inferred types, or customer payloads. Root bindings, spreads,
computed/duplicate filter keys, logical composition, unsupported expressions,
dynamic controls, unsupported operators, and traversal overflow remain typed
gaps.

This is candidate static evidence. It does not admit the producer, change the
host gate, prove backend operator compatibility, publish a denominator, or
authorize deployment.
