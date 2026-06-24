# Site TraceMap Tools Change-Risk Language Guide Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public change-risk language guide for `tracemap.tools`. The
guide will help reviewers, managers, engineers, architects, and implementation
agents choose bounded words when describing static evidence around a change.

This is a spec-only site phase. It does not implement site code, scanner code,
reducer behavior, generated output, or existing specs. The future page is
public wording guidance only; deterministic scanner facts, reducer outputs,
reports, rule catalogs, coverage labels, documented limitations, commit
context, and extractor versions remain the source of evidence.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or section shall say `Public claim level: concept`.
- The future page or section shall say `No public conclusion without evidence`.
- The guide may teach cautious public wording for static evidence, but it must
  not say or imply that TraceMap proves impact, absence of impact, safety,
  runtime behavior, operational readiness, or release readiness.
- The guide must not claim production traffic knowledge, endpoint performance
  knowledge, complete coverage, AI/LLM impact analysis, or replacement of human
  judgment.
- The guide must not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, command output, hidden validation details, or credential-like values.
- Public copy must avoid blame language. It should describe evidence,
  uncertainty, ownership, coverage, and handoff needs without assigning fault.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs, evidence tiers, coverage labels, limitations, analysis gaps,
  public-safe proof paths, and human review decisions.

## Requirements

### Requirement 1: Choose a public placement

The future implementation shall add a public language guide page or section
using an explicit placement decision.

Acceptance criteria:

- The implementation evaluates the candidate placements
  `/language/change-risk/`, `/review-claim-checklist/language/`, a section on
  `/review-claim-checklist/`, and a section on `/questions/objections/`.
- The implementation may select a different public-safe placement only if it
  records why each named candidate was rejected and how the selected placement
  remains discoverable.
- The selected placement and rejected alternatives are recorded in
  `implementation-state.md` with short reasons.
- The chosen page or section says `Public claim level: concept`.
- The chosen page or section says `No public conclusion without evidence`.
- The primary audience is reviewers, managers, engineers, architects, and
  implementation agents who need bounded public language for change review.
- The route copy makes clear that the guide provides wording discipline, not
  a product claim that TraceMap proves impact, safety, runtime behavior, or
  release readiness.
- If a standalone route is chosen, the implementation adds route metadata,
  sitemap metadata, discovery metadata, canonical metadata, and internal-link
  validation.
- If a folded placement is chosen, the implementation records why standalone
  sitemap and discovery metadata were not added, and it ensures the section has
  stable anchors and public discovery from nearby pages.
- A folded placement is selected only if all required sections, required
  tables, and named required table examples fit within the folded-section word
  count bound. If the required content cannot fit, a standalone route is
  selected instead.
- If a folded placement is chosen, the host route's claim level is compatible
  with `concept`, or the `Public claim level: concept` text is unambiguously
  scoped to the guide section so it does not contradict the host page's claim
  level. Validation confirms section-level and page-level claim signals do not
  conflict.

### Requirement 2: Distinguish the guide from adjacent public surfaces

The future page shall explain its role without duplicating or overriding
existing public guidance surfaces.

Acceptance criteria:

- The guide distinguishes itself from `/review-claim-checklist/` by focusing
  on language choices rather than checklist completion.
- The guide distinguishes itself from `/questions/objections/` by focusing on
  wording patterns rather than objection handling.
- The guide distinguishes itself from `/release-review-boundary/` by avoiding
  release approval, readiness, safety, or go/no-go claims.
- The guide distinguishes itself from `/static-vs-runtime/` by staying inside
  static evidence wording and not claiming runtime observation.
- The guide distinguishes itself from `/proof-paths/faq/` by teaching claim
  language rather than proof-path navigation.
- The guide distinguishes itself from `/manager-faq/` by providing reusable
  phrasing rules rather than management Q&A.
- The page links to adjacent routes when they exist and when the link clarifies
  responsibility boundaries.
- Link text must not imply that the language guide is a source of truth over
  scanner facts, reducer findings, rule catalog entries, or documented
  limitations.

### Requirement 3: Include required sections

The future guide shall include the required content sections using stable
anchors.

Acceptance criteria:

- The guide includes a `why wording matters` section that explains how public
  wording can accidentally overstate static evidence.
- The guide includes a `safe static-evidence phrases` section.
- The guide includes an `unsafe phrases` section.
- The guide includes an `evidence-required wording` section.
- The guide includes a `reduced-coverage wording` section.
- The guide includes an `owner-handoff wording` section.
- The guide includes a `stop conditions` section.
- The guide includes a `non-claims` section.
- Required stable anchors include `#why-wording-matters`,
  `#safe-static-evidence-phrases`, `#unsafe-phrases`,
  `#evidence-required-wording`, `#reduced-coverage-wording`,
  `#owner-handoff-wording`, `#stop-conditions`, and `#non-claims`.
- Each required section uses public-safe authored examples and does not expose
  raw evidence, raw repository material, command output, or hidden validation
  details.
- Section copy uses neutral, non-blaming language such as `needs review`,
  `coverage is reduced`, `evidence shows`, `TraceMap found`, `not established
  by this scan`, and `owner decision needed`.

### Requirement 4: Include required wording tables

The future guide shall include scannable tables that map review situations to
public-safe wording.

Acceptance criteria:

- The guide includes a `safe phrasing` table with examples that stay inside
  static evidence, rule IDs, evidence tiers, coverage labels, and limitations.
- The guide includes an `unsafe/blocked phrasing` table that names phrases to
  avoid and explains the boundary they cross.
- The guide includes a `when to use needs review` table.
- The guide includes a `when to say evidence shows` table.
- The `when to say evidence shows` table also covers `TraceMap found` with at
  least one row that distinguishes its narrower scope: deterministic static
  evidence only, not runtime behavior, production behavior, release safety, or
  business correctness.
- The guide includes a `when to say coverage is reduced` table.
- The guide includes a `when to stop` table.
- Tables include at least one column for the condition, at least one column for
  the allowed wording or blocked wording, and at least one column for the
  evidence or boundary reason.
- The `safe phrasing` table includes examples such as `static evidence shows`,
  `evidence is limited to`, `coverage is reduced`, `needs review`, and
  `owner decision needed`.
- The `unsafe/blocked phrasing` table blocks phrases equivalent to
  `TraceMap proved impact`, `safe to release`, `no impact`, `runtime confirms`,
  `production is unaffected`, `complete coverage`, `AI analyzed the change`,
  and `approved for merge`.
- Unsafe or blocked examples and non-claims are rendered in a
  machine-distinguishable form, such as a stable wrapper, class, data
  attribute, or strictly negated `do not say` context, so validation can
  distinguish teaching examples from affirmative product claims.
- Tables do not publish raw facts, source snippets, SQL, config values, local
  paths, remotes, private sample names, command output, or credential-like
  values.

### Requirement 5: Define evidence-required wording

The future guide shall teach when stronger wording is allowed and what
evidence must be attached.

Acceptance criteria:

- The guide says `evidence shows` is allowed only when the statement is backed
  by a public-safe proof path, rule ID or equivalent supporting ID, evidence
  tier, coverage label, and limitation.
- The guide says `TraceMap found` may describe deterministic static evidence
  only, not runtime behavior, production behavior, release safety, or business
  correctness.
- The guide says `needs review` is appropriate when evidence exists but does
  not support a deterministic conclusion, when names are high fan-out or noisy,
  when ownership is unclear, or when coverage is reduced.
- The guide says `not established by this scan` is appropriate when absence of
  impact, runtime behavior, operational safety, or release readiness has not
  been proven.
- The guide forbids upgrading static evidence into claims about runtime
  behavior, production traffic, endpoint performance, operational safety,
  release safety, or complete coverage.
- The guide describes limitations as part of the claim, not as optional
  disclaimers.

### Requirement 6: Define reduced-coverage and stop-condition wording

The future guide shall provide public-safe language for partial analysis and
hard stops.

Acceptance criteria:

- The guide says `coverage is reduced` when MSBuild or project load fails,
  semantic analysis is unavailable, analysis falls back to syntax/textual
  evidence, relevant adapters did not run, or expected evidence is missing.
- Reduced-coverage wording must identify the consequence without implying the
  scan is useless or clean.
- The guide says to stop before publishing stronger claims when required
  evidence is missing, coverage is reduced and the claim depends on the missing
  coverage, a reviewer asks for raw or private material, public copy would
  imply release approval, or wording would blame a team or person.
- Stop-condition guidance includes an owner-handoff phrase that asks for a
  decision without declaring the change safe, unsafe, impacted, or unimpacted.
- Stop-condition guidance must not instruct readers to expose raw artifacts,
  private repository material, secrets, hidden validation details, or command
  output.

### Requirement 7: Preserve public-safe material boundaries

The future guide shall avoid private, raw, or credential-like material.

Acceptance criteria:

- The page does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, command output, hidden validation details, or credential-like values.
- Public examples are authored wording examples or derived from already public
  concept/demo summaries.
- Examples may mention artifact families, but they must not include raw
  artifact contents or instructions to publish raw artifacts.
- The guide may say evidence should include rule IDs, evidence tiers, coverage
  labels, limitations, public-safe proof paths, and supporting IDs.
- The guide does not include local commands or copied tool output.
- The guide avoids blame language and does not name private owners, teams,
  samples, remotes, paths, databases, services, or credentials.

### Requirement 8: Add metadata and validation expectations

The future implementation shall make the guide discoverable and validate its
claim boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph fields following existing site patterns.
- Standalone discovery metadata includes `publicClaimLevel: concept`,
  `sourceType: site-page`, a valid existing `hintCategory`, a
  `preferredProofPath`, limitations, and `nonClaims`.
- Standalone sitemap metadata includes the route if a standalone route is
  chosen.
- If the guide is folded into an existing route, validation confirms the
  containing page has a stable section anchor, discoverable inbound links, and
  no conflicting claim-level signal.
- Validation checks visible text for `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validation checks that all required sections and required tables are present.
- Validation checks required links to the selected adjacent routes and records
  any route that was expected but unavailable at implementation time.
- Validation checks forbidden claims for impact proof, absence-of-impact proof,
  release approval/safety, operational safety, runtime proof, production
  traffic, endpoint performance, complete coverage, AI/LLM analysis, and
  replacement of human judgment. Each forbidden-claim guard distinguishes
  affirmative public claims from sanctioned non-claims and quoted blocked
  examples across all categories.
- Validation checks forbidden private/raw material, including raw fact names,
  SQLite names, analyzer log names, raw source snippets, SQL/config values,
  secrets, local absolute paths, remotes, generated scan directories, private
  sample names, command output, hidden validation details, and credential-like
  values.
- Validation enforces word-count bounds. Recommended standalone page bounds:
  1000 to 2400 rendered words. Recommended folded-section bounds: 650 to 1600
  rendered words.
- `Rendered words` means whitespace-delimited tokens in the guide's main
  visible content region after HTML rendering, including section prose and
  table cell text, and excluding site chrome, global navigation, footer text,
  metadata, code or attribute values, and machine-only wrapper markup used to
  tag blocked examples.
- Validation includes desktop and mobile browser sanity for layout, table
  readability, visible claim-level text, visible principle text, and absence of
  horizontal overflow when route, layout, or interaction changes are made.
- Future implementation runs `npm test`, `npm run validate`, and
  `npm run build` from `site/`, then `git diff --check` and
  `./scripts/check-private-paths.sh` from the repository root.

### Requirement 9: Keep this phase spec-only

This phase shall modify only the new Kiro spec directory.

Acceptance criteria:

- This phase writes only files under
  `.kiro/specs/site-tracemap-tools-change-risk-language-guide/`.
- This phase does not edit `site/src`, generated output, scanner code, reducer
  code, existing specs, package files, or validation scripts.
- Future implementation tasks remain unchecked until implementation work is
  actually performed in a later phase.
- `tasks.md` and `implementation-state.md` remain accurate after spec review.
