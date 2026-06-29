# Site TraceMap Tools Release Review Boundary Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Purpose

This design defines the information architecture for a future public-safe
release-review boundary page or section. The future surface should help
release participants understand:

1. what deterministic static evidence can contribute;
2. what release review still owns;
3. what claims are forbidden;
4. what wording is safe;
5. when to stop;
6. which owner must answer next;
7. what TraceMap explicitly does not claim.

The design does not implement site code.

## Placement Options

Preferred candidate options:

- `/release-review-boundary/`: clearest standalone route if the site needs a
  direct release-review reference.
- `/review-room/release-boundary/`: strong fit if implementation wants the
  surface nested under review-room workflows.
- Section on `/limitations/`: useful if the page should remain part of the
  site-wide non-claim boundary.
- Section on `/static-vs-runtime/`: useful if the page should sit beside the
  runtime-telemetry distinction.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/limitations/`, because limitations are site-wide boundaries and
  this page applies them to release-review roles and decisions.
- Replacing `/static-vs-runtime/`, because runtime separation is only one part
  of release review.
- Replacing `/review-claim-checklist/`, because the checklist answers whether
  a claim can be repeated and this surface answers what static evidence can
  contribute to release review.
- Replacing `/deploy-audit/`, because deploy-audit evidence is
  deployment-adjacent and must not become release approval or deployment
  success proof.
- Replacing `/validation/`, because validation evidence is one input and not a
  release decision.
- Replacing `/manager-packet/`, because the manager packet is a broader
  evidence conversation.
- Replacing `/questions/objections/`, because objection handling is broader
  than release-review ownership.
- Primary navigation placement without a recorded information-architecture
  decision, because this concept-level surface may belong in secondary
  discovery.

Future implementation must record the final placement and rejected
alternatives before changing site source.

## Page Model

Recommended sections:

1. Opening boundary: visible `Public claim level: concept`, visible
   `No public conclusion without evidence`, and a short statement that
   TraceMap can contribute static evidence to release review but cannot
   approve or prove a release.
2. What static evidence can contribute: explain changed source surfaces,
   package/config surfaces, route/endpoint adjacency, SQL/data surfaces,
   validation evidence, coverage gaps, and owner handoffs.
3. What release review still owns: list release-owner decision, tests, code
   review, source review, runtime telemetry, service-owner judgment,
   operational readiness, deployment verification, rollback planning, and
   human release controls.
4. Release-boundary matrix: required eight rows with stable anchors and row
   fields.
5. Forbidden claims: release approval, safety, production proof, runtime
   proof, endpoint performance proof, deployment success proof,
   absence-of-impact proof, complete coverage, AI/LLM analysis, and
   replacement claims.
6. Safe wording: bounded phrases that keep static evidence attached to proof,
   limitations, coverage labels, and owners.
7. Stop conditions: missing proof, private-only or raw-only material, hidden
   details, reduced coverage, runtime-dependent questions, release-owned
   decisions, forbidden wording, and confidence without rule evidence.
8. Required next owners: role-category handoffs.
9. Non-claims and private-material boundary: centralized public-safety rules.
10. Supporting routes: verified live routes and recorded substitutions or
    deferrals.

The release-boundary matrix may render as a table on wide viewports and
stacked cards or grouped definition lists on narrow viewports. Row field
labels must remain accessible to screen readers and stable enough for focused
validation.

## Required Release-Boundary Rows

| Row | Release-review question | TraceMap contribution | Evidence needed | Boundary or non-claim | Stop condition | Required next owner | Public claim level | Supporting route |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| changed source surface | What changed in source that release review should inspect? | Can orient static changed-source evidence when a public-safe proof path exists. | Rule ID or rule family, evidence tier, coverage label, file path and line span if public-safe, scan commit, limitation, and proof path. | Does not approve the change, prove the change is safe, or replace source review. | Stop when the question asks whether the release may proceed or whether the change is acceptable. | Code reviewer, service owner, test owner, or release owner. | concept | `/review-claim-checklist/` or `/limitations/` |
| package/config surface | Are package, project, or configuration surfaces near the release change? | Can show static package/config references or gaps when supported. | Public-safe package/project/config evidence, rule family, evidence tier, coverage label, limitation, and proof path. | Does not prove runtime configuration, environment parity, secrets handling, or production behavior. | Stop when the answer requires environment values, deploy settings, secrets, or runtime config. | Build or tooling owner, service owner, security owner, or release owner. | concept | `/limitations/` |
| route/endpoint adjacency | Are routes or endpoints adjacent to the changed surface? | Can orient static route/endpoint adjacency when rules support it. | Route or endpoint evidence surface, rule ID or rule family, evidence tier, coverage label, limitation, and proof path. | Does not prove live traffic, endpoint performance, request behavior, production reachability, or service safety. | Stop when the question needs logs, traces, metrics, traffic, latency, errors, dashboards, or runtime tests. | Runtime observability owner or service owner. | concept | `/static-vs-runtime/` |
| SQL/data surface | Are SQL or data-facing surfaces visible near the release change? | Can identify SQL/data-facing static surfaces or gaps when public-safe proof exists. | Public-safe data-surface summary, rule family, evidence tier, coverage label, limitation, and proof path. | Does not publish raw SQL, prove data migration safety, prove data correctness, or replace data-owner review. | Stop when the answer needs raw SQL, data contents, migration execution, production data behavior, or private schema details. | Service owner, security owner, or release owner. | concept | `/limitations/` |
| coverage gap | Is coverage reduced, partial, unavailable, private-only, or unknown? | Can label a gap and keep it visible. | Coverage label, analysis gap, scan/build status, evidence tier, limitation, and proof path. | Does not prove no impact, no dependency, clean coverage, or absence of risk. | Stop when a gap is being used to strengthen a release claim or hide uncertainty. | TraceMap site owner, build or tooling owner, code reviewer, or service owner. | concept | `/limitations/` |
| validation evidence | What validation evidence exists for public-safe site or demo material? | Can point to validation results as review input when public-safe. | Public-safe validation status, linked validation page or summary, coverage label, limitation, and implementation-state note. | Does not prove release safety, operational safety, deployment success, runtime behavior, or test sufficiency. | Stop when validation is being treated as release approval or production proof. | Test owner, code reviewer, TraceMap site owner, or release owner. | concept | `/validation/` |
| runtime telemetry need | What questions require runtime evidence? | Can route runtime-dependent questions away from static evidence. | Static/runtime boundary, coverage label, limitation, and linked runtime-boundary page. | Does not provide production proof, runtime behavior proof, traffic, endpoint performance, live errors, or operational state. | Stop when the question asks what ran, served traffic, failed, performed, alerted, or behaved in production. | Runtime observability owner, service owner, or release owner. | concept | `/static-vs-runtime/` |
| release-owner decision | Who decides go/no-go, hold, exception, or risk acceptance? | Cannot own this decision; can provide static-evidence input and visible gaps. | Release checklist evidence, tests, code review, runtime evidence, service-owner judgment, validation status, and release-control record. | Does not approve, block, certify, guarantee, or replace release controls or human judgment. | Stop when copy, UI, metadata, or review text implies TraceMap made the release decision. | Release owner. | concept | `/review-room/` or `/questions/objections/` |

Implementation may adjust supporting routes to current public equivalents, but
it must preserve every required row, row field, owner handoff, limitation, and
non-claim.

## Safe Wording

Preferred phrases:

- `TraceMap can orient static evidence for release review.`
- `This is a review input, not release approval.`
- `The evidence suggests a question for the release owner.`
- `Coverage is reduced and must remain visible.`
- `Runtime evidence is required for production behavior.`
- `Release owner decision required.`
- `Owner follow-up needed.`
- `Static adjacency does not prove runtime behavior.`
- `Validation evidence is not release safety proof.`

Avoid phrases unless they appear inside explicit forbidden-claim examples or
non-claim copy:

- `approved`
- `safe`
- `certified`
- `validated for release`
- `production proven`
- `runtime proven`
- `deployment succeeded`
- `no impact`
- `complete coverage`
- `AI impact analysis`
- `release control replacement`

## Forbidden Claims

The future page must not claim or imply:

- TraceMap approves releases or makes go/no-go decisions.
- TraceMap proves release safety or operational safety.
- TraceMap proves production behavior, runtime behavior, live traffic,
  endpoint performance, request behavior, deployment success, or production
  readiness.
- TraceMap proves absence of impact, absence of dependency, absence of risk,
  or complete coverage.
- TraceMap performs AI/LLM impact analysis, prompt-based classification,
  embedding search, or vector database reasoning in the core scanner or
  reducer.
- TraceMap replaces tests, code review, source review, runtime observability,
  service-owner review, security review, release controls, release owners, or
  human judgment.

## Stop Conditions

Release-boundary content must tell readers to stop when:

- proof path is missing;
- rule ID or rule family is missing;
- evidence tier is missing;
- coverage label is missing;
- limitation is missing;
- evidence is private-only, raw-only, hidden, local-only, future-only, or not
  public-safe;
- coverage is reduced, partial, syntax-only, failed, unavailable, or unknown;
- a row would expose raw facts, raw SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, remotes, generated scan
  directories, private sample names, raw command output, hidden validation
  details, or credential-like values;
- the question requires runtime logs, traces, metrics, telemetry, dashboards,
  production traffic, endpoint performance, deployment verification, runtime
  tests, or service-owner interpretation;
- the question asks for release approval, release hold, exception handling,
  risk acceptance, deployment success, rollback readiness, release safety, or
  operational safety;
- the answer would make or imply an absence-of-impact proof, absence of
  dependency, absence of risk, or complete-coverage conclusion;
- the claim depends on confidence, seniority, repetition, manager pressure, AI
  wording, LLM judgment, embeddings, vector databases, or prompt
  classification instead of documented rule evidence.

## Required Next Owners

Use public role categories:

- `release owner`: owns go/no-go, release hold, risk acceptance, rollback
  readiness, exception handling, and release-control decisions.
- `service owner`: owns service behavior interpretation and service-specific
  source/runtime questions.
- `runtime observability owner`: owns logs, traces, metrics, dashboards,
  production traffic, endpoint performance, alerts, and runtime signals.
- `test owner`: owns test coverage, test results, and test sufficiency.
- `code reviewer`: owns source review and code-review judgment.
- `security owner`: owns secrets, security review, sensitive configuration,
  data exposure, and release security questions.
- `build or tooling owner`: owns build status, project loading, validation
  tools, and analysis environment gaps.
- `manager`: owns staffing, priority, coordination, and stakeholder follow-up.
- `TraceMap site owner`: owns public page wording, proof links, metadata,
  validation scripts, and public-safe summaries.

Do not use private individual names, private team names, private service names,
or blame language.

## Supporting Route Guidance

Candidate supporting routes to verify at implementation time:

- `/limitations/` for site-wide non-claims, coverage gaps, absence-of-impact
  boundaries, private-material rules, and complete-coverage boundaries.
- `/static-vs-runtime/` for runtime behavior, production traffic, endpoint
  performance, logs, traces, metrics, telemetry, and service-owner boundaries.
- `/review-claim-checklist/` for proof fields, claim repeatability, safe
  wording, stop conditions, and owner follow-up.
- `/deploy-audit/` for deployment-adjacent evidence boundaries if present.
- `/validation/` for validation evidence, validation limitations, and what
  validation does not prove.
- `/manager-packet/` for manager-facing static-evidence conversation.
- `/questions/objections/` for objection handling and owner handoffs.
- `/review-room/` or `/review-room/release-boundary/` for review-room
  meeting context when present.

Dead links are not acceptable. If a candidate route is unavailable, record the
substitution, deferral, or decision to block implementation in
`implementation-state.md`.

## Validation Design

Future validation should use existing site validation patterns and should
inspect rendered text, decoded HTML, raw HTML attributes, metadata, sitemap
output, discovery output, tests, fixtures, and generated pages where those
artifacts exist.

Validation must check:

- visible `Public claim level: concept`;
- visible `No public conclusion without evidence`;
- required release-boundary rows;
- required row fields;
- required links and link resolution, failing when a supporting route is dead
  and no substitution, deferral, or blocking rationale is recorded;
- standalone metadata, sitemap metadata, and discovery metadata if
  standalone;
- section anchor and host-page metadata if sectioned;
- forbidden release, safety, runtime, production, endpoint-performance,
  deployment-success, absence-of-impact, complete-coverage, AI/LLM, and
  replacement claims;
- forbidden private/raw material and credential-like values;
- word count bounds of 900 to 2,400 rendered visible words unless amended,
  excluding only page-level navigation, breadcrumbs, site headers, site
  footers, metadata blocks, and all release-boundary matrix column header row
  cells; row names and data-cell values still count;
- desktop and mobile browser sanity for row readability and absence of
  incoherent overlap, using existing site browser-check patterns when
  available or recording at least one wide desktop viewport and one narrow
  mobile viewport.

Validation regexes or checks may allow forbidden words inside explicit
non-claim, forbidden-claim, safe-wording, stop-condition, row-boundary, or
objection-boundary contexts. They should not allow those words in metadata,
hero copy, link text, summaries, or discovery output where they would be read
as a positive release claim.
