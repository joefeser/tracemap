# Site TraceMap Tools Legacy .NET Evidence Lane Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks record the completed legacy .NET evidence lane implementation.
Historical spec-review tasks remain checked as completed; this normalization
does not advance deferred follow-up work.

## Spec Review

- [x] Run Kiro Opus spec review, or record the exact blocker in
  `implementation-state.md`.
- [x] Run Kiro Sonnet spec review, or record the exact blocker in
  `implementation-state.md`.
- [x] Record Sonnet review outcomes (status, findings, patched items,
  dispositioned-without-patch items, and clean artifact path) in
  `implementation-state.md`.
- [x] Patch Medium or higher actionable spec-review findings.
- [x] Patch Low findings only when narrow and safe.
- [x] Run one bounded Kiro re-review because review findings were patched in
  prior passes. Save the review artifact to `.tmp/kiro-reviews/` without
  committing it, and record the re-review status, model, findings, patched
  items, dispositioned-without-patch items, and clean artifact path in
  `implementation-state.md`.
- [x] During the spec-review phase, update packet readiness only after review
  findings, including the re-review pass, are patched or explicitly
  dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the spec-only diff is limited to
  `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/`.

## Completed Implementation

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  target, scope, placement decision, public claim level, validation results,
  review findings, oddities, and follow-up items before changing site code.
- [x] Choose the final lane placement, such as `/legacy-dotnet/evidence/`,
  `/legacy-modernization/dotnet-evidence/`, a section on an existing page, or
  a recorded equivalent.
- [x] Record rejected placement alternatives and why the chosen placement does
  not imply shipped legacy .NET coverage.
- [x] Confirm the lane is not added to primary navigation unless a future
  information architecture review records the rationale.
- [x] Recheck the target branch and current public legacy evidence story claim
  ledger before assigning evidence-status rows.
- [x] Record every row promotion, omission, hidden default, and proof-path gap
  in `implementation-state.md`.
- [x] Add the concept-level page or section using existing static-site layout,
  typography, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] Explain that TraceMap works from repository snapshots and checked-in
  artifacts, not runtime traffic, production telemetry, live inspection, or
  migration execution.
- [x] Add the evidence-status matrix with surface, reviewer question, evidence
  shape, status, proof path required, limitation, allowed wording, and
  forbidden wording.
- [x] Include `shipped`, `demo`, `dev` or `dev-only`, `future`, and `hidden`
  in the status vocabulary.
- [x] Require public-safe proof that exact wording is true on `main` before
  any row is labeled `shipped`.
- [x] Require checked-in public-safe demo proof before any row is labeled
  `demo`.
- [x] Require target-branch proof and explicit non-main wording before any row
  is labeled `dev` or `dev-only`.
- [x] Keep unsupported or uncertain rows at `future`, `hidden`, or omitted.
- [x] Include WCF rows for service hosts, service references, service
  contracts, operation metadata, binding or endpoint metadata, generated
  clients, and metadata normalization only at the status supported by proof.
- [x] Include ASMX/SOAP rows for service declarations, SOAP operation metadata,
  generated proxy clues, and checked-in metadata only at the status supported
  by proof.
- [x] Include .NET Remoting rows for remoting API references, channel
  registration clues, marshal-by-reference type clues, and remoting
  configuration only at the status supported by proof.
- [x] Include WebForms rows for markup event bindings, code-behind handlers,
  designer-field clues, route/navigation clues, and postback-related review
  questions only at the status supported by proof.
- [x] Include WinForms rows for form/control metadata, designer-file clues,
  event-handler references, launch or navigation clues, and UI-to-backend
  review questions only at the status supported by proof.
- [x] Include legacy data metadata rows for DBML, EDMX, typed DataSet,
  TableAdapter, provider metadata, connection-name metadata, and ORM-like
  mapping clues only at the status supported by proof.
- [x] Include project and toolchain diagnostics rows for target framework,
  project style, SDK or non-SDK shape, toolset, restore clues, package
  metadata, generated files, project-load failures, build failures, syntax
  fallback, and analysis gaps.
- [x] Include modernization review rows for planning questions, owner
  follow-up, proof gaps, review sequencing, and limitation language.
- [x] Keep general evidence-model rows separate from legacy-surface support
  rows so generic static-evidence wording does not promote hidden capabilities.
- [x] Explain semantic, structural, syntax/textual, and unknown evidence tiers.
- [x] Explain that failed build or failed project load means reduced coverage,
  not a clean repository.
- [x] Explain syntax fallback as useful reduced-coverage evidence, not
  compiler-resolved semantic proof.
- [x] Avoid `impacted` wording unless deterministic reducer output with
  public-safe evidence supports the exact claim.
- [x] Add non-claims for runtime behavior, production traffic, deployed
  endpoint existence, endpoint performance, service reachability, UI
  reachability, user workflow execution, outage cause, release approval,
  release safety, operational safety, security posture, exploitability,
  database existence, query execution, schema compatibility, package
  compatibility, migration feasibility, migration completeness, AI/LLM
  analysis, and complete product coverage.
- [x] Ensure copy does not imply TraceMap replaces runtime telemetry,
  source-owner review, architecture review, security review, database review,
  test results, build or restore validation, migration planning, release
  approval, or human judgment.
- [x] Ensure page copy, metadata, alt text, discovery output, generated output,
  and validation fixtures do not publish raw source snippets, raw SQL, config
  values, connection strings, secrets, local paths, raw remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, analyzer
  logs, raw service addresses, raw endpoint values, production identifiers,
  hidden validation details, hidden capability counts, or unreleased
  sequencing.
- [x] Add stable anchors for overview, branch boundary, evidence-status
  matrix, surface families, modernization review, proof paths, limitations, and
  non-claims when standalone.
- [x] If implemented as a section, ensure host metadata and discovery output
  remain concept-level and anchor IDs are unique.
- [x] Add concept-level title, description, canonical URL, Open Graph metadata,
  sitemap metadata, and discovery metadata if standalone.
- [x] Add safe cross-links only after verifying target routes exist in
  generated output.
- [x] Re-verify every listed adjacent route at implementation time before
  linking, and record substitutions, omissions, or deferrals in
  `implementation-state.md`.
- [x] Record missing, moved, or deferred links in `implementation-state.md`.
- [x] Add focused validation for required claim-level text, matrix statuses,
  required row fields, row families, proof paths, limitations, branch-status
  wording, metadata, route anchors, cross-links, forbidden claims, forbidden
  private/raw material, and unsupported `impacted` wording.
- [x] Wire focused validation into the existing site validation and test
  workflow.
- [x] Run `git diff --check`.
- [x] Run `git diff --cached --check` after staging.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`, or record the exact gap if the
  validator no longer exists.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [x] Update `implementation-state.md` with placement decisions, validation
  results, review findings, claim-boundary decisions, oddities, partial-work
  labels if applicable, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, analyzer log content, raw runtime
  telemetry, raw SQL, raw config values, generated scan directories, and
  private sample names.
