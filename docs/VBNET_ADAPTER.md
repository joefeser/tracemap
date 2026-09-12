# VB.NET Adapter

Status: foundation plus bounded event/Web Forms composition (issues
[#736](https://github.com/joefeser/tracemap/issues/736) and
[#738](https://github.com/joefeser/tracemap/issues/738)). Every claim below is bounded by
the cataloged rule limitations in `rules/rule-catalog.yml` (`vb.semantic.*`,
`vb.syntax.*`). Nothing in this adapter proves runtime reachability, execution,
build success, or impact.

## What the adapter does

- Inventories `.vb` sources and `.vbproj`/`.sln` inputs. Inventory classifies
  `VisualBasic`, `VisualBasicCodeBehind`, `VisualBasicDesigner`,
  `VisualBasicGenerated`, `VisualBasicAssemblyInfo`, and `VisualBasicProject`
  kinds. Filename and header conventions are structural classification only,
  never semantic proof.
- Loads supported `.vbproj` inputs through MSBuildWorkspace and the pinned
  Roslyn Visual Basic compiler when the projects and their references resolve;
  emits compiler-resolved `Tier1Semantic` facts.
- Falls back to bounded per-file Visual Basic syntax parsing
  (`Tier3SyntaxOrTextual`) for inventoried `.vb` files that received no
  compiler-resolved coverage, with explicit `Tier4Unknown` gaps and a reduced
  coverage label.
- Joins VB evidence into the shared SQLite tables (`symbols`,
  `fact_symbols`, `symbol_occurrences`, `call_edges`, `object_creations`,
  `argument_flows`, `symbol_relationships`, `parameter_forward_edges`) with no
  VB-specific schema.
- Protects VB sources, projects, and checked-in generated inputs with the
  semantic-input snapshot; mutation inside the scan window fails the scan with
  the typed `SourceSnapshotException`.
- Retains compiler-resolved and bounded syntax fallback evidence for
  `Handles`, `AddHandler`, `RemoveHandler`, `RaiseEvent`, and `WithEvents`, and
  projects supported VB code-behind/control/lifecycle bindings into the shared
  Web Forms evidence contracts.

## Extractor identities

| Extractor | Identity/version | Tier | Rules |
| --- | --- | --- | --- |
| Visual Basic semantic extractor | `vb-semantic/0.5.1` | Tier1 (facts), Tier2 (project observation), Tier4 (workspace, call-site, and event gaps) | `vb.semantic.compilation.v1`, `vb.semantic.workspace.v1`, `vb.semantic.declarations.v1`, `vb.semantic.propertyaccess.v1`, `vb.semantic.methodinvocation.v1`, `vb.semantic.callgraph.v1`, `vb.semantic.objectcreation.v1`, `vb.semantic.valueflow.v1`, `vb.semantic.symbolrelationship.v1`, `vb.semantic.event-wiring.v1` |
| Visual Basic syntax fallback | `vb-syntax/0.3.0` | Tier3 (facts), Tier4 (parse/read/budget/semantic-unavailable/event gaps) | `vb.syntax.declarations.v1`, `vb.syntax.memberaccess.v1`, `vb.syntax.invocation.v1`, `vb.syntax.callgraph.v1`, `vb.syntax.objectcreation.v1`, `vb.syntax.event-wiring.v1` |
| Shared Web Forms extractor | `legacy-webforms/0.8.1` | Tier1/Tier2/Tier3 facts and Tier4 gaps | existing `legacy.webforms.*` contracts, now with bounded VB code-behind and designer parsing |

Symbol identities use the canonical .NET normalization shape with
`visualbasic`-tagged language values (see `VisualBasicSymbolIdentityProvider`).
Symbols resolved by a VB scan are distinguishable from C# rows by language.

## Fact families

Compiler-resolved (Tier1): `TypeDeclared` (class/module/structure/interface/
enum/delegate), `MethodDeclared` (including constructors), `PropertyDeclared`,
`FieldDeclared`, `ParameterDeclared`, `PropertyAccessed` (including
unambiguous default-member indexing such as `catalog(0)`), `MethodInvoked`,
`CallEdge` (`SemanticMethodInvocation` and `SemanticObjectCreation` kinds),
`ObjectCreated`, `ArgumentPassed` (compiler-resolved parameter binding via
`IArgumentOperation`, covering positional, named, ByRef, optional-bound, and
params-expanded arguments), and `SymbolRelationship` (`InheritsFrom`,
`ExtendsInterface`, `ImplementsInterface`, `ImplementsInterfaceMember`,
`Overrides`).

Event composition adds `VisualBasicEventBindingDeclared` for compiler-resolved
or syntax-only `Handles`, `AddHandler`, and `RemoveHandler` sites and
`VisualBasicEventRaised` for `RaiseEvent`. `isAttach=False` distinguishes
detach evidence. These facts are event relationships, not executed call edges.
Supported linked Web Forms surfaces additionally receive the existing
`WebFormsEventBindingDeclared`, `WebFormsHandlerResolved`, lifecycle/postback,
flow, report, packet, review, handoff, and anonymous projection behavior.

Project observation (Tier2): `VisualBasicProjectObserved` records load and
compilation only, with aggregate document and error-diagnostic counts.

Gaps (Tier4): sanitized workspace/compilation/restore gaps under
`vb.semantic.workspace.v1` (category-only messages with bounded `CSxxxxx`,
`MSBxxxx`, or `BCxxxxx` diagnostic ids), plus per-file fallback gaps under the
syntax rules (`SemanticAnalysisUnavailable`, `SyntaxParseDiagnostic`,
`SyntaxFileReadFailed`, `SyntaxFallbackBudgetExhausted`).

Syntax fallback (Tier3): `TypeDeclared`, `MethodDeclared`, `PropertyDeclared`,
`EnumDeclared`, `MemberAccessName`, `InvocationName`, `CallEdge`
(`SyntaxInvocation`/`SyntaxObjectCreation`), and `ObjectCreated` — text-only
candidates that are never compiler-resolved targets and never join to semantic
symbols by identity. Emission is bounded by a deterministic per-file budget
(2,000 facts) and bounded parse-diagnostic gaps (20 per file, diagnostic id and
message hash only).

## Fallback boundary

The full-file fallback granularity is per file, not per scan. Files with zero
semantic coverage (failed load, orphan files outside any project) are parsed
by the syntax fallback, and each emits one explicit
`SemanticAnalysisUnavailable` gap. A semantically analyzed file does not
receive duplicate full-file fallback output, but an individual unresolved
invocation or known-type constructor site may retain bounded Tier3 invocation,
call-edge, or object-creation evidence plus a Tier4 call-site gap. Designer,
generated, and auto-generated-
header files are skipped by the fallback; designer and generated
(filename-convention) documents are also excluded from semantic facts, while
ordinary-named files with an `<auto-generated>` header keep compiler-resolved
analysis exactly like their C# counterparts.

## Supported project types

- SDK-style `.vbproj` (including `net10.0`, `netstandard2.0`, and other
  SDK-resolvable targets): full semantic path when references resolve.
- Solutions (`.sln`) containing VB projects: VB projects are extracted by the
  VB adapter; C# projects inside the same solution stay with the C# adapter
  (one language owner per project).
- Legacy non-SDK `.vbproj` (old `ToolsVersion`, .NET Framework targets,
  Web Application projects): these load only when the local MSBuild can
  evaluate their targets and reference assemblies. When they cannot — the
  normal case on cross-platform SDKs — the scan stays useful but reduced:
  sanitized workspace/compilation gaps plus partial Tier1 evidence over the
  readable files, with the reduced coverage label. This mirrors C# legacy
  behavior; no legacy-specific VB targets are added.
- Legacy `TargetFrameworkVersion` values are reported by the build-environment
  diagnostics lane, same as legacy C# projects.

## Important boundaries

These limits are deliberate scope boundaries for the foundation slice, and the
catalog documents them per rule:

- Cross-language symbol-identity joins are NOT established. VB-scan symbols
  carry `visualbasic`-tagged identities; they do not join by identity to
  C#-declared symbols even when both compile into the same solution. Display
  strings, assembly names, and fact-level evidence do cross the language
  boundary; identity joins are future work.
- VB event wiring is static evidence only. Compiler-resolved language facts do
  not prove attachment lifetime, event firing, delegate invocation, ordering,
  or execution. Shared Web Forms projection requires a linked markup surface,
  one supported receiver/control or lifecycle event, and one handler method;
  ambiguous partials, late-bound receivers, unsupported delegates, missing
  designer/framework metadata, and unlinked code remain gaps.
- VB identifiers used by shared Web Forms joins follow VB's ordinal
  case-insensitive semantics; C# joins remain case-sensitive. Unqualified
  `IsPostBack` is rejected when a VB local, parameter, or containing-type member
  shadows the name, while explicit `Me.IsPostBack`/`MyBase.IsPostBack` remains
  bounded syntax evidence.
- Event declarations are emitted. Operator statements and `Declare` (P/Invoke)
  statements are not emitted as declarations in this slice.
- Late-bound invocations, unresolved or ambiguous default members, overload
  resolution failures, reflection, and conditional-compilation branches that
  did not compile never produce a guessed Tier1 target. Unresolved invocation
  and constructor sites retain bounded Tier3 call-site evidence plus a Tier4
  gap; other unsupported shapes remain visible through compiler diagnostics.
- No runtime claims of any kind: facts prove source structure and
  compiler-resolved binding at scan time only, never execution,
  reachability, deployment state, or impact.

## Validation

Exact local commands, fixture scans, determinism, privacy, and the pinned OSS
smoke procedure are in [`VALIDATION.md`](VALIDATION.md) under the VB.NET
adapter section. The fixture corpus and the pinned smoke repository are
documented in [`VBNET_FIXTURES.md`](VBNET_FIXTURES.md).
