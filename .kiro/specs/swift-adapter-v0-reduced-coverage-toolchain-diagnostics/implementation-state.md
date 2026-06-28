# Swift Adapter v0 Reduced Coverage And Toolchain Diagnostics Implementation State

Status: `ready-for-implementation`

## Branches

- Spec branch: `codex/spec-swift-toolchain-diagnostics`
- Intended implementation branch:
  `codex/implement-swift-toolchain-diagnostics`
- Base: `dev`

## Issue Links

- Parent: https://github.com/joefeser/tracemap/issues/377
- This spec: https://github.com/joefeser/tracemap/issues/386

## Scope

This spec is for structured Swift reduced-coverage and toolchain diagnostics.
It does not implement SourceKit semantic enrichment, Xcode builds, package
restores, simulator/device execution, storyboard wiring, runtime proof, or
production impact claims.

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

- `swift build --package-path src/swift`
- `swift run --package-path src/swift tracemap-swift-smoke-tests`
- Swift reduced-coverage fixture scan.
- `dotnet run --project src/dotnet/TraceMap.Cli -- export ...`
- `dotnet run --project src/dotnet/TraceMap.Cli -- combine ...`
- `dotnet run --project src/dotnet/TraceMap.Cli -- report ...`
- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

## Notes

- This is a natural prerequisite before broader Swift UI/storage slices because
  those slices need a clear way to explain unsupported dynamic runtime
  boundaries.
- If the existing Swift adapter already emits a similar diagnostic or gap, the
  implementation should normalize and document it rather than duplicate facts.
