# Package and Dependency Surfaces Review Prompts

Use these prompts after an implementation exists. They are intentionally review-focused and should not ask the model to invent facts not present in the code or artifacts.

## Opus Review Prompt

You are reviewing a TraceMap implementation for GitHub issue #32, Package and dependency surfaces.

Review stance:

- Prioritize correctness, determinism, evidence quality, schema compatibility, redaction, and overclaiming risk.
- Do not suggest LLM calls, embeddings, vector databases, prompt classification, package registry lookups, vulnerability scanning, SBOM generation, or transitive dependency solving for the MVP.
- Treat static package metadata as structural evidence only. It does not prove runtime loading, installed versions, exploitability, deployment, or business impact.

Context to read:

- `AGENTS.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/package-dependency-surfaces/requirements.md`
- `.kiro/specs/package-dependency-surfaces/design.md`
- `.kiro/specs/package-dependency-surfaces/tasks.md`
- The implementation diff.

Questions to answer:

1. Does every new or changed package-surface conclusion have rule-backed evidence with fact IDs, evidence tiers, file paths, line spans, repo identity, commit SHA, extractor IDs, and extractor versions?
2. Are package facts and combined package surfaces deterministic across repeated scans and repeated JSON report generation?
3. Are package names, versions, scopes, ecosystems, and manifest kinds normalized consistently across .NET, TypeScript, Python, and JVM without erasing useful ecosystem distinctions?
4. Does the implementation avoid claiming runtime loading, impact, vulnerability, exploitability, or business impact?
5. Are partial scans, parser failures, dynamic dependency declarations, unsupported manifests, duplicate package identities, and selector misses reported as reduced coverage or gaps?
6. Are raw source snippets, raw package scripts, local absolute paths, credentials, tokens, URL secrets, and unknown sensitive metadata excluded from facts, reports, logs, and JSON?
7. Are schema changes additive and compatible with existing combined indexes?
8. Do `report`, `diff`, `impact`, `paths`, `reverse`, and contract delta v2 handle `package-config` surfaces coherently?
9. Are tests present for adapter fact shape, redaction, surface projection, diff, impact, paths, reverse, contract delta, private-path guard, and deterministic output?
10. Were the relevant validation commands from `docs/VALIDATION.md` run or explicitly deferred with a clear reason?

Output format:

- Start with findings ordered by severity.
- Include file and line references.
- For each finding, include why it matters and the smallest credible fix.
- Then list open questions.
- End with a brief validation summary and residual risk.

## Sonnet Review Prompt

You are doing a focused implementation review for TraceMap package and dependency surfaces, GitHub issue #32.

Check only these things:

- Package surface facts use deterministic safe metadata and include `surfaceKind=package-config` where needed.
- Reports and JSON do not leak raw scripts, snippets, secrets, credentials, URL tokens, or developer-local absolute paths.
- Combined `report`, `diff`, `impact`, `paths`, and `reverse` behavior uses rule IDs and evidence tiers.
- Diff and impact wording does not overclaim runtime loading or business impact.
- Reduced coverage and analysis gaps are explicit.
- Tests cover the changed behavior.

Files to read:

- `.kiro/specs/package-dependency-surfaces/requirements.md`
- `.kiro/specs/package-dependency-surfaces/design.md`
- implementation diff
- relevant tests
- `rules/rule-catalog.yml`

Output format:

- Findings first, highest severity first.
- Use exact file and line references.
- Keep the summary short.
- Mention any missing validation command from `docs/VALIDATION.md`.

## Artifact Inspection Prompt

Use this prompt when generated scan or combined artifacts are available.

Inspect the package-surface artifacts for TraceMap issue #32:

- `facts.ndjson`
- `index.sqlite`
- `dependency-report.json`
- `dependency-report.md`
- `diff-report.json`
- `impact-report.json`
- `paths-report.json`
- `reverse-report.json`
- `logs/analyzer.log`

Verify:

1. Every package row has a rule ID, evidence tier, fact ID, source label when combined, repo identity, commit SHA, file path, and line span.
2. Package rows include safe package name, ecosystem, manifest kind, scope/group, and safe version or version hash metadata.
3. No artifact contains raw source snippets, raw package scripts, credentials, auth tokens, local absolute paths, or URL secrets.
4. Reduced coverage and gaps are visible when metadata is unresolved, dynamic, malformed, duplicate, or selector-missing.
5. Re-running the same command produces byte-stable JSON output where expected.

Report findings with artifact path and JSON path or line reference when possible.
