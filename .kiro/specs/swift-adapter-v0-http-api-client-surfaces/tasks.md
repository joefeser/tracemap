# Swift Adapter v0 HTTP And API Client Surfaces Tasks

## Spec And Review

- [x] Create the Kiro spec for issue #383.
- [x] Run Opus spec review.
- [x] Run Sonnet spec review.
- [x] Patch Medium+ review findings or document explicit non-actionable
  dispositions.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Open a spec PR to `dev` and complete the PR review loop.

## Implementation

- [x] Add Swift HTTP/API rule catalog entries and limitations.
- [x] Add shared `HttpCallDetected` fact emission for Swift HTTP/API evidence.
- [x] Add Foundation URL/URLRequest/URLSession static literal extraction.
- [x] Add method extraction for visible static `httpMethod` assignments.
- [x] Add Alamofire-style static request extraction.
- [x] Add Moya target-type static path/method/baseURL extraction.
- [x] Add dynamic/ambiguous/unsafe HTTP analysis gaps.
- [x] Add shared `normalizedPathKey` normalization and role-prefixed hashing.
- [x] Add Swift local report counts for HTTP/API client surfaces.
- [x] Add public-safe checked-in sample fixture.
- [x] Add smoke assertions for redaction across artifacts.
- [x] Add smoke assertions for fact ID stability across output paths.
- [x] Wire the HTTP/API smoke assertions into
  `src/swift/Sources/tracemap-swift-smoke-tests/main.swift` or a source file
  compiled by that executable.
- [x] Add smoke assertions for dynamic/interpolated URL gaps.
- [x] Add smoke assertions for no default method inference.
- [x] Add smoke assertions that static path plus unknown/dynamic method emits a
  gap and no shared path-projected `HttpCallDetected`.
- [x] Add smoke assertions for Moya partial target evidence.
- [x] Add smoke assertions for Alamofire method normalization and unsupported
  argument gaps.
- [x] Add smoke assertions for force-unwrap URL construction, async URLSession,
  multiple URLRequest variables, query redaction, and Tier3 evidence ceiling.
- [x] Add smoke assertions for query absence, Alamofire dynamic URL gaps,
  URLRequest shadowing safety, and shared-reader naming safety.
- [x] Add smoke assertions for local report counts.
- [x] Validate shared export/combine/report readers over the generated Swift
  HTTP sample index.
- [x] Verify the shared .NET reader does not filter or skip Swift-emitted
  `HttpCallDetected` facts from the combined Swift index.
- [x] Assert the combined report contains an `http-client` surface with the
  expected static fixture `normalizedPathKey`.
- [x] Ensure Swift API names use `swiftApiName`, not `methodName`, and Swift
  client categories use `swiftClientKind`, not shared-reader `surfaceKind`.
- [x] Update `docs/VALIDATION.md` with the Swift HTTP sample command.
- [x] Update the Swift validation matrix/checklist entry to mention HTTP/API
  client surface artifact checks.
- [x] Update this implementation state with branch, scope decisions,
  validation, and follow-ups.
- [x] Run `swift build --package-path src/swift`.
- [x] Run `swift run --package-path src/swift tracemap-swift-smoke-tests`.
- [x] Run the checked-in Swift HTTP/API sample scan.
- [x] Run `dotnet build src/dotnet/TraceMap.sln`.
- [x] Run `dotnet test src/dotnet/TraceMap.sln`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `git diff --check`.
- [x] Open an implementation PR to `dev`, complete the PR review loop, and
  merge when ACK returns `merge_ready`.

## Follow-Ups Out Of Scope

- Cross-repo client/server endpoint alignment from Swift mobile apps to
  backend services.
- Runtime network capture or simulator/device instrumentation.
- Deep semantic resolution of builder functions, request middleware,
  dependency injection, Combine/async chains, or protocol dispatch.
- Public site claims beyond static evidence-backed Swift HTTP/API discovery.
