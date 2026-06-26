# Site TraceMap Tools Proof Paths For Managers Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

This packet defines the implemented `tracemap.tools` manager-facing proof-path
surface. The page explains proof paths in decision terms and helps managers,
reviewers, engineering leads, and non-implementing stakeholders ask the right
question, find the evidence packet, understand what the packet supports from
deterministic static evidence, understand what it does not prove, preserve the
coverage label, and route the next runtime, product, release, or owner
judgment to the right human owner.

The implementation is limited to public static-site source, metadata, and
validation. It does not implement scanner behavior, reducer behavior,
generated artifacts, runtime monitoring, release automation, approval
workflow, or management-decision automation.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the implemented surface explains how
managers should read and route proof-path evidence. It does not add a new
scanner capability, reducer result, demo artifact, runtime observation,
release decision, product decision, or automated management decision.

The implemented surface must visibly include:

- `Public claim level: concept`
- `No public conclusion without evidence`

Do not upgrade the page or section to `demo` merely because it links to
demo-backed proof paths or packet pages. Links can point to evidence-bearing
surfaces, but this manager-facing explanation remains concept-level unless a
future spec amendment records exact public-safe evidence for a stronger claim.

## Claim Boundaries

The implemented surface may explain TraceMap's deterministic static evidence model:
rule IDs or rule families, evidence tiers, coverage labels, limitations,
analysis gaps, public-safe file paths and line spans, commit or public-safe
source context, extractor versions, proof paths, generated artifact families,
review packets, and next-owner handoffs.

The implemented surface must not claim runtime proof, production traffic,
endpoint performance, outage cause, release safety, operational safety,
complete product coverage, AI impact analysis, LLM analysis, embeddings,
vector databases, prompt-based classification, automated approval, automated
management decisions, or replacement of human review, source review, tests,
telemetry, logs, traces, product judgment, service-owner judgment, release
process, or manager judgment.

The implemented surface must not publish raw source snippets, raw SQL, config
values, secrets, local absolute paths, raw repository remotes, generated scan
directories, raw `facts.ndjson`, raw `index.sqlite`, combined SQLite files,
analyzer logs, hidden validation details, raw command output, private sample
names, private owner names, or credential-like values. Artifact names may be
mentioned only as local output categories or as public-safe summary families,
not as raw published evidence.

## Requirements

### Requirement 1: Choose placement without duplicating adjacent pages

The future implementation shall choose a public route or section placement
that complements existing manager and proof-path surfaces without replacing
them.

Acceptance criteria:

- Evaluate `/proof-paths/for-managers/`, `/manager-proof-paths/`, a section
  on `/manager-packet/`, a section on `/manager-faq/`, and a section on
  `/proof-paths/`.
- `/proof-paths/for-managers/` is the non-binding design recommendation
  because it keeps the page near proof-path education while naming the
  manager-specific decision lens.
- A section on `/manager-packet/` remains allowed when the final content is
  compact and best read as part of the manager packet.
- A section on `/manager-faq/` remains allowed when the final content is
  mostly question-and-answer text rather than a reusable matrix.
- A section on `/proof-paths/` remains allowed when the proof-path overview is
  short enough to carry a manager-specific reading path.
- The implementation records the selected placement, rejected alternatives,
  and rationale in this spec's `implementation-state.md`.
- Any placement outside the named candidates requires a spec amendment or an
  explicit implementation-state entry before implementation begins, including
  why the named candidates no longer fit the site's information architecture.
- The selected page or section visibly says `Public claim level: concept`.
- The selected page or section visibly says `No public conclusion without
  evidence`.
- The selected placement does not replace `/manager-brief/`,
  `/manager-faq/`, `/manager-packet/`, `/proof-paths/`,
  `/proof-paths/faq/`, `/proof-paths/tour/`, `/packets/`, or
  `/packets/assembly/`.
- The selected placement is not added to primary navigation unless a future
  information-architecture note records why a concept-level manager proof
  path belongs there.

### Requirement 2: Distinguish this surface from neighboring pages

The future surface shall define its role relative to existing manager,
review-packet, and proof-path pages.

Acceptance criteria:

- It distinguishes itself from `/manager-brief/` by translating proof paths
  into decision questions and owner routing, while the brief remains the
  high-level manager value framing.
- It distinguishes itself from `/manager-faq/` by using a reusable question
  matrix and evidence-packet anatomy, while the FAQ remains a concise answer
  set for repeated stakeholder questions.
- It distinguishes itself from `/manager-packet/` by explaining how to read
  and route proof paths, while the manager packet remains the summary of what
  TraceMap helps teams discuss.
- It distinguishes itself from `/packets/` and `/packets/assembly/` by
  describing manager-facing proof interpretation, while packet pages remain
  artifact and handoff assembly references.
- It distinguishes itself from `/proof-paths/` by adding the manager and
  reviewer decision lens, while `/proof-paths/` remains the canonical proof
  path overview.
- It distinguishes itself from `/proof-paths/faq/` by focusing on decision
  routing, owner handoff, and coverage-label consequences rather than general
  proof-path questions.
- It distinguishes itself from `/proof-paths/tour/` by acting as a reference
  matrix, while the tour remains a guided reading flow.
- It distinguishes itself from `/proof-source-catalog/` by routing managers
  and reviewers to publication decisions, while the catalog remains the
  source-to-public-surface inventory for public-safe proof material.
- It links to adjacent pages only when the route exists at implementation
  time or a recorded public-safe substitute exists.
- Missing adjacent routes are recorded as deferrals or substitutions in
  `implementation-state.md`; dead links are not acceptable.

### Requirement 3: Include a manager question matrix

The future surface shall include a manager question matrix that turns common
decision questions into bounded evidence checks and next-owner routing.

Acceptance criteria:

- The matrix includes these row fields: manager or reviewer question,
  evidence packet to inspect, what static evidence can support, what it does
  not prove, coverage-label consequence, stop condition, next owner, and
  supporting public route.
- The matrix includes a row for "What changed in the code path we are
  reviewing?"
- The matrix includes a row for "What evidence supports repeating this
  claim?"
- The matrix includes a row for "What does reduced or partial coverage mean
  for this decision?"
- The matrix includes a row for "Who should answer runtime or product
  behavior next?"
- The matrix includes a row for "Can this evidence approve, block, or
  certify a release?"
- The matrix includes a row for "Can this evidence explain production
  traffic, endpoint performance, or outage cause?"
- The matrix includes a row for "Can this evidence be shared publicly?"
- The matrix includes a row for "What should happen when evidence is missing,
  private-only, syntax-only, or unknown?"
- Each row keeps `Public claim level: concept` or a stricter row-level label.
  A row may reference a stronger claim level only if it links to an exact
  public-safe demo-backed proof path and the implementation-state note records
  why the linked evidence carries that separate claim level. If that note is
  absent, the row defaults to `concept`.
- Each row uses public role categories for `next owner`, such as manager,
  reviewer, service owner, code owner, architect, runtime observability
  owner, release owner, test owner, product owner, security owner, repository
  owner, build/tooling owner, incident owner, or TraceMap owner.
- The matrix does not imply TraceMap assigns accountability, incident command,
  release authority, staffing, priority, product judgment, or organizational
  decision rights.

### Requirement 4: Explain proof path anatomy in manager terms

The future surface shall include a compact proof path anatomy section that
names the fields a manager or reviewer should preserve before repeating a
claim.

Acceptance criteria:

- The anatomy includes claim or question being reviewed.
- The anatomy includes public claim level.
- The anatomy includes proof path or packet link.
- The anatomy includes rule ID or rule family and documented limitation.
- The anatomy includes evidence tier using only `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- The anatomy includes coverage label and explains it as a boundary on what
  can be repeated.
- The anatomy includes commit or public-safe source context when available.
- The anatomy includes extractor version or schema family when available.
- The anatomy includes public-safe file path and line span when public-safe,
  or an explicit limitation when not public-safe.
- The anatomy includes snippet hash or artifact summary when available and
  public-safe, but not raw snippets by default.
- The anatomy includes generated artifact family, such as
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, or
  `logs/analyzer.log`, only as local output categories unless a sanitized
  public summary exists.
- The anatomy includes limitations, non-claims, unresolved gaps, validation
  evidence, and next owner.
- The section states that dropping any required field weakens or blocks the
  public conclusion.

### Requirement 5: Preserve limitations and non-claims

The future surface shall make limitations and non-claims visible enough that a
manager cannot mistake static evidence for runtime, product, or release
judgment.

Acceptance criteria:

- It states that a proof path can support a static evidence conversation, not
  a runtime conclusion.
- It states that coverage labels such as reduced, partial, unknown,
  unavailable, syntax-only, private-only, future-only, or gap-labeled states
  must remain visible with the claim.
- It states that missing evidence is a gap, downgrade, internal-only note, or
  owner follow-up, not proof of no impact, no dependency, no risk, safety, or
  correctness.
- It states that a manager may use the packet to coordinate review questions,
  but the packet does not make the product, release, runtime, security,
  staffing, priority, or approval decision.
- It states that static evidence does not prove production traffic, runtime
  behavior, endpoint performance, outage cause, release safety, operational
  safety, complete coverage, or customer impact.
- It states that TraceMap core scanner and reducer claims are deterministic
  and evidence-backed, not AI/LLM impact analysis, embeddings, vector
  databases, prompt classification, or confidence scoring.
- It avoids blame language around teams, owners, reviewers, managers,
  vendors, customers, services, or code quality when evidence is missing,
  reduced, or conflicting.

### Requirement 6: Route next-owner judgments explicitly

The future surface shall explain who owns the next question after a static
proof path reaches its boundary.

Acceptance criteria:

- It includes an owner routing section or table.
- It routes runtime behavior, telemetry, live errors, logs, traces, metrics,
  production traffic, and endpoint performance questions to a runtime
  observability owner or service owner.
- It routes product behavior, user workflow, customer impact, priority, and
  product tradeoff questions to a product owner, manager, or service owner as
  appropriate.
- It routes release approval, release blocking, release certification,
  deployment timing, and release safety questions to release owners, test
  owners, reviewers, and the release process.
- It routes test evidence, regression coverage, reproduction, and verification
  questions to test owners and reviewers.
- It routes source interpretation, code ownership, and service architecture
  questions to service owners, code owners, reviewers, or architects.
- It routes raw artifact sharing, secrets, private paths, repository remotes,
  and public-publication questions to security owners, repository owners, or
  TraceMap site owners.
- It routes scanner/reducer limitations, rule catalog gaps, coverage labels,
  extractor versions, and public site claim wording to TraceMap owners or
  reviewers.
- It says TraceMap can point to owner categories, but does not assign
  accountability, organizational authority, incident command, release
  authority, staffing, priority, or product ownership.

### Requirement 7: Block forbidden wording and private material

The future implementation shall validate copy, metadata, examples, fixtures,
and generated output against forbidden overclaims and private/raw material.

Acceptance criteria:

- Forbidden unsupported claim wording includes unqualified `proves`,
  `guarantees`, `certifies`, `approves`, `blocks`, `resolves`, `safe`,
  `unsafe`, `root cause`, `production proven`, `complete coverage`, `no
  impact`, absence-of-impact wording, `release approval`, `release approved`,
  `release safe`, `operationally safe`, `AI impact analysis`, `LLM analysis`,
  `AI-powered`, `embeddings`, `vector database`,
  `prompt-based classification`, `autonomous approval`, `automated
  management decision`, and `replaces telemetry`. Conclusion verbs
  such as `confirms` and `verifies` are also forbidden when they imply
  runtime behavior, product correctness, release safety, operational safety,
  absence of impact, or complete coverage; they are allowed only when clearly
  scoped to validation evidence such as confirming that a static field or
  route is present.
- Forbidden terms may appear only inside explicitly bounded non-claim,
  limitation, forbidden-wording, or stop-condition contexts where the sentence
  clearly says TraceMap does not make that claim.
- Required question titles may contain terms such as release approval,
  production traffic, endpoint performance, outage cause, or missing evidence
  only as bounded questions, not as affirmative claims.
- Forbidden private or raw material includes raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw repository remotes,
  generated scan directories, raw facts streams, raw SQLite content, combined
  SQLite files, analyzer logs, private sample names, private owner names, raw
  command output, hidden validation details, and credential-like values.
- Artifact names such as `facts.ndjson`, `index.sqlite`, and
  `logs/analyzer.log` may appear only as artifact type labels or local-output
  categories, not as raw public content.
- Examples are synthetic, illustrative, or linked to existing public-safe
  demo/proof surfaces.
- Any illustrative rule IDs, commit-like values, extractor versions, claim
  labels, packet labels, or owner labels are marked as illustrative and not
  presented as real TraceMap findings.

### Requirement 8: Add concept-level metadata and discovery safely

The future implementation shall make the chosen page or section discoverable
without overstating claim level or duplicating route responsibilities.

Acceptance criteria:

- If implemented as a standalone route, add title, description, canonical
  metadata, Open Graph metadata, route metadata, discovery metadata, and
  sitemap metadata using concept-level wording.
- If implemented as a section, add a stable anchor and record how the host
  page's title, description, canonical URL, Open Graph title, Open Graph
  description, discovery summary, and sitemap or route-index entry remain
  compatible with a concept-level manager proof-path section.
- Discovery metadata uses `publicClaimLevel: concept` or the equivalent
  existing site field.
- Metadata describes a manager-facing explanation of deterministic static
  proof paths, evidence packets, coverage labels, limitations, and next-owner
  routing.
- Metadata does not claim runtime proof, production proof, endpoint
  performance, outage cause, release safety, operational safety, complete
  coverage, AI/LLM analysis, embeddings, vector databases,
  prompt-based classification, or automated decisions.
- Safe inbound links may be added from `/manager-brief/`, `/manager-faq/`,
  `/manager-packet/`, `/proof-paths/`, `/proof-paths/faq/`,
  `/proof-paths/tour/`, `/proof-source-catalog/`, `/packets/`,
  `/packets/assembly/`, `/questions/`, `/limitations/`,
  `/static-vs-runtime/`, and `/review-claim-checklist/` when those routes
  exist and the link text preserves concept-level boundaries.
- Inbound link text must not upgrade the linked surface's claim level or imply
  proof, approval, automation, runtime knowledge, release safety, complete
  coverage, or management-decision automation.
- The page or section remains out of primary navigation unless the
  implementation-state note records a site information-architecture decision.

### Requirement 9: Validate the future implementation

The future implementation shall run focused and aggregate validation and
record the results in this spec's implementation-state note.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Validate that future implementation tasks are checked only after the
  corresponding implementation work is complete.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- For layout or interaction changes, run desktop and mobile browser sanity
  checks for the selected route or host page.
- Validate visible `Public claim level: concept`.
- Validate visible `No public conclusion without evidence`.
- Validate selected placement, rejected alternatives, and adjacent-route
  substitutions or deferrals are recorded in `implementation-state.md`.
- Validate every required manager question matrix row and every required row
  field.
- Validate proof path anatomy fields, evidence tier vocabulary, coverage-label
  preservation, limitations, non-claims, unresolved gaps, and next-owner
  routing.
- Validate standalone metadata, discovery metadata, sitemap metadata, and
  route index metadata when standalone.
- Validate section anchors and host-page metadata compatibility when
  sectioned.
- Validate forbidden runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM, embedding,
  vector-database, prompt-classification, autonomous-approval,
  automated-management-decision, replacement, conclusion verbs such as
  `confirms` or `verifies` when used for unsupported runtime/product/release
  conclusions, and absence-of-impact wording across rendered text, decoded
  HTML, raw HTML, attributes, alt text, captions, metadata, fixtures, tests,
  sitemap output, discovery output, and generated pages.
- Validate forbidden private/raw material and credential-like values across
  rendered text, decoded HTML, raw HTML, attributes, alt text, captions,
  metadata, fixtures, tests, sitemap output, discovery output, and generated
  pages.
- Validate links resolve only to public-safe routes, documentation,
  public-safe summaries, validation pages, limitations pages, proof-path
  pages, packet pages, or sanctioned demo surfaces.
