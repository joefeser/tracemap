# Site TraceMap Tools Proof Path Index Tasks

Status: implemented
Readiness: implemented
Public claim level: demo

Tasks 1-2 establish route and layout decisions before entry authoring and
discovery-link work. Validation tasks run after implementation changes.

- [x] Choose the final route or section placement for the proof path index.
- [x] Add the public proof path index using existing site styles and static-site
  patterns.
- [x] Create index entries for public pages and demo sections that have
  public/demo proof paths.
- [x] For each entry, include artifact type, rule ID or rule-family reference,
  evidence tier, coverage label, proof path, limitation, and public status.
- [x] Transcribe coverage labels from cited public/demo artifacts instead of
  normalizing them into site-only wording.
- [x] Verify every proof path resolves to an existing checked-in artifact,
  public-safe generated summary, or public route in the implementation branch.
- [x] Add reader-oriented paths for managers, reviewers, engineers, and bots
  without changing the underlying evidence.
- [x] Verify bot-oriented labels match TraceMap's documented tier, coverage, and
  public status vocabulary rather than one-off free text.
- [x] Label dev-only entries as `dev-only` or omit them until promotion to
  `main`.
- [x] Keep future-only entries clearly labeled `future` and avoid wording them
  as available capability.
- [x] Add explicit non-claims for runtime proof, production traffic, endpoint
  performance, deployment state, release safety, and AI impact analysis.
- [x] Preserve the public-safe artifact boundary and do not publish raw facts,
  SQLite files, analyzer logs, source snippets, raw SQL, config values, secrets,
  local absolute paths, raw repository remotes, generated scan directories, or
  private sample identities.
- [x] Add discovery links from relevant existing proof surfaces and include the
  index in sitemap metadata if it is a standalone route.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `scripts/check-private-paths.sh`.
- [x] Confirm public demo generated summaries were not refreshed; the
  demo-public assertion workflow was not required.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes.
- [x] Update `implementation-state.md` with route decisions, validation
  results, claim-boundary decisions, and follow-up items.
