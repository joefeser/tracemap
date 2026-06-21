# Site TraceMap Tools Guided Proof-Path Tour Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Summary

The future guided proof-path tour should feel like a cautious walkthrough for
readers who want to inspect one public TraceMap claim without needing raw scan
artifacts. The tour should lead from claim label to stopping point: what the
claim says, where the proof path points, which rule or rule family supports the
evidence, what tier and coverage label apply, what source context bounds it,
what limitation travels with it, and who owns the next action.

This design is concept-level. It is not a proof engine, not a runtime trace,
not AI analysis, not an approval workflow, and not release guidance.

## Recommended Information Architecture

Non-binding preferred route for implementation evaluation:
`/proof-paths/tour/`.

Alternative route to evaluate: `/demo/proof-path-tour/`.

Folded placement to evaluate: a folded section on `/proof-paths/`.

Recommended route rationale: `/proof-paths/tour/` keeps the tour close to the
canonical proof-path vocabulary while leaving `/proof-paths/` as a compact
overview. It also avoids making the page sound like a demo result unless the
future copy truly uses demo framing.

The future page or section should use these sections:

- Header with `Public claim level: concept` and
  `No public conclusion without evidence`.
- Short orientation: "follow a public claim until the evidence says stop."
- Step 1: claim label and public claim level.
- Step 2: proof path and supporting public route/artifact. This may be a
  combined visual step, but `#proof-path` and
  `#supporting-public-route-artifact` must both resolve as stable anchors.
- Step 3: rule ID or rule family.
- Step 4: evidence tier and coverage label.
- Step 5: commit SHA or source context and extractor version.
- Step 6: limitation and non-claim.
- Step 7: next owner and where to stop.
- Related public-safe routes.

Stable anchors should include `#claim-label`, `#public-claim-level`,
`#proof-path`, `#rule-id-family`, `#evidence-tier`, `#coverage-label`,
`#commit-source-context`, `#extractor-version`,
`#supporting-public-route-artifact`, `#limitation`, `#step-non-claim`,
`#next-owner`, `#where-to-stop`, and `#non-claims`. Reserve
`#non-claims` for the boundary section so validators do not confuse it with
the per-step non-claim field.

## Tour Card Shape

The core tour can use a single ordered sequence or a compact checklist. Each
step should have:

- Step label.
- What the reader checks.
- Why it matters.
- Stop condition.
- Optional link to the public-safe route that owns deeper context.

The implementation should avoid decorative complexity. The tour's job is to
make the existing evidence surface easier to read, not to introduce a new
visual metaphor or a separate evidence model.

## Public Copy Tone

The copy should be direct, restrained, and reviewer-friendly. Useful verbs are
`check`, `follow`, `confirm`, `record`, `link`, and `stop`. Avoid verbs that
imply the concept page proves or approves anything, such as `certifies`,
`guarantees`, `verifies production`, `proves runtime behavior`, or `clears a
release`.

Examples should be authored and public-safe. They may use placeholder claim
labels, rule families, public claim levels, evidence tiers, coverage labels,
short illustrative commit SHAs, and extractor versions. They must not expose
private sample names, raw repository remotes, local paths, raw snippets, raw
SQL, config values, secrets, analyzer logs, raw facts, or raw SQLite content.
Every worked example should be visibly labeled as illustrative and not a real
product claim.

## Relationship to Existing Routes

The future implementation should use existing routes as supporting surfaces:

- `/proof-paths/`: canonical proof-path overview and proof-path vocabulary.
- `/proof-source-catalog/`: source-family catalog and evidence origin
  reference.
- `/demo/evidence-trail/`: demo evidence trail, when demo framing is needed.
- `/review-room/`: reviewer-oriented context, not approval authority for this
  tour.
- `/packets/`: packet-family context, not raw packet publication.
- `/validation/`: validation expectations, not a validation result for the
  tour itself.
- `/limitations/`: broad limitations and analysis gaps.
- `/demo/runbook/`: demo runbook context, not operational runbook authority.
- `/review-claim-checklist/`: concept-level repeat, downgrade, hold, internal,
  or do-not-repeat decision checklist; the tour links to it for the decision
  and does not restate it.
- `/glossary/`: canonical concept-level evidence vocabulary; the tour links to
  it rather than redefining claim levels, tiers, or coverage labels.

If any route is absent or renamed at implementation time, the implementer
should document the status and choose the closest public-safe substitute rather
than inventing a link to a missing page.

## Validation Shape

Future validation should follow existing site validator patterns. It should
inspect rendered text, decoded HTML, and raw HTML attributes as needed for:

- Required phrases:
  `Public claim level: concept` and
  `No public conclusion without evidence`.
- Required proof-step labels and stable anchors.
- Required public-safe links and link resolution in generated output.
- Standalone metadata: title, description, canonical, Open Graph, discovery,
  and sitemap entries.
- Folded metadata rationale when the tour is not standalone.
- Forbidden overclaims about runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, complete
  coverage, AI/LLM impact analysis, embeddings, vector databases, and prompt
  classification.
- Forbidden private/raw material, with sanctioned boundary sections allowed
  only to say that raw material is not public.
- Bounded rendered word count.
- Validation confirms each worked example is visibly labeled as illustrative
  and not a real product claim.
- Validation confirms at least one worked example traverses the required
  proof-step fields to a bounded non-claim conclusion, not only that an
  example is present and labeled illustrative.
- The non-binding `hintCategory` vocabulary reference at spec-authoring time
  is: `start`, `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`, and
  `use-case`. The recommended value for an evidence-reading tour is
  `evidence`. Confirm the current vocabulary at implementation time and record
  the selection and rationale in `implementation-state.md`.
- Validation follows existing semantic heading, landmark, and descriptive-link
  patterns for public site accessibility.

## Sanctioned-Section Markup Convention

The implementation must choose and record one of: a dedicated CSS class such
as `tm-boundary-section`, a `data-` attribute such as
`data-tm-boundary="non-claim"`, or a comment sentinel that wraps boundary
copy. The choice must be recorded in `implementation-state.md` before
validators are written. Validators should use that convention to treat
required boundary text, including the `#non-claims` section and per-step stop
conditions, as sanctioned copy rather than affirmative claims that fail their
own checks.

Validation enforces a rendered word-count range: suggested 650 to 1600 words
for a standalone route and 350 to 900 words for a folded section. If mandatory
content cannot fit within the folded-section range, record an adjusted bound
with reason in `implementation-state.md`: raise the floor when required
minimum content exceeds 350 words, and raise the ceiling when the complete
worked example plus boundary sections exceed 900 words.

The AI/LLM and raw-material guards should distinguish sanctioned non-claim
copy from affirmative positioning so the required boundary text does not fail
its own validator.
