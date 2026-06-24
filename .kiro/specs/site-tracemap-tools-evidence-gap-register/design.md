# Site TraceMap Tools Evidence Gap Register Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design describes a future public-safe page or section for an evidence gap
register on `tracemap.tools`. It gives the future implementation a bounded
information architecture, content model, required row matrix, validation plan,
and wording boundary while keeping this branch spec-only.

The design does not implement site code, generated output, scanner behavior,
reducer behavior, validation scripts, navigation, sitemap metadata, public
copy, runtime proof, production traffic proof, endpoint performance proof,
outage-cause proof, release approval, operational safety, complete coverage,
clean-repo status, AI/LLM analysis, replacement of human review, or any other
boundary forbidden in `requirements.md`.

## Placement Options

Recommended placement options:

- `/evidence/gaps/`: preferred standalone route if the site has or gains an
  evidence family. It makes the register a sibling of proof and evidence
  recording surfaces.
- `/coverage/gaps/`: preferred standalone route if the site has or gains a
  coverage family. It emphasizes reduced, stale, missing, unsupported, and
  unknown coverage states.
- Section on `/limitations/reduced-coverage/`: use when the register should
  remain close to reduced-coverage guidance and can fit without overwhelming
  that page.
- Section on `/reviewer-quickstart/`: use when implementation treats gap
  recording as a reviewer workflow step rather than a standalone reference.

If `/evidence/gaps/` or `/coverage/gaps/` would introduce a new top-level
family for one page, record the information-architecture tradeoff in
`implementation-state.md`. If a host page is already compact, default to a
standalone route or keep the section near the lower word-count bound.
When both `/evidence/` and `/coverage/` options are otherwise acceptable,
prefer the existing route family over creating a new top-level family for one
concept page.

When the register is placed as a section on `/limitations/reduced-coverage/`,
the adjacent-surfaces list references the host page or the gap-register
section anchor for that entry instead of a separate cross-link, and the "gap
register, not reduced-coverage playbook" distinction is made inline within the
host page. The implementation must record the self-host case in
`implementation-state.md`.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/limitations/reduced-coverage/`, because that surface explains
  reduced coverage while the register records follow-up rows.
- Replacing `/limitations/`, because limitations remain the canonical boundary
  and non-claim surface.
- Replacing `/validation/`, because validation remains about checks and
  evidence quality.
- Replacing `/questions/objections/`, because objections need stakeholder
  answers while the register needs row-level follow-up.
- Replacing `/owners/follow-up/`, because owner handoff explains workflow
  while the register supplies the gap item.
- Replacing `/decisions/evidence-record/`, because a decision record captures
  an owner decision after evidence review while the register captures missing
  or insufficient evidence before a stronger public claim can be repeated.
- Replacing `/review-claim-checklist/`, because the checklist decides whether
  a claim can be repeated while the register records the reason to hold,
  downgrade, or hand off.
- Adding the register to primary navigation before an information-architecture
  review records why concept-level governance content belongs there.

## Page Model

Recommended sections:

1. Opening: state `Public claim level: concept`, `No public conclusion without
   evidence`, and the short promise that a gap row is a follow-up item, not a
   public conclusion.
2. When a gap is useful: explain that gaps are useful when they preserve what
   evidence exists, what cannot be concluded, next owner, validation route,
   and stop condition.
3. Gap register fields: define the required row field set.
4. Example gap rows: include the required rows and fields.
5. Stop conditions: define where readers must stop, downgrade, keep internal,
   or hand off.
6. Next-owner handoff: show role-based handoff shape without private names or
   blame language.
7. Safe wording: provide bounded examples that preserve the gap label and next
   proof route.
8. Unsafe wording: list rejected patterns inside a programmatically
   identifiable boundary region.
9. Non-claims: restate the hard boundaries and private/raw material rules.
10. Adjacent surfaces: link to public-safe equivalents for
   `/limitations/reduced-coverage/`, `/limitations/`, `/validation/`,
   `/questions/objections/`, `/owners/follow-up/`,
   `/decisions/evidence-record/`, and `/review-claim-checklist/` when they
   exist.

## Gap Register Field Set

Every example row must preserve:

- Gap label.
- What evidence exists.
- What cannot be concluded.
- Public claim level.
- Next owner.
- Proof or validation route.
- Safe wording.
- Stop condition.

Richer implementations may also show rule ID or rule family, evidence tier,
coverage label, commit context, extractor version, limitation, and review
date placeholder when public-safe. If any richer field is unavailable or
private-only, the public surface labels it `unavailable`, `private-only`,
`stale`, or `pending` rather than silently omitting the boundary.

The public claim level for all required rows starts at `concept`. A future row
may link to demo-backed material, but the register row remains concept-level
unless a separate evidence-backed decision upgrades the row.

## Required Gap Rows

The implementation may adjust wording for house style, but it must preserve
the scenario set, field set, boundaries, and stop conditions below.

| Gap label | What evidence exists | What cannot be concluded | Public claim level | Next owner | Proof/validation route | Safe wording | Stop condition |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Missing proof path | A public-safe summary says a claim needs proof, but the supporting route or artifact is absent, unavailable, or not ready for public use. | Public proof, repeatable proof-link support, demo-backed wording, or absence of impact. | concept | Site owner or reviewer | Target `/validation/`, `/review-claim-checklist/`, or public-safe proof-path documentation. | `Record this as a missing proof path and collect a public-safe proof route before repeating the claim.` | Stop before publishing proof-link wording or treating the claim as supported. |
| Reduced coverage | Scan or review context carries a reduced, partial, fallback, unsupported, or unavailable coverage label. | Clean repo, complete coverage, release safety, operational safety, runtime behavior, or no impact. | concept | Scanner owner or reviewer | Target `/limitations/reduced-coverage/` or `/limitations/`. | `Coverage is reduced, so keep the label attached and use the row as review input only.` | Stop before removing the limitation or upgrading the conclusion. |
| Tier4Unknown | Evidence exists only as an unknown tier, unresolved gap, unavailable source, or unclassified support note. | `Tier1Semantic`, `Tier2Structural`, or `Tier3SyntaxOrTextual` certainty; semantic proof; complete proof; conclusion by confidence or repetition. | concept | Reviewer or scanner owner | Target evidence-tier documentation, `/limitations/`, or `/validation/`. | `The evidence tier is Tier4Unknown, so keep the statement downgraded until evidence is classified.` | Stop before upgrading the tier or repeating a stronger claim. |
| Private-only support | Internal evidence may exist, but public-safe summary, public route, or reusable proof wording is not available. | Public proof, public demo support, customer-specific conclusion, or publishable detail. | concept | Site owner or evidence owner | Target `/review-claim-checklist/`, `/limitations/`, or a public-safe summary route. | `Treat private-only support as internal follow-up until a public-safe summary exists.` | Stop before publishing raw or private material or citing it as public proof. |
| Stale commit | Evidence belongs to an older source context, previous commit, older branch state, or outdated public summary. | Current-head proof, current-release wording, current source behavior, or current validation status. | concept | Reviewer or source owner | Target `/validation/` or the current public-safe proof route. | `The source context is stale, so current wording needs current-context confirmation.` | Stop before current-head, current-release, or current-proof wording. |
| Unsupported framework surface | A framework, route style, file type, adapter surface, or project pattern is outside public support or only structurally recognized. | Complete framework coverage, complete route coverage, runtime behavior, or semantic certainty. | concept | Framework owner or scanner owner | Target `/limitations/` or `/limitations/reduced-coverage/`. | `Record the framework surface as unsupported and route it to owner or scanner follow-up.` | Stop before complete-framework, complete-route, or runtime wording. |
| Missing validation evidence | A claim, row, or route lacks public-safe validation evidence, focused test evidence, build evidence, or browser sanity evidence. | Validation passed, demo backed, implementation ready, release ready, or proof complete. | concept | Implementer or reviewer | Target `/validation/` or a public-safe validation summary route. | `Hold the claim until the required validation evidence is present or explicitly deferred; internal validation plans are next steps, not public proof routes.` | Stop before marking the implementation or claim ready. |
| Unresolved owner question | The evidence points to a question that requires a service, framework, source, artifact, reviewer, or site owner response. | Owner agreement, absence of impact, operational safety, release approval, or resolved risk. | concept | Named public role category, not a private person | Target `/owners/follow-up/`, `/decisions/evidence-record/`, or closest public-safe owner handoff route. | `Carry the owner question forward with the evidence and stop condition attached.` | Stop before converting an unanswered question into an assumption. |

If a target route does not exist at implementation time, record it as
deferred, substituted, or omitted in `implementation-state.md` with the target
type and follow-up task. No more than two required rows may carry deferred,
substituted, or omitted route values at one time without a spec amendment.

## Stop Conditions

A gap row should stop public claim repetition when:

- the proof path is missing, unavailable, private-only, or stale;
- coverage is reduced, partial, syntax-only, unsupported, or unknown;
- the evidence tier is `Tier4Unknown`;
- validation evidence is absent, hidden, raw, or not public-safe;
- the next owner has not answered a question needed for the public claim;
- the wording would imply runtime proof, production traffic proof, release
  safety, operational safety, endpoint performance proof, outage-cause proof,
  complete coverage, clean-repo status, AI/LLM impact analysis, or absence of
  impact.

Stop means hold the claim, downgrade the claim, keep it internal, collect
public-safe evidence, or hand it off. It does not mean the source is broken,
the owner is at fault, the release is unsafe, the system is safe, or no impact
exists.

## Next-Owner Handoff

Use role labels:

- site owner;
- reviewer;
- scanner owner;
- framework owner;
- source owner;
- artifact publisher;
- implementation owner;
- evidence owner.

Avoid real people, private team names, customer names, service names,
repository identities, private sample names, local paths, remotes, command
output, or hidden validation detail.

Recommended handoff shape:

```text
Gap label:
Evidence that exists:
What cannot be concluded:
Requested next evidence:
Public-safe proof or validation route:
Stop condition:
Non-claim:
```

The handoff transfers the evidence question. It does not approve a release,
prove runtime behavior, assign blame, settle an owner decision, or replace
human review.

## Safe Wording Patterns

Use bounded wording:

- `Record this as a missing proof path until a public-safe proof route exists.`
- `Coverage is reduced, so this is review input, not a final conclusion.`
- `The evidence tier is Tier4Unknown and cannot support a stronger public
  claim yet.`
- `Private-only support needs a public-safe summary before public citation.`
- `The source context is stale and needs current-context confirmation.`
- `The framework surface is unsupported, so complete framework coverage is not
  available from this evidence.`
- `Validation evidence is missing, so hold readiness wording.`
- `The owner question remains open and should travel with the stop condition.`

Preferred verbs:

- `record`
- `label`
- `preserve`
- `hold`
- `downgrade`
- `route`
- `hand off`
- `collect`
- `verify`
- `stop`

## Unsafe Wording Patterns

Reject unsupported wording:

- `The missing proof path proves there is no impact.`
- `Reduced coverage means the repository is clean.`
- `Tier4Unknown is enough because reviewers are confident.`
- `Private evidence proves the public claim.`
- `Stale evidence supports the current release.`
- `Unsupported framework coverage is complete.`
- `Missing validation evidence is acceptable for readiness.`
- `The unresolved owner question can be treated as approval.`
- `The register proves production traffic, runtime behavior, operational
  safety, or release safety.`
- `The register proves endpoint performance or identifies an outage cause.`
- `The register is AI or LLM impact analysis.`
- `The register replaces human review.`

Unsafe examples must be framed as rejected patterns, not live claims. They
must not appear in page metadata, link text, summaries, captions, or discovery
records as affirmative statements. The implementation must place them in a
programmatically identifiable rejected-pattern, limitation, non-claim, or
validation-warning region.

The rejected-pattern region identifier must be recorded in
`implementation-state.md` before the unsafe-wording validator is written.
Prefer an existing site convention such as `data-blocked-phrase` or a
site-consistent equivalent. Forbidden-live-claim and private/raw-material
validators must exclude only those sanctioned boundary regions and must still
fail if unsafe wording appears outside them.

## Private And Raw Material Boundary

The future surface must not publish raw facts, SQLite, analyzer logs, source
snippets, SQL, config values, secrets, local paths, remotes, generated scan
directories, private sample names, command output, hidden validation details,
or credential-like values.

The page may name those artifact families only inside boundary copy that says
they are not public material and require public-safe summaries before public
linking.

All examples must be synthetic, authored, or already public-safe. If an
example is illustrative, label it illustrative unless it links to an existing
public-safe demo or documentation surface.

## Validation Design

Future implementation should add focused tests or validators for:

- visible `Public claim level: concept`;
- visible `No public conclusion without evidence`;
- required sections and stable anchors;
- required rows and required row fields;
- accessible table semantics or equivalent programmatic field association;
- proof/validation route resolution, allowed target families, and deferral
  cap;
- adjacent-surface distinctions and required links or recorded substitutions;
- standalone metadata, sitemap metadata, and discovery metadata if standalone;
- section host metadata, duplicate IDs, and anchor resolution if section
  placement is chosen;
- forbidden live claims and unsafe wording context;
- stable rejected-pattern, limitation, non-claim, or validation-warning region
  markers recorded in `implementation-state.md` before validator work begins;
- forbidden private/raw material in rendered text, decoded HTML, attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and
  bot-oriented discovery surfaces;
- concrete site artifacts that satisfy discovery metadata, discovery records,
  and bot-oriented discovery surfaces, recorded in `implementation-state.md`
  before validator work begins;
- blame-language avoidance in row labels, examples, and handoff copy;
- visible body word count bounds: 900 to 1,700 visible body words for a standalone route,
  or 450 to 1,000 visible body words for a section placement, excluding
  navigation, metadata, code blocks, and required row field text. Required row
  field text means only the content of the eight required fields within each
  example gap row: gap label, what evidence exists, what cannot be concluded,
  public claim level, next owner, proof/validation route, safe wording, and
  stop condition. Introductory text, section headings, and safe-wording,
  unsafe-wording, non-claim, and handoff copy outside the row structure count
  toward the bound. A handoff template rendered as a code block is excluded as
  a code block; handoff prose outside the code block counts. Authoritative
  definition: `requirements.md` Requirement 5.
- aggregate site validation integration;
- desktop and mobile browser sanity for layout, table readability, and
  responsive behavior.

Validation should inspect generated output after build, not only source files,
when checking metadata, sitemap, discovery records, raw/private material, and
rendered wording.
