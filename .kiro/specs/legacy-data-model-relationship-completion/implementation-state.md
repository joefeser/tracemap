# Legacy Data Model Relationship Completion Implementation State

Status: implementation-pr2-validated
Readiness: ready-for-pr2-delivery
Spec branch: `codex/legacy-data-model-relationship-completion`
Target base: `dev`
Public claim level: hidden

## Implementation PR 1

- Implementation branch:
  `codex/legacy-relationship-gap-classifier-pr1`.
- Base: `origin/dev` at
  `31cd8f0415fc691aede8ba3b12e201b164c599db`.
- Selected boundary: add the preferred shared deterministic relationship gap
  classifier and focused harness, then wire only the existing DBML duplicate
  association and missing-target decisions through it.
- Rationale: DBML already emits a duplicate-name `AnalysisGap` and preserves a
  missing-target association as reduced unidirectional evidence. Centralizing
  those live decisions exercises both gap and reduced-relationship outcomes
  without changing an existing fact type, `mappingKind`, endpoint policy, or
  downstream projection. EDMX, typed DataSet, and NHibernate wiring would make
  this first PR materially broader and remains deferred.
- Catalog gate: add a machine-readable closed reason-code list under
  `legacy.data.model.relationship.v1`, repair ownership text for the three
  already-emitted extractor classifications, and test catalog ownership before
  classifier-driven output is emitted.
- Product non-scope: no new XML extraction, runtime ORM or database access,
  projection/report changes, or broad downstream expansion.

## Implementation PR 2

- Implementation branch:
  `codex/typed-dataset-relationship-classifier-pr2`.
- Base: `origin/dev` at merged PR #499 commit
  `15510f5f454da7de83b3778a722b09f90a71d5c5`.
- Selected boundary: wire the shared classifier to existing typed DataSet
  `msdata:Relationship` and `xs:keyref` endpoint decisions. Preserve existing
  full and unidirectional facts, preserve ambiguous-keyref reduced facts plus
  their existing gaps, and emit a relationship-rule gap instead of a terminal
  relationship fact only when neither endpoint is deterministic.
- Add a focused TableAdapter SQL non-inference regression and typed DataSet
  relationship privacy/determinism coverage. Extend the committed public-safe
  smoke fixtures for this family.
- Deferred: composite key/keyref field matching, broader duplicate-constraint
  redesign, schema-indicator behavior outside the existing gate, EDMX,
  NHibernate, projections/reports/exports, and all runtime database behavior.

## Current Context

Authoring started from a clean worktree created from current `origin/dev`.

- `git fetch origin dev`: passed on 2026-06-27.
- Working branch:
  `codex/legacy-data-model-relationship-completion`.
- Initial base SHA confirmed with `git rev-parse HEAD origin/dev`:
  `4b5844ff07199969eacd040e9383037d0b266d49`.
- `origin/dev` was refreshed again before PR delivery:
  `6bec000244340311cc385e4ebdeee4655a7251d4`.
- Branch was rebased onto that refreshed `origin/dev` before final validation.
- Predecessor context:
  - `legacy-data-model-orm-mapping-completion` is
    `implementation-slice-1-merged-with-follow-ups`.
  - `legacy-data-model-metadata-extraction` is
    `implementation-slice-11-merged-with-follow-ups`.
  - `legacy-data-metadata-extraction` is `implemented-mvp`.

The original checkout had unrelated Swift spec edits, so this work was moved to
a separate clean worktree. Those unrelated edits were not touched.

## Scope

This is a spec-only branch for the next implementation-ready legacy data model
relationship follow-up. No product code is implemented here.

The spec intentionally does not duplicate the merged
`legacy-data-model-orm-mapping-completion` slice-1 work, which added regression
coverage for:

- rule-catalog vocabulary ownership text;
- cross-format descriptor identity separation;
- index/combine/report no-double-count behavior for one relationship source
  fact.

The new implementation focus is deterministic relationship extraction/gap
behavior for DBML, EDMX, typed DataSet, and NHibernate relationship shapes that
remain open. The preferred first PR is a small shared relationship gap
classifier/harness, optionally wired to one relationship family.

## Source Context Reviewed

Specs reviewed:

- `.kiro/specs/legacy-data-model-orm-mapping-completion/requirements.md`
- `.kiro/specs/legacy-data-model-orm-mapping-completion/design.md`
- `.kiro/specs/legacy-data-model-orm-mapping-completion/tasks.md`
- `.kiro/specs/legacy-data-model-orm-mapping-completion/implementation-state.md`
- `.kiro/specs/legacy-data-model-metadata-extraction/tasks.md`
- `.kiro/specs/legacy-data-model-metadata-extraction/implementation-state.md`
- `.kiro/specs/legacy-data-metadata-extraction/tasks.md`
- `.kiro/specs/legacy-data-metadata-extraction/implementation-state.md`

Live code/tests/catalog inspected enough to avoid stale scope:

- `src/dotnet/TraceMap.Core/LegacyDataMetadataExtractor.cs`
- `src/dotnet/TraceMap.Core/LegacyDataModelIdentity.cs`
- `src/dotnet/TraceMap.Core/LegacyDataModelDescriptorProjection.cs`
- `src/dotnet/TraceMap.Core/CombinedSurfaceProjection.cs`
- `src/dotnet/tests/TraceMap.Tests/LegacyDataMetadataExtractorTests.cs`
- `src/dotnet/tests/TraceMap.Tests/LegacyDataModelDescriptorProjectionTests.cs`
- `src/dotnet/tests/TraceMap.Tests/LegacyDataModelRuleCatalogTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyReportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedReverseQueryTests.cs`
- `rules/rule-catalog.yml`

Observed live behavior:

- DBML associations already emit `LegacyDataMappingDeclared` relationship
  semantics and duplicate-name gaps.
- EDMX CSDL/MSL relationships already emit deterministic relationship evidence
  and some ambiguity/inherited-shape gaps.
- Typed DataSet `msdata:Relationship` and `xs:keyref` constraints already emit
  relationship evidence and ambiguity/reduced endpoint behavior.
- NHibernate `.hbm.xml` already emits static XML relationship evidence for
  common relationship descriptors and unsupported-shape gaps for several
  complex descriptors.
- `legacy.data.model.surface.v1` already owns ambiguous reverse selector gaps.
- Slice-1 report no-double-count behavior is already covered and should not be
  repeated unless touched by new implementation.

## Rule Catalog Baseline

Current `rules/rule-catalog.yml` was checked during authoring. Baseline:

| Vocabulary | Current catalog state on base SHA | Implementation implication |
| --- | --- | --- |
| `AmbiguousLegacyDataModelIdentity` | Emitted in code, not literally present in catalog. | Treat as catalog repair before relationship follow-up code reuses or expands it. |
| `UnsupportedLegacyOrmMappingShape` | Emitted in code, not literally present in catalog. | Treat as catalog repair before relationship follow-up code reuses or expands it. |
| `UnsupportedLegacyOrmDescriptor` | Emitted in code, not literally present in catalog. | Treat as catalog repair before relationship follow-up code reuses or expands it. |
| `AmbiguousLegacyDataModelSelector` | Literally present under `legacy.data.model.surface.v1`. | Existing reverse selector vocabulary; not extractor-local ambiguity. |
| `DuplicateLegacyDataModelSurface` | Not present in code or catalog. Catalog describes `DuplicateIdentity` gaps with reason `duplicate-surface`. | Do not emit this new string unless cataloged first; prefer existing projection vocabulary. |

PR 1 should add a machine-readable reason-code registry or equivalent testable
catalog mechanism for any new closed relationship reason-code values before code
emits them. Prose-only substring tests are acceptable only as a temporary bridge
when no structured catalog field exists yet, and the limitation should be
recorded in the implementation state.

## Scope Decisions

- Reuse existing `LegacyDataMappingDeclared` and `AnalysisGap` fact types.
- Keep source relationship evidence under the source rule IDs:
  `legacy.data.dbml.v1`, `legacy.data.edmx.v1`,
  `legacy.data.typed-dataset.v1`, and `legacy.data.orm.nhibernate.v1`.
- Keep `legacy.data.model.relationship.v1` as relationship semantics and gap
  ownership, not as a duplicate scan-time relationship emitter.
- Require rule catalog entries or amendments before emitting any new
  relationship gap string, needs-review caveat, coverage label, or classifier.
- Keep default output privacy strict: no raw SQL, config, connection strings,
  snippets, local absolute paths, remotes, URLs, provider values, private labels,
  or secrets.
- Public claim level remains hidden. This spec must not create public site copy.
- PR 1 should be small: shared relationship gap classifier/harness, optionally
  wired to one relationship family.
- Live code currently emits unidirectional or reduced endpoint relationship
  evidence for DBML missing target type, EDMX missing endpoint type, typed
  DataSet missing/ambiguous constraint endpoint, and NHibernate missing target
  class. Future implementation should preserve those existing family policies
  rather than inventing new unidirectional behavior elsewhere without cataloged
  limitations.

## Review Plan

Initial Kiro spec review commands requested by the task:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patch Medium+ actionable findings. Patch Low findings only when narrow and safe.
Run one bounded re-review if feasible and record it here.

## Review Results

One authoring oddity occurred before the clean worktree copy was corrected:

- Command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  initially failed from the clean worktree because the newly drafted files had
  been written to the original checkout. Error:
  `Missing expected spec files: requirements.md, design.md, tasks.md`.
  The drafted folder was copied into the clean worktree and removed from the
  original checkout; unrelated original-checkout edits were not touched.

Initial Kiro spec reviews:

- Opus command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with status 0 and full coverage.
  Artifacts:
  `.tmp/kiro-reviews/legacy-data-model-relationship-completion/2026-06-27T164847-839Z-spec-claude-opus-4.8.clean.md`
  and matching prompt/raw/meta files.
  Findings: two blocking issues around misstated catalog ownership and missing
  classifier reason-code ownership, plus important determinism, test mechanism,
  optionality, and fixture guidance.
- Sonnet command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with status 0 and full coverage.
  Artifacts:
  `.tmp/kiro-reviews/legacy-data-model-relationship-completion/2026-06-27T165154-943Z-spec-claude-sonnet-4.6.clean.md`
  and matching prompt/raw/meta files.
  Findings: two blocking issues around implementation-state/review-material
  baseline and rule catalog baseline, plus non-blocking classifier schema,
  missing-endpoint decision, PR 1 alternate, projection vocabulary, validation,
  and missing-test guidance.

Patched Medium+ actionable findings:

- Added the rule catalog baseline table above, including emitted-but-not-
  literally-cataloged gap strings and the actual projection duplicate
  vocabulary.
- Corrected the design vocabulary table to require catalog repair before reuse
  of emitted-but-undocumented strings.
- Added a closed recommended `safeReasonCode` set owned by
  `legacy.data.model.relationship.v1` before emission.
- Promoted classifier input/output fields to normative schema tables.
- Split missing-endpoint decision behavior by whether the family already
  supports unidirectional evidence.
- Added precedence ordering and overlapping-condition determinism test
  expectations.
- Reconciled the shared classifier as the preferred PR 1 path while allowing a
  one-family alternate only with recorded rationale and a PR 2 follow-up.
- Added committed smoke-fixture expectations and docs/VALIDATION guidance for
  legacy data relationship extractor changes.

Bounded Sonnet re-review:

- Command:
  `node scripts/kiro-review.mjs --phase legacy-data-model-relationship-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with status 0 and reduced coverage because Kiro reported denied
  tool access for a shell command.
  Artifacts:
  `.tmp/kiro-reviews/legacy-data-model-relationship-completion/2026-06-27T165454-762Z-re-review-claude-sonnet-4.6.clean.md`
  and matching prompt/raw/meta files.
  Findings: initial blockers were resolved; no new blocking spec-content issues
  were introduced. Patched follow-up suggestions by removing the local worktree
  path from this state file, recording current unidirectional family behavior,
  and clarifying the `not-in-scope` test expectation.

## Validation Plan

Spec delivery validation:

```bash
git diff --check
./scripts/check-private-paths.sh
git diff --name-only origin/dev...HEAD
git status --short --branch
```

Expected diff scope: only files under
`.kiro/specs/legacy-data-model-relationship-completion/`.

Implementation validation expected for future product branch:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataMetadataExtractorTests|LegacyDataModelRuleCatalogTests"
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo <synthetic-committed-fixture-repo> --out <temporary-output>
./scripts/check-private-paths.sh
git diff --check
```

Future implementation must run or explicitly defer relevant pinned smoke checks
from `docs/VALIDATION.md` when shared graph/report/export behavior changes.

## Validation Results

Spec delivery validation after rebasing onto refreshed `origin/dev`:

- `git diff --check origin/dev...HEAD`: passed with no output.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- `git diff --name-only origin/dev...HEAD`: only listed files under
  `.kiro/specs/legacy-data-model-relationship-completion/`.
- `git status --short --branch`: branch ahead of `origin/dev` by one commit,
  no unstaged or untracked files.

## PR And ACK State

- PR opened: https://github.com/joefeser/tracemap/pull/398
- Initial ACK command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 398 --base dev --require-codex-review --quiet --json`
- Initial ACK result: `not_merge_ready` with stop reason
  `MERGE_STATE_NOT_CLEAN`.
- ACK evidence included head SHA
  `630d8e00444a19f737ac35994a92c4f5fa264ca8`, two unresolved review threads,
  and `humanNextAction: patch_actionable_findings`.
- ACK-authorized patch pass:
  - Added the relative path segment-check acceptance criterion requested by the
    Gemini review thread.
  - Updated `tasks.md` current state and spec authoring checklist so the PR-open
    and initial ACK state are no longer stale.
- Final state: PR #398 merged into `dev` as
  `3d52856e7999014e67972e4b5def48fccdae5255` from exact reviewed head
  `e10241a6cbee0f35b1f6fd0e81e33909324026c6`. This merged the reviewed spec;
  it did not implement the first product slice.

## Implementation PR 1 Validation

Implemented scope:

- Added a pure closed-state relationship classifier covering deterministic,
  reduced, gap, and not-in-scope outcomes with explicit precedence.
- Added machine-readable `gapClassifications`, `safeReasonCodes`, coverage,
  endpoint-coverage, limitation-code, and safe-property registries under
  `legacy.data.model.relationship.v1`.
- Repaired catalog ownership text for
  `AmbiguousLegacyDataModelIdentity`, `UnsupportedLegacyOrmMappingShape`, and
  `UnsupportedLegacyOrmDescriptor` under their emitting source rules.
- Wired DBML duplicate association gaps and the existing missing-target
  unidirectional policy through the shared classifier. Existing fact types,
  source rule IDs, `mappingKind`, deterministic relationship properties, and
  family policy remain unchanged.
- Added a public-safe committed DBML sample plus focused classifier, catalog,
  extractor, determinism, no-invented-endpoint, non-claim, and default-artifact
  privacy tests.

Validation results:

- Focused extractor/classifier/catalog filter: 58 passed, 0 failed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 errors and
  the existing 8 `NU1903` SQLite advisories.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build`: 822 passed,
  0 failed.
- CLI smoke copied the committed synthetic sample into a temporary Git
  repository, committed it as
  `58e408f420b4778e813359527292d26a744bf8b7`, and scanned that exact commit.
  The scan emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`; analysis was
  `Level3SyntaxAnalysis`, build status was `NotRun` because the fixture is
  metadata-only, deterministic relationship coverage included `full` and
  `reduced`, and the expected `AmbiguousLegacyDataModelIdentity` gap carried
  `safeReasonCode=duplicate-relationship-identity`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- The pinned legacy-data metadata guidance in `docs/VALIDATION.md` was run.
  Broader model-surface projection/report/query/export filters were not
  applicable because this slice does not change those paths.

Deferred after PR 1:

- EDMX, typed DataSet, and NHibernate classifier wiring.
- Remaining DBML missing/ambiguous keys, duplicate scopes, provider-extension,
  and unsafe-identity decision integration beyond existing hash behavior.
- Broad downstream projection/report/export work and runtime ORM/database
  validation.

Initial implementation ACK on PR #499 returned `actionable_findings` at exact
head `b0c3f08820f54e0121146fea52d88900ea7e135c`:

- Qodo correctly found that the DBML adapter used only the classifier decision
  and ignored its endpoint-coverage and limitation outputs. The patch now
  applies classifier endpoint coverage and deterministically maps classifier
  limitations to the existing DBML compatibility vocabulary, including a
  focused missing-source-endpoint regression.
- Codex correctly found that the closed `limitationCodes` catalog registry
  omitted limitation values already emitted by DBML, EDMX, typed DataSet, and
  NHibernate relationship facts. The registry and exact-list test now include
  those existing values plus the classifier values.
- Post-patch focused validation remained 58 passed; the full solution remained
  822 passed; private-path and diff checks passed.
- Final ACK returned `merge_ready` for exact head
  `03c9e33a6cf7db19419a6c5002af342ce4d911d1`; the ACK executor merged PR #499
  to `dev` as `15510f5f454da7de83b3778a722b09f90a71d5c5`.

## Implementation PR 2 Validation

Implemented scope:

- Routed existing typed DataSet `msdata:Relationship` and `xs:keyref` endpoint
  decisions through the shared relationship classifier.
- Preserved deterministic full facts, existing reduced unidirectional facts,
  `mappingKind=relation`, source rule IDs, ambiguous-keyref reduced facts, and
  the existing ambiguous constraint gaps.
- Descriptors with neither endpoint now emit cataloged
  `IncompleteLegacyDataModelRelationship` gaps under
  `legacy.data.model.relationship.v1` and do not emit invented terminal
  relationship facts.
- Added focused missing-parent, missing-both, ambiguous-keyref, deterministic
  fact-order/ID, unsafe-name default-artifact privacy, and TableAdapter SQL
  non-inference regressions.
- Added a committed public-safe typed DataSet relationship smoke fixture.

Validation results:

- Focused extractor/classifier/catalog filter: 59 passed, 0 failed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 errors and
  the existing 8 `NU1903` SQLite advisories.
- `dotnet test src/dotnet/TraceMap.sln --no-restore --no-build`: 823 passed,
  0 failed.
- CLI smoke copied the committed sample into a temporary Git repository,
  committed it as `301e6fa6092c5a387388181174b7d079f34e847d`, and scanned that exact commit.
  The scan emitted all five required artifacts with
  `Level3SyntaxAnalysis`, metadata-only `NotRun` build status, full and reduced
  endpoint coverage, and two `IncompleteLegacyDataModelRelationship` gaps
  carrying `safeReasonCode=missing-endpoint` for the empty relation and keyref
  descriptors.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- The pinned legacy-data metadata guidance in `docs/VALIDATION.md` was run;
  broader surface/report/query/export filters remain not applicable because
  those code paths are unchanged.

Review follow-up on PR #500:

- Qodo identified that multiple relationship descriptors on the same XML line
  could produce duplicate analysis-gap fact IDs. Relationship gaps now include
  the descriptor's deterministic document-node ordinal in their evidence seed
  and public-safe properties. A minified-XSD regression proves four same-line
  typed DataSet gaps retain distinct IDs and write successfully to SQLite.
- Gemini requested consistent endpoint-coverage fallback behavior between the
  two typed DataSet relationship paths. The `msdata:Relationship` path now uses
  the same endpoint-aware fallback as `xs:keyref`.
- Post-patch focused validation passed 3 tests; the build passed with 0 errors
  and the existing 8 `NU1903` advisories; the full solution passed 824 tests.

Deferred after PR 2:

- Composite key/keyref field-count and field-identity matching.
- Broader duplicate constraint and ambiguous selector classification beyond the
  existing deterministic resolver.
- Schema-indicator behavior outside the existing typed DataSet gate.
- EDMX, NHibernate, broad downstream expansion, and runtime database behavior.

## Oddities And Follow-Ups

- The predecessor ORM mapping completion spec is intentionally broad; this
  follow-up narrows only relationship gap behavior and should not reopen every
  unchecked NHibernate or downstream task.
- Some relationship gap behavior already exists in live code. Future
  implementation should add missing cases surgically rather than changing
  deterministic relationship facts.
- Project-local ORM DSL detection remains out of scope until a deterministic
  signal taxonomy exists.
- Broad downstream selector/report/export expansion remains deferred unless a
  specific implementation slice touches those workflows.
