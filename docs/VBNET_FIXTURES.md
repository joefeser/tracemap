# VB.NET Fixture Corpus

Scope: fixture and smoke-corpus preparation for
[#736](https://github.com/joefeser/tracemap/issues/736) (VB.NET adapter
foundation) and [#738](https://github.com/joefeser/tracemap/issues/738)
(Web Forms event composition). This document intentionally makes no claims
about extractor behavior: the fixtures define what the future adapter should
be able to observe, not what it proves today. Spec:
`.kiro/specs/vbnet-adapter-foundation/`.

All fixture content is synthetic. No proprietary names, paths, SQL,
configuration, or business logic are used.

## Fixture inventory

| Fixture | Shape | Cross-platform build | Intended coverage when scanned by the future VB adapter |
| --- | --- | --- | --- |
| `samples/vb-modern-sample` | SDK-style `.vbproj`, net10.0, `Option Strict On` | Builds with the .NET SDK (verified on macOS arm64, SDK 10.0.302) | Full semantic path (`Tier1Semantic`), no reduced-coverage label expected from the VB side |
| `samples/vb-legacy-sample` | Old-style `ToolsVersion=3.5` `.vbproj`, `Option Strict Off`, `My Project` folder | Does not build with the modern SDK (expected MSB3644: .NET Framework 3.5 reference assemblies unavailable) | Project load/compile failure path: per-file syntax fallback (`Tier3SyntaxOrTextual`) plus explicit `Tier4Unknown` gaps and a reduced-coverage label |
| `samples/vb-webforms-sample` | Old-style .NET Framework 4.8 Web Application project with `.aspx`, code-behind, and designer | Does not build with the modern SDK (expected MSB3644: net48 reference assemblies; Web Application targets) | Inventory/classification corpus for #738. #736 makes no claim about event relationships in this fixture |

## samples/vb-modern-sample (semantic success fixture)

Files: `VbModernSample.vbproj`, `Contracts.vb`, `Domain.vb`, `Services.vb`,
`CallSites.vb`.

Covered constructs, chosen to match the spec's "first supported evidence"
list:

- Namespaces (`Contracts`, `Domain`, `Services`, `CallSites`) and a project
  `RootNamespace` qualifier.
- Interfaces (`IOrderRepository`, `IPriceFormatter`) with `Implements` on a
  concrete repository; `MustInherit` base class (`EntityBase`) with
  `Overridable`/`Overrides` methods.
- Delegates (custom `PriceFormatter` plus `EventHandler(Of T)` pattern),
  fields (constant, read-only, initialized), constructors (chained `Me.New`),
  methods, auto-properties and full accessors.
- Events declared with `Event ... As EventHandler(Of T)`, raised via
  `RaiseEvent`. Event-edge composition is out of scope for #736 (see #738);
  the fixture only proves the constructs exist and must not become silently
  promoted resolved event edges.
- Direct calls and object creation, including interface dispatch
  (`IOrderRepository.Save`/`FindById`), BCL calls, and delegate invocation.
- `ByRef` (`TryNormalizeSku`), `Optional` (`BuildLabel`), `ParamArray`
  (`JoinTags`), `Overloads` pair (`Sum(Integer,Integer)` /
  `Sum(Decimal,Decimal)`), and parameterized default properties
  (`InventoryBatch.Item`, `PriceCatalog.Quote`) exercised through default
  member invocation (`catalog(0)`).
- Argument-to-parameter material: positional, named (`suffix:=...`),
  ParamArray expansion, ByRef forwarding, and overload-selected calls.

Expected evidence categories once the adapter lands: namespace/type/delegate/
field/property/event/constructor/method/parameter declarations, symbol
occurrences and member access, invocations with resolved targets, object
creation rows, argument/parameter relationships, and direct
containment/inheritance/implementation/override relationships, all
`Tier1Semantic` with VB rule IDs.

The `catalog(0)` default-member call is expected to retain `Tier1Semantic`
evidence only when Roslyn resolves `PriceCatalog.Quote` unambiguously. An
unresolved or ambiguous default member must instead produce an explicit
limitation or gap. Expected gaps for this buildable fixture are otherwise none
from the VB side beyond ordinary absence claims.

## samples/vb-legacy-sample (syntax fallback fixture)

Files: `VbLegacyCatalog.vbproj`, `CatalogModels.vb`, `CatalogService.vb`,
`LegacyQueueBridge.vb`, `My Project/AssemblyInfo.vb`,
`My Project/Application.Designer.vb`, `My Project/Application.myapp`,
`My Project/Resources.Designer.vb`, `My Project/Resources.resx`.

Legacy shapes exercised:

- Old-style non-SDK `.vbproj` (`ToolsVersion=3.5`, .NET Framework v3.5,
  project-level `<Import>` namespace aliases, `<Reference>` with
  `RequiredTargetFramework`, `HintPath` to a vendor DLL that is deliberately
  not shipped).
- Windows Forms application-framework wiring: `OutputType=WinExe` with
  `MyType=WindowsForms` and `EnableApplicationFramework=true`, so the VB 3.5
  toolset supplies the application-framework entry point from
  `Application.Designer.vb` plus `Application.myapp` instead of a checked-in
  `Sub Main` or `StartupObject`. `CatalogHostForm` inherits
  `System.Windows.Forms.Form` so the main-form assignment stays valid on
  toolsets that get past the missing reference assemblies.
- VB 9 (vbc 9.0) syntax compatibility: all readable `.vb` files use explicit
  `_` line continuations and avoid VB 10+ syntax, so no unrelated syntax
  errors are injected on top of the intentional failure below. Enforced by
  the committed `Legacy_vb_fixture_files_parse_as_visual_basic_9` test.
- Assembly-info file under `My Project/`.
- Generated/designer pair (`Application.Designer.vb`,
  `Resources.Designer.vb`) plus generator metadata (`AutoGen`, `DesignTime`,
  `DependentUpon`). Filename conventions must classify inventory but never act
  as semantic proof.
- Conditional compilation: `#Const LegacyGatewayEnabled = True`, `#If` /
  `#Else` / `#End If`, and a `#If DEBUG` region. Both `#If` branches are kept
  syntactically valid.
- One intentionally unresolved reference:
  `LegacyVendor.Connectors.Gateway` (created and invoked in
  `CatalogService.vb`; the assembly only exists as a `HintPath`).
- Late binding: `LegacyQueueBridge.vb` runs with `Option Strict Off` and calls
  members on `Object` returned by `CreateObject`, which cannot have
  compiler-resolved targets.

Expected evidence categories once the adapter lands: readable `.vb` files
continue to be scanned via VB syntax fallback with
`Tier3SyntaxOrTextual` declaration/call candidates; the failed project
load/compile path emits `Tier4Unknown` gaps for the unresolved vendor
reference, the late-bound calls, and the conditional-compilation surfaces, and
the scan is labeled reduced coverage. Syntax-only callee text must not be
represented as a compiler-resolved target.

## samples/vb-webforms-sample (future #738 corpus)

Files: `VbWebFormsSample.vbproj`, `Default.aspx`, `Default.aspx.vb`,
`Default.aspx.designer.vb`, `StatusNotifier.vb`, `Web.config`.

This fixture exists so #738 can plan against a stable corpus. It is
deliberately minimal and makes no claim that #736 supports Web Forms control
or event composition:

- `.aspx` page with server controls (`Label`, `TextBox`, two `Button`s) and
  `@ Page` directive binding to `VbWebFormsSample._Default`.
- Code-behind with page lifecycle methods (`Page_Init`, `Page_Load`,
  `Page_PreRender`, `Page_Unload` wired via `Handles Me.*` with
  `AutoEventWireup="false"`) and control-event handling.
- Event relationships in all five forms reserved for #738: `Handles` on a
  designer control event (`SaveButton.Click`), `AddHandler` /
  `RemoveHandler` with `AddressOf` (dynamic refresh wiring), `WithEvents`
  fields (designer controls plus a custom notifier), and `RaiseEvent` in
  `StatusNotifier`. Until #738, these must not be emitted as resolved event
  edges; at most the foundation adapter may record explicit limitations or
  gaps.
- A generated-shape designer file (`Default.aspx.designer.vb`) declaring
  `Protected WithEvents` control fields, for inventory classification.

Expected evidence categories once #736 lands: inventory classification
(ordinary, code-behind, designer), syntax-level declaration candidates over
the `.vb` files, and `Tier4Unknown` gaps for event-relationship composition
and the unbuildable legacy project. Control-to-handler event edges are a #738
deliverable, not a #736 one.

## Validation commands

Recorded on macOS arm64 with .NET SDK 10.0.302:

```bash
# Cross-platform buildability proof for the SDK-style fixture.
dotnet build samples/vb-modern-sample/VbModernSample.vbproj
# Result: Build succeeded. 0 Warning(s). 0 Error(s).

# Expected failure modes for the legacy-shaped fixtures (MSB3644:
# reference assemblies for .NETFramework v3.5 / v4.8 are unavailable on the
# cross-platform SDK; compilation never runs, which is the intended fallback
# scenario).
dotnet build samples/vb-legacy-sample/VbLegacyCatalog.vbproj
dotnet build samples/vb-webforms-sample/VbWebFormsSample.vbproj

# Automated syntax validation of all 13 fixture .vb files at the latest
# language version, plus the legacy fixture's six files re-parsed as
# Visual Basic 9 (the toolset its ToolsVersion=3.5 project models). The
# focused tests use Microsoft.CodeAnalysis.VisualBasic at TraceMap's pinned
# Roslyn version.
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~VbNetFixtureTests

# Regression guard for the solution (no production code changed in this
# fixture branch, but the suite must stay green).
dotnet build src/dotnet/TraceMap.sln
git diff --check
./scripts/check-private-paths.sh
```

`TraceMap.Tests` also references the modern fixture project with
`ReferenceOutputAssembly=false`, so the standard solution build continuously
compiles that fixture without adding intentionally failing legacy projects to
the product solution.

## Commit-pinned open-source smoke candidates

Candidates for the real VB.NET smoke test (spec task 9). SHAs are HEAD as
observed via `git ls-remote` on 2026-09-11 and must be re-verified and
re-pinned at implementation time; none has been scanned end-to-end yet.

| Candidate | License | Pin (HEAD @ 2026-09-11) | Suitability and caveats |
| --- | --- | --- | --- |
| `dotnet/roslyn` | MIT | `251b3e508a06508649124493d7f19a8e726654c6` | The VB compiler is self-hosted, so `src/Compilers/VisualBasic/**` is the canonical modern Tier1 VB corpus (every declaration/call family, including syntax-fallback-hostile code). Caveat: the full repository is very large; scope the scan to the VB compiler subtree and expect long scan times. |
| `CommunityVB/Community.VisualBasic` | MIT | `20d2a51dfc9f342848ad134952ceaa8d79302559` | Community re-implementation of the `Microsoft.VisualBasic` runtime written in VB.NET; moderate size (~1 MB of source), modern SDK-style projects, cross-platform targets. Best candidate for a full-semantic smoke that should load and build without Windows-only dependencies. |
| `mono/mono-basic` | Mixed: `vbnc` compiler LGPL-2.0; class libraries MIT/X11 style (GitHub reports NOASSERTION) | `bdb5276f7d85100e8e9ddd7e5ba2360a792644a9` | Mono's VB compiler and runtime written in VB; older language level and legacy-ish shapes, useful for fallback/legacy comparison. Caveats: no standard single license file (verify the per-component license before any redistribution; scanning locally is unaffected), and the build is autotools/Mono-era, so expect reduced coverage rather than a clean project load. |
| `dotnet/docs` | MIT | `c2bef19f060e409b2f71f8d46eee85f5cac94106` | `samples/snippets/visualbasic/**` is a broad corpus of small projectless VB files; good for syntax-fallback and inventory-at-scale checks rather than semantic project loading. Caveat: the whole repository is large; scope to the snippets subtree. |

Evaluated and rejected for the Web Forms lane: public VB Web Forms
corpora are scarce. `riganti/dotvvm-samples-webforms-migration-vbnet`
(Apache-2.0) was considered and rejected because its language distribution is
dominated by JavaScript with only minimal VB content, so it is not
representative of a VB Web Forms application. Until a suitable public corpus
is identified, `samples/vb-webforms-sample` is the Web Forms planning corpus
for #738, and any proprietary Web Forms scan stays behind the private
validation boundary documented in the spec's implementation state.

## Limitations

- These fixtures have not been scanned by a VB adapter because #736's
  extractor work lives on the foundation branch; all "expected" statements
  here are guidance, not verified adapter output.
- Build expectations were recorded on macOS arm64 with the .NET 10 SDK. On
  Windows with .NET Framework targeting packs installed, the legacy and
  Web Forms fixtures may evaluate further; reduced coverage is a
  cross-platform expectation, not a universal claim.
- The legacy fixture intentionally references a vendor assembly that does not
  exist; that is the point. Do not "fix" it by shipping a stub DLL.
- `.aspx` markup has no automated syntax validation in this branch; it was
  reviewed manually. Markup parsing belongs to #738.
- OSS pins drift and none of the candidates has been scanned end-to-end;
  spec task 9 owns the final pin and validation.
