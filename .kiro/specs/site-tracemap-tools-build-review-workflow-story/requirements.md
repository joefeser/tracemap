# Site TraceMap Tools Build Review Workflow Story Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future `tracemap.tools` article or page about how TraceMap is being
built under review pressure and coordination. The surface should tell a
public-safe workflow story about Codex-assisted implementation, Kiro spec
review, Qodo PR review, ACK/agent-control review loops, evidence-led specs,
claim levels, and deterministic validation.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, product
runtime behavior, or public copy changes.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or article must say `Public claim level: concept`.
- The future page or article must say `No public conclusion without evidence`.
- The article may describe workflow lessons, coordination patterns, and review
  pressure in tasteful factual terms.
- The article must not claim vendor endorsement, tool endorsement, partnership,
  certification, sponsorship, or approval by Codex, Kiro, Qodo, ACK,
  agent-control, or any review tool.
- The article must not say or imply that external workflow tools consume,
  validate, certify, operate, or analyze TraceMap output as a product feature.
- The article must not claim that TraceMap core uses AI, LLMs, embeddings,
  vector databases, prompt classification, or AI impact analysis.
- The article must not claim runtime behavior, production traffic knowledge,
  endpoint performance measurement, outage cause identification, release
  approval, release safety, operational safety, autonomous merge authority, or
  complete coverage.
- The article must not publish private session IDs, hidden run IDs, raw bot
  transcripts, private review logs, secrets, credential-like values, local
  absolute paths, private repository paths, raw remotes, private sample or
  project names, raw source snippets, raw facts, raw SQLite content, analyzer
  logs, SQL, configuration values, generated scan directories, or hidden
  validation details.

## Requirements

### Requirement 1: Define the public workflow story

The future page or article shall explain how a small deterministic tool is
built with review pressure, explicit coordination, and evidence-led specs.

Acceptance criteria:

- The story names the subject as a build workflow story, not a product
  capability claim.
- The story states that the page is concept-level public writing about the
  build process.
- The story explains that specs are used before implementation when useful,
  especially where wording, claim level, validation, or public safety matter.
- The story connects review pressure to TraceMap principles: no conclusion
  without evidence, no evidence without rule or review context, limitations
  stay attached, partial states remain labeled, and deterministic validation
  earns stronger wording.
- The story uses bounded language such as `workflow`, `review loop`,
  `handoff`, `claim level`, `spec packet`, `validation evidence`, and
  `limitations`.
- The story does not present the workflow as a universal best practice,
  benchmark, endorsement, guarantee, or replacement for human engineering
  judgment.

### Requirement 2: Cover the coordination roles without endorsement claims

The future page or article shall describe the named workflow participants and
tool classes respectfully, without overclaiming what they prove.

Acceptance criteria:

- Codex is framed as an implementation assistant or coding collaborator in the
  build workflow, not as a product capability or external endorsement.
- Kiro is framed as a spec-review pressure source that can challenge the
  packet before implementation, not as a certification authority.
- Qodo is framed as PR review pressure that may surface actionable review
  findings, not as final approval or product validation.
- ACK/agent-control is framed as a coordination loop for review state,
  evidence-backed stop reasons, and merge-readiness handoff, not as a merge
  permission shortcut.
- The article may mention that review loops can produce stop reasons, patch
  passes, re-reviews, and human decisions.
- The article must state that human ownership remains necessary for merge,
  publication, product claims, and unresolved judgment calls.
- The article must not copy raw review comments, raw bot transcripts, command
  output, internal-only or non-public reviewer identities, internal review
  dates, private cadence, hidden thresholds, session IDs, local paths, or run
  artifact paths. Public tool names and publicly documented workflow
  participants may be named.
- Before naming ACK/agent-control publicly, the implementation must confirm it
  is a publicly nameable tool or class of workflow. If it is internal,
  unreleased, or not appropriate to name publicly, the article must describe it
  generically, for example as a `review-loop coordination layer` for stop
  reasons, patch passes, validation evidence, and merge-readiness handoff, and
  record the naming decision in `implementation-state.md`.

### Requirement 3: Keep the article evidence-led and claim-level aware

The future page or article shall show how the workflow uses claim discipline
without turning concept writing into a shipped feature claim.

Acceptance criteria:

- The article explains `Public claim level: concept` as the correct level for
  a workflow story about how TraceMap is being built.
- The article describes claim levels as a way to keep future-looking,
  demo-backed, and shipped statements separate.
- The article requires limitations to travel with public process claims.
- The article requires any example claim about TraceMap behavior to cite
  public-safe evidence, a rule ID or rule family, coverage label, and
  limitation, or to remain omitted.
- The article includes non-claims that TraceMap does not replace telemetry,
  logs, traces, tests, source review, code ownership, release approval,
  incident response, or human judgment.
- The article avoids positive standalone uses of `impact`, `safe`,
  `approved`, `complete`, `production-proven`, or `autonomous` as claims about
  TraceMap behavior. Bounded compound terms already used by site vocabulary,
  such as `public-safe`, an `impact report` or `contract-impact` product-name
  reference, or `complete the checklist`, are not targeted. The
  forbidden-wording validator enforces the phrase-level patterns in
  Requirement 6, not these bare tokens, and only flags matches outside
  explicit negated non-claim or rejected-wording regions.

### Requirement 4: Define public-safe article structure

The future page or article shall use a concise article structure that fits the
existing static site and can be validated.

Acceptance criteria:

- The recommended article title is `Building TraceMap Under Review Pressure`.
- The article should include these sections:
  - claim-level note
  - the pressure that shaped the workflow
  - specs before implementation
  - implementation with reviewable diffs
  - Kiro, Qodo, and ACK review loops, or generic review-loop coordination if
    ACK/agent-control is not publicly nameable
  - what the workflow does not prove
  - lessons for evidence-led specs
  - validation and publication checklist
- The `what the workflow does not prove` section and any rejected-wording
  examples must use established non-claim or rejected-pattern region markers,
  such as `data-non-claim-region` and `data-rejected-pattern-region`, or an
  equivalent negated-pattern convention already used by site validators, so
  required non-claims containing otherwise-forbidden phrases do not trip the
  article's own forbidden-wording validation.
- The article should keep body copy between 700 and 1600 rendered words unless
  implementation records an existing site constraint. If the implementation
  deviates, it must record the constraint, source, and accepted range in
  `implementation-state.md`; the recorded range must not fall below 500 words
  or exceed 1800 words.
- The article should use a calm factual tone and avoid hype, blame, scorecard
  framing, tool-ranking language, or insider storytelling that depends on
  private context.
- Examples must be synthetic, public-safe, or category-level. They must not
  expose private sample names, hidden feature names, internal paths, private
  repositories, raw review text, raw validation logs, or session identifiers.
- The article must not include screenshots of private tooling, review
  transcripts, local terminals, private dashboards, or hidden run artifacts.

### Requirement 5: Define navigation, metadata, and hint expectations

The future implementation shall make a deliberate information-architecture
choice and record it.

Acceptance criteria:

- Candidate placements include `/blog/building-tracemap-under-review-pressure/`,
  `/building-tracemap-under-review-pressure/`, or a section on an existing
  build, review, or claim-governance surface if such a route exists at
  implementation time.
- The implementation must reconcile with the existing
  `/blog/building-tracemap-with-codex-kiro-qodo/` article. It must decide and
  record whether the new article supersedes, complements, or extends it;
  choose a non-colliding slug and clearly distinct title; cross-link the two
  when both remain public; and avoid duplicate canonical metadata, Open Graph
  metadata, and overlapping body copy.
- If the new article complements rather than supersedes the existing article,
  it must not re-explain shared Codex, Kiro, and Qodo basics already covered
  there. It should link to the existing article for that background and focus
  on differentiators: review-loop coordination, claim-level discipline,
  evidence-led spec lessons, explicit non-claims, and the validation
  checklist.
- Differentiation must extend beyond slug and title to blog-index category and
  card copy where those fields exist, so the blog index does not show two
  near-identical entries in one category.
- If implemented as a blog article, the route is included in blog index
  metadata, canonical metadata, Open Graph metadata, sitemap metadata, and any
  existing discovery metadata used for article surfaces.
- If implemented outside the blog, the implementation records why the selected
  route is clearer than the blog placement.
- The title, description, card copy, and discovery hint remain concept-level
  and process-focused.
- For blog placement, the visible in-body label `Public claim level: concept`
  is the source of truth, consistent with the proof-path article's established
  pattern. The blog article metadata schema does not currently expose
  `publicClaimLevel` or proof-path fields; if implementation adds them, it must
  extend the schema deliberately and update the blog validator.
- If placed outside the blog on a discovery-tracked route, discovery metadata
  uses the existing `publicClaimLevel: concept` field.
- The preferred proof path, when supported by existing metadata, should point
  to the closest public-safe proof, claim-guardrail, review-checklist, or
  validation route instead of raw artifacts.
- The surface is not added to primary navigation unless the implementation
  records why the existing navigation pattern supports it.
- The page should have inbound links only from related blog, review, claim,
  proof-path, or site-governance surfaces where that does not crowd the host
  page.

### Requirement 6: Add forbidden wording and private-material checks

The future implementation shall include focused validation for article safety.

Acceptance criteria:

- Validation checks rendered site output: rendered text, decoded HTML, raw
  HTML attributes, metadata, sitemap output, discovery output, fixtures, and
  article data for forbidden private material. Spec source files under
  `.kiro/specs/` are not in scope for this validation.
- Forbidden private material includes private session IDs, hidden run IDs, raw
  bot transcripts, raw review logs, secrets, credential-like values, local
  absolute paths, private repository paths, raw remotes, private sample or
  project names, raw source snippets, raw facts, raw SQLite content, analyzer
  logs, SQL, configuration values, generated scan directories, and hidden
  validation details.
- Validation checks for forbidden product and workflow claims outside explicit
  negated non-claim or rejected-example regions.
- Forbidden claim patterns include:
  - `TraceMap uses AI`
  - `AI impact analysis`
  - `LLM impact analysis`
  - `embeddings`
  - `vector database`
  - `prompt classification`
  - `production traffic`
  - `endpoint performance`
  - `outage cause`
  - `release safe`
  - `safe to release`
  - `approved by Codex`
  - `approved by Kiro`
  - `approved by Qodo`
  - `certified by`
  - `endorsed by`
  - `autonomous merge`
  - `complete coverage`
  - `tools consume TraceMap`
- Validation allows these terms only in explicitly marked boundary,
  non-claim, or rejected-wording contexts where the wording is negated and
  cannot be mistaken for a positive claim.
- The forbidden-wording validator must scope exceptions using existing
  region-marker conventions, such as `data-non-claim-region` and
  `data-rejected-pattern-region`, or equivalent negated patterns already used
  by site validators, so required non-claims and rejected-wording examples are
  not flagged.
- Blog implementation should mirror the existing per-article validator pattern
  and register the article slug in the existing blog-slug allowlist or
  collision-control mechanism where that pattern still exists.
- Validation must ensure the article includes the visible concept claim-level
  label and shared principle.

### Requirement 7: Define future implementation validation

The future implementation shall run the existing site and repository
validation appropriate for a public static page.

Acceptance criteria:

- Future implementation runs `npm test` from `site/` after site source changes.
- Future implementation runs `npm run validate` from `site/`.
- Future implementation runs `npm run build` from `site/`.
- Future implementation runs `git diff --check` from the repository root.
- Future implementation runs `./scripts/check-private-paths.sh` from the
  repository root.
- Future implementation performs desktop and mobile browser sanity checks when
  layout or interaction changes are made.
- Future implementation records route decisions, metadata decisions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items in this spec's `implementation-state.md`.
