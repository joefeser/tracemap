# Site TraceMap Tools Legacy .NET Evidence Lane Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future `tracemap.tools` content or page lane for legacy .NET evidence.
The lane should explain how deterministic static evidence can orient
modernization review for WCF, ASMX/SOAP, .NET Remoting, WebForms, WinForms,
legacy data metadata, project and toolchain diagnostics, and migration-planning
questions.

This is a spec-only site packet. It does not implement site source, generated
site output, scanner behavior, reducer behavior, validation scripts, navigation,
sitemap metadata, runtime proof, production telemetry, AI or LLM impact
analysis, embeddings, vector databases, or prompt-based classification.

The future lane must stay honest about the target branch and public proof
state. It must not claim shipped scanner coverage for any legacy .NET surface
unless that exact claim is true on the relevant branch and backed by a
public-safe proof path.

## Shared Site Principle

No public conclusion without evidence.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Claim Level Rationale

The future lane starts at `Public claim level: concept` because it explains how
a public page could organize reviewer questions and evidence statuses. It does
not create new scanner coverage, publish a demo result, prove migration
readiness, or certify any legacy .NET system.

The lane may use framework-family names such as WCF, ASMX/SOAP, .NET Remoting,
WebForms, WinForms, and legacy data metadata as future evidence surfaces, but
each row must carry its own evidence status and proof requirement. A concept
page does not upgrade a hidden, future, demo-only, or dev-only capability into a
shipped public claim.

## Target Branch Boundary

The implementation target for this packet is `dev`.

Future implementation must distinguish:

- `main`: use `shipped` only for wording that is true on `main` and backed by a
  public-safe proof path.
- `dev`: use `dev` or `dev-only` only for wording that is true on the target
  branch and not yet promoted to `main`; never imply shipped main coverage.
- `demo`: use only for checked-in public-safe demo evidence with a stable proof
  path and limitation text.
- `future`: use for concept, planned, or reviewer-question framing that does
  not assert support.
- `hidden`: use when capability details, validation results, private examples,
  or proof material are not public-safe.

If branch or proof status is uncertain, the row must be `future`, `hidden`, or
omitted, not `shipped`, `demo`, or `dev`.

## Relationship To Adjacent Site Surfaces

The legacy .NET evidence lane answers: "Which legacy .NET surfaces could a
reviewer ask about, what evidence status is public-safe, what proof path is
required, and what limitation travels with each row?"

It must remain distinct from adjacent surfaces when present:

- `/legacy-evidence/`: canonical legacy evidence story and theme claim ledger.
  This lane may link to it for status reconciliation, but must not override it.
- `/legacy-modernization/evidence-map/`: broader modernization evidence map.
  This lane is a focused legacy .NET lane, not a replacement.
- `/legacy-validation/`: validation planning surface. This lane may point to
  validation needs, but must not imply validation has passed.
- `/capabilities/`: capability maturity overview. This lane does not become the
  source of truth for shipped features.
- `/limitations/`: canonical public non-claims. This lane must link or mirror
  the relevant limits without weakening them.
- `/validation/`: public validation route. This lane may require proof links to
  it, but does not certify validation success.
- `/manager-packet/`: manager summary. This lane supplies evidence status rows,
  not executive assurance.
- `/review-claim-checklist/` or a claim-ledger equivalent: claim-governance
  surface. This lane must align with it.

If a route has moved or is unavailable at implementation time, the future
implementation must link the closest public-safe equivalent or record the
substitution, omission, or deferral in `implementation-state.md`.

## Requirements

### Requirement 1: Publish a bounded legacy .NET evidence lane

The future implementation shall add a concept-level public page or section that
orients readers to legacy .NET static evidence surfaces.

Acceptance criteria:

- The rendered lane says `Public claim level: concept`.
- The rendered lane says `No public conclusion without evidence`.
- The rendered lane says TraceMap works from repository snapshots and
  checked-in artifacts, not runtime traffic, production telemetry, live system
  inspection, or migration execution.
- The rendered lane identifies itself as a future evidence-status lane, not a
  shipped scanner coverage page.
- The implementation chooses a final placement such as
  `/legacy-dotnet/evidence/`, `/legacy-modernization/dotnet-evidence/`, a
  section on an existing modernization page, or a recorded equivalent.
- The implementation records the chosen placement, rejected alternatives, and
  why the placement does not imply shipped legacy .NET coverage in
  `implementation-state.md`.
- The lane stays out of primary navigation unless a future information
  architecture review explicitly records why a concept-level lane belongs
  there.
- If standalone, title, description, canonical URL, Open Graph metadata,
  sitemap metadata, and discovery metadata preserve concept-level wording.
- If implemented as a section, the host page metadata and discovery entry do
  not imply a stronger claim level than the lane supports.
- The lane introduces no upload flow, runtime collector, production telemetry,
  local scanner invocation, AI workflow, LLM workflow, embedding workflow,
  vector database, generated-artifact dependency, or release gate.

### Requirement 2: Include an evidence-status matrix

The future lane shall include a scannable evidence-status matrix for legacy
.NET surfaces.

Acceptance criteria:

- The matrix includes columns or equivalent fields for surface, reviewer
  question, evidence shape, evidence status, proof path required, limitation,
  allowed wording, and forbidden wording.
- The evidence status vocabulary includes `shipped`, `demo`, `dev` or
  `dev-only`, `future`, and `hidden`.
- Every matrix row has a proof path requirement, even when the current proof
  path is `not public-safe yet`, `future proof required`, or `hidden pending
  validation`.
- Every matrix row has limitation text adjacent to the status.
- Rows labeled `shipped` require public-safe proof that the exact wording is
  true on `main`.
- Rows labeled `demo` require checked-in public-safe demo artifacts, generated
  summaries, or validation output that support the exact public wording.
- Rows labeled `dev` or `dev-only` require proof that the exact wording is true
  on `dev` and must not imply main availability.
- Rows labeled `future` may describe reviewer questions and evidence shapes,
  but must not assert detection, coverage, reducer output, or shipped support.
- Rows labeled `hidden` may name a public framework family only with adjacent
  hidden status and limitation text. They must not publish hidden results,
  counts, validation cadence, private sample names, raw values, or unreleased
  sequencing.
- The implementation rechecks the adjacent legacy evidence story claim ledger
  before assigning row statuses.
- Any row whose sibling ledger status is hidden remains hidden unless the same
  implementation change updates the ledger with public-safe proof.
- Any required surface absent from the sibling ledger defaults to `hidden` or
  `future` until a ledger update adds public-safe proof and limitations.

### Requirement 3: Cover required legacy .NET surfaces

The matrix and surrounding copy shall cover the required legacy .NET evidence
surfaces as deterministic static evidence surfaces.

Acceptance criteria:

- Required surface families are included as row groups at the evidence status
  supported by public proof. A family or sub-surface row may be collapsed to a
  single family-level row, marked `future` or `hidden`, or omitted when listing
  its sub-surfaces would disclose a hidden capability inventory, hidden scope,
  or implied count of hidden legacy work. Any collapse or omission and the
  reason are recorded in `implementation-state.md`.
- The matrix must not enumerate hidden sub-surfaces at a granularity that
  amounts to a hidden capability count, validation cadence, or unreleased scope.
- Include WCF rows or row groups covering service hosts, service references,
  service contracts, operation metadata, binding or endpoint metadata, and WCF
  metadata normalization only at the evidence status supported by public proof.
- Include ASMX/SOAP rows or row groups covering service declarations, SOAP
  operation metadata, generated proxy clues, and checked-in metadata only at
  the evidence status supported by public proof.
- Include .NET Remoting rows or row groups covering remoting API references,
  channel registration clues, marshal-by-reference type clues, and remoting
  configuration only at the evidence status supported by public proof.
- Include WebForms rows or row groups covering markup event bindings,
  code-behind handlers, designer-field clues, route/navigation clues, and
  postback-related review questions only at the evidence status supported by
  public proof.
- Include WinForms rows or row groups covering form/control metadata,
  designer-file clues, event-handler references, navigation or launch clues,
  and UI-to-backend review questions only at the evidence status supported by
  public proof.
- Include legacy data metadata rows or row groups covering DBML, EDMX, typed
  DataSet, TableAdapter, provider metadata, connection-name metadata, and
  ORM-like mapping clues only at the evidence status supported by public proof.
- Include project and toolchain diagnostics rows or row groups covering target
  framework, project style, SDK or non-SDK project shape, toolset, restore
  clues, package metadata, generated files, project-load failures, build
  failures, and syntax fallback.
- Include modernization review rows or row groups that translate evidence
  statuses into reviewer questions, owner follow-up, proof gaps, and
  migration-planning input.
- The lane distinguishes general evidence-model rows from legacy-surface
  support rows so generic static-evidence principles do not smuggle hidden
  support claims.

### Requirement 4: Preserve deterministic evidence language

The future lane shall use TraceMap's deterministic evidence vocabulary and
avoid unsupported impact language.

Acceptance criteria:

- Allowed vocabulary includes rule ID, evidence tier, coverage label, line
  span, file path, commit SHA, extractor version, limitation, proof path,
  analysis gap, reduced coverage, syntax fallback, public claim level, and
  evidence status.
- The lane says no conclusion is supported without a rule ID, evidence tier,
  limitation, and proof path appropriate to the claim.
- The lane does not say a surface is `impacted` unless a deterministic reducer
  result with public-safe rule IDs, evidence tiers, proof path, and limitation
  supports that exact wording.
- Project-load failure, restore failure, build failure, unsupported project
  type, missing toolchain, generated-file uncertainty, malformed metadata, and
  syntax-only evidence are labeled as reduced coverage or analysis gaps.
- Failed build or failed project load is never described as a clean repository.
- Syntax fallback is described as useful reduced-coverage evidence, not
  compiler-resolved semantic proof.
- Semantic evidence is not implied where only structural, syntax, textual, or
  unknown evidence exists.

### Requirement 5: Preserve public-safe claim boundaries

The future lane shall avoid public claims that outrun static evidence.

Acceptance criteria:

- The lane does not claim runtime behavior, production traffic, deployed
  endpoint existence, endpoint performance, service reachability, UI
  reachability, user workflow execution, outage cause, release approval,
  release safety, operational safety, security posture, exploitability,
  database existence, query execution, schema compatibility, package
  compatibility, migration feasibility, migration completeness, or complete
  product coverage.
- The lane does not imply TraceMap replaces runtime telemetry, source-owner
  review, architecture review, security review, database review, test results,
  build or restore validation, migration planning, release approval, or human
  judgment.
- The lane does not claim AI impact analysis, LLM analysis, prompt-based
  classification, embeddings, vector search, vector databases, probabilistic
  ranking, autonomous approval, or agentic release decisions.
- The lane does not publish raw source snippets, raw SQL, config values,
  connection strings, secrets, tokens, credentials, local paths, raw remotes,
  generated scan directories, raw facts, raw SQLite indexes, raw analyzer
  logs, raw service addresses, raw endpoint values, raw database identifiers,
  private sample names, customer names, production identifiers, hidden
  validation details, hidden capability counts, or unreleased sequencing.
- Public copy may name artifact types such as `facts.ndjson`, `index.sqlite`,
  `scan-manifest.json`, `report.md`, and analyzer logs only as output
  categories, not as raw contents or paths.
- Public examples use sanitized labels, rule IDs, evidence tiers, coverage
  labels, public-safe proof links, snippet hashes, and limitations.

### Requirement 6: Define navigation, metadata, and discovery expectations

The future implementation shall make the lane discoverable without inflating
claim maturity.

Acceptance criteria:

- Standalone implementation adds stable anchors for overview, evidence-status
  matrix, surface families, modernization review, proof paths, limitations, and
  non-claims.
- Section implementation adds stable anchors within the host page and validates
  that anchor IDs are unique.
- Metadata uses concept-level wording and avoids shipped feature, runtime,
  migration-safety, AI, or complete legacy coverage language.
- Discovery metadata includes the public claim level, branch-status boundary,
  proof-path requirement, and non-claims.
- Candidate cross-links are added only after verifying generated routes exist.
- Candidate inbound links may come from legacy evidence, modernization,
  limitations, validation, capabilities, proof-path, manager-packet, roadmap,
  or claim-checklist pages if the link text preserves static evidence
  boundaries.
- Missing, moved, or deferred links are recorded in `implementation-state.md`
  rather than added as placeholder anchors or commented-out links.

### Requirement 7: Add validation for future implementation

The future implementation shall include focused validation before marking the
lane implemented.

Acceptance criteria:

- Validation checks visible `Public claim level: concept` and visible
  `No public conclusion without evidence`.
- Validation checks required matrix statuses, required row fields, required
  surface families, proof-path requirements, and adjacent limitation text.
- Validation checks that `shipped`, `demo`, and `dev` rows include qualifying
  proof status and do not overstate branch availability.
- Validation checks forbidden runtime, production, migration-safety, AI/LLM,
  complete coverage, and unsupported `impacted` wording.
- Validation checks forbidden raw/private material and generated-artifact
  leakage.
- Validation checks metadata, sitemap/discovery output, route anchors, and
  cross-links.
- Validation includes desktop and mobile browser sanity checks for layout or
  interaction changes.
- Validation includes `npm test`, `npm run validate`, and `npm run build` from
  `site/`, or records the exact unavailable command and replacement.
- Repository-level validation includes `git diff --check` and
  `./scripts/check-private-paths.sh`.
