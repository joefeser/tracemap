# Site TraceMap Tools Proof Paths For Managers Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design describes a future manager-facing proof-path page or section for
`tracemap.tools`. The future surface should help a manager or reviewer move
from a decision question to a bounded static evidence packet, then stop at the
right boundary and route the remaining runtime, product, release, ownership,
or security judgment to the correct owner category.

The design does not implement site code.

## Placement Options

Preferred candidate options:

- `/proof-paths/for-managers/`: recommended default. It keeps the surface in
  the proof-path family while making the manager decision lens explicit.
- `/manager-proof-paths/`: acceptable if implementation wants a short direct
  route for stakeholder sharing and records why the route should not live
  under `/proof-paths/`.
- Section on `/manager-packet/`: acceptable if the final content is compact
  enough to act as a manager-packet subsection.
- Section on `/manager-faq/`: acceptable if the final copy becomes mostly
  question-and-answer prose.
- Section on `/proof-paths/`: acceptable if the existing proof-path overview
  can carry a manager-specific decision path without becoming too long.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/manager-brief/`, because the brief is high-level value framing
  and should not become proof-path mechanics.
- Replacing `/manager-faq/`, because FAQ answers and decision matrices have
  different reading patterns.
- Replacing `/manager-packet/`, because the packet is a summary handoff while
  this surface is a proof interpretation guide.
- Replacing `/proof-paths/`, because the base proof-path page should remain
  the canonical overview for all readers.
- Replacing `/proof-paths/faq/`, because the FAQ handles recurring
  proof-path questions and this page handles manager decision routing.
- Replacing `/proof-paths/tour/`, because a tour should stay a guided reading
  flow.
- Replacing `/packets/` or `/packets/assembly/`, because those pages describe
  packet ingredients and assembly rather than manager decision ownership.
- Replacing `/proof-source-catalog/`, because the catalog inventories
  public-safe source material while this surface explains manager-facing
  proof interpretation and owner routing.
- Adding to primary navigation without an information-architecture note,
  because concept-level explanatory surfaces may belong in secondary
  discovery.

## Page Model

Recommended sections:

1. Opening boundary: visible `Public claim level: concept`, visible
   `No public conclusion without evidence`, and a short statement that the
   surface translates proof paths into manager and reviewer decision terms.
2. How to use this page: start with a question, inspect the evidence packet,
   keep coverage and limitations attached, and route the next judgment to an
   owner.
3. Manager question matrix: required rows and fields from the requirements,
   presented as a table on wide screens and stacked groups on small screens.
4. Proof path anatomy: compact checklist of fields a claim must preserve
   before it can be repeated.
5. Coverage labels and limits: explain that reduced, partial, unknown,
   unavailable, syntax-only, private-only, future-only, and gap-labeled states
   shape what may be said.
6. Next-owner routing: map static-analysis boundaries to runtime, product,
   release, test, source, security, and TraceMap owner categories.
7. What this page is not: repeat non-claims for runtime proof, production
   traffic, endpoint performance, outage cause, release safety, complete
   coverage, AI/LLM analysis, autonomous approval, and automated management
   decisions.
8. Adjacent surfaces: link to manager brief, manager FAQ, manager packet,
   proof-path overview, proof-path FAQ or tour, packet assembly, limitations,
   proof-source catalog, static-vs-runtime, review checklist, and question
   index when available.

The future surface should be static HTML. Accordions or progressive
disclosure are allowed only if all answer text remains present in the static
HTML for validation, crawlers, and bots.

## Required Manager Question Matrix

| Anchor | Manager or reviewer question | Evidence packet to inspect | What static evidence can support | What it does not prove | Coverage-label consequence | Stop condition | Next owner | Supporting route |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `#question-code-path-change` | What changed in the code path we are reviewing? | Review packet or proof path with rule ID or rule family, evidence tier, coverage label, commit context, file span, limitation, and validation evidence. | A static reference, dependency, route, contract, or artifact relationship when the cited rule and tier support it. | Runtime behavior, production traffic, user impact, release safety, or product correctness. | Preserve exact coverage label; downgrade if syntax-only, reduced, unknown, or private-only. | Stop when the answer would say what actually ran, served traffic, or affected users. | Reviewer, service owner, code owner, or architect. | `/proof-paths/` or `/packets/assembly/` |
| `#question-repeat-claim` | What evidence supports repeating this claim? | Claim review packet with public claim level, proof path, rule ID or rule family, evidence tier, coverage label, limitation, non-claim, and source context. | Whether the public sentence has visible static evidence and the fields needed for review. | Approval, safety, absence of risk, absence of impact, or complete coverage. | If coverage is reduced or unknown, repeat only a downgraded or gap-labeled claim. | Stop when required proof fields are missing, private-only, or inconsistent with the wording. | Reviewer or TraceMap owner. | `/review-claim-checklist/` |
| `#question-coverage-meaning` | What does reduced or partial coverage mean for this decision? | Evidence packet with coverage label, analysis gaps, build or scan status, evidence tier, and limitation. | The analysis boundary and what was or was not available to the static scanner. | That the repo is clean, fully analyzed, safe, unsafe, affected, unaffected, or ready for a reducer-backed impact conclusion. | Keep the reduced or partial label visible and route unknowns instead of smoothing them into a clean conclusion. | Stop when someone tries to upgrade partial evidence into full certainty. | TraceMap owner, reviewer, service owner, or build/tooling owner. | `/limitations/` |
| `#question-next-runtime-product-owner` | Who should answer runtime or product behavior next? | Proof path plus limitation and owner routing row. | The static evidence boundary and the owner category needed for the next question. | Runtime truth, product tradeoff, customer impact, priority, or ownership assignment. | Coverage labels decide whether the static answer can be repeated or must become an owner follow-up. | Stop when the question asks what users saw, what happened in production, or what product decision to make. | Runtime observability owner, product owner, manager, or service owner. | `/static-vs-runtime/` and `/questions/` |
| `#question-release-decision` | Can this evidence approve, block, or certify a release? | Release-review input packet with claim level, proof path, validation status, tests, limitations, and review notes. | Static review inputs and questions to ask before a release decision. | Release approval, release blocking, release certification, release safety, or operational safety. | Reduced, unknown, or private-only evidence cannot be promoted to approval language. | Stop before wording would approve, block, certify, or declare a release safe. | Release owner, test owner, reviewer, and service owner. | `/review-claim-checklist/` |
| `#question-runtime-incident-performance` | Can this evidence explain production traffic, endpoint performance, or outage cause? | Static proof path plus static-vs-runtime boundary. | Static code, config, route, dependency, or contract evidence when a rule supports it. | Live requests, latency, throughput, errors, traces, logs, incident timeline, or outage cause. | Coverage labels remain static-analysis labels, not observability labels. | Stop when the answer needs telemetry, dashboards, logs, traces, incident command, or postmortem evidence. | Runtime observability owner, service owner, or incident owner. | `/static-vs-runtime/` |
| `#question-public-sharing` | Can this evidence be shared publicly? | Public-safe packet summary, proof-source catalog entry, or sanitized demo surface. | Whether a public-safe route or summary can carry the claim without raw material. | That raw local artifacts, private paths, snippets, SQL, config, secrets, remotes, logs, or private names are safe to publish. | Private-only evidence becomes a limitation, not public proof. | Stop when supporting material is raw, private, local-only, hidden, or credential-like. | Security owner, repository owner, or TraceMap site owner. | `/proof-source-catalog/` |
| `#question-missing-evidence` | What should happen when evidence is missing, private-only, syntax-only, or unknown? | Gap-labeled packet with missing field, evidence tier, coverage label, limitation, and owner follow-up. | The existence and location of the analysis gap. | Absence of impact, absence of dependency, correctness, safety, or complete coverage. | Gap and unknown labels must travel with the claim or block public repetition. | Stop, downgrade, keep internal, or hand off rather than filling the gap with confidence. | Reviewer, service owner, TraceMap owner, or manager. | `/limitations/` |

Implementation may add rows, but it must preserve these required questions,
row fields, coverage consequences, stop conditions, owner categories, and
non-claims.

## Proof Path Anatomy

The future page should include this checklist in manager-friendly language:

- Claim or question being reviewed.
- Public claim level.
- Proof path or evidence packet link.
- Rule ID or rule family, with documented limitation.
- Evidence tier: `Tier1Semantic`, `Tier2Structural`,
  `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- Coverage label copied from the cited evidence.
- Commit or public-safe source context when available.
- Extractor version or schema family when available.
- Public-safe file path and line span when public-safe, or a visible
  limitation when not public-safe.
- Snippet hash or public-safe summary when available; no raw snippets by
  default.
- Artifact family as a local-output category or sanitized summary reference.
- Limitation and non-claim.
- Validation evidence.
- Unresolved gaps.
- Next owner.

The copy should say that a proof path is weakened or blocked when the claim is
separated from its rule basis, evidence tier, coverage label, limitation,
claim level, or next-owner handoff.

## Owner Routing

Use public role categories:

- `manager`: owns prioritization and coordination after evidence and owner
  inputs are visible.
- `reviewer`: owns claim checking, repeatability, and proof-path review.
- `service owner`, `code owner`, or `architect`: owns source interpretation,
  service architecture, and service follow-up.
- `runtime observability owner`: owns telemetry, logs, traces, metrics,
  dashboards, production traffic, endpoint performance, and runtime evidence.
- `product owner`: owns product behavior, customer impact, user workflow, and
  product tradeoffs.
- `release owner`: owns release gates, deployment timing, and final release
  decisions.
- `test owner`: owns test evidence, reproduction, regression coverage, and
  verification strategy.
- `build/tooling owner`: owns build, project-load, scan setup, and tooling
  gaps that affect static-analysis coverage.
- `incident owner`: owns incident coordination and incident-record questions;
  runtime evidence still belongs with runtime observability and service
  owners.
- `security owner` or `repository owner`: owns publication of sensitive
  material, raw artifacts, secrets, remotes, private paths, and sharing policy.
- `TraceMap owner`: owns rule documentation, extractor limitations, public
  site claim wording, validation, and implementation gaps.

The page must not imply that TraceMap assigns accountability, incident
command, release authority, staffing, priority, product ownership, or
organizational decision rights.

## Copy Rules

Use bounded wording:

- `inspect the proof path`
- `check the evidence packet`
- `preserve the coverage label`
- `keep the limitation attached`
- `route the runtime question`
- `name the next owner`
- `downgrade the claim`
- `label the gap`
- `hold the claim internal`
- `ask a product, release, runtime, or service owner`

Avoid unsupported conclusion wording as live claims:

- `proves runtime behavior`
- `production proven`
- `confirms runtime behavior`
- `verifies product correctness`
- `endpoint performance proven`
- `outage cause`
- `safe to release`
- `release approved`
- `operationally safe`
- `complete coverage`
- `no impact`
- absence-of-impact wording
- `AI-powered impact analysis`
- `embeddings`
- `vector database analysis`
- `prompt-based classification`
- `autonomous approval`
- `automated management decision`
- `replaces telemetry`

Forbidden terms may appear inside explicitly bounded non-claim,
forbidden-wording, limitation, question-title, or stop-condition contexts
when the sentence clearly says TraceMap does not make that claim.
Conclusion verbs such as `confirms` and `verifies` are allowed only when they
refer to validation evidence, static-field presence, route presence, or other
bounded checks, not runtime behavior, product correctness, release safety,
operational safety, absence of impact, or complete coverage.

## Validation Design

Focused validation should use structured HTML parsing where practical. It
should verify:

- visible `Public claim level: concept`;
- visible `No public conclusion without evidence`;
- selected placement and rejected alternatives recorded in
  `implementation-state.md`;
- required manager question matrix rows;
- required matrix fields for every row;
- required proof path anatomy fields;
- exact evidence tier vocabulary;
- coverage-label preservation and no upward normalization of reduced,
  partial, unknown, unavailable, syntax-only, private-only, future-only, or
  gap-labeled states;
- next-owner routing uses public role categories, treating the listed owner
  roles as an open public-role vocabulary rather than private people, private
  teams, or a brittle closed allowlist;
- standalone metadata, discovery metadata, sitemap metadata, and route index
  metadata when standalone;
- section anchors and host-page title, description, canonical URL, Open Graph
  title, Open Graph description, discovery summary, and sitemap or route-index
  compatibility when sectioned;
- adjacent-route links resolve or are recorded as deferrals/substitutions;
- no unsupported runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM, embedding,
  vector-database, prompt-classification, autonomous-approval,
  automated-management-decision, replacement, conclusion-verification verbs,
  or absence-of-impact claims outside bounded rejection contexts;
- no forbidden private/raw material in rendered text, decoded HTML, raw HTML,
  attributes, alt text, captions, metadata, fixtures, tests, sitemap output,
  discovery output, or generated pages;
- illustrative rule IDs, commit-like values, extractor versions, claim
  labels, packet labels, and owner labels are visibly illustrative;
- full row text is present in static HTML even if the page uses responsive
  cards, details elements, or accordions;
- desktop and mobile browser sanity checks pass for layout or interaction
  changes.

Focused validation should be wired into the aggregate site validation command
so `npm run validate` exercises the future manager proof-path surface.
