# SwiftSyntax Declarations and Basic Call Facts Tasks

All tasks are intentionally unchecked. This PR creates the spec only and does
not implement Swift analyzer/runtime code.

## Implementation Plan

### Phase 0: Scope and Prerequisites

- [ ] 0.1 Confirm this implementation branch starts from `origin/dev` and
  references issue #380 and parent issue #377.
- [ ] 0.2 Confirm the Swift scaffold/output-contract slice from issue #378 has
  landed on the implementation base before starting product-code work for this
  slice.
- [ ] 0.3 Confirm the Swift inventory/project/package slice from issue #379 can
  provide module or target context; if it is absent, keep this slice file-scoped
  and emit module context gaps.
- [ ] 0.4 Confirm the selected SwiftSyntax package/toolchain version and
  document local installation requirements without requiring app builds for
  basic scan output.
- [ ] 0.5 Confirm v0 evidence tier defaults:
  `Tier3SyntaxOrTextual` for SwiftSyntax declarations/calls and `Tier4Unknown`
  for gaps.
- [ ] 0.6 Confirm no SourceKit, sourcekit-lsp, SwiftPM semantic loading, Xcode
  build execution, macro expansion execution, simulator/device inspection, or
  runtime UI discovery is included in this slice.
- [ ] 0.7 Confirm issue #381 owns canonical Swift symbol identity and
  relationship semantics; if #381 has not landed, document the interim
  file-scoped syntax ID scheme and migration note before emitting IDs.
- [ ] 0.8 Check whether #381 has landed. If it has, skip the interim
  `swift-syntax:v0:<hash>` format entirely and emit only #381-compatible IDs;
  if it has not, document the interim format and migration note before emitting
  any fact.
- [ ] 0.9 Confirm and document the exact Swift `scanId` input formula from #378
  or this implementation state before any fact-emitting product code is written
  in Phase 3.
- [ ] 0.10 Confirm `git diff origin/dev --name-only` for the spec PR contains
  only files under
  `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/` and no
  `src/swift/` or other product-code paths.

### Phase 1: Rule Catalog and Contract Gate

- [ ] 1.1 Add `rules/rule-catalog.yml` entries before product code emits any
  Swift declaration, call, object creation, navigation, relationship, import,
  or gap rule ID.
- [ ] 1.2 Document each Swift rule with the existing catalog fields: `id`,
  `name`, `description`, `evidenceTier`, `emits`, and `limitations`; include
  property expectations, false positives, and false negatives in prose unless a
  future catalog-schema spec extends the validator.
- [ ] 1.3 Add or reuse a rule-catalog validation hook so tests fail when a
  Swift fact references an uncataloged rule ID.
- [ ] 1.4 Document whether the rule-catalog validation hook is existing shared
  infrastructure or new Swift adapter infrastructure; if it is new, implement
  the hook before Phase 3 begins.
- [ ] 1.5 Add a negative catalog-gate test proving an emitted uncataloged Swift
  rule ID fails validation.
- [ ] 1.6 Decide which shared fact types can be reused safely: default to weaker
  or Swift-specific syntax call/member facts for Tier3 Swift call evidence, and
  do not emit `MethodInvoked` or `PropertyAccessed` unless reducer-safety tests
  prove no semantic upgrade.
- [ ] 1.7 Decide whether `TypeDeclared`, `AnalysisGap`, and related
  relationship/call rows can be reused with syntax-tier limitations; use a
  Swift-specific construction candidate by default instead of shared
  `ObjectCreated`.
- [ ] 1.8 Add Swift-specific fact types only when shared fact types would
  overstate semantics.
- [ ] 1.9 Record an explicit written decision in `implementation-state.md` for
  each reused shared fact type, including the reducer-safety test that proves
  the Tier3 Swift fact is not consumed as Tier1 semantic proof, before Phase 3
  begins.

### Phase 2: Fixtures and Test Baseline

- [ ] 2.1 Add a minimal Swift fixture with imports, a struct, class, enum,
  protocol, extension, functions, methods, initializers, and properties.
- [ ] 2.2 Add a call fixture covering simple calls, member calls, static-looking
  calls, `self`/`super` calls, initializer calls, `.init(...)`, optional
  chaining, trailing closures, and nested closures.
- [ ] 2.3 Add reduced-coverage fixtures for parser diagnostics, macro syntax,
  conditional compilation ambiguity, generated-source exclusion, Objective-C
  selector/bridge syntax, `#if canImport(...)`, ambiguous module context, and
  unavailable module context.
- [ ] 2.4 Add deterministic output tests for repeated scans over the same
  fixture and options.
- [ ] 2.5 Add public-safety tests proving raw snippets, raw expressions, raw
  string literals, raw URLs, hostnames, local absolute paths, raw remotes,
  secrets, signing metadata, and private labels are omitted or hashed.
- [ ] 2.6 Add path-normalization determinism tests, including path casing rules
  for platforms with different filesystem casing behavior.
- [ ] 2.7 Add scan ID stability tests proving unchanged repo, commit, selected
  Swift file inventory, and normalized options produce the same `scanId`, even
  when `--out` points at a different output path.
- [ ] 2.8 Add parser diagnostic safety tests proving SwiftSyntax diagnostic
  message text is hashed or omitted rather than stored verbatim.

### Phase 3: SwiftSyntax Parsing and Span Mapping

- [ ] 3.1 Wire SwiftSyntax file parsing into the Swift adapter without running
  app code, SwiftPM builds, Xcode builds, simulators, or devices.
- [ ] 3.2 Map SwiftSyntax source locations to repo-relative file paths and
  stable line spans.
- [ ] 3.3 Continue after recoverable parse diagnostics and emit bounded
  `AnalysisGap` facts.
- [ ] 3.4 Emit reduced coverage when SwiftSyntax is unavailable,
  version-incompatible, or unable to parse selected files.
- [ ] 3.5 Add tests for source location mapping, skipped files, unreadable
  files, file-size caps, and deterministic parser diagnostics.

### Phase 4: File, Module, Package, and Import Evidence

- [ ] 4.1 Emit or preserve Swift file inventory facts with relative path, file
  kind, safe hash/size metadata, rule ID, tier, extractor ID, and extractor
  version.
- [ ] 4.2 Attach module/package/target context from the inventory slice when it
  is deterministic.
- [ ] 4.3 Emit `ModuleContextUnavailable` or `ModuleContextAmbiguous` gaps when
  context is missing or unsafe.
- [ ] 4.4 Emit import facts for normal and scoped import declarations with safe
  module path segments and import kind.
- [ ] 4.5 Add tests for conditional imports, scoped imports, ambiguous module
  context, `@_exported` imports, `#if canImport(...)`, and safe import
  metadata.

### Phase 5: Declaration Extraction

- [ ] 5.1 Emit declaration facts for classes, structs, enums, protocols,
  extensions, functions, methods, initializers, properties, subscripts, enum
  cases, typealiases, and associated types where deterministic.
- [ ] 5.2 Generate #381-compatible Swift symbol IDs from safe module context,
  relative path, lexical containment, declaration kind, names, generic arity,
  parameter labels, line span, and syntax hash where needed; if #381 has not
  landed, use only the documented interim file-scoped syntax ID scheme.
- [ ] 5.3 Emit reviewer-friendly display signatures without claiming semantic
  resolution.
- [ ] 5.4 Record function/method/init parameter labels, arity, async/throws
  markers, and signature hashes.
- [ ] 5.5 Record property shape metadata for stored/computed/accessor/wrapper
  syntax only as closed-set syntax evidence.
- [ ] 5.6 Emit syntax relationship evidence for containment, extension target
  syntax, inheritance syntax, and protocol conformance syntax only where
  supporting declaration facts exist.
- [ ] 5.7 Add tests for duplicate names, overloads, extensions, conditional
  declarations, anonymous/ambiguous declarations, generated-code gaps, and
  macro-generated member gaps.

### Phase 6: Basic Call and Object Evidence

- [ ] 6.1 Emit call facts for simple identifier calls, member calls,
  static-looking member calls, `self`/`super` calls, and chained call sites.
- [ ] 6.2 Emit object/construction evidence for initializer-like syntax,
  explicit `.init(...)`, and type construction candidates where the created
  type syntax is safely identifiable.
- [ ] 6.3 Attach nearest containing declaration symbol IDs to calls in methods,
  functions, initializers, property initializers, accessors, closures, local
  functions, and result-builder-like bodies.
- [ ] 6.4 Store argument labels, arity, expression kinds, and expression hashes
  without storing raw argument text.
- [ ] 6.5 Emit gaps or reduced limitations for optional chaining,
  closure-valued callees, dynamic member lookup, operators, key paths,
  Objective-C selectors, macros, result builders, async task bodies, and
  unresolved call shapes.
- [ ] 6.6 Add tests for deterministic fact IDs across duplicate call display
  names using file spans and syntax hashes.
- [ ] 6.7 Add chained-call determinism tests proving nested call receivers are
  recorded as `chained-expression` rather than invented named receiver symbols.
- [ ] 6.8 Add a test proving unresolvable chained-call receivers emit
  `UnsupportedCallShape` or an equivalent limitation/gap instead of a strong
  callee identity.

### Phase 7: Navigation Edges and Storage Rows

- [ ] 7.1 Populate shared `symbols`, `symbol_occurrences`, `fact_symbols`,
  `call_edges`, `object_creations`, and `symbol_relationships` rows only when
  backing facts exist.
- [ ] 7.2 Preserve supporting fact IDs for every derived row.
- [ ] 7.3 Implement same-file or same-module syntax call target matching only
  if exact safe name/signature/arity matching is deterministic and ambiguous
  matches remain unresolved.
- [ ] 7.4 Keep syntax call/navigation edges capped at `Tier3SyntaxOrTextual`.
- [ ] 7.5 Emit gaps instead of strong edges for protocol dispatch, overload
  resolution, Objective-C bridging, dynamic dispatch, dependency injection,
  SwiftUI navigation, storyboard wiring, macro-generated calls, and runtime
  reflection.
- [ ] 7.6 Add SQLite and export/combine compatibility tests for Swift symbols,
  relationships, call edges, object creations, and supporting fact IDs.
- [ ] 7.7 Add an assertion that every derived `call_edges`,
  `object_creations`, and `symbol_relationships` row has a supporting fact ID.
- [ ] 7.8 Add a tier-cap assertion that Swift syntax call, navigation, and
  relationship facts/rows do not exceed `Tier3SyntaxOrTextual`.
- [ ] 7.9 Add Tier2 promotion tests proving Swift declaration/call facts upgrade
  only when #379 inventory provides deterministic package target identity,
  non-ambiguous module name, and no module-context gap for the file.
- [ ] 7.10 Add Tier2 downgrade tests proving `ModuleContextUnavailable` or
  `ModuleContextAmbiguous` keeps SwiftSyntax facts at `Tier3SyntaxOrTextual`.

### Phase 8: Report and Downstream Compatibility

- [ ] 8.1 Update Swift scan report sections with declaration counts, call
  counts, object creation counts, relationship counts, module context status,
  reduced-coverage gaps, and limitations.
- [ ] 8.2 Verify report wording says syntax-backed, static, candidate,
  unresolved, or gap as appropriate.
- [ ] 8.3 Add forbidden-wording tests so reports and exports do not claim
  runtime target, actual navigation, will call, executed, injected, rendered,
  reachable, or impacted without reducer evidence.
- [ ] 8.4 Verify `tracemap reduce` can consume compatible shared Swift fact
  types without treating syntax-only evidence as semantic proof.
- [ ] 8.5 Verify `tracemap combine`, `report`, `paths`, `reverse`, `diff`,
  `impact`, and export either consume Swift rows precisely or label unsupported
  evidence as gaps.
- [ ] 8.6 Add reducer-safety tests for any reused shared semantic fact type,
  especially proving syntax-tier Swift facts are not consumed as
  `Tier1Semantic` method/property proof.
- [ ] 8.7 Add forbidden-wording coverage proving `@_exported import` facts do
  not claim runtime re-export behavior for consumers.

### Phase 9: Documentation and Validation

- [ ] 9.1 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if Swift v0 reveals a
  shared adapter contract gap.
- [ ] 9.2 Update `docs/VALIDATION.md` with Swift build/test/fixture and public
  smoke guidance after exact commands exist.
- [ ] 9.3 Update `docs/ACCEPTANCE.md` with Swift declaration/call acceptance
  scenarios after implementation behavior is real.
- [ ] 9.4 Run the Swift adapter build command selected by the scaffold spec.
- [ ] 9.5 Run the Swift adapter test command selected by the scaffold spec.
- [ ] 9.6 Run a Swift fixture scan and verify required artifacts:
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`.
- [ ] 9.7 Run `dotnet build src/dotnet/TraceMap.sln` when shared .NET
  readers/schema/report behavior changes.
- [ ] 9.8 Run `dotnet test src/dotnet/TraceMap.sln` when shared .NET
  readers/schema/report behavior changes.
- [ ] 9.9 Run relevant `docs/VALIDATION.md` smokes for adapter, combine,
  report, paths, reverse, diff, impact, release-review, or export behavior
  touched by the implementation.
- [ ] 9.10 Run `./scripts/check-private-paths.sh`.
- [ ] 9.11 Run `git diff --check`.

## Spec-Only PR Validation

Run these commands before merging this spec-only PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Patch Medium or higher review findings before opening the PR, then run one
Sonnet or Opus re-review if a patch materially changes requirements, design, or
tasks.

## Deferred Follow-Ups

- Swift semantic analysis through SourceKit/sourcekit-lsp, SwiftPM semantic
  loading, or compiler APIs.
- Xcode build execution and compiler-plugin/macro expansion support.
- Package/dependency surfaces beyond module context.
- SwiftUI/UIKit route or UI surface extraction.
- HTTP, storage, config, serializer, package, and dependency surfaces.
- Parameter/value-origin flow, local aliases, field aliases, and derived
  parameter-forward edges.
- Protocol witness resolution, override target resolution, overload selection,
  Objective-C selector binding, runtime navigation, and dynamic dispatch
  approximation.
- Device/simulator/runtime inspection.
