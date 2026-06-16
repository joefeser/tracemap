# Legacy Data Metadata Extraction Implementation State

Status: implementation-mvp-ready-for-review
Branch: codex/legacy-data-metadata-extraction
Public claim level: hidden

## Why This Spec Exists

WCF metadata normalization and WebForms event flow are now implemented on `dev`.
The next missing static layer for older .NET codebases is design-time data
metadata: LINQ to SQL DBML, Entity Framework EDMX, typed DataSet/XSD,
TableAdapter metadata, config provider/connection metadata, and generated data
code linkage.

Old repositories frequently fail local project load because dependencies,
toolsets, SDKs, or Visual Studio design-time generators are unavailable. The
scanner should still preserve useful deterministic evidence from checked-in
metadata and clearly label reduced coverage when it cannot prove a link.

## Scope Decisions

- This branch is spec-only. It does not implement scanner code.
- Static checked-in metadata only; no runtime database connections, SQL
  execution, service calls, EF model loading, or config transform execution.
- DBML and EDMX are included anywhere in the repository because their extensions
  are specific data metadata formats.
- Typed DataSet `.xsd` extraction is gated by deterministic typed DataSet or
  TableAdapter indicators so unrelated schemas do not become data facts.
- TableAdapter command text uses existing SQL hash/shape conventions only when
  complete static text is visible; raw SQL is never stored.
- Config provider and connection metadata can explain names and provider
  declarations but must not reveal raw connection strings or imply runtime
  environment selection.
- Generated-code linkage can be semantic, structural, syntax/textual, or unknown;
  ambiguity produces gaps.
- New facts should support existing reducer/report surfaces without changing
  current reducer semantics.
- Public claim level stays hidden until redacted validation artifacts are
  intentionally reviewed.

## Review State

Initial spec drafted for Kiro Opus and Sonnet review. This should not be marked
ready-for-implementation until Medium+ and blocking review findings are resolved.

Review outcomes:

- Sonnet spec review completed with full coverage. Blocking findings patched:
  fact type selection rules, validation test ambiguity, parser safety test
  detail, typed DataSet `.xsd` gating, safe identifier examples, and additional
  missing tests.
- Opus spec review timed out after the 10 minute wrapper limit and produced
  reduced coverage. Partial findings patched: exact gap classifications,
  rule-to-fact/tier mapping, config extractor relationship, tier ceilings,
  determinism, extractor version naming, committed scope decisions, and PR
  slicing guidance.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  shell access after reading files. Remaining blockers patched: copy-ready rule
  catalog entries and XSD-intrinsic typed DataSet gating.
- Final Sonnet re-review completed with reduced coverage because Kiro reported
  denied shell access after reading files. No blocking or important issues
  remain. Spec is ready for implementation.
- The six `legacy.data.*` rule catalog entries remain an implementation task;
  this spec-only import does not change `rules/rule-catalog.yml`.
- PR review loop addressed Gemini's actionable note about config fact ownership:
  `legacy.data.config.v1` no longer lists `ConfigKeyDeclared` as an emitted fact;
  generic config-key evidence remains under existing config rules.
- PR review loop addressed Qodo's actionable note about typed DataSet `.xsd`
  gating: requirements now require XSD-intrinsic indicators first and treat
  `.designer.cs` or generated-code linkage as corroborating evidence only.

## Suggested PR Boundaries

- PR 1: Tasks 1-4, covering rule catalog, fact model, extractor version,
  inventory, parser safety, and safe identifier policy.
- PR 2: Task 5, covering DBML extraction.
- PR 3: Task 8, covering config provider and connection metadata.
- PR 4: Task 6, covering EDMX extraction.
- PR 5: Tasks 7 and 9, covering typed DataSet/TableAdapter extraction and
  generated-code linkage.
- PR 6: Tasks 10 and 11, covering docs, validation, compatibility, and final
  implementation validation.

## Validation Commands For Spec Delivery

```bash
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-data-metadata-extraction --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000
./scripts/check-private-paths.sh
git diff --check
```

No .NET implementation validation is required for this spec-only branch unless
review patches touch source code, docs outside the spec, or validation scripts.

## Implementation Validation

Implemented on `codex/legacy-data-metadata-extraction` as a conservative MVP
covering:

- Rule catalog entries, fact type constants, and
  `ScannerVersions.LegacyDataExtractor`.
- Safe XML helper shared by config and legacy metadata parsing. DTDs are
  prohibited, `XmlResolver` is null, line info is preserved, and size/depth/node
  bounds emit explicit gaps.
- Inventory and extraction for DBML, EDMX, typed DataSet/XSD/TableAdapter,
  relevant XML config provider/connection metadata, and `.designer.cs` linkage
  candidates.
- Static descriptor facts for DBML/EDMX/typed DataSet entities, storage
  objects, columns, mappings, routines, provider config, and generated-code
  links.
- TableAdapter complete static command text evidence as `SqlTextUsed` hash/length
  and SQL-shape `QueryPatternDetected` only; raw SQL is not stored.
- Scan report section and docs for legacy data metadata facts and limitations.
- Focused tests for DBML, EDMX, typed DataSet, config redaction, XSD gating,
  generated-code linkage, parser safety, deterministic fact IDs, and SQLite
  privacy checks.

Validation run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo <temporary-git-fixture> --out <temporary-output>
./scripts/check-private-paths.sh
git diff --check
```

Observed results:

- `dotnet build src/dotnet/TraceMap.sln`: passed.
- Focused legacy data metadata tests: passed, 9 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 270 tests.
- CLI smoke against a temporary committed Git fixture: exit code 0, emitted
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`; manifest had a concrete commit SHA; SQLite contained
  `LegacyDataColumnDeclared`, `LegacyDataEntityDeclared`,
  `LegacyDataMappingDeclared`, `LegacyDataMetadataDeclared`,
  `LegacyDataProviderConfigDeclared`, and `LegacyDataStorageObjectDeclared`;
  raw connection string values were absent from report and facts.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `python3 -m unittest scripts.tests.test_legacy_codebase_validation`: skipped
  because this MVP did not extend `scripts/legacy_codebase_validation.py`.
- Broader pinned OSS/combined smoke checks from `docs/VALIDATION.md`: deferred
  because this slice only adds single-scan fact/report rows and does not change
  combined report/path/reverse/impact/release-review command behavior. Task 10
  keeps broad compatibility tests unchecked for a follow-up.

Scope decisions and oddities:

- Generated `.designer.cs` files remain skipped by the general C# syntax and
  semantic extractors. This MVP parses them only inside
  `LegacyDataMetadataExtractor` for descriptor-scoped generated-code linkage.
  Links are `Tier2Structural` for explicit generated file matches and
  `Tier3SyntaxOrTextual` for scoped filename/type fallback. Semantic generated
  linkage remains a follow-up.
- Metadata descriptor facts are capped at `Tier2Structural`; generated-code
  linkage does not upgrade descriptor tiers.
- Config extraction now uses the shared safe XML loader. Generic config facts
  remain under `config.key.v1`; legacy data provider/connection facts are emitted
  separately under `legacy.data.config.v1`.
- DBML multiple `<Database>` descriptors use the documented
  `UnsupportedLegacyDataMetadataVersion` gap classification rather than adding a
  new MVP classification string.
- Public claim level remains hidden; no site copy or public validation artifacts
  were added.

## Follow-Ups To Keep Out Of This Slice

- Semantic generated-code linkage for generated data types without classifying
  generated code as hand-authored business logic.
- Broader compatibility tests across combine, report, paths, reverse, impact,
  release-review, and portfolio commands.
- Deeper DBML provider-extension tests, multiple-Database ambiguity tests, EDMX
  inheritance/complex/split/condition mapping tests, typed DataSet stale designer
  tests, encrypted/transform config tests, and oversized/deeply nested XML tests.
- Site copy or public site claims.
- Runtime data access proof.
- EF runtime model loading or query evaluation.
- Arbitrary ORM DSL support without a deterministic parser spec.
- Committed local sample names, private paths, raw SQL, connection strings,
  config values, remotes, snippets, or generated smoke artifacts.
