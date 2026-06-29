# Site TraceMap Tools Proof Path Index Requirements

Status: implemented
Readiness: implemented
Public claim level: demo

## Summary

Create a queued site phase for a public proof path index that helps managers,
reviewers, engineers, and bots find the evidence trail behind TraceMap public
pages and demo sections. The index should organize public-safe site surfaces by
artifact, rule ID, evidence tier, coverage label, proof path, and limitation.

This PR is spec/runway only and does not implement the page or edit site source.
The shared site principle for the future implementation is: No public conclusion
without evidence.

## Claim Boundaries

- The page may describe checked-in public/demo artifacts and public-safe
  generated summaries.
- Capabilities that exist only on `dev` must be labeled `dev-only` or omitted
  until promotion to `main`.
- The page must not claim runtime proof, production traffic, endpoint
  performance, deployment state, release safety, AI impact analysis, or
  capabilities not backed by public/demo artifacts.
- The page must not publish raw facts, SQLite databases, analyzer logs, source
  snippets, raw SQL, config values, secrets, local absolute paths, raw
  repository remotes, generated scan directories, or private sample identities.

## Requirements

### Requirement 1: Publish a proof path index

The site shall publish a proof path index page or section that organizes
TraceMap public pages and demo sections by their evidence trail.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page states the shared site principle: No public conclusion without
  evidence.
- Each index entry names the public page or section it supports.
- Each index entry includes artifact type, rule ID or rule-family reference,
  evidence tier, coverage label, proof path, and limitation.
- Each index entry points to checked-in public/demo source material or generated
  public-safe summaries rather than asking readers to trust site prose alone.
- Each proof path resolves to an existing checked-in artifact, public-safe
  generated summary, or public route in the implementation branch.
- The page avoids describing any evidence row as production proof, runtime
  proof, release approval, or AI analysis.

### Requirement 2: Keep artifact and proof-path vocabulary precise

The index shall use TraceMap's static evidence vocabulary consistently so
readers can compare claims across public surfaces.

Acceptance criteria:

- Artifact language distinguishes `scan-manifest.json`, `facts.ndjson`,
  `index.sqlite`, `report.md`, analyzer logs, generated public-safe summaries,
  combined reports, diff reports, impact reports, release-review reports, and
  sitemap or site metadata where applicable.
- Public copy explains that raw scan artifacts and SQLite files are local-only
  unless a future public-safe sample explicitly checks in sanitized output.
- Rule IDs are shown directly when public-safe and specific; otherwise a
  rule-family placeholder is used with a limitation explaining why.
- Evidence tiers use TraceMap tier names such as `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Coverage labels are transcribed from the public/demo artifact being cited
  instead of normalized into site-only wording.
- As of the implementation branch snapshot, report examples may include labels
  such as `Full`, `Partial`, `Reduced`, `FullEvidenceAvailable`,
  `ReducedCoverage`, and `UnknownAnalysisGap`; classification names such as
  `PartialAnalysis` are not presented as coverage labels unless the cited
  artifact uses them that way.

### Requirement 3: Support reader-specific discovery

The index shall help different readers find the same evidence trail without
changing the underlying claim level.

Acceptance criteria:

- Manager-oriented entries highlight what can be reviewed from public-safe
  summaries and where limitations remain.
- Reviewer-oriented entries show rule IDs, evidence tiers, coverage labels, and
  proof paths needed to verify wording.
- Engineer-oriented entries point to public/demo scripts, fixtures, and reports
  that reproduce or support the public row.
- Bot-oriented entries use stable labels and paths suitable for link checking,
  claim checking, and future automation.
- Bot-oriented labels match TraceMap's documented tier, coverage, and public
  status vocabulary rather than one-off free text.
- Reader paths do not introduce stronger claims than the evidence row supports.

### Requirement 4: Preserve main/dev wording boundaries

The index shall avoid promoting dev-only capability into public main-level
claims.

Acceptance criteria:

- Entries backed only by `dev` branch behavior are labeled `dev-only` or omitted
  until promotion.
- Entries backed by `main` checked-in samples or generated public-safe summaries
  may be labeled `demo`.
- Entries whose proof path is future-only are labeled `future` and must not be
  worded as available capability.
- The page-level `demo` claim level and per-entry public status are treated as
  separate axes so a `dev-only` or `future` entry cannot be mistaken for an
  available demo claim.
- The page does not imply parity across language adapters, workflows, or report
  types unless public/demo evidence supports that parity.

### Requirement 5: Integrate with existing public proof surfaces

The future implementation shall connect the index to existing site pages without
expanding the public claim boundary.

Acceptance criteria:

- The index links to relevant existing public proof surfaces such as `/demo/`,
  `/demo/result/`, `/demo/proof-assets/`, `/demo/proof-upgrades/`,
  `/packets/`, `/capabilities/`, `/roadmap/`, and `/docs/` when those routes
  exist in the implementation branch.
- Existing proof surfaces link back to the index where it helps readers verify
  a page's evidence trail.
- The index is included in sitemap metadata if implemented as a standalone
  route.
- The implementation-state note records final route decisions, validation,
  claim-boundary decisions, and follow-up items.

### Requirement 6: Validate static-site behavior

The implementation shall preserve static-site validation and responsive layout.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `scripts/check-private-paths.sh`.
- Verify proof paths resolve to existing checked-in artifacts, public-safe
  generated summaries, or public routes.
- If public demo generated summaries are refreshed, run the public demo sentinel
  scan through the existing demo-public assertion workflow.
- Run desktop and mobile browser sanity checks if any layout or interaction is
  changed.
