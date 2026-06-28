# Site TraceMap Tools Proof Path FAQ Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe FAQ page or section for `tracemap.tools` that
answers common questions about proof paths. The FAQ should explain what proof
paths are, how to read them, why rule IDs, evidence tiers, coverage labels,
limitations, and missing evidence matter, and how proof paths relate to review
packets.

This is a spec-only public-site phase. It does not implement site code,
scanner behavior, reducer behavior, generated artifacts, validation scripts,
AI/LLM behavior, runtime telemetry, release approval, or review automation.

The future FAQ is an explanatory concept surface. It helps readers ask better
evidence questions; it does not prove runtime behavior, certify a release, or
replace human review.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the FAQ explains how to read and
bound proof paths. It does not add a new evidence artifact, demo result,
scanner capability, reducer result, or shipped workflow.

Do not upgrade the page to `demo` merely because it links to demo-backed
surfaces. A FAQ answer may route readers to a demo-backed proof path, but the
FAQ itself remains concept-level unless a future spec amendment records
checked-in public-safe evidence supporting a stronger claim for the FAQ
content itself.

## Claim Boundaries

- The future page or section shall visibly say `Public claim level: concept`.
- The future page or section shall visibly say
  `No public conclusion without evidence`.
- The FAQ may explain public-safe proof vocabulary, authored examples,
  safe/unsafe answer patterns, review-packet relationships, and stop
  conditions.
- The FAQ must not claim runtime proof, production traffic, endpoint
  performance, outage cause, release safety, operational safety, complete
  coverage, AI/LLM analysis, release approval, autonomous approval, or
  replacement of human review, tests, source review, runtime observability, or
  service-owner judgment.
- The FAQ must not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, private sample names,
  command output, hidden validation details, or credential-like values.
- The FAQ must avoid blame language. Missing, reduced, or conflicting evidence
  is described as a limitation, analysis gap, handoff, or review need, not as
  fault by a person, team, service, customer, or reviewer.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs or rule families, evidence tiers, coverage labels, limitations,
  analysis gaps, commit or public-safe source context, extractor versions,
  supporting public routes or artifacts, review packets, and next-owner
  handoffs.

## Requirements

### Requirement 1: Choose route or section placement conservatively

The future implementation shall choose a public route or section placement for
the proof path FAQ without implying a shipped proof engine.

Acceptance criteria:

- The implementation evaluates all candidate placements:
  `/proof-paths/faq/`, a section on `/proof-paths/`, a section on
  `/proof-paths/tour/`, and a section on `/questions/`.
- `/proof-paths/faq/` is the non-binding design recommendation because it
  keeps repeated reader questions near the proof-path concept without making
  the canonical overview or guided tour carry a long FAQ.
- A section on `/proof-paths/` remains allowed when a standalone route would
  duplicate a concise overview page.
- A section on `/proof-paths/tour/` remains allowed when the final FAQ is
  tightly coupled to the guided reading flow.
- A section on `/questions/` remains allowed when the implementation treats
  the FAQ as part of reader-question routing rather than proof-path reference.
- The implementation records the selected placement, rejected alternatives,
  and short reasons in this spec's `implementation-state.md`.
- The chosen page or section says `Public claim level: concept`.
- The chosen page or section states `No public conclusion without evidence`.
- If implemented as a standalone route, route metadata, sitemap metadata,
  canonical metadata, discovery metadata, and internal-link validation use
  concept-level wording.
- If implemented as a section, the implementation records how the section's
  visible concept claim level is reconciled with the host route's metadata and
  gives the FAQ stable anchors that are unique within the host document and
  namespaced, such as with a `faq-` prefix, so they cannot collide with
  existing host-route anchors.
- The page or section is not added to primary navigation unless a future
  information-architecture review records why concept-level FAQ placement
  belongs there.

### Requirement 2: Distinguish the FAQ from adjacent public surfaces

The FAQ shall answer common questions without replacing neighboring routes or
duplicating their full tables, definitions, tours, or checklists.

Acceptance criteria:

- The FAQ distinguishes itself from `/questions/` by answering proof-path
  questions after a reader has chosen the proof-path topic, while
  `/questions/` remains a broader question-to-surface orientation index.
- The FAQ distinguishes itself from `/proof-paths/` by using question-and-
  answer explanations, while `/proof-paths/` remains the canonical proof-path
  overview and route family entry point.
- The FAQ distinguishes itself from `/proof-paths/tour/` by answering
  repeated reader objections and edge cases, while the tour remains a guided
  step-by-step reading flow.
- The FAQ distinguishes itself from `/evidence/` by applying evidence
  vocabulary to proof-path questions, while `/evidence/` remains the broader
  evidence vocabulary and artifact-shape explanation.
- The FAQ distinguishes itself from `/limitations/` by explaining why
  limitations matter inside a proof path, while `/limitations/` remains the
  canonical boundary and non-claim surface.
- The FAQ distinguishes itself from `/static-vs-runtime/` by answering how
  static proof paths should be read, while `/static-vs-runtime/` remains the
  boundary between static evidence and operational telemetry.
- The FAQ distinguishes itself from `/review-claim-checklist/` by explaining
  proof-path reading questions, while the checklist remains the decision
  ritual for whether a claim may be repeated, downgraded, held, or kept
  internal.
- The FAQ links to adjacent routes where they exist at implementation time
  instead of copying their full matrices, catalogs, tours, or checklists.
- If an adjacent route is absent, renamed, or replaced at implementation time,
  the implementation records the route status and uses the closest public-safe
  substitute or defers the link.

### Requirement 3: Answer required proof-path questions

The future FAQ shall cover the common questions readers need before they can
interpret a proof path safely.

Acceptance criteria:

- Include an answer for "What is a proof path?" that defines a proof path as a
  public-safe route or reference trail from a public claim to supporting static
  evidence, rule ID or rule family, evidence tier, coverage label,
  limitation, and source context.
- Include an answer for "How do I read a proof path?" that gives a fixed
  order: claim, public claim level, proof path, rule ID or rule family,
  evidence tier, coverage label, commit or public-safe source context,
  extractor version or schema family, limitation, non-claim, and next owner.
- Include an answer for "What do evidence tiers mean?" that names
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` without implying that a higher tier proves complete coverage
  or that a lower tier is useless.
- Include an answer for "What do coverage labels mean?" that explains full,
  partial, reduced, unknown, unavailable, future-only, and gap-labeled states
  as visibility and confidence boundaries rather than quality judgments.
- Include an answer for "Why do limitations matter?" that says a limitation is
  part of the claim boundary and must travel with any repeated claim.
- Include an answer for "What should I do when evidence is missing?" that says
  to stop, downgrade, label the gap, keep the claim internal, or hand off to
  the next owner rather than filling the gap with confidence, seniority,
  repetition, or pressure.
- Include an answer for "How do proof paths relate to review packets?" that
  explains review packets may gather proof paths, limitations, review notes,
  and owner follow-ups, but do not convert missing evidence into approval.
- Include an answer for "What can static evidence not prove?" that explicitly
  rejects runtime behavior, production traffic, endpoint performance, outage
  cause, release safety, operational safety, complete coverage, autonomous
  approval, release approval, AI/LLM analysis, and replacement of tests,
  source review, human review, service-owner judgment, or runtime
  observability.
- Include an answer for "Can a proof path use private or raw artifacts?" that
  says public proof paths link only to public-safe summaries, checked-in docs,
  public routes, or sanctioned demo artifacts; raw local artifacts and private
  material are not public proof.
- Include an answer for "What should agents and reviewers preserve?" that says
  automated readers and humans must not repeat or summarize a claim after
  dropping proof path, rule ID or rule family, evidence tier, coverage label,
  limitation, non-claim, or public claim level.
- Every answer remains bounded to deterministic static evidence and avoids
  unsupported `impacted` conclusions unless it explicitly points to a public-
  safe reducer-backed result with rule IDs, evidence tiers, coverage labels,
  limitations, and an appropriate public claim level.

### Requirement 4: Include safe and unsafe answer patterns

The future FAQ shall teach answer shapes that preserve proof boundaries and
call out patterns that overclaim.

Acceptance criteria:

- Include a "safe answer patterns" section or repeated callouts with bounded
  verbs such as `inspect`, `follow`, `compare`, `check`, `record`,
  `downgrade`, `hold`, `label the gap`, `hand off`, and `escalate`.
- Safe patterns include examples such as:
  `This proof path shows a static reference under Rule <rule-family> with
  <coverage label>; the limitation still applies.`
- Safe patterns include examples such as:
  `The evidence is missing, so the public answer is a gap or owner follow-up,
  not a stronger conclusion.`
- Safe patterns include examples such as:
  `The review packet can carry this proof path and limitation to a reviewer;
  it does not approve the release.`
- Include an "unsafe answer patterns" section or repeated callouts that
  rejects unsupported verbs such as `proves`, `guarantees`, `certifies`,
  `approves`, `replaces`, `resolves`, and unqualified `impacted`.
- Unsafe patterns include examples that reject runtime proof, production
  proof, endpoint performance proof, outage-cause claims, release-safety
  claims, operational-safety claims, complete-coverage claims, AI/LLM analysis
  claims, and autonomous-approval claims.
- Unsafe patterns include examples that reject using raw facts, raw SQLite,
  analyzer logs, raw source snippets, raw SQL, config values, local paths,
  raw remotes, generated scan directories, private sample names, command
  output, hidden validation details, secrets, or credential-like values as
  public FAQ content.
- Unsafe examples are framed as "do not say" or "do not imply" patterns, not
  as blame toward an author, reviewer, team, or service.
- If forbidden terms appear in unsafe examples, validation allows them only
  inside explicitly bounded non-claim, limitation, or unsafe-pattern regions.

### Requirement 5: Preserve public-safe examples and artifact boundaries

The FAQ shall use only authored, synthetic, or already public-safe examples.

Acceptance criteria:

- Examples are labeled as illustrative unless they link to an existing
  public-safe demo or documentation surface.
- Placeholder rule IDs, commit-like values, extractor versions, claim labels,
  and review-packet labels are visibly illustrative and not presented as real
  TraceMap findings.
- Public examples link only to checked-in public pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, demo artifacts,
  validation pages, limitations pages, proof paths, or review-packet surfaces.
- The FAQ does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, private sample names,
  command output, hidden validation details, or credential-like values.
- The FAQ may name local-only artifact families only inside boundary copy that
  explains they are not public raw material.
- The FAQ does not include local commands that encourage readers to publish
  ignored output directories or raw generated scan artifacts.
- The FAQ does not reveal internal reviewer identities, private sample
  identities, hidden capability names, in-flight sequencing, confidential
  customer context, or private repository details.

### Requirement 6: Add focused validation for the future surface

The future implementation shall validate content, links, metadata, and claim
boundaries for the FAQ.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- If implemented as a section, add a focused check that parses the generated
  host-page HTML for duplicate `id` attributes and confirms each `faq-`
  prefixed anchor resolves. Record the check name or command in
  `implementation-state.md`. If no automated tool is available at
  implementation time, record the manual verification step and deferral.
- If site source changes, run `npm test` from `site/`.
- If site source changes, run `npm run validate` from `site/`.
- If `npm test` or `npm run validate` do not cover the new page at
  implementation time, record the gap in `implementation-state.md` and add
  focused validation tasks before closing the implementation phase.
- If site source changes, run `npm run build` from `site/`.
- If layout or interaction changes are made, run desktop and mobile browser
  sanity checks.
- Validate visible `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validate every required FAQ question is present.
- Validate that every FAQ answer's full text is present in the statically
  generated HTML, not revealed only by client-side interaction, so rendered-
  text, bot-oriented, and crawler-facing validation can read every answer even
  when accordions or progressive disclosure are used.
- Validate that placeholder rule IDs, commit-like values, extractor versions,
  claim labels, and review-packet labels are visibly marked illustrative and
  are not presented as real TraceMap findings.
- Validate that example outbound links resolve only to allowed public-safe
  target families: checked-in public pages, public-safe generated summaries,
  documentation, rule catalog pages, reports, sanctioned demo artifacts,
  validation pages, limitations pages, proof paths, or review-packet
  surfaces. If an example link is unavailable at implementation time, record
  the deferral, substitution, or omission in `implementation-state.md`.
- Validate safe and unsafe answer pattern sections are present and bounded.
- Validate candidate-route decision and rejected alternatives are recorded in
  `implementation-state.md`.
- Validate links resolve in generated output or are recorded as deferred,
  substituted, or unavailable in `implementation-state.md`.
- Validate discovery, sitemap, canonical, title, description, Open Graph, and
  internal-link metadata preserve concept-level wording if a standalone route
  is chosen.
- If implemented as a section, validate that the host route's title,
  description, canonical, Open Graph, sitemap, and discovery metadata are not
  upgraded above the host route's recorded claim level by the FAQ section, and
  that the FAQ section's stable anchors resolve in generated output and are
  unique within the host document.
- Validate forbidden runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM,
  release-approval, autonomous-approval, and replacement-of-review wording
  across rendered text, decoded HTML, raw HTML attributes, alt text, captions,
  metadata, fixtures, tests, sitemap/discovery output, and bot-oriented
  surfaces, allowing forbidden terms only inside explicitly bounded non-claim,
  limitation, or unsafe-pattern contexts.
- Validate unsupported conclusion verbs (`proves`, `guarantees`,
  `certifies`, `approves`, `replaces`, `resolves`, and unqualified
  `impacted`) do not appear in rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests,
  sitemap/discovery output, or bot-oriented surfaces except inside explicitly
  bounded unsafe-pattern, non-claim, or limitation regions. Allow `impacted`
  only when tied to a public-safe reducer-backed result.
- Validate forbidden private or raw material across rendered text, decoded
  HTML, raw HTML attributes, metadata, sitemap/discovery output, fixtures,
  tests, and bot-oriented surfaces, including raw `facts.ndjson`, raw
  `index.sqlite`, analyzer logs, raw source snippets, raw SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, private
  sample names, command output, hidden validation details, and
  credential-like values.
- Validate no blame language is introduced in visible copy, metadata,
  validation messages, examples, or review-packet references, checking at
  least a recorded set of representative blame indicators, such as `failed`,
  `fault`, `to blame`, `negligent`, `careless`, or attributing missing,
  reduced, or conflicting evidence to a named person, team, service,
  customer, or reviewer. The list is advisory, not exhaustive.
- Update this spec's `implementation-state.md` with route decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, and follow-up items.
