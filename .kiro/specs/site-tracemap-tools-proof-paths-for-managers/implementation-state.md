# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-26
Branch: codex/spec-site-proof-paths-for-managers-20260626002459
Worktree: isolated temporary worktree; local absolute path omitted from
tracked spec
Base: origin/dev
PR target: dev
Public claim level: concept

## Summary

This spec-only packet defines a future manager-facing `tracemap.tools` page or
section about proof paths in decision terms. The future surface should help a
manager or reviewer ask a question, inspect a static evidence packet, preserve
coverage labels and limitations, understand what the packet does not prove,
and route the next runtime, product, release, ownership, or security judgment
to the correct owner category.

No site source, scanner code, reducer code, generated output, validation
script, runtime behavior, public copy, or automation workflow is implemented
in this branch.

## Scope Decisions

- Target base: `origin/dev`.
- Scope is limited to
  `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/`.
- Future public claim level remains `concept`.
- Required visible future copy:
  - `Public claim level: concept`
  - `No public conclusion without evidence`
- Recommended placement starts at `/proof-paths/for-managers/`, with
  alternatives recorded in `requirements.md` and `design.md`.
- The future surface is distinct from `/manager-brief/`, `/manager-faq/`,
  `/manager-packet/`, `/packets/`, `/packets/assembly/`, `/proof-paths/`,
  `/proof-paths/faq/`, and `/proof-paths/tour/`.
- The packet intentionally keeps future implementation tasks unchecked until
  a later site implementation phase completes them.
- Tracked notes avoid local absolute paths, raw remotes, private sample names,
  raw artifacts, source snippets, SQL, config values, secrets, and hidden
  validation details.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: reduced coverage because Kiro reported denied internal tool
    access.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-proof-paths-for-managers/2026-06-26T053027-076Z-spec-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: reduced coverage because Kiro reported denied internal tool
    access.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-proof-paths-for-managers/2026-06-26T053421-383Z-spec-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: reduced coverage because Kiro reported denied internal tool
    access. No Medium or higher findings remained in the patched packet.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-proof-paths-for-managers/2026-06-26T053743-267Z-re-review-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: reduced coverage because Kiro reported denied internal tool
    access. One Medium and one Low finding were returned and patched.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-proof-paths-for-managers/2026-06-26T054136-961Z-re-review-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.

Reduced-coverage note: the wrapper ran and saved review text, but Kiro's
internal `execute_bash`/`fs_write` style tools were denied in non-interactive
mode. The review artifacts therefore include tool-denied analysis gaps.

## Review Findings

- Opus initial review found one Medium and one Low issue.
- Opus M1, forbidden-wording enforcement omitted embeddings, vector database,
  and prompt-based classification: patched in requirements, design, and
  tasks.
- Opus L1, owner-category vocabulary drift could make validation ambiguous:
  patched by adding the public owner categories used in the matrix and by
  clarifying open public-role vocabulary validation.
- Sonnet initial review found one Medium and four Low issues.
- Sonnet M1, conclusion verbs `confirms` and `verifies` could bypass
  forbidden-wording validation: patched in requirements, design, and tasks
  with bounded validation-evidence exceptions.
- Sonnet L1, row-level claim-level escape lacked an absent-note fallback:
  patched so absent implementation-state evidence defaults the row to
  `concept`.
- Sonnet L2, recorded-equivalent placement fallback needed a guard: patched
  to require a spec amendment or explicit implementation-state entry before
  using a placement outside the named candidates.
- Sonnet L3, inbound link text preservation needed validation: patched in
  requirements and tasks.
- Sonnet L4, compound proof-path anatomy checkbox was easy to over-check:
  patched by splitting anatomy fields into individual future implementation
  tasks.
- Opus re-review found no Medium or higher issues and one Low route-inventory
  note for `/proof-source-catalog/`: patched by adding that route to adjacent
  surface and discovery/inbound-link guidance.
- Sonnet re-review found one Medium and one Low issue.
- Sonnet re-review M1, section-case metadata compatibility was
  underspecified: patched by listing host title, description, canonical URL,
  Open Graph title, Open Graph description, discovery summary, and sitemap or
  route-index entry checks.
- Sonnet re-review L1, `release approval` was implicit but not explicit in
  the forbidden-wording list: patched in requirements.
- No unpatched Medium or higher findings remain. Readiness moved to
  `ready-for-implementation` after the bounded re-review findings were
  patched.

## Validation

- `git diff --check`: passed on 2026-06-26 after `git add -N` made the
  spec-only files visible to the diff checker.
- `./scripts/check-private-paths.sh`: passed on 2026-06-26.
- Diff-scope confirmation: passed on 2026-06-26. `git diff --name-only`
  listed only files under
  `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/`.
- Future implementation tasks remain unchecked; only the spec-only review
  tasks are marked complete.

Future implementation validation is defined in `requirements.md` and
`tasks.md`, including site tests, site validation, site build, and desktop and
mobile browser sanity checks when layout or interaction changes.

## Oddities

- This surface overlaps manager FAQ, manager packet, proof-path FAQ,
  proof-path tour, packet assembly, and stakeholder objection vocabulary. Its
  distinct job is manager/reviewer decision routing: question, evidence
  packet, static support, non-claim, coverage consequence, stop condition, and
  next owner.
- The future page should not promise that management decisions are automated.
  It can identify owner categories and handoff boundaries only.

## Follow-ups

- Patch or disposition Medium or higher Kiro spec-review findings before
  moving readiness to `ready-for-implementation`.
- Keep any future implementation PR site-only and run the site validation plan
  before marking implementation tasks complete.
