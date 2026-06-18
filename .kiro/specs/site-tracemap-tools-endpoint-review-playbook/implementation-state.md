# Implementation State

Status: implemented
Readiness: ready-for-implementation
Last verified: 2026-06-18
Branch: codex/impl-site-endpoint-review-playbook
Worktree: not recorded (private-text guardrail)
Worktree path is intentionally omitted to satisfy the private absolute-path
guardrail in this spec and `check-private-paths.sh`.
Source of truth: spec files in branch `codex/impl-site-endpoint-review-playbook`
Public claim level: concept

## Summary

This implementation publishes the public-safe endpoint review playbook at
`/use-cases/endpoint-review/`. The page helps engineers use TraceMap's
deterministic static evidence to decide where an endpoint-adjacent review needs
deeper code review, targeted tests, telemetry questions, or owner follow-up.

The implementation keeps the page at `Public claim level: concept` and does
not claim a specific endpoint finding.

## Implementation Results

- Added `/use-cases/endpoint-review/` using the existing static site page,
  canonical top-navigation, long-form section, grid, and link-section patterns.
- Added the route to sitemap metadata and discovery metadata with
  `publicClaimLevel: "concept"`, `sourceType: "site-page"`,
  `hintCategory: "use-case"`, and `preferredProofPath: "/proof-paths/"`.
- Linked the page from `/use-cases/` and added minimal inbound links from
  `/review-room/` and `/static-triage/`.
- Added `site/scripts/endpoint-review.mjs` and companion tests for required
  rendered phrases, required links, discovery metadata, artifact boundaries,
  private-text patterns, forbidden overclaims, blame/scare framing, and
  unsupported endpoint conclusions.
- Wired the focused validator into aggregate site validation.
- Kept artifact-family names on the rendered page inside the sanctioned
  artifact-boundary section and in discovery output inside `nonClaims`.

## Scope

- Define requirements for a future `/use-cases/endpoint-review/` page.
- Define future implementation tasks, leaving all tasks unchecked.
- Keep the public claim level at `concept`.
- Frame endpoint review professionally around static coupling, dependency
  surfaces, gaps, and review friction without blaming teams, vendors, or code
  authors.
- Require evidence dimensions for endpoint-adjacent static paths, packages,
  config surfaces, SQL-facing surfaces, coverage labels, and limitations.
- Require artifact-boundary guidance for public-safe summaries versus local-only
  scanner artifacts and private review material.
- Require claim-safe language, discovery metadata, sitemap metadata, focused
  validation, and related-page cross-links.

## Claim Boundary Decisions

- Public claim level is `concept` because this spec defines an endpoint review
  workflow and safe public framing. It does not claim a specific public demo
  endpoint has an evidence-backed finding.
- The future page may link to demo and proof surfaces for orientation, but those
  links do not upgrade this page to a demo-level endpoint conclusion.
- Static evidence may route inspection and human review. It cannot prove runtime
  behavior, production traffic, endpoint performance, outage cause, release
  safety, operational safety, AI impact analysis, LLM analysis, complete product
  coverage, or team/vendor fault.
- Raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL, config
  values, secrets, local absolute paths, raw remotes, generated scan
  directories, and private sample names stay local-only.

## Route Decision

- Selected future route: `/use-cases/endpoint-review/`.
- Reason: endpoint review is an engineer-facing use case and should sit beside
  related public use-case material.
- Rejected alternate: `/endpoint-review/`. Reason: less consistent with the
  existing use-case grouping.
- Rejected alternate: `/demo/endpoint-review/`. Reason: this spec is concept
  level and does not anchor a specific endpoint packet to checked-in public demo
  proof.
- Rejected alternate: `/review-room/endpoint/`. Reason: `/review-room/` owns
  meeting-agenda framing; this page owns the engineer playbook.

## Spec Review Commands

Spec review commands planned:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-endpoint-review-playbook --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-endpoint-review-playbook --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Review results:

- Initial Sonnet spec review completed with full coverage and no Medium or
  higher findings. Low lifecycle findings requested review-results tracking,
  an explicit readiness gate, and open-question disposition.
- Initial Opus spec review completed with reduced coverage because Kiro
  reported denied tool access. Exact wrapper message: `Review coverage reduced:
  Kiro reported denied tool access. See meta analysisGaps.`
- Opus found one High issue requiring overclaim, blame, and scare-framing
  validator checks to use sanctioned-section carve-outs so the future page can
  teach red flags without failing its own validator.
- Opus found Medium issues requiring the required link list to be consistent
  between page requirements and validation, and requiring discovery metadata to
  avoid non-shipped availability wording.
- Opus re-review found one Medium issue requiring discovery-output
  artifact-family-name handling to mirror the page-copy carve-out by allowing
  artifact names only in discovery `nonClaims`.
- Final Sonnet re-review after the discovery carve-out patch completed with
  full coverage and no Medium or higher findings.
- Final Opus re-review after the discovery carve-out patch completed with
  reduced coverage because Kiro again reported denied tool access. Exact
  wrapper message: `Review coverage reduced: Kiro reported denied tool access.
  See meta analysisGaps.` No Medium or higher findings remained in the clean
  review output.
- Patched `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.

## Validation

Spec phase validation to run on this branch:

- `git diff --check`
- `git diff --cached --check`
- `./scripts/check-private-paths.sh`

Spec readiness gate before marking `ready-for-implementation`:

- Initial spec review complete and findings recorded.
- `git diff --check` passes on the spec branch.
- `git diff --cached --check` passes after staging intended spec files.
- `./scripts/check-private-paths.sh` passes.
- Readiness is upgraded from `review-needed` to `ready-for-implementation` only
  after the review and validation gates pass.

Spec phase validation results:

- `git diff --check` passed on 2026-06-18 before the readiness update.
- `git diff --cached --check` passed on 2026-06-18 after staging the owned
  spec folder.
- `./scripts/check-private-paths.sh` passed on 2026-06-18 after staging the
  owned spec folder.
- Final validation was rerun after the readiness update and recorded before
  commit.
- PR review-loop patch validation on 2026-06-18:
  - `git diff --check` passed after patching review-thread findings.
  - `./scripts/check-private-paths.sh` passed after patching review-thread
    findings.
  - `git diff --cached --check` was rerun after staging the PR-loop patch.

Implementation validation results:

- `npm test` from `site/` passed on 2026-06-18. Result: 187 tests passed.
- `npm run validate` from `site/` passed on 2026-06-18. Result: built static
  site, validated 43 HTML files, 1295 internal references, 42 sitemap URLs,
  and 1 legacy story safety target.
- `npm run build` from `site/` passed on 2026-06-18.
- Desktop browser sanity check passed on 2026-06-18 at 1440px width. The
  generated route loaded with the expected title, H1, concept/evidence hero
  note, and no horizontal overflow.
- Mobile browser sanity check passed on 2026-06-18 at 390px width. The
  generated route loaded with the expected H1 and concept/evidence hero note,
  and no horizontal overflow.
- `git diff --check` passed on 2026-06-18 before spec bookkeeping updates.
- `./scripts/check-private-paths.sh` passed on 2026-06-18 before spec
  bookkeeping updates.
- `git diff --cached --check` passed on 2026-06-18 after staging
  implementation changes.
- `./scripts/check-private-paths.sh` passed on 2026-06-18 after spec
  bookkeeping updates.

## Review Findings

- Initial Sonnet review found only Low lifecycle findings. Patched review
  results, open-question disposition, and readiness-gate recording.
- Initial Opus review found one High and two Medium findings. Patched
  validator carve-outs for red-flag and non-claim vocabulary, pinned the
  outbound link list to match validation, and added non-shipped discovery
  wording constraints.
- Opus re-review found one Medium finding requiring the discovery-output
  artifact-family-name carve-out to allow artifact names only in discovery
  `nonClaims`, matching existing discovery convention. Patched
  `requirements.md`, `design.md`, and `tasks.md`.
- Final Sonnet and Opus re-reviews after the discovery carve-out patch found no
  Medium or higher findings. Remaining Low lifecycle items were patched or
  deferred to spec-phase validation recording.
- Added a design note explaining that the dedicated validator is intentionally
  stricter than the closest `/use-cases/incident-review/` sibling, which relies
  on aggregate validation.
- PR review loop found unresolved review-thread findings requesting explicit
  discovery `path` coverage in `requirements.md`, `design.md`, and `tasks.md`,
  plus clearer raw-content validation wording that preserves artifact-family
  carve-outs. Patched those findings in the spec files.
- Implementation self-review found one copy issue: a negated endpoint-impact
  phrase in the authored concept example was too close to the hard boundary.
  Reworded it to say the example does not make an impact conclusion.
- Initial implementation test run found the focused validator needed to check
  the `<rule-id>` placeholder against decoded HTML because the shared rendered
  text helper treats angle-bracket placeholders like tags. Patched the
  validator and companion fixtures.
- Initial aggregate validation tests needed the new endpoint route and
  `/use-cases/` route in their generated fixture. Patched the fixture and
  reran tests.

## Oddities

- This spec intentionally avoids demo-level endpoint proof because no
  endpoint-specific public proof packet is introduced by this implementation.
- Existing `/review-room/` and `/static-triage/` pages already cover adjacent
  review framing. The implemented page links those surfaces without adding the
  new route to canonical top navigation.
- Local site port `4173` was already occupied during browser sanity checks, so
  the check used an alternate local port. No local port or machine path is
  recorded here because this state file must stay public-safe.

## Follow-ups

- Run the required PR review loop after opening the ready PR.
