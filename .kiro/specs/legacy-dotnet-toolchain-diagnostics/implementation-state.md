# Legacy .NET Toolchain Diagnostics Implementation State

Status: not-started
Branch: codex/spec-legacy-dotnet-toolchain-diagnostics
Spec path: `.kiro/specs/legacy-dotnet-toolchain-diagnostics/`

## Current Scope

This is a spec-only branch. It creates the Kiro spec for a future deterministic
legacy .NET analyzer capability and toolchain coverage contract. It does not
implement product code.

The spec intentionally builds on the implemented
`legacy-build-environment-diagnostics` feature. That earlier feature emits
`BuildEnvironmentDiagnostic` facts for static target framework, toolset,
project-format, restore, generated-file, and sanitized workspace diagnostics.
This spec asks for a follow-on `AnalyzerCapabilityDiagnostic` layer that
summarizes which analyzer capability was available, reduced, unavailable, not
requested, or unknown, and how those states affect downstream no-evidence and
coverage explanations.

## Scope Decisions

- Keep the MVP centered on existing scan evidence, project/config/static
  metadata, artifact/report integration, and one downstream reduced-coverage
  explanation path.
- Reuse existing build-environment facts as supporting evidence instead of
  duplicating raw project values.
- Keep restore-not-requested as scan-option/capability context, not a missing
  package claim.
- Treat static framework and toolset signals as informational unless scan
  behavior observes semantic load, restore, reference assembly, SDK, toolset, or
  generated/design-time linkage gaps.
- Preserve all privacy constraints: no raw local absolute paths, raw remotes,
  package source URLs, config values, connection strings, snippets, secrets, or
  raw native tool output in shareable artifacts.

## Oddities And Risks

- The name overlaps with the already implemented
  `legacy-build-environment-diagnostics` spec. The distinction is intentional:
  build-environment facts describe static signals; this spec describes
  analyzer capability and downstream coverage effects.
- Broad downstream integration could grow large. The design recommends an MVP
  that proves combined-index propagation and one release-review explanation
  before widening every report/export surface.
- Manifest schema additions are optional for MVP if facts, SQLite, and report
  output provide enough stable contract.
- Direct evidence graph/vault export, legacy smoke catalog, and evidence-pack
  schema-version presentation is explicitly deferred from the MVP. Future work
  must add closed capability-code consumption or schema compatibility-gap tests
  before claiming those consumers support `AnalyzerCapabilityDiagnostic`.

## Validation Log

- Completed: Kiro CLI Opus spec review with
  `node scripts/kiro-review.mjs --phase legacy-dotnet-toolchain-diagnostics --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  Full coverage; patched blocking scope/fact-ID findings and Medium+ clarity
  findings.
- Completed: Kiro CLI Sonnet spec review with
  `node scripts/kiro-review.mjs --phase legacy-dotnet-toolchain-diagnostics --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Full coverage; patched blocking vocabulary/deferral findings and Medium+
  clarity findings.
- Completed: Kiro CLI Sonnet re-review with
  `node scripts/kiro-review.mjs --phase legacy-dotnet-toolchain-diagnostics --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Full coverage; no blocking findings. Patched the remaining non-blocking
  release-review naming, tier-cap, orphaned-code, and schema-version test
  clarity findings.
- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed: `node scripts/kiro-review.mjs --self-test`.
- Passed: targeted unsafe-value grep over this spec folder found no local paths,
  token patterns, connection-string markers, package credential markers, private
  keys, or raw remote markers.
- No general Kiro spec validator was found; this branch used the local Kiro
  review wrapper and its self-test as the available spec-review tooling.
- PR loop initially reported four unresolved review threads. Patched the
  actionable findings by clarifying string-only fact property encoding,
  aligning limitation-code examples with the closed vocabulary, adding the
  release-review fact-loader task, and keeping `informational` as a
  `coverageEffect` rather than a capability state.
- Passed after PR-loop patch: `git diff --check`.
- Passed after PR-loop patch: `./scripts/check-private-paths.sh`.

## Follow-Up Items

- Implement product code in a separate future branch/spec implementation loop.
- Add public/demo-safe validation only after checked-in fixtures or redacted
  summaries prove the capability.
