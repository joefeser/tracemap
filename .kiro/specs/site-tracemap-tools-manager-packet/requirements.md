# Site TraceMap Tools Manager Packet Requirements

Status: implemented
Readiness: implemented

Public claim level: demo

## Objective

Publish a manager-facing packet page at `/manager-packet/` that answers
"what does TraceMap solve for us?" for engineering managers, reviewers,
architects, and engineering leads. The page should explain how TraceMap helps
teams have a concrete static-evidence conversation before claiming impact,
safety, or production behavior.

This is a site-only phase. It does not add scanner or reducer behavior, and it
does not claim runtime observability, production telemetry, incident response,
code ownership, release approval, or AI impact analysis.

## Why This Phase Exists

The current public site has detailed proof surfaces such as `/demo/result/`,
`/demo/proof-upgrades/`, `/packets/`, `/capabilities/`, and `/roadmap/`. Those
pages are useful but still require readers to assemble the higher-level value
story themselves. The manager packet should give leaders and reviewers a short,
dense, evidence-oriented entry point that stays attached to rule IDs, coverage
labels, generated summaries, gaps, and limitations.

## Route And Surface

- Route: `/manager-packet/`
- Source file: `site/src/manager-packet/index.html`
- Sitemap metadata: add `/manager-packet/` to `site/src/_site/pages.json`
  with `changefreq: "monthly"` and `priority: "0.8"`.
- Cross-links: add selective links from existing discovery and packet surfaces
  where they help readers move between the higher-level packet and detailed
  proof pages.

## Audiences

- Engineering managers who need to know whether a review conversation has
  static evidence, visible gaps, and clear limits.
- Reviewers who need to trace a summary back to rule IDs, evidence tiers,
  coverage labels, source spans, and generated artifacts.
- Architects who need to see coupling and dependency surfaces without turning
  static evidence into runtime topology.
- Engineering leads who need a safe explanation of where TraceMap helps and
  where human review, telemetry, ownership, and release process still matter.

## Requirements

### Requirement 1: Publish the manager packet page

The site shall publish a page at `/manager-packet/` using existing static site
patterns.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page answers what TraceMap solves for teams in manager-facing language.
- The page keeps the answer evidence-oriented instead of sales-oriented.
- The page links to `/demo/result/`, `/demo/proof-upgrades/`, `/packets/`,
  `/capabilities/`, `/roadmap/`, and `/limitations/`.
- The page does not introduce a runtime service, form, script dependency, or
  client-side state.

### Requirement 2: Explain the evidence conversation

The page shall explain that TraceMap helps teams discuss static evidence before
claiming impact or safety.

Acceptance criteria:

- The page states that TraceMap can show static dependencies, coverage labels,
  rule IDs, generated summaries, gaps, and limitations from checked-in public
  demos.
- The page describes how managers and reviewers can use those artifacts to ask
  better review questions.
- The page distinguishes generated summaries from raw artifacts such as
  `facts.ndjson` and `index.sqlite`.
- The page says evidence packets are a way to inspect the review trail, not a
  replacement for source facts, human review, or release approval.

### Requirement 3: Preserve public claim boundaries

The page shall keep non-claims visible and conservative.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, deployment
  state, endpoint performance, production dependency understanding, release
  safety, incident root cause, or AI impact analysis.
- The page does not imply TraceMap replaces Dynatrace, production telemetry,
  incident response, code ownership, human review, test results, or release
  approval.
- The page does not imply TraceMap proves a P1 root cause, proves an endpoint
  is bad, or validates a release.
- The page describes TraceMap as static evidence that can orient questions
  during reviews or incident follow-up.

### Requirement 4: Keep artifacts public-safe

The page shall summarize only public-safe evidence and source paths.

Acceptance criteria:

- The page does not publish raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repo remotes, raw `facts.ndjson`,
  `index.sqlite`, combined SQLite files, generated scan directories, analyzer
  logs, or private sample identities.
- The page may mention public-safe generated summaries and checked-in public
  demo source paths.
- The page keeps limitations and coverage labels visible alongside conclusions.

### Requirement 5: Make the page discoverable

The page shall be discoverable from relevant public site surfaces without
overloading the primary navigation.

Acceptance criteria:

- `/` links to `/manager-packet/` from the existing `#packets` callout section.
- `/packets/` links to `/manager-packet/` as the higher-level reader path.
- `/capabilities/` links to `/manager-packet/` near claim boundaries or source
  material.
- `/demo/proof-upgrades/` links to `/manager-packet/` as a non-technical reader
  path.
- `/manager-packet/` is included in sitemap metadata.
- The `/manager-packet/` top navigation exactly matches the canonical site
  navigation enforced by `site/scripts/build.mjs` and `npm run validate`.

## Validation Plan

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run a desktop browser sanity check for `/manager-packet/`.
- Run a mobile browser sanity check for `/manager-packet/`.
- Confirm generated `site/dist/` output is build output and not hand-edited.

## Artifact Safety Rules

Safe to publish or summarize:

- Public routes on `tracemap.tools`.
- Public-safe demo summaries and generated report names.
- Checked-in public demo script or fixture paths.
- Rule/status framing, evidence tiers, coverage labels, gap counts, and
  limitations already visible in public demo pages.

Do not publish:

- Raw source snippets, raw SQL, config values, secrets, raw remotes, local
  absolute paths, generated scan directories, raw facts streams, SQLite files,
  analyzer logs, combined SQLite files, or private sample identities.

## Claim Boundaries

Use:

- "TraceMap helps teams have a concrete static-evidence conversation before
  claiming impact or safety."
- "The public page summarizes demo evidence from checked-in samples and links
  to the detailed proof pages."
- "Coverage labels, rule IDs, gaps, and limitations stay visible."

Avoid:

- "TraceMap proves production impact."
- "TraceMap validates releases."
- "TraceMap replaces telemetry, incident response, ownership, or approval."
- "TraceMap uses AI to decide impact."
