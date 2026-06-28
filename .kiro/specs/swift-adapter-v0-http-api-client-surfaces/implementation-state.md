# Swift Adapter v0 HTTP And API Client Surfaces Implementation State

Status: `spec-draft`

Issue: [#383](https://github.com/joefeser/tracemap/issues/383)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-http-api-client-surfaces`

Intended implementation branch:
`codex/implement-swift-http-api-client-surfaces`

## Current Scope

This spec defines a future Swift v0 HTTP/API client surface extraction slice.
It is not implementation by itself.

The implementation should extract conservative static outbound HTTP/API
evidence from Swift source shapes such as Foundation `URLSession`/`URLRequest`,
Alamofire-style request calls, and Moya target-type declarations. It must emit
the shared `HttpCallDetected` fact shape, safe metadata, and reduced-coverage
gaps without runtime, network, build, simulator, device, package-install, or
endpoint-reachability claims.

## Public Claim Level

Claim level: `concept` until implementation lands on `dev`; `dev-only` after
implementation and validation; not generally shipped until promoted to `main`.

Allowed future claim after validation:

- TraceMap can emit deterministic static Swift outbound HTTP/API client
  evidence for supported source-visible shapes, with explicit gaps for dynamic
  or unsupported cases.

Forbidden claims:

- Runtime HTTP traffic.
- Server endpoint existence or reachability.
- Authentication/authorization correctness.
- Production usage.
- Vulnerability, license, freshness, compatibility, or impact conclusions.
- AI/LLM/vector/prompt-based analysis in the scanner.

## Scope Decisions

- Implement only checked-in source evidence.
- Treat Swift HTTP/API evidence as syntax/textual in v0 unless a later slice
  proves deterministic structural backing.
- Reuse shared `HttpCallDetected` rather than creating a Swift-only fact type,
  because HTTP client evidence has cross-language reducers/report consumers.
- Prefer facts with unknown/dynamic statuses over inferred defaults.
- Use shared `normalizedPathKey` rules and role-prefixed 64-character lowercase
  UTF-8 SHA-256 hashes such as `swift.http|url|...` and
  `swift.http|host|...`.
- Define status values precisely: `pathStatus=present` only when a safe
  `normalizedPathKey` is emitted; `queryStatus=present-omitted` only when a
  parsed static URL visibly contains a query that is not rendered raw.
- Use `swiftClientKind` and `swiftApiName` for Swift-local metadata to avoid
  colliding with shared-reader `surfaceKind` and `methodName` behavior.
- Limit URLRequest method association to syntax-local same-function variable
  name identity, first static assignment before first URLSession use, and no
  semantic shadowing resolution.
- Gate shared projection: do not emit `HttpCallDetected` with
  `normalizedPathKey` unless a static `httpMethod` is present, because current
  shared readers can treat missing methods as broad `ANY` evidence.
- Do not chase URL literals through intermediate variables in the first cut;
  emit unknown/dynamic path evidence and gaps instead.
- For Moya targets, emit safe static path evidence when available but always
  keep baseURL/path composition partial in v0, with companion `AnalysisGap`
  facts for partial target joins.
- Omit or hash raw URL, host, query, token, header, body, and private values.
- Keep cross-repo client/server alignment out of scope.

## Validation Plan

Spec branch:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-http-api-client-surfaces --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-http-api-client-surfaces --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Implementation branch:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-http-api-client-surfaces --out /tmp/tracemap-swift-http-api-client-surfaces
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-http-api-client-surfaces/index.sqlite --out /tmp/tracemap-swift-http-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-http-api-client-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-http-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-http-combined.sqlite --out /tmp/tracemap-swift-http-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Follow-Ups

- Cross-repo client/server endpoint alignment.
- Swift route-centered reports.
- Runtime/simulator/browser-assisted evidence, if ever added, as a separate
  opt-in lane outside deterministic static scan.
