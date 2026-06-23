# Implementation State

Status: in-progress
Readiness: ready-for-implementation
Last verified: 2026-06-23
Branch: codex/impl-site-evidence-handoff-template
Worktree: isolated implementation worktree; local absolute path omitted from tracked spec
Base: origin/dev
PR target: dev
Public claim level: concept

## Summary

Implementation is adding a public-site evidence handoff template. The surface
helps humans carry one bounded TraceMap static evidence question, proof path,
evidence metadata, limitation, non-claim, validation evidence, owner to ask,
and stop condition to another reviewer or owner.

The work remains site-only. It does not change scanner code, reducer code,
generated handoff output, runtime monitoring, ownership automation, release
approval, operational safety proof, AI or LLM analysis, or replacement for
human review.

## Scope Decisions

- The packet is concept-level only.
- The shared public principle is `No public conclusion without evidence`.
- Candidate placements are `/handoff/template/`,
  `/team-evidence-handoff/template/`, a section on
  `/team-evidence-handoff/`, or a section on `/packets/assembly/`.
- Final placement selected: `/handoff/template/`.
- `/handoff/template/` was selected because all neighboring routes exist and a
  neutral standalone route keeps the reusable field template distinct from
  receiver-specific handoff language and packet assembly workflow copy.
- Rejected `/team-evidence-handoff/template/` because the live
  `/team-evidence-handoff/` page focuses on receiver framing. Nesting under it
  could imply the template is team-specific instead of reusable across review
  contexts.
- Rejected a section on `/team-evidence-handoff/` because the required
  template fields, synthetic example, unsafe example, stop conditions,
  non-claims, and six neighbor distinctions are too large for the embedded
  word-count target without weakening content.
- Rejected a section on `/packets/assembly/` because the live
  `/packets/assembly/` page already owns the broader packet-preparation
  checklist. The template is the reusable handoff form for one bounded claim
  after packet ingredients are selected.
- Rejected `/team-evidence-handoff/template/` as a nested standalone route for
  the same receiver-specific placement risk; it remains a useful inbound link
  target from the team handoff page if needed later.
- Metadata consequence: the implementation uses standalone page metadata,
  canonical and Open Graph tags, sitemap metadata, and discovery metadata with
  `publicClaimLevel: concept`, `sourceType: site-page`,
  `hintCategory: use-case`, and `preferredProofPath: /proof-paths/`.
- Route gaps: none for required neighboring or support routes. The live site
  includes `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, `/proof-paths/`, `/limitations/`, and
  `/validation/`.
- Navigation decision: do not add the template to primary navigation. Add
  focused discovery through metadata and useful inbound links from neighboring
  handoff or packet surfaces only where it improves route discovery.
- Future implementation must distinguish this template from
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`.
- The spec forbids generated handoff feature claims, real organization
  ownership claims, runtime proof, release approval, operational safety,
  complete coverage, AI or LLM analysis, and replacement of human review.
- The tracked state note avoids local absolute paths so the repository
  private-path guard can remain strict.
- Claim-level vocabulary consulted from existing discovery metadata:
  `concept` and `demo`. This page uses only `concept`.
- Coverage-label vocabulary consulted from the spec and neighboring site copy:
  concept-only, demo-only, partial, reduced, gap, unknown, and syntax-only.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-handoff-template --kind spec --model claude-opus-4.8 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access after
    reading the spec. Artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T224406-243Z-spec-claude-opus-4.8.clean.md`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-handoff-template --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access after
    reading the spec. Artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T224731-037Z-spec-claude-sonnet-4.6.clean.md`.
- Re-reviews were run with both requested models after Medium findings were
  patched. Several intermediate re-reviews returned reduced coverage from
  Kiro tool-denial or one Kiro internal server/tool-approval error; exact
  errors are preserved in the matching `.meta.json` and `.clean.md`
  artifacts under `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/`.
- Latest full-coverage Opus review artifact before the final Sonnet
  bookkeeping pass:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T230326-748Z-re-review-claude-opus-4.8.clean.md`.
- Latest Sonnet review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T232058-496Z-re-review-claude-sonnet-4.6.clean.md`.

## Review Findings

- Initial Opus review found two Medium issues: validation scoping for
  required non-claims/unsafe examples, and private proof-path wording that
  could be read as permitting private identifiers. Both were patched.
- Initial Sonnet review found Medium issues around operator-requested model
  task wording, machine-checkable validation context, and spec-phase
  private-material checks. The model task was clarified as dispositioned if
  unavailable, and validation/private-material checks were tightened.
- Re-review rounds found additional Medium issues around context-scoped
  forbidden-claim validation, support-link traceability, synthetic example
  completeness, word-count behavior, checklist fields, named-person/org
  validation, realistic SHA/token scanning, placement wording, embedded
  placement viability, and review-packet checklist completeness. All Medium
  or higher findings were patched.
- Final Sonnet review found one Medium bookkeeping issue: this state file
  still said "Not reviewed yet" after reviews had run. This section records
  the review commands and findings, resolving that bookkeeping issue.
- Remaining Low items are accepted residual notes only: minor wording parity,
  continuous guard checkbox shape, and expected future implementation details
  such as concrete metadata values.

## Validation

- `git diff --check`: passed on 2026-06-22.
- `./scripts/check-private-paths.sh`: passed on 2026-06-22.
- Focused spec text checks: passed on 2026-06-22 with 76 checks covering
  required headers, all 15 required fields, required sections, all required
  stop conditions, all six neighbor distinctions, boundary wording, synthetic
  labeling, word-count expectations, and forbidden raw/private patterns.
- Implementation focused validator: `site/scripts/evidence-handoff-template.mjs`.
  It checks `/handoff/template/` route output, sitemap metadata, discovery
  metadata, required field rows, synthetic example completeness, required
  links, neighbor distinctions, checklist content, non-claims, stop
  conditions, forbidden positive claims, hard private material, realistic SHA
  shapes, and inbound links from adjacent routes.
- Focused validator tests:
  `node --test site/scripts/evidence-handoff-template.test.mjs` passed on
  2026-06-23.
- `cd site && npm test`: passed on 2026-06-23.
- `cd site && npm run validate`: passed on 2026-06-23. The validator rebuilt
  `dist/` and reported 62 HTML files, 2109 internal references, 61 sitemap
  URLs, 1 legacy story safety target, and 13 legacy modernization evidence-map
  rows.
- `cd site && npm run build`: passed on 2026-06-23.
- `git diff --check`: passed on 2026-06-23.
- `./scripts/check-private-paths.sh`: passed on 2026-06-23.
- Desktop browser sanity on `/handoff/template/`: passed on 2026-06-23 at
  1440x1100. The page title, H1, required sections, support links, and
  `No public conclusion without evidence` rendered; page-level horizontal
  overflow was false.
- Mobile browser sanity on `/handoff/template/`: passed on 2026-06-23 at
  390x844. Hero buttons fit within the viewport, page-level horizontal
  overflow was false, and the template table scrolled inside its existing
  table wrapper.
- PR loop outcome: pending until the ready PR exists.

If `./scripts/check-private-paths.sh` is absent at review time, record the
absence here and treat the check as an open gap rather than a pass.
The manual substitute for an absent private-path guard is a grep-based scan of
new spec files for absolute paths, private remote patterns, named individuals,
real organization names, command output, and credential-like strings; record
the scan command and result here before marking the task done.
`git diff --check` is a standard git command and is expected to be available.
If it fails for an unexpected reason, record the exact error here before
treating the check as done.

## Oddities

- `/owners/follow-up/` and `/decisions/evidence-record/` exist in the live site
  at implementation time and are linked directly.
- The template intentionally overlaps vocabulary with handoff and packet
  surfaces. Its distinct job is to keep one claim's reusable handoff fields
  together, not to explain receiver-specific language or the broader packet
  assembly process.
- The page uses a standalone route, so no embedded section anchor or
  separate-sitemap exemption was needed.
- The primary navigation was not changed. Discovery is via sitemap,
  discovery metadata, page links, and focused inbound links from
  `/team-evidence-handoff/` and `/packets/assembly/`.
- Desktop and mobile sanity used a temporary local static server on an
  alternate port because the default local port was already in use.

## Follow-ups

- Update this state file with the final PR-loop decision after the PR review
  workflow reaches a terminal state.
