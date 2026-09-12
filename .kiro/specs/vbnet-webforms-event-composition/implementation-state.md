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
- Shared Web Forms projection is limited to linked markup surfaces and supported
  lifecycle/control receivers. Custom `WithEvents` sources remain language-level
  evidence unless they correspond to one markup control.
- A semantic `MethodDeclared` fact carries the method identity in its target
  fields; shared handler resolution normalizes that shape so retained Tier1 or
  exact same-method Tier3 call evidence is preserved.

## Validation

- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 1,899/1,899 passed after
  case-insensitive-join, lifecycle-shadow, and lambda hardening.
- Focused VB/fixture/legacy Web Forms tests: 94/94 passed.
- Focused Web Forms/legacy-flow composition tests: 207/207 passed.
- `Test-FocusedWebFormsCodePathReviewSet.ps1`: passed, including private and
  anonymous/shareable review generation.
- `Test-FocusedWebFormsApplicationWorkbench.ps1`: passed with raw-source off,
  explicit raw-source on, symlink safety, and reviewed WITS state validation.
- Fresh `vb-webforms-sample` scans: 200 facts,
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
- Late-bound/error-typed receivers, unsupported delegate/lambda shapes,
  ambiguous handlers or partials, missing framework metadata, and truncated
  budgets remain explicit gaps rather than guessed joins.
- Cross-language VB/C# symbol-identity joins remain unestablished.
- VB client-script registration and broader business-logic interpretation are
  outside this issue.
