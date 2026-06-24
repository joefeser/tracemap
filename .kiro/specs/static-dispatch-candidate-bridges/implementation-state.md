# Static Dispatch Candidate Bridges Implementation State

## Branch

- Branch: `codex/spec-static-dispatch-candidate-bridges`
- Base: `origin/dev`
- Scope: spec-only creation loop
- Suggested PR target: `dev`

## Current Status

Status: `ready-for-implementation`

This spec narrows the broad interface/override/DI approximation backlog into a
reviewable next slice: shared static dispatch candidate bridges derived from
existing call edges, symbol relationships, and optional DI registration
evidence, then consumed conservatively by route-flow, reverse, impact, report,
vault export, and docs-export/RAG-oriented artifacts.

No product code is implemented on this branch.

## Scope Decisions

- Candidate bridges are static candidate evidence only. They do not prove
  runtime dispatch, runtime DI binding, selected implementations, production
  traffic, or runtime impact.
- Reuse `combined.dispatch-candidate.v1` and `combined.dispatch-gap.v1` for
  shared candidate edge/gap semantics unless a future implementation adds a
  documented successor rule before emitting product behavior.
- Route-flow, reverse, impact, report, vault, and docs-export should preserve
  their consumer-specific presentation rule IDs while carrying dispatch
  candidate rule IDs in supporting evidence.
- DI registration support is an annotation on relationship-backed candidates,
  not proof of runtime service selection.
- Open generics, factories, scanning, keyed/named services, decorators, service
  locators, reflection, config, dynamic branches, and custom containers remain
  review context or gaps in v1.
- Candidate output must be deterministic, capped, stable-ID-backed,
  public-safe, and review-tier.

## Files

- `.kiro/specs/static-dispatch-candidate-bridges/requirements.md`
- `.kiro/specs/static-dispatch-candidate-bridges/design.md`
- `.kiro/specs/static-dispatch-candidate-bridges/tasks.md`
- `.kiro/specs/static-dispatch-candidate-bridges/implementation-state.md`
- `.kiro/specs/static-dispatch-candidate-bridges/review-prompts.md`

## Kiro Review State

Initial reviews completed with full wrapper coverage.

Review commands:

```bash
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-opus-4.8 --fresh --save-review-text
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-sonnet-4.5 --fresh --save-review-text
```

After patching Medium+ findings, run one bounded re-review:

```bash
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind re-review --model claude-sonnet-4.5 --fresh --save-review-text --timeout-ms 900000
```

Exact artifacts:

- Opus spec review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T030855-125Z-spec-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T030855-125Z-spec-claude-opus-4.8.meta.json`
- Sonnet spec review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T031244-230Z-spec-claude-sonnet-4.5.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T031244-230Z-spec-claude-sonnet-4.5.meta.json`

Coverage:

- Opus: full wrapper coverage, `reviewComplete = true`, `timedOut = false`.
- Sonnet: full wrapper coverage, `reviewComplete = true`, `timedOut = false`.
  The Sonnet wrapper metadata unexpectedly recorded git branch
  `codex/spec-route-flow-service-data-composition-final`, and the checkout was
  found on `codex/spec-legacy-data-model-orm-mapping-completion` after the
  review. The checkout was switched back to
  `codex/spec-static-dispatch-candidate-bridges` before patching. No product
  code was edited.
- Final Sonnet re-review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T032048-194Z-re-review-claude-sonnet-4.5.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T032048-194Z-re-review-claude-sonnet-4.5.meta.json`.
  The re-review completed with `reviewComplete = true`, `timedOut = false`,
  and reduced coverage because Kiro reported denied `execute_bash` tool access
  under `kiro.review.wrapper.v1`. It found no remaining blockers and said the
  spec was ready to merge after local validation.

## Review Results

Medium+ findings patched:

- Mandated reading original `relationship_kind` metadata from
  `combined_symbol_relationships` rather than relying only on normalized graph
  edge kinds such as `implements` or `inherits`.
- Replaced shared emitted classification labels with internal candidate states:
  `SymbolBackedCandidate`, `WeakerCandidate`, and `CandidateGap`, plus
  consumer-specific caps.
- Clarified that existing `StaticDispatchCandidate` is a paths note code, not a
  strengthening shared classification.
- Added catalog gate language for expanding `combined.dispatch-gap.v1` or
  adding a successor before emitting registration, generic, schema, identity,
  or missing-candidate gaps.
- Added gap reconciliation for `RuntimeBindingNotProven`,
  `DynamicDispatchBoundary`, `RegistrationCompatibilityUnproven`,
  `UnsupportedRegistrationShape`, and `DispatchCandidateFanOut`.
- Renamed DI wording from registration-supported to registration-context so
  static registration evidence does not imply runtime binding.
- Added type-level-only bridge behavior, explicit interface implementation
  symbol guidance, override depth bounds, volatile ID handling, route-flow
  `interface-bridge` row schema, vault/docs-export rule audits, and missing
  tests for byte stability, forbidden wording, and DI compatibility gaps.
- Final re-review non-blocking suggestion patched by clarifying that existing
  gap codes should be reused where semantics already match rather than
  creating parallel aliases.

## Validation State

Spec-only validation completed:

```bash
git diff --cached --check
./scripts/check-private-paths.sh
git diff --cached --name-only | rg -v '^\.kiro/specs/static-dispatch-candidate-bridges/' || true
```

Results:

- `git diff --cached --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Staged diff scope check: passed; no staged paths outside
  `.kiro/specs/static-dispatch-candidate-bridges/`.

## Safety Notes

The spec avoids private local paths in examples, raw source snippets, raw SQL,
raw config values, URLs, hostnames, raw remotes, private labels, and secrets.
Implementation PRs must keep generated artifacts public-safe or hidden/local as
appropriate.

## Follow-Up Items

- Patch Medium+ Kiro review findings before opening the implementation-ready
  spec PR.
- Use the PR loop after PR creation and record the terminal ACK decision in the
  final handoff.

## PR Review Loop Notes

- Initial ACK run with installed `agent-control` returned
  `environment_blocked` / `LOCAL_BUILD_STALE`.
- Fallback through `<agent-control-kit checkout>` using
  `npm run dev -- pr-loop ...` crashed with
  `ReferenceError: withRunArtifactsReadback is not defined`.
- Ran `npm run build` in `agent-control-kit` to refresh `dist`, then reran the
  installed `agent-control pr-loop`.
- ACK posted/observed the required Codex review request and initially returned
  `nextAction = wait_for_required_reviewers`; no review findings were patched
  until the follow-up status snapshot showed Codex and Qodo had both returned
  and `patchAuthorized = true`.
- Patched authorized review findings:
  - aligned stable ID inputs between requirements and design;
  - aligned deterministic sort keys between requirements and design;
  - changed route-flow row kind from provisional `interface-bridge` to existing
    `interface-implementation-candidate`;
  - clarified that `WeakerCandidate` requires relationship-backed evidence and
    does not permit registration-only candidates.
