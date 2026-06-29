# Site TraceMap Tools Review Meeting Agenda Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Purpose

This design describes a future public-site concept surface for a TraceMap
review meeting agenda. The agenda helps a human team keep a review meeting
attached to deterministic static evidence: start with the question, inspect
proof paths, check evidence tier and coverage, capture gaps, assign owners,
prepare decision-record handoff, and stop before unsupported claims.

No site code is implemented in this spec-only phase.

## Placement Options

Candidate placements:

- `/review-room/agenda/`
- `/meetings/evidence-review/`
- section on `/review-room/`
- section on `/reviewer-quickstart/`

Recommended starting point: `/review-room/agenda/`.

Rationale: the agenda is closely related to the review-room concept, but it is
more procedural than the broader `/review-room/` orientation. A nested route
allows direct linking, standalone metadata, sitemap metadata, and focused
validation without inflating the parent review-room page.

Conditional alternatives:

- `/meetings/evidence-review/`: choose this if implementation finds the live
  site has or is adding a meeting-oriented hierarchy.
- Section on `/review-room/`: choose this if the agenda is compact enough to
  strengthen the parent page without duplicating the parent page's existing
  meeting framing.
- Section on `/reviewer-quickstart/`: choose this only if implementation finds
  the agenda is primarily first-review onboarding rather than a reusable
  meeting script.

Before changing `site/src`, implementation must record the selected placement,
rejected alternatives, route gaps, metadata consequences, and validation
consequences in `implementation-state.md`.

## Page Structure

The future page or section should use this structure:

1. Opening frame
   - Visible label: `Public claim level: concept`
   - Shared principle: `No public conclusion without evidence`
   - One paragraph stating that the agenda is a human meeting guide for static
     evidence review.
2. Before the meeting
   - Name the review question.
   - Identify public-safe proof paths or state that proof paths are missing.
   - Bring known evidence metadata, limitations, and coverage labels.
   - Pre-mark private-only or unavailable material as gaps.
3. Agenda
   - A compact table with the required agenda rows.
   - Each row includes purpose, evidence input, and output.
4. Evidence checks
   - Proof path present or missing.
   - Rule ID or rule family.
   - Evidence tier.
   - Coverage label.
   - Limitation.
   - Public-safe provenance when available.
5. Gap capture
   - Missing proof path.
   - Reduced, unknown, unavailable, syntax-only, or private-only coverage.
   - Unsupported runtime, release, safety, production, performance,
     absence-of-impact, complete-coverage, AI/LLM, or governance-replacement
     wording.
6. Owner assignment
   - Assign role-based next owners for code, review, tests, runtime,
     release, architecture, documentation, or evidence preparation.
   - Avoid personal names in public examples.
7. Decision record handoff
   - Preserve the review question, bounded static-evidence state, limitations,
     gaps, next owners, validation evidence category, and non-claims.
8. Stop conditions
   - State when the meeting must pause, downgrade language, move private, or
     assign another owner.
   - Use the enumerated stop or downgrade trigger categories from the
     validation design so the implementation does not render only a heading.
9. Non-claims
   - State the boundaries for automation, runtime, production, release,
     safety, endpoint performance, absence of impact, complete coverage,
     AI/LLM analysis, and human judgment or governance replacement.

## Required Agenda Rows

The agenda table or list must include these rows exactly:

| Row | Purpose | Evidence input | Expected output |
| --- | --- | --- | --- |
| `question framing` | State the review question before claims are repeated. | Bounded review question and any repeated claim wording. | One bounded question and any wording to remove or downgrade. |
| `proof path check` | Confirm where public-safe evidence can be inspected. | Candidate proof-path links or private-review status. | Proof path link, private-review note, or missing-proof gap. |
| `evidence tier/coverage check` | Pair the cited evidence tier with the coverage label. | Cited tier and coverage label. | Tier and coverage label retained without upgrade. |
| `limitation check` | Attach what the static evidence cannot prove. | Rule or limitation notes for the cited evidence. | Limitation kept with the claim or agenda note. |
| `gap register check` | Capture unknown, reduced, unavailable, or private-only evidence. | Unknown, reduced, unavailable, or private-only coverage states. | Gap entry with review question and owner type. |
| `owner follow-up` | Assign the next owner for unresolved work. | Open gaps and unresolved questions. | Role-based owner and requested follow-up. |
| `decision record` | Prepare bounded handoff notes. | Bounded question, evidence state, validation evidence category, limitations, gaps, owners, and non-claims. | Decision-record ingredients (question, evidence state, validation evidence category, limitations, gaps, owners, non-claims) preserved without stronger claims. |
| `closeout` | Remove unsupported wording before notes are shared. | Draft meeting notes. | Meeting notes with unsupported claims removed, downgraded, or assigned. |

## Evidence Vocabulary

Use concrete static-evidence vocabulary:

- review question
- proof path
- rule ID or rule family
- evidence tier
- coverage label
- limitation
- non-claim
- gap
- next owner
- decision record
- public-safe provenance

Evidence tiers must use TraceMap's canonical terms:
`Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
`Tier4Unknown`.

Coverage labels must remain visible and should not be silently normalized into
stronger wording. Unknown, reduced, unavailable, syntax-only, or private-only
coverage must become either a visible limitation, a gap entry, a stop
condition, or an owner follow-up.

## Neighbor Distinctions

The future page should include a concise distinction block:

- `/review-room/`: broad review-room orientation. The agenda is the
  procedural meeting script or nested section.
- `/reviewer-quickstart/`: reviewer onboarding. The agenda is used during a
  focused evidence review meeting.
- `/packets/assembly/`: human assembly of public-safe packet ingredients. The
  agenda decides how those ingredients are reviewed in the meeting.
- `/handoff/template/`: reusable handoff fields. The agenda produces meeting
  outputs that may later use the template.
- `/owners/follow-up/`: post-meeting owner tracking. The agenda names the
  follow-up owner type and question.
- `/decisions/evidence-record/`: preservation of the bounded decision record.
  The agenda prepares what should be handed off.
- Manager demo or presentation route, such as `/manager-demo-script/` or the
  live equivalent `/demo/manager-script/` if present: presentation aid. The
  agenda is an evidence review meeting concept, not a demo. Implementation
  must verify the actual route name in generated output before referencing it,
  or record it as a documented gap.

The distinction block must not imply any neighboring page proves runtime
behavior, release safety, operational safety, production traffic, endpoint
performance, absence of impact, complete coverage, AI/LLM analysis, or human
governance replacement.

## Cross-Link Design

Candidate links, subject to generated-output verification at implementation
time:

- `/proof-paths/` for proof-path vocabulary.
- `/evidence/` for evidence vocabulary.
- `/validation/` for validation boundaries.
- `/limitations/` for claim and analysis limits.
- `/review-room/` for the broader review room.
- `/reviewer-quickstart/` for first-review orientation.
- `/packets/assembly/` for packet assembly.
- `/handoff/template/` for the reusable handoff field set.
- `/owners/follow-up/` for post-meeting owner follow-up, if present.
- `/decisions/evidence-record/` for decision-record handoff, if present.
- `/manager-demo-script/` as a conditional neighboring-distinction link only,
  never as meeting evidence, if present. If the live equivalent is a different
  route, implementation verifies that route before linking or records the
  mismatch as a documented gap.

Required core links are `/proof-paths/`, `/validation/`, and `/limitations/`.
Each must resolve in generated output or block standalone route readiness until
implementation records why a section placement or live route gap requires a
substitute.

The implementation should link only to routes that resolve in generated output
or explicitly record route gaps in `implementation-state.md`. Link text should
name the destination or review action without implying proof of runtime,
release, safety, production, endpoint performance, absence of impact, complete
coverage, AI/LLM analysis, or governance replacement.

## Public Safety

The future page and metadata must not publish raw facts, SQLite content,
analyzer logs, source snippets, SQL, config values, secrets, local paths, raw
remotes, generated scan directories, private sample names, command output,
hidden validation details, credential-like values, connection strings, tokens,
keys, real customer names, real owner names, real internal dates, or private
repository identifiers.

A bounded owner/repo PR-loop slug required by the operator's exact final
command is acceptable in `implementation-state.md` when
`./scripts/check-private-paths.sh` confirms it is not flagged. The public page
and metadata still must not publish raw remote URLs or private repository
identifiers.

Examples should be authored concept examples or approved public-safe demo
summaries. Public examples should use role labels such as `reviewer`,
`runtime owner`, `test owner`, or `release reviewer`, not personal names.

Blame language is out of scope. The page should say evidence is missing,
reduced, private-only, or assigned for follow-up instead of attributing fault.

## Metadata Design

For a standalone route, add or update:

- title and description;
- canonical URL;
- Open Graph title, description, URL, and type following neighboring
  concept-page patterns;
- route index metadata;
- sitemap metadata;
- discovery metadata with `publicClaimLevel: concept`;
- discovery limitations and non-claims for automation, runtime, production,
  endpoint performance, absence of impact, release safety, operational safety,
  AI/LLM, complete coverage, and human-governance boundaries.

For section placement, record why standalone route metadata and sitemap tasks
do not apply. Add a stable section anchor and any section-level discovery or
route-index metadata pattern supported by the live site.

## Validation Design

Future validation should be focused and route-aware:

- Required copy checks for `Public claim level: concept`,
  `No public conclusion without evidence`, required section labels, required
  agenda rows, evidence vocabulary, stop conditions, and non-claims.
- Accessibility checks for agenda table or list semantics, descriptive
  cross-link anchor text, heading hierarchy, and assistive-technology reachable
  section or anchor structure, reusing the live site's accessibility validator
  pattern where one exists.
- Evidence-check validation that rule ID or rule family, proof path, evidence
  tier, coverage label, and limitation are present before evidence can be
  repeated as meeting support.
- Stop-condition validation that the section body includes missing proof path,
  private-only evidence, raw or private material, unknown or reduced coverage
  without label, unsupported runtime or release wording, unsupported safety or
  production wording, endpoint performance, absence-of-impact, complete
  coverage, AI/LLM analysis, governance replacement, no next owner, and blame
  language.
- Metadata checks for title, description, canonical URL, Open Graph fields,
  route index, discovery metadata, sitemap metadata, and
  `publicClaimLevel: concept` when standalone.
- Section-placement checks for stable anchor, parent validator coverage, and
  any section-level discovery metadata pattern used by the live site.
- Link checks for selected required links and generated-output resolution.
- Rendered main-content word count with bounds selected from neighboring
  concept-page validators and recorded in `implementation-state.md`.
- If no comparable neighboring concept-page word-count pattern exists,
  implementation should target 400 to 1000 rendered main-content words as a
  conservative fallback and record the rationale in `implementation-state.md`.
- Agenda-structure checks that each required row renders a purpose, evidence
  input, and output cell or field rather than a bare label.
- Forbidden-positive-claim checks for meeting automation, release approval or
  safety, operational safety, runtime proof, production traffic, endpoint
  performance, absence-of-impact proof, complete coverage, AI or LLM analysis,
  prompt-based classification, and replacement of human judgment or
  governance.
- Unsupported-certainty-language checks that flag `impacted`, `safe`,
  `unsafe`, `approved`, `blocked`, `production proven`, `performance proven`,
  `complete`, and `root cause` outside explicit non-claim or stop-condition
  boundary regions, matching Requirement 2.
- Non-canonical-tier checks that flag confidence-style or stronger tier labels
  such as `high confidence`, `medium confidence`, `low confidence`,
  `verified`, and `guaranteed` outside explicit non-claim or stop-condition
  boundary regions.
- Forbidden private/raw material checks across rendered text, decoded HTML,
  raw HTML attributes, metadata, and examples.
- Blame-language checks or a manual public-safety gate if automated detection
  is unreliable.
- If automated blame-language detection is not implemented, the manual gate
  must record role-based public-safety reviewer signoff in
  `implementation-state.md` before publishing.
- Positive validation that required non-claim copy renders, paired with scoped
  scans that allow those topics only in clearly negated, cautionary, or
  sanctioned boundary regions.
- Site command checks: `npm test`, `npm run validate`, and `npm run build`
  from `site/`, plus `git diff --check` and
  `./scripts/check-private-paths.sh`.
- Desktop and mobile browser sanity checks when route, layout, or interaction
  changes are made.

## Non-Goals

- No scanner or reducer behavior changes.
- No generated artifact changes.
- No meeting automation.
- No release approval or release safety workflow.
- No runtime proof, production traffic proof, endpoint performance proof, or
  absence-of-impact proof.
- No complete coverage claim.
- No AI/LLM impact-analysis claim.
- No replacement of human judgment, code review, source review, tests,
  runtime observability, ownership, release process, or governance.
