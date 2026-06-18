# Legacy WinForms Event Navigation Discovery Implementation State

Status: implementation-complete
Branch: codex/implement-legacy-winforms-event-navigation-discovery
Public claim level: hidden

## Implementation Update - 2026-06-18

Implemented the static WinForms event/navigation discovery slice on
`codex/implement-legacy-winforms-event-navigation-discovery`.

### Landed Scope

- Added WinForms fact/model constants and rule catalog entries for inventory,
  controls, event binding, handler resolution, navigation, callback boundaries,
  handler-flow projection, and safe resource metadata.
- Added deterministic `LegacyWinFormsExtractor` coverage for syntax/structural
  WinForms surfaces, designer `InitializeComponent` controls, explicit event
  subscriptions, scoped handler resolution, `Application.Run`, `Show`,
  `ShowDialog`, MDI parent assignment, timer/background-worker/UI marshal
  callback boundaries, safe `.resx` key-hash metadata, and explicit gaps.
- Reused existing scan-local call/object/backend facts for direct
  `WinFormsHandlerFlowProjected` evidence. Terminal names are hashed through
  existing TraceMap digest helpers.
- Integrated WinForms evidence into scan output, generic SQLite/NDJSON storage,
  scan Markdown report sections, legacy validation UI-event reconciliation,
  combined legacy-flow root/projection graph consumption, adapter contract docs,
  acceptance docs, and validation guidance.
- Added focused tests for designer subscriptions, code subscriptions,
  navigation/callback/resource extraction, unsafe resource rejection, lambda
  gap behavior, ambiguous/missing handlers, UI marshal receiver scoping,
  terminal hash format, non-designer `InitializeComponent` tier boundaries,
  older-index availability gaps, validation reconciliation, redaction, and
  deterministic fact IDs.

### Scope Decisions And Oddities

- Implementation remains static and deterministic. It does not execute WinForms
  apps, run designers, automate UI, resolve runtime DI/reflection, evaluate
  config transforms, fetch services, connect to databases, or claim runtime
  reachability.
- Semantic enrichment is opportunistic through existing Roslyn facts; syntax and
  structural fallback remain useful when no project loads.
- `.resx` support stores only resource presence, culture suffix, safe key
  hashes, and kind labels. Raw resource values are omitted.
- Rich multi-hop graph expansion, runtime control-tree reconstruction, layout,
  localization rendering, auth/role proof, branch feasibility, and scheduling
  proof remain deferred.
- The temporary CLI smoke fixture lived under `/tmp` and was not committed.

### Validation Run

- `dotnet test src/dotnet/TraceMap.sln --filter LegacyWinFormsExtractorTests`
  passed: 11 tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 497 tests.
- `python3 -m unittest scripts.tests.test_legacy_codebase_validation` passed:
  12 tests.
- Temporary synthetic WinForms CLI smoke passed twice, produced
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`, emitted WinForms SQLite counts, rendered WinForms report
  sections, and produced byte-stable `facts.ndjson` across repeated scans.

### Kiro Review

- Sonnet implementation review (`claude-sonnet-4.6`) completed with reduced
  coverage because Kiro reported denied tool access
  (`kiro.review.wrapper.v1` / `ToolDenied`).
- Patched actionable findings from the first review: UI marshal receiver
  scoping, same-name handler evidence scoping, noisy dynamic/reflection gaps,
  navigation tier derivation, and explicit `WinFormsBackendPathUnavailable`
  gap rows.
- Sonnet re-review (`claude-sonnet-4.6`) also completed with reduced coverage
  because Kiro reported denied tool access.
- Patched re-review findings: removed raw handler names from gap messages,
  excluded WinForms structural/navigation/callback rows from handler-flow
  backend evidence, added validation non-divergence coverage, added older-index
  availability-gap coverage, and added terminal-hash/tier-boundary tests.

### Follow-Ups

- Add public-safe smoke catalog rows only after reviewed synthetic/public
  fixture evidence is approved for promotion.
- Expand tests for additional custom delegate, duplicate partial, and
  factory-selected navigation cases if later specs require deeper precision.

## Why This Spec Exists

Status normalized during spec-state cleanup. This is a spec-only slice with
review and validation notes recorded below; product implementation remains
deferred to future implementation tasks.

TraceMap has deterministic legacy evidence for WebForms event flow, WCF and ASMX
service references, remoting, legacy data metadata, SQL/query surfaces, value
flow, dependency paths, evidence graph export, and vault export. Very old
desktop .NET applications still need a comparable static entry-point layer for
Windows Forms designer files and event handlers.

This spec defines that next old-codebase slice. It keeps WinForms evidence as
static repository evidence and connects it to existing backend evidence only
where rule-backed facts support the link.

## Scope Decisions

- Spec-only PR: no product code, no site files, no generated outputs, and no
  rule catalog edits in this branch.
- The implementation must remain deterministic and static.
- Do not execute applications, run designers, automate UI, simulate WinForms
  runtime behavior, fetch services, connect to databases, or evaluate config
  transforms.
- Prefer semantic Roslyn evidence when old projects load, but keep syntax and
  structural fallback useful when MSBuild/project load fails.
- Treat `.Designer.cs` and `.resx` as supporting static artifacts. Generated
  designer files may be stale, and resource metadata may be localized,
  generated, binary, or unsafe.
- Emit explicit gaps for dynamic controls, runtime event subscription,
  reflection, generated designer drift, localization/resource indirection,
  dependency injection, auth/role gating, branch feasibility, thread scheduling,
  runtime state, missing backend metadata, and reduced semantic coverage.
- Reuse existing method call, object creation, value-flow, dependency path,
  WCF/ASMX/remoting, legacy data metadata, SQL/query, evidence graph, and vault
  export infrastructure rather than creating a parallel graph engine.
- Defer full WinForms runtime control-tree simulation.

## Safety Notes

Committed spec text intentionally avoids raw local absolute paths, private
sample names, raw remotes, raw config values, raw SQL, raw resource values,
source snippets, endpoint addresses, connection strings, and secrets.

Future implementation must keep those values out of facts, reports, validation
summaries, graph exports, vault exports, smoke catalog entries, and committed
artifacts unless an explicit safe hash/omission policy allows metadata to be
stored.

## Review Loop Notes

- Kiro CLI Opus review ran with `claude-opus-4.8` and completed with full
  coverage.
- Kiro CLI Sonnet review ran with `claude-sonnet-4.6` and completed with reduced
  coverage because Kiro reported denied tool access:
  `kiro.review.wrapper.v1` / `ToolDenied`.
- Medium+ actionable findings patched in the spec:
  scan-time versus report-time handler-flow locus, `AnalysisGap` terminology,
  deterministic fact-ID guidance, `legacy.validation.ui-events.v1`
  reconciliation, minimal fixture guidance, canonical handler symbol property,
  safe hash guidance, navigation tier tiebreakers, designer-subscription tier
  rules, UI marshal scoping, upstream availability gaps, and missing test tasks.
- Re-review cycle 1:
  - Opus `claude-opus-4.8` completed with reduced coverage because Kiro reported
    denied tool access: `kiro.review.wrapper.v1` / `ToolDenied`.
  - Sonnet `claude-sonnet-4.6` completed with full coverage and found no
    blockers.
  - Non-blocking clarifications patched afterward: handler-resolution tier table
    conditions, WinForms-only `legacy.validation.ui-events.v1` supersession,
    WebForms-attributed evidence preservation, hash-format consistency, MVP
    integration priority, and explicit non-divergence/backward-compat/hash tests.
- PR review loop follow-up patched five medium Gemini wording findings:
  inventory-versus-surface enrichment redundancy, event-binding gap terminology,
  fully qualified WinForms base types, callback-boundary classification wording,
  and evidence-tier versus classification wording in handler-flow tasks.
- PR review loop second follow-up patched one Codex P2 finding so absent
  report-time combined graph data does not downgrade scan-time handler-flow
  evidence.

## Validation Plan For This Spec PR

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- `node scripts/legacy-sample-smoke-catalog.mjs validate --catalog docs/validation/legacy-sample-smoke-catalog/catalog.json`
  passed.

Product implementation validation is intentionally deferred to the implementation
tasks in `tasks.md`.

## Follow-Ups To Keep Out Of This Spec PR

- Rule catalog edits and model constants.
- Scanner/extractor/report implementation.
- Static site changes.
- Public marketing or AI impact-analysis claims.
- Runtime app behavior claims.
