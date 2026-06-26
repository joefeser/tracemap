# Site TraceMap Tools Static Vs Runtime Field Guide Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: run available Kiro spec reviews before implementation, patch or
explicitly disposition Medium or higher findings, run one bounded re-review if
findings were patched, then keep task status current as future implementation
and validation complete.

## Spec Review Tasks

- [x] Run Kiro spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  or record the exact unavailable-tool/model blocker in
  `implementation-state.md`.
- [x] Run Kiro spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  or record the exact unavailable-tool/model blocker in
  `implementation-state.md`.
- [x] Patch or explicitly disposition Medium or higher actionable findings;
  patch Low only when narrow and safe.
- [x] If findings were patched, run one bounded Kiro re-review and record the
  command, outcome, artifact path, and remaining findings in
  `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to
  `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/`.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are patched or explicitly dispositioned and validation passes.

## Future Implementation Tasks

- [x] Confirm or update `implementation-state.md` with implementation branch,
  base, target PR base, selected scope, and current status before changing site
  code.
- [x] Verify current site information architecture, candidate slugs, article
  or guide patterns, and adjacent route existence before selecting placement.
- [x] Confirm the relationship to the existing `/static-vs-runtime/` page and
  the `site-tracemap-tools-static-vs-runtime-telemetry` spec before selecting
  placement.
- [x] Evaluate `/static-vs-runtime-field-guide/`, a guide/article-family
  route, a section on `/static-vs-runtime/`, a section on `/limitations/`, and
  incident-adjacent sections such as `/incident-call/` or
  `/use-cases/incident-review/`.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] If a separate `/static-vs-runtime-field-guide/` route is selected,
  record why a second concept-level static-versus-runtime surface is warranted,
  how the two pages cross-link, and how duplicate discovery entries are
  avoided.
- [x] Keep the page or section out of primary navigation unless a recorded
  information-architecture decision explicitly selects primary navigation.
- [x] Add the future public page or section using existing static-site layout,
  typography, accessibility, metadata, and validation patterns.
- [x] Add visible copy that says `Public claim level: concept`.
- [x] Add visible copy that says `No public conclusion without evidence`.
- [x] Add visible copy equivalent to
  `TraceMap shows static dependency evidence and limitations; runtime tools
  show observed behavior. Neither replaces the other.`
- [x] Explain that TraceMap provides deterministic static repository evidence,
  not live operational telemetry.
- [x] Add a `Different questions` comparison with static question, TraceMap
  evidence shape, runtime question, runtime owner or system, limitation, and
  handoff fields.
- [x] Ensure the comparison table uses accessible header semantics and remains
  readable on desktop and mobile.
- [x] Include static evidence examples such as repository snapshot, commit
  SHA, rule IDs, evidence tiers, file paths, line spans, extractor versions,
  coverage labels, limitations, dependency references, route or endpoint
  references, contract surfaces, package references, configuration/project
  surfaces, SQL-facing references, and analysis gaps only when public-safe.
- [x] Include generic runtime observability examples such as logs, traces,
  metrics, APM, telemetry, dashboards, production alerts, incident timelines,
  production traffic, endpoint performance, request behavior, runtime errors,
  and service-owner interpretation.
- [x] Add a `How to use both` workflow section for before runtime review,
  during handoff, and after runtime review.
- [x] Add a `Reading a static evidence packet` section covering rule ID,
  evidence tier, file path, line span, commit SHA, extractor version, coverage
  label, limitation, and follow-up owner.
- [x] Add a `Where runtime tools remain authoritative` section that keeps
  production behavior, traffic, performance, outage cause, operational safety,
  incident-response conclusions, service-owner conclusions, and release
  decisions outside TraceMap static evidence.
- [x] Add a `Non-claims` section for runtime behavior, production traffic,
  endpoint performance, outage cause, incident truth, release safety,
  operational safety, complete coverage, AI/LLM impact analysis, LLM analysis,
  prompt-based classification, embeddings/vector search, and replacement of
  human judgment.
- [x] Add a `Proof paths and limitations` section that links only to
  public-safe routes verified at implementation time.
- [x] Link to adjacent public-safe surfaces when they exist and when the link
  text reinforces boundaries, such as `/docs/`, `/validation/`,
  `/limitations/`, `/outputs/`, `/proof-paths/`, `/capabilities/`, `/demo/`,
  `/demo/result/`, `/static-vs-runtime/`, `/static-triage/`,
  `/incident-call/`, `/use-cases/incident-review/`,
  `/language/change-risk/`, and `/site-claim-guardrails/`.
- [x] Resolve and document any expected route that is absent, moved, or
  deferred at implementation time.
- [x] Avoid naming specific observability vendors as shipped integrations
  unless current public repo evidence proves those integrations.
- [x] Avoid `impacted`, `safe`, `unsafe`, `no impact`, `production is
  unaffected`, `runtime confirmed`, `TraceMap proved`, `ready to release`, and
  `approved to merge` wording unless it appears only in a
  machine-distinguishable forbidden-wording example or non-claim context.
- [x] Do not publish raw facts, raw SQLite indexes, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, raw remotes, generated
  scan directories, private sample names, raw telemetry payloads, incident
  timelines, customer data, service names, production identifiers, dashboard
  screenshots, command output, hidden validation details, or credential-like
  values.
- [x] If a standalone route is chosen, add title, description, canonical URL,
  Open Graph metadata, sitemap metadata, and discovery metadata.
- [x] If a standalone route is chosen, ensure discovery metadata includes
  `publicClaimLevel: concept`, `sourceType`, an existing valid
  `hintCategory`, `preferredProofPath`, `limitations`, and `nonClaims` using
  the current site discovery schema.
- [x] If a folded section is chosen, record host title, description, social
  metadata, sitemap, discovery, and anchor reconciliation in
  `implementation-state.md`.
- [x] Add validation for required visible wording, required sections, stable
  anchors (`#different-questions`, `#how-to-use-both`,
  `#reading-static-evidence`, `#runtime-authority`, `#non-claims`,
  `#proof-paths-and-limitations`, and `#related-links`), comparison table
  fields, related links, route metadata, sitemap metadata, discovery metadata,
  forbidden claims, private/raw material, credential-like values, and
  forbidden-wording examples.
- [x] Validate that generic runtime telemetry terms are allowed only when
  framed as complementary systems or non-claims.
- [x] Add positive and negative tests for static-versus-runtime wording,
  metadata non-claims, forbidden operational claims, AI/LLM non-claims, and
  forbidden private/raw material.
- [x] Document the chosen forbidden-wording wrapper mechanism, such as
  `data-forbidden-wording-example` or an equivalent site pattern, in
  `implementation-state.md` before writing validation tests that depend on it.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made, or document why they were deferred.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, partial-work labels if
  applicable, oddities, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw
  remotes, secrets, raw facts, raw SQLite index paths, analyzer log content,
  private sample names, command output, hidden validation details, raw runtime
  telemetry, and production identifiers.
