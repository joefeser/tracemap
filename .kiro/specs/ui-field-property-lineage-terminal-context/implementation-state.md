# UI Field Property Lineage Terminal Context Implementation State

Status: ready-for-implementation
Readiness: validated-spec-only
Spec branch: `codex/ui-field-terminal-context-spec`
Target base: `dev`
Public claim level: hidden

## Current Context

Fetched `origin/dev` before drafting. Initial verified PR #376 baseline:

```text
4b5844ff07199969eacd040e9383037d0b266d49
```

Context note confirmed: `main`/`dev` were recently aligned at `4b5844ff`.

Fetched again after initial Kiro reviews. Latest verified `origin/dev`, and the
fast-forwarded spec branch base before re-review:

```text
6bec000244340311cc385e4ebdeee4655a7251d4
```

PR #376, `[codex] Harden property-flow route schema gaps`, merged into `dev`
on 2026-06-26. Merge commit:

```text
21343d88e795d0f0348dae361036241c128a343b
```

PR #376 added `UnsupportedRouteFlowSchema` and property-flow route-schema gap
evidence. The live code now distinguishes missing, empty, available, and
unsupported `combined_route_flow_edges` schema signals.

## Scope

This is a spec-only branch. It creates an implementation-ready .NET/core spec
for the next backend-only property-flow terminal-context slice.

No product code, generated outputs, site files, rule catalog entries, scanner
logic, reducer logic, or export code are changed by this branch.

## Source Material Reviewed

- `.kiro/specs/ui-field-property-lineage/requirements.md`
- `.kiro/specs/ui-field-property-lineage/design.md`
- `.kiro/specs/ui-field-property-lineage/tasks.md`
- `.kiro/specs/ui-field-property-lineage-next-slice/implementation-state.md`
- `.kiro/specs/ui-field-property-lineage-continuation/implementation-state.md`
- `.kiro/specs/ui-field-property-lineage-composition/requirements.md`
- `.kiro/specs/ui-field-property-lineage-composition/design.md`
- `.kiro/specs/ui-field-property-lineage-composition/tasks.md`
- `.kiro/specs/ui-field-property-lineage-composition/implementation-state.md`
- `.kiro/specs/route-flow-service-data-composition/design.md`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`
- GitHub PR #376 metadata.

## Scope Decisions

- This spec does not reopen UI root extraction, selector parsing, or
  route-flow schema availability work.
- PR 1 should attach at most one narrow backend terminal context family.
- Terminal context requires an existing selected-property trail.
- Broad endpoint reachability, route reachability, same method, same class,
  same file, same short symbol name, and same property name are negative test
  cases, not attachment evidence.
- Route-flow context groups, touched files, touched symbols, logic rows,
  dependency surfaces, path rows, and reverse rows are supporting context only
  after a selected-property bridge exists.
- New emitted rule IDs and gap codes are rule-catalog-first.
- `property-flow` remains a deterministic static evidence report. It must not
  claim runtime behavior, production execution, browser visibility,
  authorization, database execution, impact proof, AI/LLM analysis, or complete
  coverage.
- Public claim level is hidden.

## PR 1 Implementation Slice

Recommended PR 1:

1. Audit current report/schema/fact families and choose one terminal context
   kind supported by existing facts.
2. Add catalog-first rule/gap coverage if needed.
3. Attach the chosen terminal context only through an existing
   selected-property bridge.
4. Add negative tests for broad endpoint, route, method, class, file, and
   property-name proximity.
5. Add one positive property-specific fixture and one insufficient-evidence gap
   fixture.
6. Run focused `PropertyFlowTests`, relevant route-flow/path/reverse/export
   tests if touched, full `dotnet test`, private-path guard, and whitespace
   check.

### PR 1 Audit Decision Placeholder

Implementation PR 1 must fill this section before product-code changes:

- Chosen terminal context kind:
- Existing facts and tables/views consumed:
- Existing rules reused:
- Whether `property-flow.terminal-context.v1` is needed:
- New gap codes, if any, and catalogued rule mapping:
- Generic-name set decision, including whether `result` and `response` require
  live `PropertyFlowReporter` and test updates:
- Consumer compatibility decision for report version `1.0` versus a version
  bump:
- Validation commands planned from `docs/VALIDATION.md`:

Do not attach terminal context or emit candidate terminal-context gaps until
these decisions are recorded and rule-catalog/test prerequisites are satisfied
in the implementation PR.

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Initial review results:

- Opus spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage. The wrapper process returned non-zero because
  the review found blockers. Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context/2026-06-27T164803-435Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.
  Findings patched: gap-code authorization now requires catalogued rule
  mapping; same short symbol and broad endpoint dependency-edge negative tests
  were added; consumer compatibility, gap-code catalog mapping, weak
  `NeedsReviewLineage`, generic-name precedence, and stable hash requirements
  were added.
- Sonnet spec review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed successfully with full coverage. Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context/2026-06-27T165013-997Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
  Findings patched: review results are recorded here; predecessor
  `ui-field-property-lineage-next-slice` and
  `ui-field-property-lineage-continuation` were added to source material and
  relationship notes; rule reuse decisions must be recorded before code;
  read-write terminal context is included in negative tests; generic-name
  precedence and report version bump criteria are clarified.

Pending: one bounded re-review after patches.

Bounded re-review:

- Sonnet re-review command:
  `node scripts/kiro-review.mjs --phase ui-field-property-lineage-terminal-context --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed successfully with full coverage after the initial review patches
  and after the branch was fast-forwarded to `origin/dev` at `6bec0002`.
  Artifacts:
  `.tmp/kiro-reviews/ui-field-property-lineage-terminal-context/2026-06-27T165334-760Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
  Findings patched: candidate terminal-context gap codes/rules now require
  catalog entries, emitted-artifact documentation, and tests in the same
  implementation PR before first emission; task 4 now requires no silent
  path/edge classification upgrades; task 3 cross-references the required
  consumer compatibility test; task 5 requires the weak-evidence explanation
  to name any generic-name narrowing criterion.

## Validation Log

Spec-only validation:

```bash
git diff --cached --check
./scripts/check-private-paths.sh
git diff --cached --name-only
```

Results:

- Initial `git diff --cached --check` found one extra blank line at EOF in
  `review-prompts.md`; patched before final validation.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --cached --name-only`: limited to
  `.kiro/specs/ui-field-property-lineage-terminal-context/`.
- Final `git diff --cached --check`: passed.
- Final `git diff --check`: passed.
- Final `./scripts/check-private-paths.sh`: passed.
- Final staged scope remained limited to the five files in
  `.kiro/specs/ui-field-property-lineage-terminal-context/`.

## ACK / PR State

PR opened:

```text
https://github.com/joefeser/tracemap/pull/396
```

Initial ACK command after the requested 3-minute wait:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 396 --base dev --require-codex-review --quiet --json
```

Result: `actionable_findings` / `UNRESOLVED_REVIEW_THREADS` with
`nextAction: patch_actionable_findings`.

Patch disposition:

- Added this PR 1 audit decision placeholder.
- Added a task requiring implementation PRs to update the live
  `PropertyFlowReporter` generic-name set/tests when documented generic names
  such as `result` and `response` are introduced or retained.
- Checked completed spec-only PR lifecycle tasks after commit, push, PR open,
  wait, and initial ACK.

Follow ACK-authorized actions only. Do not manually tag Codex, Qodo, Gemini,
or Sourcery. Do not force-push or squash. Auto-merge is authorized only through
the Agent Control Kit executor after ACK returns a clean exact-head
`merge_ready` result for `dev`.
