# Site TraceMap Tools Legacy .NET Evidence Lane Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Surface

The future implementation should add a concept-level public page or section
focused on legacy .NET evidence status. A standalone route such as
`/legacy-dotnet/evidence/` or `/legacy-modernization/dotnet-evidence/` is the
preferred starting point, but implementation may choose a section on an
existing modernization, legacy evidence, limitations, or validation page if
that keeps the site information architecture clearer.

The selected placement must be recorded in this spec's
`implementation-state.md`, including rejected alternatives and why the chosen
placement does not imply shipped legacy .NET scanner coverage.

## Content Model

The lane is a status-and-evidence explainer, not a product feature page. It
should organize public-safe content around reviewer questions:

- Which legacy .NET surface is being discussed?
- What static evidence shape would support a public statement?
- Is the row `shipped`, `demo`, `dev`, `future`, or `hidden`?
- Which proof path is required before the wording can be published?
- What limitation must remain adjacent to the row?
- What branch does the wording apply to?
- What cannot be concluded from this evidence?

## Recommended Sections

1. Opening: identify the lane as concept-level and show
   `Public claim level: concept`.
2. Branch and proof boundary: explain main, dev, demo, future, and hidden
   status handling.
3. Evidence-status matrix: surface, reviewer question, evidence shape, status,
   proof path required, limitation, allowed wording, and forbidden wording.
4. Surface families: WCF, ASMX/SOAP, .NET Remoting, WebForms, WinForms, legacy
   data metadata, project and toolchain diagnostics, and modernization review.
5. Coverage model: explain semantic, structural, syntax/textual, and unknown
   evidence tiers without implying stronger proof than exists.
6. Modernization review use: show how a reviewer uses the matrix as planning
   input, owner follow-up, or a proof-gap handoff.
7. Hidden until proof exists: explain private or unsanitized material at an
   abstract level only.
8. Non-claims: list runtime, production, migration, operational, security,
   database, package, AI/LLM, and product-completeness boundaries.
9. Proof paths and validation: link only to verified public-safe routes and
   record deferred links in implementation state.

## Evidence-Status Matrix Concept

The matrix is the core of the lane. Each row should be a compact review object:

| Field | Purpose |
| --- | --- |
| Surface | Names the legacy .NET surface family without implying support. |
| Reviewer question | States the review question the evidence can help frame. |
| Evidence shape | Names deterministic artifacts such as rule IDs, tiers, metadata, or coverage labels. |
| Status | One of `shipped`, `demo`, `dev` or `dev-only`, `future`, or `hidden`. |
| Proof path required | Names the public-safe proof needed for the row's exact wording. |
| Limitation | States what cannot be concluded. |
| Allowed wording | Gives safe public phrasing. |
| Forbidden wording | Gives wording the implementation must reject. |

Status rules:

- `shipped`: exact wording is true on `main` and linked to public-safe proof.
- `demo`: exact wording is backed by checked-in public-safe demo evidence, but
  not necessarily a general shipped capability.
- `dev` or `dev-only`: exact wording is true on the target branch and
  explicitly not a main claim.
- `future`: reviewer-question or evidence-shape framing only; no support claim.
- `hidden`: theme may be named only with adjacent hidden status and limitation,
  or omitted if even naming the row would leak private context.

The future implementation should prefer sparse honest rows over filling the
matrix with aspirational support language. A row with no proof path is still
useful when it clearly says what proof is missing and what cannot be claimed.

General static-evidence-model rows and legacy-surface support rows must be
visually and structurally separated. The implementation may use two distinct
tables or sections, or an explicit row-type field with validation, but a
general evidence-model row for tiers, syntax fallback, reduced coverage, or
analysis gaps must never read as a legacy-surface support claim. Each
legacy-surface row carries its own status, proof path, and limitation
independent of the general-model rows.

## Required Row Families

WCF rows should cover service hosts, service references, service contracts,
operation metadata, binding or endpoint metadata, generated clients, and WCF
metadata normalization only at the status supported by public proof.

ASMX/SOAP rows should cover service declarations, SOAP operation metadata,
generated proxy clues, and checked-in metadata only at the status supported by
public proof.

.NET Remoting rows should cover remoting API references, channel registration
clues, marshal-by-reference type clues, and remoting configuration only at the
status supported by public proof.

WebForms rows should cover markup event bindings, code-behind handlers,
designer fields, route/navigation clues, and postback-related review questions
only at the status supported by public proof.

WinForms rows should cover form/control metadata, designer-file clues,
event-handler references, launch or navigation clues, and UI-to-backend review
questions only at the status supported by public proof.

Legacy data metadata rows should cover DBML, EDMX, typed DataSet,
TableAdapter, provider metadata, connection-name metadata, and ORM-like mapping
clues without claiming database existence, query execution, schema
compatibility, or production usage.

Project and toolchain diagnostics rows should cover target framework, project
style, SDK or non-SDK project shape, toolset, restore clues, package metadata,
generated files, project-load failures, build failures, syntax fallback, and
analysis gaps. These rows must say failed build or failed project load means
reduced coverage, not a clean repository.

Modernization review rows should translate evidence status into planning
questions, owner follow-up, proof gaps, review sequencing, and limitation
language. They must not imply migration approval or complete modernization
coverage.

## Ledger And Branch Reconciliation

Before publishing, implementation must recheck the public legacy evidence story
claim ledger and the target branch state. The current packet starts from the
existing public posture where WCF/service-reference mapping, WCF metadata
normalization, .NET Remoting detection, WebForms event flow, legacy data
metadata, build diagnostics, and flow composition reporting are hidden pending
validation. Required surfaces not represented in that ledger, including
ASMX/SOAP and WinForms at the time this packet was written, default to hidden
or future until public-safe proof and a ledger entry exist.

The implementation may not promote any row based only on private validation,
internal sample results, branch hopes, or a spec name. Promotion requires a
public-safe proof path for the exact wording and an implementation-state entry
recording why the status is justified.

## Visual And Interaction Guidance

- Use existing static site layout, typography, table, link, metadata, and
  validation patterns.
- Keep the matrix dense and scannable. This is a reviewer surface, not a hero
  marketing page.
- Use accessible table semantics where a table is used.
- If rows become cards on mobile, each card must preserve surface, status,
  proof path, and limitation.
- Avoid interactive filters unless the implementation also provides no-script
  content, keyboard support, and focused validation.
- Do not add visible instructions about how to use the site controls.
- Keep public framework names readable, but keep hidden status and limitation
  adjacent to any hidden or future support row.

## Cross-Link Plan

Candidate links, only after verifying generated routes exist:

- `/legacy-evidence/`: canonical legacy theme ledger and story.
- `/legacy-modernization/evidence-map/`: broader modernization evidence map.
- `/legacy-validation/`: legacy validation planning boundary.
- `/capabilities/`: general capability maturity.
- `/limitations/`: public non-claims and static-evidence boundaries.
- `/validation/`: validation evidence.
- `/proof-paths/`: how proof paths are presented. This route name is a
  candidate and must be verified against generated output at implementation
  time.
- `/manager-packet/`: manager-facing companion, when wording remains bounded.
- `/roadmap/`: future proof upgrades or concept status, if present.
- `/review-claim-checklist/`: claim repeat-or-hold guidance.

Do not add the lane to primary navigation unless a later information
architecture review records the rationale. Do not add placeholder anchors,
commented-out links, or links to generated output that is not committed.

## Metadata

If implemented as a standalone route, add or update:

- Page title and description.
- Canonical URL.
- Open Graph metadata.
- Sitemap metadata.
- Discovery metadata using the current site schema.
- Machine-readable or structured `concept` claim level, if the site schema
  supports it.
- Non-claim and proof-path summary fields, if the site schema supports them.

Metadata must not classify the lane as a shipped migration, runtime, AI,
scanner-coverage, or complete legacy modernization feature.

## Safety

The lane must not publish raw source snippets, raw SQL, config values,
connection strings, secrets, tokens, credentials, local paths, raw remotes,
generated scan directories, raw facts, raw SQLite indexes, analyzer log
content, raw service addresses, raw endpoint values, database identifiers,
private sample names, customer names, production identifiers, hidden validation
details, hidden capability counts, or unreleased sequencing.

The lane may summarize public-safe generated reports, checked-in public demo
proof paths, rule IDs, evidence tiers, coverage labels, limitations, extractor
versions, commit context, snippet hashes, and abstract legacy surface families
when those summaries do not leak private material or overstate evidence.

## Validation

Future implementation should add focused validation for:

- Visible `Public claim level: concept`.
- Visible `No public conclusion without evidence`.
- Required matrix statuses and required row fields.
- Required WCF, ASMX/SOAP, Remoting, WebForms, WinForms, legacy data,
  project/toolchain, and modernization review row families.
- Proof path and limitation text per row.
- Branch-accurate `shipped`, `demo`, `dev`, `future`, and `hidden` labels.
- Metadata, sitemap, discovery output, anchors, and generated links.
- Forbidden runtime, production, migration-safety, AI/LLM, complete coverage,
  and unsupported `impacted` claims.
- Forbidden raw/private material.
- Desktop and mobile layout sanity when page layout changes.

The focused checks should be part of the existing site test and validation
workflow rather than a manual-only review.
