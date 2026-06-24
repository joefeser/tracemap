# Implementation State

Status: implemented
Readiness: implemented
Last verified: 2026-06-21
Branch: codex/impl-site-review-packet-assembly
Worktree: isolated implementation worktree; local absolute path omitted from tracked spec
Base: origin/dev
PR target: dev
Public claim level: concept

## Summary

This implementation adds the public-site review packet assembly surface at
`/packets/assembly/`. The page is a human checklist for preparing public-safe
review handoff material from existing TraceMap evidence surfaces. It is not a
generated packet-builder feature, scanner change, reducer change, runtime
proof, release gate, or autonomous review workflow.

## Scope Decisions

- `origin/dev` did not contain this spec packet at implementation start; the
  spec directory was restored from `origin/main` before site edits. This is a
  temporary main/dev sync fact, not a route or claim change.
- Site source changes are limited to `site/src/` and `site/scripts/`. Generated
  `site/dist/` and `site/output/` are not hand-edited.
- The page remains `Public claim level: concept`.
- The required public principle is `No public conclusion without evidence`.
- Final placement: `/packets/assembly/`.
- Rejected alternative `/review-packet/`: too likely to read like a competing
  packet taxonomy or generated packet feature.
- Rejected alternative section on `/packets/`: the workflow needs standalone
  metadata, sitemap coverage, direct route validation, and enough space for the
  full ingredient and stop-condition checklist.
- Rejected alternative section on `/review-room/`: assembly is pre-handoff
  preparation, while the review room is a meeting agenda.
- Navigation decision: no top-nav addition. Discovery comes through sitemap,
  discovery metadata, route-level links from `/packets/` and `/review-room/`,
  and page-local links to adjacent surfaces.
- No adjacent route omissions were needed; all required adjacent routes existed
  at implementation time and are linked from the page.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind spec --model claude-opus-4.8 --fresh --save-review-text`
  - Result: review completed with reduced coverage because Kiro reported
    denied tool access.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023537-509Z-spec-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: full coverage wrapper result, but the model returned a
    clarification prompt instead of review findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023843-261Z-spec-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind re-review --model claude-opus-4.8 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access. Clean
    output confirmed the Medium findings were patched; Kiro could not write
    bookkeeping updates because its `execute_bash` and `fs_write` tools were
    denied in non-interactive mode.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023957-489Z-re-review-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind re-review --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access. Clean
    output independently confirmed the same patched state and no remaining
    Medium or higher findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T024430-655Z-re-review-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.

## Review Findings

- `claude-opus-4.8` found four Medium and five Low issues.
- M1, forbidden-claim validation collided with required non-claim content:
  patched by requiring negation-aware or sanctioned-region-scoped validation.
- M2, commit SHA, extractor version, and file span needed explicit fallback
  semantics: patched by requiring explicit limitations when values are not
  public-safe or unavailable.
- M3, adjacent-surface differentiation missed change-review, stakeholder
  questions, and claim-ledger overlap: patched in requirements, design, and
  review packet map.
- M4, completed spec packet tasks were stale: patched in `tasks.md`.
- L1, missing shared principle in `review-packet.md`: patched.
- L2, possible generated-builder misread from assembly naming: dispositioned as
  a future implementation concern; design now leads with human checklist
  framing and future title/H1/metadata should do the same.
- L3, invented discovery hint categories: patched by pinning `hintCategory` to
  `use-case` unless an existing more specific value exists at implementation.
- L4, word-count pressure: acknowledged in design as a risk requiring compact
  tables and short bullets.
- L5, neighboring map omitted proof/support routes: patched by adding
  `/proof-paths/`, `/limitations/`, and `/validation/`.
- `claude-sonnet-4.6` initial spec review returned a clarification prompt
  rather than actionable findings.
- Re-reviews with both requested models found no remaining Medium or higher
  issues in the patched packet, with reduced coverage noted because Kiro's
  internal non-interactive tool access was denied.

## Validation

- `git diff --check`: passed on 2026-06-21 after implementation.
- `./scripts/check-private-paths.sh`: passed on 2026-06-21 after
  implementation.
- Focused spec text checks: passed on 2026-06-21 for required
  status/readiness/claim-level copy, shared principle, required ingredients,
  workflow sections, stop conditions, adjacent-surface references, and
  forbidden local/private marker patterns.
- `cd site && npm test`: passed on 2026-06-21.
- `cd site && npm run validate`: passed on 2026-06-21; generated static site
  validated 51 HTML files, 1676 internal references, 50 sitemap URLs, 1 legacy
  story safety target, and 13 legacy modernization evidence-map rows.
- `cd site && npm run build`: passed on 2026-06-21.
- Browser sanity: passed on 2026-06-21 using the Playwright CLI fallback
  against `http://localhost:4183/packets/assembly/`. Desktop 1440x1000 and
  mobile 390x844 snapshots showed the expected route title, H1, checklist
  content, required boundary copy, and no page-level horizontal overflow. On
  mobile the required ingredients table scrolls inside the existing
  `.claim-ledger-wrap` container.
- In-app browser control was attempted first but failed before navigation with
  a browser-control environment metadata error; terminal Playwright CLI was
  used for the real browser sanity check.
- PR loop:
  - Initial run on PR #259 stopped on
    `PR_BODY_LITERAL_ESCAPED_NEWLINES`; PR body was edited to replace literal
    escaped newline sequences with real Markdown newlines.
  - Second run stopped on four unresolved Gemini review threads in
    `site/scripts/review-packet-assembly.mjs`; fixed by normalizing sitemap
    paths, route-index paths, and required href checks, and by tightening
    attribute parsing so `data-href` cannot satisfy required `href` links.
    Added regression tests for trailing-slash link normalization and
    `data-href` rejection. Threads were resolved after the fix.
  - Third run stopped on a Qodo top-level actionable finding for
    tag-splitting scan bypasses and brittle case-sensitive non-claim metadata
    checks; fixed by adding tight tag-stripped scan text for forbidden/private
    checks and case-insensitive metadata topic matching. Added regression tests
    for tag-split forbidden claims, tag-split private text, and case-varied
    route nonClaims.
  - A PR-level disposition was recorded for the Qodo top-level comment with
    fixing commit `4856f8d1066418304a4f7df34b0ada7addace2f3`, validation
    evidence, and low residual risk.
  - Final recorded run before this state-note-only update returned
    `merge_ready` on head `4856f8d1066418304a4f7df34b0ada7addace2f3`,
    stop reason `NONE`, no unresolved threads, no pending checks, no failed
    checks, no actionable bot findings, merge state `CLEAN`, and residual risk
    `medium` because required Codex review was satisfied by the configured
    `trustedCodeReview` quorum after Qodo returned.

## Oddities

- The spec intentionally overlaps vocabulary with packets, handoff,
  review-room, checklist, and proof-source surfaces, but its distinct job is
  pre-handoff assembly: gather ingredients, check stop conditions, and record
  next owners.
- Tracked spec notes avoid local absolute paths so the repository private-path
  guard can remain strict.
- `/manager-packet/` exists as a public route but is not currently present in
  discovery metadata. The new validator accepts adjacent links backed by either
  discovery metadata or sitemap metadata so route validation matches the live
  site shape without adding unrelated discovery churn.

## Follow-ups

- Keep future copy centered on human checklist assembly so the route does not
  read as a generated packet-builder feature.
