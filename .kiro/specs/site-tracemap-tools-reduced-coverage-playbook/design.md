# Site TraceMap Tools Reduced Coverage Playbook Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design describes a future public-safe page or section for reduced
coverage handling on `tracemap.tools`. It gives the future implementation a
stable information architecture, content model, row matrix, validation plan,
and boundary vocabulary while keeping this branch spec-only.

The design does not implement site code, generated output, scanner behavior,
reducer behavior, or validation scripts.

## Placement Options

Recommended starting options:

- `/coverage/reduced/`: preferred standalone route when the site has or will
  have a coverage family. It keeps reduced coverage guidance discoverable
  without crowding limitations or validation.
- `/limitations/reduced-coverage/`: preferred standalone route when the site
  treats reduced coverage primarily as a limitation-and-non-claim surface.
- Section on `/limitations/`: use when the content must stay close to the
  canonical limitations page and the row matrix can remain compact.
- Section on `/validation/`: use when implementation primarily presents this
  as post-validation guidance for interpreting reduced scan results.

If `/coverage/reduced/` would introduce a new top-level route family with only
one child, justify that choice against the existing `/limitations/` and
`/validation/` families in `implementation-state.md`. If a host page is
already compact, roughly under 900 visible body words at implementation time,
prefer a standalone route or keep the section near the lower end of the
section word-count bound.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/limitations/`, because limitations should remain the canonical
  boundary reference while this playbook teaches reader action.
- Replacing `/validation/`, because validation should remain about checks and
  evidence quality while this playbook focuses on reduced-coverage handoff.
- Replacing `/static-vs-runtime/`, because the playbook can link to runtime
  boundaries but should not become the runtime telemetry explainer.
- Replacing `/questions/objections/`, because objections need broad
  stakeholder answers while this playbook needs a scenario matrix.
- Replacing `/proof-paths/faq/`, because the FAQ explains proof paths while
  this page gives reduced-coverage labels, next evidence, and stop conditions.
- Replacing `/review-claim-checklist/`, because the checklist decides whether
  a claim may be repeated while this playbook supplies reduced-coverage inputs
  for that decision.
- Adding to primary navigation before information-architecture review, because
  concept-level playbook content is likely secondary governance content.

## Page Model

Recommended sections:

1. Opening: state `Public claim level: concept`, the shared site principle,
   and the short promise that reduced coverage is a label and handoff state,
   not a conclusion.
2. What reduced coverage means: define reduced, partial, syntax fallback,
   private-only, stale, unavailable, and unknown-tier states as evidence
   boundaries.
3. How to label it: show the required field set that should travel with any
   reduced-coverage statement.
4. Reduced coverage matrix: include the required rows and fields.
5. Safe conclusions: provide bounded phrases that preserve the label and
   avoid stronger proof.
6. Unsafe conclusions: list rejected patterns for absence-of-impact, clean
   repo, runtime, production traffic, endpoint performance, outage cause,
   release, operational, complete-coverage, AI/LLM, prompt-based
   classification, embedding search, vector database analysis, and
   replacement claims.
7. Next evidence to collect: map each required row to a public-safe evidence
   target.
8. Owner handoff: provide a role-based handoff shape that names the evidence
   question, requested action, proof target, and stop condition.
9. Stop conditions: define when readers must stop, downgrade, keep internal,
   or hand off.
10. Non-claims and private/raw boundary: restate hard boundaries and public
   proof-link rules.
11. Adjacent surfaces: link to current public-safe equivalents for
   `/limitations/`, `/validation/`, `/static-vs-runtime/`,
   `/questions/objections/`, `/proof-paths/faq/`, and
   `/review-claim-checklist/` when they exist.

## Label Field Set

Every reduced-coverage example or row should preserve:

- Coverage label.
- Evidence available.
- Missing or reduced evidence.
- Rule ID or rule family when public-safe.
- Evidence tier.
- Commit context or public-safe source context.
- Extractor version or extractor-version context when public-safe.
- Limitation.
- What cannot be concluded.
- Next owner role.
- Safe wording.
- Stop condition.
- Proof or validation link.

When a field is unavailable or private-only, the field should be labeled as
unavailable or private-only rather than omitted.

The eight matrix rows below enforce the required row fields from the
requirements, including evidence tier. The full label field set is also the
recommended shape for richer prose examples, owner-handoff templates, and
review packets. Extractor version, rule ID, and commit context are not
required in every matrix cell. If a full-field value is not public-safe, label
it `unavailable` or `private-only` rather than omitting the boundary.

Supplementary state markers use a closed vocabulary: `unavailable`,
`private-only`, and `stale`. A row may list more than one of the four allowed
evidence tier tokens, but it must not introduce custom tier names.

## Required Matrix

| Scenario | Coverage label | Evidence tier | Evidence available | What cannot be concluded | Next owner | Safe wording | Stop condition | Proof/validation link |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Build/load failure | reduced build/load coverage | `Tier3SyntaxOrTextual` or `Tier4Unknown` with `unavailable` marker depending on fallback evidence | Syntax fallback, config/project scan, public-safe gap summary, or analyzer summary when available | Clean repo, complete analysis, compiler-resolved conclusions, absence of impact | Build/tooling owner or scanner owner | `Coverage is reduced because build/load evidence is unavailable; use syntax and gap evidence as review input only.` | Stop before clean-repo, complete-coverage, or release-safety wording. | Target `/validation/` or `/limitations/` |
| Syntax fallback | syntax fallback coverage | `Tier3SyntaxOrTextual` | Syntax-only references, textual matches, line spans, rule family, and gap label | Tier1 semantic symbol resolution, call graph certainty, complete dependency path | Scanner owner or reviewer | `Syntax fallback found a static reference that needs semantic or owner review.` | Stop before semantic, runtime, or impact conclusions. | Target `/proof-paths/faq/` or `/validation/` |
| Missing semantic evidence | missing semantic coverage | `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown` with `unavailable` marker, but not `Tier1Semantic` | Structural or syntax evidence, unresolved symbol note, limitation | Tier1Semantic conclusion, compiler-resolved ownership, complete path | Scanner owner or build/tooling owner | `Semantic evidence is missing, so the claim stays reduced and needs owner follow-up.` | Stop before upgrading to Tier1 or removing the limitation. | Target `/validation/` or `/limitations/` |
| Unsupported framework surface | framework surface coverage gap | `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown` depending on the support note | Known framework limitation, structural clue, public-safe support note | Complete framework coverage, route completeness, runtime behavior | Framework owner or scanner owner | `This framework surface is outside current public proof, so the row is a coverage gap.` | Stop before complete-framework or complete-route wording. | Target `/limitations/` |
| Missing generated artifact | public proof artifact unavailable | Source-side tier when public-safe, otherwise `Tier4Unknown` with `unavailable` marker | Source-side evidence summary, scan manifest family, or recorded artifact gap | Public proof-link claim, reproducible public demo evidence, complete artifact set | Artifact publisher or site owner | `The public-safe artifact is unavailable, so the claim needs a replacement proof link or must stay internal.` | Stop before linking, publishing, or repeating the claim publicly. | Target `/proof-paths/faq/` or `/validation/` |
| Private-only support | private-only coverage | `Tier4Unknown` with `private-only` marker in public copy | Internal evidence category, public-safe limitation, owner note | Public proof, customer-specific conclusion, public demo support | Site owner or reviewer | `Evidence may support internal follow-up, but public wording needs a public-safe summary first.` | Stop before publishing private/raw details or public proof wording. | Target `/limitations/` or `/review-claim-checklist/` |
| Stale commit context | stale source context | Source-context tier with `stale` marker | Previous commit summary, branch label, public-safe age note | Current-head behavior, current release status, current proof path | Reviewer or source owner | `This evidence belongs to an older source context and needs current-context confirmation.` | Stop before current-head, current-release, or current-proof wording. | Target `/validation/` |
| Unknown evidence tier | unknown tier | `Tier4Unknown` | Gap label, rule family, available artifact summary, limitation | Stronger evidence tier, semantic certainty, complete coverage | Reviewer or scanner owner | `The tier is unknown, so the statement must stay downgraded until evidence is classified.` | Stop before tier upgrade or conclusion by confidence. | Target `/limitations/` or evidence-tier documentation |

The implementation may adjust wording for house style, but it must preserve
the scenario set, fields, claim boundaries, and stop conditions.

When source-side evidence is private-only and cannot be summarized publicly,
the public evidence tier must be `Tier4Unknown` with a visible private-only
label rather than a custom tier name.

Unavailable proof links and explicitly labeled analysis gaps are represented
through the existing rows, especially missing generated artifact, build/load
failure, private-only support, and unknown evidence tier, rather than as
separate required scenarios.

If a target route does not exist at implementation time, record it as
deferred in `implementation-state.md` with the target type and a follow-up
task. No more than two rows may be deferred simultaneously without a spec
amendment.

## Safe Wording Patterns

Use bounded wording:

- `Coverage is reduced, so this is review input, not a final conclusion.`
- `Syntax fallback found a static reference that needs semantic or owner
  review.`
- `The proof path is incomplete because public-safe artifact output is
  unavailable.`
- `This evidence can support owner follow-up, but it cannot support public
  proof wording yet.`
- `The source context is stale, so current-head wording must wait.`
- `The tier is unknown, so the claim should be downgraded or held.`
- `The next step is to collect <public-safe evidence target> or keep the claim
  internal.`

Preferred verbs:

- `label`
- `preserve`
- `inspect`
- `record`
- `downgrade`
- `hold`
- `hand off`
- `collect`
- `verify`
- `stop`

## Unsafe Wording Patterns

Reject unsupported wording:

- `Reduced coverage proves there is no impact.`
- `A failed or reduced analysis is a clean repo.`
- `Syntax fallback proves runtime behavior.`
- `Missing semantic evidence still proves compiler-resolved impact.`
- `The release is approved or safe because no public proof was found.`
- `Operational safety is confirmed.`
- `Framework coverage is complete.`
- `The analysis is AI or LLM impact analysis.`
- `The analysis uses prompt-based classification, embedding search, or vector
  database analysis.`
- `The page authorizes autonomous approval.`
- `The page replaces human review, tests, runtime observability, service owner
  review, or release process.`
- `Raw facts, SQLite files, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, remotes, generated scan dirs, private sample
  names, command output, hidden validation details, or credential-like values
  should be pasted into public copy.`

Unsafe examples must be framed as rejected patterns, not live claims. They
must not appear in page metadata, link text, summaries, captions, or discovery
records as affirmative statements.
An explicitly bounded rejected-pattern region must use a programmatically
identifiable marker such as a dedicated component, wrapper element, or data
attribute. Visual-only styling or prose-only labels are not enough for
validation.

## Adjacent Surface Distinctions

The playbook should answer "What do we do now that coverage is reduced?"

Neighboring pages answer different questions:

- `/limitations/`: "What are the general boundaries and non-claims?"
- `/validation/`: "What checks ran and what did they verify?"
- `/static-vs-runtime/`: "Which questions belong to static evidence versus
  runtime systems?"
- `/questions/objections/`: "How should stakeholders understand common
  concerns?"
- `/proof-paths/faq/`: "How do proof paths work, and what do missing proof
  fields mean?"
- `/review-claim-checklist/`: "May this claim be repeated, downgraded, held,
  or kept internal?"

If an adjacent route is absent, moved, or concept-only at implementation time,
record that fact in `implementation-state.md` and avoid dead links.
When an adjacent route does not yet exist, omit the link and name the surface
without a hyperlink, or include a visible `(planned)` qualifier. Do not link
to a known missing route.
Validation must confirm that absent routes appear as non-linked text with a
`(planned)` qualifier or are fully omitted, and must reject any live hyperlink
to a route recorded as absent in `implementation-state.md`.

## Metadata And Discovery

Standalone route metadata should:

- Use concept-level title and description wording.
- Include `publicClaimLevel: concept` or the current equivalent site metadata.
- Prefer `hintCategory` values aligned with governance, limitations,
  validation, or proof-path guidance.
- Use `preferredProofPath` or equivalent fields to point to public-safe
  limitations, validation, or proof-path routes when they exist.
- Keep concept-level discovery title, summary, and limitations text clear of
  shipped-wording traps. In particular, avoid putting the field label
  `evidence available` in discovery metadata if the active discovery
  validator treats `available` as shipped wording.
- Include non-claims for runtime proof, production traffic proof, endpoint
  performance proof, outage-cause proof, release approval, operational safety,
  complete coverage, AI/LLM analysis, prompt-based classification, embedding
  search, vector database analysis, absence-of-impact proof, clean-repo claims
  under reduced analysis, and replacement of human review.

Section placement metadata should:

- Preserve the host route's claim level and non-claims.
- Add stable anchors for the playbook sections.
- Validate duplicate IDs and anchor resolution in generated HTML.
- Avoid turning a concept section into a shipped page-level capability.

## Validation Design

The future implementation should add a focused validator or focused tests
wired into the existing site validation workflow. Validation should inspect:

- Rendered text.
- Decoded HTML.
- Raw HTML attributes.
- Metadata.
- Sitemap output.
- Discovery output.
- Fixtures and tests for the playbook.
- Link targets in generated output.

Required validation groups:

- Visible metadata and principle.
- Required sections.
- Required rows and required row fields.
- Required adjacent route distinctions.
- Required proof or validation links.
- Proof/validation link deferral cap and follow-up records.
- Standalone metadata or section-host metadata.
- Cross-link anchor text that names the destination boundary or topic instead
  of generic or claim-asserting phrases.
- Forbidden claims, including absence-of-impact proof, clean-repo claims under
  failed or reduced analysis, runtime proof, production traffic proof,
  endpoint performance proof, outage-cause proof, release approval or safety,
  autonomous approval, operational safety, complete coverage, AI/LLM analysis,
  prompt-based classification, embedding search, vector database analysis, and
  replacement of human review.
- Forbidden private/raw material.
- Blame-free phrasing and row-label neutrality.
- Build/load failure row-label preservation, with surrounding copy checked for
  attribution rather than flagging the label itself.
- Unsafe wording context.
- Rejected-pattern scoping that prevents phrase detectors from flagging
  explicitly bounded rejected examples, non-claims, limitations, or
  validation warnings.
- Static HTML presence and programmatic association for required matrix fields
  before manual browser sanity.
- Synthetic-example safety.
- Word count bounds.
- Desktop and mobile sanity checks when layout or interaction changes.

Recommended word-count bounds:

- Bounds exclude navigation, metadata, code blocks, and all text inside cells
  of the required reduced-coverage matrix, including row labels, coverage
  labels, evidence tiers, evidence-available text, cannot-conclude text,
  owner text, safe-wording text, stop-condition text, and proof/validation
  link text. The exclusion applies to all text structurally inside the matrix
  element, including column headers, captions, footnotes, and in-cell links,
  but not prose sections that precede, follow, or describe the matrix. Body
  prose outside the matrix counts toward the bound.
- Standalone route: 1,000 to 1,900 visible body words.
- Section placement: 500 to 1,100 visible body words for playbook-section
  prose only, not pre-existing host page prose outside the section.
- If the full matrix cannot fit the section bound without dropping required
  rows or fields, choose a standalone placement instead.

## Accessibility And Layout

The required matrix may be a table, cards, or a hybrid responsive pattern.
Whichever pattern is chosen:

- Row labels and field labels must remain programmatically associated.
- The coverage label, what cannot be concluded, next owner, and stop condition
  must remain visible on mobile.
- Links must have descriptive anchor text.
- The page must not rely on hover-only disclosure for required guidance.
- If accordions or progressive disclosure are used, the full required text
  must be present in the static HTML and keyboard accessible.

## Copy Rules

Use static-evidence and handoff vocabulary:

- `reduced coverage`
- `partial evidence`
- `syntax fallback`
- `analysis gap`
- `coverage label`
- `evidence tier`
- `proof path`
- `public-safe summary`
- `owner follow-up`
- `stop condition`
- `non-claim`

Avoid blame language:

- Do not say a person, team, reviewer, service, or owner caused missing,
  reduced, stale, or conflicting evidence.
- Prefer neutral states and next actions.

Avoid unsupported conclusion wording outside rejected-pattern context:

- `proves`
- `guarantees`
- `certifies`
- `approves`
- `replaces`
- `resolved`
- unqualified `impacted`
- `safe to release`
- `operationally safe`
- `clean repo`
- `complete coverage`
- `AI impact analysis`
- `LLM analysis`
- `prompt-based classification`
- `embedding search`
- `vector database analysis`
