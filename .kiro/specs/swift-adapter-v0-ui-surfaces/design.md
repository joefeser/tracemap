# Swift Adapter v0 UI Surfaces Design

## Overview

This slice adds conservative static SwiftUI and UIKit UI surface evidence to
the Swift adapter. It builds on the SwiftSyntax declaration/call and symbol
identity slices by recognizing narrow source-visible UI shapes, emitting
TraceMap-compatible facts, and recording explicit gaps for runtime-only or
unsupported UI behavior.

The implementation should favor a narrow, well-tested first cut over broad UI
inference. It should help reviewers locate likely screens, controller classes,
navigation candidates, and action-ish entry points while staying honest about
runtime uncertainty.

## Evidence Flow

```text
selected .swift files
  -> SwiftSyntax parse tree
  -> existing Swift declaration/call/symbol evidence
  -> SwiftUI/UIKit syntax recognizers
  -> UI surface/action/navigation facts
  -> optional SQLite/report rows using shared fact storage
  -> AnalysisGap facts for storyboard/nib, ObjC selector, runtime navigation,
     dynamic route, macro/generated-code, toolchain, and reduced-coverage
     boundaries
```

Every edge remains static evidence. The implementation must not launch an app,
load Interface Builder resources, run a simulator/device, execute SwiftUI view
builders, resolve UIKit responder chains, or prove that a UI path is reachable
at runtime.

## Data Model

### Fact Types

- `SwiftUiSurfaceDeclared`
  - Syntax-visible SwiftUI view, scene, or UI container surface.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `SwiftUiNavigationCandidate`
  - Syntax-visible navigation or presentation candidate with visible source and
    destination/context metadata.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `SwiftUiActionCandidate`
  - Syntax-visible SwiftUI action-ish surface such as a `Button` or gesture
    modifier.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `UIKitControllerDeclared`
  - Syntax-visible UIKit controller subclass candidate.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `UIKitActionDeclared`
  - Syntax-visible `@IBAction` or lifecycle/action-ish method surface.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `UIKitActionBindingCandidate`
  - Syntax-visible target/action binding candidate for deterministic simple
    `addTarget` shapes.
  - Default evidence tier: `Tier3SyntaxOrTextual`.
- `AnalysisGap`
  - Gap kinds for unsupported UI resources, dynamic destinations/selectors,
    parser/toolchain limitations, macros/generated code, and reduced coverage.
  - Evidence tier: `Tier4Unknown`.

`Tier2Structural` may be used only when deterministic file/project role
evidence from prior Swift slices supports the UI fact without compiler
semantics, for example a known Swift source file inside a parsed app target.
`Tier1Semantic` is out of scope for this SwiftSyntax-only UI slice.

### Common Properties

Common safe properties for UI facts:

- `uiFramework`: `swiftui`, `uikit`, or `unknown`.
- `surfaceKind`: `view`, `scene`, `container`, `controller`, `action`,
  `binding`, `navigation`, `presentation`, or `lifecycle`.
- `surfaceName`: safe source-local declaration or API name when visible.
- `symbolId`: source-local Swift symbol ID when existing declaration evidence
  proves one.
- `symbolIdentityStatus`: `resolved`, `unresolved`, `ambiguous`, `dynamic`, or
  `not-applicable`.
- `moduleName` and `moduleIdentityStatus` when already available from Swift
  inventory/declaration evidence.
- `uiRole`: closed vocabulary such as `root-view`, `destination-view`,
  `source-view`, `controller`, `button`, `gesture`, `sheet`, or `lifecycle`.
- `supportingFactIds`: optional sorted supporting declaration, call, or
  relationship fact IDs.
- `staticEvidenceOnly`: `true`.

Forbidden properties:

- Raw source snippets.
- Raw storyboard/nib/xib XML or object IDs.
- User-facing text strings, accessibility labels, placeholder text, or
  localization values.
- Raw URLs, hostnames, secrets, tokens, local absolute paths, raw remotes,
  private org/repo/team names, or private labels.

If a literal label is useful only for disambiguation, store a role-prefixed
SHA-256 hash such as `sha256("swift.ui|label|" + rawValue)`, not the raw value.

## Rule IDs

Add these rules to `rules/rule-catalog.yml` before code emits them:

- `swift.ui.swiftui.view.v1`
  - Emits `SwiftUiSurfaceDeclared` for syntax-visible SwiftUI view, scene, or
    container surfaces.
  - Limitations: SwiftSyntax only; no view builder execution, runtime state,
    scene activation, conditional branch feasibility, environment resolution,
    or rendered-screen proof.
- `swift.ui.swiftui.navigation.v1`
  - Emits `SwiftUiNavigationCandidate` for visible SwiftUI navigation and
    presentation shapes.
  - Limitations: no runtime navigation stack proof, selected tab proof,
    dynamic path expansion, destination reachability, or route execution.
- `swift.ui.swiftui.action.v1`
  - Emits `SwiftUiActionCandidate` for visible SwiftUI action-ish syntax.
  - Limitations: no tap/gesture execution proof, async scheduling proof,
    Combine pipeline proof, branch feasibility, or downstream impact
    conclusion.
- `swift.ui.uikit.controller.v1`
  - Emits `UIKitControllerDeclared` for syntax-visible UIKit controller
    subclasses.
  - Limitations: no runtime instantiation, storyboard/nib loading,
    navigation-controller membership, presented state, or lifecycle execution
    proof.
- `swift.ui.uikit.action.v1`
  - Emits `UIKitActionDeclared` for `@IBAction` and documented lifecycle or
    action-ish methods.
  - Limitations: no Interface Builder wiring proof, selector binding proof,
    responder chain proof, target execution, or user-trigger proof.
- `swift.ui.uikit.binding.v1`
  - Emits `UIKitActionBindingCandidate` for deterministic simple target/action
    syntax.
  - Limitations: no Objective-C runtime selector resolution, control hierarchy,
    enabled/visible state, event delivery, or target lifetime proof.
- `swift.ui.analysis-gap.v1`
  - Emits `AnalysisGap` for dynamic, unsupported, ambiguous, or reduced UI
    extraction boundaries.
  - Limitations: gaps are conservative and may become resolvable through future
    semantic, Interface Builder, or generated-code support.

Each rule must document false positives and false negatives. Common false
positives include test fixtures, preview-only code, user-defined APIs named like
SwiftUI/UIKit APIs, and source shapes in examples. Common false negatives
include helper builders, wrappers, dynamic destinations, Objective-C sources,
storyboard/nib wiring, macro-generated views, generated Swift, and runtime
composition.

## SwiftUI Extraction Strategy

### View and Scene Surfaces

Recognize narrow syntax shapes:

- `struct Foo: View { var body: some View { ... } }`
- `struct Foo: SwiftUI.View { ... }`
- `@main struct AppName: App { var body: some Scene { WindowGroup { Foo() } } }`
- `WindowGroup { Foo() }`, `DocumentGroup { Foo() }`, or similar scene syntax
  when a destination view expression is visible.

Do not emit a view surface for names alone. A type named `FooView` without
visible `View` conformance and body syntax is not enough.

For body and scene facts, store declaration names, safe display signatures, and
line spans. Do not store view builder source text or user-facing string
literals.

### Navigation and Presentation

Recognize narrow syntax shapes:

- `NavigationLink { DestinationView() } label: { ... }`
- `NavigationLink(destination: DestinationView())`
- `.navigationDestination(for: Type.self) { value in DestinationView(...) }`
- `.sheet(...) { DestinationView(...) }`
- `.fullScreenCover(...) { DestinationView(...) }`
- `.popover(...) { DestinationView(...) }`
- `.alert(...)` as presentation context only; do not treat alert labels as
  destinations.
- `TabView { DestinationView().tabItem { ... } }` as container/child surface
  evidence where child view syntax is visible.

Destination identity status:

| Condition | Status |
| --- | --- |
| Destination matches one source-local Swift declaration symbol uniquely | `resolved` |
| Destination has visible type/call syntax but no matching declaration | `unresolved` |
| Destination text matches multiple source-local symbols | `ambiguous` |
| Destination is variable, generic, erased, helper-built, or data-driven | `dynamic` |

For `.navigationDestination(for:)`, do not treat the data type as a route
unless the destination view expression is visible. Store the data type only as
safe syntax metadata or a hash when safe.

### Action-ish Surfaces

Recognize narrow syntax shapes:

- `Button(...) { action() }`
- `Button(action: { action() }) { ... }`
- `.onTapGesture { action() }`
- `.onSubmit { action() }`
- `.onAppear { action() }`
- `.task { await action() }`
- `.refreshable { await action() }`
- `.swipeActions { Button(...) { action() } }`
- `ToolbarItem { Button(...) { action() } }`

Action target identity follows existing Swift syntax call evidence when
available. A UI action fact may cite direct supporting call fact IDs, but it
must not summarize all work inside a closure or infer impact from the closure
body.

## UIKit Extraction Strategy

### Controller Surfaces

Recognize class inheritance syntax for documented UIKit bases:

- `UIViewController`
- `UITableViewController`
- `UICollectionViewController`
- `UITabBarController`
- `UINavigationController`
- `UISplitViewController`
- `UIPageViewController`
- `UIHostingController`

Simple generic inheritance such as `UIHostingController<RootView>` may record
the safe root type syntax as static metadata when visible, but must not prove
the SwiftUI root is loaded at runtime.

### Actions, Outlets, and Lifecycle

Recognize:

- `@IBAction func submit(_ sender: Any)`.
- `@IBOutlet weak var button: UIButton!` as optional structural context.
- lifecycle/action-ish methods by exact name in UIKit controller classes:
  `viewDidLoad`, `viewWillAppear`, `viewDidAppear`, `viewWillDisappear`,
  `viewDidDisappear`, and `prepare(for:sender:)`.

`@IBOutlet` facts are not required in v0. If emitted, they should stay
structural/contextual and never claim Interface Builder wiring.

### Target/Action Binding

Recognize only deterministic simple `addTarget` shapes:

- `button.addTarget(self, action: #selector(Self.submit(_:)), for: .touchUpInside)`
- `button.addTarget(self, action: #selector(submit(_:)), for: .primaryActionTriggered)`

The first implementation should require selector syntax in the same call
expression. Same small syntax-local statement-group support may be added only
with tests for shadowing and ambiguity. Dynamic selectors, string selectors,
Objective-C-only targets, nil targets/responder chain routing, and helper
bindings emit gaps or no binding candidate.

## Gap Model

Recommended `gapKind` values:

- `swift-ui-storyboard-unsupported`
- `swift-ui-nib-unsupported`
- `swift-ui-objc-selector-unsupported`
- `swift-ui-runtime-navigation-unproven`
- `swift-ui-dynamic-destination`
- `swift-ui-dynamic-action-target`
- `swift-ui-macro-generated-unsupported`
- `swift-ui-generated-code-unsupported`
- `swift-ui-toolchain-unavailable`
- `swift-ui-parser-failed`
- `swift-ui-reduced-coverage`
- `swift-ui-identity-ambiguous`
- `swift-ui-shape-unsupported`

Gap messages must use closed-vocabulary safe text. They may include rule IDs,
counts, safe type names, hashes, and line spans, but not snippets or raw UI
labels.

## SQLite and Reporting

The first implementation should store UI facts through the existing generic
facts table and NDJSON output. If shared symbol or relationship rows are
already available from declaration/call extraction, UI facts may link to them
through existing `fact_symbols` and supporting fact IDs.

No new SQLite table is required for v0 UI evidence unless implementation review
finds a concrete shared-reader need. Any schema addition must be additive,
documented, and covered by export/combine/report tests.

Local Swift report output should include a `## Swift UI Surfaces` section with:

- counts by `uiFramework`;
- counts by `surfaceKind`;
- counts by action/navigation/presentation kind;
- counts by evidence tier and rule ID;
- gap counts by `gapKind`;
- a short limitations note that all UI evidence is static source evidence.

Combined readers should preserve UI facts in generic fact/export output. They
must not imply app navigation, route reachability, rendered screens, or user
actions unless a future shared UI surface model explicitly defines that
behavior with tests.

## Fixture Plan

Add `samples/swift-ui-surfaces/` with:

- SwiftUI view declarations with `body`.
- `@main App` / `WindowGroup` root-view syntax.
- `NavigationLink`, `.sheet`, `.navigationDestination`, and `TabView` examples
  with static and dynamic destinations.
- `Button`, `.onTapGesture`, `.onSubmit`, `.task`, and toolbar/swipe action
  examples.
- UIKit controller subclasses.
- `@IBAction`, optional `@IBOutlet`, lifecycle methods, and simple
  `addTarget(... #selector(...))` examples.
- Unsupported storyboards/nibs or placeholder resource files proving gap
  labeling without wiring claims.
- Macro/generated-code or conditional-compilation fixtures where practical.
- Redaction fixture values proving labels/snippets/private values do not appear
  in artifacts.

Smoke assertions should prove at least:

- supported SwiftUI and UIKit facts are emitted with expected rule IDs;
- dynamic destinations/actions emit gaps;
- storyboards/nibs are inventoried or gapped without wiring claims;
- UI facts never use `Tier1Semantic` in this slice;
- fact IDs are stable when only output path changes;
- generated artifacts omit raw labels/snippets/local absolute paths;
- export/combine/report can read the Swift UI index.

## Validation Commands

Implementation validation should run or explicitly defer with reason:

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

For this spec-only branch, required validation is:

```bash
git diff --check
./scripts/check-private-paths.sh
```

## Open Decisions for Implementation

- Whether to keep all UI fact types Swift-specific for v0 or introduce a shared
  cross-language UI surface vocabulary later.
- Whether `@IBOutlet` should emit its own fact type in v0 or remain only
  contextual metadata on controller/action facts.
- Whether lifecycle methods should use `UIKitActionDeclared` or a narrower
  `UIKitLifecycleCandidate` fact if reports need to distinguish user actions
  from framework callbacks.
- Whether storyboards/nibs should be inventory-only gaps in this slice or get a
  future dedicated Interface Builder parser spec.
