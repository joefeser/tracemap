# Swift Adapter v0 UI Surfaces Tasks

Issue: [#384](https://github.com/joefeser/tracemap/issues/384)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Implementation is present on `codex/implement-swift-adapter-v0-ui-surfaces`.
Keep the final PR-loop task unchecked until the implementation PR is merged.

## Spec And Review

- [x] Create the Kiro spec for issue #384.
- [ ] Run Opus spec review.
- [ ] Run Sonnet spec review.
- [x] Patch Medium+ review findings or document explicit non-actionable
  dispositions.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Open a spec PR to `dev` and complete the PR review loop.

## Phase 0: Scope And Contracts

- [x] Confirm implementation branch name:
  `codex/implement-swift-adapter-v0-ui-surfaces`.
- [x] Confirm this implementation-state file is updated with implementation
  branch, scope decisions, validation, oddities, and follow-up notes before
  analyzer code changes begin.
- [x] Re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`,
  `docs/ACCEPTANCE.md`, issue #377, issue #384, and merged Swift v0
  prerequisite specs before implementation.
- [x] Confirm the implementation starts from latest `origin/dev` after required
  Swift v0 prerequisite slices are merged.
- [x] Confirm no runtime app execution, Xcode build, scanned-app package
  restore/build requirement, simulator, device, storyboard runtime loading, nib
  runtime loading, LLM, embedding, vector database, or prompt-based
  classification is in scope. Building the TraceMap Swift adapter package for
  validation is allowed.
- [x] Confirm all new fact types and rule IDs are added to
  `rules/rule-catalog.yml` before emission.

## Phase 1: Rule Catalog, Fact Model, And Safety

- [x] Add rule catalog entries and limitations for
  `swift.ui.swiftui.view.v1`,
  `swift.ui.swiftui.navigation.v1`,
  `swift.ui.swiftui.action.v1`,
  `swift.ui.uikit.controller.v1`,
  `swift.ui.uikit.action.v1`,
  `swift.ui.uikit.binding.v1`, and
  `swift.ui.analysis-gap.v1`.
- [x] Define closed vocabularies for `uiFramework`, `surfaceKind`, `uiRole`,
  `symbolIdentityStatus`, action kinds, navigation/presentation kinds, and
  UI-specific `gapKind` values.
- [x] Define UI fact stable identity inputs and confirm fact IDs never include
  raw source snippets, raw UI labels, raw storyboard/nib XML, local absolute
  paths, URLs, secrets, or private labels.
- [x] Add or reuse safe-value helpers for UI names, labels, selector metadata,
  destination names, and role-prefixed hashes.
- [x] Document that Swift UI evidence is static source evidence only and does
  not prove runtime rendering, navigation, action execution, controller
  loading, storyboard/nib wiring, deployment, production use, or impact.
- [x] Assert SwiftSyntax-only UI facts use `Tier3SyntaxOrTextual` by default;
  reserve `Tier1Semantic` for future compiler/SourceKit evidence only.

## Phase 2: Fixtures And Test Harness

- [x] Add checked-in sample `samples/swift-ui-surfaces/`.
- [x] Add SwiftUI fixtures for `View` declarations, `body`, `@main App`,
  `WindowGroup`, and common containers.
- [x] Add SwiftUI navigation/presentation fixtures for `NavigationLink`,
  `.navigationDestination`, `.sheet`, `.fullScreenCover`, and `.popover`.
- [x] Add presentation/action-ish fixtures for `.alert` that prove it does not
  become a destination-backed navigation edge.
- [x] Add container fixtures for `NavigationStack`, `NavigationSplitView`,
  `TabView`, and `List` that prove containers emit container context, not
  destination edges.
- [x] Add SwiftUI action-ish fixtures for `Button`, `.onTapGesture`,
  `.onSubmit`, `.onAppear`, `.task`, `.refreshable`, `.swipeActions`, and
  `ToolbarItem`.
- [x] Add UIKit fixtures for controller subclasses, `@IBAction`, optional
  `@IBOutlet`, lifecycle methods, `prepare(for:sender:)`, and simple
  `addTarget(... #selector(...))` patterns.
- [x] Add unsupported/reduced fixtures for storyboards/nibs, Objective-C
  selectors, runtime navigation, dynamic destinations, macro/generated-code
  boundaries, parser/toolchain gaps, and reduced coverage.
- [x] Add redaction fixtures proving raw labels, snippets, storyboard/nib XML,
  local paths, secrets, URLs, hostnames, and private labels do not appear in
  `facts.ndjson`, SQLite `properties_json`, `report.md`, or logs.
- [x] Add fact ID stability assertions across identical scans when only
  `--out` changes.
- [x] Add assertions that all UI gap facts have non-empty `extractorId`,
  `extractorVersion`, rule ID, evidence tier, file path, line span, and closed
  vocabulary `gapKind`.
- [x] Wire UI assertions into
  `src/swift/Sources/tracemap-swift-smoke-tests/main.swift` or a source file
  compiled by that executable.

## Phase 3: SwiftUI Surface Extraction

- [x] Emit `SwiftUiSurfaceDeclared` for syntax-visible `View` declarations with
  visible `body` evidence.
- [x] Emit scene/root-view evidence for supported `@main App`, `Scene`,
  `WindowGroup`, and `DocumentGroup` shapes when a static view expression is
  visible.
- [x] Reject view-name-only evidence when `View` conformance or `body` syntax
  is absent.
- [x] Record SwiftUI property wrapper names only as static metadata or gaps;
  do not infer runtime state, environment values, or data flow.
- [x] Emit gaps for macros, generated code, conditional compilation,
  unavailable SwiftSyntax support, missing module context, and ambiguous
  surface identity.

## Phase 4: SwiftUI Navigation And Action-ish Extraction

- [x] Emit `SwiftUiNavigationCandidate` only for supported destination-backed
  static `NavigationLink`, `.navigationDestination`, `.sheet`,
  `.fullScreenCover`, and `.popover` evidence where destination syntax exists.
- [x] Emit `.alert` as presentation/action-ish context or a gap, not as a
  destination-backed navigation candidate.
- [x] Emit `TabView`, `NavigationStack`, `NavigationSplitView`, and `List` as
  container context facts or gaps, not as navigation destination edges.
- [x] Populate destination identity status as `resolved`, `unresolved`,
  `ambiguous`, or `dynamic`.
- [x] Emit gaps instead of destination facts for helper-built, variable,
  generic, feature-flagged, dynamic, or unsupported destinations.
- [x] Emit `SwiftUiActionCandidate` for supported static `Button`, gesture,
  submit, lifecycle-ish modifier, toolbar, swipe, task, and refreshable syntax.
- [ ] Link direct source-local call/declaration support through sorted
  `supportingFactIds` when existing Swift call facts prove them.
- [x] Do not summarize closure behavior, async scheduling, Combine pipelines,
  branch feasibility, downstream calls, or impact beyond direct visible syntax.

## Phase 5: UIKit Controller, Action, And Binding Extraction

- [x] Emit `UIKitControllerDeclared` for syntax-visible classes inheriting from
  documented UIKit controller bases.
- [x] Emit `UIKitActionDeclared` for visible `@IBAction` methods.
- [x] Optionally record `@IBOutlet` metadata as structural context without
  wiring claims.
- [x] Emit lifecycle/action-ish evidence for documented controller lifecycle
  methods only when the containing controller candidate is visible.
- [x] Emit `UIKitActionBindingCandidate` only for deterministic simple
  `addTarget` shapes with same-expression `#selector` syntax.
- [x] Emit gaps or omit claims for Objective-C selector binding, nil
  target/responder-chain routing, storyboard/nib wiring, helper-built
  bindings, dynamic selectors, runtime controller construction, and ambiguous
  target identity.

## Phase 6: SQLite, Reports, Export, And Combine

- [x] Persist UI facts in `facts.ndjson` and `index.sqlite` with sorted
  properties and stable fact IDs.
- [ ] Populate shared symbol/fact-symbol support when existing Swift
  declaration or call facts provide symbol IDs.
- [x] Add local Swift report counts for UI framework, surface kind,
  action/navigation kind, evidence tier, rule ID, and gap kind.
- [x] Verify `tracemap export` over the Swift UI index preserves rule IDs,
  evidence tiers, file spans, extractor versions, and safe properties.
- [x] Verify `tracemap combine` and `tracemap report` read the Swift UI index
  without schema errors and without runtime UI proof language.
- [x] Add tests proving combined/generic reports do not imply screens are
  reachable, rendered, tapped, loaded, selected, presented, or impacted.

## Phase 7: Validation

- [x] Run `swift build --package-path src/swift`.
- [x] Run `swift run --package-path src/swift tracemap-swift-smoke-tests`.
- [x] Run
  `swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces`.
- [x] Run
  `dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --out /tmp/tracemap-swift-ui-export --format json`.
- [x] Run
  `dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-ui-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-ui-combined.sqlite`.
- [x] Run
  `dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-ui-combined.sqlite --out /tmp/tracemap-swift-ui-report`.
- [x] Run `dotnet build src/dotnet/TraceMap.sln`.
- [x] Run `dotnet test src/dotnet/TraceMap.sln`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `git diff --check`.
- [x] Update implementation-state with final validation results and follow-up
  items.
- [ ] Open an implementation PR to `dev`, complete the PR review loop, and
  merge when ACK returns `merge_ready`.

## Follow-Ups Out Of Scope

- [ ] Storyboard/nib/xib parser for static Interface Builder wiring.
- [ ] Objective-C source and selector resolution.
- [ ] Swift compiler/SourceKit semantic UI evidence.
- [ ] Runtime UI automation, simulator/device instrumentation, or screenshot
  proof.
- [ ] SwiftUI state graph, navigation path, environment, property wrapper, or
  Combine/async flow modeling.
- [ ] Cross-language shared UI surface vocabulary beyond generic fact export.
- [ ] Public site claims beyond static evidence-backed Swift UI discovery.
- [ ] Add source-local `supportingFactIds` linkage from Swift UI action rows to
  existing Swift call/declaration facts.
