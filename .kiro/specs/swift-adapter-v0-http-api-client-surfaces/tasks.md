# Swift Adapter v0 HTTP And API Client Surfaces Tasks

## Spec And Review

- [x] Create the Kiro spec for issue #383.
- [x] Run Opus spec review.
- [x] Run Sonnet spec review.
- [x] Patch Medium+ review findings or document explicit non-actionable
  dispositions.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [ ] Open a spec PR to `dev` and complete the PR review loop.

## Implementation

- [ ] Add Swift HTTP/API rule catalog entries and limitations.
- [ ] Add shared `HttpCallDetected` fact emission for Swift HTTP/API evidence.
- [ ] Add Foundation URL/URLRequest/URLSession static literal extraction.
- [ ] Add method extraction for visible static `httpMethod` assignments.
- [ ] Add Alamofire-style static request extraction.
- [ ] Add Moya target-type static path/method/baseURL extraction.
- [ ] Add dynamic/ambiguous/unsafe HTTP analysis gaps.
- [ ] Add shared `normalizedPathKey` normalization and role-prefixed hashing.
- [ ] Add Swift local report counts for HTTP/API client surfaces.
- [ ] Add public-safe checked-in sample fixture.
- [ ] Add smoke assertions for redaction across artifacts.
- [ ] Add smoke assertions for fact ID stability across output paths.
- [ ] Wire the HTTP/API smoke assertions into
  `src/swift/Sources/tracemap-swift-smoke-tests/main.swift` or a source file
  compiled by that executable.
- [ ] Add smoke assertions for dynamic/interpolated URL gaps.
- [ ] Add smoke assertions for no default method inference.
- [ ] Add smoke assertions that static path plus unknown/dynamic method emits a
  gap and no shared path-projected `HttpCallDetected`.
- [ ] Add smoke assertions for Moya partial target evidence.
- [ ] Add smoke assertions for Alamofire method normalization and unsupported
  argument gaps.
- [ ] Add smoke assertions for force-unwrap URL construction, async URLSession,
  multiple URLRequest variables, query redaction, and Tier3 evidence ceiling.
- [ ] Add smoke assertions for query absence, Alamofire dynamic URL gaps,
  URLRequest shadowing safety, and shared-reader naming safety.
- [ ] Add smoke assertions for local report counts.
- [ ] Validate shared export/combine/report readers over the generated Swift
  HTTP sample index.
- [ ] Verify the shared .NET reader does not filter or skip Swift-emitted
  `HttpCallDetected` facts from the combined Swift index.
- [ ] Assert the combined report contains an `http-client` surface with the
  expected static fixture `normalizedPathKey`.
- [ ] Ensure Swift API names use `swiftApiName`, not `methodName`, and Swift
  client categories use `swiftClientKind`, not shared-reader `surfaceKind`.
- [ ] Update `docs/VALIDATION.md` with the Swift HTTP sample command.
- [ ] Update the Swift validation matrix/checklist entry to mention HTTP/API
  client surface artifact checks.
- [ ] Update this implementation state with branch, scope decisions,
  validation, and follow-ups.
- [ ] Run `swift build --package-path src/swift`.
- [ ] Run `swift run --package-path src/swift tracemap-swift-smoke-tests`.
- [ ] Run the checked-in Swift HTTP/API sample scan.
- [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `git diff --check`.
- [ ] Open an implementation PR to `dev`, complete the PR review loop, and
  merge when ACK returns `merge_ready`.

## Follow-Ups Out Of Scope

- [ ] Cross-repo client/server endpoint alignment from Swift mobile apps to
  backend services.
- [ ] Runtime network capture or simulator/device instrumentation.
- [ ] Deep semantic resolution of builder functions, request middleware,
  dependency injection, Combine/async chains, or protocol dispatch.
- [ ] Public site claims beyond static evidence-backed Swift HTTP/API discovery.
