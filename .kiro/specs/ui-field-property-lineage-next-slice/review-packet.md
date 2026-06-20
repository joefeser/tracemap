# TraceMap Kiro Review Request

Expected mode: Review mode. Findings first, severity ordered; do not edit
files.

Repository: joefeser/tracemap
Phase: ui-field-property-lineage-next-slice
Kind: spec

## Review Goal

Review this continuation Kiro spec for the next UI field/property lineage
implementation slice. The original `.kiro/specs/ui-field-property-lineage`
spec and initial property-flow implementation slices already exist. This new
spec should be focused, implementable, public-safe, and static-evidence-only.

## Key Boundary

This is a spec-only PR. It should not require product-code changes.

The next product-code slice should connect existing Angular/Razor
field/control/binding evidence to DTO/model properties and downstream static
route/path/reverse/data/dependency/export evidence. Browser/computer-use
evidence should remain deferred/demo-only unless a future opt-in workflow is
explicitly specified.

## Review Checklist

Please check:

- Does the continuation avoid duplicating the broad original UI lineage spec?
- Is the first implementation PR boundary small and reviewable?
- Are selector, generic fan-out, ambiguity, DTO/model identity, and same-name
  downgrade rules precise enough?
- Are downstream route-flow/path/reverse/data/dependency/vault/docs/export
  integrations evidence-backed without runtime claims?
- Are optional browser/computer-use observations clearly outside the core
  deterministic static evidence model?
- Are safety requirements strong enough to avoid private paths, private labels,
  raw routes, raw SQL, config values, snippets, raw remotes, hostnames, and
  secrets?
- Are validation and fixture expectations specific enough for implementation?

## TraceMap Principles

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Failed build is not a clean repo.
- Partial analysis is useful, but must be labeled partial.
- Prefer deterministic, testable extractors.
- Do not add LLM calls, embeddings, vector databases, or prompt-based
  classification in core scanner/reducer/reporting logic.

## Required Return Format

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested fixes.
- Missing tests.
- Whether this spec is ready to merge after fixes.

## Files To Review

Review:

- `.kiro/specs/ui-field-property-lineage-next-slice/requirements.md`
- `.kiro/specs/ui-field-property-lineage-next-slice/design.md`
- `.kiro/specs/ui-field-property-lineage-next-slice/tasks.md`
- `.kiro/specs/ui-field-property-lineage-next-slice/implementation-state.md`

Context:

- `.kiro/specs/ui-field-property-lineage/`
- Current `tracemap property-flow` implementation and tests.
- Public issue #165 and related issue #159.
