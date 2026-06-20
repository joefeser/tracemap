# Site TraceMap Tools Change Review Brief Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Purpose

This design describes the future public-safe page shape for the TraceMap
change review brief. It exists because route choice, content architecture,
cross-linking, and validation boundaries are substantial enough to deserve a
separate implementation guide.

No site code is implemented in this spec-only phase.

## Recommended Route

Preferred route: `/use-cases/change-review/`

Rationale: the page describes a practical review use case and can sit beside
the existing endpoint review use case without implying a top-level product
claim.

Rejected or conditional alternatives to record during implementation:

- `/change-review/`: shorter, but may make the brief look like a primary
  product surface instead of a use case.
- Section inside `/review-room/`: useful if the site wants fewer pages, but it
  risks hiding a reusable brief packet inside a meeting-oriented route.
- Section inside `/packets/`: keeps artifact vocabulary nearby, but it risks
  turning a review-use-case page into packet taxonomy.

The implementation may choose a different placement only after recording the
selected route and rejected alternatives in `implementation-state.md`.

## Page Structure

The future page should be concise and scan-friendly. Recommended sections:

1. Opening frame
   - Visible label: `Public claim level: concept`
   - Shared principle: `No public conclusion without evidence`
   - One paragraph defining a change review brief as a bounded static-evidence
     packet for PR, release, or change-review conversation prep.
2. Change Context
   - Review question
   - Changed area or branch/commit context when public-safe
   - What prompted the review
   - What is deliberately outside the brief
3. Evidence Packet
   - Proof path
   - Visible static dependency surfaces, such as HTTP route, HTTP client, SQL
     query, package, or config references, described as visible references and
     not proven runtime behavior
   - Rule ID or rule family
   - Evidence tier
   - Coverage label
   - File path and line span when public-safe
   - Commit SHA
   - Extractor version
   - Limitations and non-claims
4. Review Questions
   - Prompts for code review, test review, runtime review, release review,
     architecture review, and agent handoff
   - Prompts must remain questions, not conclusions
5. Stop Conditions
   - Missing proof path
   - Private-only evidence
   - Unknown or reduced coverage
   - Unsupported runtime, release, or operational wording
   - Raw artifact exposure
   - No named next owner
   - Recommended sanctioned section ID: `change-review-stop-conditions`
6. Next Owners
   - Code owner
   - Reviewer
   - Test owner
   - Runtime or service owner
   - Release reviewer
   - Architect
   - Agent handoff owner
7. Limitations
   - Reduced coverage
   - Syntax-only evidence
   - Unknown, unavailable, or future-only proof
   - Framework/project loading gaps
   - Recommended sanctioned section ID: `change-review-limitations`
8. Non-Claims
   - Runtime behavior
   - Production traffic
   - Endpoint performance
   - Outage cause
   - Release safety
   - Operational safety
   - AI/LLM impact analysis
   - Complete coverage
   - Recommended sanctioned section ID: `change-review-non-claims`

## Copy Guidance

Use concrete but bounded nouns: `review question`, `static evidence`, `visible
surface`, `proof path`, `rule ID`, `evidence tier`, `coverage label`,
`limitation`, `non-claim`, and `next owner`.

Avoid upgrade words except inside explicit non-claims: `impacted`, `safe`,
`unsafe`, `approved`, `blocked`, `root cause`, `validated for release`,
`production proven`, `operational assurance`, and `production observability
tool`.

Avoid AI/LLM positioning entirely except inside explicit forbidden-copy
boundaries. TraceMap's public site should keep this use case tied to
deterministic static evidence.

## Cross-Link Design

Candidate links, subject to generated-output verification at implementation
time:

- `/proof-paths/` for proof-path vocabulary.
- `/packets/` for packet and artifact vocabulary.
- `/review-room/` for the broader review discussion surface.
- `/validation/` for validation boundaries.
- `/limitations/` for claim and analysis limits.
- `/use-cases/endpoint-review/` for endpoint-specific review framing.
- `/use-cases/incident-review/` for incident-review framing.
- `/static-vs-runtime/` for static versus runtime boundaries.
- `/review-claim-checklist/` for deciding whether a statement may be repeated.
- `/use-cases/` for the use-case index if the route is standalone.

Links should use bounded anchor text and should not imply TraceMap proves
runtime behavior, release safety, operational safety, outage cause, endpoint
performance, or complete coverage.

## Positioning Vs Adjacent Existing Pages

These routes exist today and share vocabulary with the change review brief.
The implementation must state the distinction in public copy and in
`implementation-state.md`:

- `/team-evidence-handoff/`: focuses on moving an evidence packet between
  receivers without losing the proof boundary. The change review brief instead
  frames a single PR, release, or change-review conversation, adding `Change
  Context`, `Review Questions`, and `Stop Conditions` rather than only a
  receiver handoff field set.
- `/manager-packet/`: focuses on leadership-facing evidence conversation. The
  change review brief is reviewer- and agent-facing review preparation, not a
  manager-only framing.
- `/static-triage/`: focuses on static evidence for incident or triage
  conversation. The change review brief is pre-review and in-review change
  preparation, not incident triage.
- `/manager-brief/` and `/deploy-audit/`: implementation must record why the
  change review brief does not restate their manager or deployment-audit
  framing.

If the distinction cannot be stated crisply, prefer adding a section to the
closest existing page over creating a near-duplicate standalone route.

## Metadata Design

For a standalone route, add or update:

- page title and description
- canonical URL
- Open Graph title, description, URL, and the neighboring concept-page
  `og:type` pattern, commonly `article`
- route index metadata
- current route-index artifact entry, including `nonClaims` when the schema
  supports it
- sitemap metadata
- discovery metadata with `publicClaimLevel: concept`
- discovery limitations and non-claims for runtime, production, endpoint
  performance, outage cause, release safety, operational safety, AI/LLM, and
  complete coverage boundaries

For section placement, record why standalone route metadata and sitemap tasks
do not apply, and add any existing section-level discovery metadata pattern if
the site has one.

## Validation Design

Future validation should be focused, not broad churn:

- Required copy checks for claim level, shared principle, required section
  labels, evidence vocabulary, and non-claims.
- Accessibility checks follow the same pattern applied to neighboring concept
  pages.
- For a standalone route, add `site/scripts/change-review.mjs` exporting
  `validateChangeReviewDist`, register it in `site/scripts/validate.mjs`, and
  add `site/scripts/change-review.test.mjs` using the existing per-page
  validator/test convention.
- For section placement, extend the host page's validator module and matching
  test file instead of creating a standalone validator.
- Metadata checks for title, description, canonical URL, Open Graph fields,
  route index, discovery, sitemap, and `publicClaimLevel: concept` when
  standalone.
- If section placement is chosen, the required-copy, sanctioned-section-ID,
  and forbidden-copy checks are added to the host page's validator, and new
  sanctioned section IDs are namespaced to avoid collisions with the host
  page's existing `sanctionedSectionIds`.
- If section placement is chosen, record a section-appropriate word-count
  range instead of automatically inheriting the standalone route's
  neighboring-validator word-count range.
- Link checks for the selected route and required adjacent routes.
- Forbidden-content checks follow the established scan partition used by
  neighboring concept-page validators. Whole-page scans catch values and
  locations that are never allowed, including real local paths, file URLs,
  localhost/loopback addresses, raw remotes, connection strings,
  credential/secret values, raw command output, hidden validation details, and
  blame/scare framing, plus actual private identifier values such as private
  repository names, customer names, service names, owner names, private sample
  names, and real internal review dates. Because arbitrary private identifiers
  cannot be fully pattern-matched, value-level exclusion also depends on
  synthetic examples, `./scripts/check-private-paths.sh`, and manual review.
  Sanctioned-region-stripped scans catch artifact-family
  names, descriptive boundary phrases, unsupported overclaim wording, and
  forbidden AI/LLM positioning outside the namespaced `Non-Claims`, `Stop
  Conditions`, and `Limitations` regions.
- Unsupported-overclaim checks prefer contextual phrase patterns that do not
  flag legitimate review-process nouns such as approval discussion or blocked
  checklist items, while still catching unsupported `impacted` wording and
  release/runtime/production assurance claims.
- Unsupported-overclaim checks also catch replacement and approval claims such
  as `replaces tests`, `replaces code review`, `replaces release review`,
  `replaces source review`, `approves the release`, or `release approval`,
  while allowing the authored non-claim sentence inside the sanctioned
  `Non-Claims` region.
- Required-copy validation confirms the replacement/approval non-claim sentence
  renders.
- Link validation confirms required cross-links resolve; bounded anchor-text
  review is a manual implementation-state checklist item unless future site
  validators already expose a stable anchor-text helper.
- Navigation placement is a manual information-architecture gate: record that
  primary navigation was left unchanged, or record the review that chose to add
  the page there.
- Rendered main-content word count should match the neighboring validator the
  implementation patterns from, such as endpoint review's 700 to 1900 words or
  team evidence handoff's 400 to 1500 words, and the selected range should be
  recorded in `implementation-state.md`.
- Site command checks: `npm test`, `npm run validate`, `npm run build` from
  `site/`, plus `git diff --check` and `./scripts/check-private-paths.sh`.
- Browser sanity checks on desktop and mobile if route or layout changes are
  made.

## Non-Goals

- No scanner or reducer behavior changes.
- No generated artifact changes.
- No raw fact, SQLite, report, analyzer log, source snippet, SQL, config,
  secret, command output, private sample, local path, or raw remote
  publication.
- No runtime telemetry or production integration.
- No release approval workflow.
- No AI/LLM impact-analysis claim.
