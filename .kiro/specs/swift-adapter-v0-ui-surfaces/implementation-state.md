# Swift Adapter v0 UI Surfaces Implementation State

Status: `ready-for-implementation`

Issue: [#384](https://github.com/joefeser/tracemap/issues/384)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-adapter-v0-ui-surfaces`

Intended implementation branch:
`codex/implement-swift-adapter-v0-ui-surfaces`

## Current Scope

This spec prepares a future implementation slice for conservative static
SwiftUI and UIKit UI surface evidence. It covers syntax-visible SwiftUI views,
navigation/presentation candidates, action-ish containers, UIKit controller
classes, `@IBAction` methods, simple `addTarget` target/action candidates, and
explicit gaps for unsupported UI evidence.

No analyzer code is implemented in this branch.

## Public Claim Level

Claim level: `specification only`.

Allowed claim after this spec PR:

- TraceMap has a ready-for-implementation plan for deterministic static SwiftUI
  and UIKit UI surface evidence.

Forbidden claims:

- Swift UI analysis is implemented.
- Runtime rendering, screen reachability, navigation, action execution,
  controller loading, storyboard/nib wiring, or production usage is proven.
- UI code is impacted without reducer-backed rule and evidence.
- Swift UI support is AI-powered, LLM-driven, vector-based, or prompt-based.

## Scope Decisions

- Keep this issue limited to SwiftUI and UIKit UI surfaces from checked-in Swift
  syntax.
- Use SwiftSyntax-only evidence for v0 UI facts unless future implementation
  records deterministic structural project/file-role support.
- Reserve `Tier1Semantic` for future compiler/SourceKit evidence; this slice
  should not use it.
- Treat SwiftUI navigation and UIKit target/action rows as static candidates,
  not runtime route or user-action proof.
- Include action-ish SwiftUI modifiers and UIKit lifecycle methods only as
  source-visible review context.
- Keep storyboards, nibs/xibs, Objective-C selectors, responder chains,
  runtime navigation, dynamic destinations/routes, macros, generated code,
  unavailable toolchain/parser support, and reduced coverage as explicit gaps
  or non-claims.
- Avoid raw source snippets, raw UI strings, storyboard/nib XML, local absolute
  paths, secrets, URLs, hostnames, and private labels in output by default.
- Do not add reducer/path/reverse claims for UI evidence in this slice unless a
  future implementation task adds explicit shared semantics and tests.

## Proposed Rule IDs

- `swift.ui.swiftui.view.v1`
- `swift.ui.swiftui.navigation.v1`
- `swift.ui.swiftui.action.v1`
- `swift.ui.uikit.controller.v1`
- `swift.ui.uikit.action.v1`
- `swift.ui.uikit.binding.v1`
- `swift.ui.analysis-gap.v1`

Implementation must add these, or reviewed replacements, to
`rules/rule-catalog.yml` before emitting product facts.

## Validation Plan

Spec branch:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Implementation branch:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --out /tmp/tracemap-swift-ui-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-ui-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-ui-combined.sqlite --out /tmp/tracemap-swift-ui-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Spec Validation

Run on `codex/spec-swift-adapter-v0-ui-surfaces`:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.

## Follow-Up Items

- Decide during implementation whether `@IBOutlet` needs a dedicated fact type
  or should remain contextual metadata.
- Consider a future Interface Builder parser spec for storyboard/nib/xib wiring
  if static XML evidence becomes valuable.
- Consider a future SourceKit/compiler enrichment slice for semantic UI symbol
  identity only if the toolchain cost is justified.
- Consider a future shared UI surface vocabulary after more than one language
  adapter needs comparable UI facts.
