# Legacy Build Environment Diagnostics Implementation State

Status: ready-for-implementation
Branch: codex/legacy-build-environment-diagnostics-spec
Scope: spec-only
Public claim level: hidden

## Summary

This spec defines deterministic scan/build environment diagnostics for legacy
.NET repositories. It is intended to help TraceMap explain reduced coverage when
old target frameworks, missing SDK/runtime/MSBuild/toolset, unsupported project
formats, NuGet restore blockers, Web Application project quirks, or
generated/designer-file gaps prevent full semantic analysis.

## Scope Decisions

- Spec-only branch; no scanner implementation in this worktree.
- Diagnostics are machine-readable facts/gaps and report sections, not runtime
  claims.
- Guidance must be conservative and evidence-backed.
- TraceMap must not install or mutate local tooling.
- Semantic failure remains reduced coverage; syntax/config fallback remains
  useful evidence.
- No local sample repository names, absolute paths, raw remotes, raw SQL, config
  values, secrets, package source credentials, or source snippets are committed.
- No LLM, embedding, vector database, or prompt-based classification belongs in
  TraceMap core.

## Review State

- Initial spec draft created.
- Opus first-pass review completed; it found blockers around existing raw
  workspace/restore message leakage, sanitized-only fact IDs, and inventory
  scope.
- Sonnet first-pass review completed; it found blockers around existing raw
  workspace/restore gap remediation, rule-catalog timing, and hash algorithm
  specificity.
- Review fixes applied to requirements, design, and tasks:
  - existing `csharp.semantic.workspace.v1` and restore gap message remediation
    is explicit;
  - diagnostic fact IDs must derive from sanitized fields only;
  - observed-value hash behavior is specified;
  - inventory extension vs. explicit unsupported-inventory gaps is pinned;
  - restore-not-requested is scan-option state rather than structural evidence;
  - combine/snapshot/portfolio compatibility tests are required;
  - `check-private-paths.sh` is documented as a tracked-file guard, not the
    artifact redaction gate.
- Re-review pending.
- Opus re-review completed with no blocking issues. It recommended precision
  fixes around capture-time sanitization, the `KnownGaps` fan-out,
  `RestoreNotRequested` representation, and `messageHash` compatibility; those
  fixes were folded into requirements, design, and tasks.
- PR review loop found two Gemini medium comments. Both were addressed in
  `design.md`: non-C# project files now require distinct inventory kinds or C#
  extractor filtering, and the report table template includes a Markdown
  separator row.
- Spec is ready for implementation after safety checks.

## Validation

Completed:

- Opus spec review.
- Sonnet spec review.
- Opus re-review after fixes.
- Repo spec validation command discovery.
- `node scripts/kiro-review.mjs --self-test`.
- `./scripts/check-private-paths.sh`.
- `git diff --check`.
- `git diff --cached --check`.

Repo spec validation command discovery found the Kiro wrapper self-test. No
broader non-site spec validator was present.

## Follow-Ups For Implementation

- Keep implementation tasks unchecked until code lands.
- Update this file and check off tasks only in the implementation PR.
- Run or explicitly defer relevant pinned smoke checks from `docs/VALIDATION.md`
  when implementation changes language adapter or report behavior.
