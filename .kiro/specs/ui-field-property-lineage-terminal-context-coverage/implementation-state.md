# UI Field Property Lineage Terminal Context Coverage Implementation State

Status: ready-for-implementation
Readiness: validated-spec-only
Spec branch: `codex/ui-field-property-lineage-terminal-context-coverage`
Target base: `dev`
Public claim level: hidden

## Current Context

Fetched `origin/dev` before drafting and created an isolated worktree/branch
from the current target base. Verified baseline:

```text
5e88a10486a1bf0c088ee681f140c643a2635415
```

That commit is the `dev` merge for PR #400, `[codex] Add property-flow
terminal context gate`.

Before validation and commit, fetched/rebased onto latest `origin/dev`:

```text
4410fd6caaf72ed76f7c7fcff4384bd3744299f9
```

The spec remains intentionally scoped as a follow-up to the PR #400
property-flow terminal-context gate.

## Scope

This is a spec-only branch. It creates one new implementation-ready Kiro spec
for terminal-context coverage and vocabulary hardening after PR #400.

No product code, site files, generated output, rule catalog entries, scanner
logic, reducer logic, existing specs, or public copy are changed by this
branch.

## Source Material Reviewed

- `.kiro/specs/ui-field-property-lineage-terminal-context/requirements.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/design.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/tasks.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/implementation-state.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context/review-prompts.md`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/CombinedTerminalSurfaceKinds.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`
- `rules/rule-catalog.yml`

## Scope Decisions

- Follow PR #400 rather than reopening the broader predecessor terminal-context
  implementation spec.
- Focus on deterministic backend tests for property-flow terminal surface
  vocabulary buckets and exact selected-property bridge behavior.
- Keep public claim level hidden.
- Do not claim runtime behavior, dependency execution, database execution,
  impact proof, AI/LLM analysis, or complete coverage.
- Do not add Swift, site, scanner extraction, reducer, generated-output, or
  existing-spec changes.
- Treat method, endpoint, class, file, same-name, generic-name, route-flow
  context, and broad dependency proximity as negative coverage unless exact
  selected-property identity is present.
- Require rule-catalog and report-version decisions before broader
  terminal-context families or new emitted artifacts are added.

## PR 1 Implementation Slice

Recommended first implementation PR:

1. Add closed vocabulary tests for all current `CombinedTerminalSurfaceKinds`
   values.
2. Add exact selected-property bridge tests for current bridge identities.
3. Add proximity-only negative fixtures for method, endpoint/route, class,
   file, namespace/folder/project when representable, short property name,
   short symbol/method name, generic-name, and broad dependency context.
4. Add rule-catalog resolution assertions for emitted path/surface rule IDs.
5. Record report-version and rule-catalog decisions before product edits.
6. Run focused `PropertyFlowTests`, full solution tests unless deferred,
   private-path guard, and whitespace check.

PR 1 should not add broader terminal-context families or scanner facts.

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Initial Opus review:

- Command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with full coverage.
- Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-coverage/2026-06-27T181453-092Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.
- Findings patched: added a direct mapping-level test seam for closed
  vocabulary enumeration; clarified that per-value coverage does not require
  scanner-like report fixtures for every surface kind; documented exact
  `wcf-operation` asymmetry; required HTTP suppression with an exact bridge
  present; clarified rule-less additive terminal metadata over existing
  path/source rules; added report-version pin/compatibility testing; added raw
  evidence leakage and deterministic note-ordering tasks.

Initial Sonnet review:

- Command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with full coverage.
- Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-coverage/2026-06-27T181902-863Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- Findings patched: added a concrete out-of-vocabulary `wcf-service` /
  `wcf-binding` mapping assertion; explicitly listed the literal
  `dependency-surface` kind; documented all `asmx-*` variants as
  legacy-communication context; documented `message-unknown` as message-surface
  context; specified the `report.Version == "1.0"` pin shape; documented
  ordinal note ordering for `StaticRouteFlowContext` before
  `StaticTerminalContext`.

Bounded Sonnet re-review:

- Command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with full coverage.
- Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-coverage/2026-06-27T182131-662Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- Findings patched: clarified that WCF future-kind behavior falls through the
  catch-all branch and must not be silently absorbed by prefix arms; documented
  ordinal-ignore-case bridge identity as an explicit PR 1 decision/test
  boundary; clarified short-name negative fixture shape; documented
  SurfaceKind mapping tests separately from node-kind terminal heuristics;
  specified ordinal note ordering; added selected combined fact ID isolation
  guidance; added validation-script unavailable fallback wording.

Final Sonnet re-review:

- Command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context-coverage --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with full coverage.
- Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context-coverage/2026-06-27T182427-271Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- Findings patched: named `asmx-service` and `asmx-operation` in the ASMX
  vocabulary decision; documented that the literal `dependency-surface` kind
  reaches the catch-all branch; required deferred case-boundary decisions to
  resolve before later bridge expansion; added `result`/`response` to generic
  negative fixture guidance; added current-catalog verification wording for
  named rule IDs.

## Validation Log

Planned spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
git diff --name-only
```

Results:

- `git diff --cached --check`: passed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed (`Private path guard passed.`).
- `git diff --cached --name-only`: limited to the five files in
  `.kiro/specs/ui-field-property-lineage-terminal-context-coverage/`.

## ACK / PR State

PR: pending.
ACK: pending.
Auto-merge: pending.
