# Evidence Export Usability Polish Review Prompts

Branch:

```text
codex/spec-evidence-export-usability-polish
```

Issue:

- `https://github.com/joefeser/tracemap/issues/189`

Spec files:

- `.kiro/specs/evidence-export-usability-polish/requirements.md`
- `.kiro/specs/evidence-export-usability-polish/design.md`
- `.kiro/specs/evidence-export-usability-polish/tasks.md`
- `.kiro/specs/evidence-export-usability-polish/implementation-state.md`

Adjacent context:

- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/vault-export-hidden-safety/`
- `docs/VAULT_EXPORT.md`
- `docs/EVIDENCE_DOCS_EXPORT.md`
- `rules/rule-catalog.yml`

## Opus Review Prompt

Review the TraceMap `evidence-export-usability-polish` Kiro spec on branch
`codex/spec-evidence-export-usability-polish` for product, evidence, and public
safety readiness.

This is a spec-only PR for issue 189. It should only add files under
`.kiro/specs/evidence-export-usability-polish/`.

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Prefer deterministic, testable extractors and exporters.
- Do not add LLM calls, embeddings, vector databases, or prompt-based
  classification to TraceMap core.
- RAG/vector systems may consume TraceMap evidence but cannot be evidence for
  TraceMap conclusions.
- Do not publish private local paths, private repo names, exact private routes,
  raw SQL, raw config values, source snippets, secrets, raw remotes, or
  production data.

Review questions:

1. Does the spec cover vault `Start Here`, folder/index organization, safe
   display titles, aliases, tags, graph categories, deterministic review mode,
   and generated-file collision safety?
2. Does it cover docs-export chunk titles, question-oriented families,
   claim/citation-first schema, hidden/local redaction, and downstream RAG
   import boundaries?
3. Are public/demo-safe outputs kept strict while hidden/local behavior remains
   labeled, deterministic, and safe?
4. Are rule IDs, evidence tiers, supporting fact/report IDs, safe source spans,
   coverage labels, and limitations preserved wherever claims are emitted?
5. Does the spec avoid runtime proof, production telemetry, vulnerability,
   ownership, release approval, or business impact claims?
6. Is optional important-only/review-friendly graph mode specified
   deterministically enough, or should it be deferred more explicitly?
7. Are tasks sliceable and ordered for implementation?
8. What Medium+ gaps or blocking public-safety risks should be patched before
   merge?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested edits.
- Missing requirements or tests.
- Whether the spec is ready to merge after fixes.

## Sonnet Review Prompt

Review the TraceMap `evidence-export-usability-polish` Kiro spec on branch
`codex/spec-evidence-export-usability-polish` as an implementation planner.

Focus on:

- Current vault exporter code seams for generated notes, indexes, graph JSON,
  title rendering, tags, aliases, claim-level filtering, hidden/local safety,
  and generated-file collision handling.
- Current docs-export code seams for chunk family selection, JSONL schema,
  Markdown rendering, manifest hashing, redaction, and collision behavior.
- Whether schema changes can be additive.
- Whether the proposed closed vocabularies are concrete enough to implement.
- Whether optional review-friendly graph mode should be built in the first
  implementation slice or deferred.
- Tests that should be written first.
- Rule catalog and docs updates required for new exporter gaps, redactions, and
  limitations.
- Validation commands likely to fail or need narrowing.

Return:

- Recommended first PR boundary.
- Concrete code seams by exporter area.
- Risky assumptions.
- Missing tests.
- Suggested scope cuts.
- Whether implementation should proceed after spec fixes.
