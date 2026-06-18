# Site TraceMap Tools Legacy Modernization Evidence Map Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are future implementation work. They remain unchecked because this
branch is spec creation only.

Note: validation tasks must pass before future implementation tasks are checked
complete.

- [ ] Run re-review with at least one of `claude-opus-4.8` or
  `claude-sonnet-4.6` after patching Medium or higher findings, and record
  results in `implementation-state.md`, before advancing `Readiness` to
  `ready-for-implementation`.
- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, route or placement decision, public claim level, validation results,
  review findings, oddities, and follow-up items before changing site code.
- [ ] Choose the future page placement: standalone
  `/legacy-modernization/evidence-map/`, a section on an existing
  modernization/use-case page, or a section on an existing validation,
  limitations, or orientation page.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`, including why the chosen placement does not imply
  a shipped modernization assessment product.
- [ ] Record why the selected placement does not duplicate or supersede
  `/legacy-evidence/`, `/legacy-validation/`, `/capabilities/`,
  `/limitations/`, `/validation/`, `/manager-packet/`, or the
  claim-governance or claim-ledger page.
- [ ] Confirm the page or section is not added to primary navigation unless a
  future site information-architecture review explicitly chooses that
  placement and records the rationale.
- [ ] Add the concept-level page or section using existing static-site layout,
  typography, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept` copy and the shared site
  principle.
- [ ] Explain that TraceMap organizes deterministic static repository evidence
  for modernization planning, not runtime behavior, production telemetry, or
  migration safety.
- [ ] Add reader-question guidance for managers, architects, engineers, and
  reviewers.
- [ ] Add an evidence map table or responsive equivalent with legacy concern,
  reviewer question, evidence shape, public status, limitation, and proof path.
- [ ] Include evidence-map rows or groups for old frameworks/toolchains,
  project load/build gaps, syntax fallback, config/project metadata,
  WCF/service references, ASMX/SOAP services, remoting, WinForms navigation or
  events, WebForms event/route/navigation surfaces, and legacy data metadata.
- [ ] Render required public framework-family rows that default to a non-public
  label as named `hidden` rows with surface family, reviewer question, and
  limitation, but no theme-specific support claim.
- [ ] Reserve `omitted` only for cases where naming the surface family itself
  would leak hidden capability detail or private validation information.
- [ ] Label each row as `demo-backed`, `main` or `shipped`, `dev-only`,
  `concept`, `hidden`, or `omitted` according to public-safe proof.
- [ ] Classify rows before labeling them as either general
  static-evidence-model rows or legacy-surface detection rows.
- [ ] Allow `concept` labels for general static-evidence-model rows such as old
  frameworks/toolchains, project-load/build-as-reduced-coverage, syntax
  fallback, and config/project metadata coverage when they do not assert hidden
  legacy detection support.
- [ ] Ensure config/project metadata rows do not assert WCF or
  `system.serviceModel` binding detection, service-reference detection,
  endpoint extraction, or connection-string extraction while those themes are
  hidden, and never render raw service addresses, endpoint values, connection
  strings, or config values.
- [ ] Treat WCF, WCF metadata, remoting, WebForms event/route/navigation,
  WinForms, ASMX/SOAP, legacy data metadata, build environment diagnostics
  detection, and flow composition as legacy-surface detection rows governed by
  sibling-ledger reconciliation.
- [ ] Reconcile every legacy theme row label against the
  `site-tracemap-tools-legacy-evidence-story` claim ledger as the
  authoritative label source, and cross-check
  `legacy-story-reconciliation` as an internal coexistence reference whose
  contents stay hidden.
- [ ] Do not publish a `concept`, `demo-backed`, `main`, or `shipped` row as
  support language for any theme the sibling ledger pins at `hidden` unless the
  same change updates that ledger with public-safe proof.
- [ ] Re-check the sibling ledger state at implementation time before assigning
  row labels; do not assume the current snapshot remains true.
- [ ] Treat any theme absent from the sibling ledger at implementation time,
  currently WinForms, ASMX/SOAP, and WebForms route/navigation surfaces beyond
  the sibling ledger's narrower WebForms event-flow theme, as `hidden` and
  record the gap in `implementation-state.md`.
- [ ] Use `concept` row labels only for general static-evidence model and
  reviewer-question framing for general-model rows or surface families with no
  hidden sibling-ledger entry and no required-but-unledgered detection default,
  not theme-specific support, maturity, or detection capability for
  hidden-ledger themes.
- [ ] Require checked-in public-safe proof paths before showing specific demo
  rows.
- [ ] Require inline public proof links for any row labeled `main` or
  `shipped`, even though the page itself remains concept-level.
- [ ] Label capabilities that exist only on `dev` as `dev-only` or omit them.
- [ ] Keep hidden or unsanitized validation abstract; do not disclose private
  sample names, validation cadence, hidden capability counts, or unreleased
  sequencing.
- [ ] Explain that project load failure or failed build means reduced coverage,
  not a clean repository.
- [ ] Explain syntax fallback as useful reduced-coverage evidence, not semantic
  proof.
- [ ] Explain semantic, structural, syntax/textual, and unknown/gap evidence in
  public-safe language.
- [ ] Avoid saying a surface is `impacted` unless a deterministic reducer
  result with public-safe rule IDs, evidence tiers, proof path, and limitations
  supports the wording.
- [ ] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  production dependency understanding, exploitability, database existence,
  package compatibility, migration feasibility, migration completeness, AI
  impact analysis, LLM analysis, and complete product coverage.
- [ ] Ensure copy does not imply TraceMap replaces runtime telemetry,
  source-owner review, architecture review, security review, database review,
  test results, build/restore validation, migration planning, release
  approval, or human judgment.
- [ ] Link proof paths only to public-safe routes, generated summaries,
  documentation, rule catalog material, validation summaries, reports, or demo
  artifacts.
- [ ] Verify every linked route resolves in generated output before adding it.
- [ ] Record moved, unresolved, or unavailable candidate routes in
  `implementation-state.md` instead of linking to them.
- [ ] Ensure no page copy, metadata, alt text, captions, discovery output, or
  generated output publishes raw source snippets, raw SQL, config values,
  connection strings, secrets, local absolute paths, raw remotes, generated
  scan directories, private sample names, raw facts, raw SQLite indexes,
  analyzer logs, raw service addresses, raw endpoints, customer data,
  production identifiers, hidden validation details, or hidden capability
  counts.
- [ ] Add stable section anchors for reader questions, evidence map, coverage
  gaps, hidden material, proof paths, and non-claims if a standalone route is
  implemented.
- [ ] If the content is implemented as a section on an existing page, ensure
  the host page title, description, social metadata, sitemap entry, and
  discovery metadata remain concept-level and do not imply a shipped
  modernization, runtime, migration, AI, or complete coverage feature.
- [ ] If implemented as a standalone route, add page title, description,
  canonical URL, Open Graph fields, sitemap metadata, and discovery metadata.
- [ ] Ensure discovery metadata and any bot/LLM-oriented discovery entry
  preserve the concept claim level and the non-claims.
- [ ] Add safe cross-links from relevant public pages only where the link text
  reinforces static evidence boundaries and does not imply runtime proof,
  migration safety, or complete legacy coverage.
- [ ] Before adding any inbound cross-link from an existing page, verify both
  the target route and the host page's generated output resolve correctly.
  Record deferred or unresolved inbound links in `implementation-state.md`
  rather than adding placeholder anchors or commented-out links.
- [ ] Link to relevant public-safe surfaces available at implementation time,
  such as `/capabilities/`, `/limitations/`, `/validation/`, `/proof-paths/`,
  `/demo/result/`, `/demo/proof-upgrades/`, `/legacy-evidence/`,
  `/legacy-validation/`, `/manager-packet/`, `/claims/` or `/claim-ledger/`,
  and any generated adoption page.
- [ ] Add focused validation for required claim-level text, shared principle,
  reader questions, evidence-map rows, proof paths, route metadata, dev-only
  labeling, hidden-material boundaries, forbidden overclaims, and forbidden
  private/raw material.
- [ ] Add focused validation that rendered copy avoids unsupported `impacted`
  wording unless a reducer-backed result with public-safe evidence supports it.
- [ ] Wire focused validation into the existing site validation and test
  workflow rather than relying only on manual review.
- [ ] Run `git diff --check`.
- [ ] Run `git diff --cached --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`, or record the exact gap in
  `implementation-state.md` only if the validator no longer exists at
  implementation time.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [ ] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, partial-work
  labels if applicable, and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, analyzer log content, raw runtime
  telemetry, raw SQL, raw config values, generated scan directories, and
  private sample names.
