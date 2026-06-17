# Legacy Baseline Regression Artifacts Implementation State

Status: implemented
Branch: codex/legacy-baseline-regression-artifacts-2
Scope: CLI baseline creation, validation, comparison, fixtures, docs, tests, and rule catalog
Public claim level: hidden

## Summary

Status normalized during spec-state cleanup. The baseline CLI workflow,
fixtures, docs, rule IDs, tests, and validation notes are present on `dev`;
portfolio comparisons and optional drilldowns remain follow-up scope.

Implemented a deterministic `tracemap baseline` workflow for redacted legacy
baseline manifests and regression comparisons. The implementation reads existing
TraceMap scan outputs from checked-in synthetic fixtures or ignored local
storage, emits counts-only manifests, validates safety categories, and compares
static evidence count movement without reducer or runtime claims.

## Scope Decisions

- Implemented the CLI shape directly in `TraceMap.Cli`: `baseline create`,
  `baseline validate`, and `baseline compare`. No script fallback was needed.
- Put the baseline model, validator, creation logic, Markdown rendering,
  migration-map support, and comparison logic in `TraceMap.Reporting`.
- Public-safe manifests stay counts-only. Raw facts, SQLite rows, paths,
  snippets, SQL text, config values, remotes, analyzer logs, and private sample
  identities are not copied into manifests or summaries.
- Local-only manifests are supported only under ignored `.tmp/legacy-baselines/`.
  Optional path-hash drilldowns remain deferred.
- Public source identity hashing requires an explicit `--public-source` flag and
  uses context-separated SHA-256 input. Secret-like identity material is omitted
  category-only instead of hashed.
- Comparison output stays local-only under `.tmp/legacy-baselines/` until a
  manual promotion runs `baseline validate`, redaction validation, and
  `scripts/check-private-paths.sh`.
- No scanner extraction rules changed. No LLM, embedding, vector, prompt-based,
  vulnerability, registry, or runtime analysis was added.

## Implemented Artifacts

- `src/dotnet/TraceMap.Reporting/LegacyBaselineArtifacts.cs`
- `tracemap baseline create|validate|compare`
- `samples/synthetic-legacy-scan/`
- `.kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06/`
- focused tests in `LegacyBaselineArtifactsTests`
- baseline workflow docs in `docs/VALIDATION.md`
- baseline rule IDs in `rules/rule-catalog.yml`

## Validation

Completed:

- `dotnet build src/dotnet/TraceMap.sln` passed with 0 warnings and 0 errors.
- `dotnet test src/dotnet/TraceMap.sln` passed with 336 tests after PR
  review-loop fixes.
- `dotnet test src/dotnet/TraceMap.sln --filter LegacyBaselineArtifactsTests`
  passed with 24 tests during focused validation.
- Generated checked-in synthetic public-safe fixture with pinned `2026-06`
  metadata.
- Baseline dry-run smoke against `samples/synthetic-legacy-scan` reported
  `public-safe` and wrote no output directory.
- Baseline local create smoke wrote `baseline-manifest.json` and
  `baseline-summary.md` under ignored `.tmp/legacy-baselines/`.
- Baseline candidate create plus compare smoke wrote `comparison.json` and
  `comparison.md` under ignored `.tmp/legacy-baselines/comparisons/`.
- `tracemap baseline validate` passed against the local smoke manifest.
- `git check-ignore .tmp/legacy-baselines/example` passed.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- PR review-loop fixes addressed Qodo findings by including rule IDs and paths
  in CLI diagnostics, rejecting comparison output outside ignored
  `.tmp/legacy-baselines/` before writing files, and matching rule catalog IDs
  with exact line-based lookup instead of substring search.
- Follow-up PR review-loop fixes addressed Codex connector findings by treating
  unsafe compare validation diagnostics as fatal before writes, adding review
  entries for extractor version/category movement, and preserving missing
  `facts.ndjson` as a partial scan known gap instead of clean zero facts.

Pinned smoke rationale:

- This change adds a new baseline CLI/reporting workflow and does not change
  scanner extractors, language adapters, combine/report/path/reverse/diff,
  reducer, or generated site behavior. The relevant pinned smoke is the new
  baseline workflow documented in `docs/VALIDATION.md`.

## Oddities

- The preferred branch name was already checked out in a separate site worktree,
  so this work uses
  `codex/legacy-baseline-regression-artifacts-2` and leaves that worktree alone.
- `origin/dev` already contained the legacy flow composition work, so this
  branch includes it only as landed base code.
- The initial relative path handling worked from repo root but not from
  `dotnet test` output directories. Baseline create, validate, and compare now
  resolve relative paths from the repository root when a `.git` root can be
  found.
- A validator pattern originally treated normal JSON braces as source-like; it
  was narrowed to source-keyword patterns. A local-only limitation sentence was
  also reworded to avoid tripping that same category.

## Follow-Ups

- Add portfolio-level comparisons across multiple baseline IDs after the
  single-baseline workflow is reviewed.
- Add optional local-only path-hash drilldowns only if count-only comparison is
  insufficient for private validation.
- Add a dedicated `baseline promote` command only if manual validation plus
  private-path guard proves error-prone.
