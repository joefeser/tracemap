# Site TraceMap Tools Blog Proof Path Series Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Summary

Use the existing static blog system to publish one or more concept-level
articles about proof paths and deterministic static evidence. The phase should
feel like a practical reading guide, not a marketing launch, runtime assurance
page, or raw artifact browser.

Design stays intentionally narrow: select article count and slugs, write
bounded public copy, register metadata, validate links/claim boundaries, and
record decisions. Do not change scanner/reducer behavior or publish raw
artifacts.

## Recommended Article Strategy

Start with one strong article if the future implementation wants the smallest
reviewable change. A natural first article is `What a proof path is`, because
it can point readers to `/proof-paths/`, `/proof-source-catalog/`,
`/evidence/`, `/limitations/`, and `/review-claim-checklist/` without needing
new public proof.

Expand to a short series only when each article has a distinct job:

- Definition article: what a proof path is and why it keeps claims reviewable.
- Reading article: how to move from public claim to evidence tier, limitation,
  and follow-up question.
- Review article: what static evidence can bring before telemetry is available,
  while preserving the runtime handoff boundary.
- Principle article: why "no public conclusion without evidence" prevents
  accidental overclaiming.

The implementation-state note should record rejected article ideas as well as
selected ones. Rejection is a feature here; a bounded series is better than a
wide set of overlapping posts.

## Required Article Structure

Each article should use this shape unless the implementation-state note records
a narrow reason to adapt it:

- Opening problem.
- Evidence-backed claim example.
- Proof-path reading steps.
- Limitations and non-claims.
- Safe language examples.
- Unsafe language examples clearly labeled as wording to avoid.
- Links to relevant proof surfaces.
- Closing handoff/action.

The article set should avoid repeating the current blog posts:
`why-tracemap-exists`, `what-tracemap-solves-for-engineering-teams`, and
`building-tracemap-with-codex-kiro-qodo`. Those posts can be linked as
background; the new article set should own proof-path reading and
claim-boundary examples.

## Proof Surface Link Model

Required routes to verify before publishing:

- `/proof-paths/`
- `/proof-source-catalog/`
- `/evidence/`
- `/packets/`
- `/review-claim-checklist/`
- `/static-vs-runtime/`
- `/limitations/`
- `/validation/`
- `/demo/result/`
- `/questions/`

Use these links as evidence-orientation surfaces, not proof of runtime behavior.
Anchor text should stay plain: "proof paths", "proof-source catalog",
"evidence tiers", "review claim checklist", "static vs runtime boundaries",
"limitations", "validation", "demo result", and "questions".

## Safe And Unsafe Language Examples

Safe examples:

- "This proof path shows where the public claim is supported and what limits
  still apply."
- "Static evidence can help prepare review questions before runtime telemetry
  is inspected."
- "The article should link the claim to rule IDs, evidence tiers, coverage
  labels, limitations, and the relevant proof surface."
- "When the question is about runtime behavior, take the static context to the
  owner, telemetry, logs, traces, tests, or release review."

Unsafe examples, allowed only as wording to avoid:

- "TraceMap proves this endpoint is safe in production."
- "The proof path confirms release safety."
- "The scan identifies the root cause of the outage."
- "AI impact analysis determines the affected services."
- "Static evidence replaces telemetry and tests."

## Public Copy Tone

Write like a careful reviewer explaining a useful tool to another adult. Use
plain words, short examples, and concrete handoffs. Avoid blame toward
consultants, vendors, teams, or past project choices. Avoid internal workplace
details and private names. Do not paste command output.

Appropriate verbs include "shows", "points to", "links", "supports",
"limits", "labels", "records", "asks", and "hands off". Avoid standalone
"proves" except in non-claim wording such as "does not prove runtime behavior."

## Metadata And Discovery

The future implementation should use the existing blog registry and generated
blog pages. The default public claim level is `concept`. The current blog
registry does not have a claim-level field, so the implementer must either add
visible claim-level text to article bodies or extend the metadata/rendering
schema with a `publicClaimLevel` field and validation. If discovery metadata,
sitemap metadata, `llms.txt`, or `llms-full.txt` supports claim levels or proof
paths in the future, keep article entries concept-level unless the article is
explicitly demo-backed.

For every article, metadata should stay conservative:

- Title: proof-path or static-evidence reading topic.
- Description: what the article teaches, not what TraceMap proves.
- Canonical URL: the final public slug.
- Social copy: bounded to static evidence and proof-path reading.
- Preferred proof path, when supported: `/proof-paths/` or a more specific
  public route recorded in implementation-state.

## Validation Shape

Validation should inspect article source, rendered HTML, generated blog index,
metadata, sitemap/discovery output where applicable, and decoded rendered text.
Use the site's established dedicated validation module pattern under
`site/scripts/` for article content and claim-boundary checks; generic site
validation is necessary but not sufficient.
It should check:

- Required content blocks.
- Required routes and link anchors.
- Blog registration and unique slug decisions.
- Public claim level metadata and rendered claim-level text.
- Forbidden runtime, production, outage, release-safety, operational-safety,
  complete-coverage, AI/LLM, embedding, vector-database, and prompt
  classification wording outside sanctioned non-claim examples.
- Forbidden finding-label usage without false-positive failures for sanctioned
  terms such as `public-safe`, `safe language examples`, `unsafe language
  examples`, and wording-to-avoid examples.
- Private/raw material guardrails.
- Word count bounds selected by the implementer.
- Desktop and mobile rendering sanity for article pages and the blog index
  when the implementation changes layout or discovery.

Validation is a deterministic guardrail, not semantic proof that every future
reader will interpret the articles correctly. Manual review should still check
claim boundaries, examples, and tone before publishing.
