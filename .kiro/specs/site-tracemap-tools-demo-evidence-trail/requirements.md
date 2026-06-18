# Site TraceMap Tools Demo Evidence Trail Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Summary

Define a future public demo page or section that walks a reader through one
public-safe question and the evidence trail behind it:
changed surface, endpoint or route, static path or surface, package, config, or
SQL-facing evidence, then coverage and limitations.

The page must make the existing evidence packet easier to follow. It must not
claim stronger proof, complete coverage, runtime behavior, production traffic,
endpoint performance, outage cause, release safety, operational safety, AI
impact analysis, LLM analysis, or product-wide completeness.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded demo evidence trail route or section

The site shall publish a demo-level evidence trail experience on an existing
demo route or a new public route selected during implementation.

Acceptance criteria:

- The page or section says `Public claim level: demo`.
- The page or section states the shared site principle.
- The placement is either under an existing demo route or a new route whose
  metadata, sitemap entry, and internal links label it as demo-level.
- The implementation records the chosen route or section anchor in this spec's
  `implementation-state.md`.
- If the implementation adds a new public route, it follows the existing
  canonical site top-navigation pattern and either keeps the shared top
  navigation unchanged or updates the shared navigation source consistently
  across generated pages.
- The primary message is: the same evidence packet is made easier to follow,
  not made stronger.
- The page or section uses bounded public demo language: it references only
  checked-in samples, public-safe generated summaries, static evidence packets,
  rule IDs, evidence tiers, coverage labels, limitations, and proof paths.
- The page or section does not imply the walkthrough is production proof,
  runtime proof, customer-specific evidence, or release guidance.

### Requirement 2: Use only public-safe demo proof sources

The demo shall be grounded in checked-in samples or public-safe generated
summaries that can be reviewed without exposing private material.

Acceptance criteria:

- The implementation identifies each proof source in
  `implementation-state.md` using repository-relative paths or public routes.
- Proof sources are checked-in sample data, checked-in public demo summaries,
  or future generated summaries that have already been sanitized for public
  use.
- Before a proof source is selected, the implementation confirms all of the
  following and records the result in `implementation-state.md`: the source is
  checked into the repository, contains no local absolute paths, raw remotes,
  connection-string tokens, private sample names, secrets, raw SQL, raw source
  snippets, or config values, does not cause `./scripts/check-private-paths.sh`
  to fail, and passes the forbidden-copy patterns used by the dedicated dist
  validator.
- Before a proof source is selected, the implementation confirms the source can
  actually supply the demo question's trail: a changed surface, an endpoint or
  route, a static path or explicit static-path gap, and for the downstream step
  at least one package, config, and SQL-facing surface item carrying a real rule
  ID and evidence tier, or an explicit per-type coverage gap.
- The evidence-sufficiency result is recorded in `implementation-state.md` as a
  separate check from the public-safety checklist.
- If the only public-safe source is a section-level rollup that cannot supply
  per-surface rule IDs and evidence tiers, the implementation either
  regenerates a sanitized per-surface summary from checked-in samples or records
  the limitation and renders affected steps as explicit coverage gaps.
- If a candidate proof source fails any public-safety check, the
  implementation stops and records the gap instead of publishing derived copy.
- The page does not publish raw `facts.ndjson`, `index.sqlite`, analyzer logs,
  raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw repository remotes, generated scan directories, or private sample names.
- Demo labels use public-safe names such as `Demo surface`, `Demo route`, or
  `Demo evidence packet` when a real identifier would expose private or raw
  details.
- Any hashes, counts, rule IDs, evidence tiers, coverage labels, extractor
  versions, and commit SHAs shown are copied from public-safe summaries or
  generated from checked-in public samples.
- If no suitable public-safe proof source exists at implementation time, the
  implementation must stop before publishing the page and record the gap in
  `implementation-state.md`.

### Requirement 3: Walk through one public-safe demo question

The demo shall follow a single bounded question so readers can inspect how a
TraceMap evidence packet is organized.

Acceptance criteria:

- The page frames exactly one public-safe demo question, such as `What static
  evidence connects this changed surface to a route and downstream surfaces?`
- The answer is presented as an evidence trail, not as a release decision,
  operational diagnosis, or product coverage claim.
- The trail includes these ordered steps: (1) changed surface, (2) endpoint or
  route, (3) the static path or connecting surface that links the changed
  surface to downstream evidence, or an explicit coverage gap if the selected
  sample contains no resolvable static path evidence, (4) a downstream
  dependency-surface step that enumerates the three surface types in scope for
  this demo: package evidence, config evidence, and SQL-facing evidence, and
  (5) coverage and limitations.
- The static path or connecting surface step is the connecting trail segment,
  distinct from the terminal downstream dependency-surface step.
- Surface types that the selected sample lacks are shown as explicit coverage
  gaps in the downstream dependency-surface step rather than omitted or implied
  present.
- The dedicated dist validator asserts that each in-scope downstream surface
  type appears in rendered evidence-trail output either as a trail item with at
  least one rule ID and evidence tier, or as a named coverage gap with an
  explicit coverage label and limitation note. A package, config, or
  SQL-facing surface type absent from both categories is a validator error.
- The rendered evidence-trail output emits a stable, machine-detectable marker
  for each in-scope downstream surface type and for each coverage gap, such as
  a data attribute or canonical label token, so the dist validator can assert
  per-type presence or gap without brittle prose matching. The marker scheme is
  recorded in `implementation-state.md`.
- Downstream surface types outside package, config, and SQL-facing evidence are
  out of scope for this demo phase unless a future spec amendment adds them.
- Package, config, and SQL-facing surfaces are represented only by public-safe
  surface names, rule IDs, evidence tiers, coverage labels, counts, and hashes
  copied from public-safe summaries.
- The page never renders raw config keys or values, raw SQL text, connection
  strings, or raw package manifests, even when enumerating that surface type as
  present.
- Each trail step includes at least one public-safe proof path, rule ID,
  evidence tier, and coverage label when that evidence exists.
- When evidence is missing, partial, reduced, or unknown, the step says so with
  an explicit coverage label and limitation instead of implying clean coverage.
- The page avoids the word `impacted` entirely in this phase; use bounded
  alternatives such as `referenced`, `connected`, or `shown in the packet`.

### Requirement 4: Preserve rule IDs, evidence tiers, coverage labels, and limitations

The demo shall keep every visible conclusion attached to deterministic
evidence and documented uncertainty.

Acceptance criteria:

- The page or section explains that rule IDs identify the extractor rule behind
  a fact, evidence tiers describe confidence in the static evidence, coverage
  labels describe scan completeness, and limitations describe what the packet
  cannot prove.
- Visible trail items include rule IDs and evidence tiers where available.
- Evidence tiers use the established names `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- Coverage labels distinguish complete, reduced, partial, missing, or unknown
  evidence using the site's existing vocabulary at implementation time.
- The page names at least one limitation for every major trail segment.
- The page does not convert a lower-tier or partial trail segment into a
  higher-confidence public conclusion.

### Requirement 5: Link to proof paths without exposing raw artifacts

The demo shall give readers proof-path navigation to public-safe supporting
material.

Acceptance criteria:

- Each major trail segment links to a public proof path, public demo summary,
  public documentation page, or public-safe generated summary.
- Links use public routes or repository-relative checked-in documentation paths
  only.
- Link text describes the kind of evidence, such as `route proof path`,
  `package evidence summary`, or `coverage limitation`, without exposing raw
  private identifiers.
- The page links to `/proof-paths/`, `/evidence/`, `/validation/`, and
  `/limitations/` or their current equivalent public routes if renamed before
  implementation.
- Before implementation starts, `implementation-state.md` confirms that each
  required target route or its renamed equivalent exists in built site output
  and records the resolved target routes.
- If a required target route does not exist at implementation time, the
  implementation records it as a coverage gap and links only to the nearest
  public alternative that preserves the same claim boundary.
- Internal links are validated against generated site output when the future
  implementation runs site validation.

### Requirement 6: Add discovery metadata and public route metadata

The demo shall be discoverable as a demo-level guide and not as a production
proof or broad impact claim.

Acceptance criteria:

- Discovery metadata labels the page or section as `demo`.
- If the implementation adds a route, the route appears in the site's page
  metadata and sitemap using the same metadata model as comparable demo pages.
- The page route belongs in sitemap metadata when comparable public routes are
  listed there; discovery hint URLs must follow the existing discovery
  validator rules and must not be incorrectly added to the sitemap.
- The implementation must run `npm run validate` against any proposed discovery
  entry before treating the field list as final.
- Required fields enforced by `site/scripts/discovery.mjs` at implementation
  time include at minimum: `title`, `summary`, `sourceType`,
  `publicClaimLevel` set to `demo`, `hintCategory`, the `path` field for a
  site-page or the `url` field for a repo-doc, and non-empty `limitations` and
  `nonClaims` arrays.
- Additional required discovery fields must be satisfied and recorded in
  `implementation-state.md`.
- Metadata includes a title, description, canonical URL, and Open Graph fields
  appropriate to the selected route or page.
- Metadata and discovery copy use bounded terms such as `demo evidence trail`,
  `static evidence packet`, `rule IDs`, `evidence tiers`, `coverage labels`,
  and `limitations`.
- Metadata and discovery copy do not claim runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, complete product coverage, or
  stronger proof than the underlying packet supports.
- The implementation records any new discovery fields or route metadata
  decisions in `implementation-state.md`.

### Requirement 7: Forbid private text and overstated public copy

The future implementation shall guard visible copy, metadata, tests, and
generated output against private material and forbidden positioning.

Acceptance criteria:

- The implementation adds a dedicated dist validator for the evidence-trail
  page, exports a validator function for the route or section, wires it into
  `site/scripts/validate.mjs`, and adds a companion test file.
- If no comparable page-validator pattern exists in the site at implementation
  time, the implementation defines the pattern first, documents the pattern
  decision in `implementation-state.md`, and uses that pattern consistently for
  the evidence-trail validator.
- `npm run validate` enforces required labels, trail steps, proof-path links,
  metadata, internal-link resolution, forbidden private/raw text, and forbidden
  AI/LLM positioning for the rendered evidence-trail output.
- The AI/LLM forbidden-pattern check applies to the full rendered
  evidence-trail output, including coverage-gap labels, limitation notes, trail
  step text, metadata-derived rendered text, and summary copy.
- The dedicated dist validator function enforces the `impacted` ban and the
  AI/LLM pattern list in the same exported validator function so both checks
  run during one `npm run validate` pass.
- `Same exported validator function` means a single exported function whose
  body performs both checks. Internal helper functions are permitted, but two
  separate exported functions wired individually do not satisfy this
  requirement.
- Rendered HTML, decoded rendered text, metadata, discovery output, and tests
  avoid raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names,
  connection-string tokens, and ignored artifact paths.
- The implementation validates forbidden AI/LLM positioning with a pattern
  that includes at minimum: `AI-powered`, `AI impact analysis`,
  `LLM-powered`, `LLM analysis`, `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`, and
  `smart impact`, case-insensitively.
- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, complete
  product coverage, or production readiness.
- The validator bans the word `impacted` in rendered evidence-trail output
  using a case-insensitive `/impacted/i` check, so implementation copy must use
  bounded alternatives such as `referenced`, `connected`, or `shown in the
  packet` unless a future spec amendment defines a reducer-backed citation rule
  and updates this pattern.
- The page avoids private or customer-like story framing unless the story is a
  checked-in public sample or a clearly synthetic demo.
- Alt text, social metadata, structured data, and image filenames follow the
  same public claim and privacy boundaries as visible page copy.

### Requirement 8: Validate and update implementation state

The future implementation shall run normal site validation and keep this spec's
state current.

Acceptance criteria:

- Before implementation starts, `implementation-state.md` is updated with the
  current branch, chosen route or section, selected public-safe proof source,
  and any scope decisions.
- Future implementation runs `git diff --check`.
- Future implementation runs `npm test` from `site/`.
- Future implementation runs `npm run validate` from `site/`.
- Future implementation runs `npm run build` from `site/`.
- Future implementation runs `./scripts/check-private-paths.sh`.
- For layout or interaction changes, future implementation performs desktop
  and mobile browser sanity checks or records why they were deferred.
- Future implementation confirms the dedicated evidence-trail dist validator
  is wired into `site/scripts/validate.mjs` and covered by tests before
  validation is recorded as complete.
- `implementation-state.md` records validation commands, results, review
  findings, oddities, and follow-up items before the implementation PR is
  opened.
