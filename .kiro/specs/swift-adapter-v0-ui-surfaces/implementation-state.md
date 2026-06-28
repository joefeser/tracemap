# Swift Adapter v0 UI Surfaces Implementation State

Status: `implemented`

Issue: [#384](https://github.com/joefeser/tracemap/issues/384)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-adapter-v0-ui-surfaces`

Implementation branch:
`codex/implement-swift-adapter-v0-ui-surfaces`

Implementation PR: [#421](https://github.com/joefeser/tracemap/pull/421)

Merged to `dev`: 2026-06-28, merge commit
`6d4b956fdfff9d5d8705ed6f468fa12584801a85`.

## Current Scope

This implementation adds conservative static SwiftUI and UIKit UI surface
evidence from checked-in Swift source. It emits syntax/textual facts for:

- SwiftUI `View` declarations with visible `body` evidence.
- SwiftUI scene/root-view syntax such as `WindowGroup`.
- Destination-backed SwiftUI navigation/presentation candidates for narrow
  syntax-visible shapes.
- SwiftUI container context for `NavigationStack`, `NavigationSplitView`,
  `TabView`, and `List`, explicitly not as destination-backed edges.
- SwiftUI action-ish candidates such as `Button`, toolbar items, `.onAppear`,
  `.task`, and `.alert` presentation context.
- UIKit controller declarations, `@IBAction` methods, lifecycle-ish candidates,
  `@IBOutlet` context, and simple `addTarget(... #selector(...))` binding
  candidates.
- UI-specific analysis gaps for storyboards, xibs, dynamic presentation,
  Objective-C selector mediation, and generated UI source.

The slice keeps all UI evidence syntax-only and static. It does not execute app
code, run Xcode builds, require restoring/building the scanned app, launch
simulators/devices, load storyboards/nibs at runtime, inspect responder chains,
or claim runtime reachability, rendering, user action, production usage, impact,
or full SwiftUI state/data flow.

## Public Claim Level

Claim level: `dev-only until promoted to main`.

Allowed claim after dev/main promotion:

- TraceMap emits deterministic static SwiftUI and UIKit UI surface evidence for
  selected narrow source-visible patterns.

Forbidden claims:

- Runtime rendering, screen reachability, navigation, action execution,
  controller loading, storyboard/nib wiring, or production usage is proven.
- UI code is impacted without reducer-backed rule and evidence.
- Swift UI support is AI-powered, LLM-driven, vector-based, or prompt-based.

## Rule IDs Added

- `swift.ui.swiftui.view.v1`
- `swift.ui.swiftui.navigation.v1`
- `swift.ui.swiftui.action.v1`
- `swift.ui.uikit.controller.v1`
- `swift.ui.uikit.action.v1`
- `swift.ui.uikit.binding.v1`
- `swift.ui.analysis-gap.v1`

## Validation

Run on `codex/implement-swift-adapter-v0-ui-surfaces`:

- `swift build --package-path src/swift` passed.
- `swift run --package-path src/swift tracemap-swift-smoke-tests` passed.
- `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces` passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --out /tmp/tracemap-swift-ui-export --format json` passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-ui-combined.sqlite` passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-ui-combined.sqlite --out /tmp/tracemap-swift-ui-report` passed.
- Redaction grep over generated UI scan/export/report artifacts passed for local paths, storyboard/xib XML, raw UI labels, raw URLs, and secret-like tokens.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 696 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Follow-Up Items

- Add source-local `supportingFactIds` linkage from Swift UI action rows to
  existing Swift call/declaration facts.
- Consider a future Interface Builder parser spec for storyboard/nib/xib wiring
  if static XML evidence becomes valuable.
- Consider a future SourceKit/compiler enrichment slice for semantic UI symbol
  identity only if the toolchain cost is justified.
- Consider a future shared UI surface vocabulary after more than one language
  adapter needs comparable UI facts.
