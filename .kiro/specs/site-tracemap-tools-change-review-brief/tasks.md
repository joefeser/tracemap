# Site TraceMap Tools Change Review Brief Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: this is a spec-only phase. Future implementation tasks stay
unchecked until site code work actually begins.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings from spec review.
- [x] Rerun re-review after Medium+ patches; iterative rounds are recorded in
  `implementation-state.md`.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or recorded as not applicable.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route/placement choice, rejected alternatives, scope decisions,
  and initial implementation status before changing site code.
- [ ] Choose `/use-cases/change-review/`, `/change-review/`, or section
  placement on an existing route, and record the selected placement plus
  rejected alternatives.
- [ ] Add a bounded public concept page or section using existing static-site
  layout, navigation, accessibility, and validation patterns.
- [ ] Record that primary navigation was left unchanged, or record the
  information-architecture review that chose primary navigation placement.
- [ ] Add the visible `Public claim level: concept` label.
- [ ] Add the visible principle `No public conclusion without evidence`.
- [ ] Address engineers, code reviewers, architects, managers, release
  reviewers, and agents preparing a review handoff.
- [ ] Include a `Change Context` section for the review question, changed area,
  public-safe commit or branch context, and review trigger.
- [ ] Include an `Evidence Packet` section with proof path, rule ID or rule
  family, visible static dependency surfaces, evidence tier, coverage label,
  public-safe file path and line span, commit SHA, extractor version,
  limitations, and non-claims.
- [ ] Include a `Review Questions` section that keeps prompts framed as
  questions rather than conclusions.
- [ ] Include a `Stop Conditions` section for missing proof path, private-only
  evidence, unknown or reduced coverage, unsupported runtime/release wording,
  raw artifact exposure, and no named next owner.
- [ ] Include a `Next Owners` section covering code owner, reviewer, test
  owner, runtime/service owner, release reviewer, architect, and agent handoff
  owner.
- [ ] Include `Limitations` and `Non-Claims` sections that preserve runtime,
  production, release, operational, AI/LLM, and complete-coverage boundaries.
- [ ] State that a change review brief is not release approval, runtime proof,
  production safety proof, operational safety proof, or complete coverage.
- [ ] State that the brief does not replace tests, code review, source review,
  runtime observability, release review, owner confirmation, or human
  judgment.
- [ ] Use canonical evidence tier labels: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [ ] Avoid unsupported `impacted`, `safe`, `unsafe`, `approved`, `blocked`,
  `root cause`, `validated for release`, `production proven`, `operational
  assurance`, and `production observability tool` wording outside explicit
  non-claims.
- [ ] Avoid AI/LLM impact-analysis, LLM analysis, prompt-based classification,
  embedding, vector database, autonomous approval, complete reasoning, runtime
  monitoring, production traffic, endpoint performance, outage cause, release
  safety, operational safety, and complete coverage claims.
- [ ] Avoid blame language around vendors, consultants, teams, or bad code.
- [ ] Avoid publishing raw facts, raw SQLite content, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output,
  hidden validation details, or credential-like values.
- [ ] Verify candidate cross-links in generated output before adding them:
  `/proof-paths/`, `/packets/`, `/review-room/`, `/validation/`,
  `/limitations/`, `/use-cases/endpoint-review/`,
  `/use-cases/incident-review/`, `/static-vs-runtime/`,
  `/review-claim-checklist/`, and `/use-cases/`.
- [ ] Record why the selected page does not duplicate adjacent routes such as
  `/use-cases/incident-review/`, `/static-triage/`, `/manager-brief/`,
  `/manager-packet/`, `/team-evidence-handoff/`, or `/deploy-audit/` when
  those routes exist at implementation time.
- [ ] If the distinction from `/team-evidence-handoff/` cannot be stated
  crisply, choose section placement on the closest existing page instead of a
  near-duplicate standalone route.
- [ ] Record any unavailable, substituted, or deferred cross-links in
  `implementation-state.md`.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata following the neighboring concept-page `og:type`
  pattern, and route-index metadata including `nonClaims` when supported by
  the current schema.
- [ ] If implemented as a standalone route, add route-index metadata and
  sitemap metadata using existing site patterns.
- [ ] If implemented as a standalone route, add discovery metadata with
  `publicClaimLevel: concept`, bounded limitations, and non-claims.
- [ ] Add focused validation for required copy, required links, forbidden
  claims, private material, unsupported `impacted` wording, link resolution,
  route metadata, discovery metadata, and sitemap metadata.
- [ ] Validate that replacement and approval non-claim copy renders and that
  unsupported `replaces ...`, `approves the release`, and `release approval`
  wording is caught outside the sanctioned `Non-Claims` region.
- [ ] Manually verify bounded cross-link anchor text, or use an existing
  anchor-text validator helper if one exists at implementation time.
- [ ] For a standalone route, add `site/scripts/change-review.mjs` exporting
  `validateChangeReviewDist`, register it in `site/scripts/validate.mjs`, and
  add `site/scripts/change-review.test.mjs`.
- [ ] For section placement, extend the host page's existing validator module
  and matching `*.test.mjs` instead of adding a standalone validator.
- [ ] If implemented as a section on an existing route, wire required-copy,
  sanctioned-section-ID, and forbidden-copy checks into the host page's
  validator with namespaced sanctioned section IDs.
- [ ] Add sanctioned section IDs for `Non-Claims`, `Stop Conditions`, and
  `Limitations`, and validate that overclaim and AI/LLM forbidden-copy scans
  run against sanctioned-region-stripped copy while the required boundary copy
  still renders.
- [ ] Partition forbidden-content scans so never-allowed values are checked on
  the whole page, while artifact-family names, descriptive boundary phrases,
  unsupported overclaims, and forbidden AI/LLM positioning are checked on
  sanctioned-region-stripped copy.
- [ ] Validate rendered main-content word count against a bounded range that
  matches the chosen neighboring concept-page validator, and record the range
  in `implementation-state.md`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks if route or layout changes
  are made, or document why they were deferred.
- [ ] Update `implementation-state.md` with final validation, route choice,
  review-loop outcomes, oddities, and follow-up notes.
