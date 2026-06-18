# Site TraceMap Tools Proof Source Catalog Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Branch

Spec branch: `codex/spec-site-proof-source-catalog`

Base: `origin/dev`

## Scope

This delegated worker phase creates only the Kiro spec for
`site-tracemap-tools-proof-source-catalog`. It does not implement site code,
edit site source, edit generated site output, or mark implementation tasks
complete.

The future site phase should define a public-safe proof source catalog that maps
existing public routes and claim labels to allowed proof sources: route, claim
level, proof path, source artifact or source document, rule ID or rule family
where available, evidence tier or coverage label where available, limitations,
and non-claims.

## Public Claim Level Decision

Selected page-level public claim level: `demo`.

Reasoning: the catalog is intended to index current checked-in public site
metadata, public-safe demo summary rows, public routes, repository docs, and
rule catalog references. It is not a new source of truth and does not claim
runtime behavior or production behavior. Row-level claim fields still use
`shipped`, `demo`, `concept`, or `hidden`, and the page-level `demo` label must
not upgrade concept or hidden rows.

## Scope Boundaries

Safe to specify:

- A public-safe index/orientation layer over existing routes and source
  material.
- Required row fields for route, claim label, `Public claim level`, proof path,
  source artifact or source document, rule ID or rule family, evidence tier or
  coverage label, limitation, and non-claims.
- Mapping from existing site vocabulary such as `main`, `demo`, `concept`,
  `future`, `dev`, and `hidden` into the required catalog claim levels.
- Mapping from evidence source vocabulary such as `FullEvidenceAvailable`,
  `PartialAnalysis`, `not_requested`, `unavailable`, and TraceMap evidence tiers
  into bounded evidence-status labels.

Not safe to specify as public claims:

- Runtime behavior, production traffic, endpoint performance, outage cause,
  release safety, operational safety, AI impact analysis, LLM analysis, or
  complete product coverage.
- Publication of raw facts, raw SQLite indexes, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw remotes,
  generated scan directories, private sample names, or hidden private-work
  details.

## Spec Review Commands

Planned:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  passed with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062036-979Z-spec-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062036-979Z-spec-claude-opus-4.8.meta.json`.
  Findings: three Medium spec tightenings. Patched the catalog relationship to
  `site-tracemap-tools-claim-ledger`, added hidden-row aggregate validation,
  and added validation for the complete required row field set with non-empty
  limitations and non-claims.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062352-158Z-spec-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062352-158Z-spec-claude-sonnet-4.6.meta.json`.
  Findings: one High and three Medium spec issues. Patched by adding
  `design.md`, clarifying shipped rows on a demo-labeled page, making
  `not-yet-backed` a pre-publication blocker, and specifying the hidden
  aggregate placeholder format.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062551-944Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062551-944Z-re-review-claude-sonnet-4.6.meta.json`.
  Findings: four Medium clarity gaps. Patched task wording so
  `not-yet-backed` blocks publishing, clarified in tasks that page-level `demo`
  is not a ceiling on row-level claim status, added the catalog's
  route-to-source distinction to `design.md`, and added a forbidden public
  wording pattern list for future validation.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062722-139Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062722-139Z-re-review-claude-sonnet-4.6.meta.json`.
  Findings: four Medium spec clarity issues. Patched the page-level `demo`
  rationale, strengthened `not-yet-backed` schema language, added validation for
  catalog rows that duplicate `/proof-paths/` evidence trails, and expanded the
  hidden aggregate placeholder to list every required field.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062900-050Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T062900-050Z-re-review-claude-sonnet-4.6.meta.json`.
  Findings: no High or Medium issues. Patched the three Low validation polish
  items by adding proof-path sentinels, hidden count/cadence/sequencing
  forbidden patterns, and row-anchor format validation.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063056-517Z-re-review-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063056-517Z-re-review-claude-opus-4.8.meta.json`.
  Findings: one High and two Medium consistency issues. Patched forbidden
  wording validation so it applies to affirmative claim contexts and exempts
  negated limitation/non-claims text, aligned proof-path sentinel rules between
  requirements and design, and clarified that `/claims/` is linked only after
  the claim-ledger route ships.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063409-430Z-re-review-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063409-430Z-re-review-claude-opus-4.8.meta.json`.
  Findings: four Medium consistency issues despite exit 0. Patched anchor
  derivation with a reserved hidden-anchor exception, moved hidden placeholder
  negated wording out of `allowedPublicWording`, required live status-token
  enumeration for `/capabilities/`, `/roadmap/`, and `/proof-paths/`, and tied
  private-name validation to `scripts/check-private-paths.sh`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced review coverage because Kiro reported denied tool
  access. Saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063736-668Z-re-review-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T063736-668Z-re-review-claude-opus-4.8.meta.json`.
  Findings: two Medium consistency gaps. Patched the hidden placeholder
  `proofPath` to use the bare `hidden` sentinel and added a
  claim-level/evidence-status allowed-combination matrix for future validation.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T064114-110Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T064114-110Z-re-review-claude-sonnet-4.6.meta.json`.
  Findings: three Medium precision gaps. Patched the Final Validation Gate
  deferral note, route-anchor stripping rules, and the meaning of a
  `shipped` row backed only by demo-grade evidence.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-source-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full review coverage and saved artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T064249-762Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-source-catalog/2026-06-18T064249-762Z-re-review-claude-sonnet-4.6.meta.json`.
  Findings: no Medium or High issues; five Low refinements. Patched the manual
  unchecked-task note, per-field word-count requirement text, anchor slug edge
  cases, deferred word-count follow-up, and drive-letter pattern guidance.

## Validation

Run before PR:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- `rg -n "^- \\[x\\]" .kiro/specs/site-tracemap-tools-proof-source-catalog/tasks.md`
  returned no checked implementation tasks.

Implementation validation such as `npm test`, `npm run validate`,
`npm run build`, and browser sanity checks is deferred to the future site
implementation phase because this branch intentionally changes only spec files.
The Final Validation Gate tasks in `tasks.md` are correctly listed as unchecked;
they are future implementation tasks and will not be checkable on this
spec-only branch.

## Oddities

- The primary checkout contained unrelated modified files, so this spec was
  created in a dedicated worktree from `origin/dev`.
- First attempted Kiro review before the files were moved into the dedicated
  worktree failed with:
  `Error: Missing expected spec files: .kiro/specs/site-tracemap-tools-proof-source-catalog/requirements.md, .kiro/specs/site-tracemap-tools-proof-source-catalog/design.md, .kiro/specs/site-tracemap-tools-proof-source-catalog/tasks.md`.
  The files were then moved into the worktree. This transient issue is
  resolved; the spec now includes `design.md`.
- This spec intentionally uses catalog `shipped` as the public row label for
  existing source vocabulary that says `main`; `main` remains source vocabulary,
  not the required public claim-level enum.

## Follow-Ups

- Patch Medium or higher findings from Kiro spec review before setting
  readiness to `ready-for-implementation`.
- Keep implementation tasks unchecked until a future site implementation branch
  changes site code.
- Future implementation must verify `site/package.json` includes the expected
  `validate` script before checking validation tasks complete; if missing, add
  it with the catalog-specific checks from Requirement 7.
- Word-count bound per row field, including `limitation` and
  `allowedPublicWording`, is not set on this spec-only branch. The future
  implementation phase must choose and record it in this file before the
  word-count validation task can be marked complete.
