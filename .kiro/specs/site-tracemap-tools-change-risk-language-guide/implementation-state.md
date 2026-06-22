# Site TraceMap Tools Change-Risk Language Guide Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-change-risk-language-guide`
Base: `origin/dev`
Target PR base: `dev`

## Scope

This is a spec-only public-site packet for a future change-risk language guide.
The current phase may write only this spec directory. It must not edit site
source, generated output, scanner code, reducer code, package files, validation
scripts, or existing specs.

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

## Placement Decision Status

Not selected. Future implementation must evaluate:

- `/language/change-risk/`
- `/review-claim-checklist/language/`
- A section on `/review-claim-checklist/`
- A section on `/questions/objections/`

Selection and rejected alternatives must be recorded here during future
implementation.

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

## Implementation Summary

Not implemented. This packet defines future implementation requirements only.
Spec review findings have been handled, so the packet is ready for a future
implementation phase.

## Follow-Ups

- Future implementation should begin by verifying current site information
  architecture and recording placement decisions here.
