# Site TraceMap Tools Legacy Data Surface Story Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Review Objective

Review this spec-only packet for a future `tracemap.tools` legacy data surface
evidence story. The future surface should help managers, reviewers, architects,
and engineers understand deterministic static evidence about legacy data
surfaces without implying raw data access, database execution, runtime SQL
behavior, data contents, migration success, or shipped coverage.

This packet intentionally changes no site source, scanner code, reducer code,
generated output, or public copy.

## Files In Scope

- `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/requirements.md`
- `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/design.md`
- `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/tasks.md`
- `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/review-packet.md`

## Reviewer Questions

1. Does the packet consistently use `Status: not-started`, `Readiness:
   ready-for-implementation`, and `Public claim level: concept` after review
   findings are patched?
2. Does the future surface visibly require `Public claim level: concept` and
   `No public conclusion without evidence`?
3. Does the spec cover design-time metadata, data model metadata, ORM/mapping
   clues, SQL/query-facing references, storage/persistence context, and
   limitations?
4. Does it relate the future story to `/legacy-dotnet/evidence/`,
   `/legacy-evidence/`, and `/legacy-modernization/evidence-map/` without
   promoting hidden rows or private validation detail?
5. Is the evidence-status matrix specific enough for a future implementation
   agent to build and validate?
6. Are proof path requirements clear enough: rule ID or rule family, evidence
   tier, coverage label, limitation, public-safe provenance, commit/extractor
   metadata when safe, and no public conclusion without evidence?
7. Does the spec avoid implying raw data access, database execution, runtime SQL
   behavior, data contents, schema compatibility, migration success, endpoint
   performance, production traffic, outage cause, release safety, operational
   safety, complete coverage, or shipped capability?
8. Does the spec block AI-powered, LLM-powered, embeddings, vector databases,
   prompt-based classification, and AI/LLM impact-analysis claims?
9. Does the spec block raw source snippets, raw SQL, raw config values, secrets,
   credentials, connection strings, database contents, table dumps, raw facts,
   raw SQLite content, analyzer logs, local paths, raw remotes, generated scan
   directories, private sample names, hidden validation details, raw command
   output, private URLs, and credential-like values?
10. Are hidden/future/dev wording rules precise enough to prevent branch-only
    or hidden implementation behavior from becoming public copy?
11. Are metadata, discovery, sitemap, link-resolution, forbidden-wording, and
    route-scoped validation requirements specific enough?
12. Are tasks correctly split between spec-review tasks and future
    implementation tasks, with future implementation unchecked?

## Required Review Commands

Run if available:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-data-surface-story --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-data-surface-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model, tool, credential, or network dependency is unavailable, record the
exact command and error in `implementation-state.md`.

## Review Status

Initial Opus and Sonnet reviews completed with reduced coverage because the
spawned Kiro review sessions reported denied tool access. Saved artifacts are
listed in `implementation-state.md`.

Medium+ actionable findings were patched, including validator
affirmative-assertion matching, evidence-status matrix scaffolding, required
link validation, forbidden-wording cell handling, and hidden/future/dev wording
rules. Readiness is `ready-for-implementation`.
