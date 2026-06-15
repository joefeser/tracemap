# Legacy Baseline Regression Artifacts Review Prompts

## Opus Review Prompt

Review the Kiro spec in
`.kiro/specs/legacy-baseline-regression-artifacts/`.

This is a spec review, not an implementation review. Do not edit files.

Focus on:

- Whether the spec preserves TraceMap's deterministic evidence model: no
  conclusion without evidence, no evidence without a rule ID, no rule without
  documented limitations, and partial analysis labeled partial.
- Whether redacted baseline manifests are specific enough to preserve original
  parser snapshots without storing raw scan outputs.
- Whether public-safe versus local-only artifact boundaries are clear and safe
  for private legacy samples.
- Whether count summaries, coverage labels, rule coverage snapshots, fact
  coverage snapshots, extractor versions, known gaps, and limitations are enough
  to compare future improvements.
- Whether regression comparisons avoid business-impact, runtime reachability,
  safety, production-usage, or reducer claims.
- Whether safety checks cover local paths, sample identities, raw remotes, raw
  SQL, config values, connection strings, endpoint addresses, secrets, raw
  analyzer output, and source snippets.
- Whether tasks are implementation-ready while remaining unchecked.

Please identify merge-blocking or Medium+ issues separately from nice-to-have
polish.

## Sonnet Review Prompt

Review `.kiro/specs/legacy-baseline-regression-artifacts/` for implementation
readiness.

This is a spec review, not an implementation review. Do not edit files.

Check:

- Are the manifest schema, comparison schema, safety classification, and
  storage boundaries concrete enough for an implementation PR?
- Are tests specific enough to prove deterministic output, redaction, coverage
  snapshots, and comparison behavior?
- Are there contradictions with current TraceMap artifact behavior, legacy
  validation conventions, or repo privacy rules?
- Are tasks sliced clearly and left unchecked?
- Is any scanner implementation, site work, private sample detail, raw artifact
  storage, or AI-based classification accidentally requested?

Return blockers first, then important non-blocking issues, then suggested fixes.
