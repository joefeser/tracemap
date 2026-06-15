# Site TraceMap Tools Incident Review Use Case Design

Public claim level: concept

## Surface

Add a future static page at `/use-cases/incident-review/` with source in
`site/src/use-cases/incident-review/index.html`. The page should use existing
site chrome and layout classes such as `page-hero`, `hero-note`, `section`,
`workflow-grid`, `detail-list`, `boundary-section`, and `link-section`.

The source page must include a placeholder `<header class="site-header">` block
so `site/scripts/build.mjs` can replace it with the canonical navigation and set
`aria-current` dynamically. Do not add `/use-cases/incident-review/` to the
primary top navigation. The page should be reached through `/use-cases/`,
`/manager-packet/`, packet/proof pages, capabilities, limitations, and roadmap
context.

## Content Structure

1. Hero: "Incident review use case" or similarly literal heading, visible
   `Public claim level: concept`, and a short statement that TraceMap orients
   code questions with static evidence but does not prove runtime behavior.
2. Context: explain the narrow problem of answering code questions during
   review or incident follow-up when teams need file spans, rule IDs, evidence
   tiers, coverage labels, proof paths, and limitations.
3. What TraceMap can orient: changed contracts, endpoints, static paths,
   packages, SQL/config surfaces, scan coverage, and analysis gaps.
4. Role questions: engineers, reviewers, managers, and architects each get
   concrete questions they can ask from the evidence packet.
5. Proof path: link to `/use-cases/`, `/manager-packet/`, `/packets/`,
   `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`,
   `/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`, and explain
   how each page should be used.
6. Non-claims: prominent boundary copy for runtime behavior, production
   traffic, deployment state, endpoint performance, P1 root cause, release
   safety, and AI impact analysis.
7. Artifact safety: spell out what can be summarized publicly and what remains
   local-only.

## Suggested Page Copy Constraints

Use phrases like:

- "static evidence for code questions"
- "orient review or incident follow-up"
- "inspect rule IDs, tiers, coverage labels, and limitations"
- "ask better follow-up questions"
- "raw artifacts stay local"

Avoid phrases like:

- "prove runtime impact"
- "find the root cause"
- "validate the release"
- "replace traces/logs/tests"
- "understand production dependencies"
- "AI impact analysis"

## Cross-Link Plan

Canonical new-page back-link set: `/use-cases/`, `/manager-packet/`,
`/packets/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`,
`/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`.

- `/use-cases/`: add the primary link to the new use-case page.
- `/use-cases/` placement: add the new entry near "Release review" or
  "Reviewer handoff" so incident-adjacent review copy stays in the use-case
  grid rather than becoming an orphaned footer link.
- `/manager-packet/`: link managers to the incident-review concept as a bounded
  follow-up path.
- `/packets/`: link packet readers to the use case when they need a role-based
  workflow for review or follow-up questions.
- `/demo/proof-assets/`: link only with copy that says visuals orient demo
  evidence and do not prove incidents.
- `/demo/proof-upgrades/`: link only with copy that stays attached to demo proof
  rows, coverage labels, and limitations.
- `/capabilities/`: link near static evidence capabilities or claim boundaries.
- `/limitations/`: link only if the surrounding copy reinforces runtime,
  production, release, and incident-root-cause non-claims.
- `/roadmap/`: add the route to an existing concept/future planning section with
  concept-level wording. If the future implementation no longer has a natural
  concept section, defer the roadmap link and record the reason in
  `implementation-state.md`.
- `/use-cases/incident-review/`: link back to the full canonical back-link set.

Incoming links from `/demo/proof-assets/`, `/demo/proof-upgrades/`,
`/limitations/`, and `/roadmap/` are conditional on bounded wording and may be
deferred with a recorded reason. Outgoing links from
`/use-cases/incident-review/` to those pages are unconditional because the new
page can frame them safely in its own proof-path section.

## Layout Notes

- Keep the page dense and work-focused. This is not a marketing hero or a
  standalone incident-response product page.
- Use cards only for role-specific questions or repeated evidence categories.
- Keep non-claims visible above the final link section.
- Use normal site text links for cross-links; do not add a special navigation
  component unless a future shared pattern already exists.
- Confirm the longest labels fit at mobile width and that proof-path cards do
  not overflow.

## Safety

The page must not publish raw source snippets, raw SQL, config values, secrets,
local absolute paths, raw repo remotes, raw `facts.ndjson`, `index.sqlite`,
combined SQLite files, generated scan directories, analyzer logs, or private
sample identities.

The page may publish public-safe labels, counts, checked-in public demo source
paths, report-family names, rule/status framing, evidence tiers, coverage
labels, supporting IDs, limitations, and links to existing public pages.

## Validation

The implementation phase should run `git diff --check`, `npm test`,
`npm run validate`, and `npm run build` from `site/`, then do desktop and
mobile browser sanity checks for `/use-cases/incident-review/` and at least one
touched doorway page.

The claim-level marker, non-claims, and artifact-safety language require manual
review even when site validation passes. Automated link and nav checks do not
prove the public claim boundary.
