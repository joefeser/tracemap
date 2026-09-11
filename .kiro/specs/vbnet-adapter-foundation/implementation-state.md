# VB.NET Adapter Foundation Implementation State

- Status: complete (spec tasks 1-11 done; PR to `dev` opened from this branch)
- Branch: `codex/issue-736-vbnet-adapter-foundation`
- Slice start: `673cf6f19af4b470f4a031479317fed3947d26b9` (foundation slice,
  tasks 1-4)
- Slice end: `23c5cca0d550faf93662e4d0ae35c10e8f8baf0c` (tasks 5-7)
- Completion slice: `ad35b8ba` (smoke-surfaced fixes), `40f80456` (task 8
  matrix), `139875fc` (task 9 pin), `27731336` (task 10 docs), plus the
  task-11 state update
- Base: `origin/dev` at `c50f82ce0920d3c948e6ec798c6eb4b4d0959c24`
- Fixture corpus merged into this branch via PR #740 (merge commit
  `a357f7b9`); `samples/vb-modern-sample`, `samples/vb-legacy-sample`,
  `samples/vb-webforms-sample` are consumed directly by the implementation
  tests.
- Issue: [#736](https://github.com/joefeser/tracemap/issues/736)
- Parent: [#1](https://github.com/joefeser/tracemap/issues/1)
- Follow-ups: [#738](https://github.com/joefeser/tracemap/issues) for
  events/Web Forms and [#737](https://github.com/joefeser/tracemap/issues)
  for data/external boundaries.

## Implemented in the foundation slice (tasks 1-4, commit 673cf6f1)

- **Inventory/classification (task 2).** `FileInventory` includes `.vb` and
  classifies `VisualBasic`, `VisualBasicCodeBehind`, `VisualBasicDesigner`,
  `VisualBasicGenerated`, `VisualBasicAssemblyInfo`, and a dedicated
  `VisualBasicProject` kind. Filename conventions are structural evidence
  only, never semantic proof.
- **Snapshot protection (task 2).** `CaptureSemanticInputSnapshot` covers VB
  source kinds and projects; the extractor registers repo-local VB documents
  as protected compilation inputs with SDK/compiler-generated documents
  skipped.
- **Packages, versions, rules (task 3).** Roslyn Visual Basic packages pinned
  at 5.3.0; extractor identity `vb-semantic/*`; rules
  `vb.semantic.compilation.v1` and `vb.semantic.workspace.v1`.
- **Project selection and semantic loading (task 4).** Solution/standalone
  `.vbproj` selection, MSBuild registration, opt-in restore, per-project
  compilations, sanitized gaps, and honest reduced coverage. The C# extractor
  skips non-C# projects inside loaded solutions (one language owner per
  project). My-template no-source-location diagnostics are excluded from
  coverage reduction and counted as `injectedTemplateErrorCount`; this
  bounded behavior is preserved unchanged.

## Implemented in the extraction slice (tasks 5-7, commit 23c5cca0)

- **Task 5 - compiler-backed semantic facts.** `VisualBasicSemanticExtractor`
  (identity `vb-semantic/0.3.0`) walks each repo-local, inventoried,
  non-generated VB document of every compiled project and emits Tier1 facts
  under `vb.semantic.declarations.v1` (TypeDeclared for
  class/module/structure/interface/enum/delegate, MethodDeclared incl.
  constructors, PropertyDeclared, FieldDeclared, ParameterDeclared),
  `vb.semantic.propertyaccess.v1` (PropertyAccessed incl. unambiguous
  default-member indexing such as `catalog(0)`),
  `vb.semantic.methodinvocation.v1` (MethodInvoked),
  `vb.semantic.callgraph.v1` (CallEdge, `SemanticMethodInvocation` and
  `SemanticObjectCreation` kinds), `vb.semantic.objectcreation.v1`
  (ObjectCreated), `vb.semantic.valueflow.v1` (ArgumentPassed with
  compiler-resolved parameter binding via `IArgumentOperation`, covering
  named, ByRef, optional-bound, and params-expanded arguments), and
  `vb.semantic.symbolrelationship.v1` (SymbolRelationship kinds
  `InheritsFrom`, `ExtendsInterface`, `ImplementsInterface`,
  `ImplementsInterfaceMember`, `Overrides`). Every fact carries rule ID,
  tier, repo-relative path, one-based line span, commit SHA, project path,
  and extractor identity/version.
- **Symbol identity.** `VisualBasicSymbolIdentityProvider` mirrors the
  canonical .NET normalization with `visualbasic` language tags, so VB-scan
  symbol rows are distinguishable from C# rows while keeping the same ID
  shape. Cross-language symbol-identity joins (VB scan symbols to C#-declared
  symbols) are NOT established; display strings, assembly names, and
  fact-level evidence still cross the language boundary. Both rules' catalog
  limitations document this.
- **Deliberate exclusions (documented in catalog limitations).**
  `Handles`/`AddHandler`/`RemoveHandler`/`RaiseEvent`/
  `WithEvents` wiring are never promoted to resolved event edges (#738 owns
  event composition; tests assert none appear). Operator/`Declare`
  (P/Invoke) statements and LocalAlias/FieldAlias families are not emitted in
  this slice. Designer/generated documents are not analyzed for semantic
  facts. Event declarations are retained, but never promoted to event wiring.
  Unresolved or late-bound invocation and constructor sites retain bounded
  Tier3 evidence plus Tier4 gaps, never a guessed Tier1 target.
- **Task 6 - bounded per-file syntax fallback.** `VisualBasicSyntaxExtractor`
  (`vb-syntax/0.2.0`) runs inside the shared SyntaxFallback stage over
  inventoried VB files that received NO compiler-resolved coverage
  (`SemanticAnalysisUnavailable` Tier4 gap per file under
  `vb.semantic.workspace.v1`). It emits Tier3 facts under
  `vb.syntax.declarations.v1`, `vb.syntax.memberaccess.v1`,
  `vb.syntax.invocation.v1`, `vb.syntax.callgraph.v1`, and
  `vb.syntax.objectcreation.v1` with text-only callees (`SyntaxInvocation` /
  `SyntaxObjectCreation` call kinds), never symbol-ID joins and never
  compiler-resolved targets. Bounds are deterministic: 2000 facts per file
  with a `SyntaxFallbackBudgetExhausted` gap, 20 parse-diagnostic gaps per
  file carrying diagnostic code + message hash only (no raw message text).
  Designer/generated/auto-generated-header files are skipped. Files with
  partial binding keep Tier1 facts + gaps and get no Tier3 duplicates.
- **Task 7 - shared joins and project metadata.** VB facts reuse the shared
  fact types and property conventions, so `SqliteIndexWriter` populates
  `symbols` (language `visualbasic`), `fact_symbols`, `symbol_occurrences`,
  `call_edges`, `object_creations`, `argument_flows`,
  `symbol_relationships`, and `parameter_forward_edges` from VB evidence
  with no new schema. `ProjectFileReader` reads target frameworks and
  package references for `VisualBasicProject` inventory items
  (`manifestKind=vbproj`); `PackageReferenced` facts set `projectPath` for
  `.vbproj`. Project *references* are not read by `ProjectFileReader` for
  either language (MSBuild workspace loading and binlog evidence carry
  them); VB is at C# parity. Ordering stays deterministic through
  `SemanticExtractionResultMerge` and the existing final fact sort.

## Implemented in the completion slice (tasks 8-11)

- **Two shared-code defects surfaced by the pinned smoke (commit ad35b8ba).**
  1. `LegacyWebFormsExtractor.WebFormsDirectEvidenceIndex.SourceMemberName`
     threw `ArgumentOutOfRangeException` on fact symbol text shaped like
     `()` — the Visual Basic display string of an anonymous-method caller
     (`Sub() ...` lambdas), where the member-name portion is empty. The
     member-name join now returns null for signature-shaped text; such
     symbols still join by full symbol text. Regression-covered.
  2. `BuildEnvironmentDiagnosticExtractor.SafeDiagnosticIdRegex` only
     accepted `CS`/`MSB` ids, so Visual Basic `BCxxxxx` compiler diagnostic
     ids were dropped from `vb.semantic.workspace.v1` gaps. The pattern now
     also accepts `BCxxxxx`. Besides labeling gaps, this stopped silent
     collapse of distinct compiler diagnostics: the final fact list dedupes
     by FactId, which hashes all properties, and gaps without a diagnostic
     id at the same location shared identical properties and FactIds. VB now
     behaves like C#, whose gaps always carried `CSxxxxx` ids. Visible
     effect: the legacy fixture scan went from 140 to 163 facts — 23 gap
     facts that previously collapsed are now distinct; Tier1 counts are
     unchanged.
- **Task 8 - validation matrix (`VisualBasicValidationMatrixTests`, 8
  tests).** Generated/designer-source handling (designer/`.g.vb` skipped by
  semantic analysis and fallback under the documented filename-convention
  boundary; `<auto-generated>`-header files keep compiler-resolved analysis
  like C# and stay protected compilation inputs); protected-input mutation
  inside the live capture-extract-verify sequence failing with the typed
  `SourceSnapshotException` (including mutating a designer file); per-file
  semantic/syntax-fallback boundary (compiled file Tier1-only, orphan file
  Tier3-only with exactly one `SemanticAnalysisUnavailable` gap); sanitized
  gap honesty over the legacy fixture (category-only messages, no paths or
  source text, bounded `BCxxxxx` ids, no Tier3 duplicates for partially
  bound files); byte-identical repeated CLI scans; public artifact safety
  over every output file (temp path, value sentinel, and a parse-broken
  file's unique identifier must not appear — raw compiler diagnostic text is
  suppressed); and cross-family symbol-identity stability for modules,
  default properties, constructors (`.ctor`), named arguments, overrides,
  and interface-member implementations.
- **Task 9 - pinned OSS smoke.** Pinned `community-visual-basic`
  (`CommunityVB/Community.VisualBasic`, MIT) at
  `20d2a51dfc9f342848ad134952ceaa8d79302559` (re-verified via `git
  ls-remote` and pinned checkout on 2026-09-11), wired into
  `scripts/smoke-open-source-repos.sh`, documented with recorded totals in
  `docs/VBNET_FIXTURES.md`. Candidates `dotnet/roslyn`, `mono/mono-basic`,
  and `dotnet/docs` are documented as considered and not pinned.
  Operational requirement discovered and documented: the clone must be reset
  with `git clean -fdx` between scans because design-time builds write
  `obj/` state inside the clone that changes later design-time loads
  (without the reset, fact counts drifted between runs; with it, repeated
  scans are byte-identical).
- **Task 10 - documentation.** New `docs/VBNET_ADAPTER.md` (extractor
  identities, fact families, tiers, fallback boundary, supported project
  types, explicit not-established limits: cross-language symbol-identity
  joins and Web Forms event wiring). `docs/VALIDATION.md` gained a VB.NET
  adapter section with exact commands and expected fixture postures plus the
  OSS smoke table row. `docs/ACCEPTANCE.md` gained VB acceptance bullets.
  `rules/rule-catalog.yml` documents the bounded BC diagnostic id on
  `vb.semantic.workspace.v1` gaps.
- **No changes to C# extraction.** The completion slice touched only the two
  shared-code defects above plus tests/docs; the full suite and the C#
  regression tests stay green.

## Key decisions and oddities

- **Fallback granularity is per-file.** Syntax fallback fires only for files
  with zero semantic coverage (failed load, orphan files). A file with
  partial binding keeps its Tier1 facts and compiler-diagnostic gaps; adding
  Tier3 duplicates would blur evidence strength. This differs from C#, whose
  syntax extractor runs unconditionally; the difference is intentional and
  requirement-driven.
- **Broken-vbproj behavior.** An unparseable `.vbproj` does not always throw
  from `OpenProjectAsync`; MSBuildWorkspace may surface it as a workspace
  diagnostic plus a compilation diagnostic at the project path. The scan
  stays `FailedOrPartial`/`...Reduced` with sanitized gaps either way.
- **Sanitized no-project gap.** A repo with VB files but no VB project emits
  the `NoVisualBasicProjectOrSolution` gap whose manifest message is the
  sanitized category. This mirrors C# symmetric behavior.
- **My-template exclusion preserved.** The bounded
  no-source-location/My-template suppression and
  `injectedTemplateErrorCount` reporting are unchanged; source-referencing
  compiler errors still reduce coverage.
- **Designer boundary detail.** Semantic analysis skips designer/generated
  documents by filename convention (`.designer.vb`, `.g.vb`, `.generated.vb`,
  assembly-info); files whose only generated marker is an
  `<auto-generated>` header are analyzed semantically (C# parity — the C#
  semantic extractor also analyzes them) and only skip the syntax fallback.
  Both behaviors are pinned by tests.
- **Fixture `obj/` state.** Design-time builds write `obj/`/`bin/` into
  scanned fixture directories (gitignored). Repeated scans of the same
  checkout are stable, but a cleaned checkout can legitimately produce
  different compiler-diagnostic populations than an accumulated one. The
  OSS smoke therefore requires `git clean -fdx` between runs; checked-in
  fixture expectations were recorded on a clean checkout.
- One intentional asymmetry: `ReadTargetFrameworks` reads SDK-style
  `TargetFramework`/`TargetFrameworks` elements only; legacy
  `TargetFrameworkVersion` (vb-webforms/vb-legacy) is covered by the build
  environment diagnostics, same as legacy C# projects.
- **MSBuildRegistrationFailed watch.** No recurrence during this slice
  (dozens of scans + full suite). The single historical non-reproducing
  occurrence from the foundation slice remains documented here; nothing is
  suppressed.
- Pre-existing analyzer note: `VisualBasicSemanticExtractor.cs(613)` emits
  an RS1039 warning claiming `GetDeclaredSymbol(MethodBaseSyntax)` always
  returns null; runtime behavior is proven otherwise by the MethodDeclared
  assertions across the VB suites (constructors and methods emit). Left
  unchanged; not introduced by this branch.

## Tests added (completion slice)

`src/dotnet/tests/TraceMap.Tests/VisualBasicValidationMatrixTests.cs`
(8 tests): generated/designer boundary + designer mutation protection;
protected-source mutation in the live scan sequence; per-file fallback
boundary; legacy reduced-coverage gap sanitization + BC ids + no-duplicate
assertions; byte-identical repeated CLI scans; public artifact safety;
symbol-identity stability across fact families and VB constructs; lambda
caller-text regression over the full scan pipeline.

## Validation recorded (2026-09-11, macOS arm64, .NET SDK 10.0.302)

- `dotnet build src/dotnet/TraceMap.sln` - clean (one pre-existing RS1039
  analyzer warning, see notes).
- Full `dotnet test src/dotnet/TraceMap.sln` - 1866/1866 passed (was 1858
  before task 8).
- Focused: `FullyQualifiedName~VisualBasic|FullyQualifiedName~VbNetFixture`
  - 34/34 passed (14 extraction + 9 foundation + 8 validation matrix +
    3 fixture).
- CLI fixture scans (final code):
  - `samples/vb-modern-sample` -> `Level1SemanticAnalysis` / `Succeeded`,
    220 facts (206 Tier1): 31 call edges, 31 argument flows, 6 object
    creations, 6 symbol relationships, 119 `visualbasic` symbols,
    9 parameter-forward edges. Repeat scan byte-identical
    (`facts.ndjson` `cmp` clean).
  - `samples/vb-legacy-sample` -> `Level1SemanticAnalysisReduced` /
    `FailedOrPartial`, 163 facts: 24 partial Tier1 facts, 112 sanitized
    workspace/compilation gaps carrying bounded BC ids (23 more gap facts
    than the 140 recorded at the extraction slice; see the FactId
    de-collision note above - Tier1 evidence is unchanged).
  - `samples/vb-webforms-sample` -> `Level1SemanticAnalysisReduced` /
    `FailedOrPartial`, 151 facts: handler declarations, code-behind object
    creations, and resolved calls only; event wiring never becomes edges.
- `python3 scripts/validate-adapter-artifacts.py` passes on all three scan
  outputs (enforces rule registration against `rules/rule-catalog.yml`).
- Pinned OSS smoke (`community-visual-basic` @ `20d2a51d`):
  `Level1SemanticAnalysisReduced` / `FailedOrPartial`, 71,940 facts,
  6,341 `visualbasic` symbols, 1,087 `vb.semantic` call edges, 518 object
  creations, 175 argument flows, 98 symbol relationships, 92
  parameter-forward edges, 62,358 category-only `AnalysisGap` rows;
  zero `vb.syntax.*` facts (per-file boundary); validator passes; no local
  paths in any artifact; two clean-reset runs byte-identical.
- Privacy: no local absolute paths and no raw diagnostic message text in any
  validated output; value sentinels suppressed (test-enforced).
- `git diff --check` clean; `scripts/check-private-paths.sh` passes.

## Remaining work / follow-ups

- Cross-language symbol-identity joins (VB symbols to C#-declared symbols)
  remain unestablished; display-string and fact-level evidence cross the
  language boundary. Future slice.
- Event composition (Handles/AddHandler/WithEvents/RaiseEvent, .aspx control
  events) is #738. Data/external boundaries (ADO-class surfaces) are #737.
- `dotnet/roslyn` (VB compiler subtree) remains a future scale lane for a
  second, larger VB smoke; not required by this spec.

## PR #742 reconciliation (2026-09-11)

- Merged the current `origin/dev` into the feature branch without dropping the
  completed VB adapter work.
- Added compiler-resolved event declarations while continuing to exclude event
  wiring and event-flow claims.
- Added field, parameter, and event declarations to the bounded per-file syntax
  fallback and populated property contract elements.
- Unresolved invocation and constructor sites in otherwise semantic files now
  retain bounded Tier3 call-site evidence; they are never emitted as Tier1
  call edges. Unsupported invocation shapes use a categorical label plus a
  deterministic hash and never retain raw expression text.
- Tier1 argument-to-parameter facts now require Roslyn's
  `IArgumentOperation.Parameter`; name/ordinal guesses are not promoted.
- Equals-value object initializers retain their assigned variable names, and an
  exact `AssemblyInfo.vb` filename is excluded by the generated-document
  boundary.
- Reconciliation validation: full solution tests 1883/1883; focused
  `VisualBasic|VbNetFixture` tests 35/35; modern/legacy/Web Forms scans contain
  221/177/158 total facts and all pass the adapter artifact validator; a second
  modern scan is byte-identical; private-path and diff guards pass. The legacy
  scan retains 24 Tier1 facts plus 14 bounded call-site Tier3 facts, and the Web
  Forms scan retains 19 Tier1 facts plus 8 bounded call-site Tier3 facts.
- Exact-HEAD review follow-up removed optional parameter default literals from
  both persisted VB display strings and canonical symbol identities, kept VB
  operator declarations outside the documented declaration scope, and made
  capability diagnostics language-honest: VB-only scans no longer emit the
  C# semantic-compilation capability while VB syntax fallback contributes to
  the language-neutral syntax capability.
- Restore remains enabled for VB-only scans, but a mixed C#/VB scan performs
  the requested restore during the C# workspace pass and reuses that restored
  state for the VB workspace pass instead of restoring the solution twice.
- Follow-up validation: full solution tests 1886/1886 and focused
  `FullyQualifiedName~VisualBasic` tests 36/36; privacy, VB capability,
  syntax-fallback, and operator-boundary regressions are included.
- Second exact-HEAD follow-up makes every unresolved invocation or known-type
  constructor fallback coverage-reducing with a bounded Tier4 call-site gap,
  normalizes constructed generic method identities to their original
  definitions, and keeps C# capability status isolated from VB failures in
  mixed-language scans. The public adapter guide and catalog now agree that
  event declarations are emitted while event wiring is not.
- Final follow-up validation: full solution tests 1889/1889 and focused
  `VisualBasic|AnalyzerCapabilityDiagnostic` tests 48/48; generic identity,
  late-binding coverage, and mixed-language capability regressions are
  included. Extractor identity is `vb-semantic/0.4.0` for these evidence
  semantics. Final modern/legacy/Web Forms fixture scans contain 221/185/163
  facts, all pass the adapter artifact validator, and a repeated modern scan
  is byte-identical.
