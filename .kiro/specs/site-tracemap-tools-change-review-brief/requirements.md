# Site TraceMap Tools Change Review Brief Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe site page for a TraceMap change review brief,
likely `/use-cases/change-review/` or `/change-review/`. The page should help
engineers, code reviewers, architects, managers, release reviewers, and agents
prepare for a PR, release, or change-review conversation with deterministic
static evidence.

This is a site-spec-only packet. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, or public
copy changes.

A change review brief is not a release approval, runtime proof, production
safety claim, or complete coverage claim. It is a bounded packet that says what
changed, what static dependency surfaces are visible, what evidence backs the
review question, what coverage is partial or unknown, and who owns next
verification.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page shall use `Public claim level: concept` unless a future spec
  amendment cites checked-in public-safe demo evidence for this exact page.
- The page may describe a concept-level review brief built from deterministic
  static evidence, rule IDs or rule families, evidence tiers, file and line
  references when public-safe, commit identity, extractor versions, coverage
  labels, limitations, non-claims, and next owners.
- The page must not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, prompt-based classification, or complete product
  coverage.
- The page must not imply TraceMap approves releases, replaces tests, replaces
  code review, replaces release review, or proves a change is safe or unsafe.
- The page must not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, raw command output, or hidden validation details.
- Copy should avoid blame language around vendors, consultants, teams, or bad
  code. It should frame the brief as meeting-fog reduction through evidence,
  not as certainty.

## Requirements

### Requirement 1: Choose a bounded route and placement

The future implementation shall publish a concept-level public page or section
for change review brief preparation.

Acceptance criteria:

- The implementation chooses `/use-cases/change-review/`, `/change-review/`,
  or a section on an existing use-case/review page, then records the selected
  placement and rejected alternatives in `implementation-state.md`.
- The preferred starting route is `/use-cases/change-review/` because the page
  describes a review use case. `/change-review/` remains a valid alternative if
  implementation records why a shorter top-level route fits the site
  information architecture.
- The page or section says `Public claim level: concept`.
- The page or section states the shared site principle: No public conclusion
  without evidence.
- The page or section addresses engineers, code reviewers, architects,
  managers, release reviewers, and agents preparing a review handoff.
- The page or section is not added to primary navigation unless a future site
  information-architecture review records why that placement is warranted.
- Implementation records whether primary navigation was left unchanged or why
  an information-architecture review chose navigation placement.
- If implemented as a standalone route, the route is added to route metadata,
  discovery metadata, and sitemap metadata using existing static-site
  patterns.

### Requirement 2: Define the change review brief

The future page shall explain what a change review brief is and what it is not.

Acceptance criteria:

- The page defines a change review brief as a bounded static-evidence packet
  for a PR, release, or change-review conversation.
- The page says the brief records what changed, visible static dependency
  surfaces, evidence backing the review question, partial or unknown coverage,
  and the owner for next verification; visible static dependency surfaces must
  appear as references in the `Evidence Packet` section.
- The page states that a brief is not release approval, runtime proof,
  production safety proof, operational safety proof, or complete coverage.
- The page explains that the brief reduces meeting fog by keeping claims
  attached to evidence, limitations, and next owners, not by promising
  certainty.
- The page explains that deterministic static evidence can prepare a review
  conversation, but human reviewers, tests, runtime observability, source
  review, and release process remain responsible for their own decisions.
- The page avoids calling a system, endpoint, dependency, feature, or release
  `impacted`, `safe`, `unsafe`, `approved`, `blocked`, `root cause`,
  `validated for release`, `production proven`, `operational assurance`, or
  `production observability tool` unless the phrase is inside an explicit
  non-claim boundary.

### Requirement 3: Publish the required brief sections

The future page shall include a concise structure that reviewers can scan
before or during a review conversation.

Acceptance criteria:

- The page includes a `Change Context` section that captures the review
  question, changed area, commit or branch context when public-safe, and what
  prompted the review.
- The page includes an `Evidence Packet` section that lists deterministic
  static evidence fields: proof path, visible static dependency surfaces such
  as HTTP route, HTTP client, SQL query, package, or config references
  described as visible references rather than proven runtime behavior, rule ID
  or rule family, evidence tier, coverage label, file path and line span when
  public-safe, commit SHA, extractor version, limitations, and non-claims.
- The page includes a `Review Questions` section that turns static evidence
  into review prompts without converting prompts into conclusions.
- The page includes a `Stop Conditions` section for missing proof path,
  private-only evidence, unknown or reduced coverage, unsupported runtime or
  release wording, raw artifact exposure, and no named next owner.
- The page includes a `Next Owners` section that names who owns follow-up, such
  as code owner, reviewer, test owner, runtime owner, release reviewer,
  architect, or agent handoff owner.
- The page includes a `Limitations` section that keeps partial, syntax-only,
  unavailable, reduced, future-only, or unknown coverage visible.
- The page includes a `Non-Claims` section that states the runtime,
  production, release, operational, AI/LLM, and complete-coverage boundaries.
- Sections stay short enough for review-room use and avoid marketing-style
  certainty language.

### Requirement 4: Keep claims tied to deterministic evidence

The future page shall make evidence vocabulary visible wherever it asks a
reader to repeat or act on a review statement.

Acceptance criteria:

- The page says a review statement cannot be upgraded by confidence,
  seniority, meeting repetition, manager pressure, or agent summary when proof
  path, rule ID or rule family, evidence tier, coverage label, limitation, or
  next owner is missing.
- Evidence tiers use only the TraceMap tier vocabulary:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown`.
- Rule IDs are used when public-safe and specific; otherwise the brief uses a
  rule-family label plus a limitation explaining why a specific rule ID is not
  available or not public-safe.
- Coverage labels are transcribed from the cited public-safe artifact or
  summary and are not silently normalized into stronger wording.
- Static dependency surfaces are described as visible references, surfaces,
  paths, evidence, or review inputs unless a reducer-backed public-safe result
  supports stronger terminology.
- A reduced, partial, unknown, unavailable, or future-only coverage label
  remains visible and forces either a review question, stop condition, or named
  next owner.
- Commit SHA and extractor version are described as evidence provenance, not
  as proof that behavior changed at runtime.

### Requirement 5: Preserve public-safe artifact handling

The future page shall publish only public-safe explanatory copy, sanitized
examples, and public-safe links.

Acceptance criteria:

- The page and metadata do not publish raw facts, raw SQLite content, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, raw
  command output, credential-like values, or hidden validation details.
- The page may name local artifact families only as private working material
  that should not be published raw.
- Public examples use authored concept examples or existing public-safe demo
  summaries.
- Any synthetic example uses generic role labels and fictional review context,
  not private repository names, customer names, service names, owner names,
  real internal review dates, private sample names, or hidden roadmap details.
- Private repository evidence is described as requiring private review before
  any public-safe summary is created.
- Snippet-like text is avoided unless it is synthetic review-brief language
  containing no code, SQL, configuration values, secrets, local paths, private
  identifiers, raw command output, or raw artifact content.

### Requirement 6: Link to adjacent public-safe surfaces

The future page shall help readers move to neighboring proof, packet,
validation, and boundary pages without overstating what those pages prove.

Acceptance criteria:

- Before linking to a route, implementation verifies that the route resolves in
  generated site output and records unresolved or substituted links in
  `implementation-state.md`.
- Candidate cross-links include `/proof-paths/`, `/packets/`,
  `/review-room/`, `/validation/`, `/limitations/`,
  `/use-cases/endpoint-review/`, `/use-cases/incident-review/`,
  `/static-vs-runtime/`, `/review-claim-checklist/`, and `/use-cases/`.
- Cross-link anchor text stays bounded, such as `proof paths`, `packet
  vocabulary`, `review room`, `validation limits`, `limitations`,
  `endpoint review use case`, `static versus runtime`, and `claim checklist`.
- The page distinguishes itself from `/review-room/` by focusing on the brief
  packet prepared before or during a review, not the full meeting surface.
- The page distinguishes itself from `/packets/` by focusing on change-review
  questions and next owners, not packet taxonomy.
- The page distinguishes itself from `/use-cases/endpoint-review/` by covering
  change review generally, not endpoint-specific review.
- The page distinguishes itself from `/use-cases/incident-review/` by covering
  pre-review and in-review change preparation rather than incident-response
  review.
- The page distinguishes itself from `/static-vs-runtime/` by applying static
  versus runtime boundaries to a review brief, not explaining observability as
  the primary topic.
- The page distinguishes itself from `/review-claim-checklist/` by packaging a
  specific review conversation, not deciding whether a sentence may be
  repeated.
- The implementation records, in public copy and in `implementation-state.md`,
  why the selected page does not duplicate `/team-evidence-handoff/`,
  `/manager-packet/`, `/static-triage/`, `/manager-brief/`, or
  `/deploy-audit/` when those routes exist at implementation time.
- `/use-cases/incident-review/` differentiation is handled by the dedicated
  incident-review criterion above.
- Because `/team-evidence-handoff/` shares nearly the same evidence-field set,
  the differentiation note must explain why a separate page is warranted
  rather than a section on that page.

### Requirement 7: Add focused validation in the implementation phase

The future implementation shall validate route content, metadata, links, and
claim boundaries.

Acceptance criteria:

- Validation confirms the rendered page or section contains `Public claim
  level: concept`, the shared principle, and the required section labels:
  `Change Context`, `Evidence Packet`, `Review Questions`, `Stop Conditions`,
  `Next Owners`, `Limitations`, and `Non-Claims`.
- Validation confirms visible copy contains the phrase `No public conclusion
  without evidence`.
- Validation confirms the page follows the same accessibility checks applied
  to neighboring concept pages.
- Validation confirms the rendered page positively states replacement and
  approval non-claims, including that the brief does not replace tests, code
  review, source review, runtime observability, or release review, and does not
  approve a release.
- Validation confirms route metadata includes title, description, canonical
  URL, Open Graph title/description/url/type fields following the chosen
  neighboring concept-page pattern, commonly `og:type=article`, and
  `publicClaimLevel: concept` when implemented as a standalone route.
- For a standalone route, implementation adds a dedicated validator module
  `site/scripts/change-review.mjs` exporting `validateChangeReviewDist`,
  registers its import and invocation in `site/scripts/validate.mjs` alongside
  existing `validate<Page>Dist` calls, and adds
  `site/scripts/change-review.test.mjs` following the endpoint-review and
  team-evidence-handoff validator/test pattern.
- For section placement, implementation instead extends the host page's
  existing validator module and matching `*.test.mjs` rather than adding a new
  standalone validator module.
- Validation confirms discovery metadata includes a bounded limitation and
  non-claims for runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI/LLM impact analysis,
  and complete coverage.
- Validation confirms discovery metadata carries `publicClaimLevel: concept`
  for the standalone route.
- Validation confirms `routes-index.json` or the current route-index artifact
  includes the standalone route and its `nonClaims` field when comparable
  concept pages use that schema.
- Validation confirms sitemap metadata includes the standalone route when
  comparable public concept pages such as `/team-evidence-handoff/` and
  `/static-vs-runtime/` are indexed there.
- If implemented as a section on an existing route, validation adds the
  required-copy, sanctioned-section-ID, and forbidden-copy checks to that host
  page's existing validator, and the new sanctioned section IDs are namespaced
  so they do not collide with the host page's existing `sanctionedSectionIds`.
- The page marks its non-claim and boundary copy with concrete, namespaced
  sanctioned section IDs such as `change-review-non-claims`,
  `change-review-stop-conditions`, and `change-review-limitations`,
  consistent with neighboring concept-page validators that use
  `sanctionedSectionIds` and `removeSectionsById`.
- Validation confirms the required section labels and non-claim vocabulary
  still render inside the sanctioned regions before those regions are stripped
  for forbidden-copy scans.
- Validation confirms required internal links resolve in generated site
  output.
- Validation partitions forbidden-content scans using the neighboring
  concept-page validator pattern:
  - Whole-page scan, never allowed anywhere including sanctioned sections: real
    local paths, file URLs, localhost/loopback addresses, raw repository
    remotes, connection strings, credential or secret values, raw command
    output, hidden validation details, blame/scare framing, and actual private
    identifier values such as private repository names, customer names,
    service names, owner names, private sample names, and real internal review
    dates. Because arbitrary private-identifier values cannot be fully
    pattern-matched, value-level exclusion is enforced by authored synthetic
    examples, `./scripts/check-private-paths.sh`, and manual review; the
    descriptive phrases naming these categories remain allowed only inside
    sanctioned regions.
  - Sanctioned-region-stripped scan, allowed only inside the sanctioned `Stop
    Conditions`, `Limitations`, and `Non-Claims` regions: artifact-family
    names such as `facts.ndjson`, `index.sqlite`, `report.md`,
    `scan-manifest.json`, `logs/analyzer.log`, and `analyzer.log`;
    descriptive boundary phrases such as raw SQL, raw source snippets, config
    values, secrets, credentials, private sample names, and generated scan
    directories; unsupported overclaim wording; and forbidden AI/LLM
    positioning.
  - The page places artifact-family names and descriptive boundary phrases
    only inside sanctioned-id regions unless an existing public-safe validator
    pattern proves a narrower allowance is safe.
- Validation checks sanctioned-region-stripped rendered copy for unsupported
  overclaim wording with patterns that avoid legitimate review-process nouns
  while still catching unsupported `impacted` wording and claims such as safe
  or unsafe change, approval, blocking, root cause, validated for release,
  production proven, operational assurance, and production observability tool.
  Any bare `safe` check must exempt the compound form `public-safe`, for
  example with `/(?<!public-)\bsafe\b/i`, consistent with neighboring
  concept-page validators.
- The sanctioned-region-stripped overclaim scan also catches unsupported
  replacement and approval claims such as `replaces tests`, `replaces code
  review`, `replaces release review`, `replaces source review`, `approves the
  release`, and `release approval` outside the sanctioned `Non-Claims` region.
- Validation checks sanctioned-region-stripped rendered copy for forbidden
  AI/LLM positioning such as `AI-powered`, `AI impact analysis`,
  `LLM-powered`, `LLM analysis`, `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`,
  `intelligent impact analysis`, and `smart impact`.
- Validation enforces a rendered main-content word count consistent with the
  chosen neighboring concept-page validator, such as endpoint review's 700 to
  1900 words or team evidence handoff's 400 to 1500 words, and records the
  selected range in `implementation-state.md`.
- If section placement is chosen, implementation is expected to record a
  section-appropriate word-count range rather than inheriting the standalone
  route's neighboring-validator word-count range by default.
- Implementation validation includes `npm test`, `npm run validate`, and
  `npm run build` from `site/`, plus `git diff --check`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
  when route or layout changes are made.

### Requirement 8: Keep implementation state current

The future implementation shall keep this spec's task and state files aligned
with actual work.

Acceptance criteria:

- Future implementation tasks in `tasks.md` remain unchecked during this
  spec-only phase.
- Implementation tasks are checked only after the corresponding future
  implementation and validation work is complete.
- This spec-only phase keeps `Status: not-started` and moves `Readiness` to
  `ready-for-implementation` only after Medium or higher spec-review findings
  are patched or explicitly recorded as not applicable.
- `implementation-state.md` records branch, target base, scope decisions,
  public claim level, review commands and results, validation commands and
  results, oddities, and follow-up items.
- If a Kiro review identifies Medium or higher findings, the spec either
  patches them and records the rerun result or records why a rerun was not
  feasible.
