# Site TraceMap Tools Manager FAQ Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public manager FAQ page for `tracemap.tools` that answers
skeptical stakeholder questions about what TraceMap can and cannot say from
deterministic static evidence. The page should be safe to share with managers,
reviewers, architects, and non-implementing stakeholders who need clear
boundaries before using TraceMap language in planning, review, or follow-up
discussions.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, or validation scripts.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

The future page may explain TraceMap's concept-level static evidence model:
rule IDs, evidence tiers, coverage labels, limitations, generated artifacts,
and proof paths. It must keep the reader oriented around what was found in
checked-in source and generated static artifacts, not what happened at runtime.

The future page must not claim that TraceMap proves runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, AI impact analysis, LLM analysis, or complete product
coverage. It must not imply TraceMap replaces telemetry, logs, traces, tests,
ownership, human review, or release process.

## Requirements

### Requirement 1: Publish a manager FAQ route

The future implementation shall publish a concept-level public FAQ page for
manager and stakeholder questions.

Acceptance criteria:

- The page uses either `/manager-faq/` or `/faq/manager/`.
- The implementation-state note records the final route choice and rationale.
- The page says `Public claim level: concept`.
- The page states the shared site principle: No public conclusion without
  evidence.
- The page is written for skeptical stakeholders who need to know what
  TraceMap can say, what it cannot say, and what should still be verified by
  other processes.
- The page uses an FAQ pattern with clear question headings and concise
  answers.
- The page uses existing static site layout, navigation, metadata, and
  accessibility patterns.
- The page does not introduce a runtime service, form, script dependency,
  client-side state, or scanner/reducer behavior.

### Requirement 2: Include the required FAQ question set

The FAQ shall answer common manager and stakeholder questions without upgrading
static evidence into proof of runtime or release safety.

Acceptance criteria:

- The FAQ answers what TraceMap can say from deterministic static evidence.
- The FAQ answers what TraceMap cannot prove from static evidence alone.
- The FAQ answers whether TraceMap proves production traffic, runtime behavior,
  endpoint performance, outage cause, release safety, or operational safety.
- The FAQ answers whether TraceMap replaces telemetry, logs, traces, tests,
  ownership, human review, or release process.
- The FAQ answers what rule IDs, evidence tiers, coverage labels, limitations,
  and proof paths mean for a manager.
- The FAQ answers what a reduced or partial coverage label means.
- The FAQ answers how managers should use TraceMap during review,
  prioritization, incident follow-up, or stakeholder communication.
- The FAQ answers what should be escalated to engineering owners, telemetry,
  tests, logs, traces, or release review.
- The FAQ answers why TraceMap does not use AI, LLMs, embeddings, vector
  databases, or prompt-based classification for the core scanner/reducer
  claim.
- Each answer keeps the conclusion tied to static evidence, coverage labels,
  rule IDs, limitations, or proof paths.

### Requirement 3: Preserve forbidden-copy boundaries

The page shall make non-claims visible and shall not use wording that implies
TraceMap proves stronger facts than deterministic static analysis can support.

Acceptance criteria:

- The page does not claim TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety, AI
  impact analysis, LLM analysis, or complete product coverage.
- The page does not imply TraceMap replaces telemetry, logs, traces, tests,
  ownership, human review, or release process.
- The page does not say or imply that a finding is `impacted`, `safe`,
  `unsafe`, `approved`, `blocked`, `root cause`, `validated for release`, or
  `production proven` unless the phrase is explicitly framed as something
  TraceMap does not claim.
- The page does not describe TraceMap as AI-powered, LLM-powered, intelligent
  impact analysis, automated release approval, operational assurance, or a
  production observability tool.
- The page does not use scare framing, blame language, or competitor-first
  positioning.
- The page explains that static evidence can help shape better questions and
  review paths, not settle every operational or product question.

### Requirement 4: Keep artifact safety public-safe

The FAQ shall publish only public-safe explanatory copy and links.

Acceptance criteria:

- The page does not publish raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, raw `facts.ndjson`, raw SQLite files, combined SQLite files,
  analyzer logs, or private sample names.
- The page may mention generated artifact names such as `scan-manifest.json`,
  `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log` only as
  artifact types or local outputs, not as published raw content.
- The page may use public-safe relative routes, sanitized labels, counts,
  hashes, rule IDs, evidence tiers, coverage labels, limitations, and
  generated summary names.
- The page makes clear that public summaries are presentation surfaces over
  deterministic evidence, not replacements for source facts, generated
  reports, limitations, or human review.
- If examples are included, they use sanitized concept examples or existing
  public demo proof paths only.

### Requirement 5: Link to related manager and proof surfaces

The FAQ shall connect manager-facing questions to existing public pages that
give more context or evidence.

Acceptance criteria:

- The page links to `/manager-brief/`.
- The page links to `/manager-packet/`.
- The page links to `/review-room/`.
- The page links to `/limitations/`.
- The page links to `/validation/`.
- The page links to `/proof-paths/`.
- The page may link to `/docs/`, `/demo/`, `/demo/result/`, `/packets/`, or
  `/capabilities/` when those routes support a specific FAQ answer.
- Links use public routes and do not expose local filesystem paths, private
  remotes, generated scan directories, or unpublished artifact locations.
- Link anchor text must not imply runtime proof, production proof, release
  approval, operational safety, or complete coverage.
- If any required route is unavailable at implementation time, the
  implementation shall either block the page until a safe public target exists,
  or link to the current closest equivalent public route and record the
  substitution, rationale, and intended future correction in
  `implementation-state.md`. Blocking is the safer default when no close
  equivalent exists.

### Requirement 6: Add discovery metadata without overstating claim level

The route shall be discoverable as a concept-level manager FAQ.

Acceptance criteria:

- The route appears in the site's page metadata with claim level `concept`.
- The route appears in generated sitemap output if comparable public pages are
  included there.
- The route appears in discovery metadata such as
  `site/src/_site/discovery.json`, or the equivalent path confirmed at
  implementation time, if comparable manager-facing public pages are included
  there.
- Page title, description, and social metadata describe a manager FAQ about
  deterministic static evidence boundaries.
- Social metadata titles stay at 70 characters or less and descriptions stay
  at 160 characters or less unless the existing site metadata pattern requires
  a different limit.
- Metadata does not claim runtime proof, production proof, release safety,
  operational safety, AI impact analysis, LLM analysis, or complete coverage.
- Relevant public pages may link to the FAQ where it helps stakeholders choose
  the right evidence surface, but those links must preserve the concept-level
  claim boundary.

### Requirement 7: Validate the future implementation

The future implementation shall run focused public-site validation and record
the result in this spec's implementation-state note.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Validate that `tasks.md` contains no checked boxes before merging the
  spec-only PR. Checked boxes are permitted only in the future implementation
  PR after the corresponding work is complete.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- For layout or interaction changes, run desktop and mobile browser sanity
  checks for the final FAQ route.
- Validate that the rendered page includes `Public claim level: concept`.
- Validate that the rendered page links to `/manager-brief/`,
  `/manager-packet/`, `/review-room/`, `/limitations/`, `/validation/`, and
  `/proof-paths/`.
- Validate that any optional supporting links added to `/docs/`, `/demo/`,
  `/demo/result/`, `/packets/`, or `/capabilities/` use non-overclaim anchor
  text consistent with the link anchor-text constraint in Requirement 5.
- Validate that the final FAQ route appears in discovery metadata
  (`site/src/_site/discovery.json`, or the equivalent path confirmed at
  implementation time) with `publicClaimLevel: concept`, and in sitemap or
  `pages.json` output if comparable manager-facing public pages are indexed
  there.
- Validate that discovery metadata title, description, and social copy stay
  within the concept claim boundary and contain no forbidden runtime,
  production, release-safety, operational-safety, or AI/LLM positioning.
- Validate rendered copy does not contain forbidden private or raw artifact
  material listed in this spec.
- Validate rendered copy does not contain forbidden AI/LLM positioning such as
  `AI impact analysis`, `LLM analysis`, `machine learning impact analysis`, or
  `artificial intelligence impact analysis`, except when explicitly framed as
  non-claims.
- Validate rendered copy does not contain runtime, production, or release
  overclaim wording such as `impacted`, `safe`, `unsafe`, `approved`,
  `blocked`, `root cause`, `validated for release`, or `production proven`,
  except where explicitly framed as TraceMap non-claims or boundary examples.
- A suitable starting forbidden-positioning pattern is
  `/\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact|automated release approval|operational assurance)\b/i`.
- The implementation may extend this pattern but must not reduce it without a
  spec amendment.
- A suitable starting overclaim pattern is
  `/\b(impacted|safe|unsafe|approved|blocked|root cause|production proven|validated for release|approved for release|proven behavior|statically proven|deployment[- ]safe|confirmed[- ]safe)\b/i`,
  applied to rendered FAQ body copy outside sanctioned non-claim or disclaimer
  blocks. Words such as `safe` and `unsafe` inside phrases like `not safe to
  say` count as sanctioned non-claim framing. Strong standalone uses of
  `proven` should be reviewed manually because legitimate limitation wording
  may also say what cannot be proven. The manual review result shall be
  recorded in `implementation-state.md` before the implementation PR is
  merged. The implementation may extend this pattern but must not reduce it
  without a spec amendment.
- Record validation commands, results, route choice, scope decisions,
  review-loop outcomes, and follow-up items in `implementation-state.md`.

### Requirement 8: Keep implementation state current

The future implementation shall keep this spec's status files aligned with the
actual work completed.

Acceptance criteria:

- `tasks.md` remains unchecked until implementation work begins.
- Implementation tasks are checked only after the corresponding implementation
  and validation work is complete.
- `implementation-state.md` is updated with current branch, target base, scope
  decisions, validation results, oddities, and follow-up items.
- If a Kiro review identifies Medium or higher findings, the implementation
  either patches them and records the rerun result or records why a rerun was
  not feasible.
