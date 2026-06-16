# Legacy Baseline Regression Artifacts Implementation State

Status: implemented
Branch: codex/legacy-baseline-regression-artifacts
Scope: CLI baseline create/validate/compare, redacted manifest/comparison model,
synthetic fixtures, checked-in public-safe baseline, focused tests, and workflow
documentation.
Public claim level: hidden
Readiness: ready-for-review

## Summary

Implemented a deterministic redacted baseline workflow for legacy scan outputs.
The implementation creates counts-only baseline manifests and summaries, validates
their safety classification, compares redacted manifests by static evidence count
movement, and keeps local-only outputs under ignored `.tmp/legacy-baselines/`.

A synthetic fixture and a checked-in public-safe baseline now exist so the
workflow can be validated without private legacy repositories.

## Scope Decisions

- Implemented the CLI-first workflow in `TraceMap.Cli` as
  `tracemap baseline create`, `tracemap baseline validate`, and
  `tracemap baseline compare`.
- Added `TraceMap.Reporting.LegacyBaselineArtifacts` rather than changing
  scanner extraction behavior. The scanner/reducer evidence model remains
  deterministic and no AI/LLM/vector/prompt analysis was added.
- Baseline manifests store aggregate counts, safe path classes, file extension
  counts, language counts, rule/fact/evidence/extractor/surface summaries,
  coverage snapshots, known gap categories, and limitations. They do not store
  raw facts, raw SQLite rows, analyzer logs, report prose, source snippets, raw
  SQL, config values, remotes, or local absolute paths.
- Gap messages from scan artifacts are category-sanitized before being emitted.
- Public-safe commit identity defaults to category-only unless `--public-source`
  is explicitly supplied and the commit value is a safe SHA.
- Local-only output is guarded so `--local-only` writes only under
  `.tmp/legacy-baselines/`.
- Comparison Markdown intentionally uses static evidence count wording and avoids
  impact, runtime reachability, production-usage, safety-posture, or business
  claims.
- Optional SQLite aggregate verification was not implemented in this slice
  because `facts.ndjson` contains the required count dimensions for the smallest
  complete version.
- Pinned smoke checks from `docs/VALIDATION.md` were deferred because this
  change adds a reporting/CLI workflow around existing scan artifacts and does
  not change language adapters, scanner extraction, or report rendering behavior.

## Review State

- Initial spec draft created.
- Kiro Opus first-pass review completed with reduced coverage because Kiro
  reported denied shell access, but it returned complete findings. Blocking
  issues were `.tmp/legacy-baselines/` not being git-ignored and timestamp
  determinism.
- Kiro Sonnet first-pass review completed with full coverage. Blocking issues
  were ambiguous hash input construction, underspecified secret-like handling,
  missing classifier-boundary tests, and overbroad optional SQLite reads.
- Review fixes applied:
  - `.tmp/legacy-baselines/` is ignored and implementation tasks require a
    guard for the ignored path.
  - time fields are injectable or fixture-pinned, with year-month precision for
    public-safe manifests.
  - tracked storage is fixed to `.kiro/baselines/legacy/` and local-only storage
    is fixed to `.tmp/legacy-baselines/`.
  - redaction hashing requires length-prefixed, escaped, encoded, or existing
    structured TraceMap hash input.
  - secret-like and low-entropy/enumerable private identity handling is defined.
  - SQLite reads are restricted to aggregate count queries over safe columns.
  - candidate manifests are produced by the same create workflow.
  - migration-map schema is specified.
  - proposed rule IDs use the `legacy.baseline.*` namespace and tasks require
    catalog entries before emission.
  - comparison promotion is separate from compare output and gated by validation.
  - missing tests from both reviews were added to `tasks.md`.
- Sonnet re-review completed with full coverage. It confirmed the previous
  blockers were resolved and identified final readiness issues around clarifying
  catalog-entry timing and defining the synthetic fixture required by the future
  smoke command.
- Final re-review fixes applied:
  - design clarifies `legacy.baseline.*` rule IDs are to be added during
    implementation before emission, not pre-committed by this spec branch.
  - tasks define the minimum `samples/synthetic-legacy-scan/` fixture contents
    for future implementation.
  - manifest limitations are split between scanner/coverage and safety scopes.
  - identity kind variants are documented.
  - dry-run, non-dry-run, and comparison smoke commands are separated.
  - validation tasks include an independent `baseline validate` fixture test.
- Final Sonnet re-review completed with full coverage and no blocking issues.
  It suggested non-blocking clarifications for local-only neutral-label identity
  hashes, label rejection examples, comparison schema shape, tracked synthetic
  fixtures, and migration-map wording; those were folded into requirements,
  design, and tasks.
- PR review loop found three Gemini medium comments:
  - add artifact metadata to the baseline manifest shape;
  - keep `review-needed` as an overall status/review flag rather than a movement
    label;
  - define `factTypeRenames` entries in the migration-map example.
- Gemini findings were addressed in `design.md` and `tasks.md`.
- PR review loop then found a Qodo bug around inconsistent baseline directory
  naming. `design.md` now states that `baselineId` is the canonical on-disk
  directory segment derived from label, purpose, and public-safe time metadata,
  and examples use the full baseline ID consistently.
- Spec is ready for implementation.
- Implementation added rule catalog entries for:
  - `legacy.baseline.redacted-manifest.v1`
  - `legacy.baseline.coverage-snapshot.v1`
  - `legacy.baseline.regression-comparison.v1`
  - `legacy.baseline.safety-validation.v1`
- Implementation added `samples/synthetic-legacy-scan/`, schema classification
  fixtures under `samples/legacy-baseline-fixtures/`, and a checked-in redacted
  public-safe baseline under `.kiro/baselines/legacy/`.

## Validation

Completed:

- Kiro Opus spec review.
- Kiro Sonnet spec review.
- `node scripts/kiro-review.mjs --self-test`.
- Sonnet re-review after first fixes.
- Sonnet final re-review after final readiness fixes.
- Repo spec validation command discovery. No broader non-site spec validator was
  found beyond `scripts/kiro-review.mjs --self-test`.
- `git check-ignore .tmp/legacy-baselines/example`.
- `./scripts/check-private-paths.sh`.
- `git diff --check`.
- `dotnet build src/dotnet/TraceMap.sln`.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyBaselineArtifactsTests`.
- `dotnet run --project src/dotnet/TraceMap.Cli -- baseline create --scan-output samples/synthetic-legacy-scan --label synthetic-alpha --purpose original-parser-snapshot --out .kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06 --created-at 2026-06 --public-source`.
- `dotnet run --project src/dotnet/TraceMap.Cli -- baseline validate --manifest .kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json`.
- `dotnet run --project src/dotnet/TraceMap.Cli -- baseline create --scan-output samples/synthetic-legacy-scan --label synthetic-alpha --purpose original-parser-snapshot --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 --created-at 2026-06 --dry-run`.
- `dotnet run --project src/dotnet/TraceMap.Cli -- baseline create --scan-output samples/synthetic-legacy-scan --label synthetic-alpha --purpose candidate --out .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07 --created-at 2026-07 --local-only`.
- `dotnet run --project src/dotnet/TraceMap.Cli -- baseline compare --baseline .kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json --candidate .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07/baseline-manifest.json --out .tmp/legacy-baselines/comparisons/synthetic-alpha --generated-at 2026-07`.

## Follow-Ups

- Add optional SQLite aggregate cross-checks later if facts-only summaries prove
  insufficient for legacy samples.
- Add portfolio-level baseline comparison after single-baseline comparison has
  been used on reviewed public-safe manifests.
- Consider a dedicated `baseline promote` command if manual validate plus
  `check-private-paths` becomes error-prone.
