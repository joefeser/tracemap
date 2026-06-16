# Legacy Baseline Summary

Baseline ID: `synthetic-alpha__original-parser-snapshot__2026-06`
Schema: `legacy-baseline-manifest.v2`
Classification: `public-safe`
Coverage: `Level1SemanticAnalysisReduced`
Build status: `FailedOrPartial`
Scan status: `completed-partial`
Facts: `4`
Gaps: `1`

## Evidence Tiers

- `Tier1Semantic`: `1`
- `Tier2Structural`: `2`
- `Tier4Unknown`: `1`

## Surfaces

- `build-environment`: `not-observed`
- `config`: `not-in-scope`
- `csharp`: `observed`
- `http`: `not-in-scope`
- `other`: `not-in-scope`
- `packages`: `observed`
- `sql-data`: `not-in-scope`
- `ui-events`: `observed`
- `wcf-service-reference`: `not-in-scope`

## Limitations

- Baseline manifests preserve aggregate static evidence counts only and do not store raw scan artifacts.
- Build or project load did not fully succeed; baseline coverage is partial.
- Comparisons describe count and coverage movement only; they do not prove runtime behavior.
- Known gap preserved as category `LegacyProjectLoadFailed`.
- Known gap preserved as category `SyntaxFallbackUsed`.
- Reduced or unknown evidence tiers are preserved separately from semantic evidence.

Raw scan artifacts are not copied into this summary.
