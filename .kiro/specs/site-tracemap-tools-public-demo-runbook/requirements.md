# Site TraceMap Tools Public Demo Runbook Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Summary

Define a future public `/demo/runbook/` page that gives operators a concrete
checklist for running the public demo, inspecting generated public-safe
summaries, deciding what can be shared, and avoiding stronger claims than the
demo evidence supports.

This is a spec-only site phase. It does not implement site code. The future
page is an operator checklist over existing public demo and proof surfaces,
not a new scanner, runtime service, evidence source, release gate, or product
coverage claim.

## Shared Site Principle

No public conclusion without evidence.

## Relationship to Existing Public Surfaces

The runbook bridges existing public routes instead of duplicating them:

- `/demo/start-here/` remains the first-run walkthrough.
- `/demo/result/` remains the current demo-result shape and summary guide.
- `/demo/evidence-trail/` remains the bounded single-question evidence trail.
- `/demo/proof-upgrades/` remains the ledger for upgraded demo rows.
- `/proof-paths/` remains the cross-page proof path index.
- `/validation/` remains the validation and generated-output guard reference.
- `/limitations/` remains the non-claims and partial-coverage boundary.
- `/demo/proof-assets/` is intentionally out of this bridge set because it is
  a visual orientation page over generated proof assets, while the runbook is
  the operator checklist for running, inspecting, and sharing the public demo.

The runbook may summarize these routes only as checklist steps. When a reader
needs proof details, the page must link back to the existing route or
public-safe repository source rather than restating raw generated internals.

## Claim Boundaries

- The page may describe reproducible behavior from checked-in public demo
  samples and generated public-safe summaries.
- The page must label itself `Public claim level: demo`.
- The page must state that public-safe summaries and reports are a presentation
  layer over deterministic evidence, not a replacement for rule IDs, evidence
  tiers, coverage labels, file spans or hashes where public-safe, commit/source
  context, and documented limitations.
- The page must distinguish shareable public-safe summaries from local-only
  generated artifacts.
- The page must not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The page must not publish raw facts, raw SQLite, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw
  repository remotes, generated scan directories, or private sample names.

## Requirements

### Requirement 1: Publish a public demo runbook page

The future implementation shall publish a public demo runbook page at
`/demo/runbook/`.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page states the shared site principle: `No public conclusion without
  evidence`.
- The page frames itself as an operator checklist for following the public
  demo, not as a product capability page, release process, incident procedure,
  production diagnostic, runtime verification guide, or implementation guide
  for modifying the demo script or scanner.
- The page links to `/demo/start-here/`, `/demo/result/`,
  `/demo/evidence-trail/`, `/demo/proof-upgrades/`, `/proof-paths/`,
  `/validation/`, and `/limitations/`.
- The page uses existing static site layout patterns and does not introduce a
  runtime service, client-side data fetch, analytics dependency, or new
  generated evidence artifact.
- The page renders the canonical site top navigation with the standard
  `top-nav` link set so it passes the existing top-navigation validation gate;
  `/demo/runbook/` is reached through in-page links and is not added to the
  canonical navigation set.
- The implementation records the final route, source files, validation, and
  follow-ups in this spec's `implementation-state.md`.

### Requirement 2: Provide an operator checklist

The future page shall turn the public demo workflow into a concrete checklist
that a reader can follow before sharing demo results.

Acceptance criteria:

- The checklist includes a pre-run step for using a clean checkout of the
  public repository and choosing an ignored or temporary output directory.
- The checklist tells the reader to run the checked-in public demo workflow,
  with the command source linked to the public demo script.
- The checklist tells the reader to inspect the generated public-safe summary
  before opening local-only scan artifacts.
- The checklist tells the reader to compare demo rows against `/demo/result/`
  and `/demo/proof-upgrades/` before repeating any status claim.
- The checklist tells the reader to follow at least one row through
  `/demo/evidence-trail/` and `/proof-paths/` before describing why the row is
  evidence-backed or gap-labeled.
- The checklist presents this evidence-following step as a named distinct step,
  such as `Follow the evidence`, between the result-review step and the
  validation-and-limitations step.
- The checklist tells the reader to check `/validation/` and `/limitations/`
  before sharing output externally.
- The checklist includes a stop condition: if the generated output lacks a
  public-safe summary, contains forbidden private material, or lacks rule IDs,
  evidence tiers, coverage labels, or limitations for a claim, the reader must
  not publish or repeat the claim.
- Future implementation must also run `./scripts/check-private-paths.sh`
  before publishing; this is the Requirement 7 private-text validation gate,
  and if it fails, the page must not be published.

### Requirement 3: Distinguish shareable and local-only artifacts

The future page shall make artifact sharing boundaries explicit.

Acceptance criteria:

- The page classifies generated public-safe summaries and reviewed public-safe
  reports as shareable only when they are produced from checked-in public demo
  samples and pass the sentinel/private-text checks used by the repository.
- The page classifies raw `facts.ndjson`, raw `index.sqlite`, combined SQLite
  files, analyzer logs, raw `report.md` generated from private or unchecked
  inputs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, and private sample
  names as local-only.
- The page may name artifact families such as `demo-summary.md`,
  `demo-summary.json`, report-family Markdown/JSON summaries,
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log` only to explain sharing boundaries.
- Artifact families and forbidden category labels are named only inside the
  sanctioned artifact-boundary, red-flag, or sharing-guidance sections. They
  must not appear beside actual private values or raw artifact content.
- The page must not link directly to raw generated output directories or
  machine-local artifact paths.
- The page must not include local absolute path examples in visible copy,
  metadata, discovery entries, tests, implementation-state notes, or review
  packet guidance.
- If the implementation needs a command example with an output path, the
  example uses a neutral placeholder such as `<ignored-output-dir>` instead of
  a workstation-specific path.

### Requirement 4: Preserve evidence requirements for shareable summaries

The future page shall require every shareable demo conclusion to remain tied to
deterministic evidence.

Acceptance criteria:

- The page tells readers to verify rule IDs before summarizing a row.
- The page tells readers to verify evidence tiers such as `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- The page tells readers to verify coverage labels such as full, partial,
  reduced, unknown, `PartialAnalysis`, `not_requested`, or `unavailable` only
  when those labels appear in the cited public-safe artifact.
- Coverage label examples intentionally use mixed spelling because public demo
  and generated artifacts may emit mixed canonical values. Future validation
  must transcribe label values case-sensitively from the cited artifact instead
  of normalizing them into site-only wording.
- The page tells readers to keep gaps visible instead of replacing them with
  clean-coverage wording.
- The page tells readers to link claims back to `/proof-paths/` or the
  relevant public demo route when sharing a summary.
- The page states that generated public-safe summaries can summarize evidence,
  but the source of truth remains deterministic TraceMap artifacts, checked-in
  sample sources, rule IDs, coverage labels, and documented limitations.
- The page does not use the word `impacted` for public demo conclusions. If the
  page must reference the term to give wording guidance, it appears only inside
  the sanctioned sharing-guidance or red-flag section, and validator checks
  exempt that section. The page never asserts a demo row is `impacted` without
  citing a deterministic reducer output and evidence row bounded to the cited
  public demo artifact.

### Requirement 5: Add claim-safe sharing guidance

The future page shall include a copy/paste-safe sharing section that helps
operators avoid overclaiming.

Acceptance criteria:

- The sharing section includes safe wording patterns for public demo results,
  such as static evidence, checked-in samples, public-safe summaries, rule IDs,
  evidence tiers, coverage labels, limitations, and gap-labeled rows.
- Non-normative examples of safe wording patterns include `static evidence from
  checked-in public demo samples`, `rule ID <id>, Tier2Structural, public demo
  coverage only`, and `gap-labeled row: partial coverage, no clean reducer
  conclusion`.
- The sharing section includes forbidden wording patterns or red flags for
  runtime behavior, production traffic, endpoint performance, outage cause,
  release safety, operational safety, AI impact analysis, LLM analysis, and
  complete product coverage.
- The sharing section says demo evidence can route review and inspection but
  cannot approve a release, diagnose a production outage, prove endpoint
  performance, prove production usage, or certify operational safety.
- The sharing section tells readers to avoid publishing raw facts, raw SQLite,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, and private sample
  names.
- The page gives a clear escalation rule: when a claim would require runtime
  telemetry, production deployment facts, customer traffic, external incident
  context, or release policy, link to limitations instead of making the claim.

### Requirement 6: Integrate with demo discovery and navigation

The future page shall be reachable from relevant demo and proof surfaces
without changing their claim levels.

Acceptance criteria:

- `/demo/` links to `/demo/runbook/` near the walkthrough, result, proof
  upgrades, evidence trail, and proof assets links.
- `/demo/start-here/` links to `/demo/runbook/` as the operator checklist after
  the first run.
- `/demo/result/` links to `/demo/runbook/` where readers decide what can be
  shared.
- `/demo/evidence-trail/` links to `/demo/runbook/` where readers check
  wording before sharing.
- `/demo/proof-upgrades/` links to `/demo/runbook/` where readers check the
  upgraded-row sharing boundary.
- An outbound link from `/demo/proof-assets/` to `/demo/runbook/` is optional,
  not required, because `/demo/proof-assets/` is visual orientation rather than
  an operator checkpoint.
- `/proof-paths/`, `/validation/`, and `/limitations/` each include at least
  one link to `/demo/runbook/` in a location such as a see-also note or
  public-demo operation callout. The link must not imply the runbook supersedes
  or weakens those pages' source-of-truth role.
- `/demo/runbook/` is registered in the sitemap source
  `site/src/_site/pages.json` with `path`, `changefreq`, and `priority`,
  following the existing per-page pattern, so generated `sitemap.xml` includes
  it. It is also added to `site/src/_site/discovery.json`.
- Discovery metadata adds a `/demo/runbook/` entry in
  `site/src/_site/discovery.json` with `sourceType: "site-page"`,
  `hintCategory: "demo"`, `publicClaimLevel: "demo"`, a non-empty `title` and
  `summary`, non-empty `limitations` and `nonClaims` arrays, and an optional
  `preferredProofPath` that resolves to an existing public route such as
  `/proof-paths/`.
- Discovery metadata may use artifact-family names or AI/LLM red-flag terms
  only inside `nonClaims` array strings, because the discovery safety check in
  `site/scripts/discovery.mjs` exempts only `nonClaims` from its denied-phrase
  scan. `title`, `summary`, `limitations`, `preferredProofPath`, and all other
  fields are denied-phrase scanned and must not contain artifact-family names
  such as `facts.ndjson`, `index.sqlite`, `analyzer.log`, or
  `logs/analyzer.log`; AI/LLM red-flag terms such as `AI impact analysis`,
  `embedding`, `vector database`, or `prompt-based classification`; or private
  tokens such as `/Users/`, `C:\`, `Server=`, `User Id=`, `Password=`,
  `ConnectionString`, `connection string`, `raw SQL`, `SELECT *`, raw source
  snippets, or `local output roots`. The `/demo/runbook/` limitations strings
  must be phrased without those literal tokens.
- If the existing discovery schema includes a `description` field alongside
  `summary`, the entry must populate it with non-empty text that does not
  assert runtime behavior, production evidence, or product-wide coverage.
- Link text must not imply the runbook proves production behavior, runtime
  reachability, release readiness, or product-wide coverage.

### Requirement 7: Validate public safety and overclaim boundaries

The future implementation shall add or update validation so the page stays
public-safe.

Acceptance criteria:

- Add focused site validation for `/demo/runbook/` using the existing
  rendered-output validator pattern.
- The focused validator module is named `site/scripts/demo-runbook.mjs` and
  exports a `validateDemoRunbookDist` function imported by aggregate site
  validation, unless the existing site validator naming convention changes
  before implementation.
- The focused validator is wired into aggregate site validation in
  `site/scripts/validate.mjs` so `npm run validate` exercises it, following the
  existing per-page validator wiring pattern.
- A companion test module under `site/scripts/` using the existing
  `*.test.mjs` naming pattern covers the runbook validator's pass and fail
  cases so `npm test` exercises required-label, required-link,
  discovery-metadata, artifact-boundary, forbidden-private-text, and overclaim
  assertions.
- The companion test module constructs forbidden private-text fail-case
  fixtures, including home-directory paths, Windows user-directory paths,
  connection-string fragments, and raw SQL, using runtime string composition
  rather than embedded literals. The implementation should follow existing
  repository patterns such as `String.fromCharCode(47)` or a local path-builder
  helper, so the test proves rejection without introducing a literal that
  `./scripts/check-private-paths.sh` flags or that violates the Requirement 3
  no-local-absolute-path-in-tests rule.
- The validator checks required labels, required route links, discovery
  metadata, artifact sharing boundaries, and forbidden private/raw text.
- The validator checks forbidden AI/LLM positioning with a pattern that
  includes at minimum `AI-powered`, `AI impact analysis`, `LLM-powered`,
  `LLM analysis`, `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`, and
  `smart impact`, case-insensitively, outside sanctioned non-claim and
  red-flag sections. Inside those sections, the same phrases are permitted only
  as explicitly labeled red flags or non-claims, never as page positioning.
- The validator checks forbidden runtime and operational overclaim wording
  outside sanctioned non-claim or red-flag sections.
- The validator distinguishes naming a forbidden artifact family or category
  from publishing forbidden content. Artifact family names such as
  `facts.ndjson`, `index.sqlite`, `logs/analyzer.log`, `report.md`, and
  `scan-manifest.json`, plus category labels such as raw SQL, config values,
  secrets, generated scan directories, and private sample names, may appear
  only inside sanctioned artifact-boundary, red-flag, or sharing-guidance
  sections and never beside an actual value.
- The validator rejects pattern-detectable raw/private values anywhere on the
  page, including machine-local absolute paths, `file://`, `localhost`,
  `127.0.0.1`, `.tracemap` generated-scan roots, connection-string fragments
  such as `Server=`, `Password=`, and `User Id=`, raw SQL statement patterns,
  and repository-remote patterns such as `git@`, `ssh://`, and
  `https://<host>/<org>/<repo>.git`.
- The validator treats literal artifact-family names such as `.ndjson`,
  `.sqlite`, and `analyzer.log` as allowed only inside sanctioned
  artifact-boundary, red-flag, or sharing-guidance sections. The same literal
  names outside those sections are rejected, and actual artifact values or
  paths remain forbidden everywhere.
- Private sample/app names and raw source snippets are not generically
  pattern-detectable. Their rejection is delegated to
  `./scripts/check-private-paths.sh` known-private-token checks plus authoring
  review. The focused validator may additionally accept an explicit
  denied-token list for any known private sample/app names instead of
  attempting open-ended detection.
- The validator checks at minimum `.ndjson` file references, `.sqlite` file
  references, `analyzer.log` text, home-directory path patterns such as
  `/Users/` and `/home/`, Windows user-directory patterns such as `C:\Users\`,
  and `.tracemap` directory references in rendered page copy, metadata, and
  discovery output, while applying the sanctioned-section exception for
  artifact-family names only.
- The validator confirms generated `sitemap.xml` includes `/demo/runbook/`.
- The focused validator or a companion check wired into `npm run validate`
  confirms that each required inbound link to `/demo/runbook/` is present in
  the rendered output of `/demo/`, `/demo/start-here/`, `/demo/result/`,
  `/demo/evidence-trail/`, `/demo/proof-upgrades/`, `/proof-paths/`,
  `/validation/`, and `/limitations/`.
- The validator scopes any `\bimpacted\b` check to exempt sanctioned
  sharing-guidance and red-flag sections, while still rejecting any unsupported
  assertion that a public demo row is impacted.
- Future implementation runs `npm test` from `site/`.
- Future implementation runs `npm run validate` from `site/`.
- Future implementation runs `npm run build` from `site/`.
- Future implementation runs desktop and mobile browser sanity checks because
  this is a layout/content page.
- Future implementation runs `git diff --check`.
- Future implementation runs `./scripts/check-private-paths.sh`.

### Requirement 8: Keep spec and implementation state current

The future implementation shall keep this spec's task and state files accurate.

Acceptance criteria:

- Implementation tasks remain unchecked until site code, validation, and
  required browser sanity checks are complete.
- `implementation-state.md` records branch, route, scope decisions, public
  claim level, validation commands and results, review findings, oddities, and
  follow-up items.
- If a review finding changes requirements, tasks, validation, or route scope,
  the implementation patches this spec before marking readiness complete.
- If any required local tool is unavailable, the implementation records the
  exact command and error and follows repository guidance for tool discovery
  before stopping.
