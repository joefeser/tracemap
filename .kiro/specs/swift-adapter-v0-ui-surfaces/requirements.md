# Swift Adapter v0 UI Surfaces Requirements

## Introduction

TraceMap needs a conservative Swift adapter slice that extracts static SwiftUI
and UIKit UI surface evidence from checked-in Swift source. This spec covers
GitHub issue [#384](https://github.com/joefeser/tracemap/issues/384), a child
of the Swift v0 runway issue
[#377](https://github.com/joefeser/tracemap/issues/377).

This is deterministic static evidence only. The scanner must not execute app
code, run Xcode builds, require restoring or building the scanned app's
packages, launch simulators/devices, load storyboards/nibs at runtime, inspect
UIKit responder chains, observe SwiftUI navigation state, or claim that a
screen, control, action, or navigation path is reachable in a running app.
Building the TraceMap Swift adapter package itself for validation remains in
scope.

## Source Material

- GitHub issue #384: Swift adapter v0 SwiftUI and UIKit UI surfaces.
- GitHub issue #377: Swift adapter v0 runway.
- Existing Swift specs:
  - `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
  - `.kiro/specs/swift-adapter-v0-inventory-project-discovery/`
  - `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/`
  - `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/`
  - `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/`
  - `.kiro/specs/swift-adapter-v0-http-api-client-surfaces/`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `rules/rule-catalog.yml`
- `src/swift/`

## Requirements

### Requirement 1: SwiftUI View Surface Evidence

**User Story:** As an engineer reviewing a Swift codebase, I want TraceMap to
record syntax-visible SwiftUI view surfaces so I can find likely UI entry points
without runtime claims.

#### Acceptance Criteria

1. WHEN Swift source contains a declaration that conforms syntactically to
   `View` and has a visible `body` member returning or containing SwiftUI view
   syntax THEN the adapter SHALL emit a UI surface fact with rule ID, evidence
   tier, file path, line span, extractor version, and safe view identity.
2. WHEN a declaration only has a name that resembles a view but lacks visible
   `View` conformance or `body` syntax THEN the adapter SHALL NOT emit a
   SwiftUI view surface.
3. WHEN `@main App`, `Scene`, `WindowGroup`, `DocumentGroup`, or similar app
   scene syntax visibly instantiates a view type THEN the adapter MAY emit
   scene/root-view static evidence, but it SHALL NOT claim app launch,
   deployment, route reachability, or user navigation.
4. WHEN SwiftUI property wrappers such as `@State`, `@Binding`,
   `@ObservedObject`, `@StateObject`, `@Environment`, or `@EnvironmentObject`
   are present THEN the adapter MAY record wrapper names as static metadata or
   emit gaps for unavailable semantics; it SHALL NOT infer runtime state,
   dependency injection, environment values, or data flow from wrapper presence.
5. WHEN macros, generated code, conditional compilation, unavailable
   SwiftSyntax support, missing module context, or dynamic type construction
   affects a candidate view THEN the adapter SHALL emit an `AnalysisGap` or
   lower-tier candidate evidence instead of upgrading the finding.

### Requirement 2: SwiftUI Navigation and Presentation Evidence

**User Story:** As a reviewer, I want static SwiftUI navigation and presentation
evidence when syntax visibly names the destination, so UI flows can be inspected
with clear limitations.

#### Acceptance Criteria

1. WHEN source contains supported SwiftUI navigation or presentation syntax with
   a visible destination view expression, such as `NavigationLink`,
   `.navigationDestination`, `.sheet`, `.fullScreenCover`, or `.popover`, THEN
   the adapter SHALL emit static UI navigation/presentation
   evidence with supporting fact IDs where available.
2. WHEN source contains presentation syntax such as `.alert` that names content
   without a destination view expression THEN the adapter MAY emit
   presentation/action-ish surface evidence, but it SHALL NOT emit a
   destination-backed navigation edge.
3. WHEN navigation or presentation destination syntax is dynamic, generic,
   closure-built without a visible view expression, feature-flagged, or
   indirectly created through helper functions THEN the adapter SHALL emit an
   `AnalysisGap` and SHALL NOT invent a destination.
4. WHEN `NavigationStack`, `NavigationSplitView`, `TabView`, or `List` syntax
   is visible THEN the adapter MAY emit container surface evidence, but it SHALL
   NOT prove runtime path contents, selected tab, list row reachability, or
   navigation stack transitions.
5. WHEN static navigation evidence is emitted THEN it SHALL be labeled as a
   static edge or candidate between source and destination syntax, not proof of
   runtime route execution or user reachability.
6. WHEN destination identity cannot be tied to a known source-local Swift
   symbol from existing Swift declaration evidence THEN the adapter SHALL keep
   the finding syntax-only and record destination identity status as unresolved,
   ambiguous, or dynamic.

### Requirement 3: SwiftUI Action-ish Surface Evidence

**User Story:** As a maintainer, I want static evidence for obvious SwiftUI
user-triggered action shapes so action entry points can be reviewed without
claiming behavior was executed.

#### Acceptance Criteria

1. WHEN source contains supported SwiftUI action-bearing syntax such as
   `Button`, `.onTapGesture`, `.onSubmit`, `.onAppear`, `.task`,
   `.refreshable`, `.swipeActions`, or `ToolbarItem` with visible closure or
   callee syntax THEN the adapter MAY emit action-ish UI evidence.
2. WHEN action evidence points to a direct source-local function or method call
   already represented by Swift call/declaration facts THEN the UI fact SHOULD
   include sorted `supportingFactIds`.
3. WHEN an action closure contains multiple calls, async work, conditional
   branches, task scheduling, Combine pipelines, or helper calls THEN the
   adapter SHALL record only the visible static action container and direct
   syntax-local call evidence; it SHALL NOT infer full behavior or downstream
   impact.
4. WHEN action target syntax uses dynamic selectors, opaque closures, generated
   code, macro expansion, or unavailable syntax support THEN the adapter SHALL
   emit an `AnalysisGap` rather than a definitive target.

### Requirement 4: UIKit Controller and Action Evidence

**User Story:** As a UIKit reviewer, I want deterministic evidence for simple
controller and action surfaces so classic app code appears in TraceMap without
requiring Interface Builder or runtime loading.

#### Acceptance Criteria

1. WHEN Swift source contains a class declaration that syntactically inherits
   from `UIViewController`, `UITableViewController`,
   `UICollectionViewController`, `UITabBarController`,
   `UINavigationController`, or another documented UIKit controller base THEN
   the adapter SHALL emit a UIKit controller surface fact.
2. WHEN a method declaration carries a visible `@IBAction` attribute THEN the
   adapter SHALL emit a UIKit action surface fact with method identity, line
   span, and safe metadata.
3. WHEN source contains deterministic simple `addTarget` syntax where target,
   control event, and selector expression are visible in the same expression
   or same small syntax-local statement group THEN the adapter MAY emit static
   UIKit action binding evidence.
4. WHEN an `@IBOutlet` property declaration is visible THEN the adapter MAY
   emit outlet metadata only as structural context; outlet presence SHALL NOT
   prove storyboard/nib wiring, view hierarchy, or runtime availability.
5. WHEN controller lifecycle methods such as `viewDidLoad`,
   `viewWillAppear`, `viewDidAppear`, or `prepare(for:sender:)` are present
   THEN the adapter MAY emit lifecycle/action-ish static evidence, but it SHALL
   NOT claim lifecycle execution or segue reachability.
6. WHEN UIKit evidence depends on Objective-C selectors, responder chains,
   storyboard segue identifiers, nib wiring, target-action wiring outside the
   supported syntax-local shapes, or runtime controller construction THEN the
   adapter SHALL emit an `AnalysisGap` or omit the claim.

### Requirement 5: Gaps, Coverage, and No-Overclaim Boundaries

**User Story:** As a public TraceMap user, I want Swift UI output to be useful
but plainly bounded to static source evidence.

#### Acceptance Criteria

1. WHEN storyboards, nibs/xibs, segue XML, Objective-C source, Objective-C
   selectors, runtime navigation, dynamic routes, macro-generated code,
   generated Swift, unavailable SwiftSyntax/toolchain support, or reduced
   project coverage affects UI extraction THEN the adapter SHALL emit
   `AnalysisGap` facts where there is file-backed evidence of the limitation.
2. WHEN a storyboard or nib file is inventoried but not parsed for UI wiring in
   this slice THEN the adapter SHALL record the limitation as reduced coverage
   and SHALL NOT infer controllers, outlets, segues, or actions from it.
3. WHEN SwiftSyntax or the selected parser cannot parse a file THEN the adapter
   SHALL continue scanning other files, emit a parser/toolchain gap, and mark
   Swift coverage reduced according to the existing Swift manifest contract.
4. WHEN UI facts are included in reports THEN reports SHALL label them as
   static evidence and SHALL NOT say a screen is reachable, rendered, tapped,
   loaded, selected, presented, or impacted without reducer-backed evidence.
5. WHEN reduced UI coverage exists THEN absence of a UI fact SHALL NOT be
   presented as proof that a screen, action, route, storyboard, nib, or
   controller does not exist.

### Requirement 6: Fact Shape, Rules, and Safety

**User Story:** As a downstream consumer, I want Swift UI facts to follow the
shared TraceMap evidence contract so they remain auditable and public-safe.

#### Acceptance Criteria

1. Each emitted UI fact SHALL include deterministic fact ID, scan ID, repo,
   commit SHA, fact type, rule ID, evidence tier, repo-relative file path, line
   span when file-backed, extractor ID, extractor version, and sorted safe
   properties.
2. Each new rule ID SHALL be added to `rules/rule-catalog.yml` before product
   code emits it, including evidence tier expectations, emitted fact types,
   limitations, false positives, and false negatives.
3. UI surface facts in this v0 slice SHALL use `Tier3SyntaxOrTextual` unless a
   deterministic project/file-role relationship qualifies as
   `Tier2Structural`. `Tier1Semantic` is reserved for future compiler/SourceKit
   evidence and SHALL NOT be used by SwiftSyntax-only UI extraction.
4. Gap facts SHALL use `Tier4Unknown` with a closed-vocabulary `gapKind` and
   safe message.
5. Facts SHALL NOT store raw source snippets, raw storyboard/nib XML snippets,
   full local paths, secrets, URLs, hostnames, private labels, user-entered
   text, accessibility content, or unsafe literal values by default.
6. Fact IDs and stable keys SHALL be derived from safe deterministic inputs and
   role-prefixed hashes for unsafe values, never raw unsafe content.

### Requirement 7: Reporting and Shared Reader Compatibility

**User Story:** As a TraceMap user, I want Swift UI evidence to survive scan,
export, combine, and report workflows so it can be reviewed beside other
language facts.

#### Acceptance Criteria

1. The local Swift `report.md` SHALL summarize Swift UI evidence counts by UI
   framework family, surface kind, action kind, navigation/presentation kind,
   evidence tier, and gap kind.
2. The generated `index.sqlite` SHALL remain readable by existing
   `tracemap export`, `tracemap combine`, and `tracemap report` commands.
3. Combined reports MAY list Swift UI surfaces as static dependency or review
   context only when shared readers have an explicit surface vocabulary for
   them; otherwise generic fact counts and export rows are sufficient.
4. Path/reverse/reducer behavior is out of scope unless a separate
   implementation task adds explicit tests and rule-backed semantics for Swift
   UI facts.
5. Existing Swift smoke tests SHALL include UI fixtures with at least one
   supported SwiftUI surface, one supported UIKit surface, and one unsupported
   or reduced-coverage path.

### Requirement 8: Validation

**User Story:** As a reviewer, I want focused validation for the Swift UI slice
before implementation lands.

#### Acceptance Criteria

1. `swift build --package-path src/swift` SHALL pass.
2. `swift run --package-path src/swift tracemap-swift-smoke-tests` SHALL pass.
3. A checked-in Swift UI sample SHALL scan successfully.
4. Shared reader validation over the generated Swift UI index SHALL pass.
5. `dotnet build src/dotnet/TraceMap.sln` and
   `dotnet test src/dotnet/TraceMap.sln` SHALL pass or be explicitly deferred
   with evidence.
6. Swift UI fixture assertions SHALL prove static evidence, gap emission,
   reduced coverage, stable fact IDs, public-safe output, and no runtime UI
   proof language.
7. `./scripts/check-private-paths.sh` and `git diff --check` SHALL pass.

## Proposed Fact Types

- `SwiftUiSurfaceDeclared`
- `SwiftUiNavigationCandidate`
- `SwiftUiActionCandidate`
- `UIKitControllerDeclared`
- `UIKitActionDeclared`
- `UIKitActionBindingCandidate`
- `AnalysisGap`

Implementation may rename proposed Swift-specific fact types before code lands
if review finds a better shared vocabulary. Any rename must update the rule
catalog, fixtures, reports, and this spec before emission.

## Proposed Rule IDs

- `swift.ui.swiftui.view.v1`
- `swift.ui.swiftui.navigation.v1`
- `swift.ui.swiftui.action.v1`
- `swift.ui.uikit.controller.v1`
- `swift.ui.uikit.action.v1`
- `swift.ui.uikit.binding.v1`
- `swift.ui.analysis-gap.v1`

Each rule must document that evidence is static source evidence only and does
not prove runtime rendering, navigation, tap/click behavior, controller
loading, storyboard/nib wiring, deployment, production use, or impact.

## Non-Goals

- Runtime UI automation, accessibility inspection, simulator/device runs,
  snapshot tests, app launch, or Xcode build execution.
- Storyboard/nib/xib segue, outlet, or target-action resolution beyond
  explicit inventory/gap labels.
- Objective-C source analysis, Objective-C selector binding, responder chain
  analysis, dynamic dispatch, protocol witness selection, dependency injection,
  Combine/async flow, SwiftUI state graph modeling, or branch feasibility.
- Full Swift semantic resolution, overload selection, macro expansion, or
  generated-code freshness checks.
- Reducer claims that UI code is impacted without reducer-backed rule and
  evidence.
- Raw source snippets, raw storyboard/nib XML, secrets, user-facing literal
  strings, accessibility content, local absolute paths, URLs, or private labels
  in output by default.
- LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact analysis in TraceMap core.
