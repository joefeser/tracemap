# Swift HTTP Source Context Implementation State

Status: implemented
Branch: `codex/swift-http-source-context`
Public claim level: shipped on dev after PR merge.

## Scope

- `HttpCallDetected` facts from Swift HTTP/API extraction now attach to the
  innermost source-local Swift declaration span when SwiftSyntax evidence is
  available.
- The fact keeps the existing HTTP method/path identity and `Tier3SyntaxOrTextual`
  evidence tier.
- Source context is emitted as safe metadata only:
  `sourceContextStatus`, `containingDeclarationSymbolId`,
  `containingDeclarationDisplayName`, and `containingDeclarationKind`.

## Safety

- This does not claim runtime traffic, endpoint reachability, semantic compiler
  call resolution, async scheduling, branch feasibility, dependency-injection
  behavior, or user reachability.
- If no containing declaration is available, the fact is labeled
  `sourceContextStatus=unresolved` and no source symbol is invented.
- When nested declarations share the same physical line and therefore the same
  line-only span, context selection prefers deeper containing-declaration
  ancestry before falling back to symbol ID ordering.
- HTTP source-context lookup indexes declarations by file once per projection
  and selects the best containing declaration in a single pass per HTTP record.

## Validation

- `swift run --package-path src/swift tracemap-swift-smoke-tests` passed.
- Full Swift adapter validation matrix from `docs/VALIDATION.md` passed under
  `/tmp/tracemap-swift-validation-445`, including:
  - `swift build --package-path src/swift`;
  - checked-in sample scans for `swift-package-basic`,
    `swift-dependency-surfaces`, `swift-http-api-client-surfaces`,
    `swift-ui-surfaces`, `swift-storage-data-surfaces`,
    `swift-diagnostics-reduced`, `swift-metadata-reduced`,
    `swift-metadata-unsupported`, and `no-swift`;
  - required artifact existence checks;
  - `tracemap export`, `tracemap combine`, and `tracemap report` checks for
    Swift package and storage sample indexes.
- Focused checked-in HTTP sample scan passed:
  `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-http-api-client-surfaces --out /tmp/tracemap-swift-http-context-check`.
- SQLite verification over the focused scan found five `HttpCallDetected` facts
  with `sourceContextStatus=containing-declaration` and five with indexed
  non-empty `source_symbol`.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 697 tests.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
