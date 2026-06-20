# Site TraceMap Tools Incident Evidence Handoff Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: this is a spec-only branch. Leave implementation tasks unchecked
until a future site implementation branch performs the work.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or the best available Opus
  model at review time, or record the exact unavailable-tool/model error in
  `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are patched or exact unavailable-tool/model errors are recorded.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, public claim level, review status, validation plan, oddities, and
  follow-ups before changing site code.
- [x] Add the concept-level `/incident-evidence-handoff/` page using existing
  static site layout patterns.
- [x] Include `Public claim level: concept` and
  `No public conclusion without evidence` on the page.
- [x] Present the page as a handoff packet/checklist, not an incident concept
  overview, review-room agenda, manager FAQ, runtime monitor, or incident
  command workflow.
- [x] Include the exact primary distinction line: `Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.` This must render as a single logical line matching after whitespace normalization.
- [x] Include the exact static-triage distinction line: `Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.`
  This must render as a single logical line matching after whitespace
  normalization.
- [x] Include checklist labels for static evidence, proof path, rule
  ID/evidence tier, coverage label, limitation, and next owner.
- [x] Explain which static evidence can be brought into the conversation and
  which proof path backs each public-safe claim.
- [x] Identify what each static evidence item does not prove.
- [x] Identify next owners for runtime, telemetry, logs, traces, APM, service
  ownership, database ownership, tests, release controls, and incident command
  questions.
- [x] Differentiate the route from `/incident-call/`, `/static-triage/`,
  `/review-room/`, `/manager-faq/`, `/packets/`, `/manager-packet/`,
  `/manager-brief/`, and `/use-cases/incident-review/`.
- [x] Avoid runtime behavior, production traffic, endpoint performance, outage
  cause, release safety, operational safety, AI/LLM impact analysis, complete
  product coverage, and production dependency understanding claims.
- [x] Avoid publishing raw fact streams, raw SQLite databases, analyzer logs,
  raw source snippets, raw SQL, config values, secrets, local paths, raw
  remotes, generated scan directories, private sample names, connection
  strings, credentials, or local command output.
- [x] Link to `/proof-paths/`, `/validation/`, `/limitations/`,
  `/demo/result/`, `/incident-call/`, `/static-triage/`, `/review-room/`,
  `/manager-faq/`, `/packets/`, `/manager-packet/`, `/manager-brief/`, and
  `/use-cases/incident-review/`, plus `/docs/` or a future public rule catalog
  route when rule/extractor context is needed, documenting any route gap in
  `implementation-state.md`.
- [x] Add title, description, canonical URL, Open Graph metadata, and
  concept-level claim metadata.
- [x] Add `/incident-evidence-handoff/` to sitemap metadata.
- [x] Add `/incident-evidence-handoff/` discovery metadata with claim level
  `concept`, bounded limitations, non-claims, and neighboring route hints.
- [x] Add minimal safe cross-links from relevant existing pages if they help
  readers find the packet without implying runtime proof or release approval.
- [x] Add focused validation for required copy, required links, route metadata,
  sitemap/discovery coverage, rendered word count between 400 and 1800 words,
  forbidden positioning in rendered text plus public metadata/attributes, and
  forbidden private/raw text.
- [x] Wire the focused validator into the site validation entrypoint.
- [x] Add focused tests for the validator, including negative tests for missing
  required copy including both distinction lines, missing links, forbidden
  claims/private text, and word count.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for
  `/incident-evidence-handoff/`, or document why deferred.
- [x] Update this spec's `implementation-state.md` with final implementation
  scope, validation results, oddities, and follow-up items.
