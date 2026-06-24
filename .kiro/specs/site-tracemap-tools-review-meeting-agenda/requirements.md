# Site TraceMap Tools Review Meeting Agenda Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-site concept page or section for a TraceMap review
meeting agenda. The surface should help teams run a focused evidence review
meeting by starting with the question, inspecting proof paths, capturing gaps,
assigning next owners, handing off a decision record, and stopping before
runtime, release, or safety claims.

This is a site-spec-only packet. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, or public
copy changes.

The future surface is not meeting automation, release approval, runtime proof,
production traffic proof, endpoint performance analysis, absence-of-impact
proof, complete coverage, AI or LLM analysis, or a replacement for human
judgment or governance.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or section shall display `Public claim level: concept`.
- The future page or section shall display
  `No public conclusion without evidence`.
- The page may describe a human meeting agenda for inspecting deterministic
  TraceMap static evidence, rule IDs or rule families, evidence tiers, proof
  paths, coverage labels, limitations, public-safe provenance, gaps, next
  owners, and decision-record handoff.
- The page must not claim meeting automation, release approval or release
  safety, operational safety, runtime proof, production traffic, endpoint
  performance, absence-of-impact proof, complete coverage, AI impact analysis,
  LLM analysis, prompt-based classification, or replacement of human judgment
  or governance.
- The page must not publish raw facts, SQLite content, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, raw remotes, generated
  scan directories, private sample names, command output, hidden validation
  details, credential-like values, connection strings, tokens, or keys.
- Copy must avoid blame language. It should frame gaps as evidence states and
  follow-up ownership, not as fault.

## Requirements

### Requirement 1: Choose a bounded placement

The future implementation shall publish a concept-level public page or section
for the review meeting agenda.

Acceptance criteria:

- The implementation chooses one of these candidate placements:
  `/review-room/agenda/`, `/meetings/evidence-review/`, a section on
  `/review-room/`, or a section on `/reviewer-quickstart/`.
- Before changing site source, implementation records the selected placement,
  rejected alternatives, routing consequences, and validation consequences in
  `implementation-state.md`.
- Standalone placement is preferred only when the agenda needs route-level
  metadata, sitemap inclusion, discovery metadata, and direct linking.
- Section placement is preferred only when the live parent page can support the
  agenda without weakening that page's focus, word-count bounds, validation, or
  neighboring-page distinction.
- The page or section says `Public claim level: concept`.
- The page or section says `No public conclusion without evidence`.
- The page or section addresses engineers, reviewers, architects, managers,
  release reviewers, and owners who are meeting to inspect static evidence and
  decide next review work.
- The page or section is not added to primary navigation unless a future
  information-architecture review records why that placement is warranted.

### Requirement 2: Define the agenda purpose

The future page shall explain the agenda as a human meeting aid for evidence
review, not an automated decision system.

Acceptance criteria:

- The page states that the agenda starts with the review question before any
  claim is repeated.
- The page states that participants inspect proof paths and evidence metadata
  before deciding what is known, partial, missing, or out of scope.
- The page states that gaps are captured as explicit follow-up items with
  owners rather than silently upgraded into conclusions.
- The page states that decision-record handoff preserves the bounded question,
  evidence state, validation evidence category, gaps, owners, limitations, and
  non-claims.
- The page states that the meeting must stop or downgrade wording before
  runtime, release, safety, production, performance, absence-of-impact,
  complete-coverage, AI/LLM, or governance-replacement claims.
- The page avoids saying a system, endpoint, dependency, feature, release, or
  team is `impacted`, `safe`, `unsafe`, `approved`, `blocked`,
  `production proven`, `performance proven`, `complete`, or `root cause`
  unless the phrase appears inside an explicit non-claim or stop-condition
  boundary.

### Requirement 3: Publish required sections

The future page shall include all required agenda sections with compact,
meeting-friendly copy.

Acceptance criteria:

- The page includes `Before the meeting`.
- The page includes `Agenda`.
- The page includes `Evidence checks`.
- The page includes `Gap capture`.
- The page includes `Owner assignment`.
- The page includes `Decision record handoff`.
- The page includes `Stop conditions`.
- The page includes `Non-claims`.
- The sections remain concise enough for use during a meeting and avoid
  duplicating neighboring pages' full explanations.
- The `Evidence checks` section requires a rule ID or rule family alongside
  evidence tier, coverage label, proof path, and limitation.
- The `Stop conditions` section enumerates meeting stop or downgrade triggers
  for missing proof paths, private-only evidence, raw or private material,
  unknown or reduced coverage without label, unsupported runtime or release
  wording, unsupported safety or production wording, endpoint performance,
  absence-of-impact, complete coverage, AI/LLM analysis, governance
  replacement, no next owner, and blame language.

### Requirement 4: Include required agenda rows

The future page shall include a validator-checkable agenda table or list with
the required agenda rows.

Acceptance criteria:

- The agenda includes `question framing`.
- The agenda includes `proof path check`.
- The agenda includes `evidence tier/coverage check`.
- The agenda includes `limitation check`.
- The agenda includes `gap register check`.
- The agenda includes `owner follow-up`.
- The agenda includes `decision record`.
- The agenda includes `closeout`.
- Each agenda row includes a meeting purpose, evidence input, and stop or
  handoff output.
- `evidence tier/coverage check` uses only the TraceMap tier vocabulary:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown`.
- The agenda or adjacent evidence-check copy requires a rule ID or rule family
  before evidence is repeated as meeting support.
- `closeout` requires remaining unsupported claims to be removed, downgraded,
  or assigned to a non-static owner before the meeting notes are shared.

### Requirement 5: Distinguish neighboring pages

The future page shall clearly describe how the agenda differs from adjacent
TraceMap public-site surfaces.

Acceptance criteria:

- The page distinguishes itself from `/review-room/`: the agenda is the
  step-by-step meeting script or section, while `/review-room/` is the broader
  evidence-review room orientation.
- The page distinguishes itself from `/reviewer-quickstart/`: the agenda is a
  meeting runbook, while reviewer quickstart is onboarding and first-review
  orientation.
- The page distinguishes itself from `/packets/assembly/`: the agenda runs the
  meeting, while packet assembly gathers public-safe review ingredients.
- The page distinguishes itself from `/handoff/template/`: the agenda creates
  meeting outputs, while the handoff template is a portable field structure.
- The page distinguishes itself from `/owners/follow-up/`: the agenda assigns
  follow-up owners, while owner follow-up tracks post-meeting responsibility.
- The page distinguishes itself from `/decisions/evidence-record/`: the agenda
  prepares the decision record handoff, while the decision-record page
  preserves the bounded result.
- If a manager demo or presentation route exists, for example
  `/manager-demo-script/` or the live equivalent such as
  `/demo/manager-script/`, the page distinguishes itself from that route: the
  agenda is a real evidence-review meeting concept, while the demo script is a
  bounded presentation aid. Implementation verifies the actual route name in
  generated output before referencing it, or records it as a documented gap in
  `implementation-state.md`.
- Distinctions must not imply any neighboring page proves runtime behavior,
  release safety, operational safety, production traffic, endpoint
  performance, absence of impact, complete coverage, AI/LLM analysis, or human
  governance replacement.

### Requirement 6: Preserve public-safe artifact handling

The future page shall publish only public-safe explanatory copy, sanitized
examples, and public-safe links.

Acceptance criteria:

- The page and metadata do not publish raw facts, SQLite content, analyzer
  logs, source snippets, SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, credential-like values, connection strings, tokens, or
  keys.
- Public examples are authored concept examples or existing public-safe demo
  summaries.
- Synthetic examples use role labels and fictional review context, not private
  repository names, customer names, service names, owner names, real internal
  dates, private sample names, local paths, raw remotes, or hidden roadmap
  details.
- Private evidence is described as requiring private review before any
  public-safe meeting note or summary becomes public copy.
- Snippet-like text is avoided unless it is synthetic meeting-note language
  containing no code, SQL, configuration values, secrets, local paths, private
  identifiers, command output, or raw artifact content.
- `implementation-state.md` must not record absolute local paths, raw remote
  URLs, private sample names, or credential-like values. Branch names, target
  bases, and commit SHAs are acceptable.
- A bounded owner/repo PR-loop slug required by the operator's exact final
  command is acceptable in `implementation-state.md` when
  `./scripts/check-private-paths.sh` confirms it is not flagged.

### Requirement 7: Link to adjacent public-safe surfaces

The future page shall guide readers to the right neighboring proof, review,
handoff, owner, and decision-record surfaces without overstating what those
surfaces prove.

Acceptance criteria:

- Before linking to a route, implementation verifies that the route resolves in
  generated site output or records the route as a documented gap in
  `implementation-state.md`.
- Required core links are `/proof-paths/`, `/validation/`, and
  `/limitations/`; each must resolve in generated output or block standalone
  route readiness until implementation records why a section placement or live
  route gap requires a substitute.
- Candidate support links include `/proof-paths/`, `/evidence/`,
  `/validation/`, `/limitations/`, `/review-room/`,
  `/reviewer-quickstart/`, `/packets/assembly/`, `/handoff/template/`,
  `/owners/follow-up/`, and `/decisions/evidence-record/`.
- If `/manager-demo-script/` exists, the page may link to it only as a
  neighboring distinction, not as meeting evidence. If the live equivalent is
  a different route, implementation verifies that route before linking or
  records the mismatch as a documented gap.
- Cross-link anchor text stays bounded, such as `proof paths`, `evidence
  vocabulary`, `validation limits`, `limitations`, `review room`, `reviewer
  quickstart`, `packet assembly`, `handoff template`, `owner follow-up`, and
  `decision record`.
- Required-link validation confirms selected links resolve or that unresolved
  links are recorded as implementation-state gaps.

### Requirement 8: Add focused validation in the implementation phase

The future implementation shall validate route content, metadata, links, and
claim boundaries.

Acceptance criteria:

- Validation confirms the rendered page or section contains
  `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validation confirms the required section labels render:
  `Before the meeting`, `Agenda`, `Evidence checks`, `Gap capture`,
  `Owner assignment`, `Decision record handoff`, `Stop conditions`, and
  `Non-claims`.
- Validation confirms `Evidence checks` requires rule ID or rule family,
  evidence tier, coverage label, proof path, and limitation.
- Validation confirms evidence tiers use only `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`, and forbids
  non-canonical tier or confidence labels such as `high confidence`,
  `medium confidence`, `low confidence`, `verified`, or `guaranteed` outside
  explicit non-claim or stop-condition boundaries.
- Validation confirms `Stop conditions` enumerates the required stop or
  downgrade trigger categories rather than rendering only the heading.
- Validation confirms the required agenda row labels render:
  `question framing`, `proof path check`,
  `evidence tier/coverage check`, `limitation check`,
  `gap register check`, `owner follow-up`, `decision record`, and
  `closeout`.
- Validation confirms each rendered agenda row exposes a purpose, an evidence
  input, and a stop-or-handoff output column or field, not just the row label.
- Validation confirms the agenda renders with accessible table or list
  semantics, including associated header cells or equivalent list structure.
- Validation confirms cross-link anchor text is descriptive rather than bare
  URLs or vague labels such as `click here`.
- Validation confirms heading hierarchy for required sections is well formed
  and the section or anchor structure is reachable by assistive technology.
- Validation confirms required selected links resolve in generated output or
  are recorded as documented route gaps.
- If standalone, validation confirms title, description, canonical URL, Open
  Graph fields, route metadata, discovery metadata, sitemap metadata, and
  `publicClaimLevel: concept`.
- If standalone, discovery metadata labels the route as concept-level static
  evidence meeting guidance with limitations and non-claims.
- If embedded, validation confirms the parent page exposes a stable section
  anchor and any section-level discovery or route-index pattern used by the
  live site.
- Validation enforces a rendered main-content word-count range selected from
  neighboring concept-page patterns and records the selected bounds in
  `implementation-state.md`.
- Validation checks forbidden claims for meeting automation, release approval
  or safety, operational safety, runtime proof, production traffic, endpoint
  performance, absence-of-impact proof, complete coverage, AI or LLM analysis,
  prompt-based classification, and replacement of human judgment or
  governance.
- Validation checks forbidden private or raw material in rendered text,
  decoded HTML, raw HTML attributes, metadata, and any example copy.
- Validation checks for blame language and unsupported certainty language.
- If automated blame-language detection is not implemented, a role-based
  public-safety reviewer must sign off in `implementation-state.md` before the
  route or section is published.
- Implementation validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, `npm test` from `site/`,
  `npm run validate` from `site/`, `npm run build` from `site/`, and desktop
  and mobile browser sanity checks when layout or interaction changes are made.
