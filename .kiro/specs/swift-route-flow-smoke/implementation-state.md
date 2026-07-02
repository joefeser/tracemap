# Swift Route-Flow Smoke Implementation State

Status: implemented
Branch: `codex/swift-route-flow-smoke`
Public claim level: demo after generated outputs are reviewed for publication.

## Scope

- Adds `scripts/smoke-swift-route-flow.sh`.
- The smoke scans the checked-in Swift HTTP/API client sample, combines the
  index, and runs `tracemap route-flow --client-call "GET /v1/orders/{}"`.
- The smoke validates route-flow Markdown/JSON output, entry evidence,
  source-local method bridge evidence, terminal HTTP surface evidence, touched
  files/symbols, Swift HTTP supporting rule IDs, and runtime-proof limitations.
- The smoke checks generated output for fixture raw URLs and secret-like values.

## Safety

- This is a validation/demo harness only.
- The report remains static, coverage-relative evidence. It does not prove
  runtime network traffic, endpoint reachability, auth, deployment, branch
  feasibility, or user action.
- Review-loop hardening added an output cleanup guard: the smoke refuses root-like
  paths, paths outside the Swift route-flow smoke namespace, and existing
  unmarked directories before running `rm -rf`.

## Validation

- `scripts/smoke-swift-route-flow.sh /tmp/tracemap-swift-route-flow-smoke`
  passed.
  - Scan produced 76 facts.
  - Combine imported 1 source, 76 facts, 23 symbols, 2 relationships, and
    17 call edges.
  - Route-flow produced 1 entry evidence row, 3 static flow rows, 1 dependency
    surface, 2 coverage-relative gaps, and `ReducedCoverage`.
  - The smoke verified Markdown/JSON artifacts, supporting Swift HTTP rule IDs,
    source-local bridge evidence, touched files/symbols, limitations, and no raw
    fixture URL/secret leakage.
- `bash -n scripts/smoke-swift-route-flow.sh` passed.
- Unsafe output refusal checks for `/tmp` and a home-directory path passed.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 697 tests.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
