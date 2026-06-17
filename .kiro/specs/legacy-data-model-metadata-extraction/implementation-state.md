# Legacy Data Model Metadata Extraction Implementation State

Status: spec-ready
Branch: codex/spec-legacy-data-model-metadata-extraction
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
