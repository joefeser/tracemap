# Evidence Graph Vault Export Review Prompts

Use these prompts after reading:

- `.kiro/specs/evidence-graph-vault-export/requirements.md`
- `.kiro/specs/evidence-graph-vault-export/design.md`
- `.kiro/specs/evidence-graph-vault-export/tasks.md`

Related specs and docs:

- `AGENTS.md`
- `docs/VALIDATION.md`
- `.kiro/specs/combined-dependency-reporting/`
- `.kiro/specs/combined-dependency-paths/`
- `.kiro/specs/reverse-impact-query/`
- `.kiro/specs/release-review-report/`
- `.kiro/specs/multi-index-portfolio-report/`
- `.kiro/specs/public-demo-workflow/`
- `.kiro/specs/legacy-sample-evidence-pack/`
- `.kiro/specs/site-tracemap-tools-public-visual-proof-assets/`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal config values, connection strings,
  raw URLs, secrets, private paths, local absolute paths, or private source
  labels in public/demo exports.

## Opus Review Prompt

Review the TraceMap `evidence-graph-vault-export` spec for merge readiness.

This is a spec review, not an implementation review. Do not edit files.

The spec defines a deterministic evidence graph/vault export that turns existing
TraceMap artifacts into Markdown notes and optional JSON for local navigation
and demos. Obsidian compatibility is allowed, but this must remain a local
export over static evidence, not a new analyzer, hosted UI, graph database,
runtime trace, AI classifier, or site-marketing feature.

Please inspect:

- `.kiro/specs/evidence-graph-vault-export/requirements.md`
- `.kiro/specs/evidence-graph-vault-export/design.md`
- `.kiro/specs/evidence-graph-vault-export/tasks.md`
- related report/path/reverse/release-review/portfolio specs as needed

Review questions:

1. Is the feature boundary clear: export/view layer only, no new analysis or runtime proof?
2. Are supported input artifacts and schema compatibility behavior concrete enough?
3. Are node and edge vocabularies tied to existing TraceMap evidence concepts?
4. Do edges preserve rule IDs, evidence tiers, supporting IDs, coverage, and limitations?
5. Are claim levels and hidden/demo/public filtering safe and implementable?
6. Are Obsidian-friendly Markdown links/frontmatter/tags deterministic and safe?
7. Are generated sentinels, stale-output behavior, idempotency, and byte stability specified strongly enough?
8. Are redaction requirements strong enough for Markdown, JSON, tags, IDs, links, frontmatter, and diagnostics?
9. Does the spec avoid leaking local paths, raw remotes, SQL/config values, snippets, endpoints, analyzer logs, secrets, private repo names, hostnames, usernames, or branch names?
10. Are tasks sliced into reviewable implementation work?
11. What validation or tests are missing?

Return:

- Blocking issues with exact file/section references.
- Important Medium+ issues.
- Suggested concrete fixes.
- Missing tests or validation commands.
- Scope cuts or PR slicing suggestions.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review `.kiro/specs/evidence-graph-vault-export/` as an implementation planner
for the current TraceMap repository.

This is a spec review, not an implementation review. Do not edit files.

Focus on:

- Best first implementation boundary: .NET CLI versus script fallback.
- How to reuse existing combined report, paths, reverse, portfolio,
  release-review, and evidence-pack schemas without duplicating logic.
- Which node and edge kinds should ship in the first PR.
- How to make Markdown and JSON deterministic and easy to test.
- How to keep Obsidian compatibility optional and plain-Markdown friendly.
- How to enforce claim levels and redaction without making the first slice too large.
- How to avoid raw local paths and unsafe values in generated artifacts.
- Tests needed to make stale generated files, private leaks, lower-tier evidence,
  and hidden filtering hard to regress.

Return:

- Recommended first implementation slice.
- Risky assumptions.
- Blocking or important spec gaps.
- Files likely to change in implementation.
- Missing tests.
- Spec wording that should change before coding.

## Qodo/Gemini Review Prompt

Review this spec for correctness, privacy, determinism, and maintainability.

Look for:

- Public/demo graph claims that lack evidence, rule IDs, evidence tiers, or limitations.
- Any hidden private repo/path/remotes/SQL/config/snippet/analyzer-output leak risk.
- Any overclaiming around runtime execution, service reachability, production usage, security, vulnerability status, release approval, ownership, or business impact.
- Any accidental LLM, embedding, vector DB, graph database, semantic search, or prompt-based classification requirement.
- Any nondeterminism from timestamps, filesystem order, generated file names, frontmatter, tags, links, row order, or unstable IDs.
- Any Obsidian-specific feature that makes plain Markdown unusable.
- Any claim-level filtering path that silently drops hidden evidence without a visible gap.
- Any stale generated output or overwrite behavior that could destroy user notes.
- Any test gap that would let unsafe values leak into Markdown, JSON, IDs, links, tags, or diagnostics.

Return actionable findings with file/section references and suggested fixes.
