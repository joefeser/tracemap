# Legacy Data Model Relationship Completion Implementation State

Status: spec-delivery-ready
Spec branch: `codex/legacy-data-model-relationship-completion`
Target base: `dev`
Public claim level: hidden

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
