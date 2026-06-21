# Site TraceMap Tools Guided Proof-Path Tour Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-site page or section that gives readers a guided
proof-path tour. The tour starts from a public claim label, follows the
public-safe proof path, checks the rule, evidence, coverage, limitations, and
source context that bound the claim, then shows where the reader should stop.

This is a spec-only site phase. It does not implement site code, scanner code,
reducer code, generated outputs, or validation scripts. The future page is a
guided reading experience for existing public-safe evidence surfaces, not a
proof engine, not a runtime trace, not AI analysis, and not release approval.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The page or section itself shall say `Public claim level: concept`.
- The page or section shall visibly say
  `No public conclusion without evidence`.
- The guided tour may show authored, public-safe examples of how to inspect a
  claim's proof path, but it must not claim to compute impact, validate live
  systems, certify releases, or publish private evidence.
- The tour must distinguish "here is how to read the evidence surface" from
  "TraceMap has proven this product behavior." Deterministic scanner facts,
  reducer outputs, report summaries, route metadata, rule catalogs, and
  documented limitations remain the source material.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs or rule families, evidence tiers, coverage labels, limitations,
  commit/source context, extractor versions, supporting public routes or
  artifacts, public claim levels, non-claims, and next-owner handoffs.

## Requirements

### Requirement 1: Choose a public route or placement

The future implementation shall add a guided proof-path tour as a public page
or section using an explicit placement decision.

Acceptance criteria:

- The implementation evaluates `/proof-paths/tour/`,
  `/demo/proof-path-tour/`, and a folded section on `/proof-paths/` before
  selecting the final placement.
- `/proof-paths/tour/` is the non-binding design recommendation because it
  keeps the tour near the canonical proof-path route without making the base
  `/proof-paths/` page carry all tutorial copy.
- `/demo/proof-path-tour/` remains an allowed alternative when the final copy
  uses demo framing and demo metadata more than proof-path reference framing.
- A folded section on `/proof-paths/` remains an allowed alternative when a
  standalone route would duplicate existing proof-path copy or sitemap entries.
- The implementation records the selected route or placement, the rejected
  alternatives, and short reasons in `implementation-state.md`.
- The chosen page or section says `Public claim level: concept`.
- The chosen page or section states
  `No public conclusion without evidence`.
- If a standalone route is chosen, it is included in sitemap metadata,
  discovery metadata, canonical metadata, and internal-link validation.
- If a folded placement is chosen, the implementation records why standalone
  sitemap/discovery metadata was not added and gives the section stable
  anchors that can be linked from related routes.
- If a folded section is chosen and the containing route's page-level
  claim-level metadata is not `concept`, the implementation records in
  `implementation-state.md` how the section's visible
  `Public claim level: concept` is reconciled with the containing route's
  claim-level metadata, and confirms the visible section label does not
  contradict page-level metadata.

### Requirement 2: Distinguish the tour from existing routes

The future tour shall explain its role without duplicating or superseding
nearby public-safe surfaces.

Acceptance criteria:

- The tour distinguishes itself from `/proof-paths/` by presenting a guided
  reading flow for one claim, while `/proof-paths/` remains the canonical
  overview of proof-path concepts and route families.
- The tour distinguishes itself from `/proof-source-catalog/` by using source
  catalog entries as supporting references, not as a replacement catalog.
- The tour distinguishes itself from `/demo/evidence-trail/` by focusing on
  claim-to-proof-path reading steps rather than a demo evidence trail.
- The tour distinguishes itself from `/review-room/` by helping a reader
  inspect a public claim, not by simulating a reviewer workspace or approval
  queue.
- The tour distinguishes itself from `/packets/` by showing how to read a
  proof path, not by publishing or assembling delivery packets.
- The tour distinguishes itself from `/validation/` by linking to validation
  expectations without presenting the tour as a validation result.
- The tour distinguishes itself from `/limitations/` by surfacing limitations
  inside each proof step while leaving the limitations route as the broader
  boundary reference.
- The tour distinguishes itself from `/demo/runbook/` by avoiding operational
  runbook language and release-readiness guidance.
- The tour distinguishes itself from `/review-claim-checklist/` by guiding a
  reader through reading one claim's proof path, while
  `/review-claim-checklist/` remains the concept-level decision checklist for
  whether a claim may be repeated, downgraded, held for owner follow-up, kept
  internal, or not repeated. The tour links to the checklist for that
  repeat/hold decision rather than restating it.
- The tour distinguishes itself from `/glossary/` by applying evidence
  vocabulary inside a guided reading flow, while `/glossary/` remains the
  canonical concept-level definition source for claim levels, evidence tiers,
  and coverage labels. The tour links to the glossary rather than redefining
  those terms.
- The implementation records any route that is missing, renamed, or replaced
  at implementation time and updates link requirements with a documented
  public-safe substitute.
- Link text does not imply that concept-level tour content is a shipped proof
  engine or complete product workflow.

### Requirement 3: Include required proof-step fields

The future tour shall walk readers through a fixed set of public-safe proof
steps before it reaches any conclusion boundary.

Acceptance criteria:

- The tour includes a step for a `claim label` that names the public claim
  being inspected without implying the claim is proven before evidence is
  checked.
- The tour includes `public claim level`, starting with visible concept-level
  framing and explaining that claim levels constrain how strongly public copy
  may present evidence.
- The tour includes `proof path` as the public route or reference trail the
  reader follows, not a local raw artifact path.
- The tour includes `rule ID/family` to identify the deterministic rule,
  extractor family, or documented judgment that supports the evidence.
- The tour includes `evidence tier`, including the possible tiers
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` when tier examples are shown.
- The tour includes `coverage label` to show whether the evidence is full,
  partial, reduced, unknown, or gap-labeled where applicable.
- The tour includes `commit SHA` or public-safe source revision context when a
  claim depends on scan context. It must not publish private remotes or local
  paths.
- The tour includes `extractor version` or schema/extractor family version
  when an example claims deterministic extraction support.
- The tour includes a `supporting public route/artifact` step that links only
  to public-safe routes, checked-in docs, or public-safe summaries.
- The tour includes `limitation` as a first-class part of the proof path, not
  a generic footer disclaimer.
- The tour includes `non-claim` wording that says what the inspected evidence
  does not prove.
- The tour includes `next owner` to identify who should act next, such as a
  reviewer, implementation owner, validation owner, or human decision maker.
- The tour includes at least one complete, authored, public-safe worked
  example that walks a single placeholder claim from claim label through every
  required proof step to a bounded non-claim conclusion, demonstrating the
  bounded reading outcome.
- Authored examples are visibly labeled as illustrative and not real product
  claims. Placeholder claim labels, illustrative commit SHAs, and illustrative
  extractor versions must not be presented as real TraceMap findings.
- Every example conclusion is preceded by the required evidence fields and is
  phrased as a bounded reading outcome, not as an unqualified product claim.

### Requirement 4: Enforce hard public boundaries

The future tour shall reject raw, private, runtime, operational, and AI-based
positioning even when those topics appear as examples of what to avoid.

Acceptance criteria:

- The tour does not claim to prove runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety, or
  complete coverage.
- The tour does not present TraceMap as AI/LLM impact analysis and does not add
  or endorse embeddings, vector databases, prompt classification, or
  prompt-based proof steps.
- The tour does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local paths, raw
  repository remotes, generated scan directories, private sample names, or
  hidden validation details.
- The tour may name local-only artifact families only inside sanctioned
  boundary copy that explains they are not public raw material.
- The tour does not include local commands that encourage readers to publish
  ignored output directories or raw generated scan artifacts.
- The tour does not expose private sample names or examples derived from
  private repositories unless they have been transformed into authored,
  public-safe summaries.
- The tour includes a stable `#where-to-stop` section that tells readers to
  stop when any required evidence-bearing field is absent or incomplete:
  rule ID/family, evidence tier, coverage label, commit/source context,
  extractor version, limitation, or public-safe supporting route. The section
  also states that claim label, public claim level, proof path, non-claim, and
  next owner are framing or handoff fields; their absence means the tour is
  incomplete even when the missing field is not the evidence-presence gate.
- The tour includes a stable `#non-claims` section that rejects runtime,
  production, operational-safety, release-safety, complete-coverage, raw
  artifact, and AI/LLM positioning.
- The tour includes a stable `#step-non-claim` anchor for the per-step
  non-claim field and reserves `#non-claims` for the boundary section.

### Requirement 5: Link to existing public-safe evidence surfaces

The future tour shall guide readers to relevant public routes without making
those routes carry stronger claims than they already do.

Acceptance criteria:

- The tour links to `/proof-paths/`, `/proof-source-catalog/`,
  `/demo/evidence-trail/`, `/review-room/`, `/packets/`, `/validation/`,
  `/limitations/`, `/demo/runbook/`, `/review-claim-checklist/`, and
  `/glossary/` when those routes exist at implementation time.
- At minimum, the tour must link to `/proof-paths/`,
  `/proof-source-catalog/`, `/validation/`, `/limitations/`,
  `/review-claim-checklist/`, and `/glossary/`.
- If a required route is absent, renamed, or replaced at implementation time,
  the implementation records the route status and either removes the link
  requirement or uses the closest public-safe replacement with a documented
  reason.
- Links to demo routes use demo-safe wording and do not imply shipped product
  coverage or general proof.
- Links to review, packet, validation, and runbook routes use role-specific
  wording and do not imply approval, release safety, or operational readiness.
- Related routes may link back to the tour only when the link helps readers
  understand how to inspect public claims without duplicating the tour.
- Internal-link validation confirms all required tour links resolve in
  generated output.

### Requirement 6: Add metadata and validation for future implementation

The future implementation shall make the tour discoverable and validate its
claim boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph fields following existing site patterns.
- Standalone discovery metadata uses `publicClaimLevel: concept`.
- Standalone discovery metadata follows the existing discovery shape,
  including `sourceType: site-page`, `hintCategory`,
  `preferredProofPath`, `limitations`, and `nonClaims`.
- The implementation chooses `hintCategory` from the existing discovery
  vocabulary and records the selected value and rationale in
  `implementation-state.md`.
- The non-binding recommended `hintCategory` is `evidence` for an
  evidence-reading tour; the implementation confirms the value against the
  current discovery vocabulary before use.
- The non-binding discovery `hintCategory` vocabulary at the time of spec
  authoring is: `start`, `evidence`, `limitations`, `demo`, `repo-doc`,
  `roadmap`, and `use-case`. This list is non-binding and must be confirmed
  against current tooling at implementation time; if the vocabulary has
  changed, the implementation records the current set and rationale in
  `implementation-state.md`.
- Before adding standalone discovery metadata, the implementation confirms
  that `concept` is accepted by discovery and validation tooling and records
  the result in `implementation-state.md`.
- Sitemap metadata includes the route if a standalone route is chosen.
- Folded placement preserves the containing route's claim-level metadata
  unless there is a documented reason to change the whole page.
- Validation checks rendered text, decoded HTML, and raw HTML attributes where
  appropriate for required copy, required links, stable anchors, metadata,
  discovery metadata, sitemap metadata when standalone, forbidden claims, and
  forbidden private/raw material.
- Validation enforces a rendered word-count range for the tour. Suggested
  bounds are 650 to 1600 words for a standalone route and 350 to 900 words for
  a folded section, unless implementation records a different bounded reason.
  A complete worked example plus required boundary sections may push a folded
  section toward or beyond the upper bound. If mandatory content cannot fit
  within 350 to 900 words, record an adjusted bound with reason in
  `implementation-state.md`: raise the floor when required minimum content
  exceeds 350 words, and raise the ceiling when the complete worked example
  plus boundary sections exceed 900 words.
- Validation requires visible `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validation requires the proof-step fields: claim label, public claim level,
  proof path, rule ID or family, evidence tier, coverage label, commit SHA or
  source context, extractor version, supporting public route or artifact,
  limitation, non-claim, and next owner.
- Validation confirms each worked example is visibly labeled as illustrative
  and not a real product claim.
- Validation confirms at least one worked example traverses the required
  proof-step fields to a bounded non-claim conclusion, not only that an
  example is present and labeled illustrative.
- Validation fails on affirmative runtime, production traffic, endpoint
  performance, outage cause, release safety, operational safety, complete
  coverage, AI/LLM impact-analysis, embeddings, vector database, or prompt
  classification claims outside sanctioned non-claim sections.
- Validation fails on raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, and hidden validation
  details outside sanctioned boundary sections.
- Markup and validation follow existing semantic heading, landmark, and
  descriptive-link patterns for public site accessibility.
- Future implementation runs `npm test`, `npm run validate`, `npm run build`,
  `git diff --check`, `./scripts/check-private-paths.sh`, and desktop/mobile
  browser sanity checks for layout or interaction changes.

### Requirement 7: Keep the spec-only packet reviewable

This phase shall remain limited to a Kiro spec packet that future implementers
can use without guessing.

Acceptance criteria:

- This phase creates or updates only files under
  `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/`.
- The packet includes `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- The packet is created with `Status: not-started`,
  `Readiness: spec-review`, and `Public claim level: concept`.
- After Kiro spec review findings are patched or dispositioned, `Readiness` is
  updated to `ready-for-implementation` in all five packet files.
- The `Status`, `Readiness`, and `Public claim level` headers remain
  consistent across all five packet files whenever packet-level status changes.
- Future implementation tasks remain unchecked until site code is actually
  changed and validated.
- `implementation-state.md` records branch, target base, scope decisions,
  route decision status, review commands, review outcomes, validation, and
  follow-up items.
- Medium or higher Kiro spec findings are patched or explicitly dispositioned
  before the packet is marked ready for implementation.
