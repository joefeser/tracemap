# Site TraceMap Tools Proof Path FAQ Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design describes a future public-safe FAQ page or section about proof
paths. It is separated from requirements because the implementation needs a
clear information architecture: readers arrive with repeated proof-path
questions, get bounded answers, and leave with the evidence fields and
limitations still attached.

This design does not implement site code.

## Placement Options

Preferred starting options:

- `/proof-paths/faq/`: recommended default. It gives recurring questions a
  dedicated route while keeping the page near the canonical proof-path
  overview.
- Section on `/proof-paths/`: use when the existing overview is short enough
  to absorb a compact FAQ without burying the canonical proof-path model.
- Section on `/proof-paths/tour/`: use when the final content mainly supports
  the guided reading flow and would be awkward as a separate reference page.
- Section on `/questions/`: use when the implementation treats the FAQ as one
  reader-question cluster inside the broader question index.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/proof-paths/`, because the FAQ answers repeated questions while
  the base proof-path page should remain the canonical overview.
- Replacing `/proof-paths/tour/`, because a tour should show a guided flow for
  one proof path rather than answering many objections and edge cases.
- Replacing `/evidence/`, because evidence vocabulary and artifact families
  need a broader reference than this proof-path FAQ.
- Replacing `/limitations/`, because limitation definitions and global
  non-claims belong on the limitations surface.
- Replacing `/static-vs-runtime/`, because the FAQ can link to static/runtime
  boundaries but should not become the runtime-telemetry explainer.
- Replacing `/review-claim-checklist/`, because the checklist is a decision
  ritual and the FAQ is an explanation surface.
- Adding to primary navigation before an information-architecture review,
  because concept-level FAQ content may belong in secondary discovery.

## Page Model

Recommended sections:

1. Opening: state `Public claim level: concept`, the shared site principle,
   and that the FAQ explains how to read proof paths without creating runtime,
   release, operational, or approval claims.
2. Quick definition: define proof path as a public-safe route or reference
   trail from claim to static evidence fields and limitations.
3. FAQ list: answer the required questions from the requirements in a stable,
   linkable order.
4. Safe and unsafe answer patterns: show bounded answer shapes and rejected
   overclaim patterns.
5. Proof paths and review packets: explain how a review packet may gather
   proof paths, limitations, review notes, and owner follow-ups without
   converting missing evidence into approval.
6. Where to stop: explain stop conditions for missing proof path, missing rule
   ID or rule family, missing evidence tier, missing coverage label, missing
   limitation, private-only evidence, hidden detail, copy that implies a
   demo-backed or `demo`-level public claim without a recorded spec amendment
   that matches a checked-in public-safe demo artifact, or forbidden
   runtime/release/AI wording.
7. Adjacent surfaces: link to the current public-safe equivalents for
   `/questions/`, `/proof-paths/`, `/proof-paths/tour/`, `/evidence/`,
   `/limitations/`, `/static-vs-runtime/`, and
   `/review-claim-checklist/` when they exist.
8. Non-claims and private-material boundary: repeat the runtime,
   operational, release, complete-coverage, AI/LLM, autonomous-approval, and
   raw/private-material boundaries.

The FAQ may be rendered as accordions, linked sections, or a static list. If
accordions are used, the implementation must preserve accessible headings,
keyboard interaction, and stable anchors for each question. Every answer's
full text must be present in the static HTML, not injected only after user
interaction, so validation, bots, crawlers, and readers inspect the same
content. A static list is acceptable and likely simpler.

## Required FAQ Questions

| Anchor | Question | Answer purpose | Required fields to preserve | Stop or handoff condition |
| --- | --- | --- | --- | --- |
| `#what-is-a-proof-path` | What is a proof path? | Define a public-safe trail from claim to deterministic static evidence. | Claim, public claim level, proof path, rule ID or rule family, evidence tier, coverage label, limitation, source context. | Stop if the trail cannot name the evidence-bearing fields. |
| `#how-to-read` | How do I read a proof path? | Give the fixed reading order. | Claim, claim level, rule, tier, coverage, commit/source context, extractor version or schema family, limitation, non-claim, next owner. | Stop if any evidence-bearing field is missing or private-only. |
| `#evidence-tiers` | What do evidence tiers mean? | Explain tiers without turning tier rank into complete proof. | `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, `Tier4Unknown`. | Downgrade or label uncertainty when tier is syntax-only, unknown, or absent. |
| `#coverage-labels` | What do coverage labels mean? | Explain coverage as a boundary. | Full, partial, reduced, unknown, unavailable, future-only, or gap-labeled state when public-safe. | Preserve reduced or unknown labels; do not normalize them upward. |
| `#limitations` | Why do limitations matter? | Treat limitations as part of the claim. | Limitation, non-claim, owner follow-up where needed. | Do not repeat the claim after dropping the limitation. |
| `#missing-evidence` | What should I do when evidence is missing? | Make gaps actionable without inventing proof. | Gap label, missing field, next owner, downgrade or hold decision. | Stop, downgrade, keep internal, or hand off. |
| `#review-packets` | How do proof paths relate to review packets? | Explain packet assembly and review handoff. | Proof paths, limitations, review notes, owner follow-ups, public claim level. | A packet does not approve release, safety, or runtime behavior. |
| `#static-evidence-cannot-prove` | What can static evidence not prove? | State hard non-claims. | Runtime, production, performance, outage, release, operational, complete-coverage, AI/LLM, approval, and replacement boundaries. | Route operational questions to runtime owners, tests, observability, and human review. |
| `#private-or-raw-artifacts` | Can a proof path use private or raw artifacts? | Explain public-safe proof material. | Public-safe summaries, checked-in docs, public routes, sanctioned demo artifacts. | Raw/private/local material cannot become public FAQ content. |
| `#agents-and-reviewers` | What should agents and reviewers preserve? | Tell human and automated readers what must travel with a claim. | Proof path, rule ID or rule family, tier, coverage, limitation, non-claim, public claim level. | Do not repeat a claim after dropping required fields. |

Implementation may add more questions if they stay inside the same boundary.
It must not omit the required questions.

When the FAQ is placed as a section on a host route, prefix every anchor in
this table with `faq-`, for example `#faq-what-is-a-proof-path`, so anchors
are unique within the host document, per Requirement 1. The unprefixed anchors
above apply only to the standalone `/proof-paths/faq/` route.

## Safe Answer Patterns

Use bounded wording:

- `This proof path lets a reader inspect static evidence for <claim label>
  under <rule ID or rule family>, with <evidence tier>, <coverage label>, and
  <limitation>.`
- `The evidence is missing or private-only, so the public answer is a gap,
  downgrade, internal-only note, or owner follow-up.`
- `The review packet can carry this proof path, limitation, and next-owner
  handoff to a reviewer; it does not approve a release.`
- `The coverage label is reduced, so the answer must keep the reduced label
  visible and avoid complete-coverage wording.`
- `The tier is syntax-only or unknown, so the answer should name the
  uncertainty instead of upgrading the claim.`

Preferred verbs:

- `inspect`
- `follow`
- `compare`
- `check`
- `record`
- `label the gap`
- `downgrade`
- `hold`
- `hand off`
- `escalate`

## Unsafe Answer Patterns

Reject unsupported wording:

- `This proof path proves runtime behavior.`
- `This proof path proves production traffic or endpoint performance.`
- `This proof path identifies outage cause.`
- `This proof path proves the release is safe.`
- `This proof path proves operational safety.`
- `This proof path proves complete coverage.`
- `This proof path is AI or LLM impact analysis.`
- `This review packet approves the release or authorizes autonomous approval.`
- `This replaces tests, source review, runtime observability, service-owner
  judgment, or human review.`
- `Raw facts, raw SQLite, analyzer logs, source snippets, SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, private
  sample names, command output, hidden validation details, or credential-like
  values should be pasted into public FAQ copy.`

Unsafe examples must be framed as rejected patterns, not as live claims. They
must not include real private paths, private sample names, credentials,
command output, or hidden validation detail.

## Review Packet Relationship

The FAQ should explain:

- A proof path is the evidence trail for a claim.
- A review packet can gather several proof paths with their limitations,
  claim levels, review notes, and owner follow-ups.
- A review packet can help a human review what is known, partial, missing, or
  private-only.
- A review packet does not approve a release, certify safety, prove runtime
  behavior, resolve missing evidence, or replace source review, tests,
  telemetry, observability, service owners, or human judgment.

## Copy Rules

Use static-evidence wording:

- `static evidence`
- `proof path`
- `rule ID or rule family`
- `evidence tier`
- `coverage label`
- `limitation`
- `analysis gap`
- `public-safe summary`
- `review packet`
- `owner follow-up`
- `non-claim`

Avoid blame language:

- Do not say a person, team, reviewer, service, or owner "failed" because
  evidence is missing.
- Prefer `the proof path is incomplete`, `the evidence is private-only`,
  `coverage is reduced`, `the claim needs owner follow-up`, or
  `the answer must be downgraded`.
- Validation should check at least a recorded advisory set of representative
  blame indicators, such as `failed`, `fault`, `to blame`, `negligent`,
  `careless`, or attributing missing, reduced, or conflicting evidence to a
  named person, team, service, customer, or reviewer.

Avoid unsupported conclusion wording:

The canonical validated unsupported conclusion verbs are `proves`,
`guarantees`, `certifies`, `approves`, `replaces`, `resolves`, and
unqualified `impacted`. The additional phrases below are advisory wording
patterns that should also stay inside bounded rejection context.

- `proves`
- `guarantees`
- `certifies`
- `approves`
- `replaces`
- `safe to release`
- `operationally safe`
- `root cause`
- `production proven`
- `complete coverage`
- `resolves`
- unqualified `impacted` unless tied to public-safe reducer-backed evidence
- `AI impact analysis`
- `autonomous approval`
- `replaces telemetry`

If forbidden terms appear in non-claims or unsafe-pattern sections,
validation should allow them only inside explicitly bounded rejection context.

## Validation Design

Focused validation should parse the rendered standalone route or host section
with structured HTML tools where practical. It should verify:

- visible concept claim label and shared principle;
- required FAQ questions and stable anchors;
- every FAQ answer's full text is present in static HTML, not injected only
  after client-side interaction;
- if accordions or progressive disclosure are used, every answer's full text
  is present in the static HTML and focused validation asserts it;
- safe and unsafe answer pattern regions;
- adjacent route links and substitutions;
- no unsupported runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM, release-
  approval, autonomous-approval, or replacement-of-review claims outside
  bounded rejection contexts;
- no unsupported conclusion verbs (`proves`, `guarantees`, `certifies`,
  `approves`, `replaces`, `resolves`, or unqualified `impacted`) outside
  bounded unsafe-pattern, non-claim, or limitation regions, with `impacted`
  allowed only when tied to a public-safe reducer-backed result;
- no forbidden private/raw material in visible text, decoded HTML, raw HTML,
  attributes, alt text, captions, metadata, fixtures, tests, discovery output,
  sitemap output, or bot-facing metadata;
- no blame language in visible copy, metadata, examples, or validation
  errors, checking at least a recorded advisory set of representative blame
  indicators such as `failed`, `fault`, `to blame`, `negligent`, `careless`,
  or attributing missing, reduced, or conflicting evidence to a named person,
  team, service, customer, or reviewer;
- metadata and discovery claim level remain `concept` if a standalone route is
  chosen;
- host route title, description, canonical, Open Graph, sitemap, and
  discovery metadata are not upgraded above the host route's recorded claim
  level if section placement is chosen, and the FAQ section's stable anchors
  resolve in generated output and are unique within the host document;
- if implemented as a section, a focused duplicate-ID and anchor-resolution
  check runs on the generated host-page HTML, with the check name or command
  recorded in `implementation-state.md`;
- every public link resolves in generated output, or the implementation-state
  note records a substitution, deferral, or omission.

The focused validation should be wired into the existing aggregate site
validation command when site source is added.
