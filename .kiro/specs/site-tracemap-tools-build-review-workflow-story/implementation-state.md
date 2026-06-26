# Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Last verified: 2026-06-26 after spec-phase validation
Branch: codex/spec-site-build-review-workflow-story-20260625190317
Worktree: dedicated temporary worktree; local absolute path omitted from
tracked spec
Base: origin/dev
PR target: dev

## Summary

This spec defines a future `tracemap.tools` article or page about building
TraceMap with review pressure and coordination: Codex implementation help,
Kiro spec review, Qodo PR review, ACK/agent-control review loops, and
evidence-led specs. The future public surface is concept-level process
writing. It is not a TraceMap runtime feature, scanner or reducer capability,
site implementation, validation script, product proof, release approval,
vendor endorsement, or AI/LLM impact-analysis claim.

## Scope Decisions

- Scope is spec-only in this phase.
- Target branch is `dev`.
- The future article remains `Public claim level: concept`.
- The future article must keep the shared principle `No public conclusion
  without evidence` visible.
- Preferred future placement is
  `/blog/building-tracemap-under-review-pressure/`.
- The future implementation may choose a different route or section only if it
  records the decision and rejected alternatives here.
- The packet intentionally mentions workflow participants by public tool or
  class name, but forbids endorsement, certification, partnership, and product
  capability claims.
- The packet intentionally forbids saying or implying that workflow tools
  consume TraceMap output as a TraceMap product feature.
- Tracked spec notes omit local absolute paths and private run identifiers.

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

## PR Review Loop

- Qodo PR review found one readiness-lifecycle wording mismatch in
  `review-packet.md`: the review focus still asked reviewers to verify
  `Readiness: spec-review` after the packet had advanced to
  `ready-for-implementation`.
- Patched by describing the lifecycle explicitly: initial spec review starts at
  `spec-review`, committed post-review state may advance to
  `ready-for-implementation`, while `Status: not-started` and
  `Public claim level: concept` stay consistent.

## Oddities

- This is public process writing about tool-assisted development and review
  coordination, so the spec is intentionally stricter than ordinary blog copy
  about endorsement wording, raw review material, local paths, and internal
  identifiers.

## Follow-ups

- ACK/agent-control public-naming question is unresolved at spec stage. Before
  implementation begins, confirm whether ACK/agent-control is publicly
  nameable. If it is not, use `review-loop coordination layer` wording in all
  public copy and record the decision here. Do not begin article drafting until
  this is resolved.
