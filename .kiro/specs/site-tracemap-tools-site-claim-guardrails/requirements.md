# Site TraceMap Tools Site Claim Guardrails Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public or contributor-facing guardrails page or section for
`tracemap.tools` that explains how site copy may describe TraceMap without
overclaiming. The surface should help future agents, contributors, reviewers,
and maintainers decide when a claim may be shown, when it must be downgraded,
and when it must stay hidden.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, public
copy changes, or existing specs.

Readiness moved from `spec-review` to `ready-for-implementation` after Medium
or higher review findings were patched or explicitly dispositioned in
`implementation-state.md`.

The future surface is guidance for claim discipline. It is not a new product
capability, release gate, runtime proof, approval workflow, safety assertion,
AI/LLM impact-analysis feature, or replacement for human review.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the preferred future surface is
public-facing governance guidance. It describes how contributors and agents
should constrain public wording, but it does not publish a new TraceMap
finding, scanner capability, reducer result, demo result, runtime observation,
release decision, operational safety claim, or shipped workflow.

If a future implementation chooses a strictly contributor-only docs page that
is not included in public discovery, public navigation, sitemap metadata, or
external-facing route output, it may use `Public claim level: hidden` for that
surface and must record the contributor-only placement decision in this
spec's `implementation-state.md`. Public-facing implementations must visibly
render `Public claim level: concept`.

## Claim Boundaries

- The future page or section shall visibly say `Public claim level: concept`
  when public-facing.
- The future page or section shall visibly say
  `No public conclusion without evidence`.
- The future surface may explain public claim levels, proof-path requirements,
  allowed evidence references, forbidden raw material, non-claim patterns,
  downgrade and hidden rules, validation expectations, and review handoff.
- The future surface must not claim new product capability, runtime proof,
  release approval, release safety, operational safety, complete coverage,
  AI/LLM analysis, or replacement of human review.
- The future surface must not publish raw facts, SQLite files, analyzer logs,
  source snippets, raw SQL, config values, secrets, local paths, remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, or credential-like values.
- Public copy must avoid blame language. Missing, reduced, private-only, or
  hidden evidence is a boundary, limitation, downgrade, hidden state, stop
  condition, or owner handoff, not a person or team failure.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs or rule families, evidence tiers, coverage labels, documented
  limitations, public-safe proof paths, and human review decisions.

## Relationship to Existing Site Surfaces

The site claim guardrails surface is a copy-governance rulebook, not another
proof catalog or checklist. It complements these adjacent surfaces:

- `/review-claim-checklist/`: canonical checklist for deciding whether a real
  claim can be repeated with proof, downgraded, sent to an owner, withheld, or
  kept internal.
- `/proof-source-catalog/`: route-to-source mapping for public-safe proof
  sources.
- `/roadmap/`: explanation of claim gates and future/site maturity, not the
  detailed copy guardrail table.
- `/limitations/`: product and analysis limitations, not row-by-row copy
  downgrade rules.
- `/questions/objections/`: stakeholder concerns and answers, not authoring
  policy.
- `/language/change-risk/`: wording patterns for static change-risk language,
  not public claim-level authority.

Future implementation must distinguish the guardrails from those routes and
link to them when they exist and when the link clarifies the boundary. Missing
or renamed routes must be recorded in `implementation-state.md` with the
chosen substitute or deferral.

## Requirements

### Requirement 1: Choose placement and claim level

The future implementation shall choose one placement for the guardrails
surface and record the claim-level decision.

Acceptance criteria:

- The implementation evaluates all candidate placements:
  `/site-claim-guardrails/`, `/docs/site-claim-guardrails/`, a section on
  `/review-claim-checklist/`, or a contributor-only docs page linked from
  `/docs/`.
- The implementation records the selected placement, rejected alternatives,
  and short reasons in this spec's `implementation-state.md`.
- Public-facing standalone or section implementations visibly render
  `Public claim level: concept`.
- Public-facing standalone or section implementations visibly render
  `No public conclusion without evidence`.
- A contributor-only implementation may use `Public claim level: hidden` only
  when it is excluded from public discovery, sitemap metadata, external-facing
  route output, and public navigation; the hidden decision and its rationale
  are recorded in `implementation-state.md`.
- If implemented as a standalone public route, the route is added to sitemap
  metadata, discovery metadata, canonical metadata, title, description, and
  Open Graph metadata using concept-level wording.
- If implemented as a folded section, the implementation records how the
  section's concept-level or hidden claim level is reconciled with the host
  route's metadata and gives each required section a stable anchor.
- The page or section is not added to primary navigation unless an
  information-architecture note records why the existing site pattern supports
  that choice.
- Cross-links use bounded anchor text that does not imply TraceMap proves
  runtime behavior, production traffic, release safety, operational safety,
  complete coverage, AI/LLM analysis, or automated approval.

### Requirement 2: Distinguish adjacent surfaces

The future surface shall explain its role without duplicating or overriding
existing public guidance pages.

Acceptance criteria:

- The surface distinguishes itself from `/review-claim-checklist/` by
  focusing on copy rules and downgrade authority rather than a claim review
  ritual for a specific sentence.
- The surface distinguishes itself from `/proof-source-catalog/` by explaining
  what evidence may be referenced publicly, while the catalog maps routes and
  claim labels to proof sources.
- The surface distinguishes itself from `/roadmap/` by avoiding roadmap status
  promises, sequencing, cadence, or release timing.
- The surface distinguishes itself from `/limitations/` by showing when a
  limitation forces downgrade or hiding, while the limitations page remains a
  broader limitation reference.
- The surface distinguishes itself from `/questions/objections/` by teaching
  authoring boundaries rather than answering stakeholder objections.
- The surface distinguishes itself from `/language/change-risk/` by defining
  claim-level and evidence-publication rules, while the language guide remains
  wording-pattern guidance.
- The surface links to adjacent routes when they exist and records absent,
  renamed, or deferred links in `implementation-state.md`.
- Link text must not imply that this guardrails page is a source of truth over
  scanner facts, reducer findings, generated reports, rule catalogs, coverage
  labels, or documented limitations.

### Requirement 3: Define required sections

The future surface shall include all required guardrail sections using stable
anchors.

Acceptance criteria:

- Include a `public claim levels` section.
- Include a `proof-path requirements` section.
- Include an `allowed evidence references` section.
- Include a `forbidden raw material` section.
- Include a `non-claim patterns` section.
- Include a `downgrade and hidden rules` section.
- Include a `validation expectations` section.
- Include a `review handoff` section.
- Required anchors are stable and machine-checkable:
  `#public-claim-levels`, `#proof-path-requirements`,
  `#allowed-evidence-references`, `#forbidden-raw-material`,
  `#non-claim-patterns`, `#downgrade-and-hidden-rules`,
  `#validation-expectations`, and `#review-handoff`.
- Each required section uses public-safe authored examples only.
- Required sections must not expose raw artifacts, private repository
  material, command output, hidden validation details, or credential-like
  values.
- Every allowed-context zone that may legitimately contain otherwise
  forbidden wording or forbidden-material category names, including rejected
  example, limitation, boundary, and non-claim contexts, uses a stable,
  machine-distinguishable marker such as a fixed wrapper element, class, or
  data attribute so validation can exclude those zones without excluding
  ordinary body copy.

### Requirement 4: Define public claim levels

The future surface shall define the public claim levels and their publication
rules without turning them into product status promises.

Acceptance criteria:

- The page defines `shipped` as public-safe wording backed by main-true source
  behavior or documentation plus public-safe proof path, limitation, and any
  required coverage label.
- The page defines `demo` as public-safe wording backed by checked-in public
  demo proof, public-safe generated summaries, or sanctioned demo artifacts
  plus limitations.
- The page defines `concept` as future-facing, guidance, explanatory, dev-only,
  not-yet-backed, or process-level wording that must not be described as
  shipped or demo-backed.
- The page defines `hidden` as omitted or abstracted detail that must not
  disclose unreleased capability names, private sample identities, internal
  route names, counts, cadence, sequencing, in-flight status, or private proof
  details.
- The page states that a page-level concept label does not upgrade any row or
  statement to shipped, demo, impact-backed, runtime-proven, release-safe, or
  complete.
- The page states that claim levels govern public wording strength; they are
  separate from review outcomes, owner decisions, and product readiness.
- The page states that private-only evidence can support internal follow-up
  but cannot be cited as public proof until summarized through a public-safe
  route or artifact.

### Requirement 5: Define proof-path requirements

The future surface shall specify the evidence fields required before a public
claim can be shown or strengthened.

Acceptance criteria:

- A public claim requires a public-safe proof path, rule ID or rule family,
  evidence tier when applicable, coverage label, documented limitation, and
  source context such as main, public demo, future-only, dev-only, hidden, or
  local-only.
- The page states that no claim can be upgraded by confidence, seniority,
  repetition, urgency, roadmap intent, manager pressure, or appealing wording.
- Rule IDs are required when public-safe and specific; otherwise the surface
  requires a rule-family label plus a limitation explaining why a specific
  rule ID is not public-safe or not available.
- Evidence tiers use only TraceMap's tier vocabulary:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown`.
- Coverage labels are transcribed from cited public-safe evidence and are not
  silently normalized into stronger wording.
- Reduced, partial, unknown, unavailable, future-only, local-only, or hidden
  coverage forces downgrade, hiding, or owner follow-up unless a public-safe
  proof surface supports the narrower statement.
- A missing proof path, missing rule ID or rule family, missing evidence tier
  where applicable, missing coverage label, or missing limitation prevents
  public strengthening.

### Requirement 6: Define allowed evidence references

The future surface shall teach which evidence categories may be referenced in
public copy.

Acceptance criteria:

- Allowed public evidence references include public-safe pages, public-safe
  generated summaries, documentation, rule catalog pages, reports that are
  already public-safe, sanctioned demo artifacts, proof-path entries, review
  packets, route metadata, coverage labels, documented limitations, and
  extractor version labels when safe.
- Public references may name artifact families such as fact streams, SQLite
  indexes, analyzer logs, reports, and rule catalog entries only as categories
  inside boundary guidance, not as raw public proof links.
- Public references must preserve the evidence boundary they cite: rule ID or
  rule family, evidence tier where applicable, coverage label, limitation, and
  source context.
- Public references must not imply runtime observation, production traffic
  knowledge, endpoint performance, outage cause, release approval, operational
  safety, complete coverage, AI/LLM analysis, or replacement of human review.
- If evidence is private-only or raw-only, the allowed public reference is a
  public-safe summary or an owner follow-up note, not the private or raw
  artifact itself.

### Requirement 7: Forbid raw and private material

The future surface shall prohibit public exposure of raw or private material
that could leak implementation details, evidence internals, or credentials.

Acceptance criteria:

- The surface forbids public raw facts, raw SQLite databases, analyzer logs,
  source snippets, raw SQL, config values, secrets, local paths, remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, and credential-like values.
- The prohibition applies to rendered copy, decoded HTML attributes, metadata,
  sitemap or discovery output, examples, fixtures, tests, review packets, and
  validation messages.
- The surface may list forbidden material categories only in an explicit
  boundary or non-claim context.
- Examples use synthetic labels, public route names, or already public-safe
  demo/source-document references.
- Hidden work is represented as one abstract row or omitted; it must not use
  hidden capability names, internal route names, private sample identities,
  counts, cadence, sequencing, release timing, or in-flight status.

### Requirement 8: Define non-claim patterns and forbidden boundaries

The future surface shall teach contributors how to write non-claims that keep
TraceMap inside deterministic static evidence.

Acceptance criteria:

- Include non-claim patterns stating that TraceMap does not prove runtime
  behavior, production traffic, endpoint performance, outage cause, release
  approval, release safety, operational safety, complete coverage, AI/LLM
  impact analysis, or replacement of human review.
- Include non-claim patterns stating that static evidence can show a bounded
  relation, gap, coverage label, or owner handoff without proving impact,
  absence of impact, safety, approval, business correctness, or production
  behavior.
- Unsafe or forbidden example phrases are machine-distinguishable as rejected
  examples, such as a stable wrapper, class, data attribute, or strictly
  negated `do not say` context.
- The page must not use forbidden examples as affirmative product claims.
- Copy uses neutral language such as `evidence shows`, `coverage is reduced`,
  `not established by this scan`, `needs review`, `owner follow-up needed`,
  `internal only`, `downgrade`, `hidden`, and `stop condition`.
- Copy avoids blame language and does not assign fault to a team, reviewer,
  maintainer, contributor, service, or customer.

### Requirement 9: Define downgrade and hidden rules

The future surface shall provide a guardrail table that maps risky claim
conditions to allowed actions.

Acceptance criteria:

- Include all required guardrail rows: shipped, demo, concept, hidden, raw
  artifact reference, dev-only feature, reduced coverage, runtime/release
  wording, AI/LLM wording, and private-only support.
- Each row includes condition, allowed public wording or action, required
  proof path, downgrade or hidden trigger, forbidden implication, and review
  handoff.
- The `shipped` row requires main-true or source-document proof plus
  limitation and forbids runtime, release, safety, complete-coverage, and
  replacement-of-review implications.
- The `demo` row requires checked-in public demo proof or public-safe demo
  summary plus limitation and forbids shipped or production wording.
- The `concept` row allows explanatory or future-facing guidance only and
  forbids shipped, demo-backed, operational, and release wording.
- The `hidden` row requires omission or abstraction and forbids hidden names,
  counts, cadence, sequencing, in-flight status, and private proof details.
- The `raw artifact reference` row forces a public-safe summary or owner
  follow-up and forbids direct raw facts, SQLite, logs, snippets, SQL, config,
  local paths, remotes, scan directories, command output, hidden validation
  details, or credential-like values.
- The `dev-only feature` row forces concept or hidden treatment and forbids
  shipped, demo, roadmap-timing, or public availability wording.
- The `reduced coverage` row keeps the reduced or partial coverage visible and
  forces downgrade or owner follow-up unless the claim is narrowed.
- The `runtime/release wording` row forces removal or rewrite and forbids
  runtime behavior, production traffic, endpoint performance, release
  approval, release safety, operational safety, and outage-cause claims.
- The `AI/LLM wording` row forces removal or rewrite and forbids claims that
  core TraceMap scanner or reducer behavior performs AI/LLM impact analysis,
  embeddings, vector search, or prompt-based classification.
- The `private-only support` row forces `internal only`, hidden, or owner
  follow-up until a public-safe summary exists.
- If a row lacks proof path, rule ID or rule family, evidence tier where
  applicable, coverage label, limitation, or source context, the row cannot be
  strengthened.

### Requirement 10: Define validation expectations

The future implementation shall include focused validation for the guardrails
surface.

Acceptance criteria:

- Validation checks the chosen route or section output exists.
- Validation checks visible `Public claim level: concept` for public-facing
  output, or a recorded contributor-only hidden rationale when the output is
  not public-facing.
- Validation checks visible `No public conclusion without evidence`.
- Validation checks every required section and stable anchor.
- Validation checks every required guardrail row: shipped, demo, concept,
  hidden, raw artifact reference, dev-only feature, reduced coverage,
  runtime/release wording, AI/LLM wording, and private-only support.
- Validation checks every guardrail row includes all six row fields:
  condition, allowed public wording or action, required proof path, downgrade
  or hidden trigger, forbidden implication, and review handoff.
- Validation checks required links to adjacent routes when present and checks
  that absent, renamed, or deferred adjacent routes are recorded in
  `implementation-state.md`.
- Validation checks standalone public metadata, discovery metadata, sitemap
  metadata, canonical URL, title, description, and Open Graph fields remain
  concept-level when a public standalone route is chosen.
- Validation checks contributor-only output is not present in public sitemap
  or discovery metadata when hidden contributor-only placement is chosen.
- Validation checks the page or section is absent from primary navigation, or
  that an information-architecture note justifying inclusion is recorded in
  `implementation-state.md`.
- Validation checks forbidden product capability, runtime proof, release
  approval, release safety, operational safety, complete coverage, AI/LLM
  analysis, and replacement-of-human-review claims are absent outside explicit
  rejected-example, limitation, boundary, or non-claim contexts.
- Validation checks forbidden private/raw material is absent across rendered
  text, decoded attributes, raw HTML, metadata, sitemap or discovery output,
  examples, fixtures, tests, validation messages, and review-packet references.
- Validation checks no blame language appears in rendered copy, metadata,
  examples, validation messages, or review-packet references.
- Validation checks rendered body word count stays between 700 and 2200 words
  without removing mandatory sections or rows.
- For folded placements, the rendered body word count applies to the
  guardrails section subtree rather than the whole host page.
- Validation checks desktop and mobile browser sanity after layout or
  interaction changes.
- Validation is wired into the aggregate site validation workflow where the
  existing site pattern supports it.

### Requirement 11: Define review handoff

The future surface shall explain how to hand off a claim that cannot be
published or strengthened.

Acceptance criteria:

- The review handoff section gives bounded next states:
  `repeat with proof`, `downgrade before repeating`,
  `owner follow-up needed`, `do not repeat`, `internal only`, and `hidden`.
- The handoff asks for the missing proof path, public-safe summary, rule ID or
  rule family, evidence tier where applicable, coverage label, limitation,
  source context, or owner decision without declaring impact, safety, approval,
  absence of impact, or runtime behavior.
- The handoff states that private-only or raw-only evidence remains internal
  until a public-safe summary exists.
- The handoff states that unresolved claim boundaries are not defects in a
  person or team; they are evidence gaps, limitations, reduced coverage,
  private-only states, or hidden states.
- The handoff records route choice, link substitutions, validation results,
  oddities, and follow-up items in this spec's `implementation-state.md`.
