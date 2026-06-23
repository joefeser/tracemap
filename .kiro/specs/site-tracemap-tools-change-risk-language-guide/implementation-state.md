# Site TraceMap Tools Change-Risk Language Guide Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-change-risk-language-guide`
Implementation branch: `codex/impl-site-change-risk-language-guide`
Base: `origin/dev`
Target PR base: `dev`

## Scope

This implementation adds the public change-risk language guide to the static
site. The work is limited to site source, site validation/test coverage, and
this spec packet's bookkeeping. Generated site output remains uncommitted.

## Claim Boundary

The future guide is concept-level wording guidance. It helps reviewers,
managers, engineers, architects, and implementation agents choose bounded
phrasing for deterministic static evidence around a change.

The guide must not claim that TraceMap proves impact, absence of impact,
release approval, release safety, operational safety, runtime behavior,
production traffic, endpoint performance, complete coverage, AI/LLM analysis,
or replacement of human judgment.

The guide must not publish raw facts, raw SQLite indexes, analyzer logs, raw
source snippets, raw SQL, config values, secrets, local absolute paths, raw
remotes, generated scan directories, private sample names, command output,
hidden validation details, or credential-like values.

## Placement Decision

Selected placement: `/language/change-risk/`.

Reasons:

- The required eight sections and six tables fit the standalone word-count
  bound and would make a folded section too dense.
- A standalone page supports canonical metadata, sitemap metadata, discovery
  metadata, route-specific validation, and direct links from adjacent surfaces.
- The page is concept-level wording discipline, so it can stay separate from
  checklist completion, objection handling, release boundary, runtime boundary,
  proof-path FAQ, and manager Q&A surfaces.

Rejected alternatives:

- `/review-claim-checklist/language/`: close to the claim checklist, but the
  page is broader reusable wording guidance for reviewers, managers,
  engineers, architects, and implementation agents.
- Section on `/review-claim-checklist/`: rejected because the required tables
  and non-claim examples would bloat the checklist route and blur checklist
  completion with language selection.
- Section on `/questions/objections/`: rejected because it would make the guide
  look like objection handling instead of bounded phrasing discipline.

Route existence check: all named adjacent routes exist:
`/review-claim-checklist/`, `/questions/objections/`,
`/release-review-boundary/`, `/static-vs-runtime/`, `/proof-paths/faq/`, and
`/manager-faq/`.

Navigation decision: the guide is not added to primary navigation. Discovery is
through sitemap/discovery metadata, hero/related links on the new page, and
targeted inbound links from `/review-claim-checklist/`,
`/questions/objections/`, and `/manager-faq/`.

## Adjacent Surface Boundary

The future guide must distinguish itself from these adjacent public surfaces:

- `/review-claim-checklist/`: claim support checklist, not the wording guide.
- `/questions/objections/`: stakeholder objection handling, not the wording
  guide.
- `/release-review-boundary/`: release and approval boundary, not change-risk
  language approval.
- `/static-vs-runtime/`: static versus runtime explanation, not phrase tables.
- `/proof-paths/faq/`: proof-path Q&A, not wording discipline.
- `/manager-faq/`: management Q&A, not reusable phrase selection.

## Required Future Content

Required sections:

- Why wording matters.
- Safe static-evidence phrases.
- Unsafe phrases.
- Evidence-required wording.
- Reduced-coverage wording.
- Owner-handoff wording.
- Stop conditions.
- Non-claims.

Required tables:

- Safe phrasing.
- Unsafe/blocked phrasing.
- When to use `needs review`.
- When to say `evidence shows`.
- When to say `coverage is reduced`.
- When to stop.

Visible required phrases:

- `Public claim level: concept`
- `No public conclusion without evidence`

Implemented route content:

- Required sections use stable anchors:
  `#why-wording-matters`, `#safe-static-evidence-phrases`,
  `#unsafe-phrases`, `#evidence-required-wording`,
  `#reduced-coverage-wording`, `#owner-handoff-wording`,
  `#stop-conditions`, and `#non-claims`.
- Required tables use machine-readable `data-language-table` markers:
  `safe-phrasing`, `unsafe-blocked-phrasing`, `evidence-shows`,
  `needs-review`, `coverage-reduced`, and `when-to-stop`.
- Unsafe examples are wrapped with `data-blocked-phrase` so validators can
  distinguish blocked teaching examples from affirmative product claims.

## Validation Expectations

Future implementation should validate:

- Required visible phrases, sections, anchors, and tables.
- Required adjacent links and link resolution.
- Standalone metadata, discovery metadata, sitemap metadata, canonical URL, and
  Open Graph fields when a standalone route is selected.
- Stable section anchors and discoverable inbound links when a folded
  placement is selected.
- Forbidden claims for impact proof, absence-of-impact proof, release
  approval/safety, operational safety, runtime proof, production traffic,
  endpoint performance, complete coverage, AI/LLM analysis, and replacement of
  human judgment.
- Forbidden private/raw material and credential-like values.
- Word-count bounds: 1000 to 2400 rendered words for a standalone page, or 650
  to 1600 rendered words for a folded section. `Rendered words` means
  whitespace-delimited tokens in the guide's main visible content region after
  HTML rendering, including section prose and table cell text, and excluding
  site chrome, global navigation, footer text, metadata, code or attribute
  values, and machine-only wrapper markup used to tag blocked examples.
- Desktop and mobile browser sanity for readable tables, visible claim-level
  and principle text, and no horizontal overflow.

Expected validation commands for future implementation:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- `git diff --check`
- `./scripts/check-private-paths.sh`

Expected metadata files for a standalone route:

- Sitemap/page metadata: `site/src/_site/pages.json`
- Discovery metadata: `site/src/_site/discovery.json`

Initial metadata recommendation: use an existing discovery `hintCategory`
aligned with evidence or review guidance. `evidence` is the default candidate
because the page teaches language for deterministic static evidence; future
implementation must verify the current allowed values before editing
discovery metadata. Use the exact `nonClaims` field name for discovery
non-claim entries.

## Validation Results

Validation completed before commit:

- `npm test` from `site/`: passed.
- `npm run validate` from `site/`: passed.
- `npm run build` from `site/`: passed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

Focused validator coverage added for:

- Required visible text, required section anchors, and required tables.
- Required adjacent links, inbound links, sitemap entry, route metadata, and
  discovery metadata fields.
- Forbidden affirmative public claims outside sanctioned teaching/non-claim
  contexts.
- Private or credential-like material.
- Required marked blocked phrases and rendered word-count bounds.

Browser sanity:

- Desktop viewport check passed: visible claim-level/principle text, six
  language tables, required links, and no document-level horizontal overflow.
- Mobile viewport check passed: visible claim-level/principle text, six
  language tables inside scrollable wrappers, and no document-level horizontal
  overflow.

## PR Loop Status

Ready PR opened against `dev`.

PR-loop history:

- Initial run stopped with `checks_failed` / `CHECKS_FAILED` because the
  private path guard failed on a synthetic local-path fixture in the focused
  language-guide test. Patched by building the synthetic path at runtime.
- Next run stopped with `actionable_findings` /
  `UNRESOLVED_REVIEW_THREADS` for a Gemini review-thread finding about
  repeated attribute-regex compilation in the validator. Patched by caching
  the regexes at module scope, then resolved the thread.
- Next run stopped with `actionable_findings` /
  `ACTIONABLE_BOT_FINDINGS` for Qodo findings about the forbidden-claim
  validator's sanctioned-section blind spot and the non-anchor-scoped href
  check. Patched by stripping only marked blocked phrases before claim checks,
  adding negative tests for overclaims inside sanctioned sections, and making
  the href matcher anchor-scoped.
- Implementation-content PR-loop run returned `merge_ready` with stop reason
  `NONE`.
- After the docs-only state note is pushed, the final handoff records the
  exact latest PR-loop decision for the then-current head. The expected state
  remains `merge_ready` / `NONE` unless a new check, review, or merge-state
  event appears after this note.

Final PR-loop state:

- Merge state: clean.
- Unresolved review threads: 0.
- Pending checks: 0.
- Failed checks: 0.
- Actionable bot findings: 0.
- Required-review quorum: satisfied by Qodo return; Codex is recorded as
  residual medium risk by policy because it did not return.

Recommended human action: Joe can merge the current head if he accepts the
configured policy evidence and residual reviewer-risk posture.

## Review Status

- `claude-opus-4.8` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-change-risk-language-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-change-risk-language-guide/2026-06-22T224331-410Z-spec-claude-opus-4.8.clean.md`.
- Exact denied-tool messages reported by Kiro: `Command execute_bash is
  rejected because it matches one or more rules on the denied list:
  non-interactive mode (no user to approve)` and `Command fs_write is rejected
  because it matches one or more rules on the denied list: non-interactive mode
  (no user to approve)`.
- Initial Medium finding patched: forbidden-claim validation now must
  distinguish affirmative public claims from sanctioned non-claims and quoted
  blocked examples across all claim categories.
- Initial Low findings also addressed: folded-section word-count guidance,
  concrete metadata files, default `hintCategory` candidate, and exact
  `nonClaims` discovery field name.
- `claude-opus-4.8` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-change-risk-language-guide --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-change-risk-language-guide/2026-06-22T225025-975Z-re-review-claude-opus-4.8.clean.md`.
- Re-review Medium findings patched: folded placements may trim only optional
  rows and must fall back to standalone if required content cannot fit;
  `rendered words` is now defined deterministically; folded claim-level labels
  must be host-compatible or unambiguously section-scoped.
- Re-review Low findings also addressed: required anchors are normative in
  requirements, the reduced-coverage Opus checkbox is annotated, and future
  implementation must verify current site information architecture before
  selecting placement.
- `claude-sonnet-4.6` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-change-risk-language-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-change-risk-language-guide/2026-06-22T225450-839Z-spec-claude-sonnet-4.6.clean.md`.
- Sonnet returned no Medium or higher findings. Low findings patched: summary
  audience now includes engineers and architects, the `when to say evidence
  shows` table must cover `TraceMap found`, and the Opus checkbox note was
  moved out of `tasks.md` and kept here in review state.
- `claude-sonnet-4.6` final re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-change-risk-language-guide --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-change-risk-language-guide/2026-06-22T225719-347Z-re-review-claude-sonnet-4.6.clean.md`.
- Final Sonnet re-review returned no Medium or higher findings. The repeated
  Low audience and `TraceMap found` notes were already present in the current
  requirements, and the remaining Sonnet checkbox note was patched.
- Current Medium or higher findings: none known after patches. Review coverage
  remains reduced because Kiro denied shell/write tools during each review run.
- PR-loop actionable review findings patched after PR creation: added
  `TraceMap found` to the design wording progression and changed standalone
  discovery metadata wording in requirements/tasks to use the exact
  `nonClaims` field name.

## Implementation Summary

Implemented the standalone `/language/change-risk/` public site route with
concept-level wording guidance, safe and unsafe phrasing tables,
evidence-required wording, reduced-coverage wording, owner-handoff wording,
stop conditions, and non-claims.

Added standalone sitemap and discovery metadata with `publicClaimLevel:
concept`, `sourceType: site-page`, `hintCategory: evidence`,
`preferredProofPath: /proof-paths/`, limitations, and `nonClaims`.

Added targeted inbound discovery links from nearby public-safe pages without
adding the route to primary navigation.

Added route-specific validator and tests, then wired the validator into the
aggregate site validation entrypoint.

## Follow-Ups

- None for this spec packet after the final PR-loop run.
