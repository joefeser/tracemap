# Site TraceMap Tools Adoption Playbook Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public site page for introducing TraceMap into code review
workflows. The page may live at `/adoption/` or `/playbook/`, depending on the
site navigation pattern at implementation time.

This is a process and onboarding page, not a product guarantee. It should help
a team start with the public demo, identify a candidate repository, run
deterministic scans, read evidence packets, make analysis gaps explicit, and
decide follow-up ownership.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded adoption or playbook route

The site shall publish a concept-level public onboarding page for teams that
want to introduce TraceMap into review workflows.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states the shared site principle.
- The implementation chooses either `/adoption/` or `/playbook/` as the stable
  public route and records the route choice in this spec's
  `implementation-state.md`.
- The page explains that the playbook is a suggested review workflow, not a
  replacement for existing engineering judgment or organizational controls.
- The page addresses engineering leads, reviewers, staff engineers, managers,
  and repository owners who are evaluating whether deterministic static
  evidence can improve review conversations.
- The page fits an existing public site long-form pattern with a page hero,
  concise workflow sections, proof links, limitations, and a visible boundary
  section.
- The page should stay reader-friendly, roughly 500 to 1200 rendered words,
  and shall not exceed 1500 words without a spec amendment.
- The page follows the existing site accessibility baseline for heading
  hierarchy, color contrast, link text, and alt text for non-text content.
- Alt text, captions, image metadata, and discovery metadata must follow the
  same public claim boundaries as visible copy.
- The implementation should author the page from a neighboring static site
  page so it includes the replaceable `<header class="site-header">` block
  that the build replaces with canonical navigation.

### Requirement 2: Explain the adoption workflow steps

The page shall describe a bounded workflow for introducing TraceMap into review
practice without promising outcomes.

Acceptance criteria:

- The page includes a step to start with the public demo and public demo result
  before asking teams to scan private code.
- The page includes a step to identify a candidate repository or service area
  with a clear review question and known owner.
- The page includes a step to run deterministic scans against a repo and
  commit SHA, with generated artifacts treated as evidence to inspect rather
  than conclusions to accept blindly.
- The page includes a step to read evidence packets that expose rule IDs,
  evidence tiers, file paths, line spans, commit SHA, extractor versions,
  coverage labels, and documented limitations when those details are
  available.
- The page includes a step to make analysis gaps explicit, including reduced
  coverage, syntax fallback, missing project load, unavailable framework
  knowledge, or evidence that needs owner review.
- The page includes a step to decide follow-up ownership, such as repository
  owner review, runtime owner confirmation, test owner validation,
  documentation updates, or future extractor work.
- The page distinguishes deterministic static evidence from runtime telemetry,
  tests, release approval, incident response, governance, and human review.
- The page describes partial analysis as useful only when it is clearly labeled
  as partial.

### Requirement 3: Preserve concept-level claim boundaries

The page shall keep public claims bounded to TraceMap's deterministic static
evidence model and avoid stronger claims.

Acceptance criteria:

- The page explains that TraceMap produces deterministic static evidence with
  rule IDs, evidence tiers, coverage labels, limitations, and generated
  artifacts.
- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The page does not imply TraceMap replaces CI/CD, tests, telemetry, ownership,
  human review, release approval, incident response, or governance.
- The page avoids saying a change is impacted unless a reducer-backed result
  and evidence-backed public-safe material support that wording.
- The page may say TraceMap can help teams organize review questions around
  static evidence, gaps, and ownership, but must not say the playbook proves a
  change is safe or unsafe.
- The page avoids guarantee language such as `ensures`, `certifies`,
  `guarantees`, `proves release safety`, `prevents incidents`, or equivalent
  wording.

### Requirement 4: Link to existing proof and orientation surfaces

The page shall connect the onboarding workflow to existing public-safe routes
that provide examples, documentation, boundaries, and meeting structure.

Acceptance criteria:

- The page links to `/demo/` or the current public demo route.
- The page links to `/docs/` for artifact and evidence terminology.
- The page links to `/validation/` for validation expectations.
- The page links to `/limitations/` for public claim boundaries.
- The page links to `/proof-paths/` for evidence-backed proof surfaces.
- The page links to `/review-room/` for a meeting or review agenda if that
  route exists at implementation time.
- The page links to `/static-triage/` for static triage orientation if that
  route exists at implementation time.
- The five required routes (`/demo/`, `/docs/`, `/validation/`,
  `/limitations/`, and `/proof-paths/`) must resolve at implementation time.
  If any has moved or is unavailable, use the current equivalent public route
  and record the mapping in `implementation-state.md`.
- `/review-room/` and `/static-triage/` currently exist as public routes and
  should be linked unless they have moved or been removed at implementation
  time; record any route mapping or unresolved route gap in
  `implementation-state.md`.
- Link text must describe public-safe learning, review orientation, evidence
  inspection, or limitations; it must not imply runtime proof, production
  coverage, operational safety, or release approval.

### Requirement 5: Publish only public-safe copy and metadata

The page and any generated metadata shall avoid private, raw, or operationally
sensitive material.

Acceptance criteria:

- The page does not publish raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw `facts.ndjson`, raw `index.sqlite`,
  or analyzer logs.
- Examples use sanitized labels, public routes, public demo summaries,
  synthetic concept copy, hashes, counts, rule IDs, evidence tiers, coverage
  labels, and limitation wording where applicable.
- Discovery metadata, Open Graph text, sitemap data, search index data, and
  navigation labels must preserve `Public claim level: concept`.
- Page title and social title should stay at 70 characters or less unless the
  existing site metadata pattern requires a different limit.
- Page description and social description should stay at 160 characters or
  less unless the existing site metadata pattern requires a different limit.
- No metadata field may contain local paths, private repository names, raw
  remotes, secrets, raw artifact filenames, or stronger product claims than
  the rendered page.

### Requirement 6: Add discovery metadata and safe navigation

The route shall be discoverable as a concept-level onboarding page without
making it look like a production proof page.

Acceptance criteria:

- The route appears in the site's page metadata with claim level `concept`,
  including title, description, canonical URL, and Open Graph fields.
- The route is added to the sitemap source `site/src/_site/pages.json` so it
  appears in generated sitemap output, consistent with comparable public pages.
- The route is added to the discovery source `site/src/_site/discovery.json`
  with claim level `concept`, source type `site-page`, and a `hintCategory`
  that maps to an `llms.txt` route section, such as `use-case`. This ensures
  the route appears in the generated `routes-index.json` discovery output and
  the route section of `llms.txt`; the private `discovery.json` source must not
  be published to `dist`. `docs-index.json` is generated only from `repo-doc`
  entries, so the adoption page is not expected to appear there. The entry
  should mirror the field shape of comparable concept-level `site-page`
  entries, including title, summary, source type, hint category, preferred
  proof path, limitations, and non-claims.
- The generated `routes-index.json` discovery output and the sitemap entry
  preserve `Public claim level: concept` and the public-safe text boundaries.
- Relevant public pages may link to the route using safe anchor text such as
  `adoption playbook`, `review workflow playbook`, or `onboarding workflow`.
- Navigation or cross-link copy must not imply release approval, operational
  safety, runtime proof, production coverage, outage diagnosis, or complete
  dependency coverage.
- If the implementation adds a navigation item, it must fit the current site
  information architecture and remain visually consistent on desktop and
  mobile viewports.

### Requirement 7: Validate the future implementation

The future implementation shall run normal site validation and record results
in this spec's implementation state.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`, or record a gap in
  `implementation-state.md` if the script is not present at implementation
  time.
- Run `npm run build` from `site/`.
- Run `./scripts/check-private-paths.sh`.
- For layout or interaction changes, run desktop and mobile browser sanity
  checks.
- Validate rendered copy contains `Public claim level: concept` and the shared
  site principle.
- Validate rendered copy includes the selected route's required links or
  records route gaps in `implementation-state.md`.
- Implement validation as a dedicated page validator module under
  `site/scripts/` plus a matching `*.test.mjs`, following the existing
  per-page pattern used by neighboring validators such as
  `static-triage.mjs`, `review-room.mjs`, and `manager-brief.mjs`, and wire it
  into `npm run validate` and `npm test` rather than adding ad hoc checks.
  Wiring into `npm run validate` means importing the validator in
  `site/scripts/validate.mjs` and invoking it from `validateDist`.
- Forbidden-positioning validation shall use at least this canonical shared
  pattern:
  `/\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i`.
  The implementation may extend this pattern but must not reduce it without a
  spec amendment.
- Validation shall assert the rendered page contains the exact strings
  `Public claim level: concept`, the shared site principle
  `No public conclusion without evidence`, and one exact partial-analysis
  sentence chosen by the implementation that asserts partial analysis is useful
  only when clearly labeled as partial; the validator shall assert that same
  literal sentence.
- Private/raw-text validation shall cover at least the shared site page
  denylist categories used by neighboring per-page validators, including local
  paths, file URLs, localhost addresses, raw fact/index/log artifact names, raw
  SQL/source snippet wording, connection strings, secrets, credential-like
  labels, raw remotes, generated scan directories, and private sample names.
  The implementation may mirror the neighboring inline denylist pattern, or it
  may introduce a shared exported denylist module if it updates neighboring
  validators to consume that shared module in the same change and records the
  decision in `implementation-state.md`.
- The validator shall assert the route appears in generated
  `routes-index.json` with source type `site-page`, public claim level
  `concept`, the chosen `hintCategory`, and preferred proof path, and shall
  confirm the route is rendered in the `llms.txt` route section, mirroring the
  routes-index assertions in neighboring validators.
- Confirm rendered page word count is between 500 and 1500 words. Both bounds
  are enforced: the validator shall fail if word count is below 500 or above
  1500. The 1200-word value is the authoring target from Requirement 1.
  Measure from rendered visible text; `wc -w` on the rendered HTML body is
  acceptable.
- Record implementation scope, selected route, validation commands, results,
  oddities, and follow-up items in `implementation-state.md`.
