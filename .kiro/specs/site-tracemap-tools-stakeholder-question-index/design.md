# Site TraceMap Tools Stakeholder Question Index Design

Status: not-started
Readiness: ready-for-implementation
<!-- Site-code implementation is deferred to a future phase. This spec is
ready for a future implementer to pick up, not a signal to implement on this
spec branch. -->
Public claim level: concept

## Design Purpose

This design is separated from requirements because the future page needs a
clear information architecture: readers start with a question, then follow a
bounded proof path to an existing evidence surface. The design does not
implement site code.

## Placement Options

Preferred starting options:

- `/questions/`: clearest public entry point for readers and agents who arrive
  with a question rather than a TraceMap vocabulary term.
- `/use-cases/questions/`: keeps the page under the use-case tree and may fit
  if `/use-cases/` already groups reader intent.
- Section on `/use-cases/`: lowest route surface area, useful if standalone
  route metadata would make the concept feel more mature than it is.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/manager-packet/`, because this index serves multiple audiences
  and does not provide manager-ready packet copy.
- Replacing `/proof-paths/`, because this index routes to proof trails rather
  than cataloging artifacts, rule IDs, tiers, coverage, and limitations.
- Replacing `/review-claim-checklist/`, because this index asks "where should
  this question go?" while the checklist asks "may this claim be repeated?"
- Adding the page directly to primary navigation before an information-
  architecture review, because concept-level orientation may belong in
  secondary discovery instead.

## Page Model

Recommended sections:

1. Opening: state `Public claim level: concept`, the shared site principle,
   and that the page is an orientation index rather than a proof claim.
2. Question matrix: a compact table or repeated row list keyed by audience and
   question family.
3. How to read a row: define safe answer shape, public claim level, proof
   path, limitation, and non-claim.
4. Route and proof targets: link to currently live public-safe routes only,
   with substitutions recorded in implementation-state.
5. Non-claims and private-material boundary: keep the forbidden operational,
   AI/LLM, complete-coverage, and private-artifact rules visible.
6. Agent/bot guidance: instruct crawlers and automated reviewers to preserve
   proof paths, limitations, non-claims, and claim levels when repeating any
   row.

The matrix may be implemented as a table on wide viewports and stacked rows on
narrow viewports, but the rendered structure must keep the required field
labels accessible to screen readers and validators.

## Required Matrix Rows

| Question family | Primary audience | Example safe question | Preferred target surfaces | Public claim level | Safe answer shape | Proof path | Limitation | Non-claim |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Manager planning | Managers, leads | What public-safe evidence can I bring into planning without overclaiming? | `/manager-packet/`, `/proof-paths/`, `/limitations/` | concept | Inspect manager-facing summaries, proof paths, and limitations. | Public-safe manager packet plus proof-path and limitation routes. | Does not decide staffing, priority, ownership, release readiness, or operational safety. | Does not replace manager judgment or service-owner decisions. |
| Engineer endpoint/change review | Engineers, reviewers | Where can I inspect static evidence around this endpoint or change before review? | `/use-cases/endpoint-review/`, `/demo/result/`, `/validation/` | concept; upgrade to demo only with recorded public-safe evidence per Requirement 4 | Review static endpoint/change evidence and validation boundaries. | Endpoint-review route or demo result with validation links. | Does not prove the change is safe, unsafe, approved, blocked, or production-proven. | Does not replace tests, code review, telemetry, or service-owner review. |
| Incident-adjacent handoff | Incident participants, service owners | What static evidence can I hand to the runtime or incident owner without claiming cause? | `/incident-evidence-handoff/`, `/static-vs-runtime/`, `/limitations/` | concept | Route static context to runtime owners while preserving gaps. | Incident handoff route plus static-versus-runtime and limitation routes. | Does not prove outage cause, timeline, traffic, performance, or runtime behavior. | Does not replace incident command, telemetry, logs, traces, or human judgment. |
| Modernization planning | Architects, modernization planners | What evidence map can help plan modernization questions while preserving gaps? | `/legacy-modernization/evidence-map/`, `/proof-paths/`, `/validation/` | concept | Inspect modernization evidence maps and known gaps. | Modernization evidence map plus proof-path and validation routes. | Does not prove complete migration scope or complete dependency understanding. | Does not replace architects, service owners, tests, or modernization planning. |
| Reviewer claim checking | Reviewers, managers, agents | Can this sentence be repeated, and what proof must stay attached? | `/review-claim-checklist/`, `/proof-paths/`, `/proof-source-catalog/` | concept | Check whether a claim keeps its proof fields and limitations attached. | Claim checklist plus proof-path and proof-source routes. | Does not upgrade a claim by repetition, seniority, or confidence. | Does not allow dropping rule ID/rule family, tier, coverage, proof path, limitation, or non-claim. |
| Demo evaluation | Evaluators, engineers, managers | What does the public demo show, and which limits still apply? | `/demo/result/`, `/proof-paths/`, `/limitations/` | concept or demo if evidence-backed | Compare demo rows with proof paths and limitations. | Demo result plus proof-path and limitation routes. | Does not prove private repo behavior, runtime behavior, release safety, or complete coverage. | Does not turn demo evidence into production proof. |
| Proof-source inspection | Reviewers, bots, engineers | Where does this public claim point for source-of-truth evidence? | `/proof-source-catalog/`, `/proof-paths/`, `/validation/` | concept | Inspect the public-safe source mapping behind a claim. | Proof-source catalog plus proof-path and validation routes. | Does not publish raw facts, SQLite, logs, snippets, SQL, configs, private samples, remotes, or scan dirs. | Does not make local-only artifacts public proof. |
| Agent/bot discovery | Crawlers, automated reviewers | Which routes and metadata should an agent use without dropping proof fields? | sitemap/discovery metadata, `/proof-paths/`, `/validation/`, `/limitations/` | concept | Follow metadata and proof routes while preserving evidence fields. | Sitemap/discovery metadata plus proof-path, validation, and limitation routes. | Does not authorize autonomous approval, claim upgrades, or hidden-detail disclosure. | Does not imply AI impact analysis or permission to repeat claims without limitations. |

Implementation may adjust the target routes to current equivalents, but it
must preserve the question family, audience, safe answer shape, limitation, and
non-claim for every required row.

## Row Field Guidance

- `audience`: one or more reader groups, using public role names rather than
  internal team names.
- `question`: reader-facing, framed as an orientation question.
- `safe answer shape`: describes what the linked surface helps the reader
  inspect or compare.
- `target route`: public-safe route that resolves in generated output.
- `evidence surface`: page, report summary, demo result, validation page,
  limitation page, proof path, proof-source catalog, or discovery metadata.
- `public claim level`: concept by default; demo only with current
  public-safe proof for that row.
- `proof path`: public-safe link chain or route that preserves rule ID or rule
  family, evidence tier, coverage label, limitation, and non-claim when
  available.
- `limitation`: what the row does not know or cannot prove.
- `non-claim`: what the row must not imply.

## Copy Rules

Use static-evidence wording:

- `inspect static evidence`
- `follow the proof path`
- `compare the public claim level`
- `check limitations`
- `route to the evidence surface`
- `record gaps`
- `escalate to the owner`

Avoid unsupported conclusion wording:

- `proves impact`
- `safe to release`
- `root cause`
- `production proven`
- `complete coverage`
- `impacted` unless the proof path links to a reducer-backed public-safe
  output with rule IDs, evidence tiers, coverage labels, and a published
  `demo` or higher claim level
- `AI impact analysis`
- `automated approval`
- `replaces telemetry`

If forbidden terms appear in a non-claims section, validation should allow the
term only inside that explicitly bounded region.

## Validation Design

Focused validation should parse the rendered standalone route or host section
with structured HTML tools rather than brittle full-page string matching where
possible. It should verify:

- page-level claim label and shared principle;
- required row families and required row fields;
- row-level public claim levels;
- target-route and proof-path link resolution;
- candidate route substitutions recorded in implementation-state;
- metadata and discovery claim level;
- forbidden operational, AI/LLM, release-safety, complete-coverage, and
  unsupported `impacted` wording across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests, sitemap/discovery
  output, and bot-oriented discovery surfaces;
- forbidden terms are allowed only inside explicitly bounded non-claim or
  limitation statements, and validation must distinguish claim-denying context
  from claim-asserting context;
- forbidden private/raw material;
- absence of raw scanner artifacts and local-only paths in public output.

The implementation should wire focused validation into the existing aggregate
site validation command so `npm run validate` exercises the question index.
