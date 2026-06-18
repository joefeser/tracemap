# Site TraceMap Tools Adoption Playbook Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

- [ ] Confirm spec review passed and all Medium+ findings are resolved before
  beginning implementation.
- [ ] Choose `/adoption/` or `/playbook/` as the stable public route and record
  the route decision in `implementation-state.md`.
- [ ] Verify that `/demo/`, `/docs/`, `/validation/`, `/limitations/`, and
  `/proof-paths/` resolve, or identify current equivalent public routes.
- [ ] Verify whether `/review-room/` and `/static-triage/` exist before
  linking to them, and record any route gaps in `implementation-state.md`.
- [ ] Add the concept-level adoption playbook page using existing site layout
  patterns.
- [ ] Author the page from a neighboring static site page so it includes the
  replaceable `<header class="site-header">` block expected by the build.
- [ ] Include `Public claim level: concept` and the shared site principle on
  the page.
- [ ] Explain that the page is a process/onboarding playbook, not a product
  guarantee.
- [ ] Include a workflow step for starting with the public demo and public demo
  result.
- [ ] Include a workflow step for identifying a candidate repository or service
  area with a clear review question and known owner.
- [ ] Include a workflow step for running deterministic scans against a repo
  and commit SHA.
- [ ] Include a workflow step for reading evidence packets with rule IDs,
  evidence tiers, file paths, line spans, commit SHA, extractor versions,
  coverage labels, and documented limitations when available.
- [ ] Include a workflow step for making analysis gaps explicit, including
  reduced coverage and syntax fallback where applicable.
- [ ] Include a workflow step for deciding follow-up ownership across repository
  owners, runtime owners, test owners, documentation owners, or extractor work.
- [ ] Distinguish deterministic static evidence from runtime telemetry, tests,
  release approval, incident response, governance, ownership, and human review.
- [ ] State that partial analysis is useful only when labeled as partial.
- [ ] Keep the page roughly 500 to 1200 rendered words and under 1500 words
  unless the spec is amended.
- [ ] Avoid runtime behavior, production traffic, endpoint performance, outage
  cause, release safety, operational safety, AI impact analysis, LLM analysis,
  and complete product coverage claims.
- [ ] Avoid implying TraceMap replaces CI/CD, tests, telemetry, ownership,
  human review, release approval, incident response, or governance.
- [ ] Avoid guarantee language such as `ensures`, `certifies`, `guarantees`,
  `proves release safety`, or `prevents incidents`.
- [ ] Use public-safe copy, public demo summaries, sanitized labels, hashes,
  counts, rule IDs, evidence tiers, coverage labels, and limitations rather
  than private/raw artifacts.
- [ ] Avoid raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, private sample
  names, raw `facts.ndjson`, raw `index.sqlite`, and analyzer logs.
- [ ] Link to `/demo/` or the current public demo route.
- [ ] Link to `/docs/` for artifact and evidence terminology.
- [ ] Link to `/validation/`.
- [ ] Link to `/limitations/`.
- [ ] Link to `/proof-paths/`.
- [ ] Link to `/review-room/` if the route exists at implementation time.
- [ ] Link to `/static-triage/` if the route exists at implementation time.
- [ ] Add page metadata with claim level `concept`, including title,
  description, canonical URL, and Open Graph fields.
- [ ] Keep page/social titles at 70 characters or less and descriptions at 160
  characters or less unless the existing site metadata pattern differs.
- [ ] Add the route to sitemap source `site/src/_site/pages.json`.
- [ ] Add the route to discovery source `site/src/_site/discovery.json` with
  claim level `concept`, source type `site-page`, and an `llms.txt`
  route-section-compatible `hintCategory` such as `use-case`, so generated
  `routes-index.json` includes safe public metadata and the route section of
  `llms.txt` includes the page. Do not assert the entry in `docs-index.json`,
  which is generated only from `repo-doc` entries.
- [ ] Mirror the discovery field shape of comparable concept-level entries,
  including title, summary, source type, hint category, preferred proof path,
  limitations, and non-claims.
- [ ] Add safe navigation or cross-links from relevant public pages without
  implying runtime proof, production coverage, release approval, operational
  safety, outage diagnosis, or complete dependency coverage.
- [ ] Add a dedicated page validator module under `site/scripts/` and a
  matching `*.test.mjs`, mirroring the sibling per-page validator pattern, and
  wire it into `npm run validate` and `npm test`.
- [ ] Import the validator in `site/scripts/validate.mjs` and call it from
  `validateDist`.
- [ ] In that validator, assert required labels, exact principle text, the
  exact partial-analysis sentence chosen by the implementation, workflow
  steps, required links, word count, forbidden positioning, and forbidden
  private/raw text.
- [ ] In that validator, assert generated `routes-index.json` metadata and the
  `llms.txt` route-section entry for the selected route.
- [ ] Cover the private/raw-text denylist consistently with neighboring
  per-page validators by mirroring the inline pattern, or by introducing a
  shared exported denylist and migrating neighboring validators in the same
  change while recording the decision in `implementation-state.md`.
- [ ] Make the word-count validator fail below 500 words and above 1500 words.
- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`, or record a gap in
  `implementation-state.md` if the script is not present at implementation
  time.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `./scripts/check-private-paths.sh` from the repository root.
- [ ] Run desktop and mobile browser sanity checks if implementation changes
  layout, navigation, or interaction.
- [ ] Update `implementation-state.md` with selected route, implementation
  scope, validation, oddities, and follow-up items.
