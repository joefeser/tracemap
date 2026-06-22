# Site TraceMap Tools Reduced Coverage Playbook Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe TraceMap site page or section that teaches readers
what to do when scanner or reducer evidence is partial, coverage is reduced,
or analysis gaps are present.

This is a spec-only packet. It does not implement site code, generated output,
scanner behavior, reducer behavior, validation scripts, navigation, sitemap
metadata, or public copy changes.

The future surface should help engineers, reviewers, managers, and agents
label reduced coverage without overstating what TraceMap can conclude. It
should show what evidence remains useful, what cannot be concluded, who should
collect the next evidence, and where to stop.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future page or section shall visibly say `Public claim level: concept`
because it is a guidance and playbook surface. It explains how to label and
handoff partial static evidence; it does not add a shipped scanner capability,
produce a checked-in public demo result, prove runtime behavior, approve a
release, or close any analysis gap.

Do not upgrade this surface to `demo` unless a future spec amendment ties the
exact examples, rows, and proof links to checked-in public-safe demo material
without publishing raw facts, raw SQLite, analyzer logs, snippets, private
sample names, hidden validation detail, or local-only artifacts.

## Audience

- Engineers reading a TraceMap scan where semantic analysis, build/load, or
  artifact generation did not provide complete evidence.
- Reviewers checking that public or internal wording keeps reduced coverage
  labels attached.
- Managers deciding which owner needs to collect the next evidence before a
  claim can be repeated.
- Agents preparing site copy or review packets that must preserve limitations,
  evidence tiers, coverage labels, and non-claims.

## Core Message

Reduced coverage is an evidence state, not a reason to invent a conclusion.
TraceMap evidence can remain useful when it is partial, syntax-only,
framework-limited, stale, private-only, missing an artifact, or tagged with an
unknown evidence tier, but the public statement must keep that label visible.

The future page shall teach readers to say what is known, what is unknown, who
owns the next evidence, and where review must stop. It shall not turn missing
or reduced evidence into an absence-of-impact proof, a clean-repo claim, a
runtime proof, a release approval, an operational safety claim, a complete
coverage claim, AI/LLM analysis, prompt-based classification, embedding
search, vector database analysis, autonomous approval, or a replacement for
human review.

## Relationship To Adjacent Site Surfaces

The reduced coverage playbook is a gap-handling and owner-handoff surface. It
must distinguish itself from these adjacent surfaces:

- `/limitations/`: canonical boundary and non-claim definitions. The
  playbook should link to limitations but focus on actions when evidence is
  partial or reduced.
- `/validation/`: validation method and check-result orientation. The playbook
  should link to validation but focus on reader decisions after reduced
  coverage appears.
- `/static-vs-runtime/`: static evidence versus runtime telemetry boundary.
  The playbook should link to this route for runtime questions but not become
  a runtime explainer.
- `/questions/objections/`: stakeholder objection answers. The playbook
  should give operational wording and owner handoff steps, not broad objection
  handling.
- `/proof-paths/faq/`: explanation of proof paths and missing evidence. The
  playbook should provide the reduced-coverage row matrix and stop conditions.
- `/review-claim-checklist/`: claim repetition ritual. The playbook should
  feed checklist decisions when reduced coverage is present, not replace the
  checklist.

## Requirements

### Requirement 1: Choose a bounded placement

The future implementation shall add the reduced coverage playbook as a
public-safe concept-level page or section using an explicit route and placement
decision.

Acceptance criteria:

- Candidate placements are `/coverage/reduced/`,
  `/limitations/reduced-coverage/`, a section on `/limitations/`, or a
  section on `/validation/`.
- The implementation records the selected placement and rejected alternatives
  in this spec's `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section says `No public conclusion without evidence`.
- The page or section does not enter primary navigation unless a future
  information-architecture review records why the existing site pattern
  supports it.
- If implemented as a standalone route, the implementation adds title,
  description, canonical URL, Open Graph metadata, sitemap metadata, and
  discovery metadata using existing site patterns.
- If implemented as a standalone route, sitemap and discovery metadata keep
  `publicClaimLevel` or equivalent site metadata at `concept`.
- If implemented as a section, the host page's title, description, social
  metadata, sitemap entry, discovery entry, and claim-level wording must not
  imply stronger coverage than the host page and section can support.
- If implemented as a section, the implementation adds stable anchor IDs for
  each playbook subsection, and generated output validation verifies anchor
  uniqueness within the host page.
- If the chosen host page is already compact, roughly under 900 visible body
  words at implementation time, default to a standalone route or bias the
  section to the lower end of the word-count bound so the playbook does not
  overwhelm canonical host content. Record the host-page size consideration in
  `implementation-state.md`.
- All cross-links use bounded anchor text that does not imply runtime proof,
  release safety, operational safety, absence of impact, clean-repo status, or
  complete coverage.
- `Bounded anchor text` means link text names the destination surface or
  boundary, such as `limitations and non-claims`, `validation checks`, or
  `static versus runtime boundaries`, rather than generic phrases such as
  `here`, `more`, or `this page`, or phrases that assert an unsupported
  capability.
- Before linking to candidate or adjacent routes, generated output validation
  verifies each link resolves or records the route as deferred, substituted,
  or omitted in `implementation-state.md`.

### Requirement 2: Explain reduced coverage and labels

The future page shall explain what reduced coverage means and how to label it
without turning a gap into a conclusion.

Acceptance criteria:

- Include required sections for what reduced coverage means, how to label it,
  safe conclusions, unsafe conclusions, next evidence to collect, owner
  handoff, stop conditions, and non-claims.
- Explain that reduced coverage includes partial scans, syntax fallback,
  missing semantic evidence, unsupported framework surfaces, missing generated
  artifacts, private-only support, stale commit context, unknown evidence
  tiers, unavailable proof links, or explicitly labeled analysis gaps.
- Explain that reduced coverage can still provide useful review input when
  rule IDs, evidence tiers, coverage labels, limitations, commit context,
  extractor versions, and public-safe proof links remain attached.
- Require coverage labels to stay visible in page examples, tables, metadata
  summaries, review packets, and claim-checklist references.
- Require evidence tier vocabulary to use only `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`. When source
  evidence is unavailable or private-only, public copy uses `Tier4Unknown`
  with a visible unavailable or private-only label rather than creating a
  custom tier.
- Supplementary state markers use a closed vocabulary: `unavailable`,
  `private-only`, and `stale`. When a row uses `Tier4Unknown` for unavailable
  or private-only source evidence, the visible marker must be exactly
  `unavailable` or `private-only`. A stale-context row must carry the exact
  visible marker `stale`.
- Explain that a lower or unknown evidence tier is not a defect, accusation,
  or blame signal; it is a boundary on what the evidence can support.
- Explain that `reduced`, `partial`, `syntax fallback`, `unknown`,
  `unavailable`, `private-only`, `future-only`, and analysis-gap labels must
  not be silently normalized into stronger wording.
- Avoid blame language. Prefer neutral phrasing such as `coverage is reduced`,
  `the proof path is incomplete`, `semantic evidence is missing`,
  `the artifact is unavailable`, or `owner follow-up is needed`.

### Requirement 3: Provide the required reduced coverage matrix

The future page shall include a scannable matrix or equivalent repeated-row
structure for required reduced-coverage scenarios.

Acceptance criteria:

- Include rows for build/load failure, syntax fallback, missing semantic
  evidence, unsupported framework surface, missing generated artifact,
  private-only support, stale commit context, and unknown evidence tier.
- Each required row includes coverage label, evidence tier, evidence
  available, what cannot be concluded, next owner, safe wording, stop
  condition, and proof/validation link.
- The matrix uses accessible table semantics or an equivalent card/list
  pattern with programmatically associated row labels and fields.
- On narrow/mobile viewports, the matrix remains readable without hiding the
  row label, coverage label, what cannot be concluded, owner, or stop
  condition.
- Row labels are neutral and do not attribute missing, reduced, stale, or
  conflicting evidence to a person, team, service, customer, or reviewer.
- `proof/validation link` values resolve only to public-safe routes,
  documentation, validation pages, limitations pages, proof-path pages,
  review-checklist pages, or public-safe demo summaries.
- `proof/validation link` values are non-empty and non-placeholder in the
  implemented page, unless an unavailable target is explicitly recorded as
  deferred, substituted, or omitted in `implementation-state.md`.
- The row for build/load failure states that a failed or reduced build/load
  state cannot support a clean-repo claim.
- The row for syntax fallback states that syntax-only evidence cannot support
  compiler-resolved symbol conclusions.
- The row for missing semantic evidence states that unresolved or unavailable
  semantic evidence cannot support Tier1 conclusions.
- The row for unsupported framework surface states that unsupported framework
  detection cannot support complete framework coverage.
- The row for missing generated artifact states that missing public-safe
  artifact output cannot support public proof-link claims.
- The row for private-only support states that private-only evidence cannot be
  cited as public proof until summarized through a public-safe route.
- The row for stale commit context states that stale commit context cannot
  support current-head or current-release wording.
- The row for unknown evidence tier states that unknown tier state cannot
  support a stronger tier or conclusion by confidence, repetition, or
  reviewer seniority.

### Requirement 4: Define safe and unsafe conclusions

The future page shall separate safe reduced-coverage wording from unsupported
conclusions.

Acceptance criteria:

- Safe wording may say a scan has reduced coverage, a proof path is partial,
  syntax fallback found a static reference, semantic evidence is missing, an
  owner should collect another artifact, a claim should be downgraded, or a
  review should stop until a public-safe proof link exists.
- Safe wording may use terms such as `static reference`, `surface`,
  `evidence available`, `coverage label`, `analysis gap`, `needs review`,
  `owner follow-up`, `public-safe summary`, `proof path`, and `stop
  condition`.
- Unsafe wording must be rejected or shown only inside explicitly labeled
  rejected-pattern regions.
- Unsafe conclusions include absence-of-impact proof, clean-repo claim under
  failed or reduced analysis, runtime proof, release approval or safety,
  operational safety, complete coverage, AI/LLM analysis, prompt-based
  classification, embedding search, vector database analysis, autonomous
  approval, and replacement of human review.
- The page must not say TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, AI impact analysis, LLM analysis, prompt-based
  classification, embedding search, vector database analysis, or that no
  owner review is needed.
- The page must not use unqualified claim phrases such as `impacted`, `safe
  to release`, `approved`, `certified`, `guaranteed`, `resolved`, or `clean
  repo` unless they are inside a clearly rejected example or a future
  public-safe proof path supports the exact phrase with limitations. This does
  not ban approved vocabulary such as `safe wording` or `public-safe`.
- If examples include unsafe wording, each example is visually and
  semantically labeled as a rejected pattern and does not appear in metadata,
  summaries, alt text, captions, discovery output, or link text as a live
  claim.
- An explicitly bounded rejected-pattern region uses a programmatically
  identifiable marker, such as a dedicated component, wrapper element, or data
  attribute, that validation can detect structurally in rendered HTML.
  Visual-only styling or prose-only labels do not satisfy the bounding
  requirement.

### Requirement 5: Define next evidence and owner handoff

The future page shall teach readers who to involve next and what evidence to
collect when coverage is reduced.

Acceptance criteria:

- Include a next-evidence section that maps each required row to a public-safe
  evidence collection target, such as build/load diagnostics summary,
  semantic-load result, syntax fallback report, rule catalog limitation,
  framework support note, generated public-safe report, current commit
  context, or evidence-tier reconciliation.
- Include an owner-handoff section that names owner roles rather than real
  internal people, private team names, customers, service names, or repository
  identities.
- Example owner roles may include scanner owner, site owner, validation owner,
  service owner, reviewer, build/tooling owner, framework owner, artifact
  publisher, or release owner when framed as follow-up rather than approval.
- Handoff guidance must include the current label, evidence available, missing
  evidence, requested next action, public-safe proof target, stop condition,
  and non-claim.
- The page explains that a handoff transfers the evidence question; it does
  not approve a release, prove runtime behavior, assign blame, or replace
  human review.
- Owner handoff examples use synthetic or role-only values and do not include
  real owner names, real dates, private sample names, local paths, raw remotes,
  command output, hidden validation detail, or credential-like values.

### Requirement 6: Preserve private/raw material boundaries

The future page shall not publish raw, private, local, generated, hidden, or
credential-like material.

Acceptance criteria:

- Do not publish raw facts, raw SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation
  details, or credential-like values.
- Do not publish raw `facts.ndjson`, raw `index.sqlite`, raw
  `logs/analyzer.log`, source snippets, SQL fragments, configuration values,
  credentials, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, terminal output, hidden validation
  details, or token-like values.
- The page may name artifact families such as fact streams, SQLite indexes,
  analyzer logs, scan manifests, reports, rule catalog entries, commit
  metadata, coverage labels, and limitations only as material that needs a
  public-safe summary before public linking.
- Public links point only to checked-in public pages, public-safe generated
  summaries, documentation, rule catalog pages, validation pages, limitation
  pages, reports, sanctioned demo artifacts, proof-path entries, or
  review-checklist surfaces.
- Any illustrative example uses synthetic labels and is visibly marked as
  illustrative.
- The implementation must validate rendered text, decoded HTML, raw HTML
  attributes, metadata, discovery output, sitemap output, tests, and fixtures
  for forbidden private/raw material.

### Requirement 7: Add focused validation expectations

The future implementation shall include focused validation so the playbook
does not drift into unsupported public claims.

Acceptance criteria:

- Validate required visible text: `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validate presence of required sections: what reduced coverage means, how to
  label it, safe conclusions, unsafe conclusions, next evidence to collect,
  owner handoff, stop conditions, and non-claims.
- Validate the required rows: build/load failure, syntax fallback, missing
  semantic evidence, unsupported framework surface, missing generated
  artifact, private-only support, stale commit context, and unknown evidence
  tier.
- Validate every required row has coverage label, evidence available, what
  cannot be concluded, next owner, safe wording, stop condition, and
  proof/validation link.
- Validate every required row has evidence tier using only `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`. Public rows
  with unavailable or private-only source evidence use `Tier4Unknown` with a
  visible unavailable or private-only label, not a custom tier value.
- Validate supplementary state markers with the closed vocabulary
  `unavailable`, `private-only`, and `stale`. A row may list more than one of
  the four allowed tier tokens, but validation rejects tier strings outside
  the four-token set and rejects free-form supplementary markers.
- Validate that every required row's `proof/validation link` field is
  non-empty and non-placeholder, resolves to an allowed public-safe target
  type, or is explicitly recorded as deferred, substituted, or omitted in
  `implementation-state.md`.
- No more than two required rows may have deferred, substituted, or omitted
  proof/validation links at any one time. Each deferred row must record a
  specific public-safe target type and follow-up task referencing this spec.
  A third or subsequent deferral requires an explicit spec amendment that
  records the rationale and expected resolution path.
- Validate required links resolve in generated output or are recorded as
  deferred, substituted, or omitted in `implementation-state.md`.
- Validate that the adjacent-surface distinctions section is present and
  includes a distinguishing statement for each of `/limitations/`,
  `/validation/`, `/static-vs-runtime/`, `/questions/objections/`,
  `/proof-paths/faq/`, and `/review-claim-checklist/`, or records each absent
  or moved route as deferred, substituted, or omitted in
  `implementation-state.md`. When validating or checking relative paths for
  these route segments, split paths into segments and check the individual
  segment sequence rather than using string containment with slash-wrapped
  substrings, because relative paths may not have a leading slash.
- When an adjacent route is recorded as absent or deferred in
  `implementation-state.md`, validation verifies that the page omits the
  hyperlink and uses a visible `(planned)` qualifier, or names the surface in
  non-linked prose without implying the route is live. Validation must not
  accept a live hyperlink to a known-absent route.
- Validate that cross-links to adjacent surfaces use bounded anchor text that
  names the destination boundary or topic rather than generic phrases or
  claim-asserting phrases.
- Validate standalone route metadata, sitemap metadata, and discovery metadata
  when standalone placement is chosen.
- Validate section host metadata and anchor uniqueness when section placement
  is chosen.
- Validate forbidden claims for absence of impact, clean repo under failed or
  reduced analysis, runtime proof, production traffic proof, endpoint
  performance proof, outage-cause proof, release approval or safety,
  autonomous approval, operational safety, complete coverage, AI/LLM analysis,
  prompt-based classification, embedding search, vector database analysis, and
  replacement of human review.
- Validate that page copy, row labels, owner-handoff examples, metadata, and
  validation messages avoid blame attribution and use neutral state or
  next-action phrasing rather than attributing missing, reduced, stale, or
  conflicting evidence to a person, team, service, customer, or reviewer.
- The blame-free validation must not flag the required row label
  `build/load failure` as blame language. The label describes a scanner state,
  not a person, team, service, customer, or reviewer. Validation checks that
  surrounding copy, row body text, owner fields, stop conditions, and
  safe-wording fields do not attribute the state while preserving the row
  label verbatim.
- Validate that forbidden-claim and unqualified-term detection excludes text
  inside explicitly bounded rejected-pattern, non-claim, limitation, and
  validation-warning regions, and matches claim phrases such as `safe to
  release`, `clean repo`, or `proves there is no impact` rather than bare
  substrings such as `safe`, `clean`, or `resolved`.
- Validate in static HTML, not via hover-only or JavaScript-only disclosure,
  that each required row's coverage label, what-cannot-be-concluded text, next
  owner, and stop condition text are present, and that the matrix uses table
  header association or an equivalent programmatic field-label association
  marker. Manual desktop/mobile sanity checks are additive, not a substitute
  for this automated check.
- Validate forbidden private/raw material across rendered copy, decoded HTML,
  raw HTML attributes, metadata, sitemap/discovery output, fixtures, tests,
  and bot-oriented discovery surfaces.
- Validate that unsafe wording appears only inside explicitly bounded
  rejected-pattern, non-claim, limitation, or validation-warning regions.
- Validate that illustrative examples are synthetic or already public-safe and
  do not include real internal identities, private sample names, local paths,
  command output, hidden validation details, credentials, raw facts, raw
  SQLite, analyzer logs, snippets, SQL, config values, generated scan dirs, or
  raw remotes.
- Validate word count bounds so the future page is complete but not sprawling.
  Bounds exclude navigation, metadata, code blocks, and all text inside cells
  of the required reduced-coverage matrix, including row labels, coverage
  labels, evidence tiers, evidence-available text, cannot-conclude text, owner
  text, safe-wording text, stop-condition text, and proof/validation link
  text. The exclusion applies to all text structurally inside the matrix
  element, including column headers, captions, footnotes, and in-cell links,
  but not prose sections that precede, follow, or describe the matrix. Body
  prose outside the matrix counts toward the bound. Recommended visible body
  copy is 1,000 to 1,900 words for a standalone route or 500 to 1,100 words
  for a section placement. The section bound applies to prose within the
  playbook section only, not to pre-existing host page prose outside the
  section boundaries. If host prose would need to count, choose a standalone
  route instead and record the rationale in `implementation-state.md`. If the
  full matrix cannot fit the section bound without dropping required rows or
  fields, choose a standalone placement instead.
- If interaction, layout, or standalone page changes are made, run desktop
  and mobile browser sanity checks and record results in
  `implementation-state.md`.
