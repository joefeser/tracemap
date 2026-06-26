# Site TraceMap Tools Legacy Data Surface Story Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

Last verified: 2026-06-26
Branch: codex/site-legacy-data-surface-story
Base: origin/dev
Scope: public site concept route implementation

## Summary

This implementation adds a public-safe `tracemap.tools` concept page about
legacy data surface evidence at `/legacy-data-surface/`.

The story is bounded to deterministic static evidence: design-time metadata,
data model metadata, ORM or mapping clues, SQL/query-facing references, storage
and persistence context, proof paths, evidence status, owner follow-up, and
limitations. It is not a claim that TraceMap reads databases, executes SQL,
observes runtime SQL behavior, inspects production data, validates migrations,
or ships complete legacy data coverage.

## Scope Decisions

- Route decision: standalone `/legacy-data-surface/` using
  `site/src/legacy-data-surface/index.html`. Route review found all requested
  neighboring routes already exist, and a standalone page avoids overloading
  `/legacy-dotnet/evidence/`.
- Navigation decision: no primary nav addition. The route is discoverable
  through sitemap metadata, discovery metadata, and public-safe cross-links
  from the new page.
- Preferred proof path: `/legacy-evidence/`, with related proof context linked
  through `/proof-paths/`, `/validation/`, `/limitations/`, `/outputs/`,
  `/docs/`, and `/legacy-validation/`.
- Scope is public static-site work only. Scanner, reducer, core behavior,
  generated `site/dist`, and generated `site/output` were not hand-edited.
- Public claim level remains `concept`; rows use only the allowed evidence
  statuses `concept`, `future`, and `gap` in the initial public matrix.

## Public Safety Boundaries

- No runtime proof, production traffic, endpoint performance, outage cause,
  release safety, operational safety, release approval, database connectivity,
  database execution, runtime SQL behavior, data contents, schema
  compatibility, permission proof, or migration success claims.
- No AI impact analysis, LLM analysis, embeddings, vector databases,
  prompt-based classification, or autonomous migration review claims.
- No raw source snippets, raw SQL, raw config values, secrets, credentials,
  tokens, connection strings, database contents, table dumps, raw fact streams,
  raw SQLite content, analyzer logs, raw repository remotes, local absolute
  paths, generated scan directories, private sample names, hidden validation
  details, raw command output, private URLs, or credential-like values in public
  page copy.
- No raw model, table, column, stored-procedure, connection, provider, schema,
  query, sample, customer, repository, or local file-system names unless a
  future public-safe demo explicitly approves those values.

## Validation

Implementation validation run on 2026-06-26:

- `cd site && npm test` passed.
- `cd site && npm run validate` passed; it built `dist/` and reported 76 HTML
  files, 2671 internal references, 75 sitemap URLs, 1 legacy story safety
  target, 11 legacy .NET evidence-lane rows, 13 legacy modernization
  evidence-map rows, and 6 legacy data surface rows.
- `cd site && npm run build` passed.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- Desktop browser sanity at 1440x1100 on
  `http://localhost:4179/legacy-data-surface/` passed: title rendered,
  required concept phrases rendered, matrix present, and document scroll width
  equaled viewport width.
- Mobile browser sanity at 390x844 passed: no document-level horizontal
  overflow, required copy rendered, and the wide matrix scrolled inside its
  table wrapper.

Focused validation module:

- Added `site/scripts/legacy-data-surface.mjs`.
- Added `site/scripts/legacy-data-surface.test.mjs`.
- Wired `validateLegacyDataSurface` into `site/scripts/validate.mjs`.
- Assertion strategy: affirmative overclaim matching is sentence-level and
  subject-scoped to TraceMap, the page, or the tool. Private/raw disclosure
  matching remains category/value-based, with narrow exclusions for marked
  non-claim regions, limitation/negation context, and explicitly labeled
  `Forbidden example:` cells.

## Review Status

Kiro Opus and Sonnet spec reviews ran on 2026-06-26 using the required
commands. The review harness returned reduced coverage each time because Kiro
reported denied tool access inside the spawned review session; the wrapper
saved review text, meta files, and `analysisGaps` with
`ruleId: kiro.review.wrapper.v1` and `evidenceTier: Tier4Unknown`.

Actionable review findings were patched:

- Opus initial review: clarified that validator denylists must not reject the
  page's own required boundary, limitation, non-claim, or forbidden-wording
  example copy.
- Sonnet initial review: expanded matrix/link validation specificity and
  tightened `dev` wording.
- Sonnet follow-up spec review runs: added concept-level matrix placeholders, non-empty
  limitation-cell validation, token denylist parity, stricter route/link
  fallback wording, explicit forbidden-wording cell rules, subject-verb
  assertion matching, allowed status vocabulary, and future/dev label lifecycle
  rules.

All Medium+ actionable findings from the saved reviews have been patched.
Readiness is advanced to `ready-for-implementation` for this spec-only packet.

Implementation PR loop status:

- PR #365 initial ACK on head
  `6c71b855683b2e891d31599372379b5e804a45cd` returned
  `decision=actionable_findings`, `stopReason=UNRESOLVED_REVIEW_THREADS`,
  `nextAction=patch_actionable_findings`, with 8 unresolved review threads.
- Patch scope: hardened `site/scripts/legacy-data-surface.mjs` for
  attribute-order and whitespace-tolerant metadata checks, href and row
  attribute parsing, metadata/private attribute scanning, and missing matrix
  header handling; added regression tests in
  `site/scripts/legacy-data-surface.test.mjs`.
- Post-patch validation: `cd site && npm test`, `cd site && npm run validate`,
  `cd site && npm run build`, `git diff --check`, and
  `./scripts/check-private-paths.sh` passed. A parallel validate/build attempt
  was discarded after both commands raced on `site/dist`; sequential reruns
  passed.
- Final ACK after patch push: pending.

## Review Artifacts

- Opus initial review:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T052844-653Z-spec-claude-opus-4.8.clean.md`
- Opus initial meta:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T052844-653Z-spec-claude-opus-4.8.meta.json`
- Sonnet initial review:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053149-285Z-spec-claude-sonnet-4.6.clean.md`
- Sonnet initial meta:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053149-285Z-spec-claude-sonnet-4.6.meta.json`
- Sonnet follow-up spec review artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053411-238Z-spec-claude-sonnet-4.6.clean.md`
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053701-535Z-spec-claude-sonnet-4.6.clean.md`
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053857-610Z-spec-claude-sonnet-4.6.clean.md`
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T054127-814Z-spec-claude-sonnet-4.6.clean.md`

## Oddities

- The broader legacy .NET evidence lane already exists at
  `/legacy-dotnet/evidence/`; this spec intentionally narrows the future story
  to data-surface evidence instead of adding a broad legacy support page.
- The existing public site already uses hidden/future/concept language for
  legacy evidence. Future implementation should reuse that vocabulary rather
  than inventing new status labels.
- Discovery metadata rejects some raw artifact terms outside direct
  `nonClaims`, so the discovery entry uses broader public-safe wording while
  the page body names allowed artifact families in proof-path context.
- Browser sanity used generated static output served locally from `site/dist`;
  generated Playwright screenshots were removed from the worktree before
  commit.

## Follow-Up Items

- If future public-safe rule IDs or rule families are promoted, replace
  concept/future wording only after evidence tiers, coverage labels,
  limitations, and proof paths are documented.
- Keep the route out of primary navigation unless a future navigation spec
  requests promotion.
