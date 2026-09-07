# Base44 Mutation-Hook Payload Evidence Validation

Date: 2026-09-06

This receipt records a bounded TraceMap issue #713 improvement. It adds
independent executable-source evidence for payload parameters in directly
declared TanStack React Query mutation callbacks. It does not create schema
authority, prove runtime behavior, admit an SDK, or declare ShopGenie
compatible.

## Bound identities

- TraceMap baseline: exact `origin/dev`
  `6bc39bffdd7710e6cd1c41512ab0fafdfe0fc8c3`.
- Source repository: `BigRiverMachine/ShopGenie`.
- Source ref: exact `origin/main`.
- Source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`.
- Accepted source/tree SHA-256 supplied by the existing authority:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`.
- Baseline extractor: `base44-evidence/0.5.0`.
- Corrected extractor: `base44-evidence/0.6.0`.
- Baseline packet SHA-256:
  `b2cea57f3f426ae61c8ad20c2a97fbcbd7c9f8763a3536c0f8b50eff728d0f23`.
- Corrected pre-review packet SHA-256:
  `127b10a70947d577486f6a563c7c6370a5798615e7c9742ce887041bfeee8b7b`.
- Post-review replay `facts.ndjson` SHA-256:
  `8333b563e0cedc4715703f36591d2f03be620f5acdb0f53f6a4eff7ca53019e9`.

The accepted SHA-256 values above are caller-supplied authority identities.
TraceMap validates and binds them but does not independently establish packet
custody. Both scans used the same detached source checkout. Generated packets
remain disposable local artifacts and are not committed.

## Exact before/after evidence

| Evidence | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Entity-operation facts | 1,371 | 1,371 | 0 |
| Entity-payload facts | 538 | 538 | 0 |
| Entity-query facts | 833 | 833 | 0 |
| Payload field observations | 1,783 | 2,137 | +354 |
| Complete payload facts | 403 | 450 | +47 |
| Partial payload facts | 33 | 63 | +30 |
| Unresolved payload facts | 102 | 25 | -77 |
| Top-level mutation-hook-derived payload facts | 0 | 95 | +95 |

The deterministic packet diff contains 127 added and 127 removed facts, all of
type `Base44EntityPayload`; 4,134 facts are unchanged and coverage is not
reduced. Forty-seven payload facts move from unresolved to complete and thirty
move from unresolved to partial. A partial result remains a blocker-capable
analysis gap; observed fields on that row must not be interpreted as exhaustive.

The remaining 25 unresolved payload facts include unsupported or absent hook
callsites, ordinary parameter/dataflow boundaries, and destructuring cases.
The corrected packet also preserves typed gaps for dynamic computed fields,
unknown spreads, dependency-array/other hook-result escapes, and unresolved
callsite arguments. No gap is converted to clean absence.

After the review hardening, two fresh scans produced byte-identical
`facts.ndjson` artifacts at the hash above. The evidence counts and the
before/after diff remained exactly unchanged: 127 added and 127 removed
`Base44EntityPayload` facts with 4,134 facts unchanged. The enclosing packet
hash is not used as the replay-equality claim because it binds the normal scan
manifest, whose `scannedAt` value is intentionally run-specific.

## Controlled checks

The focused tests prove:

- direct and aliased named `useMutation` imports and namespace imports;
- direct callback parameters and statically named destructured `data` paths;
- field-presence intersection across more than one mutation callsite;
- numeric expression classification and contributing source spans/hashes;
- deterministic payload fact identities across identical scans;
- rejection of a spoofed local function and parameter or later local binding
  that shadows an otherwise valid `useMutation` import;
- typed gaps for dynamic callsite arguments and escaped `.mutate` methods;
- refusal to select a destructured payload through a later unknown spread; and
- rejection of shadowed, rest/non-first, and unsupported-pattern callback
  parameters;
- rejection of callbacks replaceable by later duplicate, spread, or dynamic
  mutation options while preserving explicit-after-spread proof;
- typed gaps for mutated or escaped destructured callsite wrappers and recursive
  self-hook calls; and
- continued redaction of raw literal values.

The existing issue #713 suite also continues proving that schema prose cannot
change the packet while an executable payload edit changes the exact
source-bound payload fact.

## Commands and results

The language-adapter validation procedure in `docs/VALIDATION.md` was followed,
including the required local matrix, artifact conformance checks, private-path
guard, and pinned public OSS smoke scans described below.

```bash
npm run check --prefix src/typescript
```

Result: 10 test files and 141 tests passed.

```bash
node src/typescript/dist/src/cli.js base44-evidence \
  --repo <detached-canonical-shopgenie-origin-main> \
  --out <disposable-output> \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label canonical-shopgenie-origin-main-syntax \
  --no-semantic
python3 scripts/validate-adapter-artifacts.py <disposable-output>
```

Result: 4,261 source-bound Base44 facts emitted and all adapter artifacts
validated. The operation denominator remains 1,371/1,371 relative to the
independent reverse-engineer census. This receipt reports payload evidence
coverage only, not host reconciliation or application compatibility.

The remaining repository validation matrix also passed:

- .NET solution build and 1,732/1,732 tests;
- JVM tests and distribution installation with Homebrew Java 21;
- 64/64 Python tests and the Python endpoint smoke, with its declared reduced
  coverage;
- 7/7 adapter-validator unit tests;
- private-path guard; and
- Git whitespace validation.

Pinned TypeScript OSS smoke completed at the required commits:

- `scip-typescript` `891eb4293709a6a587bf4468dfa1b45a85182fd9`:
  14,060 facts and 33 declared analysis gaps;
- `axios-npm-lock` `84a9f3b9a4f3244b8c8e818f557d64c7b964fb25`:
  11,186 facts and 56 declared analysis gaps.

The Axios artifact validator passed. The scip-typescript artifact validator
reported the same nine pre-existing `local-absolute-path` property diagnostics
on both this branch and an unmodified exact-baseline scan. The scanner fact and
gap counts were identical. This known shared-adapter conformance issue is not
caused by the Base44-only change and is not silently treated as passing.

## Remaining boundary

TraceMap still does not evaluate React, execute TanStack Query, infer database
constraints, or prove persistence. The host must explicitly admit the new
extractor identity, reconcile these facts against its independent producer,
retain every contradiction or unresolved row as a typed blocker, and then
prove the accepted denominator through the packaged frontend in local Docker.
