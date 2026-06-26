# Site TraceMap Tools Route-Flow Evidence Story Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe `tracemap.tools` page or section that tells the
route-flow evidence story: how TraceMap can present route-centered static
evidence from an endpoint or root selector to selected service, data, query,
dependency, value-origin, and gap context when the checked-in evidence supports
that story.

This is a spec-only public-site phase. It does not implement site code,
scanner behavior, route-flow behavior, reducer behavior, generated artifacts,
validation scripts, runtime probes, release gates, or public copy changes.

The future surface is concept-level. It may explain how a route-flow evidence
story should be read, what fields must travel with each row, and when the
reader must stop. It must not claim route-flow capabilities are shipped unless
the implementation cites current-branch public-safe evidence, such as a
checked-in spec state, test, rule catalog entry, report schema, or public demo
artifact that proves the narrower statement.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because this packet defines a future public
explanation surface. Current route-flow core work contains shipped and
in-progress static evidence slices, but this site surface itself is not
implemented and does not add a new route-flow artifact or demo result.

Do not upgrade the future page above `concept` merely because it links to
route-flow specs, tests, or reports. Any stronger row-level statement must
cite public-safe evidence for that row and preserve the row's rule ID,
evidence tier, coverage label, supporting IDs, limitations, and stop
conditions.

## Claim Boundaries

- The future page or section shall visibly say `Public claim level: concept`.
- The future page or section shall visibly say
  `No public conclusion without evidence`.
- The page may explain route-centered static evidence from endpoint/root to
  selected service, data, query, dependency, value-origin, context, and gap
  rows when evidence exists.
- The page may use authored illustrative diagrams or rows, but illustrative
  examples must be labeled as illustrative and must not be presented as real
  TraceMap findings.
- The page must not claim runtime proof, production traffic, endpoint
  performance, outage cause, release safety, operational safety, complete
  coverage, business impact, runtime dependency-injection target selection,
  branch feasibility, SQL execution, database state, data contents,
  AI impact analysis, LLM analysis, autonomous approval, release approval, or
  replacement of tests, source review, human review, runtime observability, or
  service-owner judgment.
- The page must not publish raw source, raw SQL, raw config, secrets, raw
  local paths, raw repository remotes, private sample names, private route
  values, generated output directories, raw `facts.ndjson`, raw
  `index.sqlite`, analyzer logs, command output, hidden validation detail, or
  credential-like values.
- Public copy must use deterministic static evidence language: rule IDs,
  rule families, evidence tiers, coverage labels, supporting IDs, safe file
  spans or public-safe source context, commit/source context, extractor
  versions or schema families, classifications, limitations, analysis gaps,
  and next-owner handoffs.
- Missing, reduced, private-only, unavailable, or conflicting evidence is a
  boundary or handoff. It must not be framed as blame toward a person, team,
  reviewer, service, customer, or owner.

## Requirements

### Requirement 1: Choose Route Or Section Placement Conservatively

The future implementation shall choose a public route or section placement for
the route-flow evidence story without implying a shipped public route-flow
demo.

Acceptance criteria:

- The implementation evaluates all candidate placements:
  `/route-flow/`, `/proof-paths/route-flow/`, a section on `/proof-paths/`,
  a section on `/evidence/`, and a section on `/capabilities/`.
- `/proof-paths/route-flow/` is the non-binding design recommendation because
  the story is a proof-path family: it begins with a selected endpoint/root and
  follows public-safe static evidence fields to context and gaps.
- `/route-flow/` remains allowed only if implementation-state records why the
  site information architecture now treats route-flow as a first-class public
  concept page rather than a proof-path subtopic.
- A section on `/proof-paths/` remains allowed when the implementation keeps
  the story compact and avoids creating a standalone route.
- A section on `/evidence/` remains allowed when the final content mainly
  explains evidence fields and row vocabulary rather than a route-centered
  narrative.
- A section on `/capabilities/` remains allowed only if the copy stays
  concept-level and does not imply the full route-flow story is shipped.
- The implementation records the selected placement, rejected alternatives,
  and short reasons in this spec's `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section states `No public conclusion without evidence`.
- If implemented as a standalone route, route metadata, sitemap metadata,
  canonical metadata, Open Graph metadata, discovery metadata, and internal
  link validation use concept-level wording.
- If implemented as a host-route section, the implementation records how the
  section's visible concept claim level is reconciled with the host route's
  claim level and gives the section stable namespaced anchors such as
  `route-flow-story-*`.
- The page or section is not added to primary navigation unless a future
  information-architecture review records why concept-level route-flow
  evidence-story content belongs there.

### Requirement 2: Distinguish Adjacent Public Surfaces

The route-flow evidence story shall link to adjacent surfaces instead of
replacing their purpose or copying their full matrices.

Acceptance criteria:

- The story distinguishes itself from `/proof-paths/` by focusing on a
  route-centered evidence trail, while `/proof-paths/` remains the broader
  proof-path overview.
- The story distinguishes itself from `/proof-paths/tour/` by explaining the
  route-flow-specific row and stop vocabulary, while the tour remains a guided
  reading flow.
- The story distinguishes itself from `/evidence/` by applying evidence
  vocabulary to route-flow rows, while `/evidence/` remains the broader
  artifact and evidence-field surface.
- The story distinguishes itself from `/limitations/` by carrying limitations
  with each route-flow claim, while `/limitations/` remains the canonical
  boundary and non-claim page.
- The story distinguishes itself from `/static-vs-runtime/` by showing static
  route-flow boundaries, while `/static-vs-runtime/` remains the static versus
  runtime telemetry explainer.
- The story distinguishes itself from `/review-claim-checklist/` by helping a
  reader inspect route-flow evidence, while the checklist remains the ritual
  for repeating, downgrading, holding, or keeping claims internal.
- The story distinguishes itself from `/glossary/` by using terms in context,
  while the glossary remains the canonical term index.
- The story links to adjacent routes where they exist at implementation time
  using bounded anchor text.
- If an adjacent route is absent, renamed, or replaced at implementation time,
  the implementation records the route status and uses the closest public-safe
  substitute or defers the link.
- Adjacent links must not imply runtime proof, shipped full coverage, release
  approval, operational certainty, or AI/LLM analysis.

### Requirement 3: Define The Route-Flow Proof Path

The future page shall explain the required proof path from selected
endpoint/root evidence to selected route-flow context.

Acceptance criteria:

- The page defines a route-flow evidence story as a public-safe static trail
  from a selected endpoint, client call, route, or root method to selected
  static flow rows, context rows, dependency/data/value rows, gaps,
  classifications, limitations, and next-owner handoffs.
- The page gives this reading order: public claim level, selector or root,
  route/root evidence, bridge state, row or context kind, rule ID or rule
  family, evidence tier, coverage label, classification, supporting IDs,
  public-safe source context, commit/source context, extractor version or
  schema family, limitation, stop condition, and next owner.
- The page requires route-flow claims to preserve rule IDs or rule families,
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or
  `Tier4Unknown`, coverage labels, supporting IDs where available,
  classifications, limitations, and gap names.
- The page explains that supporting IDs may include safe route-flow row IDs,
  fact IDs, edge IDs, symbol IDs, source labels, or public-safe artifact IDs,
  but must not expose raw private identifiers or local artifact paths.
- The page explains that a row may show selected static context only when it
  is joined through selected route-flow evidence, selected source-local symbol
  identity, selected argument/parameter evidence, or another documented
  rule-backed relationship.
- The page explains that adjacent unjoined service/data/dependency evidence is
  a gap or context limitation, not a selected route-flow path.
- The page requires every public row to carry at least one limitation or a
  documented "no additional limitation for this row" equivalent if future site
  patterns support that closed-set value.
- The page must not use raw source snippets to prove a route-flow row.

### Requirement 4: Use Bounded Row And Status Vocabulary

The future page shall use a closed, static-evidence vocabulary for route-flow
rows, classifications, coverage, and stop states.

Acceptance criteria:

- The page may use row labels such as `selector`, `endpoint/root`,
  `bridge state`, `static flow row`, `context group`, `service/helper`,
  `repository/data`, `query or SQL shape`, `dependency surface`,
  `value origin`, `implementation candidate`, `gap`, `limitation`, and
  `owner follow-up`.
- The page may name route-flow classifications only as static classifications:
  `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
  `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
  `UnknownAnalysisGap`.
- The page explains that `StrongStaticRouteFlow` is still static evidence and
  does not prove runtime request execution, runtime binding, production
  traffic, release safety, outage cause, or complete coverage.
- The page explains that `NeedsReviewStaticRouteFlow` and
  `UnknownAnalysisGap` are useful review states, not failures or blame.
- Coverage vocabulary includes full, partial, reduced, unknown, unavailable,
  future-only, and gap-labeled states as boundaries, not quality judgments.
- Stop states include missing proof path, missing rule ID or rule family,
  missing evidence tier, missing coverage label, missing limitation, missing
  supporting public-safe source context, private-only evidence, hidden detail,
  unjoined adjacent context, ambiguous endpoint/root, runtime-only binding,
  reduced coverage that affects the selected path, schema/extractor gap,
  unrecognized or unlisted classification label, unsupported demo claim, and
  forbidden runtime/release/AI wording.
- Review outcomes for future examples use bounded labels such as
  `show as static evidence`, `show as context`, `label the gap`,
  `downgrade`, `keep internal`, `owner follow-up`, and `do not repeat`.
- The page must not introduce public status labels that sound like release
  approval, operational approval, incident cause, business impact, or runtime
  verification.

### Requirement 5: State Current-Branch Evidence Rules

The future page shall align with current route-flow core work without claiming
more than the current branch proves.

Acceptance criteria:

- Before implementation, the implementer audits current `origin/dev`
  route-flow specs, implementation-state notes, rule catalog entries, tests,
  and site pages that may be cited.
- The implementation records in `implementation-state.md` which route-flow
  statements are backed by current-branch evidence and which remain
  illustrative, concept-level, or deferred.
- Any statement that says TraceMap "shows", "renders", "emits", "preserves",
  or "attaches" a route-flow row must cite a checked-in public-safe evidence
  source or be framed as concept-level, illustrative, future-only, or
  deferred.
- In-progress route-flow task families, such as attachment precision not fully
  closed on the implementation base, must be labeled as in-progress,
  partial, future-only, or limited to the cited sub-slices.
- The page must not cite branch names, private worktree paths, private sample
  names, raw remotes, private route values, or raw generated outputs as public
  proof.
- If implementation cannot prove a route-flow claim from current checked-in
  evidence, the public copy must downgrade the claim, label it concept-level,
  keep it illustrative, or remove it.

### Requirement 6: Include Safe And Unsafe Copy Patterns

The future page shall teach bounded wording and reject overclaims.

Acceptance criteria:

- Safe patterns use verbs such as `inspect`, `follow`, `compare`, `record`,
  `label`, `downgrade`, `hold`, `hand off`, and `escalate`.
- Safe examples include:
  `This route-flow row shows static evidence under Rule <rule-family> with
  <evidence tier>, <coverage label>, supporting IDs, and limitations.`
- Safe examples include:
  `This adjacent context is not joined to the selected route-flow path, so the
  public story labels a gap instead of implying a flow.`
- Safe examples include:
  `The classification is static; runtime behavior and release decisions remain
  outside this evidence story.`
- Unsafe patterns reject unsupported verbs or phrases such as `proves`,
  `guarantees`, `certifies`, `approves`, `replaces`, `resolves`,
  `production-proven`, `safe to release`, `root cause`, `complete coverage`,
  `AI impact analysis`, `LLM impact analysis`, and unqualified `impacted`.
- Unsafe examples must be framed as rejected patterns, not as live claims.
- If forbidden terms appear in unsafe examples, validation allows them only
  inside explicitly bounded rejection, non-claim, or limitation contexts.

### Requirement 7: Define Metadata, Discovery, And Public-Safety Rules

The future implementation shall expose metadata and discovery that match the
concept-level public boundary.

Acceptance criteria:

- Standalone route metadata uses `publicClaimLevel: concept` or the existing
  site discovery equivalent.
- Title, description, canonical URL, Open Graph text, sitemap metadata,
  discovery metadata, card summaries, link previews, alt text, captions,
  structured data, and bot-oriented discovery surfaces must not imply shipped
  route-flow completion, runtime proof, release safety, complete coverage, or
  AI/LLM analysis.
- Metadata must include or point to the visible concept claim level and shared
  principle when site patterns allow it.
- Metadata must not include raw source, raw SQL, raw config, secrets, local
  paths, raw remotes, generated directories, private samples, raw route
  values, command output, hidden validation detail, or credential-like values.
- Metadata must not include review artifact paths, `.tmp/` directory
  references, raw kiro-review prompt or raw output files, or internal
  spec-packet file paths.
- If the story is implemented as a section, host-route metadata must not be
  upgraded by the section. The implementation records the reconciliation in
  `implementation-state.md`.
- Discovery entries should classify the surface as concept-level route-flow
  evidence explanation, not a demo result or scanner/reducer capability.

### Requirement 8: Validate The Future Surface

The future implementation shall add focused validation before the page or
section is considered implemented.

Acceptance criteria:

- Validation checks visible `Public claim level: concept`.
- Validation checks visible `No public conclusion without evidence`.
- Validation checks required route/section placement records and adjacent-link
  decisions in `implementation-state.md`.
- Validation checks required proof-path fields: selector/root, route/root
  evidence, bridge state, row/context kind, rule ID or rule family, evidence
  tier, coverage label, classification, supporting IDs, public-safe source
  context, commit/source context, extractor version or schema family,
  limitation, stop condition, and next owner.
- Validation checks row/status vocabulary and stop states.
- Validation checks that availability verbs such as `shows`, `renders`,
  `emits`, `preserves`, and `attaches` applied to a route-flow row either cite
  a checked-in public-safe evidence source or are framed as concept-level,
  illustrative, future-only, or deferred, consistent with Requirement 5.
- Validation checks that authored illustrative diagrams, rows, or examples are
  labeled as illustrative and are not presented as real TraceMap findings,
  consistent with Requirement 1.
- Validation checks standalone route metadata/discovery or host-section
  metadata reconciliation.
- Validation checks adjacent links resolve or are recorded as substitutions,
  omissions, or deferrals.
- Validation checks forbidden runtime, production, endpoint-performance,
  outage-cause, release-safety, operational-safety, complete-coverage,
  business-impact, AI/LLM, approval, and replacement-of-review claims across
  rendered text, decoded HTML, raw HTML attributes, metadata, sitemap,
  discovery output, fixtures, tests, captions, alt text, and bot-oriented
  discovery surfaces.
- Validation checks unsupported conclusion verbs across the same surfaces and
  allows them only inside explicitly bounded rejection, non-claim, or
  limitation sections.
- Validation checks forbidden private/raw material across rendered text,
  decoded HTML, raw HTML attributes, metadata, sitemap, discovery output,
  fixtures, tests, captions, alt text, and bot-oriented discovery surfaces.
- Validation checks no blame language in visible copy, metadata, examples,
  validation errors, and review-packet references.
- Implementation validation runs `npm test`, `npm run validate`, and
  `npm run build` from `site/` after site source is added.
- Route, layout, or interaction changes require desktop and mobile browser
  sanity checks.
- The implementation reruns `git diff --check` and
  `./scripts/check-private-paths.sh`.
