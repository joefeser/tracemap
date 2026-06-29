# Site TraceMap Tools Evidence Glossary Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe evidence glossary/reference page for TraceMap
vocabulary, likely at `/glossary/` or `/docs/evidence-glossary/`. The page
will help engineers, reviewers, managers, architects, and agents understand
TraceMap terms before they repeat or automate public claims.

This is a spec-only site phase. It does not implement site code. The future
page is vocabulary and claim-boundary guidance only; deterministic scanner
facts, reducer outputs, reports, rule catalogs, commit metadata, coverage
labels, and documented limitations remain the source of truth.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The page itself shall say `Public claim level: concept`.
- The glossary may explain TraceMap vocabulary and show authored,
  public-safe examples, but it must not imply that every term is fully shipped
  or available on every public page.
- The page must not claim that TraceMap proves runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, or complete product coverage.
- The page must not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, or hidden validation details.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs, evidence tiers, proof paths, coverage labels, limitations,
  analysis gaps, commit or source context, extractor versions, supporting IDs,
  public claim levels, and local-only artifact families.

## Requirements

### Requirement 1: Choose a public route and placement

The future implementation shall add a public evidence glossary page or section
using an explicit placement decision.

Acceptance criteria:

- The implementation evaluates `/glossary/` and
  `/docs/evidence-glossary/` before selecting a route. It may also evaluate
  folding the glossary into an existing documentation or proof-path surface if
  that avoids duplicate vocabulary sources.
- `/glossary/` is a non-binding design recommendation because it is short and
  human-readable; the implementation must still record the selected placement
  and rejected alternatives.
- Before finalizing definitions, the implementation reconciles each required
  term with existing public vocabulary surfaces, especially `/evidence/`,
  `/proof-paths/`, and `/proof-source-catalog/`, and records which surface is
  canonical for each overlapping term.
- The selected placement and rejected alternatives are recorded in
  `implementation-state.md` with short reasons.
- The chosen page or section says `Public claim level: concept`.
- The chosen page or section states `No public conclusion without evidence`.
- The primary copy addresses engineers, reviewers, managers, architects, and
  agents who need stable vocabulary before repeating public TraceMap claims.
- Route copy makes clear that the glossary defines public-safe terminology and
  does not certify that every term is fully implemented, complete, or present
  on every TraceMap surface.
- If a standalone route is chosen, it is included in sitemap metadata,
  discovery metadata, canonical metadata, and internal-link validation.
- If the glossary is folded into an existing route, the implementation records
  why standalone sitemap/discovery metadata was not added and ensures the
  section has stable anchors.

### Requirement 2: Define required public vocabulary

The future glossary shall include the required TraceMap terms with conservative
definitions and clear constraints.

Acceptance criteria:

- The glossary defines all required terms: `rule ID`, `evidence tier`,
  `proof path`, `coverage label`, `limitation`, `analysis gap`,
  `commit/source context`, `extractor version`, `supporting IDs`,
  `public claim level`, and `local-only artifact family`.
- Each term entry includes a plain-language definition, why the term matters,
  what it can support publicly, and at least one limitation or non-claim.
- `rule ID` is defined as the stable identifier for the deterministic rule or
  extractor judgment that produced or classified evidence. The definition says
  evidence must remain attached to a rule ID before it is repeated as a
  TraceMap conclusion.
- `evidence tier` defines the known tiers
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` without implying lower tiers are failures or higher tiers are
  complete coverage.
- `proof path` is defined as the public-safe route or reference trail that lets
  a reader inspect why a claim is being made, not as raw local evidence.
  The definition must align with existing `/proof-paths/` usage and the
  `preferredProofPath` discovery metadata key.
- `coverage label` is defined as the scan or analysis coverage state, including
  full, partial, reduced, unknown, or gap-labeled evidence where applicable.
- `limitation` is defined as a first-class part of the claim, not a generic
  disclaimer.
- `analysis gap` is defined as explicit evidence that TraceMap could not prove
  or disprove something under current analysis conditions.
- `commit/source context` is defined as the repository identity and commit or
  source revision context used for a scan or public-safe summary, without
  publishing private remotes or local paths.
- `extractor version` is defined as the deterministic extractor or schema
  version that produced the public-safe evidence summary.
- `supporting IDs` are defined as related public-safe identifiers such as fact
  IDs, reducer finding IDs, rule IDs, route anchors, or summary IDs that help
  readers correlate claims without exposing raw artifacts.
- `public claim level` is defined as a conservative label such as `concept`,
  `demo`, or `shipped` that constrains how strongly the public site may present
  a claim.
- `local-only artifact family` is defined as a family of outputs, such as fact
  streams, SQLite indexes, analyzer logs, rule catalogs, or generated scan
  directories, that may be source material locally but must not be published raw.
- Definitions must not imply that a vocabulary term is available on every page,
  every scan, every language adapter, every public artifact, or every future
  implementation.

### Requirement 3: Preserve public-safe evidence boundaries

The future glossary shall clarify what terms mean without leaking private or
raw scanner material.

Acceptance criteria:

- The page does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  names, or hidden validation details.
- Public examples are authored glossary examples or derived from public-safe
  demo summaries. They do not name private samples or expose raw project
  material.
- Examples may name artifact families, but links go only to public-safe
  summaries, checked-in documentation, or public site routes.
- Private repository evidence is described as requiring private scans and
  human review before any summary becomes public copy.
- The glossary does not include local commands that reveal ignored output paths
  such as `site/dist/`, `site/output/`, `.tracemap/`, `.tracemap-demo/`, and
  similar generated scan directories listed in `.gitignore`, or invite readers
  to publish raw artifacts.
- The page includes an explicit non-claims section for runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI/LLM impact analysis, complete product coverage, and
  raw artifact publication.

### Requirement 4: Link glossary terms to existing public-safe routes

The future glossary shall help readers move from definitions to existing
public-safe proof and limitation surfaces without duplicating them.

Acceptance criteria:

- The page links to existing public-safe routes where relevant, including
  `/proof-paths/`, `/evidence/`, `/validation/`, `/limitations/`,
  `/roadmap/`, `/capabilities/`, `/manager-brief/`,
  `/review-claim-checklist/`, `/proof-source-catalog/`, and `/docs/` if those
  routes exist in generated output at implementation time. Link to `/docs/`
  only when it provides meaningful context for glossary vocabulary not already
  covered by a more specific route.
- At spec-review time, the following public-safe link targets exist in site
  source: `/proof-paths/`, `/evidence/`, `/validation/`, `/limitations/`,
  `/roadmap/`, `/capabilities/`, `/manager-brief/`,
  `/review-claim-checklist/`, `/proof-source-catalog/`, and `/docs/`. The
  glossary route itself does not yet exist at spec-review time. Additional
  routes also present at spec-review time include `/review-room/`,
  `/manager-faq/`, `/static-vs-runtime/`, `/adoption/`,
  `/use-cases/incident-review/`, `/use-cases/endpoint-review/`, `/workflows/`,
  and `/packets/`; these may be linked when relevant to glossary vocabulary.
- At minimum, the glossary must link to the canonical vocabulary and boundary
  surfaces it reconciles against: `/evidence/`, `/proof-paths/`,
  `/proof-source-catalog/`, and `/limitations/`. These are required links, and
  validation must fail if any required link is missing or does not resolve in
  generated output. Remaining routes in the list are linked when relevant.
- The implementation records any route that was expected by this spec but did
  not exist at implementation time, and either removes the link requirement or
  links to the closest public-safe replacement with a documented reason.
- Link text does not imply that concept terms are shipped capabilities or that
  demo evidence proves full product coverage.
- The glossary may add minimal cross-links from existing public proof,
  validation, limitation, roadmap, capability, or manager routes when the link
  helps readers understand vocabulary.
- When `/evidence/`, `/proof-paths/`, or `/proof-source-catalog/` already owns
  a definition or mapping for a term, the glossary cross-links and summarizes
  conservatively instead of creating a conflicting definition.
- Cross-links do not imply that the glossary is a source of truth over scanner,
  reducer, report, rule catalog, or documented limitation artifacts.
- Internal-link validation confirms all required glossary links resolve in
  generated site output.

### Requirement 5: Add metadata and automated validation

The future implementation shall make the glossary discoverable and validate its
claim boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph fields following existing site patterns.
- Discovery metadata labels the route as `concept` if a standalone route is
  chosen.
- Standalone discovery metadata follows the existing `discovery.json` shape,
  including `publicClaimLevel: concept`, `sourceType: site-page`, a
  `hintCategory`, `preferredProofPath`, `limitations`, and `nonClaims`.
- The `hintCategory` value must be chosen from the existing vocabulary in
  `site/src/_site/discovery.json`, and the implementation records the chosen
  value and rationale in `implementation-state.md`.
- Before adding standalone discovery metadata, the implementation confirms that
  `concept` is an accepted `publicClaimLevel` value in discovery and validation
  tooling, and records the result in `implementation-state.md`.
- Sitemap metadata includes the route if a standalone route is chosen.
- Standalone page-level metadata carries the `concept` claim-level signal so
  discovery tools and automated reviewers do not classify the glossary as a
  shipped capability page. If the glossary is folded into an existing route,
  the containing page's route metadata keeps its existing claim level unless
  the whole containing page is intentionally reclassified.
- Validation checks confirm the route or section renders, includes
  `Public claim level: concept`, includes
  `No public conclusion without evidence`, includes every required term, and
  includes the non-claims section.
- Validation enforces required route links that exist at implementation time
  and confirms they resolve in generated site output. Validation must enforce
  the required minimum link set from Requirement 4 plus any additional required
  links present at implementation time.
- Validation enforces forbidden overclaims and forbidden private/raw material
  across rendered text, decoded HTML, and raw HTML attributes.
- Forbidden AI/LLM positioning is enforced as affirmative product claims,
  following the existing affirmative-positioning or sanctioned-section
  validation pattern used by surrounding site validators. The required
  non-claims section is sanctioned so negated wording such as no AI/LLM impact
  analysis does not fail validation.
- Affirmative forbidden AI/LLM positioning includes at minimum `AI-powered`,
  `AI impact analysis`, `LLM-powered`, `LLM analysis`,
  `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`,
  `smart impact`, `vector database`, `prompt-based classification`,
  `powered by embeddings`, `uses embeddings`, `embedding-based analysis`,
  `vector search-powered`, and `vector search analysis`, tested
  case-insensitively when used as affirmative TraceMap capability claims.
- The forbidden private/raw-material guard follows the same
  affirmative-positioning or sanctioned-section pattern as the AI/LLM guard.
  Literal raw tokens, such as raw artifact filenames, raw analyzer log names,
  connection-string and credential patterns, generated scan directory paths,
  private sample names, and hidden validation details, are forbidden as
  affirmative or published content. The `local-only artifact family` term entry
  and the non-claims section are sanctioned so negated boundary wording does
  not fail validation. Glossary copy should prefer family phrasing such as fact
  streams, SQLite indexes, and analyzer logs over literal filenames.
- Forbidden private/raw text includes at minimum local path markers, raw
  artifact filenames, raw analyzer log names, raw SQL wording, connection
  string and credential tokens, secrets, raw remotes, generated scan
  directories, private sample names, and hidden validation details.
- The literal-string private/raw-material test list must include
  `facts.ndjson`, `index.sqlite`, `logs/analyzer.log`, `.tracemap/`, and
  `.tracemap-demo/`. Additional tokens may be added at implementation time. The
  complete tested token list is recorded in `implementation-state.md`.
- Validation enforces a bounded word count appropriate for a reference page.
  The implementation-time target must be in the range of 800 to 4000 words for
  a standalone glossary page and must accommodate all required term entries and
  the non-claims section; a folded section must set a minimum of at least 400
  words and a maximum no greater than 2000 words, record explicit numeric
  bounds, and explain the chosen ratio relative to the standalone floor of 800
  words. The chosen target, rationale, minimum/maximum bounds, ratio, and
  positive/negative test results are recorded in `implementation-state.md`. The
  implementation shall add a named
  test assertion in the glossary validator that enforces the chosen minimum and
  maximum; the test must fail if either bound is violated.

### Requirement 6: Keep implementation state current

The future implementation shall keep this spec packet useful for future agents
and reviewers.

Acceptance criteria:

- `implementation-state.md` is updated before implementation begins with the
  active branch, selected scope, route decision status, and review status.
- `implementation-state.md` records Kiro review commands and outcomes,
  Medium or higher findings, patches or dispositions, and re-review results
  where feasible.
- `implementation-state.md` records route placement, rejected alternatives,
  validation results, browser sanity results if layout changes are made, and
  any deferred items.
- If implementation is partial, the state file says so explicitly and labels
  which requirements remain incomplete.
- The state file does not include local absolute paths, raw remotes, secrets,
  raw facts, raw SQLite index paths, raw analyzer log content, private sample
  names, or hidden validation details.
