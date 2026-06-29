# Site TraceMap Tools Review Claim Checklist Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe reviewer checklist page or section for
`tracemap.tools` that turns TraceMap's claim boundary into a practical review
ritual. Before a public claim or internal review statement is repeated, a
reviewer should check claim level, proof path, rule ID or rule family, evidence
tier, coverage label, limitations, non-claims, source branch or main-dev
status, and owner follow-up.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, or public
copy changes.

The audience is reviewers, managers, agents, and engineers preparing public or
internal summaries. The future surface should be small, repeatable, and
decision-oriented: it helps a person decide whether a sentence may be repeated,
needs a proof link, must be downgraded, or needs an owner follow-up.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or section should use `Public claim level: concept` unless
  existing public proof at implementation time supports a stricter demo-level
  upgrade through a separate spec amendment.
- The checklist may explain a review ritual for public-safe claim checking,
  but it must not claim that TraceMap proves runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, or complete product coverage.
- The checklist must not publish raw `facts.ndjson`, raw `index.sqlite`,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, or
  private sample names.
- The checklist may name artifact families such as fact streams, SQLite
  indexes, reports, rule catalog entries, commit metadata, coverage labels, and
  documented limitations as source-of-truth categories, but public links must
  resolve only to public-safe summaries, public pages, documentation, or demo
  artifacts.
- The future page must not turn a checklist outcome into an impact conclusion.
  It may say a statement is ready to repeat, needs review, must be downgraded,
  or needs an owner, but it must not say a system is impacted, safe, unsafe,
  approved, blocked, root cause, validated for release, or production proven
  except inside explicit non-claim examples.

## Relationship to Existing Site Surfaces

The reviewer checklist is a ritual surface, not a new evidence catalog. It
complements:

- `site-tracemap-tools-evidence-review-room`, which frames the meeting agenda
  for known, partial, and missing evidence.
- `site-tracemap-tools-manager-faq`, which answers stakeholder questions about
  what TraceMap can and cannot say.
- `site-tracemap-tools-proof-path-index`, which organizes public-safe evidence
  trails by artifact, rule ID, evidence tier, coverage label, proof path, and
  limitation.
- `site-tracemap-tools-claim-ledger`, which governs public claim wording and
  claim-level vocabulary.

The future implementation must cross-link to those surfaces when they exist,
but it must not duplicate their tables or restate their full evidence catalogs.
The checklist should answer "may this sentence be repeated, and what must stay
attached to it?" while the review room answers "what are we discussing?", the
manager FAQ answers "what does this mean?", the proof path index answers "where
is the public-safe proof?", and the claim ledger answers "what level may this
claim use?"

## Requirements

### Requirement 1: Define route and placement

The future implementation shall add a public-safe checklist page or section
using an explicit route and placement decision.

Acceptance criteria:

- The implementation chooses `/review-claim-checklist/`, `/claim-checklist/`,
  or an equivalent section on an existing governance page, then records the
  selected placement and rejected alternates in `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section states the shared site principle: No public conclusion
  without evidence.
- The page or section is discoverable from relevant governance or proof
  surfaces, including `/review-room/`, `/manager-faq/`, `/proof-paths/`, and
  the claim ledger route or roadmap claim-ledger surface when those routes
  exist.
- When implemented as a standalone route, at least one existing governance or
  proof surface that is live in the implementation branch links to the
  checklist using bounded anchor text. Concept-stage surfaces that do not yet
  exist are recorded as deferred inbound links in `implementation-state.md`
  rather than left as dead links.
- If no adjacent governance or proof surface is live in the implementation
  branch, the implementation either adds the inbound link from another existing
  live governance, proof, or docs surface, or chooses section placement on an
  existing live page, then records the decision in `implementation-state.md`.
- Cross-links use bounded anchor text that does not imply the checklist proves
  runtime behavior, production traffic, release safety, operational safety, or
  complete product coverage.
- If implemented as a standalone route, the route is added to sitemap metadata
  and discovery metadata using existing site patterns.
- The page or section is not added to top navigation unless the implementation
  records why the existing site navigation pattern supports it.

### Requirement 2: Publish the checklist ritual

The future page shall present the claim-checking ritual as a scannable,
repeatable checklist.

Acceptance criteria:

- The checklist includes these required fields for each claim under review:
  claim statement, public claim level, proof path, rule ID or rule family,
  evidence tier, coverage label, limitation, non-claims, source branch or
  main-dev status, owner follow-up, reviewer, review date, and decision
  (review outcome).
- The `limitation` field is singular per checklist row by design; a row may
  summarize multiple limitations there, but validation treats `limitation` as
  the canonical field label.
- The checklist-row public claim level field uses the claim-ledger vocabulary
  only: `shipped`, `demo`, `concept`, or `hidden`.
- Checklist-row claim levels are not discovery metadata values. If the future
  implementation emits route or discovery metadata, `publicClaimLevel` must use
  the existing discovery enum (`main`, `demo`, `concept`, `planned`,
  `dev-only`, `hidden`, or `future`) and map discovery `main` to checklist
  `shipped` in the rendered checklist or implementation-state note.
- The decision field uses only these labels: `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- The canonical review outcome labels are `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`; requirements, tasks, copy, and validation use those exact
  labels when they need machine-checkable wording.
- The owner follow-up field records the responsible owner and requested action;
  `owner follow-up needed` is the review outcome state when the claim cannot be
  repeated until that owner action happens.
- The per-row non-claims field records what the specific claim must not imply;
  the page-level non-claims section records TraceMap-wide boundaries that apply
  to every row.
- The checklist distinguishes public claims from internal review statements.
  Internal statements still require evidence before being repeated outside
  their original context.
- The page explains that a claim with no proof path, no rule ID or rule family,
  no evidence tier, or no coverage label cannot be upgraded by confidence,
  seniority, repetition, or manager pressure.
- The page explains that a `concept` checklist is not evidence that the
  underlying product behavior is shipped.
- The checklist includes a short "stop conditions" group for missing proof
  path, private-only artifact, hidden claim detail, unsupported demo claim, or
  forbidden runtime/release/AI wording.
- Any illustrative example claim rows on the page use synthetic or
  already-public/demo-sourced content only, are labeled as examples, and must
  not reproduce real internal claims, private sample names, dev-only or hidden
  capability names, counts, cadence, sequencing, real internal reviewer or
  owner identities, or real internal review dates. Reviewer, owner follow-up,
  and review-date values in illustrative rows use synthetic placeholders such
  as role labels or example dates rather than real names or real dates.

### Requirement 3: Preserve proof-path and evidence vocabulary

The future page shall keep every repeatable statement attached to public-safe
proof vocabulary.

Acceptance criteria:

- Proof path entries link to public-safe pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, demo artifacts, or a
  proof path index entry.
- Proof path entries do not link directly to raw facts, raw SQLite files, raw
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, or private sample
  names.
- Rule IDs are required when public-safe and specific; otherwise the checklist
  requires a rule-family label plus a limitation explaining why a specific rule
  ID is not public-safe or not available.
- Evidence tiers use only the TraceMap tier vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Coverage labels are transcribed from the cited public-safe artifact or
  summary and are not silently normalized into stronger wording.
- A reduced, partial, unknown, unavailable, or future-only coverage label
  remains visible in the claim row and forces either downgrade or owner
  follow-up unless an existing public-safe proof surface documents why the
  statement can still be repeated.
- The checklist states that source branch or main-dev status is part of the
  proof path: `main` or checked-in public demo evidence can support stronger
  wording than dev-only, future-only, hidden, or local-only evidence.

### Requirement 4: Define public-safe claim levels and outcomes

The future page shall keep claim level and review outcome separate so the
checklist cannot upgrade a claim by process alone.

Acceptance criteria:

- Public claim level says how strongly the site may present the claim:
  `shipped`, `demo`, `concept`, or `hidden`.
- Review outcome says what the reviewer should do next using the canonical
  labels from Requirement 2: `repeat with proof`, `downgrade before repeating`,
  `owner follow-up needed`, `do not repeat`, or `internal only`.
- The page says a successful checklist does not by itself prove runtime
  behavior, release safety, operational safety, or complete coverage.
- `shipped` requires a public-safe proof path to main-true behavior or
  documentation plus limitations.
- `demo` requires checked-in public demo proof or public-safe generated
  summaries plus limitations.
- `concept` covers future-facing, dev-only, not-yet-backed, or review-process
  guidance that must not be described as shipped or demo-backed.
- `hidden` is abstracted or omitted from public detail and must not disclose
  unreleased capability names, private sample identities, internal route names,
  hidden-export details, counts, cadence, sequencing, or in-flight status.
- The page says private-only evidence can support internal follow-up but cannot
  be cited as public proof until summarized through a public-safe route or
  artifact.

### Requirement 5: State non-claims and private-material prohibitions

The future page shall make forbidden claims and forbidden publication material
visible enough for reviewers, managers, agents, and engineers to catch them
before reuse.

Acceptance criteria:

- The page includes an explicit non-claims section.
- The non-claims section says TraceMap does not prove runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI impact analysis, LLM analysis, or complete product
  coverage.
- The page says TraceMap does not replace telemetry, logs, traces, tests,
  source review, ownership decisions, incident response, or release approval.
- The page includes a private-material checklist that forbids raw
  `facts.ndjson`, raw `index.sqlite`, analyzer logs, raw source snippets, raw
  SQL, config values, secrets, local absolute paths, raw repository remotes,
  generated scan directories, and private sample names.
- The page must not publish real internal reviewer names, real owner or
  assignee identities, or real internal review dates or cadence in example or
  template rows; person fields use synthetic role-based placeholders.
- The page explains that generated artifact families may be named as local
  source-of-truth categories, but raw artifacts are not public page content.
- The page includes agent-oriented wording that a future agent must not repeat
  a claim after dropping its rule ID, evidence tier, coverage label,
  limitation, or proof path.

### Requirement 6: Differentiate from adjacent pages

The future implementation shall make the checklist's job clear relative to
existing public review, FAQ, proof, and ledger pages.

Acceptance criteria:

- The page links to `/review-room/` as the meeting agenda, not as proof that a
  claim is repeatable.
- The page links to `/manager-faq/` as stakeholder explanation, not as the
  source of proof.
- The page links to `/proof-paths/` as the evidence-trail index when that
  route exists.
- The page links to the claim ledger or roadmap claim-ledger surface as the
  source for claim-level vocabulary when that route or section exists.
- The page includes a short differentiation note explaining that this checklist
  is for repeat-before-reuse review and does not replace evidence catalogs,
  FAQ answers, or meeting agendas.
- The future implementation avoids copying large tables or full answer sets
  from adjacent pages; it links instead.
- If a referenced route does not exist at implementation time, the implementer
  records the substitution, omission, or blocking decision in
  `implementation-state.md` and avoids creating dead links.

### Requirement 7: Add metadata, anchors, and validation

The future implementation shall expose enough metadata and anchors for readers,
link checkers, and future automated claim-review tools.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical path,
  Open Graph fields where existing concept pages use them, and a
  `publicClaimLevel: concept` discovery signal.
- Discovery metadata uses existing site vocabulary for source type, hint
  category, preferred proof path, limitations, and non-claims where comparable
  pages provide those fields.
- Checklist items or sections use stable anchors suitable for cross-links and
  future automated review references.
- Validation asserts the rendered page includes `Public claim level: concept`,
  `No public conclusion without evidence`, the required checklist fields,
  required non-claims, and required adjacent-page links that exist in the
  implementation branch.
- Validation confirms that, for a standalone route, at least one existing
  adjacent governance or proof surface in the implementation branch links to
  the checklist route.
- If no adjacent governance or proof surface is live, validation confirms the
  fallback inbound link or section-placement decision recorded in
  `implementation-state.md`.
- Validation checks that example rows do not contain private sample names or
  other forbidden private/raw material, real internal reviewer or owner
  identities, or real internal review dates, and that example rows are marked
  as illustrative.
- Validation checks rendered text, decoded HTML, and metadata for forbidden
  runtime, production, release-safety, operational-safety, AI, and LLM
  positioning.
- Validation checks rendered text, decoded HTML, and metadata for forbidden
  private/raw material listed in this spec.
- Validation confirms the route appears in sitemap and discovery metadata if
  implemented as a standalone route, and that discovery metadata exposes
  `publicClaimLevel: concept`.
- Validation runs `git diff --check` and `./scripts/check-private-paths.sh`.
- Validation confirms `implementation-state.md` records route decisions,
  validation results, Kiro review outcomes, and any unresolved follow-up items.
- Site build, site validation, and browser sanity checks are required in the
  future implementation phase because that phase changes site source.

### Requirement 8: Keep spec state current

The future implementation shall keep this spec's status files aligned with the
actual work completed.

Acceptance criteria:

- `tasks.md` remains unchecked in this spec-only PR.
- Future implementation checks a task only after the corresponding site change
  and validation work are complete.
- `implementation-state.md` records current branch, scope decisions, public
  claim level, route decisions, validation results, Kiro review outcomes,
  oddities, and follow-up items.
- If Kiro review finds Medium or higher issues, the spec or implementation is
  patched and re-reviewed where feasible before readiness is treated as final.
- If required local review tools are unavailable, the exact command and error
  are recorded in `implementation-state.md`.
