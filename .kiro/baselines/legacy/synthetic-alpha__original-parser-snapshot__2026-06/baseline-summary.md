# Legacy Baseline Summary

- Baseline ID: `synthetic-alpha__original-parser-snapshot__2026-06`
- Schema: `legacy-baseline-manifest.v1`
- Classification: `public-safe`
- Coverage label: `Level1SemanticAnalysisReduced`
- Build status: `FailedOrPartial`
- Partial: `True`
- Facts: `5`
- Gaps: `1`

## Rule Counts
- `csharp.syntax.aspnetroute.v1`: `1`
- `csharp.syntax.declarations.v1`: `1`
- `project.file.v1`: `1`
- `repo.manifest.v1`: `2`

## Limitations
- Baseline comparison measures static evidence movement only.
- Baseline manifest stores aggregate counts only and omits raw facts, source snippets, raw SQL, config values, remotes, analyzer logs, and local paths.
- Build status is not succeeded; baseline is partial.
- Known gap category preserved: SemanticLoadFailed.
- Known gap category preserved: Truncated.
- Semantic coverage is reduced or unavailable; syntax/config fallback counts are preserved separately.
