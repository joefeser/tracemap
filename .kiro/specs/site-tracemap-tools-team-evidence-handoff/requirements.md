# Site TraceMap Tools Team Evidence Handoff Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public site concept page or section for
`/team-evidence-handoff/`. The page should help someone share a TraceMap
evidence packet with a teammate, reviewer, manager, or agent without losing the
proof boundaries that make the packet useful.

The page is about handoff language and receiver-specific packet framing. It is
not another artifact taxonomy, a generic review-room agenda, a manager FAQ, a
packet landing page, or a proof-source catalog.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, or agent
automation.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

The future page may explain a concept-level communication model for
deterministic static evidence: summary, proof path, rule ID or rule family,
evidence tier, coverage label, limitations, non-claims, local-only artifacts,
and next owner/action.

The future page must not claim runtime behavior, production traffic, endpoint
performance, outage cause, release safety, operational safety, AI impact
analysis, LLM analysis, or complete product coverage. It must not imply that a
handoff packet replaces human ownership, tests, telemetry, release review, code
review, source review, logs, traces, incident response, or manager judgment.

## Requirements

### Requirement 1: Publish a bounded team evidence handoff route

The future implementation shall publish a concept-level public page or section
for receiver-specific evidence handoff language.

Acceptance criteria:

- The page uses `/team-evidence-handoff/` unless implementation-state records a
  safer route choice and rationale before implementation starts.
- The page says `Public claim level: concept`.
- The page states the shared site principle: No public conclusion without
  evidence.
- The primary copy addresses people sharing a TraceMap evidence packet with a
  teammate, reviewer, manager, or agent.
- The page explains that the handoff keeps a conclusion attached to its proof
  path, rule ID or rule family, evidence tier, coverage label, limitations,
  non-claims, local-only artifacts, and next owner/action.
- The page uses concept-level language and does not imply a shipped workflow,
  collaboration product, review-room tool, runtime monitor, release gate, or
  autonomous agent handoff.
- The page reuses existing static site layout, navigation, metadata,
  accessibility, and validation patterns.

### Requirement 2: Define the handoff packet language

The page shall give readers a public-safe language shape for handing evidence
to another person or agent without weakening proof boundaries.

Acceptance criteria:

- The page includes the handoff fields `summary`, `proof path`, `rule ID/rule
  family`, `evidence tier`, `coverage label`, `limitations`, `non-claims`,
  `local-only artifacts`, and `next owner/action`.
- The page includes this deterministic, validator-checkable sentence:
  `A handoff is complete only when the summary, proof path, rule ID/rule
  family, evidence tier, coverage label, limitations, non-claims, local-only
  artifacts, and next owner/action travel together.`
- The page explains that the summary is a bounded statement of what static
  evidence supports, not a replacement for proof details.
- The page explains that the proof path points to public-safe proof surfaces
  or private review locations, not raw private scanner output on the public
  site.
- The page explains that rule IDs or rule families identify why the evidence
  exists and what limitations apply.
- The page explains that evidence tier and coverage label communicate
  confidence boundaries and partial-analysis status.
- The page explains that limitations and non-claims prevent a handoff from
  becoming a stronger claim than the evidence supports.
- The page explains that local-only artifacts remain private working material
  unless a human deliberately creates a public-safe summary.
- The page explains that next owner/action names the human or review process
  responsible for follow-up.

### Requirement 3: Provide receiver-specific handoff patterns

The page shall differentiate receiver needs while keeping the same evidence
fields attached to every handoff.

Acceptance criteria:

- The page includes a teammate handoff pattern focused on implementation
  context, nearby code review, and follow-up questions.
- The page includes a reviewer handoff pattern focused on proof path, rule
  family, evidence tier, coverage label, limitations, and review gaps.
- The page includes a manager handoff pattern focused on what can be repeated
  to stakeholders without overclaiming, what remains a non-claim, and which
  owner/action is next.
- The page includes an agent handoff pattern focused on bounded instructions,
  local-only artifact handling, proof-path preservation, and explicit
  non-claims.
- Each receiver pattern keeps the same required handoff fields visible.
- Receiver-specific copy does not imply the receiver can skip tests,
  telemetry, release review, code review, source review, owner confirmation,
  or human judgment.
- Agent-facing copy does not imply AI impact analysis, LLM analysis,
  prompt-based classification, autonomous approval, or complete reasoning over
  private artifacts.

### Requirement 4: Distinguish this page from neighboring public surfaces

The page shall clearly explain its role relative to existing packet,
management, review, and proof pages.

Acceptance criteria:

- The page distinguishes itself from `/packets/` by focusing on handoff
  language and receiver context, not packet artifact taxonomy.
- The page distinguishes itself from `/manager-packet/` by covering teammate,
  reviewer, manager, and agent receivers, not only manager-ready summaries.
- The page distinguishes itself from `/review-room/` by focusing on what one
  person hands to the next receiver, not a shared meeting agenda.
- The page distinguishes itself from `/manager-faq/` by providing handoff
  phrases and fields, not a question-and-answer explainer.
- The page distinguishes itself from `/proof-source-catalog/` by preserving
  proof boundaries in communication, not cataloging proof-source families.
- Cross-links to neighboring pages use bounded anchor text and do not imply
  runtime proof, production proof, release safety, operational safety,
  endpoint performance proof, outage cause, AI impact analysis, LLM analysis,
  or complete product coverage.

### Requirement 5: Preserve public-safe artifact handling

The page shall publish only public-safe explanatory copy, sanitized examples,
and public-safe links.

Acceptance criteria:

- The page and metadata do not publish raw facts, raw SQLite content, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, or credential-like values.
- The page may mention local generated artifact types only as private working
  material that should not be published raw.
- Public examples use authored concept examples or existing public-safe demo
  summaries.
- Private repository evidence is described as requiring private review before
  any summary becomes public copy.
- The page does not include local commands that expose ignored output paths,
  private repository paths, private remotes, or raw artifact locations.
- Snippet-like text is avoided unless it is synthetic handoff language that
  contains no source code, SQL, configuration values, secrets, local paths, or
  private identifiers.

### Requirement 6: Preserve forbidden-copy boundaries

The page shall make non-claims visible and avoid wording that upgrades static
evidence into unsupported conclusions.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The page does not imply TraceMap replaces telemetry, logs, traces, tests,
  ownership, human review, code review, source review, release process,
  incident response, or manager judgment.
- The page avoids saying a surface is `impacted`, `safe`, `unsafe`,
  `approved`, `blocked`, `root cause`, `validated for release`, or `production
  proven` unless the phrase is explicitly framed as something the handoff does
  not claim.
- The page does not describe TraceMap as AI-powered, LLM-powered, intelligent
  impact analysis, automated release approval, operational assurance, or a
  production observability tool.
- The page explains that a handoff should preserve what is known, what is
  partial, what is missing, and what a human owner must decide next.

### Requirement 7: Add discovery metadata and public-site validation

The future implementation shall make the concept discoverable and validate its
claim boundaries.

Acceptance criteria:

- Discovery metadata labels `/team-evidence-handoff/` as `concept`.
- Page metadata includes a title, description, canonical URL, and Open Graph
  fields for the route.
- Sitemap and route-index metadata include `/team-evidence-handoff/` when
  comparable public concept pages are indexed there.
- Discovery metadata includes `publicClaimLevel: concept`, a bounded
  `limitations` entry, and bounded `nonClaims` entries for runtime,
  production, endpoint-performance, outage-cause, release-safety,
  operational-safety, AI/LLM, and complete-coverage claims.
- The page links to `/proof-paths/`, `/packets/`, `/manager-packet/`,
  `/review-room/`, `/manager-faq/`, `/proof-source-catalog/`,
  `/limitations/`, and `/validation/` if those routes exist at implementation
  time.
- If a required route does not exist at implementation time, the
  implementation either blocks the route until a safe public target exists or
  records the substitution and rationale in `implementation-state.md`.
- Validation confirms the route renders and contains `Public claim level:
  concept`, the shared principle, the required handoff fields, the
  deterministic sentence from Requirement 2, required links, and route
  metadata.
- Validation confirms required internal links resolve in generated site
  output.
- Validation enforces a rendered word count between 400 and 1500 words.
- Validation strips the page's sanctioned non-claims and boundary region
  before scanning decoded HTML, rendered text, and raw HTML attributes for
  forbidden AI/LLM positioning including `AI-powered`, `AI impact analysis`,
  `LLM-powered`, `LLM analysis`, `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`,
  `intelligent impact analysis`, and `smart impact`.
- Validation checks the same sanctioned-region-stripped rendered body copy for
  unsupported overclaim wording using word-boundary matching for `impacted`,
  `safe`, `unsafe`, `approved`, `blocked`, `root cause`, `validated for
  release`, `production proven`, `operational assurance`, and `production
  observability tool`. The `safe` check must exempt the compound form
  `public-safe`, for example with `/(?<!public-)\bsafe\b/i`, consistent with
  neighboring concept-page validators.
- Validation checks forbidden private/raw material including local paths, file
  URLs, localhost addresses, raw fact/index/log content, raw SQL/source snippet
  wording, connection strings, secrets, credential-like labels, raw remotes,
  generated scan directories, and private sample names.
- Implementation validation includes `git diff --check`, `npm test` from
  `site/`, `npm run validate` from `site/`, `npm run build` from `site/`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
  when layout or interaction changes are made.

### Requirement 8: Keep implementation state current

The future implementation shall keep this spec's status files aligned with the
actual work completed.

Acceptance criteria:

- `tasks.md` remains unchecked until future implementation work begins.
- Implementation tasks are checked only after the corresponding implementation
  and validation work is complete.
- This spec-only phase keeps `Status: not-started` and moves `Readiness` to
  `ready-for-implementation` only after Medium or higher spec-review findings
  are patched or explicitly recorded as not applicable.
- `implementation-state.md` records branch, target base, scope decisions,
  public claim level, review commands and results, validation commands and
  results, oddities, and follow-up items.
- If a Kiro review identifies Medium or higher findings, the spec either
  patches them and records the rerun result or records why a rerun was not
  feasible.
