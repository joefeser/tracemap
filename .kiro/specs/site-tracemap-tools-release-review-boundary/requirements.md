# Site TraceMap Tools Release Review Boundary Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe page or section for `tracemap.tools` that explains
TraceMap's boundary in release review. The surface should show what
deterministic static evidence can contribute before or during release review
while stating clearly that TraceMap does not approve releases, prove runtime
safety, replace release controls, or replace human release judgment.

This is a spec-only public site phase. It does not implement site source,
site validation scripts, scanner behavior, reducer behavior, release
automation, generated artifacts, runtime telemetry, AI/LLM analysis,
embeddings, vector databases, prompt classification, or public copy changes.

The future surface is a release-review orientation and handoff page, not a
release gate. It should help release owners, reviewers, service owners,
runtime owners, test owners, security owners, managers, and engineers separate
static repository evidence from release approval, runtime safety, deployment
success, and production behavior.

## Shared Site Principle

No public conclusion without evidence.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Claim Level Rationale

The future surface starts at `Public claim level: concept` because it is an
orientation page about release-review boundaries. It does not publish new demo
evidence, does not prove a scanner or reducer capability by itself, and does
not establish release approval or safety.

Do not upgrade the page or section to `demo` merely because supporting links
point to demo-backed routes. A future claim-level upgrade requires a separate
evidence-backed decision recorded in this spec's `implementation-state.md`.

## Candidate Placement

Candidate placements for future implementation are:

- `/release-review-boundary/`
- `/review-room/release-boundary/`
- A section on `/limitations/`
- A section on `/static-vs-runtime/`

Future implementation must choose the final placement and record rejected
alternatives in `implementation-state.md` before changing site source.

## Relationship To Existing Public Surfaces

The release-review boundary surface is a role-and-decision boundary page. It
complements, but must not replace or duplicate, these existing or candidate
surfaces when present:

- `/limitations/`: site-wide non-claims and coverage limits. The release
  boundary applies those limits to release-review decisions.
- `/static-vs-runtime/`: separates static evidence from runtime telemetry. The
  release boundary links there when release questions need runtime behavior,
  production traffic, endpoint performance, operational safety, or telemetry.
- `/review-claim-checklist/`: tells reviewers whether a claim can be repeated
  with proof. The release boundary tells release participants what TraceMap
  evidence may contribute and which release owner still owns the decision.
- `/deploy-audit/`: explains deploy-audit evidence or deployment-adjacent
  review if present. The release boundary must not claim deploy success,
  deploy safety, or deployment approval.
- `/validation/`: explains validation evidence. The release boundary may
  require validation evidence as one row in the matrix, but must not turn
  validation into release approval or runtime safety.
- `/manager-packet/`: frames manager-facing evidence conversations. The
  release boundary gives a narrower release-review handoff.
- `/questions/objections/`: handles skeptical objections. The release boundary
  answers the specific release-review ownership question.
- `/review-room/`: frames review meeting context when present. The release
  boundary may live near it or link to it, but must not replace the broader
  evidence review agenda.

If any route has moved or is unavailable at implementation time, the
implementation must select the closest live public-safe equivalent or defer
the link, then record the decision and rationale in `implementation-state.md`.
Dead links are not acceptable.

## Claim Boundaries

The future surface may explain deterministic static evidence vocabulary:
changed source surfaces, package and configuration surfaces, route or endpoint
adjacency, SQL or data surfaces, rule IDs or rule families, evidence tiers,
coverage labels, limitations, validation evidence, public-safe summaries,
analysis gaps, and owner handoffs.

The future surface must not claim or imply:

- release approval;
- release safety;
- operational safety;
- production proof;
- runtime behavior proof;
- endpoint performance proof;
- deployment success proof;
- absence-of-impact proof;
- complete coverage;
- AI analysis, LLM analysis, embeddings, vector databases, or prompt-based
  classification in the core scanner or reducer;
- replacement of release controls, tests, source review, code review,
  runtime observability, service-owner review, release-owner decisions, or
  human judgment.

The future surface must not publish raw `facts.ndjson`, raw `index.sqlite`,
analyzer logs, raw source snippets, raw SQL, config values, secrets, local
paths, raw repository remotes, generated scan directories, private sample
names, raw command output, hidden validation details, or credential-like
values.

The future surface must not use blame language around teams, services,
vendors, consultants, reviewers, owners, release managers, test owners, or
code quality. Release-boundary copy should route questions to evidence and
owners without implying fault.

## Requirements

### Requirement 1: Publish a bounded release-review boundary in a future phase

The future implementation shall publish a concept-level public page or section
that explains TraceMap's role before and during release review while preserving
release-owner authority.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The implementation chooses one final placement from
  `/release-review-boundary/`, `/review-room/release-boundary/`, a section on
  `/limitations/`, a section on `/static-vs-runtime/`, or a recorded
  equivalent if site information architecture has changed.
- The implementation records the final placement, rejected alternatives, and
  rationale in `implementation-state.md`.
- The rationale explains why the surface is a static-evidence release-review
  handoff, not a release gate, release approval, safety claim, deploy audit,
  validation proof, runtime workflow, checklist replacement, manager packet,
  or objection guide.
- If implemented as a standalone route, page metadata, route metadata,
  sitemap metadata, and discovery metadata use `publicClaimLevel: concept`.
- If implemented as a section, the host page metadata remains concept-level
  or more conservative and does not imply a shipped release-control,
  release-approval, deployment, runtime, telemetry, or safety capability.
- The surface uses existing static site layout, navigation, metadata,
  accessibility, and validation patterns.
- The surface introduces no runtime service, release automation, approval
  form, telemetry collection, local scanner invocation, generated evidence
  artifact, client-side state requirement, or external release-system
  integration.

### Requirement 2: Explain what static evidence can contribute

The future surface shall show how TraceMap evidence may help release-review
participants prepare questions, route follow-ups, and preserve gaps without
turning static evidence into release approval.

Acceptance criteria:

- The surface includes a section titled or equivalent to `What static evidence
  can contribute`.
- The section explains that TraceMap can orient static repository evidence
  from a specific repo snapshot and commit when public-safe proof exists.
- Static evidence examples include changed source surfaces, package or
  configuration surfaces, route or endpoint adjacency, SQL or data surfaces,
  rule IDs or rule families, evidence tiers, file paths, line spans, extractor
  versions, scan commit, coverage labels, limitations, and analysis gaps only
  when those examples are public-safe.
- The section states that static evidence can help identify review questions,
  adjacent code surfaces, validation evidence to inspect, owners to involve,
  and gaps to keep visible.
- The section does not say TraceMap decides whether a release may proceed,
  whether a change is safe, whether runtime behavior is correct, whether
  production is healthy, whether an endpoint performs acceptably, whether a
  deployment succeeded, or whether no impact exists.
- The section treats unknowns as first-class: reduced, partial, unavailable,
  future-only, syntax-only, or private-only evidence remains visible and
  cannot be smoothed into clean release language.

### Requirement 3: Explain what release review still owns

The future surface shall identify release-review responsibilities that remain
outside TraceMap.

Acceptance criteria:

- The surface includes a section titled or equivalent to `What release review
  still owns`.
- The section names release-owner decision, test results, code review, source
  review, service-owner judgment, runtime observability, operational
  readiness, deployment verification, rollback planning, security review when
  relevant, and human release controls as release-review or adjacent owner
  responsibilities.
- The section explains that TraceMap evidence may inform questions for these
  owners but does not replace their evidence, judgment, gates, or approvals.
- Release-owner decisions remain owned by a release owner or explicitly named
  role category, never by TraceMap, the static site, an automated scan, or an
  agent.
- The section avoids blame language and avoids implying that missing static
  evidence proves an owner failed to review something.

### Requirement 4: Include the required release-boundary rows

The future surface shall include a scannable release-boundary matrix with all
required rows and stable row semantics.

Acceptance criteria:

- Include row `changed source surface`.
- Include row `package/config surface`.
- Include row `route/endpoint adjacency`.
- Include row `SQL/data surface`.
- Include row `coverage gap`.
- Include row `validation evidence`.
- Include row `runtime telemetry need`.
- Include row `release-owner decision`.
- Each row includes at least these fields: release-review question, TraceMap
  contribution, evidence needed, boundary or non-claim, stop condition,
  required next owner, public claim level, and supporting route.
- Row-level public claim level is `concept` unless exact public-safe evidence
  supports a stronger row-level claim and the decision is recorded in
  `implementation-state.md`.
- `TraceMap contribution` uses bounded language such as `can orient`,
  `can show static evidence when available`, `can label a gap`, `can route a
  follow-up`, or `cannot own this decision`.
- `evidence needed` names public-safe categories such as rule ID or rule
  family, evidence tier, coverage label, proof path, limitation, validation
  status, public-safe generated summary, or linked concept/demo page.
- `boundary or non-claim` states what the row must not imply.
- `stop condition` states when TraceMap evidence is insufficient, private-only,
  raw-only, partial, reduced, unavailable, runtime-dependent, release-owned,
  or outside static evidence boundaries.
- `required next owner` uses role categories such as release owner, service
  owner, runtime observability owner, test owner, code reviewer, security
  owner, build or tooling owner, manager, or TraceMap site owner. It does not
  name private people or teams.
- `supporting route` resolves in generated output or is deferred with a
  recorded rationale; no dead links.

### Requirement 5: Preserve row-specific semantics

The future matrix shall answer each required row with the correct release
boundary and owner handoff.

Acceptance criteria:

- `changed source surface`: static evidence may orient changed source files,
  symbols, contracts, or references when public-safe proof exists; release
  review still owns source review, code review, test judgment, and whether the
  change is acceptable for release.
- `package/config surface`: static evidence may identify package,
  project-file, or configuration surfaces when public-safe proof exists; it
  does not prove runtime configuration, deployment settings, secrets handling,
  environment parity, or production behavior.
- `route/endpoint adjacency`: static evidence may show route or endpoint
  adjacency when rules support it; it does not prove live traffic, endpoint
  performance, request behavior, production reachability, or service safety.
- `SQL/data surface`: static evidence may identify SQL-facing or data-facing
  surfaces when public-safe proof exists; it does not publish raw SQL, prove
  data migration safety, prove data correctness, or replace service-owner,
  security-owner, or release-owner review.
- `coverage gap`: reduced, partial, failed, syntax-only, unavailable,
  private-only, or future-only coverage is a visible gap that must downgrade a
  claim or route owner follow-up; it does not prove no impact or complete
  coverage.
- `validation evidence`: validation results may inform whether public-safe
  site or demo evidence is internally consistent; they do not prove release
  safety, operational safety, deployment success, runtime behavior, or test
  sufficiency.
- `runtime telemetry need`: runtime questions require logs, traces, metrics,
  telemetry, dashboards, runtime tests, operational signals, or service-owner
  interpretation; TraceMap does not supply production proof.
- `release-owner decision`: release approval, release hold, go/no-go, risk
  acceptance, rollback readiness, and exception handling belong to release
  owners and release controls; TraceMap cannot approve, block, certify, or
  guarantee a release.

### Requirement 6: Publish forbidden claims, safe wording, stop conditions,
and required next owners

The future surface shall make release-boundary copy usable by humans and
agents without allowing claim upgrades.

Acceptance criteria:

- Include a `Forbidden claims` section or equivalent that blocks release
  approval, release safety, operational safety, production proof, runtime
  behavior proof, endpoint performance proof, deployment success proof,
  absence-of-impact proof, complete coverage, AI/LLM analysis, replacement of
  release controls, and replacement of human judgment.
- Include a `Safe wording` section or equivalent with bounded phrases such as
  `TraceMap can orient static evidence`, `the evidence suggests a review
  question`, `coverage is reduced`, `runtime evidence is required`, `release
  owner decision required`, and `owner follow-up needed`.
- Safe wording does not use `approved`, `safe`, `certified`, `validated for
  release`, `production proven`, `deployment succeeded`, `no impact`, or
  equivalent release-strength conclusions except inside explicit forbidden
  examples or non-claim wording.
- Include a `Stop conditions` section or equivalent that covers missing proof
  path, missing rule ID or rule family, missing evidence tier, missing
  coverage label, private-only evidence, raw-only artifact, hidden detail,
  unsupported demo claim, reduced or failed coverage, runtime-dependent
  question, release-owned decision, forbidden release/safety wording, and
  AI/LLM or confidence-based claim.
- Include a `Required next owners` section or equivalent that maps question
  types to role categories: release owner, service owner, runtime
  observability owner, test owner, code reviewer, security owner, build or
  tooling owner, manager, and TraceMap site owner.
- Owner labels remain role categories, not private individual names or private
  team names.
- The page does not assign accountability, blame, staffing, priority, incident
  command, release authority, or organizational decision rights.

### Requirement 7: Publish explicit non-claims and private-material rules

The future surface shall keep public and private material separated and make
non-claims visible.

Acceptance criteria:

- Include a `Non-claims` section or equivalent stating that TraceMap does not
  approve releases, prove release safety, prove operational safety, prove
  production behavior, prove runtime behavior, prove endpoint performance,
  prove deployment success, prove absence of impact, prove complete coverage,
  perform AI/LLM impact analysis, or replace release controls and human
  judgment.
- State that private-only evidence can support internal follow-up but cannot
  be cited as public proof until summarized through a public-safe route or
  artifact.
- Do not publish or link directly to raw facts, raw SQLite files, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, raw
  command output, hidden validation details, or credential-like values.
- Artifact family names may be mentioned only as private-material boundary
  examples or public-safe source categories; they must not be presented as
  public proof artifacts unless linked through a public-safe summary route.
- Any illustrative examples are synthetic or already-public/demo-sourced,
  labeled as examples, and contain no private sample identities, unreleased
  capability names, hidden route names, internal reviewer names, internal
  owner names, real internal dates, counts, cadence, sequencing, source
  snippets, SQL, config values, secrets, runtime telemetry, or production
  identifiers.

### Requirement 8: Add discovery and supporting links without inflating claims

The future implementation shall make the surface discoverable while preserving
concept-level wording and release boundaries.

Acceptance criteria:

- If implemented as a standalone route, add route metadata, sitemap metadata,
  discovery metadata, canonical URL, page title, description, and Open Graph
  fields using existing site patterns.
- Standalone discovery metadata uses `publicClaimLevel: concept` and does not
  imply release approval, release safety, deployment success, runtime proof,
  operational safety, or a shipped release-control integration.
- If implemented as a section, add a stable in-page anchor and verify the host
  page title, description, social metadata, sitemap entry, discovery metadata,
  and any bot-oriented discovery entry remain concept-level or more
  conservative.
- Add bounded cross-links to relevant live public-safe pages such as
  `/limitations/`, `/static-vs-runtime/`, `/review-claim-checklist/`,
  `/deploy-audit/`, `/validation/`, `/manager-packet/`, and
  `/questions/objections/` when those routes exist at implementation time.
- Verify every outbound and inbound link resolves in generated output before
  publishing. Record unavailable, moved, or deferred targets in
  `implementation-state.md`.
- Add at least one inbound link from a relevant live governance, proof,
  limitations, review-room, validation, or questions surface if the release
  boundary is implemented as a standalone route.
- Cross-link text uses bounded language and does not imply TraceMap approves,
  certifies, validates, guarantees, or replaces release review.

### Requirement 9: Validate release-boundary safety

The future implementation shall include focused validation for required rows,
links, metadata, forbidden claims, private material, word count, and browser
sanity.

Acceptance criteria:

- Add focused validation for the visible `Public claim level: concept` label.
- Add focused validation for visible `No public conclusion without evidence`.
- Add focused validation for every required release-boundary row:
  `changed source surface`, `package/config surface`,
  `route/endpoint adjacency`, `SQL/data surface`, `coverage gap`,
  `validation evidence`, `runtime telemetry need`, and
  `release-owner decision`.
- Add focused validation for required row fields: release-review question,
  TraceMap contribution, evidence needed, boundary or non-claim, stop
  condition, required next owner, public claim level, and supporting route.
- Add focused validation for required links, link resolution, inbound link
  presence when standalone, and recorded substitutions or deferrals. The
  validation must fail when a supporting route is dead and no substitution,
  deferral, or blocking rationale is recorded.
- Add focused validation for page metadata, route metadata, sitemap metadata,
  and discovery metadata if standalone.
- Add focused validation for section anchor and host-page metadata if
  sectioned.
- Add focused validation for forbidden release claims in rendered text,
  decoded HTML, raw HTML attributes, metadata, sitemap output, discovery
  output, tests, fixtures, and generated pages, while allowing bounded
  forbidden-claim examples, non-claim sections, stop-condition rows, and
  release-boundary contexts.
- Forbidden-claim validation covers release approval, release safety,
  operational safety, production proof, runtime behavior proof, endpoint
  performance proof, deployment success proof, absence-of-impact proof,
  complete coverage, AI/LLM impact analysis, replacement of release controls,
  and replacement of human judgment.
- Add focused validation for forbidden private or raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- Add focused validation for visible word count bounds of 900 to 2,400 words
  unless amended in `implementation-state.md`, counting rendered body prose and
  release-boundary row cell content while excluding page-level navigation,
  breadcrumbs, site headers, site footers, metadata blocks, and the
  release-boundary matrix column header row cells. Row names and data-cell
  values still count as visible content.
- Wire focused validation into the existing aggregate site validation workflow
  rather than relying only on manual review.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks if layout, table, responsive,
  or interaction changes are made. Use existing site browser-check patterns
  when available; otherwise use at least one wide desktop viewport and one
  narrow mobile viewport and record the exact viewport sizes in
  `implementation-state.md`.
- Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
