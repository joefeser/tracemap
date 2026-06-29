# Site TraceMap Tools Legacy Modernization Evidence Map Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Surface

The future implementation should add a concept-level public page or section
that explains how TraceMap organizes static repository evidence for legacy
modernization and migration planning. A standalone route such as
`/legacy-modernization/evidence-map/` is the preferred starting point, but the
implementation may instead choose an existing use-case, adoption, validation, or
limitations page section if that keeps the site information architecture
clearer.

The selected placement must be recorded in this spec's
`implementation-state.md`, including rejected alternatives and why the chosen
placement does not imply a shipped modernization assessment product.

## Content Model

The page is a concept explainer, not a product-completeness page. It should
organize public-safe evidence around reader questions:

- What old framework, project, target framework, package, restore, or toolchain
  clues are visible in checked-in files?
- Did semantic analysis load, or did project load fail and reduce coverage?
- Which facts came from semantic evidence, structural evidence, syntax/text
  fallback, or unknown/gap evidence?
- Which service, UI, navigation, config, and legacy data metadata surfaces have
  public-safe evidence?
- Which rows are demo-backed, dev-only, future/concept, hidden, or omitted?
- What must remain private until sanitized validation exists?

## Recommended Sections

1. Opening: identify the page as a concept-level modernization evidence map and
   state `Public claim level: concept`.
2. Reader questions: group manager, architect, engineer, and reviewer questions.
3. Evidence map: table or responsive row layout with legacy concern, reviewer
   question, evidence shape, public status, limitation, and proof path.
4. Coverage model: explain semantic, structural, syntax/textual, and unknown
   gap evidence without stronger claims than the source supports.
5. Legacy surface families: include old frameworks/toolchains, project load
   failures, syntax fallback, WCF/service references, ASMX/SOAP, remoting,
   WinForms, WebForms, config/project metadata, and legacy data metadata.
6. Hidden until sanitized: name abstract categories of material that remain out
   of public copy until sanitized proof exists.
7. Non-claims: list runtime, production, migration, operational, security,
   database, package, AI/LLM, and product-completeness boundaries.
8. Proof paths: link only to verified public-safe pages available at
   implementation time.

## Evidence Map Row Guidance

Rows should use conservative labels:

- `demo-backed` only for checked-in public-safe proof paths.
- `main` or `shipped` only when the wording is true on main, public-safe, and
  linked inline to an existing public proof path.
- `dev-only` for capabilities that exist only on `dev`.
- `concept` only for the general evidence-model or reviewer-question framing of
  general static-evidence-model rows, such as old frameworks/toolchains,
  project-load/build-as-reduced-coverage, syntax fallback, and config/project
  metadata coverage, or for another surface family that has no hidden
  sibling-ledger entry and is not a required-but-unledgered legacy detection
  theme.
- `hidden` for private or unsanitized legacy validation that should not be
  claimed publicly. Publicly documented framework-family names such as WCF,
  ASMX/SOAP, remoting, WinForms, and WebForms may appear as named `hidden` rows
  with reviewer questions and limitations.
- `omitted` when even abstract mention of the surface or capability would leak
  hidden capability detail or private validation information.

Each row should carry the limitation next to the evidence. For example, a
syntax fallback row may say it can identify checked-in surfaces when semantic
load fails, but it cannot resolve compiler symbols or prove runtime behavior.

Before publish, row labels must be reconciled against the
`site-tracemap-tools-legacy-evidence-story` theme claim ledger as the
authoritative label source, and cross-checked against
`legacy-story-reconciliation` as an internal coexistence reference whose
contents stay hidden. A sibling-ledger `hidden` theme remains hidden here
unless the same change updates that ledger with public-safe proof. At
implementation time, re-check the sibling ledger state rather than relying on
this snapshot. With the current ledger all `hidden`, legacy-surface rows use
`hidden` until that ledger is updated with public-safe proof. Themes absent
from the sibling ledger, currently including WinForms navigation/event surfaces
and ASMX/SOAP services, also default to `hidden` until a future ledger update
adds public-safe proof.

A sparse evidence map is expected and acceptable for this concept-level page.
Do not fill empty space with theme-support language that outruns public-safe
proof.

General evidence-model rows may explain public TraceMap principles such as
reduced coverage, syntax fallback, and static project/config clues, but they
must not imply hidden legacy detection capabilities such as build environment
diagnostics detection or specific WCF/WebForms/WinForms/ASMX support. A
config/project metadata row may describe checked-in config/project files as
reduced-coverage evidence, but it must not assert WCF or
`system.serviceModel` binding detection, service-reference detection, endpoint
extraction, or connection-string extraction while those themes are hidden, and
it must never render raw service addresses, endpoint values, connection
strings, or config values.

## Visual And Interaction Guidance

- Use existing static site layout, typography, table, card, link, boundary, and
  metadata patterns.
- Keep the evidence map dense enough for review work; avoid oversized
  marketing composition.
- Use accessible table semantics where a table is used. If cards are used on
  mobile, preserve the row label, public status, proof path, and limitation in
  each card.
- Keep text compact and scannable for managers while preserving enough
  evidence detail for engineers and reviewers.
- Do not add interactive filters unless implementation also adds validation and
  no-script fallback for the same content.

## Cross-Link Plan

Candidate inbound links, only after verifying routes exist in generated output:

- `/capabilities/`: link near capability maturity or legacy/static evidence
  boundaries.
- `/limitations/`: link near static evidence and non-claim boundaries.
- `/validation/`: link near validation and coverage language.
- `/proof-paths/`: link where public proof paths are explained.
- `/demo/result/` or `/demo/proof-upgrades/`: link only if demo-backed rows
  use those proof surfaces.
- `/legacy-evidence/`: link as the adjacent concept story when it exists in
  generated output.
- `/legacy-validation/`: link as the adjacent legacy validation plan surface
  when it exists in generated output.
- `/manager-packet/`: link as a manager-facing companion if it exists.
- `/claims/` or `/claim-ledger/`: link if a claim-governance page exists.

Do not add the page to primary navigation unless a later information-
architecture review records why a concept-level modernization evidence map
belongs there.

When cross-links are added from existing pages, verify both the target route
and the host page's generated output before committing the inbound link. Record
deferred or unresolved inbound links in `implementation-state.md` rather than
adding placeholder anchors or commented-out links.

## Metadata

If implemented as a standalone route, add or update:

- Page title and description.
- Canonical URL.
- Open Graph metadata.
- Sitemap metadata.
- Discovery metadata using the current site schema.
- Machine-readable `concept` claim level and non-claims.

Discovery metadata must not classify the page as a shipped migration, runtime,
AI, or complete legacy coverage feature.

## Safety

The page must not publish raw source snippets, raw SQL, config values,
connection strings, secrets, local absolute paths, raw remotes, raw
`facts.ndjson`, raw `index.sqlite`, analyzer logs, generated scan directories,
private sample names, raw service addresses, raw endpoints, customer data,
production identifiers, hidden validation details, or hidden capability counts.

The page may summarize public-safe generated reports, checked-in public demo
proof paths, rule IDs, evidence tiers, coverage labels, limitations, and
abstract legacy surface families when those summaries do not leak private
material or overstate evidence.

## Validation

Future implementation should add focused validation for:

- Visible `Public claim level: concept`.
- Required reader question, evidence map, coverage, hidden material, proof
  path, and non-claim sections.
- Public status labels and proof path availability.
- `dev-only` labeling for dev-only capabilities.
- Forbidden raw/private material.
- Forbidden runtime, production, migration-safety, database-existence,
  package-compatibility, exploitability, outage-cause, AI/LLM, and complete
  coverage claims.
- Unsupported `impacted` wording.

The focused checks should be part of the existing site test and validation
workflow rather than a manual-only review.
