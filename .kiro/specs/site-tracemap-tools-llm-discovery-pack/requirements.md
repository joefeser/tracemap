# Site TraceMap Tools LLM Discovery Pack Requirements

Public claim level: demo
Status: ready-for-implementation / not started

## Objective

Create a queued site phase for bot- and LLM-friendly discovery surfaces on
`tracemap.tools`. The feature should help crawlers, documentation agents, and
future assistants find public-safe TraceMap pages, repository docs, demo proof
surfaces, and navigation hints without implying that TraceMap performs AI impact
analysis.

This PR is spec/runway only and does not implement site source, generated
outputs, scanner code, reducer code, LLM calls, embeddings, vector databases, or
prompt-based classification.

## Shared Site Principle

No public conclusion without evidence.

Any future public discovery surface must route readers to evidence-backed pages,
rule IDs, evidence tiers, coverage labels, limitations, and generated artifacts
where those claims already exist. Discovery metadata may summarize routes, but
it must not promote stronger conclusions than the linked evidence supports.

## Non-Claims

- Discovery metadata only.
- No AI impact-analysis claims.
- No LLM, embedding, vector database, or prompt-based classification features
  in the TraceMap scanner, reducer, or core product.
- No claim that static evidence proves runtime traffic, production usage,
  deployment state, release approval, endpoint performance, or absence of
  impact under reduced coverage.
- No publication of private paths, raw source snippets, raw SQL, config values,
  secrets, raw fact streams, SQLite databases, analyzer logs, or local output
  roots.

## Main/Dev Wording Boundary

The future implementation should distinguish public proof already present on
`main` from queued or in-flight work on `dev`.

- Public-facing discovery files may describe `main` evidence as available when
  linked public pages or repository docs already support the statement.
- `dev`-only pages, specs, or implementation notes may be described as planned,
  queued, or in progress, not shipped public proof.
- If a route points to future implementation work, the discovery copy must label
  it as a roadmap or implementation target rather than a completed capability.
- Public copy must keep TraceMap framed as deterministic static evidence, even
  when the audience is bots or LLM-based tools.

## Requirements

### Requirement 1: Publish an llms.txt discovery entry point

**User Story:** As a documentation agent or crawler, I want a concise public
entry point that tells me where to find TraceMap's safe public docs, proof pages,
and limitations.

Acceptance criteria:

- WHEN `https://tracemap.tools/llms.txt` is fetched THEN it SHALL return a
  static text file from the published site.
- The file SHALL identify TraceMap as a deterministic static evidence tool, not
  an AI impact-analysis product.
- The file SHALL link to public-safe site pages for evidence, outputs,
  validation, limitations, demo proof, capabilities, docs, and roadmap where
  those pages exist.
- The file SHALL include a short non-claims section covering runtime behavior,
  production usage, release approval, and AI impact-analysis boundaries.
- The file SHALL prefer stable public URLs over implementation branch URLs.

### Requirement 2: Add concise machine-readable docs indexes

**User Story:** As a bot or future agent, I want small machine-readable indexes
so I can choose the right public page or repository document without crawling
the whole site.

Acceptance criteria:

- WHEN the site is built THEN it SHALL publish `/docs-index.json` for
  source-of-truth repository documents that are safe to expose publicly.
- WHEN the site is built THEN it SHALL publish `/routes-index.json` for
  public-safe site route discovery.
- Each index SHALL include stable paths or URLs, titles or labels, public claim
  levels where applicable, and short summaries.
- The indexes SHALL identify source-of-truth repository docs separately from
  site presentation pages.
- The indexes SHALL include limitations or non-claims metadata for entries that
  describe evidence, demo results, roadmap items, or proof packets.
- The indexes SHALL avoid private implementation details and raw generated
  artifacts that are not already public-safe.

### Requirement 3: Provide public-safe navigation hints

**User Story:** As a crawler or documentation agent, I want route hints that
lead me to the safest explanation before I summarize TraceMap.

Acceptance criteria:

- WHEN route hints are generated THEN they SHALL prefer evidence and limitation
  pages before promotional or roadmap pages.
- WHEN a page describes a public conclusion THEN the hints SHALL point to the
  supporting evidence page, generated public-safe artifact, or source-of-truth
  repository document where available.
- WHEN a route is demo-level THEN the hints SHALL preserve `Public claim level:
  demo` and avoid stronger production or release claims.
- WHEN a route is concept-level, hidden, planned, or future-only THEN the hints
  SHALL label that status plainly.
- WHEN a bot asks what TraceMap cannot prove THEN the hints SHALL route to
  limitations and non-claims before use-case copy.

### Requirement 4: Preserve static site and product boundaries

**User Story:** As a maintainer, I want discovery metadata to stay static,
reviewable, and aligned with the TraceMap product boundary.

Acceptance criteria:

- The future implementation SHALL use static files or build-time generation
  only and SHALL NOT add runtime services.
- The future implementation SHALL NOT add product LLM calls, embeddings, vector
  databases, prompt-based classification, or AI impact-analysis workflows.
- The future implementation SHALL NOT edit scanner, reducer, language adapter,
  or report generation code.
- The future implementation SHALL keep generated discovery output in
  `site/dist` only and source metadata under `site/src`.
- The future implementation SHALL keep public copy bounded to deterministic
  static evidence: rule IDs, evidence tiers, coverage labels, limitations, and
  generated artifacts.

### Requirement 5: Validate discovery output and safety boundaries

**User Story:** As a reviewer, I want automated and manual checks that prove the
discovery pack is crawlable, bounded, and public-safe.

Acceptance criteria:

- WHEN validation runs THEN site tests and build or validation scripts SHALL
  cover the new discovery outputs.
- WHEN `llms.txt` and machine-readable indexes are generated THEN tests SHALL
  verify expected routes, claim-level labels, and non-claims.
- WHEN the discovery text is reviewed THEN it SHALL not contain AI
  impact-analysis claims or imply LLM features in core TraceMap.
- WHEN sitemap or robots metadata should expose the new entry points THEN the
  implementation SHALL update the generated metadata through existing site
  patterns.
- The implementation-state note SHALL record branch, scope decisions,
  validation commands, review findings, and follow-up items.
