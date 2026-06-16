# Legacy Sample Evidence Pack Review Prompts

Use these prompts after reading:

- `.kiro/specs/legacy-sample-evidence-pack/requirements.md`
- `.kiro/specs/legacy-sample-evidence-pack/design.md`
- `.kiro/specs/legacy-sample-evidence-pack/tasks.md`
- Nearby specs:
  - `.kiro/specs/legacy-codebase-validation/`
  - `.kiro/specs/legacy-baseline-regression-artifacts/`
  - `.kiro/specs/legacy-flow-composition-reporting/`
  - `.kiro/specs/public-demo-workflow/`
  - `.kiro/specs/release-review-report/`

## Opus Review Prompt

Please review this TraceMap Kiro spec for generating redacted legacy sample
evidence packs.

This is a spec review, not an implementation review. Do not edit files.

Focus on:

- Whether the pack artifact is clearly a redacted summary, not raw scan output.
- Whether `local-only`, `demo-safe`, `public-safe`, and `rejected` boundaries are
  concrete and safe.
- Whether public-safe packs preserve enough proof material: rule IDs, evidence
  tiers, coverage labels, limitations, extractor versions, safe counts, command
  provenance, and safe source labels.
- Whether input boundaries prevent committing local paths, raw remotes, private
  sample names, raw SQL, config values, connection strings, secrets, snippets,
  analyzer logs, raw facts, or private identifiers.
- Whether claim-level language avoids runtime behavior, vulnerability status,
  production usage, release approval, service reachability, SQL execution, and
  business impact.
- Whether the tasks are implementation-ready while remaining unchecked.
- Whether this duplicates or conflicts with `legacy-baseline-regression-artifacts`,
  `public-demo-workflow`, or `release-review-report`.

Return:

- Blocking issues.
- Medium+ or important non-blocking issues.
- Suggested concrete fixes.
- Missing tests or validation commands.
- Whether this is ready for implementation after fixes.

## Sonnet Review Prompt

Please review `.kiro/specs/legacy-sample-evidence-pack/` as an implementation
planner for the current TraceMap repository.

This is a spec review, not an implementation review. Do not edit files.

Check:

- Are the schema, command shape, storage locations, and validation gates
  concrete enough for an implementation PR?
- Are input readers and safety classifications well-scoped relative to existing
  legacy validation, baseline, public demo, and release-review artifacts?
- Are deterministic output, redaction, command provenance, Markdown escaping,
  and prohibited-claim tests specific enough?
- Are rule catalog entries and limitations required before emitting pack rows?
- Are there contradictions with repo privacy rules or current validation docs?
- Are tasks sliced into reviewable implementation work and left unchecked?
- Is any site implementation, scanner behavior change, reducer conclusion, raw
  artifact storage, or AI-based classification accidentally requested?

Return blockers first, then important refinements, optional follow-ups,
recommended first implementation PR, and risky assumptions with file/section
references.

## Qodo/Gemini Review Prompt

Review this spec for correctness, privacy, and maintainability risks.

Look for:

- Unsafe public artifacts or promotion gaps.
- Ambiguous claim levels.
- Missing rule IDs, evidence tiers, limitations, coverage labels, extractor
  versions, or command provenance.
- Raw scan output leakage through summaries, hashes, paths, logs, Markdown, or
  diagnostics.
- Overclaiming runtime behavior, security posture, production usage, business
  impact, or release readiness.
- Unstable JSON schema or nondeterministic ordering.
- Duplication with legacy baseline or public demo workflows.
- Tests that are too vague to catch leaks.

Please provide actionable findings with file/section references and suggested
fixes.
