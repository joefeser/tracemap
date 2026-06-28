# Site TraceMap Tools Legacy Modernization Evidence Map Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe TraceMap site page or section explaining how static
evidence can organize legacy modernization and migration planning questions.
The content should help managers, architects, and engineers understand which
repository surfaces TraceMap can inventory, where coverage is reduced, and what
must remain hidden until sanitized validation exists.

This is a site-spec-only packet. It does not implement site code, scanner code,
reducer behavior, demo artifacts, generated summaries, or static site output.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future page should use `Public claim level: concept` because it is an
explanatory modernization-planning surface, not a shipped product capability
page and not a checked-in public demo result for a complete legacy assessment.
Existing public-safe pages may support TraceMap's general deterministic static
evidence model, coverage labels, limitations, and generated artifact families,
but public legacy modernization evidence must remain bounded to what is already
safe and specific.

Do not upgrade this page to `demo` unless a future phase adds checked-in,
public-safe proof for the exact demo rows shown on the page. Do not upgrade any
capability to `shipped` unless it is true on main and public-safe evidence
supports the wording. Capabilities that exist only on `dev` must be labeled
`dev-only` or omitted.

## Audience

- Managers planning a legacy modernization or migration assessment who need a
  safe way to ask what static evidence exists before approving scope.
- Architects comparing old framework, project, service, UI, and data surfaces
  without turning static inventory into runtime architecture proof.
- Engineers who need to understand where project load failed, where syntax
  fallback still found useful facts, and where human review remains necessary.
- Reviewers who need visible rule IDs, evidence tiers, coverage labels,
  limitations, proof paths, and explicit non-claims.

## Core Message

TraceMap can help organize deterministic static evidence from a repository
snapshot during legacy modernization planning. It can frame old framework and
toolchain clues, project-load gaps, syntax fallback, config/project metadata,
and legacy surface families such as WCF, ASMX, remoting, WinForms, WebForms,
and legacy data metadata when public-safe evidence exists.

The page must not claim runtime behavior, production usage, endpoint
performance, release safety, operational safety, database existence, package
compatibility, exploitability, outage cause, or complete product coverage. It
must describe evidence as planning input and review context, not proof that a
migration is safe or complete.

## Requirements

### Requirement 1: Define a bounded legacy modernization evidence surface

The future implementation shall add a concept-level public page or section that
explains how TraceMap organizes static evidence for legacy modernization and
migration planning.

Acceptance criteria:

- The implementation chooses a placement such as a standalone
  `/legacy-modernization/evidence-map/` route, a section on an existing
  modernization/use-case page, or a section on a limitations/orientation page,
  then records the selected placement and rejected alternatives in
  `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section states the shared site principle.
- The page or section explains that TraceMap works from a repository snapshot
  and checked-in artifacts, not runtime traffic, production telemetry, or live
  system inspection.
- The page or section introduces no runtime service, repository upload flow,
  private sample dependency, telemetry collector, AI/LLM workflow, client-side
  data fetch, generated local artifact dependency, or product-completeness
  claim.
- The page or section states its distinct role relative to the existing
  `/legacy-evidence/` and `/legacy-validation/` concept surfaces: this page is
  a reviewer-question evidence map for modernization planning, not the legacy
  evidence story and not the legacy validation plan page.
- The chosen placement avoids duplicating or superseding those sibling pages,
  and the distinction is recorded in `implementation-state.md`.
- The page or section also records why it does not duplicate or supersede
  `/capabilities/`, `/limitations/`, `/validation/`, `/manager-packet/`, and
  the claim-governance or claim-ledger page. It links to those pages instead
  of restating capability maturity, limitations, validation status, the manager
  packet, or the canonical claim ledger.
- If implemented as a standalone route, route metadata, sitemap metadata, and
  discovery metadata preserve the `concept` claim level.
- The page or section does not enter primary navigation unless a future site
  information-architecture review explicitly chooses that placement and records
  the rationale.

### Requirement 2: Connect legacy evidence themes to reviewer questions

The page shall translate legacy modernization concerns into evidence-backed
review questions without claiming those questions are fully answered.

Acceptance criteria:

- The page includes a scannable evidence map with reviewer questions, public
  evidence shape, allowed wording, required limitation, and hidden or
  deferred material.
- The map covers old framework and toolchain clues, including target framework,
  project style, MSBuild/toolset, package/restore, and generated/designer-file
  indicators when public-safe proof exists.
- The map covers project load failures and build environment gaps as reduced
  coverage signals. Failed build or project load is never described as a clean
  repository.
- The map covers syntax fallback as useful reduced-coverage evidence when
  Roslyn/MSBuild semantic analysis is unavailable.
- The map covers WCF, ASMX, remoting, WinForms, WebForms, and legacy data
  metadata surfaces only where public-safe evidence exists or as concept-level
  examples with explicit `concept`, `dev-only`, or `hidden` labels.
- The map connects evidence to practical reviewer questions such as: what old
  project shape is visible, what service/UI/data surfaces need review, what was
  found by semantic analysis versus syntax/config fallback, what gaps remain,
  and what must be validated before public or migration conclusions.
- Rows use terms such as `static evidence`, `surface`, `reference`, `metadata`,
  `coverage gap`, `needs review`, and `planning input`.
- Rows avoid saying a surface is `impacted` unless a deterministic reducer
  result with public-safe rule IDs, evidence tiers, and limitations supports
  that wording.
- Evidence-map rows fall into two labeling categories:
  general static-evidence-model rows and legacy-surface detection rows.
- General static-evidence-model rows include old frameworks/toolchains,
  project-load/build-as-reduced-coverage, syntax fallback, and config/project
  metadata coverage. These rows may use `concept`, or `main`/`shipped` with an
  inline public proof link, when they describe only TraceMap's publicly
  documented deterministic evidence model, coverage tiers, and reviewer
  questions. They are not forced to `hidden` by the unledgered-theme rule.
- Legacy-surface detection rows include WCF, WCF metadata, remoting, WebForms
  event/route/navigation, WinForms, ASMX/SOAP, legacy data metadata, and build
  environment diagnostics detection. These rows are governed by sibling-ledger
  reconciliation and default-to-hidden rules.
- A general-model row must not smuggle a hidden legacy-surface detection claim.
  For example, a project-load/build-gaps row may state that failed build or
  project-load failure means reduced coverage and never a clean repository, but
  it must not assert the `build environment diagnostics` detection capability
  while the sibling ledger pins that capability `hidden`.
  Likewise, a config/project metadata row may state that TraceMap inventories
  checked-in config/project files as reduced-coverage evidence, but it must not
  assert WCF or `system.serviceModel` binding detection, service-reference
  detection, endpoint extraction, or connection-string extraction while those
  themes are pinned `hidden`. It must never render raw service addresses,
  endpoint values, connection strings, or config values.
- Per-theme row labels for WCF, WCF metadata, remoting, WebForms, WinForms,
  ASMX/SOAP, legacy data metadata, build diagnostics, and flow composition must
  be reconciled against the existing theme claim ledger in
  `site-tracemap-tools-legacy-evidence-story` as the authoritative label
  source, and cross-checked against `legacy-story-reconciliation` as an
  internal coexistence reference whose contents stay hidden.
- A theme marked `hidden` in that ledger must not be shown here as `concept`
  support language, `demo-backed`, `main`, or `shipped` unless the ledger is
  updated in the same change with public-safe proof.
- At implementation time, the sibling ledger state must be re-checked before
  row labels are assigned. In the current snapshot, every theme in the
  `site-tracemap-tools-legacy-evidence-story` ledger is pinned `hidden`, so the
  legacy-surface rows for WCF, WCF metadata, remoting, WebForms, legacy data
  metadata, build diagnostics, and flow composition default to `hidden` here
  until the sibling ledger is updated in the same change with public-safe
  proof.
- Themes that have no entry in the `site-tracemap-tools-legacy-evidence-story`
  claim ledger at implementation time, currently including WinForms
  navigation/event surfaces, ASMX/SOAP services, and WebForms route/navigation
  surfaces beyond the sibling ledger's narrower WebForms event-flow theme,
  default to `hidden` here until a future change adds them to the sibling
  ledger with public-safe proof.
- Each unledgered-theme default is recorded in `implementation-state.md`, and
  the absence is flagged as a follow-up to extend the sibling ledger.
- A `concept` row label may describe only the general static-evidence model and
  reviewer question for a general-model surface family or another surface
  family that has no `hidden` entry in the sibling ledger and is not a
  required-but-unledgered legacy detection theme. It must not assert
  theme-specific support, maturity, or detection capability for any theme the
  sibling ledger pins `hidden`, or for required-but-unledgered detection themes
  such as WinForms and ASMX/SOAP. When in doubt between abstract evidence-model
  framing and theme support language, the row is labeled `hidden`.
- `omitted` is reserved only for cases where naming the surface family itself
  would leak hidden capability detail or private validation information.
- Any divergence from the sibling ledger is recorded as a gap in
  `implementation-state.md`.

### Requirement 3: Preserve public-safe claim boundaries

The page shall keep modernization and migration language conservative and
evidence-bound.

Acceptance criteria:

- The page does not claim TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  production dependency understanding, exploitability, database existence,
  package compatibility, migration feasibility, migration completeness, or
  complete product coverage.
- The page does not claim TraceMap performs AI impact analysis, LLM analysis,
  prompt-based classification, embeddings, vector search, or probabilistic
  impact analysis.
- The page does not imply TraceMap replaces runtime telemetry, source-owner
  review, architecture review, security review, database review, test results,
  build/restore validation, migration planning, release approval, or human
  judgment.
- The page treats project-load failure, syntax-only evidence, unsupported file
  formats, generated-file gaps, missing toolchains, malformed metadata, and
  private validation gaps as first-class limitations.
- The page uses `dev-only` or omits capabilities that exist only on `dev`.
- The page uses `hidden` or aggregate wording for private or unsanitized legacy
  validation work and does not disclose hidden capability counts, private
  sample identities, validation cadence, or unreleased sequencing.

### Requirement 4: Tie examples to public-safe proof paths

The page shall use only public-safe proof paths for specific examples and shall
label unsupported examples as concept-only.

Acceptance criteria:

- Specific demo rows appear only when backed by checked-in public-safe pages,
  generated summaries, documentation, rule catalog material, validation
  summaries, reports, or demo artifacts that are safe to publish.
- Public proof links do not expose raw `facts.ndjson`, raw `index.sqlite`, raw
  SQLite extracts, analyzer logs, generated scan directories, raw source
  snippets, raw SQL, config values, connection strings, secrets, local absolute
  paths, raw repository remotes, private sample names, raw service addresses,
  raw endpoints, customer data, or production identifiers.
- When the source of truth is local-only facts, SQLite, analyzer logs, report
  artifacts, rule catalog material, or unsanitized validation, the page names
  only the artifact family and links only to a public-safe summary or public
  route.
- Rule IDs, evidence tiers, file paths, line spans, commit SHA, extractor
  versions, coverage labels, and limitations appear only when public-safe and
  backed by an existing proof path.
- Missing or future proof paths remain visible as limitations instead of being
  treated as evidence-backed rows.
- Any public-safe fixture paths must be repo-relative and already appropriate
  for public site copy.

### Requirement 5: Define recommended content structure

The future page or section shall be concise, scannable, and useful to planning
roles without becoming marketing copy.

Acceptance criteria:

- Include an opening section that says this is a concept-level modernization
  evidence map for static repository evidence.
- Include a reader-question section for managers, architects, engineers, and
  reviewers.
- Include an evidence map table or equivalent layout with columns for legacy
  concern, reviewer question, evidence shape, public status, limitation, and
  proof path.
- Include rows or grouped entries for:
  old frameworks/toolchains, project load/build gaps, syntax fallback,
  config/project metadata, WCF/service references, ASMX/SOAP services,
  remoting, WinForms navigation/events, WebForms event/route/navigation
  surfaces, and legacy data metadata.
- Surface families required here that default to a non-public label under
  Requirement 2 appear as named `hidden` rows with surface family, reviewer
  question, and limitation, but no theme-specific support claim.
- `omitted` is reserved only for cases where naming the surface family itself
  would leak hidden capability detail or private validation information.
  Publicly documented framework names such as WCF, ASMX/SOAP, remoting,
  WinForms, and WebForms do not by themselves require omission.
- Include a coverage section explaining semantic, structural, syntax/textual,
  and unknown/gap evidence in public-safe terms.
- Include a hidden-until-sanitized section explaining that private sample names,
  raw artifacts, raw config/data values, and unsanitized validation stay out of
  public copy.
- Include a non-claims section with modernization, migration, runtime,
  operational, security, database, package, AI, and product-completeness
  boundaries.
- Include a final link set to relevant public-safe routes confirmed to exist at
  implementation time. Candidate routes include `/capabilities/`,
  `/limitations/`, `/validation/`, `/proof-paths/`, `/demo/result/`,
  `/demo/proof-upgrades/`, `/legacy-evidence/`, `/legacy-validation/`,
  `/manager-packet/`, `/claims/` or `/claim-ledger/`, and any adoption page
  that exists in generated output.
- Before linking to any route, verify it resolves in generated output. Record
  unresolved, moved, or unavailable routes as gaps in `implementation-state.md`
  rather than linking to them.
- The evidence map uses accessible table semantics or a responsive equivalent
  that preserves row labels and limitations on mobile.

### Requirement 6: Add metadata and validation without inflating the claim

The future implementation shall make the page discoverable while preserving
concept-level boundaries and machine-checkable non-claims.

Acceptance criteria:

- If implemented as a standalone route, add page title, description, canonical
  URL, Open Graph fields, sitemap metadata, and discovery metadata following
  the site schema that exists at implementation time.
- Discovery metadata uses `publicClaimLevel: concept` or the current equivalent
  schema field.
- Discovery metadata and any bot/LLM-oriented discovery entry include
  limitations and non-claims for runtime proof, production behavior, release
  safety, migration completeness, database existence, package compatibility,
  AI/LLM analysis, raw artifacts, and private validation.
- Stable section anchors are added for reader questions, evidence map,
  coverage gaps, hidden material, proof paths, and non-claims when a standalone
  route is implemented.
- Cross-links from existing pages are added only where link text reinforces
  static evidence boundaries and does not imply a shipped modernization
  assessment product.
- Focused validation checks rendered copy and metadata for the concept claim
  level, route links, required sections, forbidden private material, forbidden
  runtime/migration overclaims, and unsupported `impacted` wording.
- Focused validation is wired into the existing site validation and test
  workflow when implementation touches site code.
- Future implementation validation includes both `git diff --check` and
  `git diff --cached --check` before final handoff.

### Requirement 7: Record implementation and review state

Future implementation shall keep the spec-local state file current.

Acceptance criteria:

- `implementation-state.md` records branch, scope, route or placement decision,
  public claim level, validation, review findings, oddities, and follow-ups.
- `implementation-state.md` records that `git diff --check`,
  `git diff --cached --check`, and `./scripts/check-private-paths.sh` each
  completed without errors before `Readiness` is changed to
  `ready-for-implementation`.
- Medium or higher spec-review findings are patched before readiness is moved
  to `ready-for-implementation`.
- Future implementation tasks remain unchecked until implementation actually
  completes the corresponding site work.
- State notes avoid local absolute paths, raw remotes, raw facts, raw SQLite
  paths, analyzer log content, private sample names, secrets, raw SQL, raw
  config values, and generated scan directory names.

## Validation Plan

Spec-creation validation:

- Run Kiro spec review with `claude-opus-4.8` and `claude-sonnet-4.6` if
  available through `scripts/kiro-review.mjs`.
- Patch Medium or higher review findings and rerun re-review where feasible.
- Run `git diff --check`.
- Run `git diff --cached --check`.
- Run `./scripts/check-private-paths.sh`.

Future site implementation validation:

- Run `git diff --check`.
- Run `git diff --cached --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`, or record the exact gap if the
  validator no longer exists.
- Run `npm run build` from `site/`.
- Run `./scripts/check-private-paths.sh`.
- Run desktop and mobile browser sanity checks if layout or interaction changes
  are made.

## Artifact Safety Rules

Safe to publish or summarize:

- Public routes on the TraceMap site.
- Public-safe generated summaries and checked-in demo reports.
- Repo-relative public fixture paths already suitable for public copy.
- Rule IDs, evidence tiers, coverage labels, gap labels, extractor versions,
  commit SHA, line spans, and limitations when backed by public-safe proof.
- Abstract legacy surface families such as WCF, ASMX, remoting, WinForms,
  WebForms, project metadata, and legacy data metadata when phrased as
  concept-level planning context or backed by public-safe proof.

Do not publish:

- Raw source snippets, raw SQL, config values, connection strings, secrets,
  local absolute paths, raw repository remotes, generated scan directories, raw
  fact streams, SQLite files, analyzer logs, combined SQLite files, raw service
  addresses, raw endpoint values, private sample identities, customer data,
  production identifiers, hidden validation details, hidden capability counts,
  or unreleased sequencing.

## Claim Boundaries

Use:

- "TraceMap can organize deterministic static evidence for modernization
  planning."
- "This evidence can guide reviewer questions and identify coverage gaps."
- "Specific rows need public-safe proof paths, otherwise they stay concept,
  dev-only, hidden, or omitted."
- "Project load failure or syntax-only fallback means reduced coverage, not a
  clean repository."

Avoid:

- "TraceMap proves a migration is safe."
- "TraceMap validates legacy modernization."
- "TraceMap proves runtime behavior, production traffic, endpoint performance,
  database existence, package compatibility, exploitability, or outage cause."
- "TraceMap uses AI or LLM analysis to decide impact."
- "TraceMap covers all legacy technologies."
