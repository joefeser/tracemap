# Site TraceMap Tools Proof Path Index Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

Tasks 1-2 establish route and layout decisions before entry authoring and
discovery-link work. Validation tasks run after implementation changes.

- [ ] Choose the final route or section placement for the proof path index.
- [ ] Add the public proof path index using existing site styles and static-site
  patterns.
- [ ] Create index entries for public pages and demo sections that have
  public/demo proof paths.
- [ ] For each entry, include artifact type, rule ID or rule-family reference,
  evidence tier, coverage label, proof path, limitation, and public status.
- [ ] Transcribe coverage labels from cited public/demo artifacts instead of
  normalizing them into site-only wording.
- [ ] Verify every proof path resolves to an existing checked-in artifact,
  public-safe generated summary, or public route in the implementation branch.
- [ ] Add reader-oriented paths for managers, reviewers, engineers, and bots
  without changing the underlying evidence.
- [ ] Verify bot-oriented labels match TraceMap's documented tier, coverage, and
  public status vocabulary rather than one-off free text.
- [ ] Label dev-only entries as `dev-only` or omit them until promotion to
  `main`.
- [ ] Keep future-only entries clearly labeled `future` and avoid wording them
  as available capability.
- [ ] Add explicit non-claims for runtime proof, production traffic, endpoint
  performance, deployment state, release safety, and AI impact analysis.
- [ ] Preserve the public-safe artifact boundary and do not publish raw facts,
  SQLite files, analyzer logs, source snippets, raw SQL, config values, secrets,
  local absolute paths, raw repository remotes, generated scan directories, or
  private sample identities.
- [ ] Add discovery links from relevant existing proof surfaces and include the
  index in sitemap metadata if it is a standalone route.
- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `scripts/check-private-paths.sh`.
- [ ] Run the public demo sentinel scan through the existing demo-public
  assertion workflow if public demo generated summaries are refreshed.
- [ ] Run desktop and mobile browser sanity checks if layout or interaction
  changes.
- [ ] Update `implementation-state.md` with route decisions, validation
  results, claim-boundary decisions, and follow-up items.
