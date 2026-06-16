# Legacy Sample Smoke Catalog Implementation State

Status: implemented
Branch: codex/implement-legacy-sample-smoke-catalog
Worktree: tracemap-legacy-smoke-catalog
Scope: script fallback catalog tooling, tracked catalog docs, schema, tests
Public claim level: public-safe synthetic metadata
Readiness: validation-complete-pending-pr-review-loop

## Summary

This branch implements the legacy sample smoke catalog as deterministic
metadata under `docs/validation/legacy-sample-smoke-catalog/`.

Delivered files include:

- `catalog.json` as the canonical source of truth with schema version
  `legacy-sample-smoke-catalog.v1`;
- generated `catalog.md` with the first-line
  `catalog-json-sha256` sentinel;
- `legacy-sample-smoke-catalog.v1.schema.json`;
- README workflow documentation;
- `.tmp/legacy-sample-smoke-catalog/` ignore coverage;
- `scripts/legacy-sample-smoke-catalog.mjs` script fallback tooling for
  validate, render, and promote;
- focused `node:test` coverage for schema, redaction, claim levels, command
  templates, relationships, Markdown staleness, render filtering, dry-run, and
  promotion gates;
- catalog rule IDs in `rules/rule-catalog.yml`.

## Scope Decisions

- Used a Node script fallback instead of first-class .NET CLI integration.
  First-class commands such as `tracemap catalog legacy-smoke validate`,
  `render`, and `promote` remain a migration follow-up because this feature is
  docs/tooling metadata and touching the .NET CLI would add scanner/reducer
  risk without changing catalog semantics.
- The tracked catalog contains only reviewed synthetic/public-safe metadata.
  Hidden/operator-local cases are covered by fixtures/tests and are not
  committed as catalog entries.
- The local draft schema variant for `redacted-sha256` and `local-only` commit
  identity was not implemented. The tracked schema and validator reject both
  values, matching the current v1 policy.
- Relationship references are safe schema/artifact references only. They do
  not copy raw validation summaries, baselines, evidence packs, reports,
  SQLite indexes, facts, manifests, logs, file lists, snippets, SQL, config
  values, remotes, paths, or private names.
- The generated Markdown is review output only. It is not a site page and is
  not public marketing copy.

## Validation

Completed:

```bash
node scripts/legacy-sample-smoke-catalog.mjs render --catalog docs/validation/legacy-sample-smoke-catalog/catalog.json --out docs/validation/legacy-sample-smoke-catalog --date 2026-06 --force
node scripts/legacy-sample-smoke-catalog.mjs validate --catalog docs/validation/legacy-sample-smoke-catalog/catalog.json
node --test scripts/legacy-sample-smoke-catalog.test.mjs
git check-ignore .tmp/legacy-sample-smoke-catalog/example
./scripts/check-private-paths.sh
git diff --check
```

Results:

- Catalog render: passed.
- Catalog validate: passed.
- Focused catalog tests: 14 passed, 0 failed.
- `git check-ignore`: passed for `.tmp/legacy-sample-smoke-catalog/example`.
- Private path guard: passed.
- Diff whitespace check: passed.

Deferred with rationale:

- `dotnet test src/dotnet/TraceMap.sln`: no .NET scanner, reducer, storage, or
  CLI code changed.
- Pinned scanner/language-adapter smokes from `docs/VALIDATION.md`: deferred
  because this branch only adds catalog metadata and a standalone script
  fallback; it does not change adapter extraction, report generation, combine,
  reducer, path, reverse, diff, impact, or release-review behavior.

## Follow-Ups

- Migrate the script fallback to first-class CLI commands when the catalog
  workflow needs to integrate with broader TraceMap CLI UX.
- Add an ignored local-draft schema variant only if a future policy defines
  safe handling for local-only identity and redacted commit hashes.
- Add automatic sample discovery from ignored operator manifests in a separate
  implementation slice.
- Feed promoted public-safe evidence packs from cataloged samples into future
  docs/site workflows without turning this catalog into a site page.
