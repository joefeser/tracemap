# Site TraceMap Tools Proof Paths For Managers Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

This packet began as a spec-only packet and now tracks the site
implementation phase. Do not implement scanner code, reducer code, core
generated outputs, runtime behavior, release workflow, or management-decision
automation in this site branch. Implementation tasks are checked only after
the matching site work or validation step is complete.

## Spec-Only Phase

- [x] Create the spec packet with `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- [x] Keep all tracked changes inside
  `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/`.
- [x] Run Kiro spec review with `claude-opus-4.8`, or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6`, or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch or explicitly disposition all Medium or higher spec-review
  findings.
- [x] Rerun one bounded Kiro re-review when Medium or higher findings are
  patched.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to this spec folder.
- [x] Update `implementation-state.md` with review commands, review findings,
  validation results, scope decisions, oddities, residual risks, and
  follow-up items.

## Future Implementation Phase

- [x] Confirm this spec is `ready-for-implementation` before changing site
  source.
- [x] Choose final placement from `/proof-paths/for-managers/`,
  `/manager-proof-paths/`, a section on `/manager-packet/`, a section on
  `/manager-faq/`, a section on `/proof-paths/`, or a recorded equivalent if
  site information architecture has changed.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] Amend the spec or record an explicit implementation-state entry before
  using any placement outside the named candidates.
- [x] Add the concept-level page or section using existing static site
  layout, typography, accessibility, metadata, and navigation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Explain that the surface translates proof paths into manager and
  reviewer decision terms without automating management, product, runtime, or
  release decisions.
- [x] Keep the route or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [x] Distinguish the surface from `/manager-brief/`, `/manager-faq/`,
  `/manager-packet/`, `/packets/`, `/packets/assembly/`, `/proof-paths/`,
  `/proof-paths/faq/`, and `/proof-paths/tour/`.
- [x] Link only to adjacent routes that exist at implementation time or to
  recorded public-safe substitutes.
- [x] Record missing adjacent routes as substitutions, deferrals, or blockers.
- [x] Add the required manager question matrix.
- [x] Include matrix row fields for manager or reviewer question, evidence
  packet to inspect, what static evidence can support, what it does not prove,
  coverage-label consequence, stop condition, next owner, and supporting
  public route.
- [x] Include the required matrix row `What changed in the code path we are
  reviewing?`.
- [x] Include the required matrix row `What evidence supports repeating this
  claim?`.
- [x] Include the required matrix row `What does reduced or partial coverage
  mean for this decision?`.
- [x] Include the required matrix row `Who should answer runtime or product
  behavior next?`.
- [x] Include the required matrix row `Can this evidence approve, block, or
  certify a release?`.
- [x] Include the required matrix row `Can this evidence explain production
  traffic, endpoint performance, or outage cause?`.
- [x] Include the required matrix row `Can this evidence be shared publicly?`.
- [x] Include the required matrix row `What should happen when evidence is
  missing, private-only, syntax-only, or unknown?`.
- [x] Use public owner categories for next-owner values, not private people or
  team names.
- [x] Include proof path anatomy field: claim or question.
- [x] Include proof path anatomy field: public claim level.
- [x] Include proof path anatomy field: proof path or packet link.
- [x] Include proof path anatomy field: rule ID or rule family.
- [x] Include proof path anatomy field: evidence tier.
- [x] Include proof path anatomy field: coverage label.
- [x] Include proof path anatomy field: commit or public-safe source context.
- [x] Include proof path anatomy field: extractor version or schema family.
- [x] Include proof path anatomy field: public-safe file path and line span.
- [x] Include proof path anatomy field: snippet hash or public-safe summary.
- [x] Include proof path anatomy field: artifact family.
- [x] Include proof path anatomy field: limitation.
- [x] Include proof path anatomy field: non-claim.
- [x] Include proof path anatomy field: validation evidence.
- [x] Include proof path anatomy field: unresolved gaps.
- [x] Include proof path anatomy field: next owner.
- [x] Use only `Tier1Semantic`, `Tier2Structural`,
  `Tier3SyntaxOrTextual`, and `Tier4Unknown` as evidence tier values.
- [x] Keep coverage labels visible and copied from cited evidence rather than
  normalized upward.
- [x] Treat reduced, partial, unknown, unavailable, syntax-only, private-only,
  future-only, and gap-labeled states as boundaries on what may be repeated.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI/LLM analysis, embeddings, vector databases, prompt
  classification, autonomous approval, automated management decisions, and
  replacement of telemetry, logs, traces, tests, source review, human review,
  product judgment, service-owner judgment, release process, or manager
  judgment.
- [x] Avoid blame language around missing, reduced, private-only, syntax-only,
  unknown, or conflicting evidence.
- [x] Keep examples synthetic, illustrative, or linked to existing public-safe
  demo/proof surfaces.
- [x] Do not publish raw source snippets, raw SQL, config values, secrets,
  local absolute paths, raw repository remotes, generated scan directories,
  raw facts streams, raw SQLite content, combined SQLite files, analyzer logs,
  private sample names, private owner names, raw command output, hidden
  validation details, or credential-like values.
- [x] If implemented as a standalone route, add title, description, canonical
  metadata, Open Graph metadata, route metadata, discovery metadata, and
  sitemap metadata with concept-level wording.
- [x] N/A: standalone `/proof-paths/for-managers/` route was selected, so
  host-page stable anchors and metadata compatibility were not applicable.
- [x] Add safe inbound links from adjacent manager, packet, proof-path,
  question, limitation, static-vs-runtime, or review-checklist pages when they
  help readers choose the correct surface and preserve claim boundaries.
- [x] Validate inbound link text so it does not upgrade concept-level
  boundaries or imply proof, approval, automation, runtime knowledge, release
  safety, complete coverage, or management-decision automation.
- [x] Add focused validation for visible claim-level copy and shared
  principle.
- [x] Add focused validation for placement, adjacent-route links, metadata,
  discovery metadata, and sitemap metadata when applicable.
- [x] Add focused validation for required matrix rows and required row fields.
- [x] Add focused validation for proof path anatomy, evidence tier vocabulary,
  coverage-label preservation, limitations, non-claims, unresolved gaps, and
  owner routing.
- [x] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  complete-coverage, AI/LLM, embedding, vector-database,
  prompt-classification, autonomous-approval, automated-management,
  replacement, conclusion verbs such as `confirms` or `verifies` when used
  for unsupported runtime/product/release conclusions, and absence-of-impact
  wording outside bounded rejection contexts.
- [x] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- [x] Wire focused validation into the aggregate site validation workflow.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [x] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, unresolved gaps, PR-loop outcomes, residual risk, and
  follow-up items.
