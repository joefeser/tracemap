# Site TraceMap Tools Change Review Brief Tasks

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: implementation tasks are checked only after the matching site
work or validation is complete.

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

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, route/placement choice, rejected alternatives, scope decisions,
  and initial implementation status before changing site code.
- [x] Choose `/use-cases/change-review/`, `/change-review/`, or section
  placement on an existing route, and record the selected placement plus
  rejected alternatives.
- [x] Add a bounded public concept page or section using existing static-site
  layout, navigation, accessibility, and validation patterns.
- [x] Record that primary navigation was left unchanged, or record the
  information-architecture review that chose primary navigation placement.
- [x] Add the visible `Public claim level: concept` label.
- [x] Add the visible principle `No public conclusion without evidence`.
- [x] Address engineers, code reviewers, architects, managers, release
  reviewers, and agents preparing a review handoff.
- [x] Include a `Change Context` section for the review question, changed area,
  public-safe commit or branch context, and review trigger.
- [x] Include an `Evidence Packet` section with proof path, rule ID or rule
  family, visible static dependency surfaces, evidence tier, coverage label,
  public-safe file path and line span, commit SHA, extractor version,
  limitations, and non-claims.
- [x] Include a `Review Questions` section that keeps prompts framed as
  questions rather than conclusions.
- [x] Include a `Stop Conditions` section for missing proof path, private-only
  evidence, unknown or reduced coverage, unsupported runtime/release wording,
  raw artifact exposure, and no named next owner.
- [x] Include a `Next Owners` section covering code owner, reviewer, test
  owner, runtime/service owner, release reviewer, architect, and agent handoff
  owner.
- [x] Include `Limitations` and `Non-Claims` sections that preserve runtime,
  production, release, operational, AI/LLM, and complete-coverage boundaries.
- [x] State that a change review brief is not release approval, runtime proof,
  production safety proof, operational safety proof, or complete coverage.
- [x] State that the brief does not replace tests, code review, source review,
  runtime observability, release review, owner confirmation, or human
  judgment.
- [x] Use canonical evidence tier labels: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Avoid unsupported `impacted`, `safe`, `unsafe`, `approved`, `blocked`,
  `root cause`, `validated for release`, `production proven`, `operational
  assurance`, and `production observability tool` wording outside explicit
  non-claims.
- [x] Avoid AI/LLM impact-analysis, LLM analysis, prompt-based classification,
  embedding, vector database, autonomous approval, complete reasoning, runtime
  monitoring, production traffic, endpoint performance, outage cause, release
  safety, operational safety, and complete coverage claims.
- [x] Avoid blame language around vendors, consultants, teams, or bad code.
- [x] Avoid publishing raw facts, raw SQLite content, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output,
  hidden validation details, or credential-like values.
- [x] Verify candidate cross-links in generated output before adding them:
  `/proof-paths/`, `/packets/`, `/review-room/`, `/validation/`,
  `/limitations/`, `/use-cases/endpoint-review/`,
  `/use-cases/incident-review/`, `/static-vs-runtime/`,
  `/review-claim-checklist/`, and `/use-cases/`.
- [x] Record why the selected page does not duplicate adjacent routes such as
  `/use-cases/incident-review/`, `/static-triage/`, `/manager-brief/`,
  `/manager-packet/`, `/team-evidence-handoff/`, or `/deploy-audit/` when
  those routes exist at implementation time.
- [x] If the distinction from `/team-evidence-handoff/` cannot be stated
  crisply, choose section placement on the closest existing page instead of a
  near-duplicate standalone route.
- [x] Record any unavailable, substituted, or deferred cross-links in
  `implementation-state.md`.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata following the neighboring concept-page `og:type`
  pattern, and route-index metadata including `nonClaims` when supported by
  the current schema.
- [x] If implemented as a standalone route, add route-index metadata and
  sitemap metadata using existing site patterns.
- [x] If implemented as a standalone route, add discovery metadata with
  `publicClaimLevel: concept`, bounded limitations, and non-claims.
- [x] Add focused validation for required copy, required links, forbidden
  claims, private material, unsupported `impacted` wording, link resolution,
  route metadata, discovery metadata, and sitemap metadata.
- [x] Validate that replacement and approval non-claim copy renders and that
  unsupported `replaces ...`, `approves the release`, and `release approval`
  wording is caught outside the sanctioned `Non-Claims` region.
- [x] Manually verify bounded cross-link anchor text, or use an existing
  anchor-text validator helper if one exists at implementation time.
- [x] For a standalone route, add `site/scripts/change-review.mjs` exporting
  `validateChangeReviewDist`, register it in `site/scripts/validate.mjs`, and
  add `site/scripts/change-review.test.mjs`.
- [x] Section placement was not used; host page validator/test extension is not
  applicable because the standalone route validator was added.
- [x] Section placement was not used; host page required-copy,
  sanctioned-section-ID, and forbidden-copy wiring is not applicable because the
  standalone route validator owns those checks.
- [x] Add sanctioned section IDs for `Non-Claims`, `Stop Conditions`, and
  `Limitations`, and validate that overclaim and AI/LLM forbidden-copy scans
  run against sanctioned-region-stripped copy while the required boundary copy
  still renders.
- [x] Partition forbidden-content scans so never-allowed values are checked on
  the whole page, while artifact-family names, descriptive boundary phrases,
  unsupported overclaims, and forbidden AI/LLM positioning are checked on
  sanctioned-region-stripped copy.
- [x] Validate rendered main-content word count against a bounded range that
  matches the chosen neighboring concept-page validator, and record the range
  in `implementation-state.md`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if route or layout changes
  are made, or document why they were deferred.
- [x] Update `implementation-state.md` with final validation, route choice,
  review-loop outcomes, oddities, and follow-up notes.
