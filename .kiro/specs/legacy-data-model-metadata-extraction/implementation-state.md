# Legacy Data Model Metadata Extraction Implementation State

Status: continuation-ready
Spec authoring branch: codex/spec-legacy-data-model-metadata-extraction
Current implementation branch: codex/implement-legacy-data-model-metadata-followup
Public claim level: hidden

Post-promotion note: several legacy-data model identity/reporting slices have
landed, including PR #199 and PR #236. This spec still has follow-up work, but
there is no active implementation branch running for it after the PR #247
promotion.

## Current Branch Scope

This branch is a partial follow-up slice, not a full-spec closeout. It is
merge-ready only as a contained gap-evidence improvement for unsupported old
ORM descriptors.

- [x] Task 5 partial: LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, and Castle
  ActiveRecord descriptor signals emit `legacy.data.orm.unsupported.v1`
  `AnalysisGap` facts.
- [ ] Task 5 project-local mapping DSL detection remains deferred because it
  needs a separate deterministic signal taxonomy.
- [ ] Task 4 NHibernate `.hbm.xml` MVP remains deferred.
- [ ] Task 6 generated-code and mapped-symbol linkage hardening remains
  deferred.
- [ ] Tasks 7-9 downstream surface/report/path/reverse/graph/vault integration
  remain deferred.
- [ ] Task 10 broader docs/fixtures work remains partially deferred; this slice
  updates only acceptance, validation, language-adapter contract, and rule
  limitations for unsupported ORM gaps.

The unchecked tasks above are intentional runway, not hidden completed work.
They must not be interpreted as clean absence of legacy ORM metadata or as a
runtime database conclusion.

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

## Implementation Slice 2 State

Branch: `codex/implement-legacy-data-model-metadata-extraction-slice2`

Selected scope: Task 2 only. This slice adds normalized model identity metadata
to existing DBML, EDMX, and typed DataSet/TableAdapter source facts while
leaving NHibernate parsing, unsupported ORM gaps, generated-code hardening,
relationship ambiguity gaps, and downstream surface/report/export integration
for later tasks.

Implemented:

- Added a deterministic legacy data model identity helper that derives
  `stableModelKey` from metadata format, model kind, descriptor role,
  repo-relative file path, metadata-local scope, safe display/container names,
  and hashes for unsafe identity parts.
- Added additive model identity properties to existing DBML entity, storage
  object, column, association, routine, and mapping descriptor facts.
- Added additive model identity properties to existing EDMX CSDL/SSDL/MSL
  entity, storage object, column, routine, and mapping descriptor facts.
- Added additive model identity properties to existing typed DataSet DataSet,
  DataTable, DataColumn, relation, and TableAdapter command descriptor facts
  while preserving unrelated XSD gating.
- Preserved source rule provenance and descriptor tier ceilings. Existing
  source facts still emit under `legacy.data.dbml.v1`,
  `legacy.data.edmx.v1`, or `legacy.data.typed-dataset.v1`; model identity is
  recorded through properties such as `modelIdentityRuleId`,
  `modelIdentityEvidenceTier`, `metadataFormat`, `modelKind`,
  `descriptorRole`, `coverageLabel`, `sourceMetadataFactId`, and
  `stableModelKey`.
- Added focused tests for DBML identity fields, EDMX and typed DataSet identity
  fields, distinct stable keys for duplicate display names across formats and
  files, parser node/depth bounds, descriptor tier-ceiling preservation, and
  unsafe display-name hashing through SQLite persistence.
- Removed an unused parallel `LegacyDataXml` parser so legacy data model work
  continues to use the shared `SafeXml` parser bounds and classifications.
- Updated acceptance, language-adapter contract, validation guidance, and the
  model identity rule limitation text for the new additive identity properties.

Oddities and scope decisions:

- `review-prompts.md` is still absent for this spec. This matches the prior
  implementation-state note from spec authoring; no new prompt file was added
  in this implementation slice.
- This slice intentionally does not introduce new scan fact types, runtime
  model loading, NHibernate parsing, generated-code semantic upgrades, derived
  `legacy-data` surfaces, graph/vault export behavior, or public site claims.
- TableAdapter command descriptors keep the typed DataSet source rule while
  using `metadataFormat = tableadapter` for model identity so downstream
  readers can distinguish adapter command metadata without changing source
  rule ownership.

Validation executed before PR:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests`: passed, 22 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModel`: passed, 3 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 454 tests.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out <tmp>`: passed; emitted the required scan artifacts and 23 facts.

Kiro implementation review:

- Initial Sonnet implementation review had reduced coverage because the review
  harness reported denied shell access. It found one blocking issue around an
  unused parallel XML parser helper and several Important test/catalog
  improvements.
- Patched by removing the unused `LegacyDataXml` parser, moving safe-value
  helpers to a non-parser utility, adding node-count/depth parser-bound tests,
  strengthening duplicate-name stable-key determinism tests, adding a
  descriptor tier-ceiling test, and documenting the tier-ceiling limitation in
  `rules/rule-catalog.yml`.
- One Sonnet re-review also had reduced coverage due to denied shell access. It
  found no remaining blocking issue, but requested explicit same-name
  cross-format and tier-ceiling tests. Patched by adding a DBML-vs-EDMX
  same-name stable-key test and making the tier-ceiling test intent explicit.
  A third Kiro review was intentionally not run to respect the two-review-round
  limit; final local validation passed after the patch.

PR review-loop follow-up:

- Initial PR loop found three unresolved review threads: stale safe labels when
  hashing reused properties, hardcoded full coverage labels, and line-number
  based stable-key scopes. Patched by clearing stale safe/hash/redaction keys in
  `LegacyDataSafeValues`, passing reduced coverage labels when metadata gaps
  are known, removing line numbers from stable-key scope strings, and adding
  focused regressions for all three behaviors.

Follow-ups:

- Task 3 should add relationship semantics and ambiguity gaps while preserving
  existing `mappingKind` values.
- Task 4 should add the narrow NHibernate `.hbm.xml` MVP using the shared
  legacy data safe XML parser bounds and gap classifications.
- Task 5 should add unsupported old ORM descriptor gaps.
- Tasks 7-9 should project model-enriched `legacy-data` surfaces, exclude
  `AnalysisGap` facts from terminal projection, and verify graph/vault export
  redaction.

## Implementation Slice 3 State

Branch: `codex/continue-legacy-data-model-metadata-extraction`
Base: `origin/dev` at `c3f3967a`

Selected scope: partial Task 3. This slice adds deterministic relationship
semantics to existing DBML, EDMX, and typed DataSet relationship-shaped source
facts, plus selected ambiguity gaps, while preserving source rule IDs and
`mappingKind` values. NHibernate `.hbm.xml` parsing, unsupported old ORM
descriptor gaps, generated-code hardening, downstream surface/export
projection, exhaustive unsupported relationship-shape detection, and selector
downgrade tests remain follow-ups.

Implementation boundary:

- Added explicit model relationship properties such as
  `modelRelationshipKind = relationship`,
  `modelRelationshipRuleId = legacy.data.model.relationship.v1`,
  endpoint names or hashes, endpoint coverage, supporting metadata fact IDs,
  coverage labels, and limitation codes.
- Preserved existing DBML `association` and typed DataSet `relation`
  `mappingKind` values, and add EDMX relationship facts under the existing
  EDMX source rule.
- Added deterministic DBML association relationship evidence with source and
  target endpoints when present, and unidirectional reduced-coverage evidence
  plus limitations when the target endpoint is missing.
- Added deterministic EDMX CSDL association relationship evidence and
  unambiguous MSL `AssociationSetMapping` relationship evidence.
- Added deterministic typed DataSet `msdata:Relationship` evidence and
  resolvable XSD key/keyref constraint relationship evidence.
- Emitted model-identity ambiguity gaps for duplicate DBML relationship names,
  ambiguous EDMX association endpoints, and ambiguous MSL association-set
  endpoints.
- Emitted `UnsupportedLegacyOrmMappingShape` for inherited EDMX model shapes as
  a representative unsupported relationship/model mapping gap.
- Kept descriptor evidence capped at `Tier2Structural`; relationship metadata
  remains static design-time evidence and does not claim runtime data access,
  query execution, or schema existence.

Deferred within Task 3:

- Exhaustive split-entity, conditional, many-to-many, and other unsupported
  relationship-shape detection beyond the inherited EDMX representative gap.
- Selector downgrade behavior tests for downstream workflows.
- Closing the remaining Task 3 gap/test checkboxes after those behaviors are
  implemented.
- Rationale: this extractor slice focuses on common descriptor relationships
  and one representative unsupported model-shape gap. Split entities,
  conditional mappings, many-to-many join inference, and selector downgrade
  behavior require deeper MSL/downstream workflow integration and are better
  handled with Tasks 7-8 surface/query projection work.

Validation planned/executed:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests`: passed, 27 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModel`: passed, 3 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 554 tests.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke`: passed; emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log` with 27 facts. Relationship-specific smoke is covered by focused synthetic tests because no checked-in sample currently contains DBML/EDMX/XSD legacy data relationship fixtures.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Kiro implementation review:

- Initial Sonnet implementation review had reduced coverage due to denied shell
  access. It found incomplete full Task 3 unsupported-shape coverage, missing
  selector downgrade tests, a relationship-specific `AmbiguousEdmxMapping`
  classification, and missing relationship docs. Patched by documenting this as
  a partial Task 3 slice, keeping remaining unsupported-shape and selector
  downgrade work deferred, changing the new MSL relationship ambiguity gap to
  `AmbiguousLegacyDataModelIdentity`, and updating `docs/ACCEPTANCE.md` plus
  `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
- First Sonnet re-review had reduced coverage and requested a concrete
  `UnsupportedLegacyOrmMappingShape` representative gap or a stronger deferral.
  Patched by emitting and testing `UnsupportedLegacyOrmMappingShape` for
  inherited EDMX model shapes and documenting the remaining split/conditional/
  many-to-many follow-up boundary.
- Second Sonnet re-review had reduced coverage and no further review cycle was
  run to respect the two re-review limit. It requested clearer partial
  checkboxes, validation-state details, EDMX endpoint coverage hardening, and
  more explicit relationship ambiguity tests. Patched by adding nested
  completed/deferred Task 3 checkboxes, recording validation here, marking EDMX
  CSDL associations with missing endpoint types as reduced/unidirectional with
  limitations, and adding a duplicate-role MSL ambiguity assertion.

Documentation updated in this slice:

- `docs/ACCEPTANCE.md` documents relationship metadata fields and the inherited
  EDMX unsupported-shape gap.
- `docs/LANGUAGE_ADAPTER_CONTRACT.md` documents relationship properties on
  `LegacyDataMappingDeclared`.

Remaining follow-ups:

- Complete the remaining Task 3 unsupported-shape detection for split entities,
  conditional mappings, many-to-many join inference, and other unsupported
  relationship shapes.
- Add selector downgrade tests when Tasks 7-8 project model-enriched
  `legacy-data` surfaces into downstream path/reverse/report workflows.
- Add NHibernate `.hbm.xml` MVP and unsupported old ORM descriptor gaps in
  later slices.

PR review-loop follow-up:

- Initial PR loop for PR #234 returned `actionable_findings` with two unresolved
  review threads. Patched both:
  - Gemini found EDMX CSDL `relationshipEndpointCoverage` was tied to
    file-level reduced coverage. Fixed by separating endpoint completeness from
    model identity/file coverage and adding a reduced-file/full-endpoint
    regression test.
  - Codex found dotted XSD key/keyref constraint names were collapsed by the
    typed DataSet QName helper. Fixed by preserving dotted local names and
    adding a dotted constraint-name regression test.
- Follow-up PR loop found the prior Qodo top-level review still actionable on
  the XSD constraint path. Patched both findings:
  - Duplicate `xs:key`/`xs:unique` names with different selector tables now emit
    `AmbiguousLegacyDataModelIdentity` gaps instead of silently picking the
    first table.
  - Constraint relationship extraction now requires the XML Schema namespace for
    `key`, `unique`, `keyref`, and `selector` elements so non-XSD lookalikes do
    not become relationship facts.
- Fresh Codex review on the current head found inherited EDMX model shapes
  emitted a gap but left related descriptor/relationship facts at full coverage.
  Patched by marking inherited EDMX entities/properties and CSDL associations
  involving inherited endpoint types as reduced coverage with explicit
  limitations while preserving endpoint completeness separately.

## Implementation Slice 4 State

Branch: `codex/implement-legacy-data-model-metadata-followup`
Base: `origin/dev` at `7a8bb97c`
PR: https://github.com/joefeser/tracemap/pull/260

Selected scope: partial Task 5. This slice adds conservative unsupported old
ORM descriptor gaps for recognized LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET,
and Castle ActiveRecord signals. It does not parse those mappings, infer
entities, infer tables, infer columns, infer relationships, execute queries, or
claim runtime ORM behavior.

Implementation boundary:

- Added file-inventory recognition for public-safe legacy ORM metadata
  candidates such as LLBLGen project files and SQL map XML files with
  recognizable path or filename signals.
- Added gap-only extraction for unsupported legacy ORM metadata files,
  unsupported ORM config sections/attributes, and C# code descriptors such as
  Castle ActiveRecord attributes/usings.
- Emitted only `AnalysisGap` facts under `legacy.data.orm.unsupported.v1` with
  `Tier4Unknown`, safe descriptor family labels, reduced coverage, runtime
  proof set to false, and closed descriptor-signal tokens.
- Added focused synthetic tests proving recognized unsupported descriptors
  produce gaps, not invented legacy entity/storage/column/mapping facts, and
  proving raw SQL-like text, connection-ish values, and unsafe config content
  do not persist into Markdown or SQLite properties.

Deferred within Task 5:

- Project-local mapping DSL detection remains unchecked. It needs a separate
  deterministic taxonomy so local helper names and fluent methods do not become
  false-positive ORM descriptors.
- NHibernate `.hbm.xml` parsing remains Task 4, not part of this unsupported
  descriptor-gap slice.

Validation executed so far:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests`: passed, 28 tests. Existing NuGet audit warning for `SQLitePCLRaw.lib.e_sqlite3` was reported during restore/build output.
- `dotnet build src/dotnet/TraceMap.sln`: passed with existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 583 tests, with the same existing NU1903 advisory warnings.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke`: passed; emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log` with 27 facts.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Kiro implementation review:

- Initial Sonnet implementation review had full coverage and reported blockers
  because it evaluated the entire long-running spec as if this branch intended
  to complete Tasks 3-11. Patched by adding the Current Branch Scope section
  above and by clarifying the `legacy.data.orm.unsupported.v1` limitation for
  project-local mapping DSLs.
- Sonnet re-review had full coverage and found no blocking issues. It reported
  remaining non-blocking documentation concerns around unchecked future tasks;
  this implementation-state section records the slice boundary and validation
  explicitly.

PR review-loop follow-up:

- Initial PR loop on commit `d085fa767551dd22c1f37a32991b786549cf15c9`
  returned `merge_ready` with clean checks, clean merge state, no unresolved
  threads, no actionable bot findings, Qodo returned, and Codex satisfied by
  the configured trusted review quorum. A state-only follow-up commit records
  the PR URL and loop result.
