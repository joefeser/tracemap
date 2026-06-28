# Swift Adapter v0 Reduced Coverage And Toolchain Diagnostics Implementation State

Status: `implemented`

## Branches

- Spec branch: `codex/spec-swift-toolchain-diagnostics`
- Implementation branch: `codex/implement-swift-toolchain-diagnostics`
- Base: `dev`

## Issue Links

- Parent: https://github.com/joefeser/tracemap/issues/377
- This spec: https://github.com/joefeser/tracemap/issues/386

## Scope

This spec is for structured Swift reduced-coverage and toolchain diagnostics.
It does not implement SourceKit semantic enrichment, Xcode builds, package
restores, simulator/device execution, storyboard wiring, runtime proof, or
production impact claims.

Implemented behavior:

- Emits `SwiftToolchainDiagnostic` facts with safe status categories for
  relevant Swift, SourceKit/sourcekit-lsp, Xcode, CocoaPods, and Carthage
  probes.
- Emits companion `AnalysisGap` facts for unavailable or timed-out relevant
  tool probes.
- Adds host-independent `TRACEMAP_SWIFT_TOOL_STATUS_OVERRIDES` test support for
  forced diagnostic statuses.
- Emits `swift.reduced-coverage.gap.v1` gaps for macro expansion boundaries,
  conditional compilation, Objective-C bridging markers, dynamic selectors,
  storyboard/xib wiring, generated-code markers, reflection-style lookups, and
  protocol dispatch uncertainty.
- Adds a local Swift report section for diagnostics and reduced-coverage gap
  counts.
- Adds checked-in sample `samples/swift-diagnostics-reduced`.
- Updates `docs/VALIDATION.md` with the diagnostics sample scan.

## Safe Claims

- Swift v0 can report static diagnostic gaps and tool availability categories
  to explain reduced coverage.
- Diagnostics are evidence-backed, rule-ID-backed, and public-safe.
- Missing optional tools do not prevent basic file-based Swift scan output.

## Claims To Avoid

- TraceMap proves Swift builds.
- TraceMap proves Swift runtime behavior.
- TraceMap resolves Objective-C bridging, macro expansion, storyboard wiring,
  protocol dispatch, or generated code.
- TraceMap performs AI/LLM/vector analysis.

## Expected Validation

- `swift build --package-path src/swift` - passed.
- `swift run --package-path src/swift tracemap-swift-smoke-tests` - passed.
- `TRACEMAP_SWIFT_TOOL_STATUS_OVERRIDES='swift=timeout,sourcekit-lsp=not-found,xcodebuild=error-redacted' swift run --package-path src/swift tracemap-swift scan --repo samples/swift-diagnostics-reduced --out /tmp/tracemap-swift-diagnostics-reduced` - passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-diagnostics-reduced/index.sqlite --out /tmp/tracemap-swift-diagnostics-export --format json` - passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-diagnostics-reduced/index.sqlite --label swift --out /tmp/tracemap-swift-diagnostics-combined.sqlite` - passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-diagnostics-combined.sqlite --out /tmp/tracemap-swift-diagnostics-report` - passed.
- Redaction grep over generated diagnostics scan/export/report outputs - passed.
- `dotnet build src/dotnet/TraceMap.sln` - passed.
- `dotnet test src/dotnet/TraceMap.sln` - passed, 696 tests.
- `./scripts/check-private-paths.sh` - passed.
- `git diff --check` - passed.

## Notes

- This is a natural prerequisite before broader Swift UI/storage slices because
  those slices need a clear way to explain unsupported dynamic runtime
  boundaries.
- If the existing Swift adapter already emits a similar diagnostic or gap, the
  implementation should normalize and document it rather than duplicate facts.
