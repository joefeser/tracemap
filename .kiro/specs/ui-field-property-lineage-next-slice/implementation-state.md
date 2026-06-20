# UI Field and Property Lineage Next Slice Implementation State

Status: spec-pr-open-review-loop-in-progress

## Current Branch

`codex/spec-ui-field-property-lineage-next-slice`

## Scope

This is a spec-only continuation PR for the next UI field/property lineage
implementation boundary. It does not implement product code.

The spec focuses on connecting existing Angular/Razor field/control/binding
evidence to DTO/model property identity and downstream static route, path,
reverse, data, dependency, vault, docs-export, and static HTML explorer
evidence. Optional browser/computer-use evidence remains deferred and
demo-only.

## Source Material

- Existing Kiro spec: `.kiro/specs/ui-field-property-lineage/`.
- Existing implementation state for the first property-flow slices.
- Current `tracemap property-flow` implementation and focused tests.
- Public issue #165: trace UI field bindings to model and backend properties.
- Public issue #159: route-centered static call flow report.
- Existing docs for validation, vault export, evidence docs export, and static
  HTML evidence explorer.

## Baseline Observed

- Existing property-flow supports selector parsing, source/framework filters,
  generic property-name downgrade, stable Markdown/JSON reports, safe metadata,
  and combined-index read-only reporting.
- Angular template/form/event/template-variable facts already exist for the
  initial UI root slice.
- Razor binding and static form target facts already exist for the initial
  Razor slice.
- `RazorModelBindingTarget` is reserved in the report model but model-binding
  target extraction remains follow-up work.
- The existing tasks list still leaves deeper event-handler-to-payload,
  payload-to-HTTP, endpoint-to-model, mapping/projection, validation,
  service/repository, query/data/entity, and dependency-surface property hops as
  follow-up work.
- Route-flow exists in the repository, but property-flow must still rely on a
  documented schema signal and emit gaps when route-flow-specific traversal is
  unavailable.

## Scope Decisions

- The new spec does not duplicate the broad original UI lineage spec.
- The first implementation PR after this spec should be limited to property
  identity joins and model-binding target facts with focused downstream
  composition through already indexed evidence.
- Same-name and generic property joins remain review-tier unless narrowed by
  exact fact, symbol, source, type, or rule-backed alias/value-origin evidence.
- Browser/computer-use capture is excluded from core property-flow and deferred
  to a future explicit opt-in demo workflow if needed.
- Public fixtures must be synthetic and must not include private names, local
  paths, raw routes, raw SQL, config values, raw remotes, hostnames, secrets,
  snippets, or private sample labels.

## Oddities

- The prior spec task list marks much of property-flow complete while still
  listing downstream composition details as unchecked or follow-up work. This
  continuation treats those unchecked/follow-up items as the next boundary.
- Route-flow is implemented locally even though the public issue remains open.
  This spec uses schema/evidence availability rather than issue state as the
  product gate.

## Review Commands

Planned:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-next-slice --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-next-slice --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Re-review limit: at most two cycles, preferring Sonnet for final re-review
unless Opus is clearly needed.

## Review Results

- Opus Kiro spec review completed with full coverage:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T202307-114Z-spec-claude-opus-4.8.clean.md`.
  It found blockers around Markdown section order and first-PR scope, plus
  important determinism clarifications for rule IDs, gap vocabulary, route-flow
  schema signal, fan-out threshold, and missing tests.
- Sonnet Kiro spec review completed with full coverage:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T202709-329Z-spec-claude-sonnet-4.6.clean.md`.
  It found blockers around model-binding rule IDs, report version policy, and
  generic fan-out threshold, plus non-blocking selector, endpoint-signal,
  same-name, downstream consumer gap, and spec-lint recording clarifications.
- Patched review findings:
  - Added Coverage Warnings to the documented Markdown section order.
  - Split implementation tasks into recommended PR slices so PR 1 is focused on
    model-binding/property identity joins and focused tests.
  - Collapsed model-binding rule guidance onto existing
    `csharp.razor.model-binding.v1` unless future implementation first adds and
    reviews separate catalog entries.
  - Added explicit report version compatibility and `1.1` bump policy.
  - Pinned generic property fan-out to a deterministic v1 count threshold of 10.
  - Named `combined_route_flow_edges` as the route-flow schema signal.
  - Clarified endpoint alignment signal expectations over `combined_facts` and
    `index_sources`, with future persisted `endpoint_matches` as a documented
    successor.
  - Reused existing `MissingOptionalSchema` and `RouteFlowUnavailable` gaps
    instead of adding overlapping downstream schema gaps.
  - Clarified same-name-only joins, DTO/model family overlap, downstream
    consumer gap reuse, and missing-test expectations.
- Sonnet re-review cycle 1 completed with reduced coverage because Kiro
  reported denied tool access:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T203051-780Z-re-review-claude-sonnet-4.6.clean.md`.
  The re-review found remaining rule-ID and test precision issues. Patched:
  - Documented TypeScript value-to-payload and property mapping hops as
    `property-flow.edge.v1` report-layer derived edges that must preserve source
    fact rule IDs, with new scanner rules required before new scanner facts are
    emitted.
  - Added `EndpointAlignmentUnavailable` for available endpoint-alignment schema
    with no matching rows.
  - Added same-name downgrade notes to selector rows.
  - Clarified additive closed-vocabulary kind values versus version-bump
    triggers.
  - Clarified MVC-specific versus Razor-Pages-specific safe metadata.
  - Added missing PR 1 test scenarios for Razor control filters, DTO/model
    family exclusion and overlap, `fact:` disambiguation, missing handler
    matches, and empty route-flow schema.
- Sonnet final re-review cycle completed with full coverage:
  `.tmp/kiro-reviews/ui-field-property-lineage-next-slice/2026-06-20T203504-006Z-re-review-claude-sonnet-4.6.clean.md`.
  It reported the spec was ready to merge after two narrow blocker fixes. These
  were patched after the final allowed re-review:
  - Clarified precedence when same-name-only and high fan-out gaps both apply.
  - Added a task gate requiring TypeScript scanner rule catalog entries, or an
    explicit no-new-scanner-fact note, before PR 1 fixtures rely on new
    TypeScript facts.
  Additional narrow recommendations patched without another review cycle:
  - Cross-referenced endpoint alignment schema in requirements.
  - Clarified observed evidence additive compatibility, multi-family candidate
    sorting, `[FromBody]`/`[FromForm]` without form target tests, model-binding
    byte-stability tests, and fan-out threshold boundary tests.

## Validation

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Existing spec lint/check discovery: no dedicated spec lint/check script was
  found in repository scripts or package metadata during this spec pass.

## PR Review Loop

- Ready PR opened: #237 into `dev`.
- Initial PR loop command:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 237 --base dev --require-codex-review --json
```

- Initial PR loop result on head `2c896cfb2ddbd30eb29ad2debc3df1a6a10f19ff`:
  `merge_ready`.
- Required Codex and Qodo reviews settled cleanly. Optional Gemini review was
  not requested and optional Gemini review threads were reported as residual
  risk by policy, not blockers. No pending checks, failed checks, unresolved
  threads, or actionable bot findings were reported.
- This state-only bookkeeping update should be followed by a fresh PR-loop run
  after push so the final decision reflects the current head.
- Subsequent PR-loop runs after bookkeeping found actionable Gemini and Qodo
  findings. Patched:
  - Corrected task expectations from `ModelBindingUnavailable` to
    `EndpointAlignmentUnavailable` for a Razor form target with no matching
    action/handler.
  - Corrected `model:<type>.<property>` no-match task expectation to
    `SelectorNoMatch`.
  - Aligned route-flow gap wording with the current baseline
    `RouteFlowUnavailable` schema gap.
  - Aligned selector candidate sorting with the current property-flow
    selected-root baseline order.

## Follow-Ups For Implementation

- Add or strengthen model-binding target facts.
- Add UI binding/control to property identity joins.
- Add payload/form to endpoint and DTO/model joins.
- Compose downstream static route/path/reverse/data/dependency evidence where
  existing facts support it.
- Preserve report/export contracts and safe artifact integration.
- Add public-safe fixtures and focused tests.
