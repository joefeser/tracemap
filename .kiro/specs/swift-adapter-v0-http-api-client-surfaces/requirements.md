# Swift Adapter v0 HTTP And API Client Surfaces Requirements

## Introduction

TraceMap needs a conservative Swift adapter slice that extracts static HTTP and
API client surface evidence from checked-in Swift source. This spec covers
GitHub issue [#383](https://github.com/joefeser/tracemap/issues/383), a child
of the Swift v0 runway issue
[#377](https://github.com/joefeser/tracemap/issues/377).

This is deterministic static evidence only. The implementation must not execute
app code, run Xcode builds, restore packages, launch simulators/devices, make
network requests, inspect runtime state, or claim runtime endpoint reachability.

## Source Material

- GitHub issue #383: Swift adapter v0 HTTP and API client surfaces.
- GitHub issue #377: Swift adapter v0 runway.
- Existing Swift specs:
  - `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
  - `.kiro/specs/swift-adapter-v0-inventory-project-discovery/`
  - `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/`
  - `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/`
  - `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `rules/rule-catalog.yml`
- `src/swift/`

## Requirements

### Requirement 1: URLSession and URLRequest Surface Evidence

**User Story:** As an engineer reviewing a Swift codebase, I want TraceMap to
record static outbound HTTP evidence when source visibly constructs
`URLRequest`, `URL`, or `URLSession` calls, so I can find likely client API
touchpoints without runtime claims.

#### Acceptance Criteria

1. WHEN Swift source contains a deterministic string-literal URL passed to
   `URL(string:)`, `URLRequest(url:)`, `URLSession.shared.dataTask(with:)`, or
   `URLSession.shared.data(from:)` THEN the adapter SHALL emit the shared
   `HttpCallDetected` fact with rule ID, evidence tier, file path, line span,
   method/path metadata when available, safe hashes, and extractor version.
2. WHEN source sets `httpMethod` to a static string literal THEN the adapter
   SHALL record the normalized method.
3. WHEN method evidence is absent THEN the adapter SHALL record
   `methodStatus=unknown` and MUST NOT infer a default.
4. WHEN URL evidence contains a scheme/host/query/token/path that is unsafe to
   render THEN raw values SHALL be omitted or hashed; only normalized safe path
   shape MAY be rendered.
5. WHEN URL construction is dynamic, interpolated, concatenated, environment
   driven, or not confidently parseable THEN the adapter SHALL emit an
   `AnalysisGap` instead of a definitive endpoint surface.

### Requirement 2: Common Client Library Surface Evidence

**User Story:** As a maintainer of mobile apps using common Swift networking
libraries, I want TraceMap to identify static Alamofire/Moya-style API call
surfaces where evidence is visible, so dependency and endpoint analysis can
include practical Swift apps.

#### Acceptance Criteria

1. WHEN source contains static `AF.request`, `Alamofire.request`, or
   `Session.request` calls with a string-literal URL/path THEN the adapter
   SHALL emit shared `HttpCallDetected` evidence with Swift-specific framework
   metadata.
2. WHEN source contains Moya target-type evidence such as `path`, `method`, or
   `baseURL` properties with static literals THEN the adapter SHALL emit
   static API surface evidence tied to the declaring file/span.
3. WHEN library call arguments use enum cases, builder functions, closures,
   variables, or other dynamic values that cannot be resolved by this v0 slice
   THEN the adapter SHALL emit reduced-coverage gaps rather than infer values.
4. The adapter MUST NOT require Alamofire, Moya, or other packages to be
   installed.

### Requirement 3: Fact Shape, Rules, and Safety

**User Story:** As a downstream report/query consumer, I want Swift HTTP
surfaces to follow TraceMap fact contracts and safety rules, so reports remain
deterministic and public-safe.

#### Acceptance Criteria

1. Each emitted surface fact SHALL include a documented rule ID and evidence
   tier.
2. Each gap SHALL include a documented rule ID, gap kind, file path, line span,
   and safe message.
3. Fact IDs SHALL be stable across output path changes and unaffected by raw
   unsafe values. Hashes used by fact identity SHALL be UTF-8 lowercase hex
   SHA-256 with explicit role prefixes.
4. The adapter SHALL not store raw source snippets by default.
5. Raw URLs, hostnames, query strings, credentials, bearer tokens, API keys,
   local absolute paths, private labels, and raw request bodies SHALL NOT be
   emitted.
6. The rule catalog SHALL document limitations for each new Swift HTTP/API
   rule.

### Requirement 4: Reporting and Shared Reader Compatibility

**User Story:** As a TraceMap user, I want Swift HTTP evidence to survive scan,
export, combine, and report workflows, so I can inspect it alongside other
language facts without special tooling.

#### Acceptance Criteria

1. The local Swift `report.md` SHALL summarize Swift HTTP/API client evidence
   counts by source kind, method status, and URL/path status.
2. The generated `index.sqlite` SHALL remain readable by existing `tracemap
   export`, `tracemap combine`, and `tracemap report` commands.
3. Combined reports MAY project Swift HTTP evidence through the existing shared
   HTTP client surface flow when `HttpCallDetected` fields are populated; they
   MUST NOT imply complete route alignment or server endpoint reachability.
4. Existing Swift smoke tests SHALL include HTTP/API client fixtures and at
   least one dynamic/unsupported reduced-coverage path.

### Requirement 5: Validation

**User Story:** As a reviewer, I want focused validation for the Swift HTTP
slice, so we can merge with evidence rather than broad claims.

#### Acceptance Criteria

1. `swift build --package-path src/swift` SHALL pass.
2. `swift run --package-path src/swift tracemap-swift-smoke-tests` SHALL pass.
3. A checked-in Swift HTTP/API sample SHALL scan successfully.
4. Shared reader validation over the generated Swift index SHALL pass.
5. `dotnet build src/dotnet/TraceMap.sln` and `dotnet test
   src/dotnet/TraceMap.sln` SHALL pass or be explicitly deferred with evidence.
6. `./scripts/check-private-paths.sh` and `git diff --check` SHALL pass.

## Non-Goals

- Runtime network tracing.
- Server endpoint matching or proof of reachability.
- Executing package managers, app code, tests, Xcode, simulators, or devices.
- Full Swift semantic resolution, overload selection, protocol witness
  selection, dependency injection, Combine/async scheduling, or branch
  feasibility.
- Raw request bodies, raw headers, raw query strings, secrets, snippets, or
  source text capture by default.
- Vulnerability, license, authentication, authorization, production-use, or
  impact conclusions.
