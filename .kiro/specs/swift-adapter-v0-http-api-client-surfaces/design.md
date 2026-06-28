# Swift Adapter v0 HTTP And API Client Surfaces Design

## Overview

This slice adds conservative static Swift outbound HTTP/API client evidence. It
extends the SwiftSyntax-backed extractor with a small, deterministic recognizer
for common URL/URLRequest/URLSession, Alamofire, and Moya shapes. It records
only visible source evidence and emits explicit gaps when values are dynamic or
unsupported.

The implementation should favor a narrow, well-tested first cut over broad
Swift inference. It should be useful on real code while staying honest about
runtime uncertainty.

## Data Model

### Fact Types

- `HttpCallDetected`
  - Shared TraceMap outbound HTTP/API client evidence.
  - Evidence tier: `Tier3SyntaxOrTextual` for this Swift v0 slice; future
    structural promotion requires deterministic project/library backing.
  - Reusing the shared fact type keeps Swift HTTP evidence visible to existing
    endpoint, combine, path, reverse, reducer, and route-flow readers that
    already understand client HTTP surfaces.
- `AnalysisGap`
  - Gap kinds for dynamic URLs, unsupported method/path shapes, unsafe values,
    and framework-specific uncertainty.

### Candidate Properties

Allowed properties:

- `swiftClientKind`: `urlsession`, `urlrequest`, `alamofire`, `moya`, or
  `other-client`. This is Swift-local metadata for local reports and must not
  be confused with combined-reader `surfaceKind`, which is derived from
  `factType=HttpCallDetected` as `http-client`.
- `httpMethod`: uppercase method when static and safe.
- `methodStatus`: `present`, `unknown`, or `dynamic`.
- `swiftApiName`: framework call name or declaration name when visible. Do not
  use `methodName` for Swift API names because shared readers may treat
  `methodName` as an HTTP verb fallback.
- `normalizedPathKey`: shared normalized path shape, when available.
- `pathStatus`: `present`, `dynamic`, `unknown`, or `unsafe-omitted`.
- `urlHash`: role-prefixed SHA-256 hash of raw URL/string evidence when
  present.
- `hostHash`: role-prefixed SHA-256 hash of host when present.
- `queryStatus`: `absent`, `present-omitted`, or `unknown`.
- `sourceLocationStatus`: `literal`, `dynamic`, `unknown`, or
  `unsafe-omitted`.
- `framework`: `foundation`, `alamofire`, `moya`, or `unknown`.
- `supportingFactIds`: optional sorted supporting Swift call/declaration fact
  IDs when available.

Forbidden properties:

- Raw URL, host, query, credentials, token, header, body, local path, source
  snippet, private repo/org/team name, or raw request payload.

## Rule IDs

Add these rules to `rules/rule-catalog.yml`:

- `swift.http.urlsession.v1`
  - Emits `HttpCallDetected` for URLSession/URLRequest/Foundation
    HTTP evidence.
  - Required properties: `swiftClientKind`, `framework`,
    `methodStatus`, `pathStatus`, and at least one of `normalizedPathKey`,
    `urlHash`, or `sourceLocationStatus=dynamic`.
  - False positives: commented/string-literal source shapes missed by the
    masker, overloaded helper wrappers that resemble Foundation request
    shapes, and test-only fixtures.
  - False negatives: builder functions, dependency injection, URLComponents
    assembled across scopes, protocol abstractions, Objective-C bridging, and
    generated code.
- `swift.http.client-library.v1`
  - Emits `HttpCallDetected` for library-shaped evidence such as
    Alamofire/Moya static calls.
  - Required properties: `swiftClientKind`, `framework`,
    `methodStatus`, `pathStatus`, and library-specific safe metadata when
    present.
  - False positives: user-defined APIs named like common clients and source
    shapes inside examples/tests.
  - False negatives: custom clients, wrappers, plugins, request adapters,
    generated clients, or call chains not matching the documented shapes.
- `swift.http.analysis-gap.v1`
  - Emits `AnalysisGap` for dynamic, unsafe, ambiguous, unsupported, or
    reduced-coverage HTTP/API client extraction boundaries.
  - Required properties: closed-vocabulary `gapKind`, file path, line span,
    evidence tier, and safe message.
  - False positives: conservative gaps for values that would be resolvable by a
    future semantic or data-flow pass.
  - False negatives: dynamic network behavior hidden behind runtime-only paths,
    binary frameworks, generated sources not present in the repo, or reflection.

Each rule must document that evidence is static source evidence only and does
not prove runtime network traffic, authentication, server existence, endpoint
reachability, production use, or impact.

## Extraction Strategy

### Foundation URL/URLRequest/URLSession

Recognize narrow syntax shapes:

- `URL(string: "literal")`
- `URLRequest(url: URL(string: "literal")!)`
- `var request = URLRequest(...)` followed nearby by
  `request.httpMethod = "POST"`
- `URLSession.shared.dataTask(with: requestOrUrl)`
- `URLSession.shared.data(from: url)`

For v0, method association is limited to a deterministic same-function rule.
The implementation uses syntax-local variable-name identity only; it does not
resolve shadowing semantically. A method may be associated only when:

- the `URLRequest` receiver variable is declared in the same function body;
- the `httpMethod` assignment targets the same variable name;
- the assignment appears before the first call site that passes that variable
  to a URLSession API;
- the assignment is the first and only static assignment to that variable name
  before that call site.

If the request is returned from the function, passed to an unknown helper before
a URLSession call, shadowed, assigned multiple method values, or has no static
method literal before the first request use, record `methodStatus=unknown` or
emit a method gap rather than inferring `GET`.

Shared projection gate: Swift v0 MUST NOT emit a shared `HttpCallDetected` with
`normalizedPathKey` unless a static `httpMethod` is also present. Existing
combined readers treat missing methods as broad `ANY`-style evidence, which can
overmatch server routes. If the path is static but the method is unknown,
interpolated, or ambiguous, emit an `AnalysisGap` and optional role-prefixed
hash evidence only; do not emit a projected path surface.

The first implementation does not chase URL literals through intermediate
variables. A pattern such as `let url = URL(string: "...")!` followed by
`var request = URLRequest(url: url)` may still emit request/method evidence, but
the path is treated as dynamic or unknown with a gap unless the literal appears
inside the recognized request/call expression. This avoids an implicit data-flow
pass in v0.

### Alamofire

Recognize narrow shapes:

- `AF.request("literal", method: .get)`
- `Alamofire.request("literal", method: .post)`
- `Session.default.request("literal", method: .put)`

For enum method cases, store the normalized method when the case is one of the
standard HTTP verbs. For variables/builders/closures, emit a gap.

### Moya

Recognize source-local `TargetType`-shaped declarations:

- `var baseURL: URL { URL(string: "literal")! }`
- `var path: String { "/literal" }`
- `var method: Moya.Method { .get }`

Emit evidence when static values are visible in the same declaration. Combining
`baseURL` and `path` across separate properties must not synthesize a full URL
or upgrade beyond `Tier3SyntaxOrTextual`.

Moya target output cases:

| baseURL evidence | path evidence | Output |
|---|---|---|
| static | static, method static | Emit `HttpCallDetected` with `normalizedPathKey` from the path, static `httpMethod`, `pathStatus=present`, role-prefixed `hostHash`/`urlHash` when available, and a companion `AnalysisGap` with `gapKind=swift-http-moya-target-partial` because full route reachability/join is not proven. |
| dynamic/missing | static, method static | Emit `HttpCallDetected` with `normalizedPathKey`, static `httpMethod`, `pathStatus=present`, and a companion `AnalysisGap` with `gapKind=swift-http-moya-target-partial`. |
| any | static, method dynamic/missing | Emit a gap only; do not emit a shared `HttpCallDetected` with `normalizedPathKey` because the shared reader would overmatch unknown methods. |
| static | dynamic/missing | Emit a gap only; do not emit a definitive path surface. |
| dynamic/missing | dynamic/missing | Emit one `swift-http-moya-target-partial` gap for the declaration boundary; do not emit a definitive path surface. |

Missing or dynamic method evidence emits a method-specific gap when the method
expression itself is visible but unsupported. It must not create shared
path-projected HTTP evidence.

## URL Normalization

Use deterministic URL parsing when possible:

- Preserve only a safe `normalizedPathKey` when a path is visible and safe.
- Follow the shared language-adapter normalization contract for
  `normalizedPathKey`: lowercased literal segments, collapsed slashes,
  trailing slash removed except root, query/fragment stripped, route/value
  segments normalized to `{}`, and optional segments to `{?}` where such
  syntax is explicit.
- Do not emit host, scheme, query, fragment, credentials, or token values.
- Hash raw URL and host when present using UTF-8 lowercase hex SHA-256 over a
  role/context prefix:
  - `urlHash = sha256(\"swift.http|url|\" + rawValue)` full 64 lowercase hex.
  - `hostHash = sha256(\"swift.http|host|\" + hostValue)` full 64 lowercase hex.
  - Other unsafe value hashes in this slice must use the same
    `swift.http|<role>|<value>` prefix shape.
  - Stable fact IDs may include these role-prefixed hashes, but must never
    include raw unsafe values.
- If the path itself appears unsafe or private, omit it and set
  `pathStatus=unsafe-omitted`.

Status vocabulary:

| Property | Value | Meaning |
|---|---|---|
| `pathStatus` | `present` | A static literal path/URL was parsed and a safe `normalizedPathKey` is populated. |
| `pathStatus` | `dynamic` | A path/URL expression is visible but non-literal, interpolated, concatenated, builder-driven, or otherwise dynamic. |
| `pathStatus` | `unsafe-omitted` | A literal path exists but failed safety checks; only hashes/statuses may be emitted. |
| `pathStatus` | `unknown` | This extraction depth cannot determine whether path evidence exists. |
| `queryStatus` | `absent` | A parsed static URL has no query string. |
| `queryStatus` | `present-omitted` | A parsed static URL has a query string and the raw query is intentionally omitted. |
| `queryStatus` | `unknown` | Query presence cannot be determined from the visible evidence. |

Path safety: a path is safe to render only when every segment is a short,
non-empty ASCII slug after shared normalization and does not look like a host,
IP address, UUID, credential/token, email address, local/absolute path, secret
word, or high-entropy value. Unsafe paths set `pathStatus=unsafe-omitted` and
may contribute only role-prefixed hashes/statuses.

## Gaps

Recommended gap kinds:

- `swift-http-url-dynamic`
- `swift-http-method-dynamic`
- `swift-http-method-unknown-projection-omitted`
- `swift-http-path-unsafe-omitted`
- `swift-http-client-shape-unsupported`
- `swift-http-moya-target-partial`
- `swift-http-source-ambiguous`

Gap messages must be closed-vocabulary and safe. They may include hashes,
counts, and safe rule names, but not snippets or raw URLs.

## Reporting

Swift local report should include a `## Swift HTTP/API Client Surfaces` section
with counts by `swiftClientKind`, `framework`, `methodStatus`, and
`pathStatus`.

Shared .NET readers should not need schema changes for v0 because Swift emits
the shared `HttpCallDetected` fact shape. The facts and gaps should remain
readable through existing `facts`, `export`, `combine`, and generic report
flows. Any future path/reverse/endpoint alignment change must add dedicated
tests before claiming alignment behavior beyond static client evidence.

The implementation must still verify the shared reader path in this slice,
because `HttpCallDetected` is new to the Swift adapter even though it is not new
to TraceMap.

Required shared-reader assertion: after `combine` and `report`, the generated
combined dependency report must contain at least one `http-client` dependency
surface with the expected `normalizedPathKey` from the static fixture. This is
an assertion, not just a successful command exit.

## Sample Fixture

Add `samples/swift-http-api-client-surfaces/` with:

- Foundation URLSession/URLRequest literal examples.
- Alamofire-style static request examples without requiring Alamofire to
  compile.
- Moya-style target type examples without requiring Moya to compile.
- Dynamic/interpolated URL examples that emit gaps.
- Unsafe host/query examples proving raw URL parts are omitted or hashed.

## Required Smoke Assertions

- Redaction: secret-like host, query string, request header, and bearer-token
  fixture values do not appear in `facts.ndjson`, `index.sqlite`, or
  `report.md`.
- Fact identity: identical scans to different output paths produce identical
  `HttpCallDetected` IDs.
- Shared-reader round trip: the new fact survives `export --format json`,
  `combine`, and `report`.
- Combined projection: the combined report contains at least one `http-client`
  dependency surface with the expected `normalizedPathKey`.
- Method safety: `URLRequest` or Moya evidence without an unambiguous static
  `httpMethod` emits `swift-http-method-unknown-projection-omitted` or
  `swift-http-method-dynamic` and does not emit `HttpCallDetected` with
  `normalizedPathKey`; no default `GET` or `ANY` is inferred.
- Tier safety: every Swift v0 `HttpCallDetected` fact is
  `Tier3SyntaxOrTextual`; no Swift HTTP fact is promoted above syntax tier.
- Force unwrap: `URL(string: "literal")!` produces the same kind of evidence as
  non-force-unwrap literal URL construction.
- Async URLSession: `URLSession.shared.data(from:)` is covered by at least one
  fixture assertion.
- Multiple requests: two URLRequest variables in the same function with
  different static methods produce distinct stable facts.
- Dynamic URL safety: interpolated/environment/concatenated URL evidence emits
  `AnalysisGap` with a closed `gapKind`, not a definitive endpoint.
- Alamofire dynamic URL: an Alamofire request whose URL argument is a variable
  emits a gap and no definitive `HttpCallDetected`.
- Moya partial: static path with dynamic base URL and static method emits a path
  fact plus `swift-http-moya-target-partial`.
- Moya unknown method: static path with missing/dynamic method emits a gap only
  and no shared path-projected `HttpCallDetected`.
- Moya static target: static baseURL, path, and method emits a path fact plus
  `swift-http-moya-target-partial` and remains Tier3.
- Alamofire verbs: static enum verbs normalize to standard uppercase methods;
  builder/closure/non-standard arguments emit gaps.
- Query redaction: a static URL with a query emits
  `queryStatus=present-omitted` and no raw query text.
- Query absence: a static URL without a query emits `queryStatus=absent`.
- Local report counts cover `swiftClientKind`, `framework`, `methodStatus`, and
  `pathStatus`.
- Shared-reader naming: Swift-local `swiftClientKind` and `swiftApiName` values
  do not appear as HTTP methods or combined `surfaceKind` values.
- Shadowing: same-named `URLRequest` variables in sequential nested scopes do
  not receive cross-scope method associations.

## Validation Commands

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

## Limitations

- Syntax-only evidence does not prove runtime network traffic.
- Dynamic base URLs, path builders, request interceptors, adapters, dependency
  injection, feature flags, environment switching, async scheduling, Combine,
  retries, middleware, authentication, and server availability are not proven.
- Moya/Alamofire recognition is source-shape based and does not require package
  installation.
- Path normalization is conservative and may omit values rather than overclaim.
