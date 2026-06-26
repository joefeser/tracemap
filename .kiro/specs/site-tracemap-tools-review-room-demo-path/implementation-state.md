# Site TraceMap Tools Review Room Demo Path Implementation State

Status: implemented
Readiness: ready-for-pr
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-review-room-demo-path-20260626095826`

Base: `origin/dev`

Base commit: `36eedb10 [codex] add manager proof-path guide (#361)`

Target branch: `dev`

## Scope

This implementation adds a standalone concept-level public-site route for the
review-room demo path. It remains site-only and does not change scanner,
reducer, route-flow, language-adapter, generated output, or package behavior
outside the static site validation surface.

Tracked implementation changes are limited to:

- `site/src/review-room/demo-path/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/src/styles.css`
- `site/scripts/review-room-demo-path.mjs`
- `site/scripts/review-room-demo-path.test.mjs`
- `site/scripts/validate.mjs`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/tasks.md`
- `.kiro/specs/site-tracemap-tools-review-room-demo-path/implementation-state.md`

## Placement And Claim Decisions

Selected placement: `/review-room/demo-path/`

Rationale: the path starts from the review-room family and needs one contiguous
nine-step block. A standalone route keeps the base review room and meeting
agenda from becoming tutorial pages while allowing sitemap, discovery,
canonical, and route-specific validation.

Rejected alternative: section on `/review-room/`

Reason rejected: the base room is already the concept-level orientation and
would become crowded if it also carried the full guided-path contract.

Rejected alternative: section on `/review-room/agenda/`

Reason rejected: the agenda is a meeting script; this path is broader visitor
orientation across agenda, proof paths, packets, checklist, limitations,
validation, demo context, owner routing, and stop conditions.

Rejected alternative: section on `/demo/start-here/`

Reason rejected: this implementation keeps the claim level at concept and
starts from review-room intent, not demo-start interpretation.

Primary navigation remains unchanged.

## Route Existence And Link Decisions

All preferred adjacent routes exist at implementation time.

Rendered links include:

- `/review-room/`
- `/review-room/agenda/`
- `/proof-paths/`
- `/proof-paths/tour/`
- `/review-claim-checklist/`
- `/packets/`
- `/packets/assembly/`
- `/packets/examples/`
- `/demo/`
- `/demo/start-here/`
- `/demo/evidence-trail/`
- `/demo/proof-assets/`
- `/demo/result/`
- `/demo/runbook/`
- `/demo/troubleshooting/`
- `/limitations/`
- `/validation/`
- `/owners/follow-up/`

Evidence-packet routes present: `/packets/`, `/packets/assembly/`, `/packets/examples/`

Missing or deferred adjacent links: none.

## Implementation Notes

- Added visible `Public claim level: concept`.
- Added visible `No public conclusion without evidence`.
- Rendered the nine required guided-path steps in order as one contiguous
  table block with machine-readable row and field attributes.
- Every step has non-empty limitation, stop condition, and next owner or route
  text.
- The evidence-packet step links all three present packet routes.
- Owner routing uses role labels only and states that routing transfers
  responsibility for the next review step; it does not prove, approve,
  diagnose, validate, or clear a claim.
- Metadata uses canonical route fields and `og:type` set to `article`.
- Discovery metadata uses `publicClaimLevel: concept`, `hintCategory:
  use-case`, `sourceType: site-page`, and `preferredProofPath:
  /proof-paths/`.

## Validation Plan

- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- `git diff --check`
- `./scripts/check-private-paths.sh`
- Desktop and mobile browser sanity checks for `/review-room/demo-path/`
- `git diff --name-only origin/dev...HEAD`

## Validation Results

- `cd site && npm test` passed with 579 tests.
- `cd site && npm run validate` passed after building generated output and
  validating 75 HTML files, 2642 internal references, 74 sitemap URLs, 1
  legacy story safety target, 11 legacy .NET evidence-lane rows, and 13
  legacy modernization evidence-map rows.
- `cd site && npm run build` passed.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- Scope check confirmed the diff is limited to this spec folder and required
  site source/test/validation files.

Browser sanity: desktop and mobile checks completed with Playwright against
`/review-room/demo-path/` on the local site server. Desktop snapshot showed
the hero, navigation, static-question section, and contiguous guided-path table
rendering. Mobile snapshot showed wrapped navigation and hero copy; layout
metric check reported `clientWidth: 390`, `scrollWidth: 390`, table scroller
present, and 9 guided-path rows. Browser console reported 0 warnings and 0
errors.

## Review Loop Notes

ACK pending until the implementation is validated, committed, pushed, and a
ready PR into `dev` exists.

## Oddities

- The previous spec packet recorded itself as spec-only. This implementation
  intentionally advances the same spec folder state while leaving the original
  requirements and design public-claim boundaries intact.
- The route is discoverable through sitemap and discovery metadata but is not
  added to primary navigation.

## Follow-Ups

- If future adjacent routes are renamed, update the route links and validator
  together rather than leaving dead links.
- Do not upgrade the route to demo-level unless a future spec adds checked-in
  public-safe demo evidence for every guided-path step and validation
  assertion.
