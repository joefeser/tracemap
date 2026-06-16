# Evidence Graph Vault Export Implementation State

Status: ready-for-pr-review
Branch: codex/spec-evidence-graph-vault-export
Scope: spec-only
Public claim level: concept
Readiness: patched-after-opus-and-sonnet-review

## Summary

This spec defines a deterministic evidence graph/vault export for TraceMap. The
feature would turn existing static evidence artifacts into Markdown notes plus an
optional JSON graph manifest for local navigation and demos. Obsidian-compatible
Markdown is an intended consumer shape, but the feature remains a local export
over rule-backed static evidence.

The spec does not implement exporter code, site pages, runtime analysis, AI/LLM
classification, embeddings, vector databases, graph databases, or prompt-based
classification.

## Scope Decisions

- Spec-only branch; implementation code is intentionally out of scope.
- Write scope is limited to `.kiro/specs/evidence-graph-vault-export/`.
- Supported input classes are combined indexes, portfolio reports,
  paths/reverse reports, release-review reports, and evidence packets where
  schemas are compatible.
- Generated output is a deterministic Markdown vault plus optional `graph.json`.
- Obsidian compatibility is plain Markdown compatibility with safe links, tags,
  and frontmatter; TraceMap does not depend on Obsidian.
- Exported graph edges are static evidence relationships and never runtime
  execution traces.
- Site work may later consume public-safe summaries, but this spec does not
  create site files or public copy.

## Review State

Commands:

```bash
node scripts/kiro-review.mjs --self-test
node scripts/kiro-review.mjs --phase evidence-graph-vault-export --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-graph-vault-export --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-graph-vault-export --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
./scripts/check-private-paths.sh
git diff --check
```

Completed:

- Initial spec files generated:
  - `requirements.md`
  - `design.md`
  - `tasks.md`
  - `review-prompts.md`
  - `implementation-state.md`
- Kiro wrapper self-test passed.
- Opus and Sonnet spec reviews completed with full coverage.
- Blocking and important Medium+ findings patched:
  - Claim-level defaults and promotion now require stable source identity,
    source claim catalog proof, or compatible evidence-pack metadata.
  - `tracemap vault export` avoids the existing `tracemap export --format`
    command shape.
  - Generated Markdown metadata moved into parseable top-of-file YAML
    frontmatter.
  - `graph.json` now carries a deterministic `contentHash`.
  - Stable ID hash inputs, context strings, and collision behavior are
    specified.
  - Surface families use `kind: surface` plus `surfaceKind`.
  - Evidence-pack support is deferred until a locked v1 claim schema exists.
  - Hashing policy distinguishes safe high-entropy IDs from values that must be
    omitted or category-labeled.
  - Tests now cover force-gate bypass attempts, stale manifests, frontmatter
    parseability, output-root byte stability, no-visible-node filtering, and
    source claim catalog promotion.
- Sonnet re-review completed with full coverage.
- Re-review findings patched:
  - Stale generated file collision behavior is no longer contradictory.
  - Markdown and `graph.json` content hash scopes are explicit.
  - Normal re-export versus `--force` overwrite behavior is aligned between
    requirements and design.
  - `--format` behavior is listed in requirements and test tasks.
  - Source stable IDs no longer depend on claim-catalog promotion.
  - Diff and impact report inputs are explicitly deferred until compatible
    schemas are confirmed.
  - Exporter-created gaps and validation findings use a documented
    `vault-export.*.v1` rule namespace.
  - Omission gaps are required when hidden relationship removal affects graph
    interpretation; summary counts only supplement them.
  - Frontmatter key ordering is canonical and tested.
- Final Sonnet re-review completed with no blocking findings.
- Final important polish findings patched:
  - Markdown frontmatter example now shows the complete canonical generated
    metadata block.
  - Surface stable IDs exclude mutable supporting fact ID sets.
  - Demo-safe filtering fails when no non-gap visible graph nodes remain.
  - Source claim catalog compatibility includes proof ID.
  - `graph.json` string-leaf redaction scanning is explicit in tasks.
  - `InputClaimLevelHidden` behavior is defined as a filter-time failure, not a
    default-hidden input failure.

## Validation

Pending final branch validation:

- `./scripts/check-private-paths.sh`.
- `git diff --check`.

## Follow-Ups For Implementation

- Decide whether the first implementation belongs in .NET CLI or a temporary
  script fallback.
- Define exact graph schema and generated Markdown sentinel.
- Build redaction tests before generating demo/public vaults.
- Keep generated vault outputs out of tracked files unless a later spec
  explicitly promotes public-safe fixtures.
