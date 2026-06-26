# Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

Last verified: 2026-06-26 after site implementation validation
Branch: codex/impl-site-build-review-workflow-story-20260625232947
Worktree: dedicated temporary worktree; local absolute path omitted from
tracked spec
Base: origin/dev
PR target: dev

## Status Normalization - 2026-06-26

This PR normalized stale status/readiness headers after verifying the
implemented `/blog/building-tracemap-under-review-pressure/` page exists on
`origin/dev`.

## Summary

This spec implements a `tracemap.tools` blog article about building TraceMap
with review pressure and coordination: Codex implementation help, Kiro spec
review, Qodo PR review, generic review-loop coordination, and evidence-led
specs. The public surface is concept-level process writing. It is not a
TraceMap runtime feature, scanner or reducer capability, product proof,
release approval, vendor endorsement, or AI/LLM impact-analysis claim.

## Scope Decisions

- Scope is site implementation for this phase.
- Target branch is `dev`.
- The article remains `Public claim level: concept`.
- The article keeps the shared principle `No public conclusion without
  evidence` visible.
- Selected placement is
  `/blog/building-tracemap-under-review-pressure/`.
- Rejected placement `/building-tracemap-under-review-pressure/` because the
  existing blog generator already owns narrative process articles and emits the
  canonical URL, Open Graph metadata, blog index card, and sitemap entry.
- Rejected placement as a section on an existing route because the topic is a
  distinct process story and would crowd claim-guardrail or validation pages.
- The packet intentionally mentions workflow participants by public tool or
  class name, but forbids endorsement, certification, partnership, and product
  capability claims.
- The packet intentionally forbids saying or implying that workflow tools
  consume TraceMap output as a TraceMap product feature.
- Tracked spec notes omit local absolute paths and private run identifiers.

## Implementation Decisions

- Existing article reconciliation: the new article complements
  `/blog/building-tracemap-with-codex-kiro-qodo/`. The existing article
  remains public as the baseline workflow overview. The new article uses the
  distinct title `Building TraceMap Under Review Pressure`, slug
  `/blog/building-tracemap-under-review-pressure/`, and category `Workflow
  governance`; it focuses on review-loop coordination, claim-level discipline,
  non-claims, validation handoff, and human ownership instead of re-explaining
  the shared Codex/Kiro/Qodo basics.
- Cross-link plan: the new article links to the existing workflow article for
  background, and the existing workflow article now links back to the new
  companion note. The new article also links to `/proof-paths/`,
  `/site-claim-guardrails/`, `/review-claim-checklist/`, `/validation/`, and
  `/limitations/`.
- ACK/agent-control naming decision: public-name status was not confirmed for
  this surface. Public copy uses the generic phrase `review-loop coordination
  layer` and does not name ACK or agent-control directly.
- Metadata consequences: blog placement uses the existing blog metadata schema
  in `site/src/_blog/articles.json`, generated canonical/Open Graph article
  metadata, generated blog index card, and generated sitemap entry. No
  `publicClaimLevel` field was added to blog metadata; the visible body label
  remains the source of truth. Discovery route metadata was not extended
  because existing blog articles are not discovery-tracked routes in
  `site/src/_site/discovery.json`.
- Adjacent route inventory before body copy: confirmed available and linked:
  `/blog/building-tracemap-with-codex-kiro-qodo/`, `/proof-paths/`,
  `/site-claim-guardrails/`, `/review-claim-checklist/`, `/validation/`, and
  `/limitations/`. Confirmed available but deferred to avoid crowding the
  article: `/proof-source-catalog/`, `/packets/`, `/review-room/`, `/roadmap/`,
  and `/team-evidence-handoff/`.
- Primary navigation remains unchanged. Discovery is through the generated blog
  index, sitemap, canonical/Open Graph metadata, and article cross-links.
- Rendered body word count: 1027 words, within the 700 to 1600 word target.
- Non-claim marker: the `What the workflow does not prove` section uses
  `data-non-claim-region="workflow-does-not-prove"` so validator exceptions
  stay scoped to explicit non-claims.

## Review Commands

- Completed:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-build-review-workflow-story --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: full coverage review with actionable findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-build-review-workflow-story/2026-06-26T000714-888Z-spec-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- Completed:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-build-review-workflow-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: full coverage review with actionable findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-build-review-workflow-story/2026-06-26T001015-503Z-spec-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.
- Both initial reviews completed before readiness was advanced. If a future
  re-review model is unavailable, record the exact error or blocker here and
  do not advance readiness with a known unresolved blocker.
- Completed:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-build-review-workflow-story --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: reduced coverage because Kiro reported denied shell tool access,
    but review output included actionable findings after reading the packet
    and verifying repository patterns with allowed tools.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-build-review-workflow-story/2026-06-26T001321-573Z-re-review-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- Completed:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-build-review-workflow-story --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: full coverage re-review with process-ordering findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-build-review-workflow-story/2026-06-26T001944-321Z-re-review-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.

## Review Findings

- Opus found one High, three Medium, and two Low findings.
- High 1, missing reconciliation with the existing
  `/blog/building-tracemap-with-codex-kiro-qodo/` article: patched by adding
  requirements, design guidance, and future tasks to decide whether the new
  article supersedes, complements, or extends the existing one, with distinct
  slug/title, cross-linking, and duplicate-content avoidance.
- Medium 2, ACK/agent-control may be internal: patched by requiring public
  name confirmation before using ACK/agent-control in public copy, with generic
  `review-loop coordination layer` wording if needed.
- Medium 3, unconditional `publicClaimLevel` discovery metadata did not match
  the current blog schema: patched by making the visible blog label the source
  of truth unless the schema is deliberately extended, while keeping
  `publicClaimLevel: concept` for non-blog discovery-tracked placement.
- Medium 4, forbidden-wording exception mechanism was underspecified: patched
  by requiring existing non-claim or rejected-pattern region markers, or
  equivalent negated validator patterns, and by naming the per-article
  validator and slug collision-control pattern.
- Low 5, word-count range could be too long for house style: patched by
  allowing a recorded site-constrained range bounded between 500 and 1800
  words.
- Low 6, validation pattern could name concrete existing patterns: patched by
  referencing the existing blog validator and slug collision-control pattern.
- Sonnet found three Medium and four Low findings.
- Medium 1, future validators might scan spec-source forbidden terms:
  patched by scoping forbidden-material and forbidden-claim validation to
  rendered site output and excluding `.kiro/specs/` source files.
- Medium 2, word-count escape hatch was unbounded: patched with a required
  recorded source and bounded 500-to-1800 range.
- Medium 3, spec-phase and future implementation tasks were intermixed:
  patched by splitting `tasks.md` into `Spec-phase tasks` and
  `Future implementation tasks`.
- Low 1, model-unavailable guidance was missing: patched in this review
  command section.
- Low 2, no minimum outbound link fallback was defined: patched in design and
  tasks.
- Low 3, spec validation tasks could be confused with future implementation
  validation: patched by task restructuring.
- Low 4, private reviewer identity wording was too broad: patched by
  distinguishing internal-only or non-public reviewer identities from public
  tool names and public workflow participants.
- Opus re-review found three Medium and three Low findings, with reduced
  coverage because Kiro reported denied shell tool access after reading files
  and verifying repository patterns with allowed tools.
- Re-review Medium 1, bare-token avoidance contradicted phrase-level forbidden
  patterns and public-safe vocabulary: patched by changing the token list to a
  guidance-only positive-claim warning and making Requirement 6's phrase-level
  patterns the validator contract.
- Re-review Medium 2, required non-claim sections could trip their own
  validator: patched by requiring non-claim or rejected-pattern region markers,
  or equivalent existing negated validator conventions, for the `what the
  workflow does not prove` section and rejected wording examples.
- Re-review Medium 3, existing article reconciliation was still
  under-constrained: patched by requiring complementary articles to avoid
  re-explaining shared Codex/Kiro/Qodo basics, focus on differentiators, and
  differentiate blog-index category and card copy where available.
- Re-review Low 1, recommended ACK heading hard-coded a conditional name:
  patched by adding generic heading fallback wording.
- Re-review Low 2, blog pattern wording overstated the current convention:
  patched by naming the proof-path article's established visible-label pattern.
- Re-review Low 3, the initial Medium-finding task could go stale: patched by
  scoping that checkbox to the initial Kiro review pass and adding a separate
  re-review findings task.
- Sonnet re-review found three Medium and four Low findings.
- Sonnet re-review Medium 1, spec-phase validation tasks were pending while
  readiness evidence was being prepared: patched by keeping validation pending
  until commands run and by requiring completed validation entries before
  readiness advances.
- Sonnet re-review Medium 2, readiness was ordered before validation tasks:
  patched by moving the readiness gate to the end of the spec-phase task list.
- Sonnet re-review Medium 3, ACK/agent-control naming was unresolved without
  an explicit handoff: patched by adding an implementation follow-up that must
  be resolved before article drafting.
- Sonnet re-review Low 1, `Last verified` was stale: patched to reflect the
  current reviewed-but-validation-pending state.
- Sonnet re-review Low 2, adjacent route existence should be checked before
  drafting links: patched in future implementation tasks.
- Sonnet re-review Low 3, non-claim marker task needed clearer sequencing:
  patched by making marker verification happen before forbidden-wording
  validation.
- Sonnet re-review Low 4, one Oddities bullet was aspirational rather than
  observed: removed from Oddities and retained as requirements/design intent.
- Readiness advanced to `ready-for-implementation` after Medium or higher
  findings from initial reviews and the bounded re-review were patched, and
  spec-phase validation passed.

## Validation

- `git diff --check`: passed after intent-to-add on 2026-06-26.
- `./scripts/check-private-paths.sh`: passed on 2026-06-26.
- Diff scope confirmation: passed on 2026-06-26. `git status --porcelain=v1`
  showed only `.kiro/specs/site-tracemap-tools-build-review-workflow-story/`
  in the worktree.
- After the Qodo wording patch, `git diff --check` and
  `./scripts/check-private-paths.sh` passed again on 2026-06-26.
- Implementation validation on 2026-06-26:
  - `npm test` from `site/`: passed, 544 tests.
  - `npm run validate` from `site/`: passed; generated-site validator reported
    72 HTML files, 2469 internal references, and 71 sitemap URLs.
  - `npm run build` from `site/`: passed.
  - `git diff --check`: passed.
  - `./scripts/check-private-paths.sh`: passed.
  - Desktop browser sanity at `1280x720`: passed; article title/H1 matched,
    required visible labels were present, and `scrollWidth` equaled viewport
    width.
  - Mobile browser sanity at `390x844`: passed; article title/H1 matched,
    required visible labels were present, and `scrollWidth` equaled viewport
    width.

## PR Review Loop

- Qodo PR review found one readiness-lifecycle wording mismatch in
  `review-packet.md`: the review focus still asked reviewers to verify
  `Readiness: spec-review` after the packet had advanced to
  `ready-for-implementation`.
- Patched by describing the lifecycle explicitly: initial spec review starts at
  `spec-review`, committed post-review state may advance to
  `ready-for-implementation`, while `Status: not-started` and
  `Public claim level: concept` stay consistent.
- Implementation PR review loop: pending until the implementation PR is opened.
  The local implementation pass intentionally records validation and branch
  evidence here before PR creation; final ACK decision is reported from the
  PR-loop readback.
- PR #350 initial ACK run against head
  `e9589eb5886f70c3acc411cc7d7265bce90e1f2b` returned
  `actionable_findings` / `UNRESOLVED_REVIEW_THREADS` with patch authorization.
  Current findings were:
  - Qodo: marked-region stripping could be bypassed because the validator
    decoded HTML entities before stripping marked non-claim regions.
  - Codex: metadata forbidden/private-material scanning covered preview
    descriptions but not all article metadata string fields such as `hero` and
    `calloutHtml`.
- Local review patch: strip marked regions from raw HTML before decoding, scan
  all article metadata string fields, and add regression tests for encoded
  closing-tag non-claim text plus `hero`/`calloutHtml` metadata.
- Post-review-patch validation on 2026-06-26:
  - `node --test scripts/build-review-workflow-story.test.mjs`: passed, 9
    tests.
  - `npm test` from `site/`: passed, 546 tests.
  - `npm run validate` from `site/`: passed; generated-site validator reported
    72 HTML files, 2469 internal references, and 71 sitemap URLs.
  - `npm run build` from `site/`: passed.
  - `git diff --check`: passed.
  - `./scripts/check-private-paths.sh`: passed.

## Oddities

- This is public process writing about tool-assisted development and review
  coordination, so the spec is intentionally stricter than ordinary blog copy
  about endorsement wording, raw review material, local paths, and internal
  identifiers.
- The generated blog system does not expose a `publicClaimLevel` metadata
  field for articles. The implementation followed the existing visible-label
  pattern instead of extending the schema for one article.
- The public page intentionally avoids the ACK/agent-control names because
  public-name status was not confirmed during implementation.

## Follow-ups

- No site follow-ups remain from local implementation validation.
- Final PR-loop outcome will be reported from the PR workflow rather than
  guessed from local state.
