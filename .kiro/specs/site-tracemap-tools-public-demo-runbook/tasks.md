# Site TraceMap Tools Public Demo Runbook Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

These tasks are future implementation work. They remain unchecked because this
phase is spec-only.

Note: validation tasks must pass before future implementation tasks are checked
complete.

- [ ] Add the `/demo/runbook/` page using existing static site layout patterns.
- [ ] Include the canonical site top navigation with the standard `top-nav`
  link set.
- [ ] Add page-level copy that says `Public claim level: demo`.
- [ ] State the shared site principle: `No public conclusion without evidence`.
- [ ] Frame the page as a public demo operator checklist, not as a production
  diagnostic, release procedure, runtime verification guide, or product
  capability page.
- [ ] Add a pre-run checklist step for using a clean public checkout and an
  ignored or temporary output directory.
- [ ] Add a run step that links to the checked-in public demo script source.
- [ ] Add an inspect step that starts with generated public-safe summaries
  before local-only artifacts.
- [ ] Add a result-review step that compares generated rows against
  `/demo/result/` and `/demo/proof-upgrades/`.
- [ ] Add an evidence-following step that routes at least one row through
  `/demo/evidence-trail/` and `/proof-paths/`.
- [ ] Present the evidence-following step as a named distinct checklist step,
  such as `Follow the evidence`, between result review and
  validation/limitations.
- [ ] Add a validation and limitations step that routes readers to
  `/validation/` and `/limitations/` before sharing externally.
- [ ] Add a stop condition for missing public-safe summaries, forbidden private
  material, missing rule IDs, missing evidence tiers, missing coverage labels,
  or missing limitations.
- [ ] Include `./scripts/check-private-paths.sh` in the publish stop condition:
  if it fails, the page must not be published.
- [ ] Add a shareable versus local-only artifact section.
- [ ] Classify public-safe summaries and reviewed public-safe reports as
  shareable only after public demo and sentinel/private-text checks pass.
- [ ] Classify raw `facts.ndjson`, raw `index.sqlite`, combined SQLite files,
  analyzer logs, raw private or unchecked `report.md`, raw source snippets, raw
  SQL, config values, secrets, local absolute paths, raw repository remotes,
  generated scan directories, and private sample names as local-only.
- [ ] Use only neutral output placeholders such as `<ignored-output-dir>` in
  command examples.
- [ ] Add evidence checklist items for rule IDs, evidence tiers, coverage
  labels, gaps, proof paths, checked-in sample sources, and documented
  limitations.
- [ ] Keep generated public-safe summaries framed as summaries over evidence,
  not replacements for deterministic artifacts and limitations.
- [ ] Avoid the word `impacted` for public demo conclusions. If the page must
  mention the term as wording guidance, keep it inside the sanctioned
  sharing-guidance or red-flag section and cite deterministic reducer evidence
  before any bounded assertion.
- [ ] Add a claim-safe sharing section with safe wording patterns for static
  evidence, checked-in samples, public-safe summaries, rule IDs, evidence
  tiers, coverage labels, limitations, and gap-labeled rows.
- [ ] Add forbidden wording/red-flag guidance for runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, AI impact analysis, LLM analysis, and complete product coverage.
- [ ] Add the escalation rule for claims requiring runtime telemetry,
  production deployment facts, customer traffic, external incident context, or
  release policy.
- [ ] Link `/demo/runbook/` to `/demo/start-here/`, `/demo/result/`,
  `/demo/evidence-trail/`, `/demo/proof-upgrades/`, `/proof-paths/`,
  `/validation/`, and `/limitations/`.
- [ ] Link to `/demo/runbook/` from `/demo/`, `/demo/start-here/`,
  `/demo/result/`, `/demo/evidence-trail/`, and `/demo/proof-upgrades/`.
- [ ] Link to `/demo/runbook/` from `/proof-paths/`, `/validation/`, and
  `/limitations/` at least once each, in a see-also note or public-demo
  operation callout that does not weaken those source-of-truth pages.
- [ ] Leave `/demo/proof-assets/` out of the required back-link set; an
  optional link is allowed but not required because that page is visual
  orientation rather than an operator checkpoint.
- [ ] Add `/demo/runbook/` to the sitemap registry
  `site/src/_site/pages.json` with `path`, `changefreq`, and `priority`.
- [ ] Verify generated `sitemap.xml` includes `/demo/runbook/`.
- [ ] Pre-check, before adding the discovery entry below: check
  `site/src/_site/discovery.json` and any schema documentation to confirm
  whether `description` is required or optional alongside `summary`, and record
  the result in `implementation-state.md`.
- [ ] Add `/demo/runbook/` to discovery metadata with
  `sourceType: "site-page"`, `hintCategory: "demo"`,
  `publicClaimLevel: "demo"`, non-empty `title` and `summary`, non-empty
  limitations and non-claims arrays, and an optional `preferredProofPath` that
  resolves to an existing public route such as `/proof-paths/`.
- [ ] Keep all discovery denied-phrase vocabulary inside the `nonClaims` array;
  keep `title`, `summary`, `limitations`, `preferredProofPath`, and all other
  fields free of artifact-family names, AI/LLM terms, and private tokens.
- [ ] Add focused rendered-output validation for required labels, required
  links, discovery metadata, artifact sharing boundaries, forbidden private/raw
  text, and forbidden overclaim wording.
- [ ] Add `site/scripts/demo-runbook.mjs` exporting
  `validateDemoRunbookDist`, unless the existing validator naming convention
  changes before implementation.
- [ ] Wire the focused runbook validator into aggregate site validation in
  `site/scripts/validate.mjs` so `npm run validate` exercises it.
- [ ] Add a companion `site/scripts/*.test.mjs` module covering runbook
  validator pass and fail cases for required labels, required links, discovery
  metadata, artifact boundaries, forbidden private text, and overclaims.
- [ ] Compose forbidden private-text fail-case fixtures at runtime, such as
  with `String.fromCharCode(47)` or a local path-builder helper, instead of
  embedding literals that would trip `./scripts/check-private-paths.sh` or the
  Requirement 3 no-local-absolute-path-in-tests rule.
- [ ] Validate forbidden AI/LLM positioning with a case-insensitive pattern
  that includes `AI-powered`, `AI impact analysis`, `LLM-powered`,
  `LLM analysis`, `machine learning impact analysis`,
  `artificial intelligence impact analysis`, `intelligent analysis`, and
  `smart impact`, scoped outside sanctioned non-claim and red-flag sections.
- [ ] Ensure validation allows artifact-family names and forbidden category
  labels only inside sanctioned artifact-boundary, red-flag, or
  sharing-guidance sections.
- [ ] Ensure validation rejects pattern-detectable raw/private content
  anywhere, including machine-local absolute paths, `file://`, `localhost`,
  `127.0.0.1`, `.tracemap` generated-scan roots, `.ndjson` and `.sqlite`
  references, `analyzer.log`, connection-string fragments, raw SQL statement
  patterns, and repository-remote patterns.
- [ ] Delegate non-generic checks for private sample/app names and raw source
  snippets to `./scripts/check-private-paths.sh` known-private-token checks,
  authoring review, and optional explicit denied-token lists rather than
  attempting open-ended detection.
- [ ] Ensure validation checks at minimum `.ndjson` file references, `.sqlite`
  file references, `analyzer.log` text, `/Users/`, `/home/`, `C:\Users\`, and
  `.tracemap` directory references in rendered page copy, metadata, and
  discovery output.
- [ ] Scope any `\bimpacted\b` validator check to exempt sanctioned
  sharing-guidance and red-flag sections while rejecting unsupported impact
  assertions.
- [ ] Validate that each required inbound link to `/demo/runbook/` is present
  in rendered `/demo/`, `/demo/start-here/`, `/demo/result/`,
  `/demo/evidence-trail/`, `/demo/proof-upgrades/`, `/proof-paths/`,
  `/validation/`, and `/limitations/` output.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route, scope, validation, browser
  checks, review findings, oddities, and follow-up items.
- [ ] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite paths, analyzer log content, generated scan
  directory paths, and private sample names.
