# Legacy .NET Toolchain Diagnostics Implementation State

Status: implemented
Branch: codex/implement-legacy-dotnet-toolchain-diagnostics
Spec path: `.kiro/specs/legacy-dotnet-toolchain-diagnostics/`

## Current Scope

This branch implements the deterministic `AnalyzerCapabilityDiagnostic` MVP on
top of the already implemented `legacy-build-environment-diagnostics` feature.
The scanner now derives analyzer capability facts from sanitized scan/build
evidence, writes them through existing facts/SQLite artifacts, renders reduced
or otherwise relevant capability rows in `report.md`, preserves capability
facts through combined indexes, surfaces reduced capability in combined known
gaps, and lets release review cite reduced/unknown toolchain capability as
coverage-relative context.

## Scope Decisions

- Added the fact type, rule IDs, schema version
  `legacy-dotnet-toolchain-diagnostics.v1`, closed vocabularies, and rule
  catalog limitations before emitting product rows.
- Reused `BuildEnvironmentDiagnostic`, `AnalysisGap`, `BuildStatus`, manifest
  coverage, semantic-result state, and static legacy fact evidence as support;
  the capability layer does not parse human report text.
- Kept static legacy framework/toolset/project/config/package signals
  informational unless observed scan behavior reduces coverage.
- Kept clean SDK-style semantic success machine-readable but report-quiet by
  suppressing noisy legacy capability report sections when all capability rows
  are available/informational.
- Stored capability diagnostics in the existing `facts` table rather than
  adding an MVP SQLite table or manifest schema expansion.
- Release review preserves `AnalyzerCapabilityDiagnostic` in the single-index
  comparable fact loader, adds source coverage gaps for reduced/unknown
  capability, and emits a schema compatibility gap for unrecognized
  `schemaVersion` values.
- Combined indexes preserve raw capability facts via `combined_facts`; combined
  dependency reports additionally roll reduced/unknown capability into
  source-scoped known gap rows.

## Oddities And Risks

- `AnalyzerCapabilityDiagnostic` facts are derived status/coverage rows. They
  are capped below Tier1 even when they cite stronger support facts.
- The MVP uses string-valued fact properties with bounded, sorted
  semicolon-delimited supporting IDs to match existing `CodeFact` conventions.
- The implementation intentionally avoids raw project/config/native values in
  capability rows. Tests construct unsafe fixture values at runtime rather than
  committing raw package source URLs or token-like literals.
- Direct evidence graph/vault export, legacy smoke catalog, and evidence-pack
  presentation remain deferred until a future spec claims direct schema
  support for `AnalyzerCapabilityDiagnostic`.

## Validation Log

- Passed: `dotnet test src/dotnet/TraceMap.sln --filter AnalyzerCapabilityDiagnosticTests`.
- Passed: `dotnet test src/dotnet/TraceMap.sln` after rebasing onto current
  `origin/dev` (507 tests).
- Passed: synthetic legacy CLI smoke using a generated non-SDK-style Web
  Application fixture; verified `scan-manifest.json`, `facts.ndjson`,
  `index.sqlite`, `report.md`, and `logs/analyzer.log`, plus
  `AnalyzerCapabilityDiagnostic` rows.
- Passed: `dotnet build src/dotnet/TraceMap.sln` after rebasing onto current
  `origin/dev`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed: `git diff --check`.
- Completed: Kiro CLI Sonnet implementation review with
  `node scripts/kiro-review.mjs --phase legacy-dotnet-toolchain-diagnostics --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Coverage was reduced because Kiro reported denied shell/tool access. Patched
  the actionable test reliability and safety findings.
- Completed: Kiro CLI Sonnet re-review with
  `node scripts/kiro-review.mjs --phase legacy-dotnet-toolchain-diagnostics --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Coverage was again reduced because Kiro reported denied shell/tool access.
  Patched the actionable downstream-coverage fallback and tier/load-failure
  test findings; did not run a third Kiro cycle.
- Pinned broad legacy smokes from `docs/VALIDATION.md` were explicitly
  deferred for this MVP because the implementation changes the C# scanner
  diagnostic/coverage contract, not a specific legacy adapter traversal such as
  WCF, ASMX, WebForms event flow, remoting, or legacy data extraction. The
  focused generated legacy smoke above exercises the new capability surfaces.

## Follow-Up Items

- Add direct capability presentation to evidence graph/vault export, legacy
  smoke catalogs, and evidence-pack workflows only when those consumers adopt
  closed capability-code/schema handling.
- Consider additive manifest capability summaries in a future compatibility
  slice if downstream consumers need manifest-only access.
- Broaden smoke coverage to public-safe checked-in fixtures if the project
  later maintains a legacy toolchain sample catalog.
