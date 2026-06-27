# UI Field Property Lineage Terminal Context Consumers Implementation State

Status: ready-for-implementation
Readiness: validated-spec-only
Spec branch: `codex/ui-field-property-lineage-terminal-context-consumers`
Target base: `dev`
Public claim level: hidden

## Current Context

Fetched `origin/dev` before drafting. Created an isolated worktree because the
original checkout had unrelated in-flight changes.

Verified starting baseline:

```text
5e88a10486a1bf0c088ee681f140c643a2635415
```

That commit is `[codex] Add property-flow terminal context gate (#400)` on
`dev`.

## Scope

This is a spec-only branch. It creates an implementation-ready follow-up spec
for backend/docs-export/vault/reporting consumers of PR #400 property-flow
terminal-context metadata.

No product code, site files, generated output, rule catalog entries, scanner
logic, reducer logic, export code, or existing specs are changed by this branch.

## Source Material Reviewed

- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `.kiro/specs/ui-field-property-lineage-terminal-context/requirements.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/design.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/tasks.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/implementation-state.md`
- `.kiro/specs/ui-field-property-lineage-composition/design.md`
- `.kiro/specs/vault-export-hidden-safety/requirements.md`
- `.kiro/specs/evidence-graph-vault-export/design.md`
- `.kiro/specs/evidence-export-usability-polish/review-prompts.md`
- `scripts/kiro-review.mjs --help`

## Live-Code Observations

- `PropertyFlowPath` has additive `Notes`.
- `PropertyFlowNode` has additive `SafeMetadata`.
- `PropertyFlowReporter.ToNode` writes `terminalContextKind` only when the
  caller passes `includeTerminalContext`.
- `includeTerminalContext` is set by `HasSelectedPropertyBridge`.
- `PathNotes` adds `StaticTerminalContext` only when the last path node has a
  recognized terminal context kind.
- `TerminalContextKind` maps known surface kinds to static terminal context
  labels and excludes `http-client` and `http-route`.
- Existing property-flow Markdown renders path notes. Node safe metadata is
  present in JSON.
- `EvidenceDocsExport` already has safe metadata key/value validation and
  hashes unsafe metadata values in several paths.
- `VaultExport` already consumes path-report notes safely for combined paths
  and has graph/gap primitives for deterministic local navigation.

## Scope Decisions

- This spec follows PR #400 and does not reopen the terminal-context producer
  gate.
- Consumers must prefer structured `terminalContextKind` safe metadata over
  parsing `StaticTerminalContext` prose.
- Path notes remain bounded display text, not primary evidence.
- Current producer terminal-context display values are
  `data-surface terminal context`, `legacy-data terminal context`,
  `package/config terminal context`, `message-surface terminal context`,
  `legacy-communication terminal context`, and
  `dependency-surface terminal context`.
- Docs export treats terminal context as retrieval metadata only.
- Vault export treats terminal context as hidden/local path-scoped navigation
  unless a separate reviewed demo/concept policy permits more.
- Reporting changes are optional; existing property-flow report output may
  already be sufficient if consumers can safely ignore additive metadata.
- Any new emitted chunk family, graph node kind, graph edge kind, gap code,
  limitation, or redaction category is rule-catalog-first.
- SQLite/facts/reports/rule catalog remain authoritative.
- No runtime behavior, DB execution, impact proof, AI/LLM analysis, or complete
  coverage claim is allowed.
- Public claim level remains hidden.

## Recommended First Implementation Slice

PR 1 should be docs-export compatibility first:

1. Audit property-flow report JSON and docs-export report-family packaging.
2. Decide whether docs export safely ignores terminal context or renders it as
   retrieval metadata.
3. If rendering, use structured `terminalContextKind` and preserve evidence
   identity fields where available.
4. Add docs-export tests for safe render/ignore, unsafe metadata handling,
   deterministic output, and non-claim wording.
5. Update docs and rule catalog only if the PR emits a new artifact family or
   gap.

Vault hidden/local graph rendering should be a later PR unless docs-export
audit proves a smaller shared consumer helper is required.

### Implementation Decision Gates

Before any consumer implementation edits product code, the implementation PR
must fill the relevant decision:

- Docs export decision: pending. Choose `ignore`, `render`, or `schema-gap`.
  The chosen behavior makes the matching tests mandatory.
- Vault export decision: pending. Choose `ignore`, `hidden-local-render`, or
  `omission-gap`. The chosen behavior makes the matching tests mandatory.
- Reporting decision: pending. Choose `unchanged`, `additive-render`, or
  `versioned-schema`. The chosen behavior makes the matching tests mandatory.

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-consumers --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-consumers --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Initial review results:

- Opus spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-consumers --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with reduced coverage because Kiro reported denied shell access.
  Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-consumers/2026-06-27T181427-183Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.
  Analysis gap recorded by wrapper: `ToolDenied`, rule
  `kiro.review.wrapper.v1`, evidence tier `Tier4Unknown`.
  Findings patched: corrected full PR #400 baseline SHA to
  `5e88a10486a1bf0c088ee681f140c643a2635415`; changed candidate docs-export
  rule naming to `docs-export.chunk.property-flow-terminal-context.v1`; made
  docs/vault render-or-ignore decisions explicit implementation gates with
  mandatory matching tests; documented the closed terminal-context vocabulary;
  clarified structured/prose mismatch is a malformed-input guard; added missing
  test obligations for mismatch, absence as unknown, claim-level omission gaps,
  and forward-compatible unknown safe metadata.
- Sonnet spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-consumers --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with reduced coverage because Kiro reported denied shell access.
  Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-consumers/2026-06-27T181939-086Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
  Analysis gap recorded by wrapper: `ToolDenied`, rule
  `kiro.review.wrapper.v1`, evidence tier `Tier4Unknown`.
  Findings patched: named `safeMetadata["terminalContextKind"]` as the primary
  consumer key; required docs-export implementation to choose augmented
  existing chunk family versus new catalogued family before product edits;
  clarified absent-key behavior; added scan-ID unavailable handling for vault
  IDs; added test obligations for malformed structured/prose mismatch, absent
  key, WCF mapping, unknown surface catch-all mapping, HTTP exclusion,
  zero-node malformed path, multi-note ordering, and named claim-level omission
  gaps.

## Validation Log

Spec-only validation planned:

```bash
git diff --check
./scripts/check-private-paths.sh
git diff --name-only origin/dev...HEAD
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Diff scope check: only `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`
  is changed.

## PR Loop Log

- PR #403 opened against `dev`.
- Initial ACK command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 403 --base dev --require-codex-review --quiet --json`
  returned `not_merge_ready` with stop reason `MERGE_STATE_NOT_CLEAN`.
  Current actionable review findings at that time:
  - Gemini comment on `design.md` requested vault-export omitted-context
    candidate rule naming follow existing `vault-export.gap.*.v1`
    convention.
  - Qodo comment on `tasks.md` requested completed spec-only workflow tasks be
    checked.
- Patch applied: renamed candidate vault rules to
  `vault-export.graph.property-flow-terminal-context.v1` and
  `vault-export.gap.terminal-context-omitted.v1`; checked the completed
  spec-only commit/push/PR/wait/ACK workflow tasks.
- Post-patch validation: `git diff --check` passed and
  `./scripts/check-private-paths.sh` passed.
- Second ACK command on head `3ab03b04651f03793c27fc5f1e4c155c8bdff23b`
  returned `actionable_findings` with one Codex thread. Patch applied:
  removed producer `surfaceKind` mapping test obligations from docs-export
  consumer tests and clarified that docs/vault consumers should use
  already-emitted structured metadata and absent-metadata fixtures.
- Post-second-patch validation: `git diff --check` passed and
  `./scripts/check-private-paths.sh` passed.
- Third ACK command on head `363eecd51475c156d9b8a0acde7b3b7d1c02cdc1`
  still reported `actionable_findings` from the Qodo top-level comment. The
  remaining live Qodo issue was stale `tasks.md` packet status. Patch applied:
  aligned `tasks.md` header to `ready-for-implementation` /
  `validated-spec-only`.
- Post-third-patch validation: `git diff --check` passed and
  `./scripts/check-private-paths.sh` passed.

## Follow-Up Items

- Implementation PRs must update this spec's task checkboxes as tasks are
  completed.
- If new emitted consumer rules or gaps are required, update
  `rules/rule-catalog.yml` before output paths can emit them.
- If demo/concept claim rendering is proposed, create a separate public/demo
  claim spec before touching site files or public copy.
