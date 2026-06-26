# Legacy Data Model ORM Mapping Completion Implementation State

Status: implementation-slice-ready-for-pr
Spec authoring branch: codex/spec-legacy-data-model-orm-mapping-completion
Implementation branch: codex/legacy-orm-mapping-slice
Target base: dev
Public claim level: hidden

## Scope

This is a spec-only branch for the remaining legacy data model extraction depth.
No product code is implemented here.

2026-06-26 implementation slice:

- Confirmed existing NHibernate, unsupported ORM, generated-link, descriptor
  projection, report/export, and rule catalog behavior already covers a large
  portion of this broad spec on `dev`.
- Added rule-catalog regression coverage for legacy data model gap/vocabulary
  ownership text before introducing any new emitted classification strings.
- Added cross-format descriptor identity coverage proving identical display
  names in DBML and NHibernate metadata remain separate descriptor identities.
- Added an index-combine-report regression proving one relationship mapping
  source fact creates exactly one terminal `legacy-data` report surface and
  retains the source metadata fact as supporting evidence.
- No extractor behavior changed in this slice; the new tests close evidence
  gaps around already-shipped implementation behavior.

The spec consolidates open follow-ups from prior legacy data metadata,
model-metadata, reporting-integration, and legacy validation specs into a single
implementation-ready slice covering:

- deterministic relationship extraction and gap behavior;
- NHibernate `.hbm.xml` descriptor completion;
- unsupported old ORM descriptor gaps;
- DBML, EDMX, typed DataSet, and TableAdapter precision gaps;
- generated-code linkage boundaries;
- safe normalized descriptors and hash-only handling;
- public-safe fixture and smoke evidence expectations.

Broader downstream combined/path/reverse/vault/RAG/static HTML behavior remains
follow-up unless an implementation slice touches those readers directly or needs
minimal compatibility safeguards.

## Source Specs Reviewed

- `legacy-data-metadata-extraction`
- `legacy-data-model-metadata-extraction`
- `legacy-data-model-reporting-integration`
- `legacy-codebase-validation`

## Current Decisions

- Reuse existing `LegacyData*` facts and `AnalysisGap` rather than introducing a
  new scan fact type.
- Keep `legacy.data.model.surface.v1` projection-only.
- Keep `legacy-data` as the canonical dependency surface kind.
- Treat unsupported old ORM descriptors as coverage gaps, not parsed surfaces.
- Keep old ORM support static and deterministic. Most descriptor evidence is
  `Tier2Structural` or `Tier3SyntaxOrTextual`; semantic linkage is separate.
- Use `legacy.data.generated-link.v1` for existing descriptor-scoped generated
  designer file/type links. Use `legacy.data.model.generated-link.v1` only when
  model-normalized linkage semantics are distinct, such as ORM mapped-class
  symbol links or model-aware gaps beyond the existing generated designer rule.
- Require safe names or hashes for descriptor labels and values.
- Do not store raw SQL, config values, connection strings, remotes, URLs, local
  absolute paths, private sample labels, source snippets, or secrets.
- Keep public claim level hidden until reviewed public-safe fixtures or redacted
  summaries exist.

## Review Plan

Initial Kiro spec review:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patch Medium+ actionable findings. Then run one bounded final re-review with
Sonnet or Opus:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model is unavailable or times out, record the exact command, status, and
artifact path here. Do not invent approval.

## Validation Plan

Spec delivery validation:

```bash
git diff --check
./scripts/check-private-paths.sh
git status --short
```

Spec delivery validation run on corrected branch
`codex/spec-legacy-data-model-orm-mapping-completion`:

- `git diff --check -- .kiro/specs/legacy-data-model-orm-mapping-completion`:
  passed with no output.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- `git status --short --branch -- .kiro/specs/legacy-data-model-orm-mapping-completion`:
  showed only the new spec folder as untracked under the corrected branch.

Implementation validation expected for future product branch:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataMetadataExtractorTests|LegacyDataModelDescriptorProjectionTests"
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo <public-safe-fixture-repo> --out <temporary-output>
./scripts/check-private-paths.sh
git diff --check
```

Implementation validation run on `codex/legacy-orm-mapping-slice`:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataModelRuleCatalogTests|CombinedDependencyReportTests"`:
  passed, 22 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataModelDescriptorProjectionTests|LegacyDataModelRuleCatalogTests|CombinedDependencyReportTests"`:
  passed, 26 tests.
- Both runs reported existing NU1903 warnings for
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

Pinned smoke checks from `docs/VALIDATION.md` must run or be explicitly
deferred when implementation touches language adapters, shared graph/report
behavior, vault/RAG export, portfolio, impact, release-review, or static HTML.

## Review Results

Initial Kiro spec reviews were run on 2026-06-24 with the repo-local wrapper:

- Sonnet command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and status 0.
  Artifacts:
  `.tmp/kiro-reviews/legacy-data-model-orm-mapping-completion/2026-06-24T030910-523Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- Opus command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with status 0 but reduced coverage because Kiro reported denied
  tool access. Artifact:
  `.tmp/kiro-reviews/legacy-data-model-orm-mapping-completion/2026-06-24T030910-414Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.

Both initial review metadata files reported a stale branch name because the
shared worktree was on another spec branch during the review process. The
worktree was switched back to
`codex/spec-legacy-data-model-orm-mapping-completion` before applying review
patches. The final re-review must be run from the corrected branch.

Patched Medium+ actionable findings:

- Reconciled premature task/state wording so readiness is not claimed before the
  review loop closes.
- Clarified descriptor tier wording so generated-code links never upgrade source
  descriptor facts.
- Added generated-link rule decision text.
- Added a gap-classification ownership table marking existing, catalog-check,
  and net-new vocabulary.
- Defined `descriptor-local scope` for DBML, EDMX, typed DataSet, NHibernate,
  and unsupported ORM gaps.
- Defined `displayClearance` as a closed presentation hint.
- Clarified NHibernate per-class cap scope and exact in-scope collection element
  names.
- Added a source-fact versus relationship-projection no-double-count boundary.
- Added project-local DSL taxonomy placeholder requirements.
- Clarified CLI smoke commit-SHA mechanics.
- Added explicit implementation tests for unknown descriptor-role non-crash
  behavior, relationship no-double-counting, cross-format stable-key separation,
  and NHibernate parser-bound/cap gaps.

Final bounded Sonnet re-review:

- Command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  was run after explicitly switching to
  `codex/spec-legacy-data-model-orm-mapping-completion`.
- Result: status 0, reduced coverage because Kiro reported denied tool access.
  Artifact:
  `.tmp/kiro-reviews/legacy-data-model-orm-mapping-completion/2026-06-24T031557-351Z-re-review-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- The re-review reported one blocker because it could not read
  `implementation-state.md`; this file is present in this spec folder and now
  records the review commands, statuses, coverage, and artifact paths.
- Patched remaining actionable review clarifications by naming catalog checks
  for `DuplicateLegacyDataModelSurface`, `AmbiguousLegacyDataModelSelector`, and
  `UnknownLegacyDataModelDescriptorRole`; requiring the relationship
  no-double-count test at the SQLite/report surface-count path; and blocking
  project-local DSL recognition unless a taxonomy is cataloged and tested.
- Fixture SHA scope decision for this spec-only branch: no implementation smoke
  fixture is created here. Future implementation should prefer a temporary
  committed fixture repository; if it scans a checked-in TraceMap sample, it
  must record that enclosing-repository SHA decision in its implementation
  state before merge.

Verification Sonnet re-review:

- Command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  was run after explicitly switching to
  `codex/spec-legacy-data-model-orm-mapping-completion`.
- Result: status 0, reduced coverage because Kiro reported denied tool access,
  but the review could read this spec folder and reported no blockers.
  Artifact:
  `.tmp/kiro-reviews/legacy-data-model-orm-mapping-completion/2026-06-24T031754-762Z-re-review-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- The review verdict was ready to merge as a spec-only PR after marking tasks
  0.6 and 0.7 complete and updating the status, now done.

## Oddities And Follow-Ups

- Existing source specs have implemented several slices after their original
  checklists were written. This spec treats unchecked items as follow-up
  candidates only when they are still relevant to extraction depth.
- Reporting/export integration remains intentionally bounded. The first
  implementation slice should not try to complete every downstream workflow
  unless that proves smaller than expected.
- Project-local ORM DSL detection remains high-risk for false positives. It
  requires a documented deterministic signal taxonomy before implementation.
