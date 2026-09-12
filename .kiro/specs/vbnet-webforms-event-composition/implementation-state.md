# Implementation state

- Issue: #738
- Branch: `codex/issue-738-vbnet-webforms`
- Base: `origin/dev` at `5abd59d79acd074f29e6986d0dd4a31102ac74f1`
- Status: implementation and validation complete; branch pushed for review
- Initial series: `8a306018` (implementation/tests), `4f64f340`
  (spec/docs/catalog), `c5cb4519` (validation bookkeeping)

## Decisions

- Language-level VB event facts are separate from shared Web Forms projection.
- Semantic resolution is authoritative; syntax fallback never invents symbol IDs.
- `RemoveHandler` records deterministic detach evidence, not a positive runtime binding.
- Existing Web Forms consumers are reused through their shared fact contracts.
- Compiler-resolved language facts use canonical VB method/event symbol IDs;
  unresolved event sites use the owning event rule and reduce coverage.
- Event-site gaps remain explicit producer-local evidence but do not change an
  otherwise successful Roslyn compilation into a failed build status.
- Shared Web Forms projection is limited to linked markup surfaces and supported
  lifecycle/control receivers. Custom `WithEvents` sources remain language-level
  evidence unless they correspond to one markup control.
- A semantic `MethodDeclared` fact carries the method identity in its target
  fields; shared handler resolution normalizes that shape so retained Tier1 or
  exact same-method Tier3 call evidence is preserved.

## Validation

The applicable Visual Basic adapter procedure in `docs/VALIDATION.md` was
followed, including the solution/focused suites, all three fixture scans,
artifact validation, deterministic comparison, privacy guards, and the pinned
`community-visual-basic` smoke. The commands, immutable pin, and sanitized
result totals below are the committed review record; no private source, paths,
or native diagnostics were retained.

- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 1,909/1,909 passed after
  case-insensitive-join, lifecycle-shadow, lambda, qualified-receiver,
  signature, source-hash, and report hardening.
- Focused VB/fixture/legacy Web Forms tests: 113/113 passed.
- Focused Web Forms/legacy-flow composition tests: 217/217 passed.
- `Test-FocusedWebFormsCodePathReviewSet.ps1`: passed, including private and
  anonymous/shareable review generation.
- `Test-FocusedWebFormsApplicationWorkbench.ps1`: passed with raw-source off,
  explicit raw-source on, symlink safety, and reviewed WITS state validation.
- Fresh `vb-webforms-sample` scans: 199 facts,
  `Level1SemanticAnalysisReduced`; repeated `facts.ndjson` byte-identical;
  adapter artifact validator passed. Shared flow facts retain exact call-edge
  support where present.
- Pinned `community-visual-basic` smoke at
  `20d2a51dfc9f342848ad134952ceaa8d79302559`: passed with 110,726 facts,
  13,905 call edges, 517 object creations, 175 argument flows, 75,455 bounded
  gaps, and reduced coverage. Event additions contributed only 13 facts/gaps.
  The first wrapper invocation could not build the unrelated TypeScript adapter
  because local `tsc` dependencies were absent; after the .NET solution had
  already built and tested, the selected VB smoke was rerun with
  `TRACEMAP_SKIP_BUILD=1` and completed successfully.
  The selected pinned smoke was rerun after PR-gate hardening with the same
  totals and posture.
- `git diff --check` and `scripts/check-private-paths.sh`: passed.

## Remaining boundaries

- Static event evidence does not prove runtime attachment, firing, ordering,
  dispatch, lifecycle execution, postback state, or reachability.
- `RemoveHandler` is detach evidence only and never projects as a positive
  shared binding.
- VB-only joins use ordinal case-insensitive identifier comparison without
  weakening C# joins. Unqualified shadowed `IsPostBack` conditions remain
  explicit gaps, and lambda delegates cannot be misread through methods called
  inside the lambda body.
- Page-qualified `Me`/`MyClass` control and handler receivers are supported;
  arbitrary qualified delegates, complex receivers, and local/parameter names
  that shadow markup controls or `Page` remain explicit gaps rather than
  name-only joins. Composite delegates remain gaps rather than selecting an
  arbitrary child method.
- Invalid delegate conversions and containing-type field/property shadows of
  the lifecycle `Page` receiver likewise remain gaps rather than positive
  compiler/shared bindings.
- Compiler-invalid `Handles` signatures/receivers and unsupported delegate
  variables are producer-local gaps. The shared Web Forms projection correlates
  those sites by source hash and does not recreate rejected bindings by name.
- Shadowed lifecycle subscription receivers do not suppress an independently
  valid `AutoEventWireup` binding.
- A declared `.vb` CodeBehind/CodeFile path preserves VB case-insensitive page
  identity for checked-in designer evidence even when that linked code file is
  absent from the scan.
- Late-bound/error-typed receivers, unsupported delegate/lambda shapes,
  ambiguous handlers or partials, missing framework metadata, and truncated
  budgets remain explicit gaps rather than guessed joins.
- Cross-language VB/C# symbol-identity joins remain unestablished.
- VB client-script registration and broader business-logic interpretation are
  outside this issue.

## Latest exact-head review fixes

- Rejected method-name fallback for delegate variables that happen to shadow a
  containing-type method; the bounded fallback now applies only to an actual
  `AddressOf` operand.
- Rejected compiler-invalid `Handles` clauses for incompatible signatures
  (`BC31029`) and non-`WithEvents` receivers (`BC30506`) without treating
  missing legacy framework metadata as proof that an otherwise useful syntax
  projection is invalid.
- Prevented the shared Web Forms extractor from recreating compiler-rejected VB
  event sites, and excluded shadowed subscriptions from explicit lifecycle
  wireup detection.
- Extractor identities are now `vb-semantic/0.5.1` and
  `legacy-webforms/0.8.1`.
- Validation after these fixes: solution 1,909/1,909; focused VB/fixture/legacy
  Web Forms 113/113; Web Forms/legacy-flow 217/217; all three VB fixture
  artifacts valid; modern facts byte-identical; pinned CommunityVB smoke
  unchanged at 110,726 facts; privacy and diff guards passed.
