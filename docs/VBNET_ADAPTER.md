# VB.NET Adapter

Status: foundation, bounded event/Web Forms composition, compiler-backed
ADO.NET plus HTTP/config/file boundary slices, and focused review/export parity (issues
[#736](https://github.com/joefeser/tracemap/issues/736) and
[#738](https://github.com/joefeser/tracemap/issues/738),
[#737](https://github.com/joefeser/tracemap/issues/737), and
[#750](https://github.com/joefeser/tracemap/issues/750)). Every claim below is bounded by
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
- Emits shared `SqlCommandDetected` evidence for compiler-resolved `DbCommand`
  construction, `CommandType` assignment, and parameter collection mutation,
  plus shared `DatabaseOperationCandidate` evidence for `ExecuteReader`,
  `ExecuteScalar`, `ExecuteNonQuery`, and `DbDataAdapter.Fill` calls. Existing
  call-edge and object-creation facts retain the supporting method path and
  DataSet/DataTable/reader types.
- Emits shared `HttpCallDetected` evidence for compiler-resolved `HttpClient`,
  `WebRequest`, and `WebClient` operations. Compile-time destinations retain a
  normalized path plus a digest, never a host or raw URL; dynamic destinations
  remain explicit gaps. `WebRequest.Create` construction remains visible even
  when a later `GetResponse` cannot be correlated to a destination.
- Emits shared `ConfigBinding` evidence for compiler-resolved
  `ConfigurationManager.GetSection`, `AppSettings(...)`,
  `ConnectionStrings(...)`, and `My.Settings` member access. Only key/member
  digests are retained; raw keys and values are excluded.
- Feeds compiler-resolved VB `System.IO.File`/`Directory` calls into the shared
  batch/data-movement projection. No VB-only file contract is introduced.
- Carries retained `.vb` spans through the existing focused Web Forms review
  and optional annotated-source output. Bounded unique-name navigation uses
  Visual Basic syntax and case-insensitive identifier matching, but remains a
  labeled navigation candidate rather than evidence. Anonymous reports retain
  safe `vb.semantic.*` rule provenance while withholding source, paths,
  symbols, fact IDs, configuration, SQL, URLs, and commit identity.

## Extractor identities

| Extractor | Identity/version | Tier | Rules |
| --- | --- | --- | --- |
| Visual Basic semantic extractor | `vb-semantic/0.8.3` | Tier1 (facts), Tier2 (project observation), Tier4 (workspace, call-site, event, and data/external-boundary gaps) | `vb.semantic.compilation.v1`, `vb.semantic.workspace.v1`, `vb.semantic.declarations.v1`, `vb.semantic.propertyaccess.v1`, `vb.semantic.methodinvocation.v1`, `vb.semantic.callgraph.v1`, `vb.semantic.objectcreation.v1`, `vb.semantic.valueflow.v1`, `vb.semantic.symbolrelationship.v1`, `vb.semantic.event-wiring.v1`, `vb.semantic.config-binding.v1`, `vb.semantic.external-boundary.v1`, plus shared database, HTTP, WCF, and ASMX contracts |
| Visual Basic syntax fallback | `vb-syntax/0.3.20` | Tier3 (facts), Tier4 (parse/read/budget/semantic-unavailable/event/phase-failure gaps) | `vb.syntax.declarations.v1`, `vb.syntax.memberaccess.v1`, `vb.syntax.invocation.v1`, `vb.syntax.callgraph.v1`, `vb.syntax.objectcreation.v1`, `vb.syntax.event-wiring.v1`, `vb.syntax.database-operation.v1`; syntax declarations retain lexical namespace-qualified containing types without duplicating the declared type segment, text-only parameter types, field types, base-type names, method arity, and a text-only member identity, while the source index remains the explicit assembly/projectless scope identity. Syntax calls retain one explicit caller parameter/local/field receiver type when the declaration is unique and in scope, or the exact type text of a direct inline `New Type().Method()` receiver. Projectless receiver bridges may use a simple type name only to discover candidates; the final join requires one exact source-index, qualified-type, member, and retained-signature identity, and ambiguous same-arity overloads fail closed. Positional invocation arguments retain text-only types when an identifier has one explicit caller parameter/local type or the expression is an explicit object creation. After receiver identity is uniquely established, complete argument types may eliminate exact signature mismatches; partial types may do so only when every unresolved position has the same retained parameter type across all candidates. Named, wholly unknown, conflicting, or signature-incomplete evidence does not authorize filtering. Syntax call edges carry the explicit `syntax-only` coverage label, primary calls and receiver creations use deterministic round-robin admission across containing members before auxiliary invocation-name witnesses under the bounded per-file budget so one noisy method cannot starve field/local initializers or later method bodies, and explicit supported ADO.NET data-adapter `Fill` or database-command execution receivers produce reduced syntax-only database-operation candidates without retaining SQL text |
| Shared Web Forms extractor | `legacy-webforms/0.13.4` | Tier1/Tier2/Tier3 facts and Tier4 gaps | existing `legacy.webforms.*` contracts, now with bounded VB code-behind/designer parsing, projectless VB syntax-call ownership normalized by exact parameter-count-suffixed handler identity and containing span, inline client-behavior candidates joined to unique retained VB `Handles`/`AddHandler` bindings, explicit selector target cardinality, literal jQuery AJAX candidates joined through unique `.ashx` directives to external or single-file inline `ProcessRequest` declarations and retained downstream paths, projectless VB server navigation/request-lifecycle/control-state candidates, unique inline server-expression joins to repository and `App_Code` declarations, shared database/WCF/ASMX terminal projection, and safe control prefix/type/registration-state metadata on unresolved registration gaps |
| Shared WCF extractor | `legacy-wcf/0.3.1` | Tier1 inputs, Tier2/Tier3 mappings, Tier4 gaps | existing `legacy.wcf.*` contracts with compiler-resolved VB client/contract inputs and compiler-resolved C# call-target identity for generated proxy operations |
| Shared ASMX extractor | `legacy-asmx/0.2.0` | Tier1 inputs, Tier3 mappings, Tier4 gaps | existing `legacy.asmx.*` contracts with compiler-resolved VB client/service inputs |
| Shared batch/data-movement extractor | `legacy-batch-data-movement/0.2.0` | Tier1/Tier2/Tier3 facts and Tier4 gaps | `legacy.webforms.batch-data-movement.v1`, including VB `Global.System.IO.*` semantic display strings |

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
`Overrides`). The initial ADO.NET slice adds compiler-resolved
`SqlCommandDetected` facts for command construction/configuration and
`DatabaseOperationCandidate` facts for supported command/adapter methods.
Receiver symbol IDs keep two commands in one method distinct. Constant command
strings retain only a SHA-256 digest and length; raw SQL, procedure names,
parameter names and values, and connection material are not retained.
`StoredProcedure` is a safe categorical candidate derived from a resolved
`CommandType` enum assignment.

The external slice reuses `HttpCallDetected`, `HttpClientCreated`, and
`ConfigBinding`. Constant HTTP destinations retain normalized path shapes and
hashes only. Configuration keys and members retain hashes only. Compiler-
resolved `System.IO` invocations are consumed by the shared
`LegacyBatchDataMovementDeclared` projection. These shared facts flow through
Web Forms call paths, modernization packets, evidence-doc query recipes,
source/review inputs, and WITS-compatible handoff corpora without a
language-specific downstream schema.

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
message hash only). Within that shared budget, declaration and event evidence
is retained first, followed by invocation and object-creation evidence; the
potentially high-volume member-access projection uses the remaining budget.

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
  Compiler-recognized late-bound invocations are producer-local uncertainty
  and do not by themselves downgrade a successful semantic build.
- A name-only late-bound or unresolved `Fill`/`Execute*` call emits a bounded
  `VisualBasicAdoNetTargetUnavailable` Tier4 gap. A syntactically plausible
  unresolved `*Command` construction emits a bounded
  `VisualBasicAdoNetCommandTypeUnavailable` gap. Neither becomes positive
  database evidence. Execute/Fill candidates must align with a supported
  framework member signature; subclass-defined same-name helper overloads are
  excluded. Helper-returned commands, reflection, provider-specific APIs
  outside the `DbCommand`/`DbDataAdapter` base families, cross-method command
  state, and runtime ordering remain outside this initial slice.
- ConfigurationManager, WebRequest, and WebClient recognition accepts both
  their package-era assembly names and their classic .NET Framework assembly
  names while retaining exact compiler-resolved metadata type and expected
  strong-name public-key-token checks. ADO.NET base types are bounded the same
  way to System.Data.Common or classic System.Data; unsigned same-name types
  never become positive boundary evidence.
- WCF `ClientBase(Of T)` maps only when the compiler proves the client contract
  and the client method's exact interface implementation, including renamed
  implementations and inherited contract interfaces when unambiguous. ASMX
  service operations require a recognized `WebService` service context; client
  operations require recognized `System.Web.Services` inheritance and SOAP
  method attributes. Framework assembly names must also
  carry the expected strong-name public-key token; unsigned same-name types are
  rejected. The public-key token is an identity discriminator, not proof of
  assembly authenticity. Custom wrappers, missing attributes/interfaces,
  ambiguous matches, and absent metadata remain Tier4 gaps. HTTP destinations that are dynamic, including a later
  `WebRequest.GetResponse` whose construction receiver is not correlated,
  likewise remain gaps while their call/construction evidence is retained.
- No runtime claims of any kind: facts prove source structure and
  compiler-resolved binding at scan time only, never execution,
  reachability, deployment state, or impact.
- Focused source review reads the supplied working tree and does not prove that
  it matches the scanned commit. Git is optional. Raw source remains opt-in,
  private, and excluded from anonymous artifacts; unique-name definition
  candidates are navigation aids only.

## Validation

Exact local commands, fixture scans, determinism, privacy, and the pinned OSS
smoke procedure are in [`VALIDATION.md`](VALIDATION.md) under the VB.NET
adapter section. The fixture corpus and the pinned smoke repository are
documented in [`VBNET_FIXTURES.md`](VBNET_FIXTURES.md).
