# Site TraceMap Tools Build Review Workflow Story Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks are checked only after the corresponding spec review,
implementation decision, site change, or validation has actually completed.

## Spec-phase tasks

- [x] Confirm Kiro Opus spec review completed, or record the exact blocker in
  `implementation-state.md`.
- [x] Confirm Kiro Sonnet spec review completed, or record the exact blocker in
  `implementation-state.md`.
- [x] Patch all Medium or higher actionable findings from the initial Kiro
  review pass.
- [x] Patch Low spec-review findings only when narrow and safe.
- [x] Run one bounded Kiro re-review because review findings were patched.
- [x] Patch all Medium or higher actionable findings from the bounded Kiro
  re-review.
- [x] Keep this phase spec-only; do not edit `site/src/`, generated site
  output, scanner code, reducer code, validation scripts, or runtime behavior.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm final diff is limited to
  `.kiro/specs/site-tracemap-tools-build-review-workflow-story/` during this
  spec-only phase.
- [x] Update `implementation-state.md` with review artifacts, validation
  results, claim-boundary decisions, oddities, and follow-up items.
- [x] Update readiness to `ready-for-implementation` only if the reviewed spec
  has no remaining Medium or higher actionable findings and no unresolved
  blocker that prevents implementation.

## Future implementation tasks

- [x] Choose the final future placement:
  `/blog/building-tracemap-under-review-pressure/`,
  `/building-tracemap-under-review-pressure/`, or a justified section on an
  existing public build, review, or claim-governance route.
- [x] Reconcile with the existing
  `/blog/building-tracemap-with-codex-kiro-qodo/` article by choosing whether
  the new article supersedes, complements, or extends it; pick a distinct
  slug/title; cross-link when both remain public; and avoid duplicate metadata
  and overlapping body copy.
- [x] Record final placement, rejected alternatives, metadata consequences,
  existing-article reconciliation, and inbound-link decisions in
  `implementation-state.md`.
- [x] Confirm whether ACK/agent-control is publicly nameable. If not, use
  generic `review-loop coordination layer` wording and record the decision in
  `implementation-state.md`.
- [x] Add the future article or page using existing static site article,
  metadata, accessibility, and navigation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Keep the title or H1 centered on `Building TraceMap Under Review
  Pressure` unless implementation records a clearer equivalent.
- [x] Present the article as a process story, not a product capability claim.
- [x] Cover specs before implementation, reviewable diffs, Kiro spec review,
  Qodo PR review, ACK/agent-control or generic review-loop coordination,
  claim levels, limitations, deterministic validation, and human ownership.
- [x] Frame Codex as implementation assistance in the build workflow, not a
  TraceMap scanner or reducer capability.
- [x] Frame Kiro as spec-review pressure, not certification or endorsement.
- [x] Frame Qodo as PR-review pressure, not final approval or product
  validation.
- [x] Frame ACK/agent-control, if public, as review-loop coordination and
  handoff, not a merge permission shortcut.
- [x] State that human ownership remains necessary for merge, publication,
  product claims, and unresolved judgment calls.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release approval, release safety,
  operational safety, complete coverage, AI/LLM impact analysis, vendor
  endorsement, autonomous review, and replacement of human judgment.
- [x] Avoid saying or implying that workflow tools consume TraceMap output as
  a TraceMap product feature.
- [x] Avoid saying or implying that TraceMap core uses LLM calls, embeddings,
  vector databases, prompt classification, or AI impact analysis.
- [x] Avoid private session IDs, hidden run IDs, raw bot transcripts, raw
  review logs, secrets, credential-like values, local absolute paths, private
  repository paths, raw remotes, private sample or project names, raw source
  snippets, raw facts, raw SQLite content, analyzer logs, SQL, configuration
  values, generated scan directories, and hidden validation details.
- [x] Use synthetic, public-safe, or category-level examples only.
- [x] Keep rendered body copy between 700 and 1600 words, or within a recorded
  site-constrained range of at least 500 and at most 1800 words.
- [x] Add metadata, canonical URL, Open Graph fields, sitemap metadata, blog
  index metadata, and discovery metadata where applicable.
- [x] For blog placement, treat visible `Public claim level: concept` as the
  source of truth unless the blog schema is deliberately extended.
- [x] For non-blog discovery-tracked placement, ensure discovery metadata uses
  `publicClaimLevel: concept`.
- [x] Ensure metadata and card copy stay process-focused and do not imply
  shipped product behavior, endorsement, certification, runtime proof, release
  readiness, or complete coverage.
- [x] Link to adjacent public-safe routes only when they exist at
  implementation time, and record missing or deferred links.
- [x] Before drafting body copy, check which candidate adjacent routes from the
  design link map exist in the current site. Record confirmed, missing, and
  deferred routes in `implementation-state.md` before writing inline links.
- [x] Ensure at least one outbound link points to an existing site surface,
  falling back to `/blog/` or the site root if all adjacent candidate routes
  are unavailable.
- [x] Do not add the article to primary navigation unless the implementation
  records why the existing navigation pattern supports it.
- [x] Add focused validation for required labels, sections, metadata, route or
  section output, article word count, and internal links.
- [x] Add forbidden wording validation for AI/LLM impact-analysis claims,
  runtime or release claims, endorsement claims, autonomous review claims,
  complete-coverage claims, and `tools consume TraceMap` style wording outside
  explicit non-claim or rejected-example contexts.
- [x] Scope forbidden-wording exceptions using existing region markers such as
  `data-non-claim-region` and `data-rejected-pattern-region`, or equivalent
  negated validator patterns.
- [x] Mark the `what the workflow does not prove` section and any rejected
  wording examples with `data-non-claim-region`,
  `data-rejected-pattern-region`, or equivalent existing negated validator
  conventions before running the forbidden-wording validator.
- [x] Add private-material validation across rendered text, decoded HTML, raw
  HTML attributes, metadata, sitemap output, discovery output, fixtures,
  validation messages, and article data.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [x] Re-run `git diff --check`.
- [x] Re-run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, and follow-up
  items.
