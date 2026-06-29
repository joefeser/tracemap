# Site TraceMap Tools Evidence Gap Register Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe `tracemap.tools` page or section that teaches
readers how to record an evidence gap, reduced coverage, missing proof path,
or private-only support as a bounded follow-up item.

This is a spec-only packet. It does not implement site source, generated
output, scanner behavior, reducer behavior, validation scripts, navigation,
sitemap metadata, public copy, AI or LLM analysis, embeddings, vector
databases, prompt classification, release approval, runtime proof, or human
review replacement.

The future surface shall help engineers, reviewers, managers, and agents keep
incomplete evidence useful without treating a gap as proof of safety,
absence of impact, runtime behavior, complete coverage, operational safety, or
release readiness.

## Shared Site Principle

No public conclusion without evidence.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Claim Level Rationale

The future surface starts at `Public claim level: concept` because it is a
recording pattern for follow-up items. It explains how to preserve evidence
boundaries when a proof path is missing, reduced, stale, private-only,
unsupported, unknown, or waiting on an owner. It does not create new scanner
facts, reducer findings, runtime telemetry, demo artifacts, release decisions,
or operational assurances.

Do not upgrade the future surface to `demo` merely because some rows link to
demo-backed public routes. A future upgrade requires a separate evidence-backed
decision in this spec's `implementation-state.md`.

## Candidate Placement

Candidate placements for future implementation are:

- `/evidence/gaps/`
- `/coverage/gaps/`
- A section on `/limitations/reduced-coverage/`
- A section on `/reviewer-quickstart/`

Future implementation must choose the final placement and record rejected
alternatives in `implementation-state.md` before changing site source.

## Relationship To Adjacent Site Surfaces

The evidence gap register answers: "What follow-up item should we record when
evidence is missing, reduced, stale, private-only, or unable to support a
public conclusion?"

It must remain distinct from these neighboring surfaces when present:

- `/limitations/reduced-coverage/`: explains reduced coverage and labels.
  The gap register records specific follow-up rows after a reduced or missing
  proof path is discovered.
- `/limitations/`: defines canonical boundaries and non-claims. The register
  uses those boundaries but does not replace them.
- `/validation/`: explains checks and validation evidence. The register names
  the validation route still needed but does not certify validation success.
- `/questions/objections/`: answers stakeholder objections. The register
  records follow-up items rather than persuasive responses.
- `/owners/follow-up/`: or an equivalent owner handoff surface, if present,
  explains ownership workflow. The register supplies the gap row that can be
  handed off.
- `/decisions/evidence-record/`: records a human decision after evidence
  review. The gap register records a missing or insufficient proof state before
  such a decision can safely repeat a public claim.
- `/review-claim-checklist/`: decides whether a claim can be repeated. The
  register provides the "hold, downgrade, or follow up" input when checklist
  evidence is incomplete.

If any route has moved or is unavailable at implementation time, the future
implementation must link the closest public-safe equivalent or record the
substitution, omission, or deferral in `implementation-state.md`. Dead links
are not acceptable.

If the final placement is a section on one of these adjacent surfaces, such as
`/limitations/reduced-coverage/`, the adjacent-surface section resolves the
self-host entry to the host page or the gap-register section anchor rather
than a separate cross-link. The gap-register-versus-host distinction must be
stated inline on that host page, and implementation validation treats the
self-host anchor as a satisfied adjacent-surface reference only when the
self-host case is recorded in `implementation-state.md`.

## Claim Boundaries

The future surface may explain deterministic static evidence vocabulary:
evidence gap, gap label, proof path, public claim level, rule ID or rule
family, evidence tier, coverage label, commit context, extractor version,
limitation, next owner, validation route, stop condition, safe wording,
unsafe wording, non-claim, and residual uncertainty.

The future surface must not claim or imply:

- absence-of-impact proof;
- runtime behavior proof, production traffic proof, endpoint performance proof,
  or outage-cause proof;
- release approval, release safety, operational safety, or governance approval;
- complete coverage, complete proof, or clean-repo status;
- autonomous approval or replacement of human review;
- AI impact analysis, LLM analysis, embeddings, vector databases, prompt-based
  classification, or prompt-based scanner or reducer behavior.

The future surface must not publish raw `facts.ndjson`, raw `index.sqlite`,
analyzer logs, source snippets, SQL, config values, secrets, local paths,
remotes, generated scan directories, private sample names, command output,
hidden validation details, or credential-like values.

The future surface must avoid blame language around teams, vendors,
consultants, service owners, reviewers, prior implementers, customers,
frameworks, or code quality. Prefer neutral language such as `proof path is
missing`, `coverage is reduced`, `public-safe support is unavailable`, `source
context is stale`, or `owner follow-up is needed`.

## Requirements

### Requirement 1: Publish a bounded gap register surface

The future implementation shall publish a concept-level public page or section
that explains how to record an evidence gap as a bounded follow-up item.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface says `No public conclusion without evidence`.
- The surface says an evidence gap is a follow-up item, not proof that impact
  is absent, runtime behavior is known, release is safe, or coverage is
  complete.
- The surface must not imply production traffic proof, endpoint performance
  proof, outage-cause proof, runtime proof, release approval, operational
  safety, complete coverage, clean-repo status, autonomous approval, AI/LLM
  analysis, prompt classification, embedding search, vector database analysis,
  or replacement of human review.
- The implementation chooses one final placement from `/evidence/gaps/`,
  `/coverage/gaps/`, section on `/limitations/reduced-coverage/`, section on
  `/reviewer-quickstart/`, or a recorded equivalent if site information
  architecture has changed.
- The implementation records the final placement, rejected alternatives, and
  rationale in `implementation-state.md`.
- The rationale explains why the surface is a gap register, not the reduced
  coverage playbook, limitations page, validation page, stakeholder objection
  guide, owner follow-up page, evidence decision record, or claim checklist.
- If implemented as a standalone route, title, description, canonical URL,
  Open Graph metadata, sitemap metadata, and discovery metadata use
  concept-level wording and include `publicClaimLevel: concept` or the
  existing site equivalent.
- If implemented as a section, the host page title, description, social
  metadata, sitemap entry, discovery entry, and claim-level wording must not
  imply stronger proof than the host page and gap section support.
- If implemented as a section, stable anchor IDs are added for every required
  section and validation checks duplicate IDs within the host page.
- The surface uses existing static site layout, typography, accessibility,
  metadata, route generation, and validation patterns.
- The surface introduces no runtime service, telemetry collection, analytics
  dependency, form submission, local scanner invocation, generated evidence
  artifact, decision automation, or release gate.

### Requirement 2: Include the required sections

The future surface shall include every section needed to record and use a gap
without turning it into a public conclusion.

Acceptance criteria:

- Include sections for when a gap is useful, gap register fields, example gap
  rows, stop conditions, next-owner handoff, safe wording, unsafe wording, and
  non-claims.
- Include an adjacent surfaces section or equivalent that links to public-safe
  equivalents for `/limitations/reduced-coverage/`, `/limitations/`,
  `/validation/`, `/questions/objections/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, and `/review-claim-checklist/` when they
  exist, or records substitutions, omissions, or deferrals in
  `implementation-state.md`.
- The "when a gap is useful" section explains that a gap row is useful when it
  preserves what evidence exists, what cannot be concluded, and who owns the
  next proof or validation step.
- The "gap register fields" section defines the required field set: gap label,
  what evidence exists, what cannot be concluded, public claim level, next
  owner, proof or validation route, safe wording, and stop condition.
- The "example gap rows" section includes all required rows from Requirement 3.
- The "stop conditions" section says readers must stop before stronger public
  wording when proof is missing, stale, private-only, unsupported, unknown, or
  validation evidence is absent.
- The "next-owner handoff" section uses public role categories rather than
  private people, team names, customer names, service names, repository names,
  or sample names.
- The "safe wording" section preserves labels such as `missing proof path`,
  `reduced coverage`, `Tier4Unknown`, `private-only`, `stale`,
  `unsupported framework surface`, `missing validation evidence`, and
  `unresolved owner question`.
- The "unsafe wording" section appears only in an explicitly bounded rejected
  pattern region that implementation validation can identify.
- The rejected-pattern region must use a stable, detectable marker recorded in
  `implementation-state.md` before validation is written. Prefer an existing
  site convention such as `data-blocked-phrase` or a site-consistent
  equivalent over a new marker.
- The "non-claims" section restates that the register does not prove absence
  of impact, runtime behavior, release safety, operational safety, complete
  coverage, AI/LLM analysis, replacement of human review, or the other
  forbidden claims enumerated in Requirement 1 and Requirement 4.

### Requirement 3: Provide the required example gap rows

The future surface shall include a scannable table, matrix, or equivalent
programmatic repeated-row structure for required evidence-gap scenarios.

Acceptance criteria:

- Include required rows for missing proof path, reduced coverage,
  `Tier4Unknown`, private-only support, stale commit, unsupported framework
  surface, missing validation evidence, and unresolved owner question.
- Each row includes gap label, what evidence exists, what cannot be concluded,
  public claim level, next owner, proof/validation route, safe wording, and
  stop condition.
- The row structure uses accessible table semantics or an equivalent card/list
  pattern with programmatically associated row labels and field labels.
- On narrow/mobile viewports, the row structure remains readable without hiding
  the gap label, what evidence exists, what cannot be concluded, next owner,
  proof/validation route, safe wording, or stop condition. Public claim level
  may be summarized once for the row group when it is uniform.
- Row labels are neutral and do not attribute the gap to a person, team,
  customer, service, vendor, reviewer, prior implementer, or code quality.
- Proof/validation route values resolve only to public-safe routes,
  documentation, validation pages, limitations pages, claim-checklist pages,
  owner handoff pages, public-safe summaries, or demo summaries that do not
  expose raw or private material.
- Proof/validation route values are non-empty and non-placeholder in the
  implemented page unless a target is explicitly recorded as deferred,
  substituted, or omitted in `implementation-state.md`.
- No more than two required rows may have deferred, substituted, or omitted
  proof/validation routes at one time without a spec amendment.
- The missing proof path row states that an unavailable proof path cannot
  support a public proof-link claim.
- The reduced coverage row states that reduced coverage cannot support clean,
  complete, release-safe, or absence-of-impact wording.
- The `Tier4Unknown` row states that unknown evidence cannot be upgraded by
  confidence, repetition, reviewer seniority, or stakeholder pressure.
- The private-only support row states that private-only evidence cannot be
  cited as public proof until summarized through a public-safe route.
- The stale commit row states that stale source context cannot support
  current-head, current-release, or current-proof wording.
- The unsupported framework surface row states that unsupported framework
  evidence cannot support complete framework or route coverage.
- The missing validation evidence row states that absent validation cannot
  support validation-passed, demo-backed, or implementation-ready wording.
- The unresolved owner question row states that an unanswered owner question
  cannot be converted into an assumption of safety or no impact.

### Requirement 4: Define safe wording, unsafe wording, and non-claims

The future surface shall separate bounded gap-register wording from
unsupported conclusions.

Acceptance criteria:

- Safe wording examples preserve the gap label and next step, for example:
  `Record this as a missing proof path and link the owner to the validation
  route before repeating the claim.`
- Safe wording examples say what is known and what remains unknown without
  using unqualified `safe`, `approved`, `resolved`, `clean`, `complete`, or
  `no impact` language.
- Unsafe wording examples are framed as rejected patterns, not live claims,
  and are excluded from metadata, summaries, captions, link text, discovery
  records, and page descriptions.
- The rejected-pattern region must use a stable, detectable marker recorded in
  `implementation-state.md` before validation is written. Acceptable markers
  include an existing site convention such as `data-blocked-phrase`, a
  dedicated data attribute, a dedicated component, or an equivalent
  site-consistent wrapper that validators can locate deterministically.
- Unsafe wording rejects absence-of-impact proof, runtime proof, release
  approval or release safety, production traffic proof, endpoint performance
  proof, outage-cause proof, operational safety, complete coverage, clean-repo
  status, AI/LLM analysis, prompt-based classification, embedding search,
  vector database analysis, autonomous approval, and replacement of human
  review.
- Non-claims say the gap register does not run TraceMap, create facts, replace
  scanner artifacts, validate a build, approve a release, prove production
  behavior, assign fault, or settle owner questions.
- Public copy and examples do not include raw facts, SQLite, analyzer logs,
  source snippets, SQL, config values, secrets, local paths, remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, or credential-like values.

### Requirement 5: Preserve adjacent links and implementation validation

The future implementation shall include focused validation for the register's
required rows, links, metadata, claim boundaries, and browser behavior.

Acceptance criteria:

- Add focused validation for visible `Public claim level: concept`.
- Add focused validation for visible `No public conclusion without evidence`.
- Add focused validation for required sections.
- Add focused validation for required rows and required row fields.
- Add focused validation for accessible table semantics or equivalent
  programmatic row-label and field-label association in the example gap rows
  structure.
- Add focused validation that required row proof/validation routes are
  non-empty, non-placeholder, and resolve to allowed public-safe targets or are
  explicitly recorded as deferred, substituted, or omitted in
  `implementation-state.md`.
- Add focused validation that every adjacent-surface link and every required
  row proof/validation route resolves to an existing generated page or anchor,
  with no dead internal links or unresolved anchors, or is explicitly recorded
  as deferred, substituted, or omitted in `implementation-state.md`. This
  validation runs against generated output produced by `npm run build`.
- The adjacent-surface link validator treats a self-host anchor as resolved
  when the register is implemented as a section on an adjacent surface,
  provided the self-host placement and inline distinction are recorded in
  `implementation-state.md`.
- Add focused validation that no more than two required rows have deferred,
  substituted, or omitted proof/validation routes.
- Add focused validation for adjacent-surface distinctions and required links
  or recorded substitutions for `/limitations/reduced-coverage/`,
  `/limitations/`, `/validation/`, `/questions/objections/`,
  `/owners/follow-up/`, `/decisions/evidence-record/`, and
  `/review-claim-checklist/`.
- Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when a standalone route is selected.
- Add focused validation for section host metadata, stable anchors, duplicate
  IDs, and anchor resolution when a section placement is selected.
- Add focused validation for forbidden live claims, including absence of
  impact, runtime proof, production traffic proof, endpoint performance proof,
  outage-cause proof, release approval or safety, operational safety, complete
  coverage, clean-repo status, AI/LLM analysis, prompt classification,
  embeddings, vector databases, autonomous approval, and replacement of human
  review.
- Add focused validation that unsafe wording appears only in a
  programmatically identifiable rejected-pattern, limitation, non-claim, or
  validation-warning region.
- The forbidden-live-claim and private/raw-material validators must exclude
  only the marked rejected-pattern, limitation, non-claim, and
  validation-warning regions where unsafe examples or raw-material category
  names are intentionally shown, and must still fail if those patterns appear
  outside marked regions, including body prose, metadata, link text, summaries,
  captions, sitemap output, and discovery records.
- Validation for private/raw material, metadata, sitemap records, discovery
  records, and rendered wording must run against generated output produced by
  `npm run build`, not only against source files.
- Add focused validation for forbidden private/raw material across rendered
  text, decoded HTML, HTML attributes, metadata, sitemap output, discovery
  output, tests, fixtures, and bot-oriented discovery surfaces.
- Add focused validation that row labels and handoff examples avoid blame
  language.
- Add word count validation: 900 to 1,700 visible body words for a standalone
  route, or 450 to 1,000 visible body words for a section placement, excluding
  navigation, metadata, code blocks, and required row field text.
- For word-count purposes, `required row field text` means the content of the
  eight fields within the example gap rows structure: gap label, what evidence
  exists, what cannot be concluded, public claim level, next owner,
  proof/validation route, safe wording, and stop condition. Introductory text,
  section headings, safe-wording lists, unsafe-wording lists, non-claim copy,
  and handoff copy outside the row structure count toward the word-count
  bound.
- Wire focused validation into the existing aggregate site validation workflow.
- Run `npm test` from `site/` after site source is added.
- Run `npm run validate` from `site/` after site source is added.
- Run `npm run build` from `site/` after site source is added.
- Run desktop and mobile browser sanity checks when route, layout, table,
  responsive behavior, or interaction changes are made.
