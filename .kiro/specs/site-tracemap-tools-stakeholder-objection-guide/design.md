# Site TraceMap Tools Stakeholder Objection Guide Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Purpose

This design defines the information architecture for a future public-safe
stakeholder objection guide. The future surface should turn skepticism into a
repeatable pattern:

1. name the objection;
2. give a safe short answer;
3. identify public-safe evidence to check;
4. name the stop condition;
5. hand the next answer to the right owner;
6. preserve the limitation and non-claim;
7. link to a supporting public route.

The design does not implement site code.

## Placement Options

Preferred candidate options:

- `/objections/`: clearest standalone route if the site needs a direct
  objection reference for managers, reviewers, engineers, and agents.
- `/questions/objections/`: strong fit if implementation wants objections to
  nest under the broader question-index route.
- Section on `/questions/`: lowest route surface area while keeping the guide
  near stakeholder question routing.
- Section on `/manager-faq/`: useful if the final copy is mostly
  manager-facing and should remain close to FAQ answers.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/questions/`, because broad question routing should remain an
  index and the objection guide should stay focused on challenge handling.
- Replacing `/manager-faq/`, because FAQ answers and objection rows have
  different schemas and uses.
- Replacing `/limitations/`, because limitations are site-wide boundaries and
  objections are applied evidence checks.
- Replacing `/static-vs-runtime/`, because runtime separation is one topic
  within the objection guide, not the whole guide.
- Replacing `/review-claim-checklist/`, because the checklist answers whether
  a sentence can be repeated while the objection guide answers what evidence
  or owner is needed.
- Replacing `/proof-paths/tour/`, because proof-path education and objection
  handling are related but distinct.
- Replacing `/manager-demo-script/` or `/demo/manager-script/`, because the
  script choreographs a conversation and the objection guide is a reference
  surface.
- Primary navigation placement without a recorded information-architecture
  decision, because this concept-level surface may belong in secondary
  discovery.

Future implementation must record the final placement and rejected
alternatives before changing site source.

## Page Model

Recommended sections:

1. Opening boundary: visible `Public claim level: concept`, visible
   `No public conclusion without evidence`, and a short statement that the
   page makes skepticism useful by routing objections to evidence, owners, and
   stop conditions.
2. How to use the guide: explain row fields and make clear that safe short
   answers are not approvals.
3. Objection matrix: required eight rows, with stable anchors for each
   objection.
4. Owner handoff guidance: define role categories and what each owner can
   answer.
5. Non-claims and raw-material boundary: centralize runtime, release,
   production, AI/LLM, absence-of-impact, and artifact-sharing limits.
6. Supporting routes: list verified live routes and recorded substitutions or
   deferrals.

The objection matrix may render as a table on wide viewports and stacked
cards or grouped definition lists on narrow viewports. The row field labels
must remain accessible to screen readers and stable enough for focused
validation.

## Required Objection Rows

| Objection | Safe short answer | Evidence to check | Stop condition | Next owner | Public claim level | Limitation/non-claim | Supporting route |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Does this prove runtime behavior? | No. TraceMap can orient static repository evidence; runtime behavior needs runtime evidence. | Rule ID or rule family, evidence tier, coverage label, proof path, scan commit, and static-vs-runtime boundary. | Stop when the question asks what actually ran, failed, served traffic, or behaved in production. | Runtime observability owner or service owner. | concept | Does not prove runtime behavior, production requests, incident timeline, or operational state. | `/static-vs-runtime/` |
| Can I use this for release approval? | No. TraceMap evidence can inform review questions, but release approval needs release owners and the release process. | Claim level, proof path, limitation, validation status, tests, code review, source review, and release-review inputs. | Stop when the answer would approve, block, certify, or declare a release safe. | Release owner, test owner, code reviewer, and service owner. | concept | Does not approve releases, certify safety, replace tests, or replace human release judgment. | `/review-claim-checklist/` |
| Does this show production traffic or endpoint performance? | No. Static evidence can show code references or route surfaces when supported; traffic and performance need observability evidence. | Endpoint or route evidence surface, public-safe proof path, coverage label, and runtime telemetry boundary. | Stop when the question needs live request counts, latency, throughput, errors, dashboards, traces, or metrics. | Runtime observability owner or service owner. | concept | Does not show production traffic, endpoint performance, live request behavior, or runtime errors. | `/static-vs-runtime/` |
| Is this AI analysis? | No. Core scanner and reducer claims are deterministic, rule-backed, and evidence-tiered. | Rule IDs or rule families, extractor version, evidence tier, coverage label, limitations, and generated public-safe summaries. | Stop if the claim depends on LLM judgment, embeddings, vector databases, prompt classification, or confidence without rule evidence. | TraceMap owner or reviewer. | concept | Does not provide AI impact analysis, LLM analysis, prompt classification, embeddings, or vector database reasoning in the core scanner/reducer. | `/capabilities/` |
| Does missing evidence mean no impact? | No. Missing evidence is a gap or unknown unless a reducer-backed public-safe proof path says otherwise. | Coverage label, analysis gaps, rule family, limitation, proof path, and any reducer-backed public-safe result. | Stop when evidence is absent, reduced, syntax-only, private-only, raw-only, or unavailable. | Service owner, reviewer, or TraceMap owner depending on the gap. | concept | Does not prove absence of impact, absence of dependency, or complete coverage. | `/limitations/` |
| Can I share raw artifacts? | No. Public sharing should use public-safe summaries and proof routes, not raw local artifacts. | Public-safe route, redaction boundary, artifact family, snippet hash, limitation, and sharing policy. | Stop when sharing would expose raw facts, SQLite, logs, snippets, SQL, config values, secrets, local paths, remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values. | TraceMap site owner, security owner, or repository owner. | concept | Does not make raw local artifacts public proof or safe to publish. | `/proof-source-catalog/` |
| Who owns the next answer? | TraceMap can point to the kind of owner needed; it does not assign accountability or organizational authority. | Objection category, evidence gap, proof path, limitation, and owner role field. | Stop when the question asks for service ownership, incident command, release authority, staffing, priority, or organizational decision rights. | Manager, service owner, release owner, runtime owner, test owner, security owner, or reviewer as appropriate. | concept | Does not assign ownership, incident command, release authority, staffing, priority, or accountability. | `/questions/` |
| What do we do under reduced coverage? | Keep reduced coverage visible, downgrade the claim, and route the unknown to an owner. | Coverage label, analysis gap, build or scan status, evidence tier, limitation, and proof path. | Stop when coverage is partial, reduced, failed, unavailable, or too weak for the requested conclusion. | TraceMap owner, reviewer, service owner, or build/tooling owner. | concept | Does not normalize partial evidence into full coverage or clean conclusions. | `/limitations/` |

Implementation may adjust supporting routes to current public equivalents, but
it must preserve every required objection, row field, owner handoff,
limitation, and non-claim.

## Supporting Route Guidance

Candidate supporting routes to verify at implementation time:

- `/static-vs-runtime/` for runtime behavior, production traffic, endpoint
  performance, logs, traces, metrics, and telemetry boundaries.
- `/review-claim-checklist/` for release-approval red flags, repeatability,
  proof fields, stop conditions, and owner follow-up.
- `/capabilities/` for deterministic static capability framing.
- `/limitations/` for gaps, reduced coverage, missing evidence, non-claims,
  and absence-of-impact boundaries.
- `/proof-source-catalog/` for public-safe source mapping and raw-artifact
  sharing boundaries.
- `/questions/` for routing broad stakeholder questions to proof surfaces.
- `/manager-faq/` for manager-facing explanation if the objection row needs
  adjacent FAQ framing.
- `/proof-paths/tour/` for teaching how to follow evidence paths.
- `/demo/manager-script/` or `/manager-demo-script/` for live-presentation
  context if present, but not as the canonical objection guide.

Dead links are not acceptable. If a candidate route is unavailable, record the
substitution, deferral, or decision to block implementation in
`implementation-state.md`.

## Owner Role Guidance

Use public role categories, not private team names or individuals:

- `service owner`: owns interpretation of service behavior, source context,
  and code-level follow-up.
- `runtime observability owner`: owns logs, traces, metrics, dashboards,
  production traffic, endpoint performance, and runtime evidence.
- `release owner`: owns release gates, approval process, deployment policy,
  and final release decisions.
- `test owner`: owns test evidence, reproduction, regression coverage, and
  verification strategy.
- `reviewer`: owns claim checking, proof-path review, and repeatability of
  public or internal statements.
- `TraceMap owner`: owns scanner/reducer evidence boundaries, public site
  copy, rule documentation, validation, and implementation gaps.
- `security owner` or `repository owner`: owns raw artifact sharing, secrets,
  private paths, remotes, and publication decisions.
- `manager`: owns prioritization and coordination decisions after evidence
  and owner inputs are identified.

The guide must not imply that TraceMap assigns accountability, service
ownership, incident command, release authority, staffing, or priority.

## Copy Rules

Use bounded answer wording:

- `can orient static evidence`
- `check the proof path`
- `look for rule ID or rule family`
- `keep the coverage label visible`
- `stop before release approval`
- `route runtime questions to observability owners`
- `label missing evidence as unknown`
- `use public-safe summaries`
- `ask the next owner`

Avoid unsupported conclusion wording as claims:

- `proves runtime behavior`
- `production proven`
- `safe to release`
- `approved`
- `blocked`
- `certified`
- `root cause`
- `endpoint performance proven`
- `complete coverage`
- `no impact`
- `not impacted`
- `AI-powered impact analysis`
- `autonomous approval`

Forbidden terms may appear inside explicitly bounded non-claim, limitation, or
objection examples when the sentence says TraceMap does not make that claim.

## Validation Design

Focused validation should use structured HTML parsing where possible. It
should verify:

- page or section visible claim label and shared principle;
- selected placement and rejected alternatives recorded in implementation
  state;
- all eight required objection categories;
- every required row field;
- supporting route resolution or recorded route deferral;
- standalone route metadata, sitemap metadata, and discovery metadata when
  standalone;
- stable in-page anchor and conservative host-page metadata when sectioned;
- row-level public claim levels;
- owner role values are public role categories;
- word count bounds;
- visible word counts should include rendered body prose and objection row
  cell content, excluding page-level navigation, breadcrumbs, site headers,
  site footers, metadata blocks, and row-field label headers;
- forbidden runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM, embedding,
  vector database, prompt-classification, autonomous-approval, and
  absence-of-impact wording across rendered text, decoded HTML, raw HTML
  attributes, alt text, captions, metadata, fixtures, tests, sitemap output,
  discovery output, and generated pages;
- forbidden terms are allowed only inside bounded non-claim, limitation, or
  objection contexts;
- stop-condition column text qualifies as an objection-boundary context and
  should be excluded from overclaim scans. Phrases such as `stop before
  release approval` or `stop when a conclusion would be blocked` are bounded
  limitation language inside the row schema, not standalone claims;
- the eight required objection titles should be whitelisted as titles, while
  the same phrases remain forbidden when repeated as unsupported claims
  elsewhere;
- forbidden private/raw material and credential-like values are absent from
  public output except inside explicit non-shareable artifact examples;
- desktop and mobile browser sanity for the selected route or host page.

The implementation should wire focused validation into the existing aggregate
site validation command so `npm run validate` exercises the objection guide.
