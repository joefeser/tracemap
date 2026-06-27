# UI Field Property Lineage Terminal Context Vault Local Navigation Implementation State

Status: ready-for-implementation
Readiness: validated-spec-only
Spec branch: `codex/ui-field-property-lineage-terminal-context-vault-local-navigation`
Target base: `dev`
Public claim level: hidden

## Current Context

Fetched `origin/dev` and created an isolated worktree from latest remote
`dev`.

Starting baseline:

```text
c37eff84ebc12cc2b4d47bae89fb8af29b35b8bb
```

That commit is merge commit `#405`, `implement-swift-inventory-project-discovery`.

## Scope

This is a spec-only branch. It creates an implementation-ready Kiro spec for
hidden/local vault navigation over existing property-flow
`terminalContextKind` evidence.

No product code, generated output, site files, docs-export files, rule catalog
entries, scanner logic, reducer logic, or existing specs are changed by this
branch.

## Source Material Reviewed

- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/requirements.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/design.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/tasks.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/implementation-state.md`
- `.kiro/specs/vault-export-hidden-safety/requirements.md`
- `.kiro/specs/vault-export-hidden-safety/design.md`
- `.kiro/specs/evidence-graph-vault-export/design.md`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `docs/VAULT_EXPORT.md`
- `rules/rule-catalog.yml`

## Live-Code Observations

- `PropertyFlowReport.cs` emits `terminalContextKind` as additive node safe
  metadata.
- `StaticTerminalContext` is path-note prose and should remain display text.
- `VaultExport.cs` already has claim-level normalization, generated-file
  sentinels, deterministic graph hashing, graph gap primitives, hidden safety
  classification, stable ID validation, and unsafe-value rejection paths.
- `VaultExport.cs` does not currently read property-flow reports and has no
  existing input seam that carries producer-gated `terminalContextKind`; the
  first implementation therefore needs an explicit compatible property-flow
  report JSON seam.
- Existing vault docs describe hidden/local safety, public/demo strictness, and
  generated-file collision handling.
- Existing rule catalog entries cover property-flow path/edge/schema rules and
  vault safety/omission behavior; this spec prefers reuse before candidate new
  rules.

## Scope Decisions

- This spec implements only the vault PR 2 lane from the consumers runway.
- Docs-export consumer implementation is explicitly out of scope.
- Vault output may render terminal context only from structured
  `safeMetadata["terminalContextKind"]`, not from prose alone.
- Terminal context remains path-scoped and hidden/local.
- Demo/public output must omit or gap terminal-context navigation unless a
  later separate public/demo policy permits static concept rendering.
- Source claim catalog promotion of other evidence does not promote this
  terminal-context navigation.
- Candidate new rule IDs are
  `vault-export.graph.property-flow-terminal-context.v1` and
  `vault-export.gap.terminal-context-omitted.v1`; implementation should reuse
  existing vault rules if sufficient.
- The live catalog does not contain generic `vault-export.graph.v1` or
  `vault-export.gap.evidence-location-category-only.v1`; those must not be
  treated as reusable rules.
- No runtime behavior, DB execution, dependency execution, impact proof,
  release safety, complete coverage, AI/LLM analysis, embeddings, vector
  databases, or prompt-based classification is allowed.

## Implementation Decision Gate

Before any future product-code PR edits `VaultExport.cs`, it must fill this
section with the chosen behavior:

- Vault decision: pending. Choose `hidden-local-render`, `omission-gap-only`,
  or `ignore-with-schema-gap`.
- Rule decision: pending. Record whether existing rules are reused or candidate
  vault-export terminal-context rules are added.
- Test decision: pending. Record the focused test files and cases made
  mandatory by the chosen behavior.
- Schema decision: selected for this spec. The first implementation SHALL use
  an explicit compatible property-flow report JSON seam for vault export, such
  as a narrow `--property-flow-report <property-flow.json>` option or an
  explicit documented report-input collection. It SHALL NOT infer
  terminal-context navigation from combined path evidence alone and SHALL NOT
  add a docs-export file-reading seam for this feature.

Minimum test set by decision option:

| Decision | Mandatory tests |
| --- | --- |
| `hidden-local-render` | Hidden render from structured metadata; absent key; unknown safe value; structured/prose mismatch; demo/public omission gap; source-claim no-promotion; unsafe metadata rejection/omission; multi-path count isolation; deterministic Markdown/graph; generated-file collision/hash; non-claim wording. |
| `omission-gap-only` | Hidden omission gap from structured metadata; absent key; unknown safe value; demo/public omission gap; source-claim no-promotion; unsafe metadata rejection/omission; deterministic Markdown/graph; non-claim wording. |
| `ignore-with-schema-gap` | Schema gap for compatible-but-unrendered terminal metadata; absent key; unknown safe value; demo/public no-promotion; deterministic Markdown/graph; non-claim wording. |

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-vault-local-navigation --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-vault-local-navigation --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Initial review results:

- Opus spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-vault-local-navigation --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage.
  Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-vault-local-navigation/2026-06-27T190102-709Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.
  Medium+ findings patched: removed non-existent reusable rule IDs
  `vault-export.graph.v1` and
  `vault-export.gap.evidence-location-category-only.v1`; clarified that
  current vault inputs do not carry producer-gated `terminalContextKind`;
  required an explicit property-flow report JSON seam; separated preserved
  source evidence rule IDs from candidate vault packaging rules; added
  malformed-input and rule-catalog guard notes; added multi-path count and
  unknown safe-value test obligations.
- Sonnet spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-vault-local-navigation --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage.
  Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-vault-local-navigation/2026-06-27T190102-741Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
  Medium+ findings patched: selected the explicit property-flow report JSON
  input seam; mapped structured/prose mismatch, unknown safe values,
  claim-level omission, safety omission, and partial-output gap behavior;
  specified ordinal lexicographic sorting for supporting IDs; added
  decision-option test matrices and multi-path/demo-public omission tests.

## Validation Log

Spec-only validation planned:

```bash
git diff --check
./scripts/check-private-paths.sh
git diff --name-only origin/dev...HEAD
```

Results:

- `git diff --cached --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Diff scope check: staged diff includes only
  `.kiro/specs/ui-field-property-lineage-terminal-context-vault-local-navigation/`.
  An initial `git diff --cached --name-only origin/dev...HEAD` attempt failed
  because Git does not accept that option/range combination; reran with
  `git diff --cached --name-only`.

## PR Loop Log

- PR #409 opened against `dev`.
- Initial SSH push failed with `Connection closed by 140.82.114.4 port 22`;
  retried with GitHub CLI authenticated HTTPS remote and push succeeded.
- After a bookkeeping commit, waited 3 minutes and ran:
  `agent-control pr-loop --repo joefeser/tracemap --pr 409 --base dev --require-codex-review --quiet --json`.
- Initial ACK returned `not_merge_ready` with stop reason
  `MERGE_STATE_NOT_CLEAN`, merge state `UNKNOWN`, current head
  `1e2e916f93643cf2cb7ec6bd1d9017f4da065dd8`, two Gemini Medium threads, and
  one Qodo actionable comment.
- Patch applied: aligned the terminal-context node example with the current
  `VaultGraphNode` record instead of an unsupported `safeMetadata` property;
  mapped candidate terminal-context gap names to `VaultGraphGap.Classification`;
  changed baseline code blocks to SHA-only form with merge context outside the
  block; kept tasks checkboxes synchronized with completed spec-only PR work.

## Follow-Up Items

- Implementation PRs must update this spec's task checkboxes as tasks are
  completed.
- Any emitted new vault graph/gap rule must be documented in
  `rules/rule-catalog.yml` before product code emits it.
- Public/demo terminal-context concept rendering requires a separate reviewed
  public/demo spec.
