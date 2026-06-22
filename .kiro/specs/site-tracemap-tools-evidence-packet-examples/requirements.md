# Site TraceMap Tools Evidence Packet Examples Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-site page or section for synthetic evidence packet
examples that teach readers how a bounded public claim travels with its proof
path, evidence tier, limitation, non-claim, validation evidence, and next
owner.

This is a spec-only site phase. It does not implement site code, generated
artifacts, scanner behavior, reducer behavior, validation scripts, or public
copy changes. The future examples are concept-level because they are synthetic
public-safe teaching shapes unless a later implementation explicitly backs an
example with checked-in demo artifacts and records that evidence.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or section must visibly say `Public claim level: concept`.
- The future page or section must visibly say
  `No public conclusion without evidence`.
- The examples must be labeled synthetic and public-safe when they are not
  backed by checked-in demo artifacts.
- The surface may explain how evidence packet fields fit together, but it must
  not publish raw artifacts or imply real customer, private repository, or
  production evidence.
- The surface must not claim runtime behavior proof, production traffic
  knowledge, endpoint performance measurement, outage cause, release approval,
  release safety, operational safety, complete coverage, AI impact analysis,
  LLM analysis, autonomous approval, or replacement of human review.
- The surface must use neutral language. It must not blame vendors,
  consultants, teams, code authors, maintainers, or "bad code".

## Relationship to Existing Site Surfaces

The examples surface is a teaching gallery for packet shapes. It must remain
distinct from these neighboring public-site surfaces:

- `/packets/` explains the general evidence packet artifact model. Evidence
  packet examples show concrete synthetic packet shapes, not the full artifact
  taxonomy.
- `/packets/assembly/` explains how a human assembles a review packet. Evidence
  packet examples show finished example rows and stop states, not the assembly
  workflow.
- `/examples/scan-packet/` may show a scan-oriented example packet. Evidence
  packet examples focus on public-safe claim shapes and must not publish raw
  scan outputs.
- `/demo/result/` may show a demo result. Evidence packet examples can link to
  demo-backed material only when the implementation verifies that the demo
  artifact is checked in and public-safe.
- `/proof-source-catalog/` maps public routes and claims to source material.
  Evidence packet examples consume proof paths without becoming a second proof
  catalog.
- `/review-claim-checklist/` decides whether a sentence can be repeated.
  Evidence packet examples show the packet ingredients the checklist should
  inspect; they do not replace the checklist.

## Requirements

### Requirement 1: Choose a bounded placement

The future implementation shall choose one final placement for the evidence
packet examples surface and record rejected alternatives before changing site
source.

Acceptance criteria:

- Candidate placements are `/packets/examples/`,
  `/examples/evidence-packets/`, a section on `/packets/`, or a section on
  `/packets/assembly/`.
- The implementation records the final placement and rejected alternatives in
  this spec's `implementation-state.md`.
- The selected surface says `Public claim level: concept`.
- The selected surface states `No public conclusion without evidence`.
- If implemented as a standalone route, the route is added to sitemap metadata
  and discovery metadata using existing site patterns.
- If implemented as a section, the implementation records why section placement
  is clearer than a standalone route and how readers can link directly to the
  section.
- If implemented as a section, the section must have a stable anchor so
  readers can link directly to it.
- The surface is not added to top navigation unless the implementation records
  why the existing site navigation pattern supports it.
- The placement must not create a competing artifact model, packet assembly
  workflow, scan-packet page, demo-result page, proof-source catalog, or claim
  checklist.

### Requirement 2: Publish four required example shapes

The future page or section shall include exactly these required example
categories unless a later spec expands the set:

- demo-backed packet
- reduced-coverage packet
- gap-labeled packet
- stop-condition packet

Acceptance criteria:

- Each example includes these fields: claim label, public claim level, proof
  path, rule ID or rule family, evidence tier, coverage label, synthetic
  public-safe path and span, commit or extractor placeholder, limitation,
  non-claim, next owner, and validation evidence.
- The next owner field must use a public-safe role or review process, such as
  `reviewing engineer`, `service owner role`, or `release review process`.
  It must not use a real person's name, private individual, or internal team
  name.
- A required example field is present when it carries a public-safe value.
  Only the stop-condition packet may satisfy field presence with an explicit
  blocked marker, such as `proof path: blocked: missing public-safe proof
  trail`, and only when the marker names the missing public-safe evidence or
  owner. It must not omit the field row. All other examples must carry a
  public-safe value for every required field.
- Each example uses `Public claim level: concept`. An individual example may
  carry the `demo-backed` coverage label only when a checked-in public demo
  artifact supports it and the evidence is recorded in
  `implementation-state.md`; the per-example and page-level public claim level
  still remains `concept`. Any public claim level stronger than `concept` is
  out of scope for this spec and requires a future spec that defines the
  stronger label, its required checked-in evidence, and how validation
  reconciles it with the page-level claim.
- The demo-backed packet may be labeled demo-backed only when it links to a
  checked-in public demo artifact or public-safe demo summary. Otherwise it
  must be labeled `synthetic public-safe example` and may describe the row as
  a "demo-backed shape" rather than a demo-backed result.
- The reduced-coverage packet must keep reduced or partial coverage visible in
  the claim label, coverage label, limitation, validation evidence, and next
  owner.
- The gap-labeled packet must use an evidence tier and coverage label that make
  the gap visible. `Tier4Unknown` is acceptable when the example demonstrates
  an analysis gap.
- The stop-condition packet must show why the packet should not become public
  copy until the blocker is resolved or downgraded to an explicit limitation.
- The synthetic public-safe path/span uses example-only paths such as
  `examples/public-demo/Controllers/OrdersController.cs:42-58` and must not use
  local absolute paths, private repository paths, raw remotes, generated scan
  directories, private sample names, or source snippets.
- The commit or extractor placeholder uses visibly fake values such as
  `commit: demo-sha-placeholder` and `extractor: tracemap-demo-extractor@x.y.z`
  unless backed by checked-in demo evidence. It must not use raw private SHAs
  or hidden extractor details.

### Requirement 3: Keep examples public-safe and synthetic

The future implementation shall prevent the examples from leaking raw,
private, or customer-like material.

Acceptance criteria:

- The rendered page, metadata, discovery metadata, decoded HTML, raw HTML
  attributes, tests, and fixtures do not publish raw facts, raw SQLite
  databases, analyzer logs, raw source snippets, raw SQL, configuration
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, raw command output, hidden validation details, or
  credential-like values.
- Examples that are not backed by checked-in demo artifacts are visibly labeled
  `synthetic public-safe example`.
- Examples do not include private organization names, customer names, personal
  names, internal team names, vendor blame, consultant blame, local machine
  names, branch names from private work, raw commit URLs, or private issue
  references.
- Snippet-like text is not used. If an implementation needs a code reference,
  it uses only public-safe path/span metadata and a snippet hash placeholder,
  not raw source.
- Any link to a demo-backed artifact must point to checked-in public site or
  repository material and must be validated as an internal link when possible.

### Requirement 4: Preserve explicit non-claims

The future page or section shall make unsupported claims visibly out of scope.

Acceptance criteria:

- The examples do not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release approval, release safety, operational
  safety, complete coverage, AI impact analysis, LLM analysis, autonomous
  approval, autonomous review, or replacement of human review.
- The examples do not imply TraceMap replaces source review, ownership
  decisions, telemetry, logs, traces, APM, tests, release controls, incident
  response, manager judgment, service ownership, database ownership, or human
  review.
- The examples avoid saying a route, endpoint, dependency, package, SQL-facing
  reference, DTO, system, release, or team is impacted, safe, unsafe,
  approved, blocked, root cause, production proven, or validated for release
  unless the phrase appears only inside explicit non-claim or stop-condition
  wording.
- The examples may say that deterministic static evidence can help humans
  preserve proof paths, limitations, and next-owner questions.

### Requirement 5: Define validation expectations

The future implementation shall add focused validation for the examples and
their public-safe boundaries.

Acceptance criteria:

- Validation checks the example schema fields: claim label, public claim level,
  proof path, rule ID or rule family, evidence tier, coverage label, synthetic
  public-safe path/span, commit or extractor placeholder, limitation,
  non-claim, next owner, and validation evidence. A blocked marker satisfies
  field presence only for the stop-condition packet and only when the marker
  names the missing public-safe evidence or owner.
- Validation checks the four required example categories: demo-backed packet,
  reduced-coverage packet, gap-labeled packet, and stop-condition packet.
- Validation checks required visible labels: `Public claim level: concept`,
  `No public conclusion without evidence`, and
  `synthetic public-safe example` for examples not backed by checked-in demo
  artifacts.
- Validation checks required links to adjacent surfaces when those routes
  exist: `/packets/`, `/packets/assembly/`, `/examples/scan-packet/`,
  `/demo/result/`, `/proof-source-catalog/`, and
  `/review-claim-checklist/`.
- Validation checks metadata and discovery metadata if the examples are
  implemented as a standalone route, including `publicClaimLevel: concept`.
- If discovery metadata uses a fallback `hintCategory`, validation or
  implementation notes must require the chosen value and justification to be
  recorded in `implementation-state.md`.
- Validation checks sitemap metadata if the examples are implemented as a
  standalone route.
- Validation checks forbidden claims, private or raw material, credential-like
  values, and local path patterns in rendered text, decoded HTML, raw HTML
  attributes, metadata, discovery metadata, tests, and fixtures.
- Validation enforces a rendered word count between 450 and 1300 words unless
  `implementation-state.md` records a justified tighter or higher bound for
  the selected placement. Any higher bound must explain why the four complete
  examples, relationship block, non-claims, and validation labels cannot fit
  without truncating required fields. The default bound assumes compact table,
  accordion, or short-card rendering rather than repeated long-form prose for
  every field.
- Validation includes desktop and mobile browser sanity checks for layout or
  interaction changes.
- Spec-only validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, and focused text checks over this spec
  directory for required status, readiness, claim level, required example
  fields, boundaries, and forbidden raw/private material.
