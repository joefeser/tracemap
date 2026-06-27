# Static HTML Evidence Explorer Follow-Up Review Prompts

Branch:

```text
codex/static-html-evidence-explorer-followup
```

Spec files:

- `.kiro/specs/static-html-evidence-explorer-followup/requirements.md`
- `.kiro/specs/static-html-evidence-explorer-followup/design.md`
- `.kiro/specs/static-html-evidence-explorer-followup/tasks.md`
- `.kiro/specs/static-html-evidence-explorer-followup/implementation-state.md`

Adjacent context:

- `.kiro/specs/static-html-evidence-explorer/`
- `src/dotnet/TraceMap.Reporting/StaticHtmlEvidenceExplorer.cs`
- `src/dotnet/tests/TraceMap.Tests/StaticHtmlEvidenceExplorerTests.cs`
- `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/vault-export-hidden-safety/`
- `.kiro/specs/evidence-export-usability-polish/`

## Opus Review Prompt

Review the TraceMap `static-html-evidence-explorer-followup` Kiro spec for
product, evidence, and safety readiness.

This is a spec-only PR. It should only add files under
`.kiro/specs/static-html-evidence-explorer-followup/`.

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Prefer deterministic, testable extractors and exporters.
- Do not add LLM calls, embeddings, vector databases, prompt-based
  classification, or AI impact analysis to TraceMap core.
- Do not publish raw source snippets, raw facts, raw SQLite content, analyzer
  logs, raw SQL, config values, secrets, local absolute paths, raw remotes,
  hostnames, endpoint addresses, private repo names, private sample names, or
  generated scan directory names.

Review questions:

1. Is the selected PR 1 slice bounded enough: compatibility ledger plus
   safety/profile conflict hardening only?
2. Does the spec avoid duplicating already-implemented explorer work from the
   predecessor spec and live code?
3. Are public/demo output and hidden/local output boundaries strict enough?
4. Does profile/claim-level conflict handling prevent public-demo weakening
   without overclaiming complete artifact coverage?
5. Are missing, unsupported, provenance-only, compatible-empty, safety-omitted,
   and profile-incompatible states distinguishable?
6. Are rule ID and rule catalog requirements clear before new emitted
   rules/gaps/limitations/validation failures?
7. Does the wording avoid runtime behavior, impact proof, complete product
   coverage, public site claims, and AI/LLM analysis claims?
8. Are validation expectations sufficient for generated static output and
   browser/smoke checks if rendering changes?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested edits.
- Missing requirements or tests.
- Whether the spec is ready to merge after fixes.

## Sonnet Review Prompt

Review the TraceMap `static-html-evidence-explorer-followup` Kiro spec as an
implementation planner.

Focus on:

- Current live code seams in `StaticHtmlEvidenceExplorer.cs`.
- Whether the compatibility ledger can be additive without destabilizing the
  current explorer schema.
- Whether the profile/claim-level conflict model is concrete enough to
  implement with existing explorer rules or whether a new rule is needed.
- Whether closed status vocabularies, sort order, support IDs, and limitation
  fields are sufficiently specified.
- Whether the first PR avoids surface/path/reducer reader scope creep.
- Focused tests that should be written first.
- Documentation and rule catalog updates required for emitted rows.
- Validation commands likely to fail or need narrowing.

Return:

- Recommended first PR boundary.
- Concrete code seams by file/function area.
- Risky assumptions.
- Missing tests.
- Suggested scope cuts.
- Whether implementation should proceed after spec fixes.
