# Site TraceMap Tools Change-Risk Language Guide Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Summary

The future guide should be a compact public reference for language discipline,
not a marketing page, not a release checklist, and not a raw evidence browser.
It should teach readers how to turn deterministic static evidence into bounded
public wording without upgrading that evidence into impact proof, safety proof,
runtime proof, or release approval.

This design is intentionally spec-only. A later implementation should make the
route or section decision after checking current site information architecture,
metadata patterns, and existing adjacent routes.

## Information Architecture

Candidate placements to evaluate:

- `/language/change-risk/`: best default if the site has or wants a language
  guidance family. It gives the guide a clear standalone job and supports
  standalone discovery and sitemap metadata.
- `/review-claim-checklist/language/`: useful if the guide should live close
  to review-claim workflows while remaining separate from the checklist itself.
- Section on `/review-claim-checklist/`: useful if the guide is short and the
  checklist already owns claim-review behavior.
- Section on `/questions/objections/`: useful if the site wants phrasing
  examples near stakeholder objections, but it risks making the guide look like
  objection handling rather than language guidance.

The implementation should prefer a standalone route when the table count,
anchors, metadata, and validation would make a folded section too dense.
If a folded placement is chosen, the future implementation may trim only
optional or illustrative example rows to stay within the folded-section word
count ceiling. The named minimum examples required by the requirements, all
required tables, and all required section anchors must be preserved. If the
required content cannot fit the folded ceiling, choose a standalone route.

## Relationship To Adjacent Routes

The guide should not duplicate existing public surfaces:

- `/review-claim-checklist/` remains the place to check whether a public claim
  has sufficient support.
- `/questions/objections/` remains the place to answer stakeholder objections.
- `/release-review-boundary/` remains the release and approval boundary.
- `/static-vs-runtime/` remains the static-versus-runtime explanation.
- `/proof-paths/faq/` remains proof-path Q&A.
- `/manager-faq/` remains management-facing Q&A.

The language guide should cross-link these routes only when the link clarifies
what the guide does not decide.

## Page Structure

Recommended section order:

1. Header with `Public claim level: concept` and
   `No public conclusion without evidence`.
2. `Why wording matters`.
3. `Safe static-evidence phrases` with the safe phrasing table.
4. `Unsafe phrases` with the unsafe/blocked phrasing table.
5. `Evidence-required wording` with tables for `evidence shows` and
   `needs review`.
6. `Reduced-coverage wording` with the `coverage is reduced` table.
7. `Owner-handoff wording`.
8. `Stop conditions` with the `when to stop` table.
9. `Non-claims`.
10. Related public-safe routes.

Required stable anchors should be predictable, for example
`#why-wording-matters`, `#safe-static-evidence-phrases`,
`#unsafe-phrases`, `#evidence-required-wording`,
`#reduced-coverage-wording`, `#owner-handoff-wording`,
`#stop-conditions`, and `#non-claims`.

## Wording Model

The guide should teach a simple progression:

- Use `evidence shows` only when a public-safe proof path, rule ID or
  equivalent supporting ID, evidence tier, coverage label, and limitation are
  present.
- Use `needs review` when static evidence exists but does not support a
  deterministic conclusion.
- Use `coverage is reduced` when the claim depends on analysis that did not
  fully run.
- Use `not established by this scan` when the statement would require runtime,
  operational, release, or absence-of-impact evidence.
- Stop and hand off when publishing the claim would require private material,
  hidden validation details, raw artifacts, release approval, or blame.

Copy should use neutral verbs: `shows`, `indicates`, `links`, `observed`,
`requires review`, `is not established`, and `is outside this evidence`.
Avoid verbs that sound like proof or certification unless the sentence is a
non-claim.

## Table Design

Each required table should be scannable on desktop and mobile. Use short cell
text, avoid wide prose columns, and prefer compact labels that wrap cleanly.
The mobile layout may stack table rows if existing site patterns support it,
but it must preserve row associations between condition, wording, and boundary.
Unsafe or blocked rows should mark blocked phrases with a stable wrapper,
class, or data attribute so validation can exclude quoted teaching examples
from affirmative-claim detection.

Minimum table set:

- Safe phrasing.
- Unsafe/blocked phrasing.
- When to use `needs review`.
- When to say `evidence shows`.
- When to say `coverage is reduced`.
- When to stop.

## Public Copy Boundaries

The guide should include authored examples only. It must not include raw
scanner output, source snippets, SQL, config values, remotes, local paths,
private sample names, command output, hidden validation details, or
credential-like values.

The guide may mention artifact families and evidence concepts in bounded
language, but it must keep raw artifacts local and private unless another
public-safe route already exposes summarized demo material.

## Validation Shape

Future validation should follow existing site validation patterns and inspect
rendered text, decoded HTML, and raw HTML attributes where relevant.

Validation should cover:

- Required visible phrases.
- Required sections and stable anchors.
- Required wording tables.
- Required adjacent links and link resolution.
- Standalone metadata, discovery metadata, sitemap metadata, and canonical
  metadata when a standalone route is selected.
- Claim-level signal and absence of conflicting route classification.
- Forbidden impact, safety, runtime, release, coverage, AI/LLM, and human
  judgment replacement claims.
- Forbidden private/raw material and credential-like values.
- Word-count bounds.
- Desktop and mobile browser sanity for table readability and no horizontal
  overflow.

Validation should define rendered words as whitespace-delimited tokens in the
guide's main visible content region after HTML rendering, including section
prose and table cell text, and excluding site chrome, global navigation,
footer text, metadata, code or attribute values, and machine-only wrapper
markup used to tag blocked examples.

Every forbidden-claim guard should allow sanctioned non-claim language and
quoted blocked examples while still blocking affirmative product claims. The
private/raw-material guard should keep blocking exposed private material or
credential-like values even when other non-claim text is sanctioned.
