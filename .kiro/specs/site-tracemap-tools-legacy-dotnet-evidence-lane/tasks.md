# Site TraceMap Tools Legacy .NET Evidence Lane Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks track future implementation of the legacy .NET evidence lane. This
packet is spec-only; implementation tasks remain unchecked until a later site
implementation phase.

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
- [x] Update all packet files from `Readiness: spec-review` to
  `Readiness: ready-for-implementation` only after review findings, including
  the re-review pass, are patched or explicitly dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the spec-only diff is limited to
  `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/`.

## Future Implementation

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  target, scope, placement decision, public claim level, validation results,
  review findings, oddities, and follow-up items before changing site code.
- [ ] Choose the final lane placement, such as `/legacy-dotnet/evidence/`,
  `/legacy-modernization/dotnet-evidence/`, a section on an existing page, or
  a recorded equivalent.
- [ ] Record rejected placement alternatives and why the chosen placement does
  not imply shipped legacy .NET coverage.
- [ ] Confirm the lane is not added to primary navigation unless a future
  information architecture review records the rationale.
- [ ] Recheck the target branch and current public legacy evidence story claim
  ledger before assigning evidence-status rows.
- [ ] Record every row promotion, omission, hidden default, and proof-path gap
  in `implementation-state.md`.
- [ ] Add the concept-level page or section using existing static-site layout,
  typography, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept`.
- [ ] Include visible `No public conclusion without evidence`.
- [ ] Explain that TraceMap works from repository snapshots and checked-in
  artifacts, not runtime traffic, production telemetry, live inspection, or
  migration execution.
- [ ] Add the evidence-status matrix with surface, reviewer question, evidence
  shape, status, proof path required, limitation, allowed wording, and
  forbidden wording.
- [ ] Include `shipped`, `demo`, `dev` or `dev-only`, `future`, and `hidden`
  in the status vocabulary.
- [ ] Require public-safe proof that exact wording is true on `main` before
  any row is labeled `shipped`.
- [ ] Require checked-in public-safe demo proof before any row is labeled
  `demo`.
- [ ] Require target-branch proof and explicit non-main wording before any row
  is labeled `dev` or `dev-only`.
- [ ] Keep unsupported or uncertain rows at `future`, `hidden`, or omitted.
- [ ] Include WCF rows for service hosts, service references, service
  contracts, operation metadata, binding or endpoint metadata, generated
  clients, and metadata normalization only at the status supported by proof.
- [ ] Include ASMX/SOAP rows for service declarations, SOAP operation metadata,
  generated proxy clues, and checked-in metadata only at the status supported
  by proof.
- [ ] Include .NET Remoting rows for remoting API references, channel
  registration clues, marshal-by-reference type clues, and remoting
  configuration only at the status supported by proof.
- [ ] Include WebForms rows for markup event bindings, code-behind handlers,
  designer-field clues, route/navigation clues, and postback-related review
  questions only at the status supported by proof.
- [ ] Include WinForms rows for form/control metadata, designer-file clues,
  event-handler references, launch or navigation clues, and UI-to-backend
  review questions only at the status supported by proof.
- [ ] Include legacy data metadata rows for DBML, EDMX, typed DataSet,
  TableAdapter, provider metadata, connection-name metadata, and ORM-like
  mapping clues only at the status supported by proof.
- [ ] Include project and toolchain diagnostics rows for target framework,
  project style, SDK or non-SDK shape, toolset, restore clues, package
  metadata, generated files, project-load failures, build failures, syntax
  fallback, and analysis gaps.
- [ ] Include modernization review rows for planning questions, owner
  follow-up, proof gaps, review sequencing, and limitation language.
- [ ] Keep general evidence-model rows separate from legacy-surface support
  rows so generic static-evidence wording does not promote hidden capabilities.
- [ ] Explain semantic, structural, syntax/textual, and unknown evidence tiers.
- [ ] Explain that failed build or failed project load means reduced coverage,
  not a clean repository.
- [ ] Explain syntax fallback as useful reduced-coverage evidence, not
  compiler-resolved semantic proof.
- [ ] Avoid `impacted` wording unless deterministic reducer output with
  public-safe evidence supports the exact claim.
- [ ] Add non-claims for runtime behavior, production traffic, deployed
  endpoint existence, endpoint performance, service reachability, UI
  reachability, user workflow execution, outage cause, release approval,
  release safety, operational safety, security posture, exploitability,
  database existence, query execution, schema compatibility, package
  compatibility, migration feasibility, migration completeness, AI/LLM
  analysis, and complete product coverage.
- [ ] Ensure copy does not imply TraceMap replaces runtime telemetry,
  source-owner review, architecture review, security review, database review,
  test results, build or restore validation, migration planning, release
  approval, or human judgment.
- [ ] Ensure page copy, metadata, alt text, discovery output, generated output,
  and validation fixtures do not publish raw source snippets, raw SQL, config
  values, connection strings, secrets, local paths, raw remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, analyzer
  logs, raw service addresses, raw endpoint values, production identifiers,
  hidden validation details, hidden capability counts, or unreleased
  sequencing.
- [ ] Add stable anchors for overview, branch boundary, evidence-status
  matrix, surface families, modernization review, proof paths, limitations, and
  non-claims when standalone.
- [ ] If implemented as a section, ensure host metadata and discovery output
  remain concept-level and anchor IDs are unique.
- [ ] Add concept-level title, description, canonical URL, Open Graph metadata,
  sitemap metadata, and discovery metadata if standalone.
- [ ] Add safe cross-links only after verifying target routes exist in
  generated output.
- [ ] Re-verify every listed adjacent route at implementation time before
  linking, and record substitutions, omissions, or deferrals in
  `implementation-state.md`.
- [ ] Record missing, moved, or deferred links in `implementation-state.md`.
- [ ] Add focused validation for required claim-level text, matrix statuses,
  required row fields, row families, proof paths, limitations, branch-status
  wording, metadata, route anchors, cross-links, forbidden claims, forbidden
  private/raw material, and unsupported `impacted` wording.
- [ ] Wire focused validation into the existing site validation and test
  workflow.
- [ ] Run `git diff --check`.
- [ ] Run `git diff --cached --check` after staging.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`, or record the exact gap if the
  validator no longer exists.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [ ] Update `implementation-state.md` with placement decisions, validation
  results, review findings, claim-boundary decisions, oddities, partial-work
  labels if applicable, and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, analyzer log content, raw runtime
  telemetry, raw SQL, raw config values, generated scan directories, and
  private sample names.
