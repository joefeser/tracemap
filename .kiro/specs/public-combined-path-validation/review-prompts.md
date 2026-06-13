# Public Combined Path Validation Review Prompts

Use these prompts to review the spec before implementation.

Spec files:

- `.kiro/specs/public-combined-path-validation/requirements.md`
- `.kiro/specs/public-combined-path-validation/design.md`
- `.kiro/specs/public-combined-path-validation/tasks.md`

## Opus Review Prompt

Review this TraceMap spec for public combined dependency path validation.

Focus on:

- Whether the spec proves the value of `tracemap paths` rather than just checking that files exist.
- Whether checked-in samples are the right source for deterministic assertions.
- Whether pinned OSS repos should be part of this script or remain a separate smoke.
- Whether the proposed assertions are strong enough: endpoint match, source transition, terminal surface, rule IDs, evidence tiers, and safe rendering.
- Whether the optional external repo support is safe for an open-source repository.
- Whether any requirement risks leaking private paths, private repo names, raw SQL, config values, or source snippets.
- Whether the default smoke is likely to be fast and reliable enough for PR review.
- Whether docs should include command output snippets or only commands and expected artifacts.
- Whether the sample fixture should require both `sql-query` and `package-config` paths before implementation.
- Whether any wording overclaims runtime behavior instead of static evidence.

Please return:

1. Blockers.
2. High-value changes before implementation.
3. Scope cuts if the validation slice is too broad.
4. Missing assertions.
5. Privacy or open-source hygiene risks.

## Sonnet Review Prompt

Review this spec as an implementation planner.

Focus on:

- The current script patterns in `scripts/`.
- The current sample fixtures under `samples/`.
- Whether `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet` are sufficient for endpoint-to-surface path assertions.
- The safest way to inspect `paths-report.json` from shell scripts.
- How to keep the script readable and deterministic.
- What sample changes, if any, are required.
- Whether optional external repo support should be implemented now or deferred.
- Whether docs and `.gitignore` need changes.
- Which validation commands should be required before PR.

Please return a concrete implementation plan, risky assumptions, and recommended first PR slice.

## Qodo/Gemini Review Prompt

Review this spec for correctness, maintainability, and privacy risks.

Look for:

- Weak smoke assertions that could pass while path analysis is broken.
- Network-dependent behavior in the default path.
- Hardcoded private paths or names.
- Unsafe report rendering expectations.
- Generated artifacts that might be committed.
- Overly brittle assertions against Markdown formatting.
- Missing reduced-coverage caveats.
- Missing toolchain prerequisite documentation.
- Ambiguous ownership between this new smoke and existing endpoint/OSS smoke scripts.

Please provide actionable findings with file/section references and suggested fixes.
