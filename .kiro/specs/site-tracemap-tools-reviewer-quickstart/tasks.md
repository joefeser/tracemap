# Site TraceMap Tools Reviewer Quickstart Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are checked only after the corresponding spec, review, future site
implementation, or validation work is complete.

## Spec-Only Phase

- [x] Create the spec packet with `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- [x] Keep all tracked changes inside
  `.kiro/specs/site-tracemap-tools-reviewer-quickstart/`.
- [x] Run Kiro spec review with `claude-opus-4.8` when available, or record
  the exact command and error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` when available, or record
  the exact command and error in `implementation-state.md`.
- [x] Patch or explicitly disposition all Medium or higher spec-review
  findings.
- [x] Rerun Kiro re-review where feasible after Medium or higher findings are
  patched, or record that no re-review was required because no Medium or
  higher findings were returned.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run focused text checks for required status/readiness/claim-level copy,
  required sections, required quickstart steps, required evidence fields,
  stop conditions, route decision, adjacent-surface distinctions, forbidden
  claims, and forbidden raw/private material in the new spec files.
- [x] Update `implementation-state.md` with review commands, review findings,
  validation results, oddities, residual risks, and follow-up items.

## Future Implementation Phase

- [ ] Confirm this spec is `ready-for-implementation` before changing site
  source.
- [ ] Use `/reviewer-quickstart/` or record any changed placement decision and
  rejected alternatives before implementation starts.
- [ ] Add the concept-level reviewer quickstart using existing static-site
  layout, metadata, accessibility, and navigation patterns.
- [ ] Include visible `Public claim level: concept` and
  `No public conclusion without evidence` on the rendered page.
- [ ] Include required sections: `Start Here`, `Five-Minute Review`,
  `Evidence Fields`, `Stop Conditions`, `Safe Review Language`,
  `Escalation Owners`, and `Non-Claims`.
- [ ] Include required quickstart steps: `identify the claim`,
  `find the proof path`, `check public claim level`, `read rule ID/family`,
  `inspect evidence tier and coverage label`,
  `check commit/extractor context`, `read limitations/non-claims`,
  `name next owner`, and `stop on missing evidence`.
- [ ] Include required evidence fields for claim, proof path, public claim
  level, rule ID or rule family, evidence tier, coverage label, commit or
  source context, extractor context, file path and line span when public-safe,
  limitation, non-claim, validation evidence, unresolved gap, and next owner.
- [ ] Add stop conditions for missing proof path, missing rule ID or rule
  family, missing evidence tier, missing coverage label, missing limitation,
  missing claim level, missing context without limitation, no validation
  evidence, no next owner, private-only support presented as public proof, raw
  artifact leakage, and unsupported runtime/release/safety/production/AI/LLM
  or complete-coverage wording.
- [ ] Add safe review language and owner categories without blame wording.
- [ ] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release approval, release safety,
  operational safety, complete coverage, AI/LLM analysis, embeddings, vector
  database analysis, prompt classification, autonomous approval, and
  replacement of tests, code review, source review, runtime observability, or
  human judgment.
- [ ] Avoid raw facts, raw SQLite content, analyzer logs, raw source snippets,
  raw SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, raw command output, hidden validation
  details, and credential-like values.
- [ ] Link to `/review-room/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/proof-paths/tour/`, `/proof-paths/`,
  `/questions/`, `/demo/runbook/`, `/limitations/`, `/validation/`, and the
  live manager script route when those routes exist.
- [ ] Record missing, renamed, substituted, or deferred adjacent links in
  `implementation-state.md`.
- [ ] Add standalone route metadata, discovery metadata, and sitemap metadata.
- [ ] Add focused validation for required copy, required links, metadata,
  discovery metadata, sitemap metadata, forbidden claims, private/raw
  material, and word count bounds.
- [ ] Validate rendered text, decoded HTML, raw HTML attributes, metadata,
  discovery entries, and fixtures for forbidden claims and private/raw
  material.
- [ ] Run `git diff --check` after implementation.
- [ ] Run `./scripts/check-private-paths.sh` after implementation.
- [ ] Run `npm test` from `site/` after implementation.
- [ ] Run `npm run validate` from `site/` after implementation.
- [ ] Run `npm run build` from `site/` after implementation.
- [ ] Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- [ ] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, unresolved
  gaps, and follow-up items.
