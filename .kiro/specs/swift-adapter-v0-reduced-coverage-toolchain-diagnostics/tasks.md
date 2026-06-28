# Swift Adapter v0 Reduced Coverage And Toolchain Diagnostics Tasks

## Implementation Plan

### Phase 1: Rule Catalog And Gap Vocabulary

- [x] 1.1 Add or update rule-catalog entries for Swift toolchain diagnostics
  and reduced-coverage gaps, with evidence tiers and documented limitations.
- [x] 1.2 Define closed `gapKind` values for missing tools, tool timeouts,
  SourceKit/Xcode unavailability, macro expansion, conditional compilation,
  Objective-C bridging, dynamic selectors, storyboard/nib wiring, generated
  code, reflection, protocol dispatch, and missing module context.
- [x] 1.3 Add tests proving every emitted diagnostic/gap rule ID is cataloged.
- [x] 1.4 Add tests proving diagnostic/gap facts do not emit raw snippets,
  shell output, local absolute paths, environment variables, URLs, remotes,
  credentials, or private labels.

### Phase 2: Toolchain Diagnostic Normalization

- [x] 2.1 Inventory current Swift toolchain probe behavior and reuse existing
  bounded probes where possible.
- [x] 2.2 Normalize probe status values to `available`, `not-found`,
  `timeout`, `unsupported`, `not-probed`, or `error-redacted`.
- [x] 2.3 Ensure probe results are non-mutating, timeout-bounded, and safe when
  tools print unexpected output.
- [x] 2.4 Add host-independent tests for unavailable and timeout statuses.
- [x] 2.5 Ensure missing optional tools do not prevent basic Swift scan output.

### Phase 3: Unsupported Feature Gap Extraction

- [x] 3.1 Add source-local gap detection for macro usage or macro expansion
  boundaries.
- [x] 3.2 Add gap detection for conditional compilation blocks that reduce
  confidence in declaration/call/UI/storage interpretation.
- [x] 3.3 Add gap detection for Objective-C bridging markers and dynamic
  selectors.
- [x] 3.4 Add gap detection for storyboard/nib references without claiming UI
  wiring.
- [x] 3.5 Add gap detection for generated-code markers, reflection-like APIs,
  protocol dispatch uncertainty, and missing module context.
- [x] 3.6 Ensure source gap detection ignores comments and string literals where
  appropriate.

### Phase 4: Coverage Aggregation And Reports

- [x] 4.1 Aggregate Swift diagnostics into manifest/report coverage summaries.
- [x] 4.2 Add a local `report.md` section for Swift diagnostics and coverage
  counts.
- [x] 4.3 Preserve useful syntax facts while adding companion reduced-coverage
  gaps; do not downgrade unrelated facts.
- [x] 4.4 Verify combined/export/report readers preserve diagnostics without
  projecting them into dependencies, endpoints, paths, impacts, or reachability.

### Phase 5: Fixtures And Validation

- [x] 5.1 Add or extend a checked-in Swift reduced-coverage fixture.
- [x] 5.2 Add smoke assertions for unsupported feature gaps and toolchain
  diagnostics.
- [x] 5.3 Verify deterministic fact ordering and stable IDs for repeated scans.
- [x] 5.4 Run `swift build --package-path src/swift`.
- [x] 5.5 Run `swift run --package-path src/swift tracemap-swift-smoke-tests`.
- [x] 5.6 Run a Swift reduced-coverage sample scan.
- [x] 5.7 Run shared reader validation over the generated Swift index:
  `export`, `combine`, and `report`.
- [x] 5.8 Run `dotnet build src/dotnet/TraceMap.sln`.
- [x] 5.9 Run `dotnet test src/dotnet/TraceMap.sln`.
- [x] 5.10 Run `./scripts/check-private-paths.sh`.
- [x] 5.11 Run `git diff --check`.

## Follow-Ups Out Of Scope

- SourceKit/compiler semantic enrichment.
- Storyboard/nib wiring extraction.
- SwiftUI/UIKit surface extraction.
- Swift storage/data surface extraction.
- Runtime instrumentation or simulator/device checks.
