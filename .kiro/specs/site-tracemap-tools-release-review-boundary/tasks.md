# Site TraceMap Tools Release Review Boundary Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These implementation tasks are checked only after the corresponding future
site change or validation has completed. Keep them unchecked during the
spec-only phase.

- [x] Confirm spec review passed and all Medium or higher findings are
  resolved before beginning implementation.
- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, route or placement decision, validation results, review findings,
  oddities, and follow-up items before changing site code.
- [x] Before changing site source, choose the future page placement:
  standalone
  `/release-review-boundary/`, standalone `/review-room/release-boundary/`,
  section on `/limitations/`, section on `/static-vs-runtime/`, or a recorded
  equivalent if site information architecture has changed.
- [x] Before changing site source, record the selected placement, rejected
  alternatives, and rationale in `implementation-state.md`.
- [x] Confirm the selected placement does not replace `/limitations/`,
  `/static-vs-runtime/`, `/review-claim-checklist/`, `/deploy-audit/`,
  `/validation/`, `/manager-packet/`, or `/questions/objections/`.
- [x] Confirm the page or section is not added to primary navigation unless a
  future site information-architecture review explicitly chooses that
  placement and records the rationale in `implementation-state.md`.
- [x] Add the concept-level page or section using existing static-site layout,
  typography, metadata, accessibility, and validation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Add a section explaining what static evidence can contribute before or
  during release review.
- [x] Add a section explaining what release review still owns.
- [x] Include the required release-boundary row `changed source surface`.
- [x] Include the required release-boundary row `package/config surface`.
- [x] Include the required release-boundary row `route/endpoint adjacency`.
- [x] Include the required release-boundary row `SQL/data surface`.
- [x] Include the required release-boundary row `coverage gap`.
- [x] Include the required release-boundary row `validation evidence`.
- [x] Include the required release-boundary row `runtime telemetry need`.
- [x] Include the required release-boundary row `release-owner decision`.
- [x] Ensure each release-boundary row includes release-review question,
  TraceMap contribution, evidence needed, boundary or non-claim, stop
  condition, required next owner, public claim level, and supporting route.
- [x] Use `concept` for row-level public claim level unless exact public-safe
  evidence supports a stronger claim and the decision is recorded in
  `implementation-state.md`.
- [x] Use bounded TraceMap contribution language such as `can orient`,
  `can show static evidence when available`, `can label a gap`, `can route a
  follow-up`, or `cannot own this decision`.
- [x] Use public role categories for required next owner values, not private
  people or private team names.
- [x] Preserve row semantics for changed source surfaces: static evidence can
  orient review questions but does not approve the change or replace source
  review.
- [x] Preserve row semantics for package/config surfaces: static evidence does
  not prove runtime configuration, deployment settings, secrets handling,
  environment parity, or production behavior.
- [x] Preserve row semantics for route/endpoint adjacency: static evidence
  does not prove live traffic, endpoint performance, request behavior,
  production reachability, or service safety.
- [x] Preserve row semantics for SQL/data surfaces: static evidence does not
  publish raw SQL, prove data migration safety, prove data correctness, or
  replace service-owner, security-owner, or release-owner review.
- [x] Preserve row semantics for coverage gaps: reduced, partial, failed,
  syntax-only, unavailable, private-only, or future-only coverage remains a
  visible gap and never proves no impact or complete coverage.
- [x] Preserve row semantics for validation evidence: validation can inform
  review but does not prove release safety, operational safety, deployment
  success, runtime behavior, or test sufficiency.
- [x] Preserve row semantics for runtime telemetry needs: runtime questions
  require runtime evidence and service-owner or runtime-owner interpretation.
- [x] Preserve row semantics for release-owner decisions: TraceMap cannot
  approve, block, certify, guarantee, or replace release controls or human
  judgment.
- [x] Include stable anchors for every required row and major section.
- [x] Add a `Forbidden claims` section or equivalent.
- [x] Add a `Safe wording` section or equivalent.
- [x] Add a `Stop conditions` section or equivalent.
- [x] Add a `Required next owners` section or equivalent.
- [x] Add a `Non-claims` section or equivalent.
- [x] Add explicit non-claims for release approval, release safety,
  operational safety, production proof, runtime behavior proof, endpoint
  performance proof, deployment success proof, absence-of-impact proof,
  complete coverage, AI/LLM impact analysis, replacement of release controls,
  and replacement of human judgment.
- [x] Ensure copy states TraceMap does not replace tests, code review, source
  review, runtime observability, service-owner review, security review,
  release owners, release controls, or human judgment.
- [x] Avoid saying a surface is impacted unless a reducer-backed result with
  public-safe evidence supports the wording.
- [x] Avoid blame language around teams, services, vendors, consultants,
  reviewers, owners, release managers, test owners, or code quality.
- [x] Verify supporting routes resolve in generated output before linking.
- [x] Record route substitutions, deferrals, or unavailable candidate routes
  in `implementation-state.md`.
- [x] Link only to public-safe pages, public-safe generated summaries,
  documentation, validation pages, limitation pages, demo pages, or proof-path
  pages.
- [x] Do not link directly to raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output,
  hidden validation details, or credential-like values.
- [x] Keep artifact names as private-material boundary examples or public-safe
  source categories only, not public proof content.
- [x] Use only synthetic or already-public/demo-sourced illustrative examples,
  label them as examples, and avoid private or in-flight material, real
  internal reviewer or owner identities, real internal review dates, counts,
  cadence, sequencing, source snippets, SQL, config values, runtime telemetry,
  and production identifiers.
- [x] If implemented as a standalone route, add page title, description,
  canonical URL, Open Graph metadata, sitemap metadata, route index metadata,
  and discovery metadata with `publicClaimLevel: concept`.
- [x] If implemented as a standalone route, add at least one inbound link from
  a relevant live governance, proof, limitations, review-room, validation, or
  questions surface.
- [x] If implemented as a section, add a stable in-page anchor and verify the
  host page title, description, social metadata, sitemap entry, and discovery
  metadata remain concept-level or more conservative.
- [x] Add safe cross-links only where they help readers choose the correct
  evidence surface and do not imply a new proof, safety, release-approval, or
  release-control claim.
- [x] Add focused validation for visible `Public claim level: concept`.
- [x] Add focused validation for visible `No public conclusion without
  evidence`.
- [x] Add focused validation for every required release-boundary row.
- [x] Add focused validation for required row fields.
- [x] Add focused validation for supporting link resolution and recorded route
  substitutions or deferrals, failing when a supporting route is dead and no
  substitution, deferral, or blocking rationale is recorded.
- [x] Add focused validation for standalone metadata, sitemap metadata, and
  discovery metadata when standalone.
- [x] Add focused validation for section anchor and host-page metadata when
  sectioned.
- [x] Add focused validation for forbidden release approval, release safety,
  operational safety, production proof, runtime behavior proof, endpoint
  performance proof, deployment success proof, absence-of-impact proof,
  complete coverage, AI/LLM impact analysis, replacement of release controls,
  and replacement of human judgment in rendered text, decoded HTML, raw HTML
  attributes, metadata, sitemap output, discovery output, tests, fixtures, and
  generated pages, while allowing explicit non-claim, forbidden-claim,
  safe-wording, stop-condition, row-boundary, or objection-boundary contexts.
- [x] Add focused validation for forbidden private/raw material and
  credential-like values in rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap output, discovery output, tests, fixtures, and generated
  pages.
- [x] Add focused validation for visible word count bounds of 900 to 2,400
  words unless amended in `implementation-state.md`, counting rendered body
  prose and release-boundary row cell content while excluding page-level
  navigation, breadcrumbs, site headers, site footers, metadata blocks, and
  all release-boundary matrix column header row cells. Row names and data-cell
  values still count as visible content.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for layout, table,
  responsive, or interaction changes, using existing site browser-check
  patterns when available or recording at least one wide desktop viewport and
  one narrow mobile viewport.
- [ ] Update `implementation-state.md` with final placement decisions,
  substitutions, validation results, review findings, claim-boundary
  decisions, oddities, PR-loop outcomes, residual risk, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw
  remotes, secrets, raw facts, raw SQLite index paths, analyzer log content,
  raw runtime telemetry, generated scan directories, and private sample names.
