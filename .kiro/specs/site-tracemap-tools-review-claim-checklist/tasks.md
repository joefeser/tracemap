# Site TraceMap Tools Review Claim Checklist Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These implementation tasks are checked only after the corresponding site change or validation completed.

Note: validation tasks passed before implementation tasks were checked
complete.

- [x] Confirm spec review passed and all Medium+ findings are resolved before
  beginning implementation.
- [x] Choose the final placement: `/review-claim-checklist/`,
  `/claim-checklist/`, or an equivalent section on an existing governance page.
- [x] Record the selected placement and rejected alternates in
  `implementation-state.md`.
- [x] Add the concept-level checklist page or section using existing static
  site layout, metadata, accessibility, and navigation patterns.
- [x] Include `Public claim level: concept` and `No public conclusion without
  evidence` on the page or section.
- [x] Publish the required checklist fields: claim statement, public claim
  level, proof path, rule ID or rule family, evidence tier, coverage label,
  limitation, non-claims, source branch or main-dev status, owner follow-up,
  reviewer, review date, and decision.
- [x] Limit checklist-row public claim level values to the claim-ledger
  vocabulary: `shipped`, `demo`, `concept`, and `hidden`.
- [x] Keep checklist-row claim levels separate from route/discovery metadata:
  discovery `publicClaimLevel` values must use the existing discovery enum, and
  discovery `main` maps to checklist `shipped` instead of being emitted as
  `shipped` metadata.
- [x] Use only the canonical review outcome labels: `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- [x] Add stop conditions for missing proof path, private-only artifact, hidden
  claim detail, unsupported demo claim, and forbidden runtime/release/AI
  wording.
- [x] Require every repeatable claim to keep its proof path, rule ID or rule
  family, evidence tier, coverage label, limitation, and non-claims attached.
- [x] Link proof paths only to public-safe pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, demo artifacts, or
  proof path index entries.
- [x] Ensure proof paths do not link to raw facts, raw SQLite files, raw
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, or private sample
  names.
- [x] Require specific rule IDs where public-safe; otherwise require a rule
  family plus a limitation.
- [x] Use only the TraceMap evidence tier vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Keep coverage labels transcribed from the cited artifact or public-safe
  summary; do not normalize reduced, partial, unknown, unavailable, or
  future-only labels into stronger wording.
- [x] Show source branch or main-dev status as part of the proof path and avoid
  upgrading dev-only, future-only, hidden, or local-only evidence to shipped or
  demo wording.
- [x] Keep claim level separate from review outcome in copy, data, metadata,
  and validation.
- [x] Keep `hidden` rows abstract or omitted and avoid unreleased capability
  names, private sample identities, internal route names, hidden-export
  details, counts, cadence, sequencing, or in-flight status.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety, AI
  impact analysis, LLM analysis, and complete product coverage.
- [x] State that TraceMap does not replace telemetry, logs, traces, tests,
  source review, ownership decisions, incident response, or release approval.
- [x] Add a private-material checklist forbidding raw `facts.ndjson`, raw
  `index.sqlite`, analyzer logs, raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, and private sample names.
- [x] Forbid real internal reviewer names, real owner or assignee identities,
  and real internal review dates or cadence in example or template rows; use
  synthetic role-based placeholders for person fields.
- [x] Add agent-oriented wording that future agents must not repeat a claim
  after dropping its rule ID, evidence tier, coverage label, limitation, or
  proof path.
- [x] Link to `/review-room/` as the meeting agenda when that route exists.
- [x] Link to `/manager-faq/` as stakeholder explanation when that route
  exists.
- [x] Link to `/proof-paths/` as the evidence-trail index when that route
  exists.
- [x] Link to the claim ledger or roadmap claim-ledger surface as the source
  for claim-level vocabulary when that route or section exists.
- [x] Add at least one inbound link from an existing live governance or proof
  surface back to the checklist when implemented as a standalone route, and
  record deferred inbound links for concept-stage routes in
  `implementation-state.md`.
- [x] Record substitutions, omissions, or blocking decisions for referenced
  routes that do not exist at implementation time.
- [x] Avoid copying full evidence tables, claim ledgers, or FAQ answer sets
  from adjacent pages; link to them instead.
- [x] Use only synthetic or public/demo-sourced example claim rows, label them
  as examples, and validate they contain no private or in-flight material,
  real internal reviewer or owner identities, or real internal review dates.
- [x] Add stable checklist anchors suitable for cross-links and future
  automated review references.
- [x] Add standalone route metadata, sitemap metadata, and discovery metadata
  with `publicClaimLevel: concept` if the checklist is implemented as a
  standalone route.
- [x] Validate standalone-route discovery metadata exposes
  `publicClaimLevel: concept`, not only that the route is present.
- [x] Validate rendered page content includes the claim-level label, shared
  principle, required checklist fields, non-claims, and required available
  adjacent-page links.
- [x] Validate standalone-route inbound discoverability from at least one
  existing live adjacent governance or proof surface.
- [x] Manually verify the checklist links to adjacent pages instead of copying
  large evidence tables, claim ledgers, or FAQ answer sets.
- [x] Validate illustrative example rows are labeled as examples and contain no
  private, hidden, dev-only, in-flight material, real internal reviewer or
  owner identities, or real internal review dates.
- [x] Validate rendered text, decoded HTML, and metadata for forbidden runtime,
  production, release-safety, operational-safety, AI, and LLM positioning.
- [x] Validate rendered text, decoded HTML, and metadata for forbidden private
  or raw material listed in this spec.
- [x] Run `git diff --check` (may run in the spec phase; check this box only
  after future implementation re-runs it).
- [x] Run `./scripts/check-private-paths.sh` (may run in the spec phase; check
  this box only after future implementation re-runs it).
- [x] Implementation phase only: run `npm test` from `site/` after site source
  is added.
- [x] Implementation phase only: run `npm run validate` from `site/` after
  site source is added.
- [x] Implementation phase only: run `npm run build` from `site/` after site
  source is added.
- [x] Implementation phase only: run desktop and mobile browser sanity checks
  if layout or interaction changes are made.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, and follow-up
  items.
