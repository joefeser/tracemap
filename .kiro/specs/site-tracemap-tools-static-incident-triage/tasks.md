# Site TraceMap Tools Static Incident Triage Tasks

Status: implemented
Readiness: ready-for-review
Public claim level: concept

- [x] Confirm or create this spec-local `implementation-state.md` with branch,
  scope, review plan, and initial implementation status before changing site
  code.
- [x] Run requested Kiro spec reviews before implementation, or record exact
  unavailable-tool/model errors in `implementation-state.md`.
- [x] Patch Medium+ spec review findings and rerun re-review where feasible.
- [x] Verify that `/proof-paths/`, `/validation/`, `/docs/`, `/limitations/`,
  `/demo/result/`, and `/incident-call/` resolve, or document any route gap.
- [x] Add a concept-level `/static-triage/` route using existing site layout
  patterns.
- [x] Include `Public claim level: concept` and the shared site principle on
  the page.
- [x] Address engineers and incident leads on a P1 or production call who need
  to orient static code questions quickly.
- [x] Explain the static evidence checklist for endpoint, package, config, SQL,
  route, handler, DTO, and nearby dependency surfaces.
- [x] Include handoff questions that separate static repository evidence from
  telemetry, logs, traces, incident timeline, owner confirmation, tests, and
  release process.
- [x] Add visible boundaries for runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI/LLM impact
  analysis, and complete product coverage.
- [x] Use public-safe summaries or concept copy instead of raw facts, SQLite
  databases, analyzer logs, raw source snippets, raw SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, or private
  sample identities.
- [x] Link to `/proof-paths/`, `/validation/`, `/docs/`, `/limitations/`,
  `/demo/result/`, and `/incident-call/`.
- [x] Add page title, description, canonical URL, and Open Graph metadata.
- [x] Add `/static-triage/` to sitemap metadata.
- [x] Add `/static-triage/` discovery metadata with claim level `concept`.
- [x] Add minimal safe cross-links from relevant existing page(s).
- [x] Add a safe `/incident-call/` cross-link to `/static-triage/` using
  checklist-oriented anchor text.
- [x] Update incident-call validation so the reciprocal `/static-triage/`
  checklist link is checked.
- [x] Add focused validation for required copy, required links, route metadata,
  word count, forbidden positioning, and forbidden private/raw artifact text.
- [x] Wire focused validation into the site validation entrypoint.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks, or record why deferred.
- [x] Update this spec's implementation state with scope, validation, oddities,
  and follow-up items.
- [x] Commit with message `[codex] Add static incident triage page`.
- [x] Push branch and create a ready PR targeting `main`.
