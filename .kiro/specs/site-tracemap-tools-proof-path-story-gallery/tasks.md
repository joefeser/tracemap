# Site TraceMap Tools Proof Path Story Gallery Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks record the completed public-site implementation phase for a
proof-path story gallery. Scanner code, reducer code, generated artifacts,
runtime telemetry, public demo artifacts, and unrelated specs remain out of
scope.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] If both requested Kiro models are unavailable, record the exact errors,
  run the review with the best available model if the harness supports it, and
  record the substitution and rationale in `implementation-state.md`. N/A:
  both requested model runs completed with reduced coverage.
- [x] Patch Medium or higher actionable spec findings and rerun one bounded
  re-review where feasible.
- [x] Patch Low findings only when narrow and safe.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to
  `.kiro/specs/site-tracemap-tools-proof-path-story-gallery/`.
- [x] Move post-review readiness only after Medium or higher findings are
  patched or explicitly dispositioned in
  `implementation-state.md`.

## Completed Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  placement choice, scope decisions, review results, validation plan, and
  initial implementation status before changing site code.
- [x] Choose the final route or placement:
  `/proof-path-stories/`, `/demo/proof-path-stories/`, section on
  `/demo/proof-upgrades/`, section on a future proof-source/catalog route, or
  a recorded equivalent that preserves the required boundaries.
- [x] Record selected and rejected placements in `implementation-state.md`.
- [x] Explain why the selected placement is a story gallery rather than the
  canonical proof ledger, proof-source catalog, claim checklist, roadmap, or
  limitations page.
- [x] Add the concept-level public gallery page or section using existing
  static-site layout, accessibility, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept` unless every card has
  recorded checked-in public-safe demo evidence.
- [x] Include visible `No public conclusion without evidence`.
- [x] Keep the page or section out of primary navigation unless
  `implementation-state.md` records a matching information-architecture
  decision.
- [x] If implemented as a standalone public route, add title, description,
  canonical URL, Open Graph metadata, sitemap metadata, and discovery metadata
  with concept-level wording.
- [x] If implemented as a folded section, record host metadata reconciliation
  and add stable unique anchors for every required section. N/A: standalone
  route selected and recorded in `implementation-state.md`.
- [x] Include required sections: story contract, proof path anatomy, evidence
  packet references, coverage and limitations, stop conditions and routing,
  non-claims and forbidden wording, and gallery validation.
- [x] Include required story-card fields: static question, story category,
  claim level, coverage label, proof path steps, evidence packet references,
  rule IDs or rule families, evidence tiers, supporting IDs when public-safe,
  limitation or non-claim, stop condition, and next owner or next question.
- [x] Include required walkthrough fields and allowed endings:
  `evidence-backed static path`, `reduced coverage`,
  `needs owner follow-up`, `internal only`, `hidden`, and
  `stop: no public-safe evidence`.
- [x] Include story categories for endpoint/service, data/config,
  package/dependency, generated artifact, and reduced-coverage orientation, or
  record omitted categories with rationale.
- [x] Ensure evidence packet references include packet/report-family label,
  rule ID or rule family, evidence tier, coverage label, public-safe
  supporting ID when available, source context, limitation, and stop condition
  when applicable.
- [x] Ensure stop conditions include `no public-safe evidence`,
  `reduced coverage`, `semantic gap`, `syntax-only fallback`,
  `private-only evidence`, `hidden detail`, `missing rule ID`, and
  `requires reducer evidence`.
- [x] Ensure stop routing uses generic owner/question labels and does not
  expose private owner names, private teams, customer context, private sample
  identities, local branches, remotes, or unpublished artifact labels.
- [x] Keep gallery conclusions bounded to deterministic static evidence and do
  not imply runtime, production, performance, release, operational safety,
  complete coverage, AI/LLM analysis, or automated approval.
- [x] Avoid "impacted" wording unless tied to public-safe reducer evidence,
  status, rule IDs, and limitations.
- [x] Do not publish raw facts, SQLite contents, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, remotes, generated scan
  directories, private sample names, private labels, command output, hidden
  validation details, or credential-like values.
- [x] Add focused validation for visible claim level and shared principle.
- [x] Add focused validation for required sections and stable anchors.
- [x] Add focused validation for story-card fields, walkthrough fields,
  evidence references, stop conditions, owner routing, and story categories.
- [x] Add focused validation for standalone metadata, sitemap metadata,
  discovery metadata, canonical URL, title, description, and Open Graph fields
  when a standalone public route is chosen.
- [x] Add focused validation for forbidden product capability, runtime proof,
  production traffic, performance, release approval, release safety,
  operational safety, complete coverage, AI/LLM impact analysis, and automated
  approval claims.
- [x] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML attributes, raw HTML, metadata, sitemap or
  discovery output, examples, fixtures, tests, validation messages, and
  review-packet references.
- [x] Wire focused validation into the existing aggregate site validation
  workflow where the existing pattern supports it.
- [x] Run `npm test` from `site/` after site source is added.
- [x] Run `npm run validate` from `site/` after site source is added.
- [x] Run `npm run build` from `site/` after site source is added.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
