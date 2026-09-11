# VB.NET Adapter Foundation Implementation State

- Status: extraction-slice-implemented (spec tasks 1-7 complete; 8-11 open)
- Branch: `codex/issue-736-vbnet-adapter-foundation`
- Slice start: `673cf6f19af4b470f4a031479317fed3947d26b9` (foundation slice,
  tasks 1-4)
- Slice end: see "Validation recorded" below for the exact pushed SHA.
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

## Implemented in this slice (tasks 5-7)

- **Task 5 - compiler-backed semantic facts.** `VisualBasicSemanticExtractor`
  (bumped to `vb-semantic/0.2.0`) now walks each repo-local, inventoried,
  non-generated VB document of every compiled project and emits Tier1 facts
  under new rules: `vb.semantic.declarations.v1` (TypeDeclared for
  class/module/structure/interface/enum/delegate, MethodDeclared incl.
  constructors, PropertyDeclared, FieldDeclared, ParameterDeclared),
  `vb.semantic.propertyaccess.v1` (PropertyAccessed incl. unambiguous
  default-member indexing such as `catalog(0)`),
  `vb.semantic.methodinvocation.v1` (MethodInvoked),
  `vb.semantic.callgraph.v1` (CallEdge, `SemanticMethodInvocation` and
  `SemanticObjectCreation` kinds),
  `vb.semantic.objectcreation.v1` (ObjectCreated),
  `vb.semantic.valueflow.v1` (ArgumentPassed with compiler-resolved parameter
  binding via `IArgumentOperation`, covering named, ByRef, optional-bound,
  and params-expanded arguments), and
  `vb.semantic.symbolrelationship.v1` (SymbolRelationship kinds
  `InheritsFrom`, `ExtendsInterface`, `ImplementsInterface`,
  `ImplementsInterfaceMember`, `Overrides`). Every fact carries rule ID,
  tier, repo-relative path, one-based line span, commit SHA, project path,
  and extractor identity/version.
- **Symbol identity.** New `VisualBasicSymbolIdentityProvider` mirrors the
  canonical .NET normalization with `visualbasic` language tags, so VB-scan
  symbol rows are distinguishable from C# rows while keeping the same ID
  shape. Cross-language symbol-identity joins (VB scan symbols to C#-declared
  symbols) are NOT established in this slice; display strings, assembly
  names, and fact-level evidence still cross the language boundary. Both
  rules' catalog limitations document this.
- **Deliberate exclusions (documented in catalog limitations).** Event
  declarations, `Handles`/`AddHandler`/`RemoveHandler`/`RaiseEvent`/ and
  `WithEvents` wiring are never promoted to resolved event edges (#738 owns
  event composition; tests assert none appear). Operator/`Declare`
  (P/Invoke) statements, event declarations, and LocalAlias/FieldAlias
  families are not emitted in this slice. Designer/generated documents are
  not analyzed for semantic facts. Unresolved or late-bound call sites
  produce no fact (visible only through compiler-diagnostic gaps), never a
  guessed target.
- **Task 6 - bounded per-file syntax fallback.** New
  `VisualBasicSyntaxExtractor` (`vb-syntax/0.1.0`) runs inside the shared
  SyntaxFallback stage over inventoried VB files that received NO
  compiler-resolved coverage (`SemanticAnalysisUnavailable` Tier4 gap per
  file under `vb.semantic.workspace.v1`). It emits Tier3 facts under
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
  with no new schema. `ProjectFileReader` now reads target frameworks and
  package references for `VisualBasicProject` inventory items
  (`manifestKind=vbproj`); `PackageReferenced` facts set `projectPath` for
  `.vbproj`. Project *references* are not read by `ProjectFileReader` for
  either language (MSBuild workspace loading and binlog evidence carry
  them); VB is at C# parity. Ordering stays deterministic through
  `SemanticExtractionResultMerge` and the existing final fact sort.

## Key decisions and oddities

- **Fallback granularity is per-file.** Syntax fallback fires only for files
  with zero semantic coverage (failed load, orphan files). A file with
  partial binding keeps its Tier1 facts and compiler-diagnostic gaps; adding
  Tier3 duplicates would blur evidence strength. This differs from C#, whose
  syntax extractor runs unconditionally; the difference is intentional and
  requirement-driven (fallback "when loading/compilation is incomplete").
- **Broken-vbproj behavior.** An unparseable `.vbproj` does not always throw
  from `OpenProjectAsync`; MSBuildWorkspace may surface it as a workspace
  diagnostic plus a compilation diagnostic at the project path. The scan
  stays `FailedOrPartial`/`...Reduced` with sanitized gaps either way, and
  the fallback still covers readable VB files. The focused test asserts the
  reduced posture rather than one specific gapKind for this reason.
- **Sanitized no-project gap.** A repo with VB files but no VB project emits
  the `NoVisualBasicProjectOrSolution` gap whose manifest message is the
  sanitized category (e.g. `UncategorizedWorkspaceFailure`); the gap kind
  remains visible on the fact. This mirrors C# symmetric behavior and was
  left unchanged.
- **My-template exclusion preserved.** The bounded
  no-source-location/My-template suppression and
  `injectedTemplateErrorCount` reporting are unchanged; source-referencing
  compiler errors still reduce coverage (legacy/webforms fixtures remain
  reduced with partial Tier1 evidence).
- **C# behavior preserved.** No changes to C# extraction, tiering, or fact
  families. The only shared-code changes: `ProjectFileReader` kind filters
  (additive), `PackageReferenced` projectPath `.vbproj` acceptance
  (additive), and the new VB syntax-fallback call in `ScanEngine`. The C#
  no-regression test over `samples/modern-sample` still passes, and the full
  suite is green.
- **MSBuildRegistrationFailed watch.** No recurrence during this slice
  (dozens of scans + full suite). The single historical non-reproducing
  occurrence from the foundation slice remains documented here; nothing is
  suppressed.
- One intentional asymmetry: `ReadTargetFrameworks` reads SDK-style
  `TargetFramework`/`TargetFrameworks` elements only; legacy
  `TargetFrameworkVersion` (vb-webforms/vb-legacy) is covered by the build
  environment diagnostics, same as legacy C# projects.

## Tests added (this slice)

`src/dotnet/tests/TraceMap.Tests/VisualBasicExtractionTests.cs` (14 tests):
compiler-resolved declarations; overload-resolved call edges (Decimal vs
Integer `Sum`); interface/inheritance/override relationships; default-member
property access; argument flow for named/ByRef/params arguments; event-wiring
non-promotion over modern+webforms fixtures; orphan-file syntax fallback with
explicit gaps; failed-project-load fallback with reduced labels; no Tier3
duplicates for semantically analyzed files; shared SQLite join coverage
(call_edges/object_creations/argument_flows/symbol_relationships/
fact_symbols/symbol_occurrences/parameter_forward_edges, language tagging);
`.vbproj` target framework + package reference facts; mixed-solution
single-ownership without duplication; fact-level determinism; and privacy
(no absolute paths, no literal values; sentinel-based).

## Validation recorded (2026-09-11, macOS arm64, .NET SDK 10.0.302)

- `dotnet build src/dotnet/TraceMap.sln` - clean.
- Focused: `VisualBasicExtractionTests` + `VisualBasicFoundationTests` +
  `VbNetFixtureTests` - 26/26 passed.
- Full `dotnet test src/dotnet/TraceMap.sln` - 1858/1858 passed.
- CLI scans (final code):
  - `samples/vb-modern-sample` -> `Level1SemanticAnalysis` / `Succeeded`,
    220 facts, all Tier1 VB families present (31 call edges, 31 argument
    flows, 6 object creations, 6 symbol relationships, 119 `visualbasic`
    symbols), 9 parameter-forward edges.
  - `samples/vb-legacy-sample` -> `Level1SemanticAnalysisReduced` /
    `FailedOrPartial`, 140 facts: sanitized compiler/workspace gaps plus 24
    partial Tier1 declaration/access facts (vendor assembly missing).
  - `samples/vb-webforms-sample` -> `Level1SemanticAnalysisReduced` /
    `FailedOrPartial`, 121 facts: page-lifecycle handler declarations,
    code-behind object creations, and status-notifier call evidence where
    the compiler resolves them; event wiring never becomes edges.
- `python3 scripts/validate-adapter-artifacts.py` passes on all three scan
  outputs (also enforces rule-ID registration against `rules/rule-catalog.yml`).
- Determinism: two consecutive modern-fixture scans produce byte-identical
  `facts.ndjson` and identical fact signatures in-process; source snapshot
  digests match.
- Privacy: no private absolute paths and no raw diagnostic message text in
  any of the three outputs; literal values are never stored (sentinel test).
- `git diff --check` clean; `scripts/check-private-paths.sh` passes.
- MSBuildRegistrationFailed: 0 occurrences in all validation scans.

## Remaining work

- Task 8: full synthetic test matrix extension (generated-source,
  snapshot-mutation during a live scan, expanded public-safety) on top of
  tasks 5-7.
- Task 9: pin and validate an open-source VB smoke repository (candidates
  pre-researched in `docs/VBNET_FIXTURES.md`; re-verify pins).
- Task 10: rule catalog/docs/acceptance updates beyond the catalog entries
  already added here (adapter docs, acceptance guidance,
  `docs/VALIDATION.md` exact commands).
- Task 11: final validation pass and PR to `dev`.
