# Evidence Export Usability Polish Implementation State

## Branch

`codex/implement-evidence-export-usability-polish`

## Scope

Implementation branch for issue 189. This PR implements the first coherent
delivery slice for vault export navigation polish and docs-export evidence
record ergonomics while preserving deterministic evidence boundaries.

Implemented in this branch:

- Added generated `Start Here.md` for vault exports while preserving generated
  `README.md` and `index.md`.
- Added deterministic vault folder `index.md` pages for folders that contain
  generated notes.
- Added bounded aliases and closed `tracemap/...` tags to vault Markdown
  frontmatter.
- Added additive graph `navigationCategory` fields for vault graph nodes and
  edges without changing canonical evidence IDs or kinds.
- Grouped dependency-surface folder indexes by closed `surfaceKind`.
- Added additive docs-export `questionFamilies`, `sectionTitle`, and
  structured `claim` fields to chunk JSONL records.
- Rendered docs-export Markdown with claim/citation-first structured sections.
- Added `docs-export.gap.unsupported-question-family.v1` to the rule catalog
  and documented its boundary with canonical unsupported families.
- Updated vault/docs export documentation and focused tests.

## Source Context

Reviewed current public docs and adjacent specs:

- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/vault-export-hidden-safety/`
- `docs/VAULT_EXPORT.md`
- `docs/EVIDENCE_DOCS_EXPORT.md`

The new spec is intentionally additive over existing vault/docs export behavior:
generated sentinels, content hashes, claim levels, public/demo strictness,
hidden/local redaction policy, deterministic output, and user-file collision
safety remain baseline requirements.

## Scope Decisions

- Keep TraceMap core deterministic. No LLM calls, embeddings, vector database
  writes, retrieval engine, prompt classification, or model-generated
  importance decisions are allowed.
- Treat downstream RAG/vector systems as consumers only. Their output cannot be
  evidence for TraceMap conclusions.
- Keep public/demo-safe output strict. Hidden/local output may redact, hash,
  category-label, or omit unsafe-looking values only under documented rules and
  must remain labeled hidden/local or partial where interpretation changes.
- Use safe display titles and aliases as navigation aids only. Stable IDs,
  hashes, rule IDs, evidence tiers, supporting IDs, coverage labels, and
  limitations remain the source of truth.
- Make optional review-friendly graph mode deterministic or defer it.
- Do not include private local paths, private repo names, exact private routes,
  raw SQL, raw config values, secrets, source snippets, raw remotes, production
  data, telemetry, vulnerability, ownership, or release-approval claims.

## Review Notes

Initial spec files drafted:

- `.kiro/specs/evidence-export-usability-polish/requirements.md`
- `.kiro/specs/evidence-export-usability-polish/design.md`
- `.kiro/specs/evidence-export-usability-polish/tasks.md`
- `.kiro/specs/evidence-export-usability-polish/implementation-state.md`
- `.kiro/specs/evidence-export-usability-polish/review-prompts.md`

Planned Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind implementation --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
```

Completed Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind implementation --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
```

Review results:

- Sonnet implementation review completed with reduced coverage because Kiro
  reported denied tool access. It identified additive graph navigation
  categories, surface-kind grouping, alias category ordering, hidden/local
  examples, validation-state updates, route-specific indexes, hidden/local
  title redaction, unsupported question-family request gaps, and review graph
  mode as issues relative to the full spec.
- Patched in this PR after implementation review: graph `navigationCategory`
  fields, surface-kind grouping, alias category ordering, concrete hidden/local
  examples, test coverage, and validation-state updates.
- Deferred after implementation review as follow-up scope: dedicated route
  indexes, a CLI/request surface for unsupported additive question-family gaps,
  additional title-kind redaction records beyond existing safety behavior, and
  deterministic review graph mode.
- Initial Opus spec review completed with full coverage. It found no blocking
  public-safety issues and requested clarifications around vault snapshot-mode
  inputs, docs snapshot-change inputs, `Start Here` link encoding, alias source
  ownership, and missing deterministic tests. Patched in requirements, design,
  and tasks.
- Initial Sonnet spec review completed with full coverage. It found blocking
  ambiguity around snapshot-change inputs and hidden/local redaction examples,
  plus important issues around review-mode boolean logic, alias/tag ordering,
  title-kind vocabulary, and unsupported question-family rule naming. Patched
  in requirements, design, and tasks.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  tool access. It verified the prior blockers were resolved and found no new
  blocking issues.
- Opus re-review completed with reduced coverage because Kiro reported denied
  tool access. It found one rule-catalog consistency issue around the new
  `docs-export.gap.unsupported-question-family.v1` and the existing
  `docs-export.gap.unsupported-family.v1`, plus clarifications around
  weak-evidence question views, generated `README.md` preservation, and additive
  route/chunk surfaces. Patched in requirements, design, and tasks.
- PR review loop posted/observed the required Codex review and stopped on one
  unresolved review thread. The thread requested multi-valued question-family
  membership so canonical chunks can belong to both primary and cross-cutting
  views without duplication. Patched requirements, design, and tasks to use a
  deterministic `questionFamilies` array and optional derived view indexes keyed
  by `chunkId`.

## Validation Log

Completed:

```bash
dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

- `dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"`:
  passed, 40 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed, 0 warnings, 0 errors.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 439 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Follow-Up Items

- Add deterministic `full|review` vault graph mode or explicitly defer it with
  a rule-backed limitation.
- Distinguish route, route-flow, static path, and route gap evidence in
  dedicated route indexes.
- Add a CLI/request surface for additive docs-export question-family gaps when
  a question view is explicitly requested but unsupported by input schema.
- Extend hidden/local redaction records for any future title/alias value that
  needs new redaction, hash, category-only, or omission behavior.
