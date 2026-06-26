# Site TraceMap Tools Legacy Data Surface Story Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Last verified: 2026-06-26
Branch: codex/spec-site-legacy-data-surface-story-20260626002417
Base: origin/dev
Scope: spec-only site packet

## Summary

This spec defines a future public-safe `tracemap.tools` page or section about
legacy data surface evidence. The preferred future route is
`/legacy-data-surface/`, but implementation may choose a subsection of the
legacy .NET evidence lane if route review shows that is clearer.

The story is bounded to deterministic static evidence: design-time metadata,
data model metadata, ORM or mapping clues, SQL/query-facing references, storage
and persistence context, proof paths, evidence status, owner follow-up, and
limitations. It is not a claim that TraceMap reads databases, executes SQL,
observes runtime SQL behavior, inspects production data, validates migrations,
or ships complete legacy data coverage.

## Scope Decisions

- Create only spec files under
  `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/`.
- Do not edit `site/src/`, scanner code, reducer code, generated site output,
  or documentation outside this spec folder.
- Keep `Status: not-started` and `Public claim level: concept`; readiness is
  `ready-for-implementation` because Medium+ Kiro findings have been patched
  and reduced-coverage review artifacts are recorded.
- Keep future implementation language route-ready but concept-bound.

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

## Validation Plan

Spec-only validation for this branch:

- Run Kiro Opus spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-data-surface-story --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- Run Kiro Sonnet spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-data-surface-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- Patch Medium+ actionable findings and rerun one bounded re-review if
  findings are patched.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Confirm `git diff --name-only origin/dev...HEAD` is limited to this spec
  folder before commit.

Future implementation validation:

- Run `npm run build` from `site/`.
- Run relevant site validation from `site/`.
- Run focused validation for required copy, evidence families, matrix columns,
  links, metadata, discovery, sitemap when standalone, forbidden claims,
  private/raw material, and rendered word count.
- Run desktop and mobile browser sanity checks for layout and overflow.

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
- Sonnet re-reviews: added concept-level matrix placeholders, non-empty
  limitation-cell validation, token denylist parity, stricter route/link
  fallback wording, explicit forbidden-wording cell rules, subject-verb
  assertion matching, allowed status vocabulary, and future/dev label lifecycle
  rules.

All Medium+ actionable findings from the saved reviews have been patched.
Readiness is advanced to `ready-for-implementation` for this spec-only packet.

## Review Artifacts

- Opus initial review:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T052844-653Z-spec-claude-opus-4.8.clean.md`
- Opus initial meta:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T052844-653Z-spec-claude-opus-4.8.meta.json`
- Sonnet initial review:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053149-285Z-spec-claude-sonnet-4.6.clean.md`
- Sonnet initial meta:
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-data-surface-story/2026-06-26T053149-285Z-spec-claude-sonnet-4.6.meta.json`
- Sonnet re-review artifacts:
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

## Follow-Up Items

- During implementation, verify whether `/legacy-data-surface/` should be a
  standalone route or a section linked from `/legacy-dotnet/evidence/`.
- During implementation, verify the current discovery schema before choosing
  `hintCategory` and `preferredProofPath`.
- During implementation, verify any public-safe rule IDs or rule families before
  rendering them as page evidence.
