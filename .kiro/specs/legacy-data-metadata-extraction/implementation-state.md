# Legacy Data Metadata Extraction Implementation State

Status: implemented-pr-review-loop-in-progress
Branch: codex/legacy-data-metadata-extraction-9305
Public claim level: hidden

## Implemented Scope

- Added `LegacyData*` fact type constants and `ScannerVersions.LegacyDataExtractor`.
- Added `LegacyDataMetadataExtractor` for checked-in DBML, EDMX, typed DataSet XSD/TableAdapter, data-provider config, and generated designer linkage.
- Added XXE-safe XML loading with DTD prohibition, `XmlResolver = null`, document-size and node-count bounds, line info, and stable metadata hashing.
- Widened inventory for `.dbml`, `.edmx`, and repository-local `.xsd`; service-reference WCF XSDs remain classified as service-reference metadata, while unrelated loose XSDs are only generic `XsdSchema` inventory until typed DataSet indicators are present.
- DBML extraction emits static entity, storage object, column, association, routine, and mapping descriptors at `Tier2Structural`.
- EDMX extraction emits CSDL/SSDL descriptors plus simple unambiguous MSL entity/table and property/column mappings, with unsupported mapping shapes and ambiguous containers as gaps.
- Typed DataSet extraction is gated on XSD-intrinsic indicators and emits DataSet/DataTable/DataColumn/relation/TableAdapter descriptors. Complete static TableAdapter command text emits only existing SQL hash/shape evidence; raw SQL is not stored.
- Config extraction emits `LegacyDataProviderConfigDeclared` for connection names, provider names, provider factories, and EF provider sections without raw connection strings or config values. External includes, encrypted sections, and transforms are gaps.
- Generated-code linkage uses compiler-resolved `TypeDeclared` facts when scoped to generated files, otherwise falls back to deterministic generated file/type syntax. Missing or ambiguous candidates emit gaps.
- Scan report now includes a Legacy Data Metadata section and static metadata limitations.
- Rule catalog, adapter contract, validation guide, and acceptance matrix document new fact types, safe keys, limitations, and validation guidance.

## Scope Decisions And Oddities

- Descriptor facts remain capped at `Tier2Structural`; a `Tier1Semantic` generated-code link does not upgrade the metadata descriptor tier.
- The MVP does not emit `DatabaseColumnMapping` from metadata alone. Existing SQL/query/reducer rules still own runtime-adjacent conclusions.
- EDMX support is intentionally conservative: complex types, conditions, inheritance/split mappings, many-to-many mappings, duplicate containers, and provider extensions become gaps or review evidence.
- Typed DataSet `.designer.cs` presence is corroborating only after XSD-intrinsic indicators are present.
- Generic config extraction now uses the same safe XML reader as legacy data metadata extraction.
- Qodo review feedback patched after PR creation: file read failures now normalize to safe XML gaps, config and metadata parse gaps avoid raw parser text, typed DataSet XSD gating requires msdata/msprop-specific evidence, and report output preserves existing stable legacy-data hash labels.
- Public claims remain hidden until redacted validation summaries are intentionally reviewed.

## Validation

- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed.
- `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-legacy-data-sample-scan`: passed and produced all required scan artifacts.
- Focused tests added in `LegacyDataMetadataExtractorTests`.
- `python3 -m unittest scripts.tests.test_legacy_codebase_validation`: not run because the legacy validation harness was not extended.
- Pinned smoke checks from `docs/VALIDATION.md`: deferred; implementation is covered by checked-in focused fixtures and no local/private legacy smoke artifacts were introduced.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Post-review rerun: `dotnet build`, `dotnet test` (300 tests), sample scan, private-path guard, and `git diff --check` passed after the Qodo fixes.

## Follow-Ups

- Rich EDMX inheritance, complex type, conditional, split, and many-to-many mapping support.
- Additional old ORM descriptors such as NHibernate XML/Fluent or project-specific mapping DSLs.
- Generated-code freshness detection beyond deterministic static linkage.
- Public/redacted legacy data metadata validation summaries.
