# Site TraceMap Tools Build Review Workflow Story Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design defines a future public `tracemap.tools` article or page about how
TraceMap is being built with review pressure and coordination. The article is a
process story: specs shape implementation, implementation creates reviewable
diffs, Kiro and PR reviewers challenge the packet, ACK/agent-control organizes
review-loop state, and public claims stay attached to evidence and limitations.

The article is not a product feature, runtime proof, release proof, vendor
endorsement, certification, benchmark, autonomous review claim, or AI/LLM
impact-analysis claim.

## Information Architecture

Preferred placement is a blog article:

- `/blog/building-tracemap-under-review-pressure/`

Allowed alternatives:

- `/building-tracemap-under-review-pressure/` if the site has a non-blog
  article pattern at implementation time.
- A section on an existing build, review, or claim-governance surface if the
  article would otherwise duplicate nearby content.

The implementation should choose the placement that best preserves the
article's process-story shape. Standalone blog placement is recommended because
the topic is narrative and time-bound, while the underlying principles remain
evergreen. If a different placement is chosen, record the final route, rejected
alternates, metadata consequences, and inbound-link decisions in
`implementation-state.md`.

The repository already has a closely related article at
`/blog/building-tracemap-with-codex-kiro-qodo/`. Future implementation must
reconcile with that page before adding another article. The new material should
be differentiated by ACK/agent-control review-loop coordination, claim-level
discipline, and evidence-led spec lessons, or the implementation should choose
to extend or supersede the existing article instead. Record the decision,
rejected options, slug/title choice, cross-link plan, and duplicate-content
avoidance in `implementation-state.md`.

If the new article complements rather than supersedes the existing article, it
should link to the existing article for shared Codex, Kiro, and Qodo basics
and keep its own body focused on the differentiators: review-loop
coordination, claim-level discipline, evidence-led spec lessons, explicit
non-claims, and the validation checklist. Differentiation must include
blog-index category and card copy where those fields exist, not only a
different slug and title.

Primary navigation should not change unless the existing site pattern already
promotes blog or article pages in navigation. Discovery should come through the
blog index, sitemap, article metadata, related proof or review surfaces, and
bounded inline links.

## Article Model

Recommended title:

`Building TraceMap Under Review Pressure`

Recommended deck:

`A concept-level note on using specs, review loops, and deterministic
validation to keep public claims attached to evidence.`

Required visible labels:

- `Public claim level: concept`
- `No public conclusion without evidence`

Recommended sections:

1. Claim-level note.
2. The pressure that shaped the workflow.
3. Specs before implementation.
4. Implementation with reviewable diffs.
5. Kiro, Qodo, and ACK review loops, or generic review-loop coordination if
   ACK/agent-control is not publicly nameable.
6. What the workflow does not prove.
7. Lessons for evidence-led specs.
8. Validation and publication checklist.

The article should read as tasteful public process writing. It can be specific
about classes of work, review roles, and lessons learned, but it should not
depend on private chronology, private sample names, internal paths, hidden
review thresholds, raw bot comments, raw command output, or session IDs.

## Content Guidance

### Claim-level note

State that this is concept-level public writing about the way the project is
being built. Explain that the article describes workflow lessons and public
claim discipline, not shipped TraceMap behavior.

### The pressure that shaped the workflow

Describe review pressure as a productive constraint: every public statement
needs evidence, limitations stay attached, partial states remain visible, and
future claims should not be upgraded by tone.

Avoid drama, blame, scorekeeping, or tool-ranking language.

### Specs before implementation

Explain that specs can be useful before implementation when the work affects
public wording, validation, navigation, claim level, or handoff expectations.
Connect this to deterministic TraceMap principles: no public conclusion without
evidence and no rule without documented limitations.

### Implementation with reviewable diffs

Describe implementation as small, reviewable diffs with validation evidence.
Do not imply that Codex itself proves correctness or that implementation
assistance is part of TraceMap's scanner or reducer.

### Kiro, Qodo, and ACK review loops

Describe the review roles carefully:

- Codex can assist with implementation and patching in the development
  workflow.
- Kiro can review spec packets and pressure ambiguous requirements before code
  is written.
- Qodo can participate in PR review and surface actionable findings.
- ACK/agent-control can coordinate review-loop state, stop reasons, patch
  passes, validation evidence, and merge-readiness handoff.

Every mention should avoid endorsement or certification. The article should
say that these are workflow participants or review pressures, not authorities
that make product claims true.

Before naming ACK/agent-control in public copy, implementation must confirm it
is publicly nameable. If it is internal, unreleased, or not appropriate to name
directly, use a generic phrase such as `review-loop coordination layer` and
record that decision in `implementation-state.md`.

### What the workflow does not prove

This section must be explicit. The workflow does not prove runtime behavior,
production traffic, endpoint performance, outage cause, release approval,
release safety, operational safety, complete coverage, or AI/LLM impact
analysis. It does not replace telemetry, logs, traces, tests, source review,
code ownership, incident response, release controls, or human judgment.

Because this section necessarily contains otherwise-forbidden phrases, future
implementation must mark it with the established non-claim region convention,
such as `data-non-claim-region`, or use an equivalent negated-pattern
validator convention already present in the site. Rejected wording examples, if
any, must use `data-rejected-pattern-region` or the equivalent existing
validator convention.

### Lessons for evidence-led specs

Use compact lessons:

- State claim level early.
- Keep acceptance criteria tied to observable evidence.
- Treat review findings as pressure to clarify, not proof by themselves.
- Record limitations and reduced coverage.
- Make validation commands and browser checks part of the handoff.
- Keep public-safe summaries separate from raw private artifacts.

### Validation and publication checklist

End with a short checklist that future contributors can apply before
publishing similar process articles:

- visible concept claim level;
- shared evidence principle;
- no forbidden product claims;
- no endorsement claims;
- no private material;
- route, metadata, sitemap, and discovery checks;
- validation results recorded in the implementation state.

## Metadata and Discovery

If implemented as a blog article, use the existing blog metadata pattern for
title, description, canonical URL, Open Graph fields, published date, article
index card, sitemap metadata, and any discovery record used for articles. The
visible in-body label `Public claim level: concept` follows the proof-path
article's established pattern and is the source of truth for blog placement
unless the blog schema is deliberately extended.

Recommended metadata:

- Title: `Building TraceMap Under Review Pressure`
- Description: `A concept-level workflow story about using specs, review
  loops, and deterministic validation to keep TraceMap public claims attached
  to evidence.`
- Public claim level: visible body label `Public claim level: concept` for
  blog placement; existing `publicClaimLevel: concept` field only if the final
  placement is discovery-tracked or the blog metadata schema is deliberately
  extended
- Hint category: use the existing article, blog, governance, or workflow
  category available in site metadata. If no exact category exists, choose the
  closest existing category and record the choice.
- Preferred proof path: closest existing public-safe claim-guardrail,
  review-checklist, proof-path, or validation route when the selected metadata
  schema supports such a field. Do not invent a proof-path metadata field for
  blog articles without a deliberate schema and validator update.
- Non-claims: runtime behavior, production traffic, endpoint performance,
  outage cause, release approval, release safety, operational safety, complete
  coverage, AI/LLM impact analysis, vendor endorsement, autonomous review, and
  replacement of human judgment.

Do not add metadata that implies shipped product behavior, customer adoption,
external endorsement, tool certification, runtime proof, release readiness, or
complete coverage.

## Link Map

Future implementation should link only to routes that exist at implementation
time. Candidate adjacent surfaces:

- `/review-claim-checklist/`
- `/site-claim-guardrails/`
- `/proof-paths/`
- `/proof-source-catalog/`
- `/validation/`
- `/limitations/`
- `/roadmap/`
- `/packets/`
- `/team-evidence-handoff/`
- `/review-room/`
- `/blog/`

The article should not copy large tables or checklists from adjacent pages.
Use concise links and record missing, renamed, or deferred links in
`implementation-state.md`.

If none of the candidate adjacent routes exist at implementation time, the
article must at minimum link to `/blog/` or the site root and record the gap in
`implementation-state.md`. The article must not be published with no outbound
links to any site surface.

## Validation Design

Future implementation should add focused validation that checks:

- route or section exists in generated output;
- visible `Public claim level: concept`;
- visible `No public conclusion without evidence`;
- required article sections are present;
- body word count is between 700 and 1600 rendered words, or within a recorded
  site-constrained range of at least 500 words and at most 1800 words
  documented in `implementation-state.md`;
- metadata, sitemap output, blog index data, discovery metadata, canonical URL,
  and Open Graph fields remain concept-level and process-focused;
- internal links resolve when present;
- preferred proof path points to a public-safe route when supported;
- no primary-navigation addition appears unless justified in
  `implementation-state.md`;
- forbidden claim patterns are absent from rendered site output outside
  explicit non-claim or rejected-example regions; spec source files under
  `.kiro/specs/` are not in scope;
- forbidden-wording exceptions use existing region-marker conventions, such as
  `data-non-claim-region` and `data-rejected-pattern-region`, or equivalent
  negated patterns already used by site validators;
- blog implementation follows the existing per-article validator and slug
  collision-control pattern where that pattern still exists;
- forbidden private material is absent from rendered text, decoded HTML, raw
  HTML attributes, metadata, sitemap output, discovery output, fixtures,
  validation messages, and article data;
- screenshots or media, if any, are public-safe and do not show private tools,
  raw logs, paths, transcripts, dashboards, or run artifacts;
- validation commands and results are recorded in `implementation-state.md`.

Implementation validation should include:

- `npm test` from `site/`;
- `npm run validate` from `site/`;
- `npm run build` from `site/`;
- `git diff --check` from the repository root;
- `./scripts/check-private-paths.sh` from the repository root;
- desktop and mobile browser sanity checks when layout or interaction changes
  are made.

## Accessibility and Layout

The future implementation should reuse existing article layout and static-site
components. Headings should be hierarchical, link text should identify the
destination, and any checklist should remain readable on mobile.

No interactive workflow runner, raw review viewer, transcript browser,
artifact downloader, agent integration, runtime service, upload flow, or
client-side state is part of this spec.
