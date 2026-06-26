# Site TraceMap Tools Evidence Gap Register Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks track a spec-only packet now and a future implementation phase.
This branch must not edit site source, generated output, scanner code, reducer
code, or existing specs.

## Spec Review Tasks

- [x] Create the spec-only packet under
  `.kiro/specs/site-tracemap-tools-evidence-gap-register/`.
- [x] Run Kiro spec review with `claude-opus-4.8` using
  `scripts/kiro-review.mjs`, or record the exact unavailable-tool/model error
  in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` using
  `scripts/kiro-review.mjs`, or record the exact unavailable-tool/model error
  in `implementation-state.md`.
- [x] If either requested model is unavailable, record exact errors, run the
  review with the best available model if the harness offers one, record the
  substitution and rationale in `implementation-state.md`, and note which
  models were used and unavailable. No requested model was unavailable during
  the initial review; one Sonnet run returned reduced coverage due to denied
  tool access and is recorded in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings from all review passes are patched or explicitly
  dispositioned and at least one re-review confirms no remaining Medium or
  higher findings, or any remaining Medium or higher findings or rerun gaps are
  explicitly dispositioned with the reason recorded in
  `implementation-state.md`. Record the confirming artifact path or paths in
  `implementation-state.md` before checking this task.
- [x] After the final re-review that confirms no Medium or higher findings, or
  after an explicit infeasibility disposition, record the confirming artifact
  path in `implementation-state.md` under gate status before checking the
  readiness gate task.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`; if absent, record the absence in
  `implementation-state.md` and substitute a manual grep for absolute paths,
  raw credential patterns, and private sample names before marking complete.
  Script was present and passed on 2026-06-24.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, placement decision, review results, validation plan, oddities, and
  follow-up items before changing site code.
- [x] Choose final placement from `/evidence/gaps/`, `/coverage/gaps/`,
  section on `/limitations/reduced-coverage/`, section on
  `/reviewer-quickstart/`, or a recorded equivalent if site information
  architecture has changed.
- [x] Before editing site source, enumerate which of the seven adjacent routes
  (`/limitations/reduced-coverage/`, `/limitations/`, `/validation/`,
  `/questions/objections/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, `/review-claim-checklist/`) exist in `site/`
  and record present, absent, moved, substituted, omitted, or deferred status
  for each in `implementation-state.md`.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] Explain why the selected placement is a gap register rather than
  `/limitations/reduced-coverage/`, `/limitations/`, `/validation/`,
  `/questions/objections/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, or `/review-claim-checklist/`.
- [x] Add the concept-level page or section using existing static-site layout,
  typography, accessibility, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Include required sections for when a gap is useful, gap register fields,
  example gap rows, stop conditions, next-owner handoff, safe wording, unsafe
  wording, and non-claims.
- [x] Include the required missing proof path row.
- [x] Include the required reduced coverage row.
- [x] Include the required `Tier4Unknown` row.
- [x] Include the required private-only support row.
- [x] Include the required stale commit row.
- [x] Include the required unsupported framework surface row.
- [x] Include the required missing validation evidence row.
- [x] Include the required unresolved owner question row.
- [x] Ensure every required row includes gap label, what evidence exists, what
  cannot be concluded, public claim level, next owner, proof/validation route,
  safe wording, and stop condition.
- [x] Use accessible table semantics or an equivalent programmatic repeated
  structure for row labels and field labels.
- [x] Keep the page or section out of primary navigation unless
  `implementation-state.md` records a matching information-architecture
  decision.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [x] If implemented as a section, validate that the host route metadata,
  claim-level wording, sitemap entry, discovery entry, and stable anchors
  preserve the host boundary and do not upgrade the section. Not applicable:
  standalone `/evidence/gaps/` route selected.
- [x] Link to public-safe equivalents for `/limitations/reduced-coverage/`,
  `/limitations/`, `/validation/`, `/questions/objections/`,
  `/owners/follow-up/`, `/decisions/evidence-record/`, and
  `/review-claim-checklist/` when they exist.
- [x] Record absent, moved, or unavailable adjacent routes in
  `implementation-state.md` with substitution, omission, or deferral notes.
- [x] Forbid absence-of-impact proof, runtime proof, production traffic proof,
  endpoint performance proof, outage-cause proof, release approval or safety,
  operational safety, complete coverage, clean-repo status, AI/LLM analysis,
  prompt-based classification, embedding search, vector database analysis,
  autonomous approval, and replacement of human review.
- [x] Do not publish raw facts, raw SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, remotes, generated scan dirs,
  private sample names, command output, hidden validation details, or
  credential-like values.
- [x] Use only synthetic, authored, or already public-safe examples.
- [x] Label illustrative examples as illustrative unless they link to an
  existing public-safe demo or documentation surface.
- [x] Avoid blame language in row labels, examples, and owner handoff copy.
- [x] Add focused validation for visible concept claim label and shared
  principle.
- [x] Add focused validation for required sections.
- [x] Add focused validation for required rows and required row fields.
- [x] Add focused validation for accessible table semantics or equivalent
  programmatic row-label and field-label association in the example gap rows
  structure.
- [x] Add focused validation for required links, allowed public-safe target
  families, and deferred/substituted/omitted route cap.
- [x] Add focused validation that every adjacent-surface link and every
  required-row proof/validation route resolves to an existing generated page
  or anchor after `npm run build`, or is explicitly recorded as deferred,
  substituted, or omitted in `implementation-state.md`.
- [x] If the register is implemented as a section on an adjacent surface,
  record the self-host case in `implementation-state.md` and validate the
  host page or section anchor as the adjacent-surface reference for that entry.
  Not applicable: standalone `/evidence/gaps/` route selected.
- [x] Add focused validation for adjacent-surface distinctions.
- [x] Before writing discovery or bot-oriented validators, enumerate and
  record in `implementation-state.md` which concrete site artifacts satisfy
  discovery metadata, discovery records, and bot-oriented discovery surfaces,
  such as sitemap, robots, bot-readable summaries, Open Graph tags, or the
  existing site-equivalent artifacts.
- [x] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata if a standalone route is chosen.
- [x] Add focused validation for section host metadata, duplicate IDs, and
  anchor resolution if a section placement is chosen. Not applicable:
  standalone `/evidence/gaps/` route selected.
- [x] Add focused validation for forbidden live claims and unsafe wording
  context.
- [x] Add focused validation for forbidden private/raw material across
  rendered text, decoded HTML, raw HTML attributes, metadata, sitemap output,
  discovery output, fixtures, tests, and bot-oriented discovery surfaces.
- [x] Add focused validation for word count bounds: 900 to 1,700 visible body
  words for a standalone route, or 450 to 1,000 visible body words for a
  section placement, excluding navigation, metadata, code blocks, and required
  row field text. Required row field text means the content of the eight
  required fields inside the example gap rows structure; prose outside the row
  structure counts toward the bound. Definition per `requirements.md`
  Requirement 5.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `npm run build` from `site/` after site source is added to produce
  generated output.
- [x] Run `npm run validate` from `site/` after build so metadata, sitemap,
  discovery, rendered wording, and private/raw material checks inspect
  generated output rather than only source files.
- [x] Run `npm test` from `site/` after build.
- [x] Run desktop and mobile browser sanity checks if route, layout, table,
  responsive behavior, or interaction changes are made.
- [x] Update this spec's `tasks.md` checkboxes as implementation tasks are
  completed.
- [x] Keep generated output out of git unless a future workflow explicitly
  changes the generated-output policy.
