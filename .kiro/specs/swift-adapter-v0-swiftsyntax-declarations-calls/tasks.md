# SwiftSyntax Declarations and Basic Call Facts Tasks

Issue: [#380](https://github.com/joefeser/tracemap/issues/380)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Implementation PR: [#411](https://github.com/joefeser/tracemap/pull/411)

Status: Swift v0 implemented. The original checklist included future semantic
enrichment and broad downstream compatibility items that are not v0
requirements. Those are recorded below as deferred follow-ups rather than
active unchecked tasks.

## Implemented V0 Scope

- [x] Confirm implementation branch, issue linkage, and prerequisite Swift
  scaffold/inventory slices.
- [x] Select and document the SwiftSyntax package/toolchain version.
- [x] Keep v0 evidence at `Tier3SyntaxOrTextual` for SwiftSyntax facts and
  `Tier4Unknown` for gaps.
- [x] Exclude SourceKit, sourcekit-lsp, SwiftPM semantic loading, Xcode build
  execution, macro expansion execution, simulator/device inspection, and
  runtime discovery from this slice.
- [x] Add rule-catalog entries for emitted Swift declaration, import, call,
  construction, source-root, and gap rule IDs.
- [x] Add catalog-gate coverage proving uncataloged Swift rule IDs fail
  validation.
- [x] Emit Swift-specific syntax-backed fact types instead of overclaiming
  shared semantic fact types such as `MethodInvoked` or `ObjectCreated`.
- [x] Add checked-in Swift fixtures for declarations, imports, calls,
  construction candidates, conditional compilation, parser diagnostics,
  optional chaining, unsupported shapes, and public-safety sentinels.
- [x] Add deterministic output and scan-ID stability assertions.
- [x] Wire SwiftSyntax parsing into the Swift adapter without running scanned
  app code or requiring app builds.
- [x] Map SwiftSyntax source locations to repo-relative file paths and stable
  one-based line spans.
- [x] Emit bounded `AnalysisGap` facts for recoverable parse diagnostics and
  unsupported syntax shapes.
- [x] Emit Swift file inventory, source-root, import, declaration, call, and
  construction candidate facts with safe properties.
- [x] Attach deterministic module/package/target context when inventory proves
  it, and emit module-context gaps otherwise.
- [x] Populate shared `symbols`, `symbol_occurrences`, `fact_symbols`,
  `call_edges`, and `object_creations` rows only when backing facts exist.
- [x] Preserve supporting fact IDs for derived rows.
- [x] Cap Swift syntax call/navigation/relationship rows at
  `Tier3SyntaxOrTextual`.
- [x] Add report sections for declarations, calls, construction candidates,
  module context, gaps, evidence tiers, and limitations.
- [x] Add forbidden-wording coverage so reports/exports do not claim runtime
  target, executed calls, injected services, rendered UI, reachability, or
  impact.
- [x] Update validation/acceptance docs where exact Swift commands and behavior
  became real.
- [x] Run Swift and shared validation:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Deferred Beyond Swift V0

- SourceKit/sourcekit-lsp, SwiftPM semantic loading, compiler APIs, Xcode build
  execution, compiler-plugin execution, and macro expansion.
- Full path-casing portability fixtures across filesystems with different
  casing behavior.
- Same-file or same-module call-target matching beyond syntax candidate rows.
- Tier2 promotion for declaration/call facts from richer package target
  identity.
- Reducer-specific contract-delta behavior for Swift syntax-only facts beyond
  the existing tier and wording safeguards.
- Runtime call targets, overload selection, protocol witness resolution,
  Objective-C selector binding, dependency injection, branch feasibility, and
  dynamic dispatch approximation.
