# Site TraceMap Tools Incident Evidence Handoff Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design describes a future public-safe concept page at
`/incident-evidence-handoff/`. The page should give incident-adjacent readers a
compact static evidence packet they can carry into a P1 call, production review,
or follow-up thread without treating static analysis as telemetry or runtime
truth.

The implementation is site-only. It does not add scanner behavior, reducer
behavior, runtime monitoring, incident diagnosis, release approval, ownership
automation, AI impact analysis, or production proof.

## Route and Placement

- Route: `/incident-evidence-handoff/`
- Source file: `site/src/incident-evidence-handoff/index.html`
- Page type: concept-level public site page
- Public claim level: `concept`
- Sitemap metadata: add the route to `site/src/_site/pages.json`
- Discovery metadata: add the route to `site/src/_site/discovery.json` with
  publicClaimLevel concept, hintCategory use-case, sourceType site-page,
  preferredProofPath `/proof-paths/`, limitations, and non-claims
- Navigation: reuse the canonical site navigation, but do not add the route to
  top navigation in this phase

The route should be short enough to share in incident-adjacent conversations and
specific enough to avoid competing with the existing `/incident-call/`,
`/static-triage/`, `/review-room/`, `/manager-faq/`, `/packets/`,
`/manager-packet/`, `/manager-brief/`, and `/use-cases/incident-review/`
routes.

## Content Structure

1. Hero: name the handoff packet, show `Public claim level: concept`, and state
   `No public conclusion without evidence`.
2. Packet purpose: explain that the page helps a reader bring static evidence,
   proof paths, limits, and next owners into a conversation.
3. Differentiation band: state how this route differs from `/incident-call/`,
   `/static-triage/`, `/review-room/`, `/manager-faq/`, `/packets/`,
   `/manager-packet/`, `/manager-brief/`, and `/use-cases/incident-review/`.
4. Handoff checklist: present the fields static evidence, proof path,
   rule ID/evidence tier, coverage label, limitation, and next owner.
5. Ownership split: map static questions to TraceMap evidence and map runtime,
   release, test, telemetry, database, and service-owner questions to their
   appropriate owners.
6. Boundaries: list explicit non-claims and artifact safety rules. The
   non-claims list must include, at minimum: runtime behavior, production
   traffic, endpoint performance, outage cause, release safety, operational
   safety, AI impact analysis, LLM analysis, complete product coverage, and
   production dependency understanding.
7. Replacement boundaries: state TraceMap does not replace, at minimum:
   telemetry, logs, traces, APM, incident command, ownership review, tests,
   release controls, service-owner judgment, database-owner judgment, and
   source review.
8. Link section: send readers to proof paths, validation, limitations, demo
   result, docs or a future public rule catalog when rule/extractor context is
   needed, and neighboring incident/review pages.

## Required Rendered Copy

The page should include this exact line so validation can lock the route's
distinct purpose:

`Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.`

The distinction line must render as a single logical line that matches after
whitespace normalization; the line break in this spec is for readability only.

The page should also include this exact static-triage distinction line:

`Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.`

This line must also render as a single logical line that matches after
whitespace normalization.

Implementation and validation must treat both distinction lines in this section
as mandatory rendered copy.

The page should also render these checklist labels exactly:

- `static evidence`
- `proof path`
- `rule ID/evidence tier`
- `coverage label`
- `limitation`
- `next owner`

The page must also render the shared site principle:

`No public conclusion without evidence`

## Proof and Evidence Model

The handoff packet should connect each public-safe static evidence item to:

- the source-to-artifact proof path that supports it;
- the rule ID and evidence tier when public-safe summaries provide them;
- the coverage label and known limitation;
- the line-span, file-path, commit-SHA, or extractor-version metadata only when
  those details are already public-safe and derived from public-safe generated
  summaries, not from private repository scans;
- the next owner for questions static evidence cannot answer.

Examples may reference routes, handlers, DTOs, package references,
configuration surfaces, SQL-facing references, cross-app references, dependency
edges, and reducer-backed findings only when the supporting public-safe
material exists. Concept examples should remain generic and should not imply a
specific production incident, private customer, private sample, or deployed
service.

## Neighbor Route Relationship

- `/incident-call/`: orientation for someone asking what static dependencies
  surround a named surface during a P1 call.
- `/static-triage/`: engineer checklist for framing the static triage question.
  Static triage may call itself a handoff page; this route must describe itself
  as the packet of already-framed evidence carried into the next conversation.
- `/review-room/`: meeting agenda for deciding what evidence is known,
  partial, or missing.
- `/manager-faq/`: manager-facing answers to common TraceMap questions.
- `/packets/`: general guide to reading TraceMap evidence packets.
- `/manager-packet/`: standing manager-facing packet for the broader TraceMap
  value story.
- `/manager-brief/`: manager problem brief, not a specific incident-adjacent
  evidence transfer packet.
- `/use-cases/incident-review/`: incident-review orientation narrative rather
  than the bringable static evidence packet.
- `/incident-evidence-handoff/`: packet/checklist for handing static evidence,
  proof paths, limits, and next owners into the next conversation.

Cross-links should use role-specific anchor text, such as incident evidence
handoff packet, static triage checklist, or review-room agenda, and must not
imply runtime proof, incident cause, release approval, or operational safety.

## Public Safety

The page and metadata must not publish raw fact streams, raw SQLite databases,
analyzer logs, raw source snippets, raw SQL, config values, secrets, local
absolute paths, raw repository remotes, generated scan directories, private
sample names, connection strings, credentials, or local command output.

The page may publish authored concept copy and public-safe generated summaries.
Any private repository evidence must remain private until reviewed and
sanitized for public use.

## Validation Design

Future implementation should add a focused validator following the neighboring
concept validator patterns. The validator should check:

- route output exists for `/incident-evidence-handoff/`;
- rendered text contains the required claim-level label, shared principle,
  distinction line, and checklist labels;
- metadata contains canonical, title, description, Open Graph fields including
  `og:type`, and article-style type where neighboring concept pages use that
  pattern;
- discovery metadata has `publicClaimLevel: concept` and bounded limitations
  and non-claims;
- generated `routes-index.json` has `publicClaimLevel: concept`,
  `hintCategory: use-case`, `sourceType: site-page`, and
  `preferredProofPath: /proof-paths/`;
- sitemap metadata includes the route;
- required links are present and resolve in generated output. Every
  `/`-relative link in the rendered page must have a corresponding
  `routes-index.json` route or `pages.json` sitemap entry unless a documented
  gap in `implementation-state.md` explains the target choice;
- ownership split contains at minimum the required static-side rows route
  existence, DTO shape, package reference, dependency edge, and SQL-facing
  reference, plus runtime/release-side rows telemetry, logs, traces, APM,
  release controls, tests, database ownership, service ownership, and incident
  command;
- rendered word count stays between 400 and 1800 words; the upper bound is a
  guard against scope drift, not a style target, and a complete,
  non-redundant packet with all required sections should typically land between
  600 and 1200 words;
- rendered word count means visible body text after stripping HTML tags,
  navigation, footer, sidebar, and metadata elements. It excludes `<title>`,
  `<meta>` content, `alt` attributes, and global navigation/footer text. The
  lower bound of 400 words is a sanity check that required sections have not
  been inadvertently removed; required sections must pass their own checks
  independently;
- word-count enforcement must not cause removal of the mandatory distinction
  lines or checklist labels; those elements are required regardless of total
  word count, and validation must check for their presence independently of the
  word-count result;
- if rendered word count is outside the 400 to 1800 word range, validation must
  fail with a clear word-count error. Authors must resolve overage by trimming
  nonmandatory copy, not by removing required distinction lines, checklist
  labels, ownership rows, proof-path links, or boundary statements;
- denylist checks are scoped to the `/incident-evidence-handoff/` route only,
  not to full site output, spec source, validator source, neighboring pages, or
  validator comments. The denylist phrase inventory below must be replicated
  inline in validator source, and the three groups are organizational sections,
  not optional rollout phases. All groups must be active in the same validator
  and covered by the same negative test suite.
- Non-claim overclaim phrases checked against normalized rendered body text,
  decoded HTML attributes, and public metadata values: proves runtime behavior,
  proves production traffic, endpoint performance proof, proves outage cause,
  proves release safety, proves operational safety, AI-powered, LLM-powered, AI
  impact analysis engine, LLM impact analysis engine, complete product coverage,
  and production dependency understanding.
- Replacement-boundary overclaim phrases checked against normalized rendered
  body text, decoded HTML attributes, and public metadata values: replaces
  telemetry, replaces logs, replaces traces, replaces APM, replaces incident
  command, replaces incident response, replaces ownership, replaces ownership
  review, replaces tests, replaces release controls, replaces service-owner
  judgment, replaces database-owner judgment, and replaces source review.
- Private/raw artifact exposure phrases checked against raw HTML, decoded HTML
  attributes, metadata values, and rendered body text: raw fact stream, raw
  SQLite, analyzer log, raw source snippet, raw SQL, raw config value,
  credential secret, local absolute path, raw remote, generated scan directory,
  private sample name, connection string, and credential. Boundary statements
  may use category words such as config values or secrets only when they do not
  match these exposure phrases.

Implementation validation should include the standard site checks, private-path
guard, and desktop/mobile browser sanity checks for layout and overflow.
