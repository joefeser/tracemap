# SwiftSyntax Declarations and Basic Call Facts Requirements

## Introduction

TraceMap needs a conservative Swift adapter v0 slice that uses SwiftSyntax to
extract source declarations and basic syntax-visible call evidence from Swift
repositories. This spec covers GitHub issue
[#380](https://github.com/joefeser/tracemap/issues/380), a child of the Swift
v0 runway issue [#377](https://github.com/joefeser/tracemap/issues/377).

This is a spec-only phase. It must not implement Swift analyzer code, runtime
code, CLI dispatch, rule catalog entries, fixtures, or docs outside this spec
folder.

The implementation target is a SwiftSyntax-backed static extractor that emits
declarations, declaration identities compatible with the Swift symbol identity
slice, call/object evidence, safe navigation edges, and reduced-coverage gaps
with rule IDs, evidence tiers, file paths, line spans, commit SHA, supporting
fact IDs where derived, and extractor versions.

This is not Swift semantic analysis and not runtime proof. The adapter must not
execute app code, run builds as part of basic extraction, connect to
simulators/devices, inspect app runtime state, infer dynamic dispatch targets,
or store raw source snippets by default.

## Source Material

- GitHub issue #380: Swift adapter v0: SwiftSyntax declarations and basic call
  facts.
- GitHub issue #377: Swift adapter v0 runway.
- GitHub issue #378: Swift adapter v0: scaffold CLI and output contract.
- GitHub issue #379: Swift adapter v0: inventory and project/package discovery.
- GitHub issue #381: Swift adapter v0: symbol identity and relationships.
- `docs/ADAPTER_RUNWAY.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/NEXT_EXECUTION_REPORT.md`
- Existing adapter planning examples:
  - `.kiro/specs/jvm-indexer/`
  - `.kiro/specs/python-depth-pass/`
  - `.kiro/specs/interface-override-di-approximation/`

## Public Claim Level

The public claim for this slice is:

> Swift v0 can emit deterministic, syntax-backed declaration and basic call
> evidence for supported Swift files, with explicit reduced-coverage gaps for
> language, toolchain, generated-code, and runtime behavior that static
> SwiftSyntax extraction cannot prove.

The public claim must not say that TraceMap proves runtime navigation, actual
method dispatch, Objective-C selector binding, macro expansion behavior,
dependency-injection state, SwiftUI runtime view reachability, UIKit storyboard
wiring, branch feasibility, thread scheduling, device behavior, or production
usage.

## MVP Scope Decisions

- Use SwiftSyntax parsing as the v0 source-code extraction mechanism.
- Treat SwiftSyntax declaration and expression evidence as
  `Tier3SyntaxOrTextual` unless a separate implementation proves stronger
  structural evidence from package/project metadata.
- Emit module/package context only when issue #379-style project inventory or
  directly parsed metadata provides deterministic context. Otherwise record a
  reduced-coverage module/package gap.
- Treat issue #381 as the owner of canonical Swift symbol identity and
  relationship semantics. This slice may define the declaration/call evidence
  that feeds those identities, but it must not diverge from #381's symbol ID or
  relationship contract once that slice exists.
- Extract declarations for Swift files, imports, modules/packages where known,
  classes, structs, enums, protocols, extensions, functions, methods,
  initializers, and properties where deterministic from syntax.
- Extract basic call facts and navigation edges only for syntax-visible
  invocation forms with a deterministic containing declaration and callee
  syntax identity.
- Emit object/construction evidence for initializer and type-construction
  syntax where the created type syntax is safely identifiable.
- Emit file spans and #381-compatible symbol IDs/signatures for every
  declaration and call fact where the source location can be mapped. If #381 is
  not yet implemented, use documented file-scoped syntax IDs as an interim
  compatibility layer and emit identity-coverage gaps where needed.
- Preserve supporting fact IDs for derived rows such as call edges, navigation
  edges, object creation rows, and future combined imports.
- Use explicit `AnalysisGap` facts and reduced scan coverage for unsupported
  SwiftSyntax/toolchain states, macros, generated code, Objective-C bridging,
  conditional compilation ambiguity, unavailable project/package context,
  parser diagnostics, skipped files, and dynamic language/runtime behavior.
- Do not add SourceKit, sourcekit-lsp, SwiftPM semantic loading, Xcode project
  builds, macro expansion execution, simulator/device inspection, or runtime UI
  discovery in this slice.

## Requirements

### Requirement 1: SwiftSyntax Availability and Coverage

**User Story:** As a maintainer, I want the Swift extractor to stay useful when
SwiftSyntax or a full Swift toolchain is unavailable, while labeling partial
analysis honestly.

#### Acceptance Criteria

1. WHEN SwiftSyntax parsing is available and a selected `.swift` file parses
   without unrecoverable diagnostics THEN the adapter SHALL emit syntax-backed
   declaration and call facts for recoverable supported nodes.
2. WHEN SwiftSyntax is unavailable, version-incompatible, or cannot parse a
   selected file THEN the adapter SHALL emit `AnalysisGap` facts where it can
   still write a scan artifact set, SHALL mark Swift coverage as reduced, and
   SHALL NOT claim clean Swift analysis.
3. WHEN the adapter cannot determine a concrete Git commit SHA THEN the scan
   SHALL fail before writing a successful artifact set, following the language
   adapter contract.
4. WHEN parser diagnostics, skipped files, unreadable files, generated-code
   exclusions, conditional compilation ambiguity, macro syntax, or unsupported
   language constructs are encountered THEN the adapter SHALL continue over
   other files and emit bounded gap evidence with safe metadata.
5. WHEN no Swift files are selected THEN the adapter SHALL emit an inventory or
   selection gap and SHALL not fabricate Swift declarations.
6. WHEN scan coverage is reduced for any Swift declaration or call evidence
   family THEN the manifest/report SHALL label coverage as partial or reduced
   and downstream outputs SHALL preserve the gap rule IDs and evidence tiers.

### Requirement 2: File, Module, Package, and Import Facts

**User Story:** As a reviewer, I want every Swift declaration and call fact to
be grounded in file and module/package context when that context is available.

#### Acceptance Criteria

1. WHEN a `.swift` file is selected THEN the adapter SHALL emit or preserve a
   `FileInventoried`-style fact with relative file path, file kind, size or
   content hash metadata, rule ID, evidence tier, line span, extractor ID, and
   extractor version.
2. WHEN project/package inventory supplies a deterministic module, target, or
   package identity for a file THEN declaration and call facts SHALL include
   that identity in safe properties and symbol ID inputs.
3. WHEN module/package context is absent or ambiguous THEN declaration and call
   facts SHALL remain useful with file-scoped identity, and the adapter SHALL
   emit a module/package coverage gap rather than guessing.
4. WHEN an `import` declaration is parsed THEN the adapter SHALL emit an import
   evidence fact with imported module name, import kind when visible, file span,
   rule ID, evidence tier, extractor ID, and extractor version.
5. WHEN imports include scoped or qualified forms such as `import struct`,
   `import class`, `import enum`, `import protocol`, or `import func` THEN the
   adapter SHALL record the visible import kind and path segments as safe
   metadata without implying the imported symbol exists.
6. WHEN an import has an `@_exported` attribute THEN the adapter SHALL record a
   closed-set safe property such as `exportedImport = true` and SHALL NOT claim
   that consumers receive or use the re-exported module at runtime.
7. WHEN conditional compilation controls whether an import or file section is
   active THEN the adapter SHALL record syntax evidence and a reduced-coverage
   gap; it SHALL not choose a runtime build configuration unless explicitly
   configured and documented in a future spec.
8. WHEN `#if canImport(...)` controls an import or declaration THEN the adapter
   SHALL treat it as conditional compilation ambiguity and emit a
   `CanImportConditionalAmbiguous` gap unless a future toolchain-aware spec
   defines a deterministic configuration.
9. WHEN issue #379 inventory provides a deterministic package/target identity,
   non-ambiguous module name, and no module-context gap for a file THEN
   declaration facts for that file MAY be emitted at `Tier2Structural`;
   otherwise SwiftSyntax declaration and call facts SHALL remain
   `Tier3SyntaxOrTextual`.

### Requirement 3: Declaration Facts and Symbol Identity Inputs

**User Story:** As a contract reviewer, I want Swift declarations represented
with stable syntax-backed identities so reducer, combine, report, and export
can reason about them without raw source snippets.

#### Acceptance Criteria

1. WHEN SwiftSyntax finds a class, struct, enum, protocol, extension, function,
   method, initializer, property, subscript, enum case, typealias, or associated
   type declaration in supported scope THEN the adapter SHALL emit a
   declaration fact with declaration kind, safe name when visible, containing
   declaration identity when available, file path, line span, rule ID, evidence
   tier, extractor ID, and extractor version.
2. WHEN a declaration's stable identity can be constructed from safe module or
   package context, relative file path, lexical containment, declaration kind,
   name, generic arity, parameter labels, and line span THEN the adapter SHALL
   emit a Swift symbol ID and display signature that follows the canonical
   contract defined by issue #381. If #381 has not landed, the implementation
   SHALL document the interim file-scoped syntax ID scheme and preserve a
   migration note so the #381 implementation can converge it.
3. WHEN a declaration is anonymous, ambiguous, duplicated by conditional
   compilation, or lacks a safe name THEN the adapter SHALL derive a
   file-scoped syntax ID from safe path, declaration kind, line span, and a
   normalized syntax hash, SHALL mark the identity as syntax-scoped or
   ambiguous, and SHALL keep that scheme compatible with the #381 collision
   and migration rules.
4. WHEN a function, method, or initializer has parameters THEN the adapter
   SHALL record safe parameter labels, argument labels, arity, async/throws
   markers when visible, and a signature hash; it SHALL NOT claim overload
   resolution or type checking.
5. WHEN a property declaration includes getter/setter syntax, stored-property
   syntax, wrapper attributes, or initializer syntax THEN the adapter MAY record
   closed-set property shape metadata, but SHALL NOT infer runtime storage,
   wrapper behavior, persistence, or side effects.
6. WHEN an extension declaration lacks an explicit nominal owner symbol that can
   be resolved semantically THEN the adapter SHALL record the extended type
   syntax as a safe display/hash and SHALL label relationships as syntax-only.
7. WHEN declarations are generated, macro-expanded, unavailable because macro
   expansion was not run, or hidden in unavailable generated files THEN the
   adapter SHALL emit generated/macro coverage gaps rather than inventing
   declarations.

### Requirement 4: Basic Call and Object Creation Facts

**User Story:** As an investigator, I want syntax-visible Swift calls and
construction sites represented as evidence, while staying clear about what
syntax cannot resolve.

#### Acceptance Criteria

1. WHEN SwiftSyntax finds a function-call expression, member-call expression,
   initializer expression, or syntactic type construction inside a known
   containing declaration THEN the adapter SHALL emit a call or object evidence
   fact with caller symbol ID, callee syntax identity, call kind, argument label
   list, arity, file span, rule ID, evidence tier, extractor ID, and extractor
   version.
2. WHEN the callee is a simple identifier, member access, static member access,
   `self`/`super` member access, or initializer-like syntax THEN the adapter
   SHALL record the safest visible callee name/path and a callee syntax hash.
3. WHEN the callee expression is dynamic, closure-valued, optional-chained,
   result-builder-produced, macro-generated, operator-overloaded, subscripted,
   generic-specialized, key-path-based, selector-based, reflective, or otherwise
   unresolved THEN the adapter SHALL emit syntax evidence with reduced
   limitations or an `AnalysisGap`; it SHALL not invent a resolved target.
4. WHEN an initializer or object construction syntax is visible THEN the
   adapter SHALL default to a Swift-specific syntax construction candidate with
   created type syntax, constructor label shape, assignment/receiver shape when
   safely visible, and no runtime allocation proof. It SHALL emit shared
   `ObjectCreated` facts only after a dedicated reducer-safety test passes.
5. WHEN a call occurs in a closure, local function, property initializer,
   computed property accessor, result builder body, async task body, or
   callback-like argument THEN the adapter SHALL attach the nearest known
   containing declaration and MAY emit a callback/closure boundary gap; it
   SHALL not claim invocation order or runtime scheduling.
6. WHEN argument expressions need identity for future flow work THEN the
   adapter SHALL store expression kind, argument label, ordinal, and expression
   hash, not raw expression text.
7. WHEN duplicate or ambiguous syntax call sites produce the same display
   identity THEN fact IDs SHALL remain deterministic and distinct by including
   safe file span and normalized syntax hash inputs.

### Requirement 5: Navigation and Relationship Edges

**User Story:** As a reviewer, I want direct navigation edges from syntax-backed
calls and declarations only where the evidence is safe, not guessed protocol or
runtime dispatch.

#### Acceptance Criteria

1. WHEN a call fact has a known containing declaration and a safe callee syntax
   identity THEN the adapter MAY emit a syntax call edge or navigation edge
   carrying source symbol ID, target syntax identity or unresolved target
   placeholder, file span, supporting call fact ID, rule ID, evidence tier,
   extractor ID, and extractor version.
2. WHEN a syntax call edge target cannot be resolved to a declared symbol in
   the same scanned source scope by deterministic local syntax matching THEN
   the target SHALL remain unresolved or syntax-scoped; the adapter SHALL NOT
   use name similarity to claim a semantic target.
3. WHEN local same-file or same-module declaration matching is implemented THEN
   it SHALL be explicitly documented as syntax-only matching, SHALL include
   supporting declaration fact IDs, and SHALL stay capped at
   `Tier3SyntaxOrTextual`.
4. WHEN the call may dispatch through protocols, generics, extensions,
   overloads, dynamic member lookup, Objective-C selectors, delegates,
   callbacks, dependency injection, SwiftUI result builders, property wrappers,
   macro-generated members, or runtime reflection THEN the adapter SHALL emit a
   gap or limitation instead of a strong navigation conclusion.
5. WHEN declaration relationship syntax is visible, such as inheritance,
   protocol conformance, protocol inheritance, extension target syntax, or
   member containment THEN the adapter MAY emit syntax relationship evidence
   with relationship kind, source/target syntax identities, file span,
   supporting fact IDs, and syntax-only limitations only as an input to the
   canonical relationship contract owned by issue #381.
6. WHEN relationship syntax cannot prove the actual resolved type, selected
   overload, protocol witness, override target, associated type binding, or
   Objective-C bridge THEN the adapter SHALL cap evidence at syntax tier and
   preserve a limitation or gap. It SHALL NOT claim canonical semantic
   relationship rows beyond the #381 contract.

### Requirement 6: SQLite, NDJSON, Report, and Combine Compatibility

**User Story:** As an automation author, I want Swift declaration and call
facts to flow through the same TraceMap artifact contract as other adapters.

#### Acceptance Criteria

1. WHEN the implementation emits Swift declaration or call facts THEN
   `facts.ndjson` SHALL include deterministic fact IDs, scan ID, repo, commit
   SHA, fact type, rule ID, evidence tier, source/target symbols when
   available, file path, line span, extractor ID, extractor version, and sorted
   string properties.
2. WHEN `index.sqlite` is written THEN Swift declaration, symbol, occurrence,
   relationship, call edge, object creation, and fact-symbol data SHALL use the
   shared SQLite schema where available and SHALL avoid Swift-only divergent
   table definitions.
3. WHEN a Swift fact participates in `symbols`, `symbol_occurrences`,
   `fact_symbols`, `symbol_relationships`, `call_edges`, or
   `object_creations` THEN the row SHALL preserve the supporting fact ID and
   rule/evidence metadata needed by combine/report/export readers.
4. WHEN existing reducers consume declaration or usage evidence THEN Swift
   facts SHALL reuse shared fact types only where their reducer semantics match
   the Swift evidence tier. Syntax-only Swift call and member-access evidence
   SHALL default to weaker or Swift-specific fact types, such as
   `InvocationName`-style call evidence or syntax `CallEdge` rows, and SHALL NOT
   emit reducer-semantic `MethodInvoked` or `PropertyAccessed` facts unless
   tests prove those Tier3 facts cannot be consumed as semantic proof.
   `TypeDeclared` and `AnalysisGap` MAY be reused only with cataloged syntax
   limitations and reducer-safety tests. Construction evidence SHALL default to
   a Swift-specific candidate unless shared `ObjectCreated` safety is proven by
   the separate checks in Requirement 4.
5. WHEN `tracemap combine`, `report`, `paths`, `reverse`, `diff`, `impact`, or
   export cannot consume a Swift-specific row precisely THEN they SHALL label
   the unsupported or reduced evidence as a gap, not silently upgrade it.
6. WHEN report output summarizes Swift declaration and call evidence THEN it
   SHALL say syntax-backed, static evidence, candidate, or unresolved as
   appropriate, and SHALL not say runtime target, will call, actual navigation,
   executed, or impacted without reducer evidence.

### Requirement 7: Rule Catalog and Limitations

**User Story:** As a maintainer, I want every Swift rule ID documented before
product code emits it.

#### Acceptance Criteria

1. WHEN the implementation introduces or changes a Swift rule ID THEN
   `rules/rule-catalog.yml` SHALL be updated before scanner, storage, report,
   or reducer tests assert emitted evidence.
2. WHEN a rule catalog entry is added THEN it SHALL follow the existing
   `rules/rule-catalog.yml` shape: `id`, `name`, `description`,
   `evidenceTier`, `emits`, and `limitations`. Required property expectations,
   known false positives, and known false negatives SHALL be folded into the
   description or limitations prose unless a separate schema-change spec extends
   the catalog validator.
3. WHEN rule IDs are still proposed in this spec THEN they SHALL be treated as
   planning names only and SHALL NOT be emitted by product code until cataloged.
4. WHEN implementation tasks add or reuse rule-catalog validation hooks THEN
   the implementation SHALL state whether the hook is new infrastructure or an
   existing adapter/test helper, and tests SHALL fail if any emitted Swift fact
   references an uncataloged rule ID.
5. WHEN a rule's limitation includes runtime-only Swift behavior THEN reports
   and exports SHALL preserve that limitation wherever the evidence is surfaced.

### Requirement 8: Safety, Determinism, and Public Artifacts

**User Story:** As a public TraceMap user, I want Swift evidence outputs to be
deterministic and safe to review without leaking code or private environment
details.

#### Acceptance Criteria

1. WHEN outputs are generated twice from the same repo commit and scan options
   THEN fact ordering, IDs, JSON property ordering, SQLite rows, report
   summaries, and gap ordering SHALL be deterministic.
2. WHEN source snippets, raw expressions, raw comments, raw string literals,
   raw URLs, hostnames, local absolute paths, raw remotes, secrets, credentials,
   signing metadata, bundle IDs, provisioning profile data, or private labels
   could appear THEN the adapter SHALL omit or hash them by default.
3. WHEN raw snippets are ever added in a future feature THEN they SHALL require
   an explicit option and SHALL remain out of scope for this v0 slice.
4. WHEN SwiftSyntax normalized syntax hashes are stored THEN they SHALL be used
   as evidence fingerprints, not as recoverable source text.
5. WHEN reports render file paths THEN they SHALL use repo-relative paths and
   line spans, not developer-local absolute paths.
6. WHEN a gap, call, declaration, edge, or relationship is emitted THEN it
   SHALL include enough safe provenance for a reviewer to inspect the source
   locally: repo, commit SHA, relative path, line span, rule ID, evidence tier,
   and extractor version.

### Requirement 9: Validation Expectations

**User Story:** As a future implementer, I want exact validation commands and
fixtures identified before implementation starts.

#### Acceptance Criteria

1. WHEN this spec-only PR is prepared THEN validation SHALL include:

   ```bash
   node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
   node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
   git diff --check
   ./scripts/check-private-paths.sh
   ```

2. WHEN implementation begins THEN validation SHALL add Swift adapter build and
   test commands chosen by the scaffold spec, plus existing repo validation
   affected by shared schema/report changes.
3. WHEN a Swift adapter implementation changes shared schema, reducer,
   combine, report, path, reverse, diff, impact, release-review, or export
   behavior THEN the implementation SHALL run the relevant commands from
   `docs/VALIDATION.md` or explicitly defer them with a reason.
4. WHEN a Swift local sample fixture is added THEN it SHALL prove declarations,
   imports, calls, object construction, reduced coverage, deterministic output,
   and public-safe redaction.
5. WHEN public smoke guidance is added THEN it SHALL use generic labels and
   placeholders; committed artifacts SHALL NOT include private local paths or
   private repository names.

## Out of Scope

- Implementing `src/swift` or any Swift analyzer/runtime code in this PR.
- Editing rule catalog, docs, CLI, samples, or tests in this spec-only PR.
- Full Swift semantic analysis, SourceKit/sourcekit-lsp enrichment, SwiftPM
  build loading, Xcode build execution, macro expansion execution, or compiler
  plugin support.
- Runtime UI navigation, actual method dispatch, protocol witness resolution,
  Objective-C selector resolution, dependency-injection state, environment
  values, storyboard runtime wiring, device/simulator inspection, branch
  feasibility, callback scheduling, production usage, or impact conclusions.
- SwiftUI/UIKit route or surface extraction beyond syntax-backed declarations
  and basic calls.
- Package/dependency, HTTP, storage, serializer, config, or UI surface depth
  outside declarations and basic call/object facts.
- LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact-analysis claims.
