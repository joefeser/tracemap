# Site TraceMap Tools Stakeholder Question Index Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe question index page or section for
`tracemap.tools`. The surface should start with the question a reader is
asking, then route that reader to the right TraceMap evidence surface and proof
path without upgrading the claim beyond what public-safe evidence supports.

This is a site-spec-only packet. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, public
copy changes, AI/LLM behavior, runtime telemetry collection, or agent
automation.

The future surface is an orientation index, not a new proof claim. It should
help managers, engineers, reviewers, architects, incident participants,
modernization planners, and agents or bots find the right public-safe evidence
surface while preserving TraceMap's deterministic static evidence boundaries.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` for the page or section unless a future
implementation phase identifies current public-safe evidence that clearly
supports `demo` for the exact question-index content. The expected initial
surface is navigational and explanatory: it points readers toward existing
proof paths, limitation pages, demo result pages, and concept pages. It does
not itself prove a new capability or publish new demo evidence.

Do not upgrade the page to `demo` merely because some target routes are
demo-backed. A row may link to a demo-backed evidence surface, but the
question index remains concept-level unless the index itself has checked-in
public-safe demo evidence and a separate claim-level decision records that
upgrade.

## Claim Boundaries

- The future surface may describe TraceMap's deterministic static evidence
  vocabulary: rule IDs or rule families, evidence tiers, coverage labels,
  public claim levels, proof paths, limitations, non-claims, commit/source
  status, and generated public-safe artifacts.
- The future surface must not claim runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  AI impact analysis, LLM analysis, prompt-based classification, or complete
  product coverage.
- The future surface must not imply TraceMap replaces managers, service
  owners, architects, tests, telemetry, logs, traces, source review, code
  review, incident command, release review, human review, or human judgment.
- The future surface must not publish raw `facts.ndjson`, raw `index.sqlite`,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, private
  sample names, raw telemetry payloads, hidden validation details, or hidden
  private-work details.
- The future surface must not use `impacted` as a conclusion unless the row is
  explicitly tied to a reducer-backed public-safe result and proof path.
  Otherwise use static wording such as `question`, `surface`, `reference`,
  `evidence trail`, `review input`, `gap`, or `needs review`.

## Relationship to Existing Site Surfaces

The stakeholder question index is an orientation surface. It routes a reader
from question shape to proof surface; it does not replace or duplicate those
surfaces.

It should complement these existing or candidate public-safe routes when they
exist at implementation time:

- `/manager-packet/` for manager-facing summary packets.
- `/use-cases/endpoint-review/` for endpoint or change-review orientation.
- `/incident-evidence-handoff/` or current incident-adjacent evidence handoff
  route for incident participant handoff wording.
- `/legacy-modernization/evidence-map/` for modernization planning evidence
  orientation.
- `/proof-paths/` for proof trail indexing.
- `/proof-source-catalog/` for route-to-source and proof-source mapping.
- `/review-claim-checklist/` for claim-checking ritual and repeatability.
- `/static-vs-runtime/` for static evidence versus runtime telemetry
  boundaries.
- `/demo/result/` for public demo result evidence when applicable.
- `/vault-export/` for public-safe export concept or demo boundaries when
  applicable.
- `/limitations/` for non-claims, partial coverage, and public boundary
  language.
- `/validation/` for validation and generated-site checking boundaries.

If a candidate target route does not exist at implementation time, the
implementation must either select the current equivalent live route, defer that
row or link, or record the substitution and rationale in
`implementation-state.md`. Dead links are not acceptable proof paths.

## Requirements

### Requirement 1: Choose route or placement conservatively

The future implementation shall choose a public route or section placement for
the question index without implying a shipped analysis capability.

Acceptance criteria:

- The implementation chooses one of `/questions/`,
  `/use-cases/questions/`, a section on `/use-cases/`, or an equivalent
  public-safe placement, then records the selected placement and rejected
  alternatives in `implementation-state.md`.
- The recorded route decision explains why the selected placement is an
  orientation/index surface rather than a new proof claim, capability matrix,
  claim ledger, or FAQ replacement.
- The page or section says `Public claim level: concept` unless a spec
  amendment records an evidence-backed `demo` upgrade.
- The page or section states the shared site principle: No public conclusion
  without evidence.
- The page or section avoids top-navigation placement unless an
  implementation-time information-architecture review records why that
  placement fits existing site navigation patterns.
- If implemented as a standalone route, route metadata, sitemap metadata, and
  discovery metadata use `publicClaimLevel: concept` unless the documented
  claim-level decision has been upgraded.

### Requirement 2: Start each row with a reader question

The future surface shall organize rows around stakeholder questions, not
TraceMap feature names or internal artifact names.

Acceptance criteria:

- Each question row includes audience, safe public wording, target evidence
  surface, public claim level, proof path, limitation, and non-claim.
- Each question row also includes a safe answer shape describing what the
  target evidence may help the reader inspect, without turning that inspection
  into a conclusion.
- The required row schema is:
  audience, question, safe answer shape, target route, evidence surface,
  public claim level, proof path, limitation, and non-claim.
- The index includes at least one row for each required question family:
  manager planning, engineer endpoint/change review, incident-adjacent
  handoff, modernization planning, reviewer claim checking, demo evaluation,
  proof-source inspection, and agent/bot discovery.
- The question text is phrased as something a reader might ask before choosing
  a proof surface, not as a statement that TraceMap has already answered the
  question.
- The safe answer shape uses bounded verbs such as `inspect`, `compare`,
  `follow`, `review`, `check`, `route to`, `look for`, `record`, or
  `escalate`, and avoids unsupported verbs such as `prove`, `guarantee`,
  `certify`, `approve`, `replace`, or `resolve`.
- The row does not require raw local artifacts, private samples, hidden
  validation details, or raw scanner output to be public proof.

### Requirement 3: Include the required question families

The future implementation shall include a question matrix that covers the
primary public-safe audiences without overclaiming what TraceMap can answer.

Acceptance criteria:

- Manager planning row: routes managers toward the current manager packet,
  use-case, limitation, or proof-path surface for planning conversations. The
  row does not claim staffing decisions, release readiness, operational
  safety, priority, or ownership decisions are automated.
- Engineer endpoint/change review row: routes engineers toward the current
  endpoint-review, proof-path, demo-result, or validation surface. The row does
  not claim a change is safe, unsafe, approved, blocked, production-proven, or
  fully impacted.
- Incident-adjacent handoff row: routes incident participants toward the
  current incident evidence handoff, static-versus-runtime, validation, or
  limitations surface. The row does not claim outage cause, incident timeline,
  runtime behavior, traffic, endpoint performance, or incident command
  authority.
- Modernization planning row: routes architects or planners toward the current
  legacy-modernization evidence map, proof-path, validation, or limitations
  surface. The row does not claim complete migration scope, complete
  dependency understanding, production safety, or replacement for architects
  and service owners.
- Reviewer claim-checking row: routes reviewers toward the claim checklist,
  proof paths, proof-source catalog, validation, and limitations surfaces. The
  row does not allow repeating a claim after dropping its rule ID or rule
  family, evidence tier, coverage label, proof path, limitation, or non-claim.
- Demo evaluation row: routes demo readers toward `/demo/result/`,
  `/proof-paths/`, `/validation/`, and `/limitations/` or current equivalents.
  The row does not claim the demo proves private repo behavior, runtime
  behavior, release safety, or complete product coverage.
- Proof-source inspection row: routes proof-oriented readers toward the proof
  source catalog, proof paths, validation, and limitations surfaces. The row
  does not publish raw facts, raw SQLite, analyzer logs, source snippets, raw
  SQL, config values, private samples, raw remotes, or generated scan
  directories.
- Agent/bot discovery row: routes crawlers and automated reviewers toward
  discovery metadata, sitemap entries, proof paths, validation, limitations,
  and claim-checking surfaces. The row tells agents not to repeat claims
  after dropping evidence fields, limitations, or non-claims, and does not
  imply autonomous approval or AI impact analysis.

### Requirement 4: Preserve public claim levels per row

The future surface shall treat row-level public claim level as a routing and
wording constraint, not a proof upgrade.

Acceptance criteria:

- Page-level `Public claim level` is visible and defaults to `concept`.
- Each row includes a `Public claim level` value using the site's current
  public claim-level vocabulary at implementation time.
- For this spec's initial implementation guidance, rows should use `concept`
  unless current public-safe evidence clearly supports `demo` for the linked
  evidence surface and answer shape.
- A row that links to a `demo` surface may still use `concept` if the row's
  wording is explanatory, future-facing, or not independently demo-backed.
- If any row uses `demo`, the implementation records the exact public-safe
  evidence and route that support that row-level claim level in
  `implementation-state.md`.
- The matrix distinguishes page-level claim level from target-route claim
  level so readers and bots do not infer that all linked surfaces share the
  same evidence maturity.
- Dev-only, future-only, hidden, partial, reduced, unknown, or unavailable
  proof states remain visible as limitations and cannot be normalized into
  stronger wording.

### Requirement 5: Keep proof paths and limitations attached

The future surface shall preserve the evidence fields a reader needs before
repeating or acting on a public claim.

Acceptance criteria:

- Each row names or links to a proof path that resolves to an existing
  public-safe route, public-safe generated summary, documentation page, demo
  artifact, or validation/limitation surface.
- Proof paths point to public-safe summaries or pages, not raw `facts.ndjson`,
  raw `index.sqlite`, analyzer logs, raw snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw telemetry payloads, or hidden
  validation details.
- Rows use specific rule IDs where public-safe and relevant; otherwise they
  use a rule family or evidence-surface label plus a limitation explaining why
  a specific rule ID is unavailable or not public-safe.
- Evidence tiers use TraceMap vocabulary when the target proof surface exposes
  tiers: `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown`.
- Coverage labels are transcribed from the target public-safe artifact or
  route when available and are not normalized into stronger site-only wording.
- Each limitation is specific enough to prevent overclaiming the row's safe
  answer shape.
- Each non-claim says what the row must not imply, such as runtime proof,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI/LLM impact analysis, or complete coverage.
- A row may use `impacted` only if the proof path links to a public-safe
  reducer output that includes rule IDs, evidence tiers, coverage labels, and
  an explicitly published claim level of `demo` or higher. A row that links
  only to concept-level, orientation, or explanation pages must use
  static-reference wording instead.

### Requirement 6: Add public-safe metadata and discovery support

If implemented as a standalone route, the future surface shall be discoverable
by people, search engines, and agents without publishing private material or
inflating the claim level.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph fields using concept-level wording.
- Sitemap metadata includes the route when comparable public concept pages are
  indexed there.
- Discovery metadata includes `publicClaimLevel: concept`, a bounded summary,
  preferred proof path, limitations, and non-claims using the current
  discovery schema.
- Discovery metadata and `llms.txt` or bot-oriented surfaces, if updated, tell
  agents to preserve proof path, rule ID or rule family, evidence tier,
  coverage label, limitation, non-claim, and public claim level when repeating
  any question-index row.
- Metadata does not include private sample names, raw artifact paths, local
  absolute paths, raw remotes, generated scan directories, hidden feature
  details, or unsupported runtime/release/AI claims.
- If implemented as a section instead of a standalone route, the host page's
  metadata remains concept-level or more conservative and does not imply the
  section is a shipped analyzer capability.

### Requirement 7: Validate rows, links, claims, and private-material safety

The future implementation shall add focused validation so the question index
remains a safe orientation surface.

Acceptance criteria:

- Focused validation confirms required rows or row families are present:
  manager planning, engineer endpoint/change review, incident-adjacent
  handoff, modernization planning, reviewer claim checking, demo evaluation,
  proof-source inspection, and agent/bot discovery.
- Focused validation confirms every row includes audience, question, safe
  answer shape, target route, evidence surface, public claim level, proof
  path, limitation, and non-claim.
- Focused validation confirms required candidate links resolve where included:
  `/manager-packet/`, `/use-cases/endpoint-review/`,
  `/incident-evidence-handoff/`, `/legacy-modernization/evidence-map/`,
  `/proof-paths/`, `/proof-source-catalog/`,
  `/review-claim-checklist/`, `/static-vs-runtime/`, `/demo/result/`,
  `/vault-export/`, `/limitations/`, and `/validation/`, or records
  implementation-time substitutions and omissions in `implementation-state.md`.
- Focused validation checks rendered text, decoded HTML, raw HTML attributes,
  alt text, captions, metadata, sitemap/discovery output, fixtures, test
  files, and any bot-oriented discovery surfaces for forbidden runtime,
  production, endpoint-performance, outage-cause, release-safety,
  operational-safety, AI/LLM, complete-coverage, and unsupported `impacted`
  wording.
- Validation must not flag a forbidden term that appears only inside an
  explicitly bounded non-claim or limitation statement, such as a sentence
  that says a row does not claim outage cause. The check must distinguish
  claim-denying context from claim-asserting context.
- Focused validation checks rendered text, decoded HTML, raw HTML attributes,
  metadata, sitemap/discovery output, and any bot-oriented discovery surfaces
  for raw or private material prohibited by this spec.
- Link validation confirms proof paths and target routes resolve in generated
  site output.
- Route metadata validation confirms `publicClaimLevel: concept` for the
  standalone route or confirms the host route remains concept-level or more
  conservative for section placement.
- Implementation validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, `npm test` from `site/`,
  `npm run validate` from `site/`, and `npm run build` from `site/`.
- Desktop and mobile browser sanity checks are run when route, layout, or
  interaction changes are made.
- The implementation updates `implementation-state.md` with route decisions,
  substitutions, validation results, review findings, claim-boundary decisions,
  oddities, and follow-up items.
