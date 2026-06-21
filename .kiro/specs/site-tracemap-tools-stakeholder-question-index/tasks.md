# Site TraceMap Tools Stakeholder Question Index Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Completed implementation tasks below reflect the published `/questions/` route,
metadata, validation, and browser sanity work.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
  Result: quota error; no review content returned. See
  `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
  Result: completed. Initial Medium findings were patched; re-review Medium
  findings were patched.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.

## Implementation Tasks

Implementation phase note: these tasks are checked because the corresponding
site source, validation, and metadata work has landed.

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  route choice, scope decisions, review results, validation plan, and initial
  implementation status before changing site code.
- [x] Confirm `implementation-state.md` has a recorded route choice and
  rejected alternatives before opening any pull request that changes
  `site/src/`.
- [x] Choose the final route or placement: `/questions/`,
  `/use-cases/questions/`, a section on `/use-cases/`, or a recorded
  equivalent.
- [x] Record the selected route or placement and rejected alternatives in
  `implementation-state.md`, including why the surface is an orientation index
  rather than a new proof claim.
- [x] Add the concept-level question index page or section using existing
  static-site layout, typography, accessibility, metadata, and validation
  patterns.
- [x] Include visible `Public claim level: concept` copy unless a spec
  amendment records an evidence-backed upgrade.
- [x] Include visible `No public conclusion without evidence` copy.
- [x] Explain that the surface starts with a stakeholder question and routes
  the reader to a public-safe proof path.
- [x] Keep the page or section out of primary navigation unless
  implementation-state records a matching site information-architecture
  decision.
- [x] Add a question matrix where each row includes audience, question, safe
  answer shape, target route, evidence surface, public claim level, proof
  path, limitation, and non-claim.
- [x] Include a manager planning row with bounded public-safe planning
  language and no staffing, priority, ownership, release-readiness, or
  operational-safety conclusions.
- [x] Include an engineer endpoint/change review row with static evidence
  review language and no safe/unsafe, approved/blocked, production-proven, or
  unsupported impacted conclusions.
- [x] Include an incident-adjacent handoff row that routes to static evidence
  and runtime-boundary surfaces without claiming outage cause, incident
  timeline, production traffic, endpoint performance, or incident command
  authority.
- [x] Include a modernization planning row that routes planners to evidence
  maps, proof paths, validation, or limitations without claiming complete
  migration scope, complete dependency understanding, or production safety.
- [x] Include a reviewer claim-checking row that routes to the claim checklist,
  proof paths, proof-source catalog, validation, and limitations.
- [x] Include a demo evaluation row that routes to demo results, proof paths,
  validation, and limitations without claiming private-repo behavior, runtime
  behavior, release safety, or complete coverage.
- [x] Include a proof-source inspection row that routes to proof-source,
  proof-path, validation, and limitations surfaces without publishing raw
  facts, raw SQLite, analyzer logs, source snippets, raw SQL, config values,
  private samples, raw remotes, or generated scan directories.
- [x] Include an agent/bot discovery row that routes agents to sitemap,
  discovery metadata, proof paths, validation, limitations, and
  claim-checking surfaces.
- [x] Ensure agent-facing copy tells agents not to repeat claims after
  dropping proof path, rule ID or rule family, evidence tier, coverage label,
  limitation, non-claim, or public claim level.
- [x] Use `concept` for row-level public claim level unless current
  public-safe evidence clearly supports `demo` for that row's answer shape.
- [x] If any row uses `demo`, record the exact public-safe evidence and route
  supporting that row-level claim level in `implementation-state.md`.
- [x] Keep page-level claim level separate from target-route and row-level
  claim levels in copy, data, metadata, and validation.
- [x] Preserve dev-only, future-only, hidden, partial, reduced, unknown, or
  unavailable proof states as limitations rather than stronger wording.
- [x] Link safely to existing routes where available:
  `/manager-packet/`, `/use-cases/endpoint-review/`,
  `/incident-evidence-handoff/`, `/legacy-modernization/evidence-map/`,
  `/proof-paths/`, `/proof-source-catalog/`,
  `/review-claim-checklist/`, `/static-vs-runtime/`, `/demo/result/`,
  `/vault-export/`, `/limitations/`, and `/validation/`.
- [x] For any candidate route that does not exist, choose the current
  equivalent, defer the link, or record the omission and rationale in
  `implementation-state.md`.
- [x] Verify every target route and proof path resolves in generated site
  output before publishing the link.
- [x] Use proof paths that point only to public-safe pages, public-safe
  generated summaries, documentation, rule catalog pages, reports, demo
  artifacts, validation pages, or limitations pages.
- [x] Do not link proof paths directly to raw `facts.ndjson`, raw
  `index.sqlite`, analyzer logs, raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw remotes, generated scan directories,
  private sample names, raw telemetry payloads, or hidden validation details.
- [x] Include specific rule IDs where public-safe and relevant; otherwise use
  a rule family or evidence-surface label plus a limitation.
- [x] Use TraceMap evidence tier vocabulary only when tiers are exposed:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown`.
- [x] Transcribe coverage labels from target public-safe artifacts or routes
  where available; do not normalize reduced, partial, unknown, unavailable, or
  future-only labels into stronger wording.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  AI impact analysis, LLM analysis, prompt-based classification, and complete
  product coverage.
- [x] State that TraceMap does not replace managers, service owners,
  architects, tests, telemetry, logs, traces, source review, code review,
  incident command, release review, or human judgment.
- [x] Avoid unsupported `impacted` wording unless the specific row is tied to
  reducer-backed public-safe evidence; otherwise use static-reference or
  review-input wording.
- [x] Avoid publishing raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw
  remotes, generated scan directories, private sample names, raw telemetry
  payloads, or hidden validation details in page copy, metadata, alt text,
  captions, fixtures, tests, discovery output, or generated HTML.
- [x] Add stable row anchors suitable for cross-links and future automated
  review references.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata.
- [x] If implemented as a standalone route, ensure discovery metadata includes
  `publicClaimLevel: concept`, a bounded summary, preferred proof path,
  limitations, and non-claims.
- [x] If implemented as a section, ensure the host page title, description,
  social metadata, sitemap entry, and discovery metadata remain concept-level
  or more conservative.
- [x] Add safe inbound links from relevant public pages only where the link
  helps readers choose the correct evidence surface and does not imply a new
  proof claim.
- [x] Add focused validation for required row families and required row fields.
- [x] Add focused validation for candidate link resolution and recorded route
  substitutions or omissions.
- [x] Add focused validation for forbidden runtime, production,
  endpoint-performance, outage-cause, release-safety, operational-safety,
  AI/LLM, complete-coverage, and unsupported `impacted` wording in rendered
  text, decoded HTML, raw HTML attributes, alt text, captions, metadata,
  fixtures, tests, sitemap/discovery output, and bot-oriented discovery
  surfaces, while allowing those terms inside explicitly bounded non-claim or
  limitation statements.
- [x] Add focused validation for forbidden private or raw material in rendered
  text, decoded HTML, raw HTML attributes, metadata, sitemap/discovery output,
  fixtures, tests, and bot-oriented discovery surfaces.
- [x] Add route metadata validation for `publicClaimLevel: concept` if the
  index is a standalone route.
- [x] Add discovery metadata validation if discovery output is updated.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [x] Update `implementation-state.md` with final route decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, and follow-up items.
