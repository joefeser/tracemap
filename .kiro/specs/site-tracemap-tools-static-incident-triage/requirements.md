# Site TraceMap Tools Static Incident Triage Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Add a public site concept phase for a static incident triage page at
`/static-triage/`. The page is for engineers and incident leads on a
production incident or P1 call who need to orient static code questions quickly
without pretending static evidence is telemetry.

Core angle: when someone asks what depends on this endpoint, package,
configuration surface, or SQL-facing surface, TraceMap can frame the static
evidence checklist and the gaps.

This phase is distinct from `/incident-call/`: `/static-triage/` focuses on the
engineer's triage checklist and handoff questions, not manager orientation or a
general incident-call narrative.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded static triage route

The site shall publish a concept-level static incident triage page at the stable
public route `/static-triage/`.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states the shared site principle.
- The primary copy addresses engineers and incident leads on a P1 or production
  call who need to orient static code questions quickly.
- The page explains that TraceMap can frame a static evidence checklist and
  the gaps around an endpoint, package, configuration surface, SQL-facing
  surface, route, handler, DTO, or nearby dependency surface.
- The copy uses concept-level language and does not imply a shipped operational
  workflow, runtime monitor, incident diagnosis, or production safety system.
- The page distinguishes static source evidence from telemetry, logs, traces,
  metrics, tests, and service-owner judgment.

### Requirement 2: Preserve static evidence boundaries

The page shall keep public claims bounded to deterministic static evidence and
clearly state what the use case cannot prove.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM impact analysis, or complete product coverage.
- The page says TraceMap can help a reader inspect rule IDs, evidence tiers,
  coverage labels, limitations, generated artifact families, file paths, line
  spans, commit SHA, and extractor versions when those details are available in
  public-safe summaries.
- The page avoids saying an endpoint, package, config surface, SQL-facing
  surface, or dependency is affected unless a reducer-backed result and
  evidence are present in public-safe material.
- The page clearly distinguishes analysis gaps and reduced coverage from clean
  or complete evidence.
- The page keeps the wording focused on triage orientation, checklist framing,
  and handoff questions.

### Requirement 3: Provide an engineer-facing checklist and handoff shape

The static triage page shall help a reader turn a live question into a bounded
static evidence checklist.

Acceptance criteria:

- The page includes a checklist for questions such as: what named surface are
  we investigating, which static references were found, what evidence tier
  produced them, what gaps remain, and what should be handed to runtime owners.
- The page includes handoff questions that separate static repository evidence
  from telemetry, logs, traces, incident timeline, owner confirmation, tests,
  and release process.
- The page makes clear that partial static evidence is useful when labeled as
  partial.
- The page links to `/incident-call/` while explaining that `/static-triage/`
  is the engineer checklist and handoff page.
- The page states: `Static triage is the engineer checklist and handoff page,
  distinct from the incident-call orientation page.`

### Requirement 4: Link to proof paths and limitations

The static triage page shall guide readers from the checklist into TraceMap's
public-safe proof surfaces.

Acceptance criteria:

- The page links to `/proof-paths/`, `/validation/`, `/docs/`,
  `/limitations/`, `/demo/result/`, and `/incident-call/` unless one of those
  routes is unavailable at implementation time, in which case the gap is
  documented in `implementation-state.md`.
- Any proof-path callout describes static source-to-artifact evidence, not live
  production confirmation.
- The page includes a clear path from a named surface to nearby static evidence
  such as routes, handlers, DTOs, config/project surfaces, SQL references,
  package references, cross-app references, rule IDs, evidence tiers, and
  limitations when public-safe evidence supports those examples.

### Requirement 5: Publish only public-safe summaries

The implementation shall use public-safe generated summaries, demo evidence, or
authored concept copy, not raw private scan artifacts.

Acceptance criteria:

- The page and metadata do not publish raw `facts.ndjson`, `index.sqlite`,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, or private
  sample identities.
- Public examples are derived from demo-safe summaries or synthetic concept
  copy grounded in existing public proof surfaces.
- Any snippet-like text is authored explanatory copy or approved public demo
  material.
- The page makes clear that private repositories require private scans and
  private review before evidence becomes public copy.

### Requirement 6: Add discovery metadata and validation

The route shall be discoverable while preserving its claim level and
public-safety boundaries.

Acceptance criteria:

- Discovery metadata labels `/static-triage/` as `concept`.
- Page metadata includes a title, description, canonical URL, and Open Graph
  fields for the route.
- Sitemap and route-index metadata include `/static-triage/`.
- Existing relevant public pages may link to the route with safe anchor text
  that does not imply runtime proof, release approval, production safety, or
  complete product coverage.
- The `/incident-call/` page shall link to `/static-triage/` with safe anchor
  text such as `static triage checklist`.
- Validation checks confirm the route renders, includes required claim text and
  links, avoids forbidden positioning/private text, and stays between 400 and
  1500 rendered words.
- Validation confirms the rendered page contains `static evidence checklist`,
  `evidence tier`, `handoff questions`, `partial static evidence is useful
  when labeled as partial`, and the engineer-checklist distinction phrase from
  Requirement 3.
- Validation asserts the rendered page contains the exact phrase: `Static
  triage is the engineer checklist and handoff page, distinct from the
  incident-call orientation page.`
- Word count validation uses normalized rendered text and must report when the
  page is below 400 words or above 1500 words.
- Forbidden private/raw-artifact validation shall cover at least the shared
  site page denylist used by neighboring validators: local paths, file URLs,
  localhost addresses, raw fact/index/log artifact names, raw SQL/source
  snippet wording, connection strings, secrets, and credential-like labels.
- Validation checks confirm required internal links resolve in generated output.
- Implementation validation includes `git diff --check`, `npm test` from
  `site/`, `npm run validate` from `site/`, `npm run build` from `site/`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity when
  feasible.
