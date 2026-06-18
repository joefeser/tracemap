# Evidence Export Usability Polish Implementation State

## Branch

`codex/spec-evidence-export-usability-polish`

## Scope

Spec-only branch for issue 189. The branch defines requirements, design, tasks,
implementation notes, and review prompts for vault export navigation polish and
RAG/evidence-doc usability polish.

This branch must not implement product code.

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
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
```

Completed Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-usability-polish --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text
```

Review results:

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

## Validation Log

Completed:

```bash
./scripts/check-private-paths.sh
git diff --check
```

- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Implementation validation such as `dotnet build` and `dotnet test` is not
required for this spec-only PR unless review finds a repository-specific reason
to run it.

## Follow-Up Items

- Keep the final diff limited to the new spec folder.
