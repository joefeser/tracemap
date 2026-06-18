# Site TraceMap Tools Team Evidence Handoff Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: this is a spec-only phase. Keep implementation tasks unchecked
until a future implementation PR performs the corresponding work.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or recorded as not applicable.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  route choice, scope decisions, and initial implementation status before
  changing site code.
- [ ] Add a bounded `/team-evidence-handoff/` concept page or section using
  existing static site layout patterns.
- [ ] Add the `Public claim level: concept` label and shared site principle.
- [ ] Address handoffs to a teammate, reviewer, manager, or agent.
- [ ] Include the handoff fields: summary, proof path, rule ID/rule family,
  evidence tier, coverage label, limitations, non-claims, local-only
  artifacts, and next owner/action.
- [ ] Include the exact deterministic handoff-completeness sentence from
  Requirement 2.
- [ ] Add receiver-specific handoff patterns for teammate, reviewer, manager,
  and agent without changing the required field set.
- [ ] Distinguish the page from `/packets/`, `/manager-packet/`,
  `/review-room/`, `/manager-faq/`, and `/proof-source-catalog/`.
- [ ] State static evidence boundaries and explicitly avoid runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI/LLM impact analysis, and complete product coverage
  claims.
- [ ] State that a handoff packet does not replace human ownership, tests,
  telemetry, release review, code review, source review, logs, traces,
  incident response, or manager judgment.
- [ ] Avoid publishing raw facts, raw SQLite content, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, or credential-like values.
- [ ] Link the page to `/proof-paths/`, `/packets/`, `/manager-packet/`,
  `/review-room/`, `/manager-faq/`, `/proof-source-catalog/`,
  `/limitations/`, and `/validation/` when those routes exist.
- [ ] Add title, description, canonical URL, and Open Graph metadata.
- [ ] Add sitemap or route-index metadata for `/team-evidence-handoff/` if
  comparable public concept pages are indexed there.
- [ ] Add discovery metadata for `/team-evidence-handoff/` with claim level
  `concept`.
- [ ] Add minimal safe cross-links from relevant existing pages only where
  they help readers choose the correct evidence surface.
- [ ] Add focused validation for required copy, required links, forbidden
  positioning/private text, route metadata, discovery metadata, internal link
  resolution, and word count.
- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks for the final route when
  layout or interaction changes are made, or document why they were deferred.
- [ ] Update `implementation-state.md` with final validation, review-loop
  outcomes, oddities, and follow-up notes.
