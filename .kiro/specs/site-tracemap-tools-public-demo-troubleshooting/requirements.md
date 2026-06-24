# Site TraceMap Tools Public Demo Troubleshooting Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe `tracemap.tools` page or section that helps
visitors understand what to check when the public demo, proof route, demo
summary, validation expectation, or claim wording is confusing, stale, or
incomplete.

This is a spec-only packet. It does not implement site code, generated output,
scanner behavior, reducer behavior, runtime diagnosis, support workflow,
navigation, sitemap metadata, public copy, or validation scripts.

The future surface is guidance for public demo interpretation. It is not a
support contract, runtime diagnostic tool, production proof, release approval,
endpoint performance monitor, or replacement for validation and human review.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future page or section shall visibly say `Public claim level: concept`
because it explains how visitors should interpret public demo guidance and
public-safe proof expectations. It does not add a shipped scanner capability,
prove that a demo route is current, produce a fresh public scan, diagnose live
site behavior, validate a release, prove production behavior, or guarantee
complete coverage.

Do not upgrade this surface to `demo` unless a future spec amendment ties the
exact troubleshooting rows, examples, proof links, and validation checks to
checked-in public-safe demo material without publishing raw facts, SQLite,
analyzer logs, source snippets, SQL, config values, secrets, local paths, raw
remotes, generated scan directories, private sample names, command output,
hidden validation details, or credential-like values.

## Audience

- Visitors trying to understand a public TraceMap demo route or summary.
- Engineers and reviewers checking whether public demo wording keeps evidence
  labels, proof boundaries, and non-claims visible.
- Site authors deciding where confusing demo guidance should point next.
- Agents preparing public-site copy that must avoid stronger claims than the
  public evidence supports.

## Core Message

Public demo troubleshooting should help a reader locate the right public-safe
route, label, proof expectation, owner, and stop condition. It should not turn
confusion, stale copy, missing links, reduced coverage, private-only evidence,
or unsupported wording into a conclusion.

The future surface shall teach visitors to say what appears confusing, what
public-safe cause is likely, what public thing to check, what not to conclude,
where the next public-safe route or owner is, and when to stop. It shall avoid
blame language and avoid suggesting that TraceMap performs AI or LLM impact
analysis, prompt-based classification, embedding search, vector database
analysis, live production proof, release safety approval, endpoint performance
diagnosis, or complete coverage.

## Relationship To Adjacent Site Surfaces

The troubleshooting surface answers: "What should I check when the public demo
or proof guidance does not line up?"

It must distinguish itself from these adjacent surfaces:

- `/demo/runbook/`: explains the demo flow and expected reading sequence. The
  troubleshooting surface points back to the runbook when the flow is unclear
  but does not replace the runbook.
- `/demo/start-here/`: orients first-time visitors. Troubleshooting handles
  confusion after a visitor has found an unclear, stale, or incomplete demo
  signal.
- `/demo/result/`: presents a public-safe demo result. Troubleshooting does not
  become the result page or make stronger result claims.
- `/demo/proof-upgrades/`: describes future proof improvements. Troubleshooting
  may point to this route for planned proof work but must not imply that a
  planned proof already exists.
- `/validation/`: remains the validation method and check-result surface.
  Troubleshooting points to validation expectations but does not replace
  validation.
- `/limitations/`: remains the canonical boundary and non-claim surface.
  Troubleshooting points there for claim limits and private/raw material
  boundaries.
- `/questions/objections/`: remains the stakeholder objection surface.
  Troubleshooting gives route-level next checks, not broad objection handling.

## Requirements

### Requirement 1: Choose a bounded placement

The future implementation shall add a public-demo troubleshooting page or
section using one explicit placement.

Acceptance criteria:

- Candidate placements are `/demo/troubleshooting/`, `/demo/help/`, a section
  on `/demo/runbook/`, or a section on `/demo/start-here/`.
- The implementation records the selected placement and rejected alternatives
  in this spec's `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section says `No public conclusion without evidence`.
- The page or section stays out of primary navigation unless a future
  information-architecture review records why the existing site pattern
  supports it.
- If implemented as a standalone route, the implementation adds title,
  description, canonical URL, Open Graph metadata, sitemap metadata, and
  discovery metadata using existing site patterns.
- If implemented as a standalone route, sitemap and discovery metadata keep
  `publicClaimLevel` or equivalent site metadata at `concept`.
- If implemented as a section, the host page's title, description, social
  metadata, sitemap entry, discovery entry, and claim-level wording must not
  imply a stronger public demo claim than the host page and section can
  support.
- If implemented as a section on a host page whose visible claim level is not
  `concept`, the section must explicitly scope its
  `Public claim level: concept` label to the troubleshooting guidance, such as
  `Troubleshooting guidance -- public claim level: concept`, so it does not
  appear to downgrade the host page or conflict with the host's claim-level
  header. The implementation records the host claim level and reconciliation
  wording in `implementation-state.md`, and validation asserts the two
  claim-level statements do not contradict.
- If implemented as a section, the implementation adds stable anchor IDs for
  troubleshooting subsections and validates anchor uniqueness within the host
  page.
- If implemented as a section, the troubleshooting content must not visually
  or structurally dominate the host page's primary purpose. The implementation
  must record a host-page crowding check in `implementation-state.md` with a
  measurable basis, such as the troubleshooting section's rendered height or
  visible word count relative to the host page's primary content. Section
  placement must render the matrix in a compact card/list pattern when needed
  to preserve the host page's primary orientation. When the crowding check
  uses a word-count basis, it must count total rendered section words including
  the troubleshooting matrix because the matrix is the dominant element. This
  is distinct from the Requirement 6 word-count bound, which excludes required
  matrix text. A rendered-height basis inherently includes the matrix.
- Cross-links use bounded anchor text that names the destination topic, such as
  `demo runbook`, `validation expectations`, or `limitations and non-claims`,
  rather than generic text such as `here`, `more`, or claim-asserting phrases.
- Before linking to candidate or adjacent routes, generated output validation
  verifies each link resolves or records the route as deferred, substituted, or
  omitted in `implementation-state.md`.

### Requirement 2: Explain public demo troubleshooting boundaries

The future page shall explain what public demo troubleshooting can and cannot
do.

Acceptance criteria:

- Explain that this is a site/demo guidance surface, not a support contract or
  runtime diagnostic tool.
- Explain that it helps route confusion, stale summary concerns, proof
  expectation mismatches, reduced coverage labels, private-only evidence, and
  unsupported wording to the right public-safe next check.
- Explain that it cannot diagnose production systems, prove live endpoints,
  confirm endpoint performance, approve a release, prove operational safety,
  guarantee complete coverage, or replace validation and human review.
- Explain that demo troubleshooting does not create public proof from private
  or raw material.
- Explain that a missing or confusing route is not evidence that a claim is
  true or false.
- Explain that stale, unavailable, private-only, incomplete, reduced, or
  unsupported labels must remain visible rather than being normalized into
  stronger wording.
- Avoid blame language. Prefer neutral phrasing such as `the public route is
  missing`, `the summary may be stale`, `the proof expectation is incomplete`,
  `coverage is reduced`, `the evidence is private-only`, or `owner follow-up
  is needed`.

### Requirement 3: Provide the required troubleshooting matrix

The future page shall include a scannable matrix or equivalent repeated-row
structure for required public-demo troubleshooting scenarios.

Acceptance criteria:

- Include rows for missing route, outdated demo summary, broken proof
  expectation, reduced coverage label, private-only evidence, unsupported
  claim wording, validation mismatch, and where to ask next.
- Each required row includes symptom, likely public-safe cause, what to check,
  what not to conclude, next owner/route, stop condition, and non-claim.
- The matrix uses accessible table semantics or an equivalent card/list
  pattern with programmatically associated row labels and fields.
- On narrow/mobile viewports, the matrix remains readable without hiding the
  symptom, likely public-safe cause, what to check, what not to conclude, next
  owner/route, stop condition, or non-claim. If any field is progressively
  disclosed on narrow viewports, it must use an accessible, programmatically
  associated disclosure control rather than being removed, and validation
  asserts the field remains reachable.
- Row labels and row copy are neutral and do not attribute missing, stale,
  confusing, reduced, private-only, or conflicting evidence to a person, team,
  service, customer, or reviewer.
- `Next owner/route` values point only to role labels and public-safe routes,
  such as site owner, demo owner, validation owner, limitations route,
  validation route, demo runbook, proof-upgrades route, or questions route.
- No row may cite raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  or credential-like values.
- No row's `what to check` value may direct public visitors to internal spec
  artifacts such as `implementation-state.md`, `tasks.md`, `.kiro/specs/`, or
  other non-public author material. Author-facing checks belong in
  `implementation-state.md`, not public copy.
- The row for missing route states that a missing public route cannot support
  a conclusion that the evidence exists, does not exist, or proves the claim.
- The row for outdated demo summary states that stale summary wording cannot
  support current-head, current-release, or current-proof claims.
- The row for broken proof expectation states that a broken or incomplete proof
  path cannot support public proof wording.
- The row for reduced coverage label states that reduced coverage cannot
  support complete coverage, absence-of-impact, clean-repo, or release-safety
  wording.
- The row for private-only evidence states that private-only evidence cannot
  be used as public proof until summarized through a public-safe route.
- The row for unsupported claim wording states that unsupported wording must be
  downgraded, removed, or routed to limitations and validation rather than
  repeated.
- The row for validation mismatch states that mismatched public validation
  expectations cannot support a passed-validation claim.
- The row for where to ask next states that asking the next owner transfers the
  evidence question and does not prove, approve, or diagnose anything by
  itself.

### Requirement 4: Define safe and unsafe demo-troubleshooting wording

The future page shall separate safe public-demo guidance from unsupported
claims.

Acceptance criteria:

- Safe wording may say a route is missing, a summary may be stale, a proof
  path is incomplete, coverage is reduced, evidence is private-only,
  validation expectations do not match, wording needs to be downgraded, or an
  owner should provide a public-safe update.
- Safe wording may use terms such as `public-safe route`, `demo summary`,
  `proof expectation`, `coverage label`, `validation expectation`,
  `limitations`, `non-claim`, `owner follow-up`, `stop condition`, and
  `public-safe summary`.
- Unsafe wording must be rejected or shown only inside explicitly labeled
  rejected-pattern regions.
- Rejected-pattern regions shall use a programmatically identifiable marker,
  such as a dedicated component, wrapper element, or data attribute.
  Visual-only styling or prose-only labels are insufficient.
- Required non-claim, limitation, and matrix `what not to conclude` and
  `non-claim` copy shall carry a programmatically identifiable non-claim
  marker, distinct from the rejected-pattern marker. Visual-only styling or
  prose-only labels are insufficient.
- Unsafe conclusions include live support SLA, runtime diagnosis, production
  proof, release safety or approval, endpoint performance proof, complete
  coverage, absence-of-impact proof, clean-repo claim under reduced analysis,
  AI/LLM analysis, prompt-based classification, embedding search, vector
  database analysis, autonomous approval, and replacement of validation or
  human review.
- The page must not say TraceMap proves production behavior, live endpoint
  behavior, endpoint performance, release readiness, operational safety, full
  coverage, no impact, or current proof unless a separate public-safe proof
  route explicitly supports that exact claim.
- Unsafe examples, if present, must be framed as rejected patterns and must
  not appear in metadata, summaries, captions, link text, discovery records, or
  route descriptions as affirmative statements.
- Owner-handoff and `where to ask next` copy shall not state or imply response
  times, support channels, ticketing, guaranteed answers, or any
  service-level commitment. It transfers the evidence question only.

### Requirement 5: Preserve public/private and raw-material boundaries

The future page shall keep private, raw, or local material out of public
guidance.

Acceptance criteria:

- Do not publish raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, command output, hidden validation details, or
  credential-like values.
- Refer to local-only or raw artifact families only inside boundary copy that
  says those materials are not public proof and need public-safe summaries
  before public linking.
- Use only synthetic, authored, or already public-safe examples.
- Label illustrative examples as illustrative unless they link to existing
  public-safe demo or documentation surfaces.
- If a future implementation needs to mention unavailable or private-only
  material, public copy uses visible `unavailable` or `private-only` labels
  rather than naming private details.
- Do not name real customers, private repositories, private sample apps,
  private branches, private remotes, local machines, or real credential-like
  values.

### Requirement 6: Require implementation validation

The future implementation shall add focused validation for the troubleshooting
surface.

Acceptance criteria:

- Validate visible `Public claim level: concept`.
- Validate visible `No public conclusion without evidence`.
- Validate all required troubleshooting rows and required row fields.
- Validate required links and adjacent-route distinctions for
  `/demo/runbook/`, `/demo/start-here/`, `/demo/result/`,
  `/demo/proof-upgrades/`, `/validation/`, `/limitations/`, and
  `/questions/objections/`.
- Validate that required row fields are present with table-header association
  or an equivalent programmatic field-label association marker.
- Validate that no public row directs visitors to internal spec artifacts such
  as `implementation-state.md`, `tasks.md`, `.kiro/specs/`, or other
  non-public author material. Relative path validation for directory segments
  such as `.kiro` and `specs` must split candidate paths into segments and
  check individual segment matches rather than using string containment or
  slash-wrapped substring matching.
- Validate that cross-links to adjacent surfaces use bounded anchor text.
- Validate that illustrative examples are synthetic or already public-safe.
- Validate standalone route metadata, sitemap metadata, and discovery metadata
  when a standalone placement is chosen.
- Validate section host metadata, duplicate IDs, and anchor resolution when a
  section placement is chosen.
- Validate section-level claim-label scoping and host claim-level
  reconciliation when the host route's visible claim level is not `concept`.
- Validate a measurable host-page crowding check for section placement,
  including rendered-height or word-count relationship to the host page's
  primary content. Validation records whether the crowding basis is rendered
  height or matrix-inclusive word count, and must not reuse the
  matrix-excluding word-count base for the crowding measure.
- Validate forbidden live claims for support SLA, runtime diagnosis,
  production proof, release safety or approval, endpoint performance, complete
  coverage, absence of impact, clean repo under reduced analysis, AI/LLM
  analysis, prompt-based classification, embedding search, vector database
  analysis, autonomous approval, response-time or ticketing commitments, and
  replacement of validation or human review.
- Validate that every rejected-pattern region carries the programmatic marker
  and that forbidden-claim scanning keys off that marker rather than styling.
- Validate that required non-claim, limitation, and matrix `what not to
  conclude` and `non-claim` regions carry the programmatic non-claim marker.
- Forbidden-claim validation targets affirmative claims only and excludes text
  inside both the marked rejected-pattern region and the marked non-claim
  region. Rejected claim examples and required negated non-claims are allowed
  only inside their respective markers.
- Forbidden private/raw/local-material validation applies everywhere with no
  exception, including rejected-pattern regions, non-claim regions, fixtures,
  tests, rendered text, decoded HTML, raw HTML attributes, metadata, sitemap
  output, discovery output, and bot-oriented discovery surfaces.
- Forbidden private/raw/local-material validation checks for raw facts,
  SQLite, analyzer logs, source snippets, SQL, config values, secrets, local
  paths, raw remotes, generated scan directories, private sample names, command
  output, hidden validation details, and credential-like values.
- Validate row labels and handoff examples avoid blame language.
- Validate word count bounds: 700 to 1,500 visible body words for a standalone
  route, or 350 to 900 visible body words for a section placement, excluding
  navigation, metadata, code blocks, and required matrix text. Required matrix
  text means the content of the required troubleshooting matrix rows and their
  column headers, but not introductory prose, section headings, adjacent-route
  descriptions, safe-wording examples, or rejected-wording examples outside
  the matrix. For section placement, the matrix-inclusive crowding check is the
  governing size guard for total rendered section dominance; this word-count
  bound applies only to surrounding prose.
- Run `npm test` from `site/` after site source is added.
- Run `npm run validate` from `site/` after site source is added.
- Run `npm run build` from `site/` after site source is added.
- Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are implemented.
