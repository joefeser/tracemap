# Legacy Build Environment Diagnostics Review Prompts

## Opus Review Prompt

Review the Kiro spec in `.kiro/specs/legacy-build-environment-diagnostics/`.

This is a spec review, not an implementation review. Do not edit files.

Focus on:

- Whether the requirements preserve TraceMap's deterministic evidence model:
  no conclusion without evidence, no evidence without a rule ID, and no rule
  without documented limitations.
- Whether diagnostics are implementable as machine-readable scan facts/gaps and
  report sections rather than runtime claims.
- Whether the spec safely handles old target frameworks, SDK/runtime/MSBuild
  requirements, unsupported project formats, NuGet/restore blockers, Web
  Application project quirks, and generated/designer-file gaps.
- Whether guidance is useful but conservative and avoids telling users to
  install obsolete or unsafe runtimes automatically.
- Whether privacy/redaction requirements cover local paths, sample repo names,
  remotes, package sources, config values, raw SQL, secrets, raw command output,
  and source snippets.
- Whether tasks are implementation-ready while remaining unchecked.

Please identify merge-blocking or Medium+ issues separately from nice-to-have
polish.

## Sonnet Review Prompt

Review `.kiro/specs/legacy-build-environment-diagnostics/` for implementation
readiness.

This is a spec review, not an implementation review. Do not edit files.

Check:

- Are the fact model, diagnostic codes, rule IDs, and report/manifest/SQLite
  expectations specific enough for a .NET implementation PR?
- Are tests concrete enough to prove deterministic behavior, reduced coverage,
  fallback scanning, and redaction?
- Are there contradictions with current TraceMap schemas or existing scan
  behavior?
- Are tasks sliced clearly and left unchecked?
- Is any scanner implementation, site work, private sample detail, or AI-based
  classification accidentally requested?

Return blockers first, then important non-blocking issues, then suggested fixes.
