# Legacy Data Model Metadata Extraction Implementation State

Status: implementation-slice-in-progress
Spec authoring branch: codex/spec-legacy-data-model-metadata-extraction
Current implementation branch: codex/implement-legacy-data-model-metadata-extraction
Public claim level: hidden

## Why This Spec Exists

Status normalized during spec-state cleanup. The spec authoring tasks, review
patches, PR-thread fixes, and spec-only validation are complete; product
implementation tasks remain not started.

The earlier `legacy-data-metadata-extraction` spec implemented a conservative
MVP for DBML, EDMX, typed DataSet/TableAdapter, config provider metadata, and
generated-code linkage. The remaining old-codebase gap is model-level
normalization and workflow integration: a reviewer should be able to follow
static WebForms, WCF, ASMX, Remoting, service, query, or dependency paths to
safe legacy data model descriptors such as likely entities, tables, columns, and
relationships without TraceMap claiming runtime database use.

This spec is the next slice. It extends the data metadata foundation rather than
replacing it.

## Scope Decisions

- Spec-only branch. No product code, site files, generated outputs, or rule
  catalog implementation were changed during spec authoring.
- Reuse existing `LegacyData*` facts and `legacy.data.*.v1` rules where they
  already express descriptor evidence correctly.
- Reuse existing `LegacyData*` fact types with additive metadata for model
  identity, relationship, and old ORM mapping evidence. MVP does not add
  `LegacyDataModelSurfaceProjected` or a parallel scan-time surface fact.
- Existing DBML, EDMX, typed DataSet, TableAdapter, config, and generated-link
  source facts keep their current source rule IDs. New model identity,
  relationship, and surface rules document derived projection semantics rather
  than re-emitting existing source facts under duplicate rule IDs.
- Start old ORM extraction with a reviewable NHibernate `.hbm.xml` MVP.
- Reuse the parser helper used by the implemented legacy data metadata extractor,
  currently `SafeXml`, and existing safety/malformed/too-large gap
  classification strings unless implementation explicitly changes that shared
  reader consistently.
- Recognize other old ORM descriptor families as unsupported gaps unless a
  future spec defines deterministic parsers.
- Keep generated-code linkage separate from descriptor facts; semantic links do
  not upgrade descriptor tier ceilings.
- Reuse canonical dependency surface kind `legacy-data` for combined/path/
  reverse/impact/export workflows, with `surfaceSubtype = data-model` and
  model-specific metadata. Do not introduce `legacy-data-model` in MVP.
- Exclude `AnalysisGap` facts under `legacy.data.*` rules from terminal surface
  projection and avoid re-consuming already-derived projection rows.
- Keep public/private safety strict: no raw SQL, snippets, connection strings,
  config values, remotes, URLs, local absolute paths, private sample labels, or
  secrets in committed artifacts by default.
- No runtime database connections, live schema introspection, migration
  execution, EF/NHibernate runtime loading, SQL execution, provider behavior
  emulation, LLM calls, embeddings, vector DBs, or prompt classification.

## Alignment Notes From Repository Inspection

- Existing data metadata spec:
  `.kiro/specs/legacy-data-metadata-extraction/`.
- Existing rules in `rules/rule-catalog.yml` include six `legacy.data.*.v1`
  entries for inventory, DBML, EDMX, typed DataSet, config, and generated links.
- `docs/VALIDATION.md` already has focused validation guidance for legacy data
  metadata extractor changes.
- `docs/ACCEPTANCE.md` already lists legacy DBML, EDMX, typed DataSet,
  provider config, and generated-code expectations.
- Existing flow and combined-report specs already refer to legacy data metadata
  as an optional terminal/supporting evidence family, so this spec should feed
  those workflows additively.

## Review State

Initial Kiro spec reviews ran with full coverage:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-metadata-extraction --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-data-model-metadata-extraction --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
```

- Opus found blocking repo-alignment gaps around the existing `legacy-data`
  surface kind and prefix-based projection of `legacy.data.*` gaps. Patched by
  reusing `legacy-data`, excluding `AnalysisGap` facts from terminal projection,
  avoiding `LegacyDataModelSurfaceProjected`, and adding validator/default-reader
  tasks and tests.
- Sonnet found Medium+ gaps around fact ownership, parser bounds, fixture
  strategy, gap grouping, cardinality limits, relationship scope, and missing
  explicit tests. Patched by committing to existing `LegacyData*` fact ownership,
  `LegacyDataMappingDeclared` relationship rows with `mappingKind`,
  parser-bound requirements, per-class caps, fixture strategy, grouped gaps, and
  targeted tests.
- Sonnet requested `review-prompts.md`; this spec branch intentionally did not
  add it because the delegated scope was limited to `requirements.md`,
  `design.md`, `tasks.md`, and `implementation-state.md`, and the wrapper
  reviews completed successfully without it.
- Re-review Sonnet found one remaining fact ownership blocker. Patched by
  clarifying that existing source rules retain fact provenance; new model rules
  primarily own derived identity/relationship/surface semantics, while
  NHibernate and unsupported ORM rules own new source evidence.
- Re-review Opus had reduced coverage due to denied shell access and found a
  parser/taxonomy alignment blocker. Patched by aligning NHibernate parsing with
  the same helper used by `LegacyDataMetadataExtractor`, currently `SafeXml`,
  and reusing existing `LegacyDataParserSecurityRejected`,
  `MalformedLegacyDataMetadata`, and `LegacyDataMetadataTooLarge` gap
  classifications.
- PR review loop found four unresolved review threads. Patched by adding
  `config` to `metadataFormat` surface fields, moving NHibernate query and
  named-query metadata to unsupported/needs-review gaps, aligning parser
  instructions with `SafeXml`, and preserving existing relationship
  `mappingKind` values through additive model relationship metadata.

Medium+ actionable findings from the first review cycle and first re-review
cycle are patched. No further re-review cycle should be run unless the spec
owner explicitly approves it.

## Validation State

Spec delivery validation run after review patches:

```bash
./scripts/check-private-paths.sh
git diff --check
```

- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- After PR-thread patches, reran `./scripts/check-private-paths.sh`: passed.
- After PR-thread patches, reran `git diff --check`: passed.
- No additional spec/docs validation scripts were found beyond
  `scripts/kiro-review.mjs`, `scripts/check-private-paths.sh`, and
  `scripts/validate-legacy-codebases.sh`; the legacy codebase validation script
  is implementation/smoke oriented and was not run for this spec-only branch.

## Follow-Ups For Implementation Planning

- Confirm combined dependency-surface projection fields before extending
  `legacy-data` selector support in report/path/reverse/impact workflows.
- Keep NHibernate MVP narrow. Provider/runtime behavior, Fluent mappings, named
  query parsing, formulas, filters, inheritance, composite IDs, and custom SQL
  should stay gaps unless deterministic tests and redaction rules are added.
- Add public-safe synthetic fixtures before making public coverage claims.

## Implementation Slice 1 State

Branch: `codex/implement-legacy-data-model-metadata-extraction`

Selected scope: Task 1 only. This slice establishes the rule/catalog and code
constant contract that later extractor, projection, graph, and export tasks must
target before they emit model-level conclusions.

Implemented:

- Added `RuleIds` constants for `legacy.data.model.identity.v1`,
  `legacy.data.model.relationship.v1`, `legacy.data.orm.nhibernate.v1`,
  `legacy.data.orm.unsupported.v1`, `legacy.data.model.generated-link.v1`, and
  `legacy.data.model.surface.v1`.
- Added rule catalog entries documenting emitted fact types, evidence tiers,
  safe properties, and limitations for the model identity, relationship,
  NHibernate, unsupported ORM, generated-link, and surface projection rules.
- Reserved `legacy.data.model.generated-link.v1` for future model-normalized
  generated-code links while leaving existing generated-code extractor
  provenance under `legacy.data.generated-link.v1`. Decision rationale:
  existing DBML, EDMX, and typed DataSet generated-code links keep correct
  provenance under the original source rule; future tasks should use the model
  rule only when they add model-normalized linkage semantics beyond that source
  rule.
- Kept `legacy.data.model.surface.v1` as a report/export projection rule only.
  No `LegacyDataModelSurfaceProjected` scan fact or new `legacy-data-model`
  surface kind was added.
- Added focused catalog/constant tests in
  `LegacyDataModelRuleCatalogTests`.

Oddities and scope decisions:

- This is a first implementation PR slice because the full spec spans scanner
  extraction, NHibernate XML parsing, relationship normalization, combined
  report/path/reverse/diff/impact integration, graph/vault export, fixtures,
  and smoke guidance. Implementing all of that in one PR would be too broad to
  review safely.
- No scanner behavior changes are included in this slice, so no new facts are
  emitted yet. Existing DBML, EDMX, typed DataSet, config, and generated-link
  source facts retain their existing rule IDs.
- Public claim level remains hidden. No site files, generated scan outputs,
  private samples, raw SQL, snippets, remotes, local absolute paths, secrets, or
  local/private artifact labels are in scope.

Validation executed before PR:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModelRuleCatalogTests`: passed, 3 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests`: passed, 11 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 443 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out <tmp>`: passed; emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Kiro implementation review:

- `node scripts/kiro-review.mjs --phase legacy-data-model-metadata-extraction --kind implementation --model claude-sonnet-4.5 --fresh --timeout-ms 600000`: reduced coverage because the review harness reported denied shell access for one command, but no blocking or Medium+ findings. The only actionable review recommendation was to record validation results here, now done.

PR review-loop follow-up:

- Initial PR loop found one required Qodo thread on nonstandard compound
  `evidenceTier` strings in the new catalog entries. Patched the new entries to
  use single fixed tier values and hardened `LegacyDataModelRuleCatalogTests` so
  it verifies that contract with whitespace-tolerant rule lookup. Reran
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModelRuleCatalogTests`, `./scripts/check-private-paths.sh`, and `git diff --check`: all passed.

Follow-ups:

- Task 2 should add deterministic model identity helpers and additive safe
  metadata fields over existing source extractors.
- Task 3 should add relationship semantics and ambiguity gaps while preserving
  existing source `mappingKind` values.
- Task 4 should add the narrow NHibernate `.hbm.xml` MVP using the existing
  legacy data safe XML parser bounds and gap classifications.
- Tasks 7-9 should project model-enriched `legacy-data` surfaces, excluding
  `AnalysisGap` facts from terminal projection and avoiding double projection.
