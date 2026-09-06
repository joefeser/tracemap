# Base44 Injected-Client Dataflow Validation — 2026-09-06

This receipt validates the bounded TypeScript extractor correction in TraceMap
issue #713. It is static producer evidence only. It does not create schema
authority, prove runtime behavior, admit an SDK, or declare ShopGenie compatible.

## Bound source

- Repository: `BigRiverMachine/ShopGenie`
- Source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted normalized tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- Extractor identity: `base44-evidence@base44-evidence/0.4.0`

## Result

Two independent scans produced byte-identical `facts.ndjson` files with SHA-256
`460896d79af6545ae750ecc7bfb722501f6180fbfe77d0c7ed66f6fd2d5fcc75`.
The entity-operation key count moved from 1,368 to 1,371. The exact additive
delta was:

| File | Line | Entity | Operation |
| --- | ---: | --- | --- |
| `src/components/utils/autoLinkVendor.jsx` | 24 | `Vendor` | `list` |
| `src/components/utils/autoLinkVendor.jsx` | 33 | `MaterialTypeVendor` | `filter` |
| `src/components/utils/autoLinkVendor.jsx` | 44 | `MaterialTypeVendor` | `create` |

No prior operation key was removed. All three new facts carry
`clientBindingKind=callsite-proven-parameter` and a registered fact-level rule
identifier.

## Review regressions

The focused fixture covers imported and local helpers, nested immutable helper
expressions, immutable caller aliases, overload implementations, bounded
recursive forwarding, and helper-file HTTP/environment extraction. Negative
cases cover conflicting callsites, lexical block/catch/loop/class/function
shadows, mutable or reassigned SDK aliases, reassigned parameters, and mutable
or reassigned helper callables, including a seeded recursive cycle whose path
changes and therefore becomes ambiguous.

## Validation matrix

The applicable requirements from `docs/VALIDATION.md` were followed:

- `npm run check --prefix src/typescript` — 95/95 tests passed.
- `dotnet build src/dotnet/TraceMap.sln` — passed with zero errors; one existing
  nullable warning was reported in a test file.
- `dotnet test src/dotnet/TraceMap.sln` — 1,732/1,732 tests passed.
- `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
  gradle -p src/jvm test` — passed.
- isolated Python environment, editable install, and
  `python -m pytest src/python/tests` — 64/64 tests passed.
- `PYTHON_BIN=<isolated-python> ./scripts/smoke-python-endpoints.sh` — passed
  with the expected reduced-evidence coverage labels.
- `python3 scripts/test_validate_adapter_artifacts.py` — 7/7 tests passed.
- `python3 scripts/validate-adapter-artifacts.py <shopgenie-output>` — passed
  for the exact source-bound replay.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.
- Two exact ShopGenie scans — fact-byte deterministic and exact three-row
  operation delta above.
- `./scripts/demo-public.sh <temporary-output>` — passed: six source scans,
  eight combined sources, and the report/paths/reverse/portfolio/diff/impact/
  release-review smokes completed.
- `swift test --package-path src/swift` — attempted and unavailable because the
  installed Apple Command Line Tools environment has no `XCTest` module, the
  exact documented local limitation in `docs/VALIDATION.md`.

The exact accepted ShopGenie replay is the pinned feature smoke because it
exercises the changed Base44 helper-injection path. The generic public demo does
not contain that feature, so it was run in addition to—not substituted for—the
source-bound check. The repository Swift CI lane with a complete toolchain
remains required before merge, as does the five-adapter combine lane.

No runtime, provider, model, deployment, public DNS, or production operation was
performed.
